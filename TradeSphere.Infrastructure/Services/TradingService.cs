using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
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
                .Take(1000)
                .ToListAsync();

            return trades
                .Where(ShouldShowTradeInReport)
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
                    ErrorReason = t.ErrorReason,
                    BrokerTicket = BuildBrokerTicket(t),
                    ActivityType = BuildActivityType(t)
                })
                .ToList();
        }

        private async Task SyncMt5ClosedDealsAsync(int userId)
        {
            var mt5EntryTrades = await _context.Trades
                .Include(t => t.UserStrategy)
                    .ThenInclude(us => us.Mt5Account)
                .Where(t =>
                    t.UserId == userId &&
                    (t.Status == "Filled" || t.Status == "Reconciled") &&
                    t.UserStrategy != null &&
                    t.UserStrategy.ExecutionProvider == "MT5" &&
                    t.UserStrategy.Mt5Account != null &&
                    t.ExternalOrderId != null &&
                    t.ExternalOrderId.Contains("MT5:") &&
                    (t.ExternalOrderId.StartsWith("Fib-Entry-") || t.ExternalOrderId.StartsWith("Entry-")))
                .OrderByDescending(t => t.CreatedAt)
                .Take(250)
                .ToListAsync();

            if (mt5EntryTrades.Count == 0)
            {
                return;
            }

            var mt5RiskAdjustments = await _context.Trades
                .Where(t =>
                    t.UserId == userId &&
                    t.Status == "Modified" &&
                    t.ExternalOrderId != null &&
                    t.ExternalOrderId.Contains("MT5-Risk") &&
                    t.ExternalOrderId.Contains("MT5:"))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

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
                    var closeDeals = FindMt5CloseDealsForEntry(dealsResult.Deals, trade, mt5Ticket, createdSec);
                    if (closeDeals.Count == 0)
                    {
                        continue;
                    }

                    trade.Pnl = closeDeals.Sum(d => d.Profit + d.Commission + d.Swap);
                    trade.Status = "Closed";
                    var closeDeal = closeDeals
                        .Where(d => d.Price > 0m)
                        .OrderBy(d => d.Time)
                        .LastOrDefault();
                    var closeTime = closeDeals.Max(d => d.Time);
                    trade.ExecutedAt = DateTimeOffset.FromUnixTimeSeconds(closeTime).UtcDateTime;
                    trade.ErrorReason = BuildMt5CloseReason(trade, closeDeal, mt5RiskAdjustments);
                    trade.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
        }

        private static List<Mt5BridgeDealDto> FindMt5CloseDealsForEntry(
            List<Mt5BridgeDealDto> deals,
            TradeSphere.Domain.Entities.Trade trade,
            long mt5Ticket,
            long createdSec)
        {
            var closeDeals = deals
                .Where(d =>
                    d.Time >= createdSec &&
                    IsMt5ClosingDeal(d) &&
                    string.Equals(d.Symbol, trade.Symbol, StringComparison.OrdinalIgnoreCase) &&
                    d.Position_Id == mt5Ticket)
                .OrderBy(d => d.Time)
                .ToList();

            if (closeDeals.Count > 0)
            {
                return closeDeals;
            }

            // Fallback for brokers that return the entry ticket as order/deal id instead of position id.
            return deals
                .Where(d =>
                    d.Time >= createdSec &&
                    IsMt5ClosingDeal(d) &&
                    string.Equals(d.Symbol, trade.Symbol, StringComparison.OrdinalIgnoreCase) &&
                    (d.Ticket == mt5Ticket || d.Order == mt5Ticket))
                .OrderBy(d => d.Time)
                .ToList();
        }

        private static bool IsMt5ClosingDeal(Mt5BridgeDealDto deal)
        {
            // MT5 deal entry: 0 = in/open, 1 = out/close, 2 = in-out/reversal.
            return deal.Entry != 0;
        }

        private static long ExtractMt5Ticket(string? externalOrderId)
        {
            if (string.IsNullOrWhiteSpace(externalOrderId))
            {
                return 0;
            }

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

        private static string BuildMt5CloseReason(
            TradeSphere.Domain.Entities.Trade trade,
            Mt5BridgeDealDto? closeDeal,
            List<TradeSphere.Domain.Entities.Trade> riskAdjustments)
        {
            var closePrice = closeDeal?.Price ?? 0m;
            if (closePrice <= 0m)
            {
                return "Closed on MT5 | Reason: Manual Close";
            }

            var closeText = $"Closed on MT5 at {closePrice:0.#####}";
            var reason = ClassifyMt5CloseReason(trade, closePrice, riskAdjustments);
            return $"{closeText} | Reason: {reason}";
        }

        private static string ClassifyMt5CloseReason(
            TradeSphere.Domain.Entities.Trade trade,
            decimal closePrice,
            List<TradeSphere.Domain.Entities.Trade> riskAdjustments)
        {
            if (trade.Price is not > 0m)
            {
                return "Manual Close";
            }

            var entryPrice = trade.Price.Value;
            var (originalStopLoss, takeProfit) = ParseRiskLevels(trade.ExternalOrderId);
            var latestRiskAdjustment = riskAdjustments
                .Where(t =>
                    t.UserStrategyId == trade.UserStrategyId &&
                    string.Equals(t.Symbol, trade.Symbol, StringComparison.OrdinalIgnoreCase) &&
                    ExtractMt5Ticket(t.ExternalOrderId) == ExtractMt5Ticket(trade.ExternalOrderId) &&
                    t.CreatedAt >= trade.CreatedAt)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault();

            var (modifiedStopLoss, _) = ParseRiskLevels(latestRiskAdjustment?.ExternalOrderId);
            var riskReference = originalStopLoss > 0m
                ? Math.Abs(entryPrice - originalStopLoss)
                : Math.Abs(entryPrice - closePrice);
            var tolerance = Math.Max(0.01m, riskReference * 0.03m);

            if (takeProfit > 0m && ArePricesNear(closePrice, takeProfit, tolerance))
            {
                return "Take-Profit";
            }

            if (modifiedStopLoss > 0m && ArePricesNear(closePrice, modifiedStopLoss, tolerance))
            {
                var riskReason = ExtractRiskReason(latestRiskAdjustment?.ExternalOrderId);
                if (riskReason.Contains("BreakEven", StringComparison.OrdinalIgnoreCase) &&
                    ArePricesNear(modifiedStopLoss, entryPrice, Math.Max(tolerance, 0.01m)))
                {
                    return "BreakEven";
                }

                if (riskReason.Contains("TrailingStop", StringComparison.OrdinalIgnoreCase))
                {
                    return "Trailing SL";
                }

                if (riskReason.Contains("BreakEven", StringComparison.OrdinalIgnoreCase))
                {
                    return "BreakEven";
                }

                return "Stop-Loss";
            }

            if (originalStopLoss > 0m && ArePricesNear(closePrice, originalStopLoss, tolerance))
            {
                return "Stop-Loss";
            }

            return "Manual Close";
        }

        private static bool ArePricesNear(decimal price, decimal level, decimal tolerance)
        {
            return Math.Abs(price - level) <= tolerance;
        }

        private static string ExtractRiskReason(string? externalOrderId)
        {
            if (string.IsNullOrWhiteSpace(externalOrderId))
            {
                return string.Empty;
            }

            foreach (var part in externalOrderId.Split('|'))
            {
                if (part.StartsWith("Reason:", StringComparison.OrdinalIgnoreCase))
                {
                    return part[7..];
                }
            }

            return string.Empty;
        }

        private static (decimal stopLoss, decimal takeProfit) ParseRiskLevels(string? externalOrderId)
        {
            var stopLoss = 0m;
            var takeProfit = 0m;
            if (string.IsNullOrWhiteSpace(externalOrderId))
            {
                return (stopLoss, takeProfit);
            }

            foreach (var part in externalOrderId.Split('|'))
            {
                if (part.StartsWith("SL:", StringComparison.OrdinalIgnoreCase))
                {
                    decimal.TryParse(part[3..], NumberStyles.Number, CultureInfo.InvariantCulture, out stopLoss);
                }
                else if (part.StartsWith("TP:", StringComparison.OrdinalIgnoreCase))
                {
                    decimal.TryParse(part[3..], NumberStyles.Number, CultureInfo.InvariantCulture, out takeProfit);
                }
            }

            return (stopLoss, takeProfit);
        }

        private static bool IsMt5EntryTrade(string? externalOrderId)
        {
            return externalOrderId != null &&
                   (externalOrderId.StartsWith("Fib-Entry-", StringComparison.OrdinalIgnoreCase) ||
                    externalOrderId.StartsWith("Entry-", StringComparison.OrdinalIgnoreCase));
        }

        private static string? BuildBrokerTicket(TradeSphere.Domain.Entities.Trade trade)
        {
            if (trade.UserStrategy?.ExecutionProvider == "MT5")
            {
                var ticket = ExtractMt5Ticket(trade.ExternalOrderId);
                return ticket > 0 ? ticket.ToString(CultureInfo.InvariantCulture) : null;
            }

            return string.IsNullOrWhiteSpace(trade.ExternalOrderId) ? null : trade.ExternalOrderId;
        }

        private static string BuildActivityType(TradeSphere.Domain.Entities.Trade trade)
        {
            if (string.Equals(trade.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                return "Failed";
            }

            if (IsMt5RiskAdjustmentTrade(trade))
            {
                return "Risk Update";
            }

            if (IsMt5ExitAuditTrade(trade))
            {
                return "Exit Signal";
            }

            if (string.Equals(trade.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            {
                return "Closed";
            }

            if (IsMt5EntryTrade(trade.ExternalOrderId))
            {
                return "Entry";
            }

            if (string.Equals(trade.Status, "Modified", StringComparison.OrdinalIgnoreCase))
            {
                return "Modified";
            }

            return trade.Status;
        }

        private static string NormalizeTradeStatus(TradeSphere.Domain.Entities.Trade trade)
        {
            return IsMt5AlreadyClosedNotice(trade) ? "Reconciled" : trade.Status;
        }

        private static bool ShouldShowTradeInReport(TradeSphere.Domain.Entities.Trade trade)
        {
            if (IsMt5AlreadyClosedNotice(trade))
            {
                return false;
            }

            if (IsMt5ExitAuditTrade(trade))
            {
                return false;
            }

            if (IsMt5RiskAdjustmentTrade(trade) &&
                !string.Equals(trade.Status, "Modified", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool IsMt5AlreadyClosedNotice(TradeSphere.Domain.Entities.Trade trade)
        {
            return (string.Equals(trade.Status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trade.Status, "Reconciled", StringComparison.OrdinalIgnoreCase)) &&
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

        private static bool IsMt5RiskAdjustmentTrade(TradeSphere.Domain.Entities.Trade trade)
        {
            return trade.UserStrategy?.ExecutionProvider == "MT5" &&
                   trade.ExternalOrderId != null &&
                   trade.ExternalOrderId.StartsWith("MT5-Risk", StringComparison.OrdinalIgnoreCase);
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
