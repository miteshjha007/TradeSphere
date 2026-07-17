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
            await SyncMt5ManualClosedDealsAsync(userId);

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
                    StrategyName = BuildStrategyName(t),
                    ExchangeName = BuildExchangeName(t),
                    ExecutionProvider = BuildExecutionProvider(t),
                    ExecutionAccount = t.UserStrategy != null && t.UserStrategy.ExecutionProvider == "MT5"
                        ? (t.UserStrategy.Mt5Account != null ? t.UserStrategy.Mt5Account.Name : "MT5 Account")
                        : IsMt5ManualTrade(t)
                        ? ExtractManualMt5AccountName(t.ExternalOrderId)
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
                    ActivityType = BuildActivityType(t),
                    CanResumeAutoRisk = CanResumeMt5AutoRisk(t)
                })
                .ToList();
        }


        private async Task SyncMt5ManualClosedDealsAsync(int userId)
        {
            var mt5Accounts = await _context.Mt5Accounts
                .Where(a =>
                    a.UserId == userId &&
                    a.TradingEnabled &&
                    (a.Name.Contains("FundingPips") ||
                     a.Server.Contains("FundingPips") ||
                     (a.AccountType != "Demo" &&
                      !a.Name.Contains("Demo") &&
                      !a.Server.Contains("MetaQuotes"))))
                .ToListAsync();

            if (mt5Accounts.Count == 0)
            {
                return;
            }

            var existingManualExternalIds = await _context.Trades
                .Where(t =>
                    t.UserId == userId &&
                    t.ExternalOrderId != null &&
                    t.ExternalOrderId.StartsWith("MT5-Manual"))
                .Select(t => t.ExternalOrderId)
                .ToListAsync();

            var existingManualKeys = new HashSet<string>(existingManualExternalIds, StringComparer.OrdinalIgnoreCase);

            var appMt5EntryTrades = await _context.Trades
                .Include(t => t.UserStrategy)
                .Where(t =>
                    t.UserId == userId &&
                    t.ExternalOrderId != null &&
                    t.ExternalOrderId.Contains("MT5:") &&
                    (t.ExternalOrderId.StartsWith("Fib-Entry-") || t.ExternalOrderId.StartsWith("Entry-")))
                .ToListAsync();

            var mt5RiskAdjustments = await _context.Trades
                .Where(t =>
                    t.UserId == userId &&
                    t.Status == "Modified" &&
                    t.ExternalOrderId != null &&
                    t.ExternalOrderId.Contains("MT5-Risk") &&
                    t.ExternalOrderId.Contains("MT5:"))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            var mt5ExchangeId = await ResolveMt5ReportExchangeIdAsync();
            var syncStart = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
            var syncEnd = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();

            foreach (var account in mt5Accounts)
            {
                var dealsResult = await _mt5BridgeClient.GetHistoryDealsAsync(new Mt5BridgeDealsRequestDto
                {
                    Login = account.Login,
                    Server = account.Server,
                    Password = EncryptionHelper.Decrypt(account.EncryptedPassword),
                    StartTime = syncStart,
                    EndTime = syncEnd
                });

                if (!dealsResult.Success)
                {
                    account.LastError = $"MT5 manual trade sync failed: {dealsResult.Message}";
                    account.LastSyncedAt = DateTime.UtcNow;
                    continue;
                }

                var manualGroups = dealsResult.Deals
                    .Where(d => d.Time >= syncStart && !string.IsNullOrWhiteSpace(d.Symbol))
                    .GroupBy(ResolveMt5DealPositionKey)
                    .Where(g => g.Key > 0)
                    .Select(g => new
                    {
                        PositionKey = g.Key,
                        Deals = g.OrderBy(d => d.Time).ToList(),
                        ClosingDeals = g.Where(IsMt5ClosingDeal).OrderBy(d => d.Time).ToList()
                    })
                    .Where(g => g.ClosingDeals.Count > 0)
                    .ToList();

                foreach (var group in manualGroups)
                {
                    var closeDeal = group.ClosingDeals.Last();
                    var entryDeal = group.Deals.FirstOrDefault(d => d.Entry == 0) ?? group.Deals.First();
                    var pnl = group.Deals.Sum(d => d.Profit + d.Commission + d.Swap);
                    var quantity = Math.Max(entryDeal.Volume, group.ClosingDeals.Sum(d => d.Volume));
                    var createdAt = DateTimeOffset.FromUnixTimeSeconds(closeDeal.Time).UtcDateTime;

                    var matchingAppTrade = appMt5EntryTrades.FirstOrDefault(t => DoesMt5TradeMatchDealGroup(t, group.PositionKey, group.Deals));
                    if (matchingAppTrade != null)
                    {
                        if (!string.Equals(matchingAppTrade.Status, "Closed", StringComparison.OrdinalIgnoreCase) ||
                            Math.Abs(matchingAppTrade.Pnl - pnl) > 0.0001m)
                        {
                            matchingAppTrade.Status = "Closed";
                            matchingAppTrade.Pnl = pnl;
                            matchingAppTrade.ExecutedAt = createdAt;
                            matchingAppTrade.ErrorReason = BuildMt5CloseReason(matchingAppTrade, closeDeal, mt5RiskAdjustments);
                            matchingAppTrade.UpdatedAt = DateTime.UtcNow;
                        }

                        continue;
                    }

                    var uniqueKey = BuildManualMt5ExternalId(account, group.PositionKey, closeDeal);
                    if (existingManualKeys.Contains(uniqueKey))
                    {
                        continue;
                    }

                    _context.Trades.Add(new TradeSphere.Domain.Entities.Trade
                    {
                        UserId = userId,
                        UserStrategyId = null,
                        ExchangeId = mt5ExchangeId,
                        Symbol = entryDeal.Symbol,
                        Side = ResolveMt5DealSide(entryDeal),
                        OrderType = "Manual MT5",
                        Price = entryDeal.Price > 0m ? entryDeal.Price : closeDeal.Price,
                        Quantity = quantity,
                        Status = "Closed",
                        ExecutedAt = createdAt,
                        CreatedAt = createdAt,
                        UpdatedAt = DateTime.UtcNow,
                        Pnl = pnl,
                        ExternalOrderId = uniqueKey,
                        ErrorReason = $"Manual MT5 trade closed at {closeDeal.Price:0.#####}",
                        BrokerResponse = $"Manual sync from MT5 history. Deal={closeDeal.Ticket}; Position={group.PositionKey}; Account={account.Name}"
                    });

                    existingManualKeys.Add(uniqueKey);
                }

                account.LastError = null;
                account.LastSyncedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
        private async Task SyncMt5ClosedDealsAsync(int userId)
        {
            var mt5EntryTrades = await _context.Trades
                .Include(t => t.UserStrategy)
                    .ThenInclude(us => us.Mt5Account)
                .Where(t =>
                    t.UserId == userId &&
                    (t.Status == "Filled" || t.Status == "Reconciled") &&
                    t.ExternalOrderId != null &&
                    t.ExternalOrderId.Contains("MT5:") &&
                    (t.ExternalOrderId.StartsWith("Fib-Entry-") || t.ExternalOrderId.StartsWith("Entry-")) &&
                    (t.UserStrategy == null || t.UserStrategy.ExecutionProvider == "MT5"))
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

            foreach (var accountGroup in mt5EntryTrades
                .Where(t => t.UserStrategy?.Mt5Account != null)
                .GroupBy(t => t.UserStrategy!.Mt5Account!))
            {
                await SyncMt5ClosedTradesForAccountAsync(accountGroup.Key, accountGroup.ToList(), mt5RiskAdjustments);
            }

            var orphanMt5Trades = mt5EntryTrades
                .Where(t => t.UserStrategy?.Mt5Account == null)
                .ToList();

            if (orphanMt5Trades.Count > 0)
            {
                var activeMt5Accounts = await _context.Mt5Accounts
                    .Where(a => a.UserId == userId && a.TradingEnabled)
                    .OrderByDescending(a => a.UpdatedAt)
                    .ToListAsync();

                foreach (var account in activeMt5Accounts)
                {
                    var remainingOrphans = orphanMt5Trades
                        .Where(t => t.Status == "Filled" || t.Status == "Reconciled")
                        .ToList();

                    if (remainingOrphans.Count == 0)
                    {
                        break;
                    }

                    await SyncMt5ClosedTradesForAccountAsync(account, remainingOrphans, mt5RiskAdjustments);
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task SyncMt5ClosedTradesForAccountAsync(
            TradeSphere.Domain.Entities.Mt5Account account,
            List<TradeSphere.Domain.Entities.Trade> trades,
            List<TradeSphere.Domain.Entities.Trade> mt5RiskAdjustments)
        {
            var password = EncryptionHelper.Decrypt(account.EncryptedPassword);
            var earliest = trades.Min(t => t.CreatedAt).AddHours(-2);
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
                foreach (var trade in trades)
                {
                    trade.ErrorReason = string.IsNullOrWhiteSpace(trade.ErrorReason)
                        ? $"MT5 history sync failed: {dealsResult.Message}"
                        : trade.ErrorReason;
                }
                return;
            }

            var usedCloseDealTickets = new HashSet<long>();
            foreach (var trade in trades.OrderBy(t => t.CreatedAt))
            {
                var mt5Ticket = ExtractMt5Ticket(trade.ExternalOrderId);
                if (mt5Ticket <= 0)
                {
                    continue;
                }

                var createdSec = new DateTimeOffset(DateTime.SpecifyKind(trade.CreatedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
                var closeDeals = FindMt5CloseDealsForEntry(dealsResult.Deals, trade, mt5Ticket, createdSec, usedCloseDealTickets);
                if (closeDeals.Count == 0)
                {
                    continue;
                }

                var positionDeals = FindMt5PositionDealsForEntry(dealsResult.Deals, trade, mt5Ticket, createdSec);
                trade.Pnl = positionDeals.Count > 0
                    ? positionDeals.Sum(d => d.Profit + d.Commission + d.Swap)
                    : closeDeals.Sum(d => d.Profit + d.Commission + d.Swap);
                trade.Status = "Closed";
                var closeDeal = closeDeals
                    .Where(d => d.Price > 0m)
                    .OrderBy(d => d.Time)
                    .LastOrDefault();
                var closeTime = closeDeals.Max(d => d.Time);
                trade.ExecutedAt = DateTimeOffset.FromUnixTimeSeconds(closeTime).UtcDateTime;
                trade.ErrorReason = BuildMt5CloseReason(trade, closeDeal, mt5RiskAdjustments);
                trade.UpdatedAt = DateTime.UtcNow;
                foreach (var closeDealTicket in closeDeals.Select(d => d.Ticket).Where(t => t > 0))
                {
                    usedCloseDealTickets.Add(closeDealTicket);
                }
            }
        }
        private async Task<int> ResolveMt5ReportExchangeIdAsync()
        {
            var exchange = await _context.Exchanges
                .OrderBy(e => e.Id)
                .FirstOrDefaultAsync();

            if (exchange == null)
            {
                throw new InvalidOperationException("At least one exchange is required before MT5 manual trades can be reported.");
            }

            return exchange.Id;
        }

        private static long ResolveMt5DealPositionKey(Mt5BridgeDealDto deal)
        {
            if (deal.Position_Id > 0)
            {
                return deal.Position_Id;
            }

            if (deal.Order > 0)
            {
                return deal.Order;
            }

            return deal.Ticket;
        }

        private static string ResolveMt5DealSide(Mt5BridgeDealDto entryDeal)
        {
            // MT5 deal type: 0 = buy, 1 = sell.
            return entryDeal.Type == 1 ? "Sell" : "Buy";
        }

        private static string BuildManualMt5ExternalId(TradeSphere.Domain.Entities.Mt5Account account, long positionKey, Mt5BridgeDealDto closeDeal)
        {
            var manualTicket = $"MANUAL-{account.Id}-{positionKey}";
            return $"MT5-Manual|Ticket:{manualTicket}|Position:{positionKey}|Deal:{closeDeal.Ticket}|Account:{account.Name}|MT5:{positionKey}";
        }

        private static bool DoesMt5TradeMatchDealGroup(TradeSphere.Domain.Entities.Trade trade, long positionKey, List<Mt5BridgeDealDto> deals)
        {
            var mt5Ticket = ExtractMt5Ticket(trade.ExternalOrderId);
            if (mt5Ticket <= 0)
            {
                return false;
            }

            return mt5Ticket == positionKey || deals.Any(deal => IsMt5DealRelatedToTicket(deal, mt5Ticket));
        }

        private static string BuildStrategyName(TradeSphere.Domain.Entities.Trade trade)
        {
            if (trade.UserStrategy != null)
            {
                return trade.UserStrategy.Strategy.Name;
            }

            return IsMt5ManualTrade(trade) ? "Manual MT5 Trade" : string.Empty;
        }

        private static string BuildExchangeName(TradeSphere.Domain.Entities.Trade trade)
        {
            if (trade.UserStrategy != null && trade.UserStrategy.ExecutionProvider == "MT5")
            {
                return "MT5";
            }

            return IsMt5ManualTrade(trade) ? "MT5" : trade.Exchange.Name;
        }

        private static string BuildExecutionProvider(TradeSphere.Domain.Entities.Trade trade)
        {
            if (trade.UserStrategy != null)
            {
                return trade.UserStrategy.ExecutionProvider;
            }

            return IsMt5ManualTrade(trade) ? "MT5" : "Delta";
        }

        private static bool IsMt5ManualTrade(TradeSphere.Domain.Entities.Trade trade)
        {
            return trade.ExternalOrderId != null &&
                   trade.ExternalOrderId.StartsWith("MT5-Manual", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractManualMt5AccountName(string? externalOrderId)
        {
            var account = ExtractExternalPart(externalOrderId, "Account:");
            return string.IsNullOrWhiteSpace(account) ? "Manual MT5" : account;
        }

        private static string? ExtractManualMt5Ticket(string? externalOrderId)
        {
            return ExtractExternalPart(externalOrderId, "Ticket:");
        }

        private static string? ExtractExternalPart(string? externalOrderId, string marker)
        {
            if (string.IsNullOrWhiteSpace(externalOrderId))
            {
                return null;
            }

            var index = externalOrderId.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var start = index + marker.Length;
            var end = externalOrderId.IndexOf('|', start);
            return end < 0 ? externalOrderId[start..] : externalOrderId[start..end];
        }
        private static List<Mt5BridgeDealDto> FindMt5CloseDealsForEntry(
            List<Mt5BridgeDealDto> deals,
            TradeSphere.Domain.Entities.Trade trade,
            long mt5Ticket,
            long createdSec,
            HashSet<long>? usedCloseDealTickets = null)
        {
            var exactCloseDeals = deals
                .Where(d =>
                    d.Time >= createdSec &&
                    IsMt5ClosingDeal(d) &&
                    string.Equals(d.Symbol, trade.Symbol, StringComparison.OrdinalIgnoreCase) &&
                    IsMt5DealRelatedToTicket(d, mt5Ticket) &&
                    !IsMt5DealAlreadyUsed(d, usedCloseDealTickets))
                .OrderBy(d => d.Time)
                .ToList();

            if (exactCloseDeals.Count > 0)
            {
                return exactCloseDeals;
            }

            // Some MT5 brokers return a different order/deal id than the open position ticket.
            // Fallback conservatively by symbol, close side, volume, and time so closed trades
            // still reconcile instead of being left as filled with missing P/L.
            var expectedCloseType = ResolveExpectedMt5CloseDealType(trade);
            var expectedVolume = trade.Quantity;
            return deals
                .Where(d =>
                    d.Time >= createdSec &&
                    IsMt5ClosingDeal(d) &&
                    string.Equals(d.Symbol, trade.Symbol, StringComparison.OrdinalIgnoreCase) &&
                    (expectedCloseType < 0 || d.Type == expectedCloseType) &&
                    (expectedVolume <= 0m || Math.Abs(d.Volume - expectedVolume) < 0.0001m) &&
                    !IsMt5DealAlreadyUsed(d, usedCloseDealTickets))
                .OrderBy(d => d.Time)
                .Take(1)
                .ToList();
        }

        private static List<Mt5BridgeDealDto> FindMt5PositionDealsForEntry(
            List<Mt5BridgeDealDto> deals,
            TradeSphere.Domain.Entities.Trade trade,
            long mt5Ticket,
            long createdSec)
        {
            return deals
                .Where(d =>
                    d.Time >= createdSec - 60 &&
                    string.Equals(d.Symbol, trade.Symbol, StringComparison.OrdinalIgnoreCase) &&
                    IsMt5DealRelatedToTicket(d, mt5Ticket))
                .OrderBy(d => d.Time)
                .ToList();
        }

        private static bool IsMt5DealRelatedToTicket(Mt5BridgeDealDto deal, long mt5Ticket)
        {
            return deal.Position_Id == mt5Ticket ||
                   deal.Order == mt5Ticket ||
                   deal.Ticket == mt5Ticket;
        }

        private static bool IsMt5DealAlreadyUsed(Mt5BridgeDealDto deal, HashSet<long>? usedCloseDealTickets)
        {
            return deal.Ticket > 0 && usedCloseDealTickets?.Contains(deal.Ticket) == true;
        }

        private static int ResolveExpectedMt5CloseDealType(TradeSphere.Domain.Entities.Trade trade)
        {
            if (string.Equals(trade.Side, "Buy", StringComparison.OrdinalIgnoreCase))
            {
                return 1; // Buy positions close with a sell deal.
            }

            if (string.Equals(trade.Side, "Sell", StringComparison.OrdinalIgnoreCase))
            {
                return 0; // Sell positions close with a buy deal.
            }

            return -1;
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
            if (IsMt5ManualTrade(trade))
            {
                return ExtractManualMt5Ticket(trade.ExternalOrderId);
            }

            if (trade.UserStrategy?.ExecutionProvider == "MT5")
            {
                var ticket = ExtractMt5Ticket(trade.ExternalOrderId);
                return ticket > 0 ? ticket.ToString(CultureInfo.InvariantCulture) : null;
            }

            return string.IsNullOrWhiteSpace(trade.ExternalOrderId) ? null : trade.ExternalOrderId;
        }

        private static string BuildActivityType(TradeSphere.Domain.Entities.Trade trade)
        {
            if (IsMt5ManualTrade(trade))
            {
                return string.Equals(trade.Status, "Closed", StringComparison.OrdinalIgnoreCase) ? "Manual Close" : "Manual MT5";
            }

            if (string.Equals(trade.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                return "Failed";
            }

            if (CanResumeMt5AutoRisk(trade))
            {
                return "Manual SL Override";
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

        private static bool CanResumeMt5AutoRisk(TradeSphere.Domain.Entities.Trade trade)
        {
            return trade.UserStrategy?.ExecutionProvider == "MT5" &&
                   string.Equals(trade.Status, "Manual Override", StringComparison.OrdinalIgnoreCase) &&
                   trade.ExternalOrderId != null &&
                   trade.ExternalOrderId.StartsWith("MT5-ManualRiskOverride", StringComparison.OrdinalIgnoreCase) &&
                   ExtractMt5Ticket(trade.ExternalOrderId) > 0;
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

            var accountsWithStrategies = await _context.UserStrategies
                .Where(us => us.UserId == userId && us.ExecutionProvider == "MT5" && us.Mt5AccountId != null)
                .Select(us => us.Mt5AccountId!.Value)
                .Distinct()
                .ToListAsync();

            var mt5Accounts = await _context.Mt5Accounts
                .Where(a =>
                    a.UserId == userId &&
                    a.TradingEnabled &&
                    (accountsWithStrategies.Contains(a.Id) ||
                     a.AccountType != "Demo" ||
                     a.Name.Contains("FundingPips")))
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
            var trades = await GetTradesAsync(userId);
            var positions = await GetPositionsAsync(userId);

            return new TradingOverviewDto
            {
                Trades = trades,
                Positions = positions,
                RealizedPnl = trades.Sum(t => t.Pnl)
            };
        }

        public async Task ResumeMt5AutoRiskAsync(int userId, int tradeId)
        {
            var overrideTrade = await _context.Trades
                .Include(t => t.Exchange)
                .Include(t => t.UserStrategy)
                    .ThenInclude(us => us.Strategy)
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Id == tradeId);

            if (overrideTrade == null)
            {
                throw new InvalidOperationException("Manual SL override row was not found.");
            }

            if (!CanResumeMt5AutoRisk(overrideTrade))
            {
                throw new InvalidOperationException("Auto risk can only be resumed from an active MT5 Manual Override row.");
            }

            var ticket = ExtractMt5Ticket(overrideTrade.ExternalOrderId);
            var (manualStopLoss, _) = ParseRiskLevels(overrideTrade.ExternalOrderId);
            if (ticket <= 0 || manualStopLoss <= 0m)
            {
                throw new InvalidOperationException("Manual override row is missing MT5 ticket or SL level.");
            }

            var formattedStopLoss = manualStopLoss.ToString("0.#####", CultureInfo.InvariantCulture);
            overrideTrade.Status = "Resolved";
            overrideTrade.ErrorReason = $"Manual SL override resolved. Auto breakeven/trailing resumed from SL {formattedStopLoss}.";
            overrideTrade.UpdatedAt = DateTime.UtcNow;

            _context.Trades.Add(new TradeSphere.Domain.Entities.Trade
            {
                UserId = overrideTrade.UserId,
                UserStrategyId = overrideTrade.UserStrategyId,
                ExchangeId = overrideTrade.ExchangeId,
                Symbol = overrideTrade.Symbol,
                Side = overrideTrade.Side,
                OrderType = "Resume Auto Risk",
                Price = overrideTrade.Price,
                Quantity = overrideTrade.Quantity,
                Status = "Modified",
                ExecutedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Pnl = 0m,
                ExternalOrderId = $"MT5-Risk|Reason:ManualResume|SL:{formattedStopLoss}|MT5:{ticket}",
                ErrorReason = $"Auto breakeven/trailing resumed from manual SL {formattedStopLoss}.",
                BrokerResponse = "User resumed TradeSphere automated risk management."
            });

            await _context.SaveChangesAsync();
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












