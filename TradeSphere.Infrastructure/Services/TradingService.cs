using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;
using TradeSphere.Infrastructure.Persistence;
using TradeSphere.Infrastructure.Security;

namespace TradeSphere.Infrastructure.Services
{
    public class TradingService : ITradingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDeltaExchangeClient _deltaClient;
        private readonly IMt5BridgeClient _mt5BridgeClient;
        private readonly ICoinDcxClient _coinDcxClient;

        public TradingService(ApplicationDbContext context, IDeltaExchangeClient deltaClient, IMt5BridgeClient mt5BridgeClient, ICoinDcxClient coinDcxClient)
        {
            _context = context;
            _deltaClient = deltaClient;
            _mt5BridgeClient = mt5BridgeClient;
            _coinDcxClient = coinDcxClient;
        }

        public async Task<List<TradeDto>> GetTradesAsync(int userId)
        {
            await SyncMt5ClosedDealsAsync(userId);

            var trades = await _context.Trades
                .Include(t => t.Exchange)
                .Include(t => t.UserStrategy)
                    .ThenInclude(us => us.Strategy)
                .Include(t => t.UserStrategy)
                    .ThenInclude(us => us.Mt5Account)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(100)
                .ToListAsync();

            return trades
                .Select(t => new TradeDto
                {
                    Id = t.Id,
                    StrategyName = t.UserStrategy != null ? t.UserStrategy.Strategy.Name : "",
                    ExchangeName = t.UserStrategy != null && t.UserStrategy.ExecutionProvider == "MT5" ? "MT5" : t.Exchange.Name,
                    ExecutionProvider = t.UserStrategy != null ? t.UserStrategy.ExecutionProvider : "Delta",
                    ExecutionAccount = t.UserStrategy != null && t.UserStrategy.ExecutionProvider == "MT5"
                        ? (t.UserStrategy.Mt5Account != null ? t.UserStrategy.Mt5Account.Name : "MT5 Account")
                        : t.Exchange.Name,
                    Symbol = t.Symbol,
                    Side = t.Side,
                    OrderType = t.OrderType,
                    Price = t.Price,
                    Quantity = t.Quantity,
                    Status = NormalizeTradeStatus(t),
                    ExecutedAt = t.ExecutedAt,
                    CreatedAt = t.CreatedAt,
                    Pnl = IsMt5ExitAuditTrade(t) ? 0m : t.Pnl,
                    ExternalOrderId = t.ExternalOrderId,
                    ErrorReason = t.ErrorReason
                })
                .ToList();
        }

