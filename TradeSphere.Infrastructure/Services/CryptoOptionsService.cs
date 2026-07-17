
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;
using TradeSphere.Domain.Entities;
using TradeSphere.Infrastructure.Persistence;

namespace TradeSphere.Infrastructure.Services
{
    public class CryptoOptionsService : ICryptoOptionsService, ICryptoOptionsBacktestService, IOptionScanner, IOptionChainProvider
    {
        private readonly ApplicationDbContext _context;
        private readonly IOptionRiskManager _riskManager;
        private readonly IOptionAnalyticsService _analytics;
        private readonly IHttpClientFactory _httpClientFactory;

        public CryptoOptionsService(ApplicationDbContext context, IOptionRiskManager riskManager, IOptionAnalyticsService analytics, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _riskManager = riskManager;
            _analytics = analytics;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IReadOnlyList<CryptoOptionStrategyConfigDto>> GetConfigsAsync(int userId)
        {
            await EnsureDefaultConfigAsync(userId);
            return await _context.CryptoOptionStrategyConfigs.Include(c => c.Legs)
                .Where(c => c.UserId == userId).OrderBy(c => c.Name)
                .Select(c => ToConfigDto(c)).ToListAsync();
        }

        public async Task<CryptoOptionStrategyConfigDto> CreateConfigAsync(int userId, UpsertCryptoOptionStrategyConfigDto dto)
        {
            var config = new CryptoOptionStrategyConfig { UserId = userId };
            ApplyConfig(config, dto);
            AddConfiguredLegs(config, dto);
            _context.CryptoOptionStrategyConfigs.Add(config);
            await _context.SaveChangesAsync();
            return ToConfigDto(config);
        }

        public async Task<CryptoOptionStrategyConfigDto> UpdateConfigAsync(int userId, int id, UpsertCryptoOptionStrategyConfigDto dto)
        {
            var config = await _context.CryptoOptionStrategyConfigs.Include(c => c.Legs)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (config == null) throw new InvalidOperationException("Crypto options strategy config not found.");
            ApplyConfig(config, dto);
            config.UpdatedAt = DateTime.UtcNow;
            _context.CryptoOptionStrategyLegs.RemoveRange(config.Legs);
            config.Legs.Clear();
            AddConfiguredLegs(config, dto);
            await _context.SaveChangesAsync();
            return ToConfigDto(config);
        }

        public async Task DeleteConfigAsync(int userId, int id)
        {
            var config = await _context.CryptoOptionStrategyConfigs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            if (config == null) throw new InvalidOperationException("Crypto options strategy config not found.");
            _context.CryptoOptionStrategyConfigs.Remove(config);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<CryptoOptionChainSnapshotDto>> GetChainSnapshotsAsync(string? exchange, string? symbol, DateTime? from, DateTime? to)
        {
            var query = _context.CryptoOptionChainSnapshots.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(exchange)) query = query.Where(s => s.Exchange == exchange);
            if (!string.IsNullOrWhiteSpace(symbol)) query = query.Where(s => s.Symbol == symbol || s.Underlying == symbol);
            if (from.HasValue) query = query.Where(s => s.SnapshotTime >= from.Value.Date);
            if (to.HasValue) query = query.Where(s => s.SnapshotTime < to.Value.Date.AddDays(1));
            return await query.OrderByDescending(s => s.SnapshotTime).ThenBy(s => s.Strike).Take(500).Select(s => ToChainDto(s)).ToListAsync();
        }

        public async Task<IReadOnlyList<CryptoOptionChainSnapshotDto>> GetSnapshotsAsync(string exchange, string symbol, DateTime from, DateTime to)
            => await GetChainSnapshotsAsync(exchange, symbol, from, to);


        public async Task<IReadOnlyList<CryptoOptionExpiryDto>> GetDeltaExpiriesAsync(string? exchange, string? underlying, string? symbol)
        {
            var parsed = await LoadDeltaOptionMarketAsync(exchange, underlying, symbol);
            return parsed.Expiries;
        }

        public async Task<CryptoOptionChainFetchResultDto> FetchDeltaChainAsync(FetchCryptoOptionChainRequestDto request)
        {
            var parsed = await LoadDeltaOptionMarketAsync(request.Exchange, request.Underlying, request.Symbol);
            var selectedExpiry = request.ExpiryDate?.Date
                ?? parsed.Expiries.Where(e => !e.IsExpired).OrderBy(e => e.ExpiryDate).FirstOrDefault()?.ExpiryDate.Date
                ?? parsed.Expiries.OrderBy(e => e.ExpiryDate).FirstOrDefault()?.ExpiryDate.Date;

            var rows = parsed.Rows
                .Where(r => !selectedExpiry.HasValue || r.ExpiryDate.Date == selectedExpiry.Value.Date)
                .OrderBy(r => r.Strike)
                .ToList();

            var imported = 0;
            if (request.SaveSnapshot && rows.Count > 0)
            {
                imported = await ImportChainSnapshotsAsync(rows.Select(r => new ImportCryptoOptionChainSnapshotDto
                {
                    Exchange = r.Exchange,
                    Underlying = r.Underlying,
                    Symbol = r.Symbol,
                    ExpiryDate = r.ExpiryDate,
                    Strike = r.Strike,
                    UnderlyingPrice = r.UnderlyingPrice,
                    SnapshotTime = r.SnapshotTime,
                    CallPremium = r.CallPremium,
                    CallBid = r.CallBid,
                    CallAsk = r.CallAsk,
                    CallVolume = r.CallVolume,
                    CallOpenInterest = r.CallOpenInterest,
                    CallIv = r.CallIv,
                    CallDelta = r.CallDelta,
                    CallGamma = r.CallGamma,
                    CallTheta = r.CallTheta,
                    CallVega = r.CallVega,
                    CallRho = r.CallRho,
                    PutPremium = r.PutPremium,
                    PutBid = r.PutBid,
                    PutAsk = r.PutAsk,
                    PutVolume = r.PutVolume,
                    PutOpenInterest = r.PutOpenInterest,
                    PutIv = r.PutIv,
                    PutDelta = r.PutDelta,
                    PutGamma = r.PutGamma,
                    PutTheta = r.PutTheta,
                    PutVega = r.PutVega,
                    PutRho = r.PutRho
                }).ToList());
            }

            var warnings = parsed.Warnings.ToList();
            if (rows.Count == 0)
            {
                warnings.Add("No option-chain rows were returned for the selected expiry. Try BTC/ETH, a different expiry, or Delta may have changed the public API shape.");
            }

            return new CryptoOptionChainFetchResultDto
            {
                Exchange = parsed.Exchange,
                Underlying = parsed.Underlying,
                Symbol = parsed.Symbol,
                SnapshotTime = parsed.SnapshotTime,
                SelectedExpiryDate = selectedExpiry,
                UnderlyingPrice = rows.FirstOrDefault()?.UnderlyingPrice ?? parsed.UnderlyingPrice,
                Imported = imported,
                Expiries = parsed.Expiries,
                Rows = rows,
                Suggestions = BuildDeltaSuggestions(rows, selectedExpiry),
                Warnings = warnings
            };
        }
        public async Task<int> ImportChainSnapshotsAsync(IReadOnlyList<ImportCryptoOptionChainSnapshotDto> snapshots)
        {
            foreach (var dto in snapshots)
            {
                _context.CryptoOptionChainSnapshots.Add(new CryptoOptionChainSnapshot
                {
                    Exchange = dto.Exchange.Trim(), Underlying = Normalize(dto.Underlying), Symbol = Normalize(dto.Symbol),
                    ExpiryDate = dto.ExpiryDate.Date, Strike = dto.Strike, UnderlyingPrice = dto.UnderlyingPrice, SnapshotTime = dto.SnapshotTime,
                    CallPremium = dto.CallPremium, CallBid = dto.CallBid, CallAsk = dto.CallAsk, CallVolume = dto.CallVolume, CallOpenInterest = dto.CallOpenInterest,
                    CallIv = dto.CallIv, CallDelta = dto.CallDelta, CallGamma = dto.CallGamma, CallTheta = dto.CallTheta, CallVega = dto.CallVega, CallRho = dto.CallRho,
                    PutPremium = dto.PutPremium, PutBid = dto.PutBid, PutAsk = dto.PutAsk, PutVolume = dto.PutVolume, PutOpenInterest = dto.PutOpenInterest,
                    PutIv = dto.PutIv, PutDelta = dto.PutDelta, PutGamma = dto.PutGamma, PutTheta = dto.PutTheta, PutVega = dto.PutVega, PutRho = dto.PutRho
                });
            }
            await _context.SaveChangesAsync();
            return snapshots.Count;
        }

        public async Task<CryptoOptionBacktestRunDto> RunAsync(int userId, CryptoOptionBacktestRequestDto request)
        {
            var config = await ResolveConfigAsync(userId, request);
            var from = request.FromDate.Date; var to = request.ToDate.Date;
            if (to < from) throw new ArgumentException("To date must be greater than or equal to from date.");
            var run = new CryptoOptionBacktestRun
            {
                UserId = userId, StrategyConfigId = config.Id == 0 ? null : config.Id, StrategyName = config.Name, StrategyType = config.StrategyType,
                Symbol = string.IsNullOrWhiteSpace(request.Symbol) ? config.Symbol : Normalize(request.Symbol), Exchange = string.IsNullOrWhiteSpace(request.Exchange) ? config.Exchange : request.Exchange.Trim(),
                FromDate = from, ToDate = to, InitialCapital = request.InitialCapital <= 0 ? 10000m : request.InitialCapital, Status = "Running", StartedAt = DateTime.UtcNow
            };
            _context.CryptoOptionBacktestRuns.Add(run);
            await _context.SaveChangesAsync();
            try
            {
                var snapshots = await _context.CryptoOptionChainSnapshots
                    .Where(s => s.Exchange == run.Exchange && (s.Symbol == run.Symbol || s.Underlying == run.Symbol) && s.SnapshotTime.Date >= from && s.SnapshotTime.Date <= to)
                    .OrderBy(s => s.SnapshotTime).ThenBy(s => s.Strike).ToListAsync();
                if (snapshots.Count == 0) throw new InvalidOperationException("Option chain snapshots are required for accurate options backtesting.");
                foreach (var day in snapshots.GroupBy(s => s.SnapshotTime.Date).OrderBy(g => g.Key))
                {
                    var position = RunShortStrangleDay(run, config, day.ToList());
                    if (position == null) continue;
                    run.Positions.Add(position);
                    run.DailyPnls.Add(BuildDailyPnl(run.Id, position));
                }
                FinalizeRun(run); run.Status = "Completed"; run.CompletedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                run.Status = "Failed"; run.ErrorMessage = ex.Message; run.CompletedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            return ToRunDto(run);
        }
        public async Task<IReadOnlyList<CryptoOptionBacktestRunDto>> GetBacktestRunsAsync(int userId)
            => await _context.CryptoOptionBacktestRuns.Where(r => r.UserId == userId).OrderByDescending(r => r.StartedAt).Select(r => ToRunDto(r)).ToListAsync();

        public async Task<CryptoOptionBacktestRunDto> GetBacktestRunAsync(int userId, int id)
        {
            var run = await _context.CryptoOptionBacktestRuns.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            return run == null ? throw new InvalidOperationException("Crypto options backtest run not found.") : ToRunDto(run);
        }

        public async Task<IReadOnlyList<CryptoOptionBacktestPositionDto>> GetBacktestPositionsAsync(int userId, int runId)
        {
            await EnsureRunOwnedAsync(userId, runId);
            return await _context.CryptoOptionBacktestPositions.Include(p => p.Legs).Where(p => p.BacktestRunId == runId)
                .OrderByDescending(p => p.TradeDate).Select(p => ToPositionDto(p)).ToListAsync();
        }

        public async Task<IReadOnlyList<CryptoOptionBacktestLegDto>> GetBacktestLegsAsync(int userId, int runId)
        {
            await EnsureRunOwnedAsync(userId, runId);
            return await _context.CryptoOptionBacktestLegs.Where(l => l.BacktestPosition!.BacktestRunId == runId)
                .OrderByDescending(l => l.EntryTime).Select(l => ToLegDto(l)).ToListAsync();
        }

        public async Task<IReadOnlyList<CryptoOptionDailyPnlDto>> GetDailyPnlAsync(int userId, int runId)
        {
            await EnsureRunOwnedAsync(userId, runId);
            return await _context.CryptoOptionDailyPnls.Where(p => p.BacktestRunId == runId)
                .OrderByDescending(p => p.TradeDate).Select(p => ToDailyPnlDto(p)).ToListAsync();
        }

        public async Task<CryptoOptionRiskReportDto> GetRiskReportAsync(int userId)
        {
            var daily = await _context.CryptoOptionDailyPnls.Where(p => p.BacktestRun!.UserId == userId).OrderByDescending(p => p.TradeDate).Take(50).Select(p => ToDailyPnlDto(p)).ToListAsync();
            var stopLossHits = await _context.CryptoOptionBacktestLegs.CountAsync(l => l.BacktestPosition!.BacktestRun!.UserId == userId && l.StopLossHit);
            return new CryptoOptionRiskReportDto
            {
                CircuitBreakerDays = daily.Count(d => d.IsCircuitBreakerHit), StopLossHits = stopLossHits,
                WorstDayPnl = daily.Count == 0 ? 0 : daily.Min(d => d.NetPnl),
                MaxDrawdown = await _context.CryptoOptionBacktestRuns.Where(r => r.UserId == userId).Select(r => r.MaxDrawdown).DefaultIfEmpty().MaxAsync(),
                RecentRiskDays = daily.Where(d => d.IsCircuitBreakerHit || d.NetPnl < 0).Take(10).ToList()
            };
        }

        public async Task<IReadOnlyList<CryptoOptionScannerResultDto>> ScanAsync(int userId, string exchange, string symbol, string scannerMode)
        {
            var latest = await _context.CryptoOptionChainSnapshots.Where(s => s.Exchange == exchange && (s.Symbol == symbol || s.Underlying == symbol))
                .OrderByDescending(s => s.SnapshotTime).Select(s => (DateTime?)s.SnapshotTime).FirstOrDefaultAsync();
            if (!latest.HasValue) return Array.Empty<CryptoOptionScannerResultDto>();
            var chain = await _context.CryptoOptionChainSnapshots.Where(s => s.Exchange == exchange && (s.Symbol == symbol || s.Underlying == symbol) && s.SnapshotTime == latest.Value).ToListAsync();
            var call = chain.Where(s => s.CallPremium.HasValue).OrderBy(s => Math.Abs((s.CallDelta ?? 0.15m) - 0.15m)).FirstOrDefault();
            var put = chain.Where(s => s.PutPremium.HasValue).OrderBy(s => Math.Abs(Math.Abs(s.PutDelta ?? -0.15m) - 0.15m)).FirstOrDefault();
            if (call == null || put == null) return Array.Empty<CryptoOptionScannerResultDto>();
            var result = new CryptoOptionScannerResult
            {
                UserId = userId, Exchange = exchange, Symbol = symbol, ScannerMode = scannerMode, StrategyType = "ShortStrangle", ScanTime = DateTime.UtcNow,
                BestCallStrike = call.Strike, BestCallPremium = call.CallPremium, BestCallDelta = call.CallDelta, BestCallTheta = call.CallTheta, BestCallIv = call.CallIv,
                BestPutStrike = put.Strike, BestPutPremium = put.PutPremium, BestPutDelta = put.PutDelta, BestPutTheta = put.PutTheta, BestPutIv = put.PutIv,
                ProbabilityOfProfit = _analytics.EstimateProbabilityOfProfit(call.CallDelta, put.PutDelta), StrategyScore = 8.5m,
                Notes = "First-pass scanner output. Future AI scanner can replace this ranking model."
            };
            _context.CryptoOptionScannerResults.Add(result); await _context.SaveChangesAsync();
            return new[] { ToScannerDto(result) };
        }

        private CryptoOptionBacktestPosition? RunShortStrangleDay(CryptoOptionBacktestRun run, CryptoOptionStrategyConfig config, List<CryptoOptionChainSnapshot> rows)
        {
            var entryRows = GetRowsAtOrAfter(rows, ParseTime(config.EntryTime, new TimeSpan(9, 0, 0)));
            if (entryRows.Count == 0) return null;
            var expiry = ResolveExpiryDate(config.ExpiryType, entryRows[0].SnapshotTime.Date);
            entryRows = entryRows.Where(s => s.ExpiryDate.Date == expiry.Date).ToList();
            if (entryRows.Count == 0) return null;
            var ceRow = SelectLegRow(entryRows, "CE", config); var peRow = SelectLegRow(entryRows, "PE", config);
            if (ceRow?.CallPremium == null || peRow?.PutPremium == null) return null;
            var position = new CryptoOptionBacktestPosition { BacktestRunId = run.Id, TradeDate = entryRows[0].SnapshotTime.Date, ExpiryDate = expiry.Date, Underlying = config.Underlying, UnderlyingEntryPrice = entryRows[0].UnderlyingPrice, Status = "Open" };
            position.Legs.Add(CreateLeg(position, "CE", ceRow, ceRow.CallPremium.Value, config));
            position.Legs.Add(CreateLeg(position, "PE", peRow, peRow.PutPremium.Value, config));
            var exitTime = ParseTime(config.ExitTime, new TimeSpan(17, 15, 0));
            foreach (var timeGroup in rows.Where(s => s.SnapshotTime.TimeOfDay > entryRows[0].SnapshotTime.TimeOfDay).GroupBy(s => s.SnapshotTime).OrderBy(g => g.Key))
            {
                var markRows = timeGroup.ToList(); var currentPnl = 0m;
                foreach (var leg in position.Legs.Where(l => l.Status == "Open").ToList())
                {
                    var premium = GetPremium(markRows.FirstOrDefault(r => r.Strike == leg.Strike && r.ExpiryDate.Date == leg.ExpiryDate.Date), leg.LegType);
                    if (!premium.HasValue) continue;
                    currentPnl += _analytics.CalculateShortOptionPnl(leg.EntryPremium, premium.Value, leg.Quantity);
                    if (_riskManager.IsLegStopLossHit(leg.EntryPremium, premium.Value, config.StopLossPercentPerLeg)) CloseLeg(leg, timeGroup.Key, premium.Value, "StopLoss");
                }
                if (_riskManager.IsDailyCircuitBreakerHit(currentPnl, config.MaxDailyLoss))
                {
                    CloseAllOpen(position, markRows, timeGroup.Key, "CircuitBreaker"); position.IsCircuitBreakerHit = true; position.ExitReason = "CircuitBreaker"; break;
                }
                if (timeGroup.Key.TimeOfDay >= exitTime) { CloseAllOpen(position, markRows, timeGroup.Key, "TimeExit"); position.ExitReason = "TimeExit"; break; }
                if (position.Legs.All(l => l.Status == "Closed")) break;
            }
            if (position.Legs.Any(l => l.Status == "Open")) CloseAllOpen(position, GetRowsAtOrBefore(rows, exitTime), rows.Last().SnapshotTime, "EndOfData");
            position.Status = "Closed"; position.GrossPnl = position.Legs.Sum(l => l.Pnl); position.Charges = config.BrokeragePerOrder * position.Legs.Count * 2; position.NetPnl = position.GrossPnl - position.Charges; position.UnderlyingExitPrice = rows.Last().UnderlyingPrice;
            return position;
        }
        private CryptoOptionBacktestLeg CreateLeg(CryptoOptionBacktestPosition position, string type, CryptoOptionChainSnapshot row, decimal premium, CryptoOptionStrategyConfig config)
        {
            var adjusted = config.UseSlippage ? premium - premium * config.SlippagePercent / 100m : premium;
            var leg = new CryptoOptionBacktestLeg { BacktestPosition = position, LegType = type, Action = "Sell", Strike = row.Strike, ExpiryDate = row.ExpiryDate.Date, EntryTime = row.SnapshotTime, EntryPremium = adjusted, Quantity = config.LotSize, Status = "Open", EntryDelta = type == "CE" ? row.CallDelta : row.PutDelta, EntryTheta = type == "CE" ? row.CallTheta : row.PutTheta, EntryIv = type == "CE" ? row.CallIv : row.PutIv };
            leg.Events.Add(new CryptoOptionBacktestLegEvent { BacktestLeg = leg, EventTime = row.SnapshotTime, EventType = "Entry", Premium = adjusted, Pnl = 0, Reason = $"Sell {type} {row.Strike}" });
            return leg;
        }

        private void CloseLeg(CryptoOptionBacktestLeg leg, DateTime time, decimal premium, string reason)
        {
            leg.ExitTime = time; leg.ExitPremium = premium; leg.Pnl = _analytics.CalculateShortOptionPnl(leg.EntryPremium, premium, leg.Quantity); leg.Status = "Closed"; leg.ExitReason = reason; leg.StopLossHit = reason == "StopLoss";
            leg.Events.Add(new CryptoOptionBacktestLegEvent { BacktestLeg = leg, EventTime = time, EventType = reason, Premium = premium, Pnl = leg.Pnl, Reason = reason });
        }

        private void CloseAllOpen(CryptoOptionBacktestPosition position, List<CryptoOptionChainSnapshot> rows, DateTime time, string reason)
        {
            foreach (var leg in position.Legs.Where(l => l.Status == "Open").ToList())
            {
                var premium = GetPremium(rows.FirstOrDefault(r => r.Strike == leg.Strike && r.ExpiryDate.Date == leg.ExpiryDate.Date), leg.LegType) ?? leg.EntryPremium;
                CloseLeg(leg, time, premium, reason);
            }
        }

        private void FinalizeRun(CryptoOptionBacktestRun run)
        {
            run.GrossPnl = run.Positions.Sum(p => p.GrossPnl); run.Charges = run.Positions.Sum(p => p.Charges); run.TotalPnl = run.Positions.Sum(p => p.NetPnl); run.TotalTrades = run.Positions.SelectMany(p => p.Legs).Count();
            run.WinningDays = run.DailyPnls.Count(p => p.NetPnl > 0); run.LosingDays = run.DailyPnls.Count(p => p.NetPnl < 0);
            var gains = run.DailyPnls.Where(p => p.NetPnl > 0).Sum(p => p.NetPnl); var losses = Math.Abs(run.DailyPnls.Where(p => p.NetPnl < 0).Sum(p => p.NetPnl));
            run.ProfitFactor = losses == 0 ? gains : Math.Round(gains / losses, 2); run.MaxDrawdown = CalculateMaxDrawdown(run.DailyPnls.OrderBy(p => p.TradeDate).Select(p => p.NetPnl));
        }

        private CryptoOptionDailyPnl BuildDailyPnl(int runId, CryptoOptionBacktestPosition p) => new() { BacktestRunId = runId, TradeDate = p.TradeDate, GrossPnl = p.GrossPnl, NetPnl = p.NetPnl, Charges = p.Charges, MaxIntradayLoss = Math.Min(0, p.NetPnl), CeLegPnl = p.Legs.Where(l => l.LegType == "CE").Sum(l => l.Pnl), PeLegPnl = p.Legs.Where(l => l.LegType == "PE").Sum(l => l.Pnl), IsCircuitBreakerHit = p.IsCircuitBreakerHit, Notes = p.ExitReason };
        private static List<CryptoOptionChainSnapshot> GetRowsAtOrAfter(List<CryptoOptionChainSnapshot> rows, TimeSpan time) { var t = rows.Where(s => s.SnapshotTime.TimeOfDay >= time).Select(s => (DateTime?)s.SnapshotTime).OrderBy(x => x).FirstOrDefault(); return t.HasValue ? rows.Where(s => s.SnapshotTime == t.Value).ToList() : new(); }
        private static List<CryptoOptionChainSnapshot> GetRowsAtOrBefore(List<CryptoOptionChainSnapshot> rows, TimeSpan time) { var t = rows.Where(s => s.SnapshotTime.TimeOfDay <= time).Select(s => (DateTime?)s.SnapshotTime).OrderByDescending(x => x).FirstOrDefault(); return t.HasValue ? rows.Where(s => s.SnapshotTime == t.Value).ToList() : rows.TakeLast(1).ToList(); }
        private static CryptoOptionChainSnapshot? SelectLegRow(List<CryptoOptionChainSnapshot> rows, string type, CryptoOptionStrategyConfig config) { if (config.StrikeSelectionMode.Equals("DistanceBased", StringComparison.OrdinalIgnoreCase)) { var target = type == "CE" ? rows[0].UnderlyingPrice * (1 + config.StrikeDistancePercent / 100m) : rows[0].UnderlyingPrice * (1 - config.StrikeDistancePercent / 100m); return rows.Where(r => GetPremium(r, type).HasValue).OrderBy(r => Math.Abs(r.Strike - target)).FirstOrDefault(); } return rows.Where(r => GetPremium(r, type).HasValue).OrderBy(r => Math.Abs(GetPremium(r, type)!.Value - config.TargetPremiumPerLeg)).FirstOrDefault(); }
        private static decimal? GetPremium(CryptoOptionChainSnapshot? row, string type) => row == null ? null : type == "CE" ? row.CallPremium : row.PutPremium;
        private static DateTime ResolveExpiryDate(string expiryType, DateTime date) => expiryType == "Tomorrow" ? date.AddDays(1) : expiryType == "Weekly" ? date.AddDays(((int)DayOfWeek.Friday - (int)date.DayOfWeek + 7) % 7) : date;
        private static TimeSpan ParseTime(string value, TimeSpan fallback) => TimeSpan.TryParse(value, out var parsed) ? parsed : fallback;
        private static decimal CalculateMaxDrawdown(IEnumerable<decimal> pnl) { decimal peak = 0, equity = 0, dd = 0; foreach (var p in pnl) { equity += p; peak = Math.Max(peak, equity); dd = Math.Max(dd, peak - equity); } return dd; }


        private async Task<DeltaOptionMarketParseResult> LoadDeltaOptionMarketAsync(string? exchange, string? underlying, string? symbol)
        {
            var normalizedUnderlying = Normalize(string.IsNullOrWhiteSpace(underlying) ? "BTC" : underlying);
            var normalizedSymbol = Normalize(string.IsNullOrWhiteSpace(symbol) ? normalizedUnderlying + "USD" : symbol);
            var exchangeName = string.IsNullOrWhiteSpace(exchange) ? "Delta Exchange India" : exchange.Trim();
            var baseUrl = ResolveDeltaPublicBaseUrl(exchangeName);
            var snapshotTime = DateTime.UtcNow;
            var warnings = new List<string>();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TradeSphere-Options-Desk");

            var products = await GetDeltaResultArrayAsync(client, $"{baseUrl}/v2/products", warnings, "products");
            var optionProducts = products.Select(p => ParseDeltaOptionProduct(p, normalizedUnderlying))
                .Where(p => p != null)
                .Select(p => p!)
                .ToList();

            if (optionProducts.Count == 0)
            {
                warnings.Add("Delta public products endpoint returned no parsable option products. This may require an API shape update.");
            }

            var tickers = await GetDeltaResultArrayAsync(client, $"{baseUrl}/v2/tickers", warnings, "tickers");
            var tickerMap = tickers
                .Select(t => new { Symbol = ReadString(t, "symbol", "product_symbol", "contract_symbol"), Node = t })
                .Where(t => !string.IsNullOrWhiteSpace(t.Symbol))
                .GroupBy(t => Normalize(t.Symbol))
                .ToDictionary(g => g.Key, g => g.First().Node);

            var underlyingPrice = FindUnderlyingPrice(tickerMap, normalizedSymbol, normalizedUnderlying);
            var rowsByKey = new Dictionary<string, CryptoOptionChainSnapshotDto>();

            foreach (var product in optionProducts)
            {
                tickerMap.TryGetValue(Normalize(product.Symbol), out var ticker);
                var premium = ReadDecimal(ticker, "mark_price", "markPrice", "close", "last_price", "lastPrice", "spot_price")
                    ?? ReadDecimal(product.Raw, "mark_price", "premium");
                var bid = ReadDecimal(ticker, "best_bid", "bid", "bid_price");
                var ask = ReadDecimal(ticker, "best_ask", "ask", "ask_price");
                var openInterest = ReadDecimal(ticker, "oi", "open_interest", "openInterest") ?? ReadDecimal(product.Raw, "oi", "open_interest");
                var volume = ReadDecimal(ticker, "volume", "volume_usd", "turnover");
                var iv = ReadDecimal(ticker, "mark_iv", "iv", "implied_volatility");
                var delta = ReadDecimal(ticker?["greeks"], "delta") ?? ReadDecimal(ticker, "delta");
                var gamma = ReadDecimal(ticker?["greeks"], "gamma") ?? ReadDecimal(ticker, "gamma");
                var theta = ReadDecimal(ticker?["greeks"], "theta") ?? ReadDecimal(ticker, "theta");
                var vega = ReadDecimal(ticker?["greeks"], "vega") ?? ReadDecimal(ticker, "vega");
                var rho = ReadDecimal(ticker?["greeks"], "rho") ?? ReadDecimal(ticker, "rho");
                var markUnderlying = ReadDecimal(ticker, "underlying_price", "spot_price", "index_price");
                if (markUnderlying.HasValue && markUnderlying.Value > 0) underlyingPrice = markUnderlying.Value;

                var key = $"{product.ExpiryDate:yyyyMMdd}|{product.Strike}";
                if (!rowsByKey.TryGetValue(key, out var row))
                {
                    row = new CryptoOptionChainSnapshotDto
                    {
                        Exchange = exchangeName,
                        Underlying = normalizedUnderlying,
                        Symbol = normalizedSymbol,
                        ExpiryDate = product.ExpiryDate.Date,
                        Strike = product.Strike,
                        UnderlyingPrice = underlyingPrice,
                        SnapshotTime = snapshotTime
                    };
                    rowsByKey[key] = row;
                }

                row.UnderlyingPrice = underlyingPrice > 0 ? underlyingPrice : row.UnderlyingPrice;
                if (product.OptionType == "CE")
                {
                    row.CallPremium = premium;
                    row.CallBid = bid;
                    row.CallAsk = ask;
                    row.CallVolume = volume;
                    row.CallOpenInterest = openInterest;
                    row.CallIv = iv;
                    row.CallDelta = delta;
                    row.CallGamma = gamma;
                    row.CallTheta = theta;
                    row.CallVega = vega;
                    row.CallRho = rho;
                }
                else
                {
                    row.PutPremium = premium;
                    row.PutBid = bid;
                    row.PutAsk = ask;
                    row.PutVolume = volume;
                    row.PutOpenInterest = openInterest;
                    row.PutIv = iv;
                    row.PutDelta = delta;
                    row.PutGamma = gamma;
                    row.PutTheta = theta;
                    row.PutVega = vega;
                    row.PutRho = rho;
                }
            }

            var nowIndia = DateTime.UtcNow.AddHours(5.5);
            var expiries = optionProducts.Select(p => p.ExpiryDate.Date).Distinct().OrderBy(d => d).Select(d => new CryptoOptionExpiryDto
            {
                ExpiryDate = d,
                Label = d.Date == nowIndia.Date ? $"Today - {d:dd MMM yy}" : d.ToString("dd MMM yy", CultureInfo.InvariantCulture),
                IsToday = d.Date == nowIndia.Date,
                IsExpired = d.Date < nowIndia.Date,
                TimeToExpiryHours = Math.Round((decimal)(d.Date.AddDays(1).AddTicks(-1) - nowIndia).TotalHours, 2)
            }).ToList();

            return new DeltaOptionMarketParseResult(exchangeName, normalizedUnderlying, normalizedSymbol, snapshotTime, underlyingPrice, expiries, rowsByKey.Values.OrderBy(r => r.ExpiryDate).ThenBy(r => r.Strike).ToList(), warnings);
        }

        private static IReadOnlyList<CryptoOptionSuggestionDto> BuildDeltaSuggestions(IReadOnlyList<CryptoOptionChainSnapshotDto> rows, DateTime? selectedExpiry)
        {
            if (rows.Count == 0)
            {
                return new[] { new CryptoOptionSuggestionDto { Status = "No Data", Recommendation = "Do not trade", ExpiryDate = selectedExpiry, Reason = "No chain rows available for the selected expiry." } };
            }

            var nowIndia = DateTime.UtcNow.AddHours(5.5);
            var is0Dte = selectedExpiry?.Date == nowIndia.Date;
            var underlying = rows.First().UnderlyingPrice;
            var call = rows.Where(r => r.CallPremium.HasValue && r.CallPremium.Value > 0 && (underlying <= 0 || r.Strike >= underlying))
                .OrderBy(r => r.CallDelta.HasValue ? Math.Abs(r.CallDelta.Value - 0.15m) : Math.Abs((r.CallPremium ?? 0) - 100m))
                .FirstOrDefault();
            var put = rows.Where(r => r.PutPremium.HasValue && r.PutPremium.Value > 0 && (underlying <= 0 || r.Strike <= underlying))
                .OrderBy(r => r.PutDelta.HasValue ? Math.Abs(Math.Abs(r.PutDelta.Value) - 0.15m) : Math.Abs((r.PutPremium ?? 0) - 100m))
                .FirstOrDefault();

            if (call == null || put == null)
            {
                return new[] { new CryptoOptionSuggestionDto { Status = "Watch", Recommendation = "Wait", ExpiryDate = selectedExpiry, Reason = "Both CE and PE legs are not available with live premiums." } };
            }

            var credit = (call.CallPremium ?? 0) + (put.PutPremium ?? 0);
            var status = is0Dte ? "Ready" : "Watch";
            var recommendation = is0Dte ? "0DTE candidate: review liquidity/spread before selling both legs." : "Not 0DTE: useful for research, but wait for same-day expiry for 0DTE playbook.";
            var reason = call.CallDelta.HasValue || put.PutDelta.HasValue
                ? "Selected near 0.15 delta wings where available. If deltas are missing, premium proximity is used."
                : "Selected by premium proximity because Delta did not return greeks in the public ticker payload.";

            return new[]
            {
                new CryptoOptionSuggestionDto
                {
                    Status = status,
                    Recommendation = recommendation,
                    ExpiryDate = selectedExpiry,
                    CallStrike = call.Strike,
                    PutStrike = put.Strike,
                    CallPremium = call.CallPremium,
                    PutPremium = put.PutPremium,
                    CallDelta = call.CallDelta,
                    PutDelta = put.PutDelta,
                    EstimatedCredit = credit,
                    Reason = reason
                }
            };
        }

        private static string ResolveDeltaPublicBaseUrl(string exchange)
        {
            if (exchange.Contains("testnet", StringComparison.OrdinalIgnoreCase)) return "https://cdn-ind.testnet.deltaex.org";
            if (exchange.Contains("global", StringComparison.OrdinalIgnoreCase)) return "https://api.delta.exchange";
            return "https://api.india.delta.exchange";
        }

        private static async Task<IReadOnlyList<JsonNode>> GetDeltaResultArrayAsync(HttpClient client, string url, List<string> warnings, string label)
        {
            try
            {
                using var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    warnings.Add($"Delta {label} request failed: {(int)response.StatusCode} {content[..Math.Min(content.Length, 180)]}");
                    return Array.Empty<JsonNode>();
                }

                var json = JsonNode.Parse(content);
                var result = json?["result"] ?? json;
                var array = result as JsonArray ?? result?["products"] as JsonArray ?? result?["tickers"] as JsonArray ?? json?["result"]?["items"] as JsonArray;
                if (array == null) return Array.Empty<JsonNode>();
                return array.Where(x => x != null).Select(x => x!).ToList();
            }
            catch (Exception ex)
            {
                warnings.Add($"Delta {label} request failed: {ex.Message}");
                return Array.Empty<JsonNode>();
            }
        }

        private static DeltaOptionProduct? ParseDeltaOptionProduct(JsonNode product, string underlying)
        {
            var symbol = ReadString(product, "symbol", "product_symbol", "contract_symbol");
            if (string.IsNullOrWhiteSpace(symbol)) return null;
            var contractType = ReadString(product, "contract_type", "contractType", "product_type") ?? string.Empty;
            var searchable = $"{symbol} {contractType} {ReadString(product, "description", "name")}".ToUpperInvariant();
            if (!searchable.Contains(underlying, StringComparison.OrdinalIgnoreCase)) return null;
            if (!searchable.Contains("OPTION") && !searchable.StartsWith("C-") && !searchable.StartsWith("P-")) return null;

            var optionType = searchable.Contains("PUT") || searchable.StartsWith("P-") ? "PE" : searchable.Contains("CALL") || searchable.StartsWith("C-") ? "CE" : string.Empty;
            if (optionType.Length == 0) return null;
            var strike = ReadDecimal(product, "strike_price", "strike", "strikePrice") ?? ParseStrikeFromSymbol(symbol);
            var expiry = ReadDate(product, "expiry_time", "expiryTime", "settlement_time", "settlementTime", "expiry", "expiry_date") ?? ParseExpiryFromSymbol(symbol);
            if (!strike.HasValue || !expiry.HasValue) return null;
            return new DeltaOptionProduct(symbol, optionType, strike.Value, expiry.Value.Date, product);
        }

        private static decimal FindUnderlyingPrice(Dictionary<string, JsonNode> tickerMap, string symbol, string underlying)
        {
            foreach (var key in new[] { symbol, underlying + "USD", underlying + "USDT" }.Select(Normalize))
            {
                if (tickerMap.TryGetValue(key, out var ticker))
                {
                    var price = ReadDecimal(ticker, "mark_price", "spot_price", "index_price", "close", "last_price");
                    if (price.HasValue && price.Value > 0) return price.Value;
                }
            }
            return 0m;
        }

        private static decimal? ReadDecimal(JsonNode? node, params string[] names)
        {
            if (node == null) return null;
            foreach (var name in names)
            {
                var value = node[name]?.ToString();
                if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
            }
            return null;
        }

        private static string? ReadString(JsonNode? node, params string[] names)
        {
            if (node == null) return null;
            foreach (var name in names)
            {
                var value = node[name]?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return null;
        }

        private static DateTime? ReadDate(JsonNode? node, params string[] names)
        {
            foreach (var name in names)
            {
                var value = node?[name]?.ToString();
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)) return parsed;
                if (long.TryParse(value, out var unix)) return unix > 9999999999 ? DateTimeOffset.FromUnixTimeMilliseconds(unix).UtcDateTime : DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }
            return null;
        }

        private static decimal? ParseStrikeFromSymbol(string symbol)
        {
            var tokens = symbol.Split('-', '_', '/', ' ');
            foreach (var token in tokens)
            {
                if (decimal.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var strike) && strike > 0) return strike;
            }
            return null;
        }

        private static DateTime? ParseExpiryFromSymbol(string symbol)
        {
            var tokens = symbol.Split('-', '_', '/', ' ');
            foreach (var token in tokens.Reverse())
            {
                if (DateTime.TryParseExact(token, new[] { "ddMMyy", "yyyyMMdd", "ddMMMyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return parsed;
            }
            return null;
        }

        private sealed record DeltaOptionProduct(string Symbol, string OptionType, decimal Strike, DateTime ExpiryDate, JsonNode Raw);
        private sealed record DeltaOptionMarketParseResult(string Exchange, string Underlying, string Symbol, DateTime SnapshotTime, decimal UnderlyingPrice, IReadOnlyList<CryptoOptionExpiryDto> Expiries, IReadOnlyList<CryptoOptionChainSnapshotDto> Rows, IReadOnlyList<string> Warnings);
        private async Task<CryptoOptionStrategyConfig> ResolveConfigAsync(int userId, CryptoOptionBacktestRequestDto request)
        {
            await EnsureDefaultConfigAsync(userId);
            var config = request.StrategyConfigId.HasValue
                ? await _context.CryptoOptionStrategyConfigs.Include(c => c.Legs).FirstOrDefaultAsync(c => c.Id == request.StrategyConfigId && c.UserId == userId)
                : await _context.CryptoOptionStrategyConfigs.Include(c => c.Legs).Where(c => c.UserId == userId).OrderBy(c => c.Id).FirstAsync();
            if (config == null) throw new InvalidOperationException("Crypto options strategy config not found.");
            if (!string.IsNullOrWhiteSpace(request.EntryTime)) config.EntryTime = request.EntryTime; if (!string.IsNullOrWhiteSpace(request.ExitTime)) config.ExitTime = request.ExitTime;
            if (request.TargetPremiumPerLeg.HasValue) config.TargetPremiumPerLeg = request.TargetPremiumPerLeg.Value; if (request.StopLossPercentPerLeg.HasValue) config.StopLossPercentPerLeg = request.StopLossPercentPerLeg.Value;
            if (!string.IsNullOrWhiteSpace(request.StrikeSelectionMode)) config.StrikeSelectionMode = request.StrikeSelectionMode; if (request.StrikeDistancePercent.HasValue) config.StrikeDistancePercent = request.StrikeDistancePercent.Value;
            if (request.MaxDailyLoss.HasValue) config.MaxDailyLoss = request.MaxDailyLoss.Value; if (request.LotSize.HasValue) config.LotSize = request.LotSize.Value;
            if (!string.IsNullOrWhiteSpace(request.Symbol)) config.Symbol = Normalize(request.Symbol); if (!string.IsNullOrWhiteSpace(request.Exchange)) config.Exchange = request.Exchange.Trim(); return config;
        }

        private async Task EnsureRunOwnedAsync(int userId, int runId) { if (!await _context.CryptoOptionBacktestRuns.AnyAsync(r => r.Id == runId && r.UserId == userId)) throw new InvalidOperationException("Crypto options backtest run not found."); }
        private async Task EnsureDefaultConfigAsync(int userId) { if (await _context.CryptoOptionStrategyConfigs.AnyAsync(c => c.UserId == userId)) return; var c = new CryptoOptionStrategyConfig { UserId = userId, Name = "BTC 0DTE Short Strangle", StrategyType = "ShortStrangle", Underlying = "BTC", Symbol = "BTCUSD", Exchange = "Delta Exchange", ExpiryType = "Today", EntryTime = "09:00", ExitTime = "17:15", TargetPremiumPerLeg = 100, StopLossPercentPerLeg = 100, StrikeSelectionMode = "PremiumBased", StrikeDistancePercent = 1.5m, MaxDailyLoss = 250, LotSize = 1, UseAtrFilter = true, AtrLength = 14, MaxAtrPercent = 1.2m, UseSlippage = true, SlippagePercent = 0.5m }; c.Legs.Add(new CryptoOptionStrategyLeg { LegName = "Short Call", Action = "Sell", OptionType = "CE", Quantity = 1, SortOrder = 1 }); c.Legs.Add(new CryptoOptionStrategyLeg { LegName = "Short Put", Action = "Sell", OptionType = "PE", Quantity = 1, SortOrder = 2 }); _context.CryptoOptionStrategyConfigs.Add(c); await _context.SaveChangesAsync(); }
        private static void ApplyConfig(CryptoOptionStrategyConfig c, UpsertCryptoOptionStrategyConfigDto d) { c.Name = string.IsNullOrWhiteSpace(d.Name) ? "Options Strategy" : d.Name.Trim(); c.StrategyType = string.IsNullOrWhiteSpace(d.StrategyType) ? "ShortStrangle" : d.StrategyType.Trim(); c.Underlying = Normalize(d.Underlying); c.Symbol = Normalize(d.Symbol); c.Exchange = d.Exchange.Trim(); c.ExpiryType = d.ExpiryType.Trim(); c.EntryTime = d.EntryTime.Trim(); c.ExitTime = d.ExitTime.Trim(); c.TimeZone = d.TimeZone.Trim(); c.TargetPremiumPerLeg = d.TargetPremiumPerLeg; c.StopLossPercentPerLeg = d.StopLossPercentPerLeg; c.StrikeSelectionMode = d.StrikeSelectionMode.Trim(); c.StrikeDistancePercent = d.StrikeDistancePercent; c.MaxDailyLoss = d.MaxDailyLoss; c.LotSize = d.LotSize; c.UseAtrFilter = d.UseAtrFilter; c.AtrLength = d.AtrLength; c.MaxAtrPercent = d.MaxAtrPercent; c.UseTrendFilter = d.UseTrendFilter; c.EmaLength = d.EmaLength; c.MaxTrendDistancePercent = d.MaxTrendDistancePercent; c.UseSlippage = d.UseSlippage; c.SlippagePercent = d.SlippagePercent; c.BrokeragePerOrder = d.BrokeragePerOrder; c.ExchangeFeePercent = d.ExchangeFeePercent; c.IsActive = d.IsActive; }
        private static void AddConfiguredLegs(CryptoOptionStrategyConfig c, UpsertCryptoOptionStrategyConfigDto d) { var legs = d.Legs.Count == 0 ? new List<UpsertCryptoOptionStrategyLegDto> { new() { LegName = "Short Call", Action = "Sell", OptionType = "CE", Quantity = 1, SortOrder = 1 }, new() { LegName = "Short Put", Action = "Sell", OptionType = "PE", Quantity = 1, SortOrder = 2 } } : d.Legs; foreach (var l in legs.OrderBy(x => x.SortOrder)) c.Legs.Add(new CryptoOptionStrategyLeg { LegName = l.LegName, Action = l.Action, OptionType = l.OptionType, ExpiryType = l.ExpiryType, StrikeSelectionMode = l.StrikeSelectionMode, TargetPremium = l.TargetPremium, StrikeDistancePercent = l.StrikeDistancePercent, Quantity = l.Quantity, SortOrder = l.SortOrder }); }
        private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
        private static CryptoOptionStrategyConfigDto ToConfigDto(CryptoOptionStrategyConfig c) => new() { Id = c.Id, Name = c.Name, StrategyType = c.StrategyType, Underlying = c.Underlying, Symbol = c.Symbol, Exchange = c.Exchange, ExpiryType = c.ExpiryType, EntryTime = c.EntryTime, ExitTime = c.ExitTime, TimeZone = c.TimeZone, TargetPremiumPerLeg = c.TargetPremiumPerLeg, StopLossPercentPerLeg = c.StopLossPercentPerLeg, StrikeSelectionMode = c.StrikeSelectionMode, StrikeDistancePercent = c.StrikeDistancePercent, MaxDailyLoss = c.MaxDailyLoss, LotSize = c.LotSize, UseAtrFilter = c.UseAtrFilter, AtrLength = c.AtrLength, MaxAtrPercent = c.MaxAtrPercent, UseTrendFilter = c.UseTrendFilter, EmaLength = c.EmaLength, MaxTrendDistancePercent = c.MaxTrendDistancePercent, UseSlippage = c.UseSlippage, SlippagePercent = c.SlippagePercent, BrokeragePerOrder = c.BrokeragePerOrder, ExchangeFeePercent = c.ExchangeFeePercent, IsActive = c.IsActive, Legs = c.Legs.OrderBy(l => l.SortOrder).Select(ToStrategyLegDto).ToList() };
        private static CryptoOptionStrategyLegDto ToStrategyLegDto(CryptoOptionStrategyLeg l) => new() { Id = l.Id, LegName = l.LegName, Action = l.Action, OptionType = l.OptionType, ExpiryType = l.ExpiryType, StrikeSelectionMode = l.StrikeSelectionMode, TargetPremium = l.TargetPremium, StrikeDistancePercent = l.StrikeDistancePercent, Quantity = l.Quantity, SortOrder = l.SortOrder };
        private static CryptoOptionChainSnapshotDto ToChainDto(CryptoOptionChainSnapshot s) => new() { Id = s.Id, Exchange = s.Exchange, Underlying = s.Underlying, Symbol = s.Symbol, ExpiryDate = s.ExpiryDate, Strike = s.Strike, UnderlyingPrice = s.UnderlyingPrice, SnapshotTime = s.SnapshotTime, CallPremium = s.CallPremium, CallBid = s.CallBid, CallAsk = s.CallAsk, CallVolume = s.CallVolume, CallOpenInterest = s.CallOpenInterest, CallIv = s.CallIv, CallDelta = s.CallDelta, CallGamma = s.CallGamma, CallTheta = s.CallTheta, CallVega = s.CallVega, CallRho = s.CallRho, PutPremium = s.PutPremium, PutBid = s.PutBid, PutAsk = s.PutAsk, PutVolume = s.PutVolume, PutOpenInterest = s.PutOpenInterest, PutIv = s.PutIv, PutDelta = s.PutDelta, PutGamma = s.PutGamma, PutTheta = s.PutTheta, PutVega = s.PutVega, PutRho = s.PutRho };
        private static CryptoOptionBacktestRunDto ToRunDto(CryptoOptionBacktestRun r) => new() { Id = r.Id, StrategyName = r.StrategyName, StrategyType = r.StrategyType, Symbol = r.Symbol, Exchange = r.Exchange, FromDate = r.FromDate, ToDate = r.ToDate, InitialCapital = r.InitialCapital, TotalPnl = r.TotalPnl, GrossPnl = r.GrossPnl, Charges = r.Charges, TotalTrades = r.TotalTrades, WinningDays = r.WinningDays, LosingDays = r.LosingDays, MaxDrawdown = r.MaxDrawdown, ProfitFactor = r.ProfitFactor, Status = r.Status, StartedAt = r.StartedAt, CompletedAt = r.CompletedAt, ErrorMessage = r.ErrorMessage };
        private static CryptoOptionBacktestPositionDto ToPositionDto(CryptoOptionBacktestPosition p) => new() { Id = p.Id, TradeDate = p.TradeDate, ExpiryDate = p.ExpiryDate, Underlying = p.Underlying, UnderlyingEntryPrice = p.UnderlyingEntryPrice, UnderlyingExitPrice = p.UnderlyingExitPrice, Status = p.Status, ExitReason = p.ExitReason, GrossPnl = p.GrossPnl, NetPnl = p.NetPnl, Charges = p.Charges, IsCircuitBreakerHit = p.IsCircuitBreakerHit, Legs = p.Legs.Select(ToLegDto).ToList() };
        private static CryptoOptionBacktestLegDto ToLegDto(CryptoOptionBacktestLeg l) => new() { Id = l.Id, BacktestPositionId = l.BacktestPositionId, LegType = l.LegType, Action = l.Action, Strike = l.Strike, ExpiryDate = l.ExpiryDate, EntryTime = l.EntryTime, EntryPremium = l.EntryPremium, ExitTime = l.ExitTime, ExitPremium = l.ExitPremium, Quantity = l.Quantity, Pnl = l.Pnl, Status = l.Status, ExitReason = l.ExitReason, StopLossHit = l.StopLossHit };
        private static CryptoOptionDailyPnlDto ToDailyPnlDto(CryptoOptionDailyPnl p) => new() { TradeDate = p.TradeDate, GrossPnl = p.GrossPnl, NetPnl = p.NetPnl, Charges = p.Charges, MaxIntradayLoss = p.MaxIntradayLoss, CeLegPnl = p.CeLegPnl, PeLegPnl = p.PeLegPnl, IsCircuitBreakerHit = p.IsCircuitBreakerHit, Notes = p.Notes };
        private static CryptoOptionScannerResultDto ToScannerDto(CryptoOptionScannerResult r) => new() { Exchange = r.Exchange, Symbol = r.Symbol, ScannerMode = r.ScannerMode, ScanTime = r.ScanTime, StrategyType = r.StrategyType, BestCallStrike = r.BestCallStrike, BestCallPremium = r.BestCallPremium, BestCallDelta = r.BestCallDelta, BestCallTheta = r.BestCallTheta, BestCallIv = r.BestCallIv, BestPutStrike = r.BestPutStrike, BestPutPremium = r.BestPutPremium, BestPutDelta = r.BestPutDelta, BestPutTheta = r.BestPutTheta, BestPutIv = r.BestPutIv, StrategyScore = r.StrategyScore, ProbabilityOfProfit = r.ProbabilityOfProfit, Notes = r.Notes };
    }
}