        private async Task SyncMt5ClosedDealsAsync(int userId)
        {
            var mt5CandidateTrades = await _context.Trades
                .Include(t => t.UserStrategy)
                    .ThenInclude(us => us.Mt5Account)
                .Where(t =>
                    t.UserId == userId &&
                    t.Status == "Filled" &&
                    t.UserStrategy != null &&
                    t.UserStrategy.ExecutionProvider == "MT5" &&
                    t.UserStrategy.Mt5Account != null &&
                    t.ExternalOrderId != null &&
                    t.ExternalOrderId.Contains("MT5:"))
                .OrderByDescending(t => t.CreatedAt)
                .Take(50)
                .ToListAsync();

            var mt5EntryTrades = mt5CandidateTrades
                .Where(t => IsMt5EntryTrade(t.ExternalOrderId))
                .ToList();

            if (mt5EntryTrades.Count == 0)
            {
                return;
            }

            foreach (var accountGroup in mt5EntryTrades.GroupBy(t => t.UserStrategy!.Mt5Account!))
            {
                var account = accountGroup.Key;
                var password = EncryptionHelper.Decrypt(account.EncryptedPassword);
                var earliest = accountGroup.Min(t => t.CreatedAt).AddHours(-2);
                var startSec = new DateTimeOffset(DateTime.SpecifyKind(earliest, DateTimeKind.Utc)).ToUnixTimeSeconds();
                var endSec = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();

                var dealsResult = await _mt5BridgeClient.GetHistoryDealsAsync(new Mt5BridgeDealsRequestDto
                {
                    Login = account.Login,
                    Server = account.Server,
                    Password = password,
                    StartTime = startSec,
                    EndTime = endSec
                });

                if (!dealsResult.Success)
                {
                    foreach (var trade in accountGroup)
                    {
                        trade.ErrorReason = string.IsNullOrWhiteSpace(trade.ErrorReason)
                            ? $"MT5 history sync failed: {dealsResult.Message}"
                            : trade.ErrorReason;
                    }
                    continue;
                }

                foreach (var trade in accountGroup)
                {
                    var mt5Ticket = ExtractMt5Ticket(trade.ExternalOrderId);
                    if (mt5Ticket <= 0)
                    {
                        continue;
                    }

                    var createdSec = new DateTimeOffset(DateTime.SpecifyKind(trade.CreatedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
                    var matchingDeals = dealsResult.Deals
                        .Where(d =>
                            d.Time >= createdSec &&
                            string.Equals(d.Symbol, trade.Symbol, StringComparison.OrdinalIgnoreCase) &&
                            (d.Ticket == mt5Ticket || d.Order == mt5Ticket || d.Position_Id == mt5Ticket))
                        .ToList();

                    var realizedDeals = matchingDeals
                        .Where(d => d.Profit != 0m || d.Commission != 0m || d.Swap != 0m)
                        .ToList();

                    if (realizedDeals.Count == 0)
                    {
                        continue;
                    }

                    trade.Pnl = realizedDeals.Sum(d => d.Profit + d.Commission + d.Swap);
                    trade.Status = "Closed";
                    trade.ExecutedAt ??= DateTimeOffset.FromUnixTimeSeconds(realizedDeals.Max(d => d.Time)).UtcDateTime;
                    trade.ErrorReason = realizedDeals.Any(d => d.Price > 0m)
                        ? $"Closed on MT5 at {realizedDeals.Last().Price:0.##}"
                        : "Closed on MT5";
                    trade.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
        }

        private static long ExtractMt5Ticket(string externalOrderId)
        {
            var marker = "MT5:";
            var index = externalOrderId.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return 0;
            }

            var start = index + marker.Length;
            var digits = new string(externalOrderId[start..].TakeWhile(char.IsDigit).ToArray());
            return long.TryParse(digits, out var ticket) ? ticket : 0;
        }

        private static bool IsMt5EntryTrade(string? externalOrderId)
        {
            return externalOrderId != null &&
                   (externalOrderId.StartsWith("Fib-Entry-", StringComparison.OrdinalIgnoreCase) ||
                    externalOrderId.StartsWith("Entry-", StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeTradeStatus(TradeSphere.Domain.Entities.Trade trade)
        {
            return IsMt5AlreadyClosedNotice(trade) ? "Reconciled" : trade.Status;
        }

        private static bool IsMt5AlreadyClosedNotice(TradeSphere.Domain.Entities.Trade trade)
        {
            return string.Equals(trade.Status, "Failed", StringComparison.OrdinalIgnoreCase) &&
                   trade.UserStrategy?.ExecutionProvider == "MT5" &&
                   trade.ErrorReason != null &&
                   trade.ErrorReason.Contains("No matching open MT5 position found", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMt5ExitAuditTrade(TradeSphere.Domain.Entities.Trade trade)
        {
            return trade.UserStrategy?.ExecutionProvider == "MT5" &&
                   trade.ExternalOrderId != null &&
                   (trade.ExternalOrderId.StartsWith("Fib-Exit-", StringComparison.OrdinalIgnoreCase) ||
                    trade.ExternalOrderId.StartsWith("Exit-", StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<PositionDto>> GetPositionsAsync(int userId)
        {
            var positions = new List<PositionDto>();

            var userExchanges = await _context.UserExchanges
                .Include(ue => ue.Exchange)
                .Where(ue => ue.UserId == userId && ue.Status == "Active" && (ue.Exchange.Name.Contains("Delta Exchange") || ue.Exchange.Name.Contains("CoinDCX")))
                .ToListAsync();

            foreach (var userExchange in userExchanges)
            {
                var apiKey = EncryptionHelper.Decrypt(userExchange.ApiKey);
                var apiSecret = EncryptionHelper.Decrypt(userExchange.ApiSecret);
                var exchangePositions = userExchange.Exchange.Name.Contains("CoinDCX", StringComparison.OrdinalIgnoreCase)
                    ? await _coinDcxClient.GetPositionsAsync(apiKey, apiSecret, userExchange.Exchange?.BaseUrl)
                    : await _deltaClient.GetPositionsAsync(apiKey, apiSecret, userExchange.Exchange?.BaseUrl);
                foreach (var position in exchangePositions)
                {
                    position.ExchangeName = $"{userExchange.Exchange.Name} - {userExchange.Name}";
                    positions.Add(position);
                }
            }

            var mt5Accounts = await _context.Mt5Accounts
                .Where(a => a.UserId == userId && a.TradingEnabled)
                .ToListAsync();

            foreach (var account in mt5Accounts)
            {
                var result = await _mt5BridgeClient.GetPositionsAsync(new Mt5BridgePositionsRequestDto
                {
                    Login = account.Login,
                    Server = account.Server,
                    Password = EncryptionHelper.Decrypt(account.EncryptedPassword)
                });

                if (!result.Success)
                {
                    account.LastError = result.Message;
                    account.LastSyncedAt = DateTime.UtcNow;
                    continue;
                }

                foreach (var position in result.Positions)
                {
                    positions.Add(new PositionDto
                    {
                        ExchangeName = $"MT5 - {account.Name}",
                        Symbol = position.Symbol,
                        Side = position.Type == 0 ? "Buy" : "Sell",
                        Size = position.Volume,
                        EntryPrice = position.Price_Open,
                        MarkPrice = position.Price_Current,
                        UnrealizedPnl = position.Profit + position.Swap,
                        RealizedPnl = 0m,
                        Margin = 0m,
                        Status = "Open",
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                account.LastError = null;
                account.LastSyncedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return positions;
        }

        public async Task<TradingOverviewDto> GetOverviewAsync(int userId)
        {
            return new TradingOverviewDto
            {
                Trades = await GetTradesAsync(userId),
                Positions = await GetPositionsAsync(userId)
            };
        }

        public async Task DeleteAllTradesAsync(int userId)
        {
            var trades = await _context.Trades
                .Where(t => t.UserId == userId)
                .ToListAsync();

            if (trades.Count == 0)
            {
                return;
            }

            _context.Trades.RemoveRange(trades);
            await _context.SaveChangesAsync();
        }
    }
}
