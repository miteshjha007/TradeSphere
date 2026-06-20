using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;
using TradeSphere.Domain.Entities;
using TradeSphere.Infrastructure.Persistence;
using TradeSphere.Infrastructure.Security;

namespace TradeSphere.Infrastructure.Services
{
    public class BacktestService : IBacktestService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDeltaExchangeClient _deltaClient;
        private readonly IMt5BridgeClient _mt5BridgeClient;

        public BacktestService(ApplicationDbContext context, IDeltaExchangeClient deltaClient, IMt5BridgeClient mt5BridgeClient)
        {
            _context = context;
            _deltaClient = deltaClient;
            _mt5BridgeClient = mt5BridgeClient;
        }

        public async Task<List<BacktestDto>> GetUserBacktestsAsync(int userId)
        {
            var backtests = await _context.Backtests
                .Include(b => b.Strategy)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return backtests
                .Select(b => new BacktestDto
                {
                    Id = b.Id,
                    StrategyId = b.StrategyId ?? 0,
                    StrategyName = b.Strategy != null ? b.Strategy.Name : "Unknown",
                    Symbol = b.Symbol,
                    Interval = GetIntervalFromResultJson(b.ResultJson),
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    TotalReturn = b.TotalReturn ?? 0,
                    MaxDrawdown = b.MaxDrawdown ?? 0,
                    TradeCount = b.TotalTrades ?? 0,
                    Status = "Completed"
                })
                .ToList();
        }

        public async Task<BacktestResultDetailsDto> GetBacktestDetailsAsync(int userId, int backtestId)
        {
            var b = await _context.Backtests
                .Include(b => b.Strategy)
                .FirstOrDefaultAsync(x => x.Id == backtestId && x.UserId == userId);

            if (b == null) return null;

            return new BacktestResultDetailsDto
            {
                Id = b.Id,
                StrategyId = b.StrategyId ?? 0,
                StrategyName = b.Strategy != null ? b.Strategy.Name : "Unknown",
                Symbol = b.Symbol,
                Interval = GetIntervalFromResultJson(b.ResultJson),
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                TotalReturn = b.TotalReturn ?? 0,
                MaxDrawdown = b.MaxDrawdown ?? 0,
                TradeCount = b.TotalTrades ?? 0,
                Status = "Completed",
                ResultJson = b.ResultJson
            };
        }

        public class StrategyConfig
        {
            public int emaLength { get; set; } = 34;
            public string dailyTimeframe { get; set; } = "1d";
            public string resolution { get; set; } = "3m";
            public string exitMode { get; set; } = "Band-Based Exit";
            public decimal rrRatio { get; set; } = 2.0m;
            public bool useATRSL { get; set; } = false;
            public int atrLength { get; set; } = 14;
            public decimal atrMultiplier { get; set; } = 1.5m;
            public string sessionStart { get; set; } = "0915-1515";
            public int squareOffHour { get; set; } = 15;
            public int squareOffMinute { get; set; } = 15;
            public string tradeSizeType { get; set; } = "Contracts"; // "Contracts", "USD", "INR", "Margin_USD", "Margin_INR"
            public decimal tradeSizeValue { get; set; } = 1.0m;
            public decimal leverage { get; set; } = 10.0m;
        }

        public class Fib55EmaConfig
        {
            public int emaLength { get; set; } = 55;
            public string htfTimeframe { get; set; } = "60";
            public string resolution { get; set; } = "5m";
            public decimal fib381 { get; set; } = 0.382m;
            public decimal fib500 { get; set; } = 0.5m;
            public decimal fib618 { get; set; } = 0.618m;
            public decimal zoneBuffer { get; set; } = 0.0015m;
            public decimal minBodyPct { get; set; } = 30.0m;
            public int cooldownBars { get; set; } = 5;
            public int rsiBuyMin { get; set; } = 40;
            public int rsiSellMax { get; set; } = 60;
            public decimal volumeMultiplier { get; set; } = 1.0m;
            public decimal tp1RiskReward { get; set; } = 1.5m;
            public decimal tp2RiskReward { get; set; } = 2.0m;
            public decimal stopLossPct { get; set; } = 0.004m;
            public string tradeSizeType { get; set; } = "Contracts";
            public decimal tradeSizeValue { get; set; } = 1.0m;
            public decimal leverage { get; set; } = 10.0m;
        }

        public class PreviousDaySweepConfig
        {
            public string resolution { get; set; } = "5m";
            public bool enableLong { get; set; } = true;
            public bool enableShort { get; set; } = true;
            public decimal tp1RiskReward { get; set; } = 5.0m;
            public decimal tp2RiskReward { get; set; } = 10.0m;
            public decimal tp1ExitQtyPct { get; set; } = 80.0m;
            public int maxTradesPerDay { get; set; } = 3;
            public int maxLossesPerDay { get; set; } = 2;
            public decimal minSweepPoints { get; set; } = 0.0m;
            public bool useSession { get; set; } = false;
            public string tradeSession { get; set; } = "0000-2359";
            public bool showLevels { get; set; } = true;
            public string tradeSizeType { get; set; } = "Contracts";
            public decimal tradeSizeValue { get; set; } = 1.0m;
            public decimal leverage { get; set; } = 10.0m;
        }

        public class SmcDleConfig
        {
            public string resolution { get; set; } = "15m";
            public string higherTimeframe { get; set; } = "240";
            public string mediumTimeframe { get; set; } = "60";
            public int swingLookbackLength { get; set; } = 5;
            public decimal zoneWickTolerancePct { get; set; } = 30.0m;
            public bool requireEngulfing { get; set; } = true;
            public bool requirePinbar { get; set; } = true;
            public decimal pinbarWickToBodyRatio { get; set; } = 2.0m;
            public bool requireBiasCloseAfterRejection { get; set; } = false;
            public decimal riskPerTradePct { get; set; } = 1.0m;
            public decimal riskRewardRatio { get; set; } = 3.0m;
            public int maxTradesPerDay { get; set; } = 3;
            public string tradeSizeType { get; set; } = "RiskPercent";
            public decimal tradeSizeValue { get; set; } = 1.0m;
            public decimal leverage { get; set; } = 10.0m;
        }

        public class BacktestTradeLog
        {
            public string side { get; set; }
            public decimal entryPrice { get; set; }
            public decimal exitPrice { get; set; }
            public long entryTime { get; set; }
            public long exitTime { get; set; }
            public decimal pnl { get; set; }
            public decimal pnlPercent { get; set; }
            public string reason { get; set; }
        }

        public class EquityPoint
        {
            public long time { get; set; }
            public decimal equity { get; set; }
        }

        private static List<decimal> CalculateEMAList(List<decimal> prices, int length)
        {
            var emaList = new List<decimal>();
            if (prices.Count < length)
            {
                for (int i = 0; i < prices.Count; i++) emaList.Add(0m);
                return emaList;
            }

            decimal multiplier = 2m / (length + 1m);
            decimal sum = 0m;
            for (int i = 0; i < length; i++)
            {
                sum += prices[i];
            }
            decimal firstEma = sum / length;

            for (int i = 0; i < length - 1; i++)
            {
                emaList.Add(0m);
            }
            emaList.Add(firstEma);

            decimal currentEma = firstEma;
            for (int i = length; i < prices.Count; i++)
            {
                currentEma = (prices[i] - currentEma) * multiplier + currentEma;
                emaList.Add(currentEma);
            }

            return emaList;
        }

        private static List<decimal> CalculateATRList(List<CandleDto> candles, int length)
        {
            var atrList = new List<decimal>();
            if (candles.Count < length + 1)
            {
                for (int i = 0; i < candles.Count; i++) atrList.Add(0m);
                return atrList;
            }

            var tr = new List<decimal>();
            tr.Add(candles[0].High - candles[0].Low);

            for (int i = 1; i < candles.Count; i++)
            {
                decimal tr1 = candles[i].High - candles[i].Low;
                decimal tr2 = Math.Abs(candles[i].High - candles[i - 1].Close);
                decimal tr3 = Math.Abs(candles[i].Low - candles[i - 1].Close);
                tr.Add(Math.Max(tr1, Math.Max(tr2, tr3)));
            }

            decimal sum = 0m;
            for (int i = 0; i < length; i++)
            {
                sum += tr[i];
            }
            decimal firstAtr = sum / length;

            for (int i = 0; i < length - 1; i++)
            {
                atrList.Add(0m);
            }
            atrList.Add(firstAtr);

            decimal currentAtr = firstAtr;
            for (int i = length; i < tr.Count; i++)
            {
                currentAtr = (currentAtr * (length - 1) + tr[i]) / length;
                atrList.Add(currentAtr);
            }

            return atrList;
        }

        private static TimeZoneInfo GetIstTimezone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            }
        }

        public async Task<BacktestDto> RunBacktestAsync(int userId, RunBacktestDto dto)
        {
            var strategy = await _context.Strategies.FindAsync(dto.StrategyId);
            if (strategy == null)
            {
                throw new Exception("Strategy not found.");
            }

            // Convert input dates to UTC to prevent Npgsql and DateTimeOffset timezone issues
            DateTime startDateUtc = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
            DateTime endDateUtc = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);

            decimal totalReturn = 0m;
            decimal maxDrawdown = 0m;
            int tradeCount = 0;
            string resultJson = "{ \"equity\": [], \"trades\": [], \"diagnostics\": [] }";

            if (strategy.LogicType == "HA-EMA")
            {
                var configJson = string.IsNullOrEmpty(dto.ConfigOverrides) ? strategy.DefaultConfig : dto.ConfigOverrides;
                var config = JsonSerializer.Deserialize<StrategyConfig>(configJson) ?? new StrategyConfig();

                long dailyStart = new DateTimeOffset(startDateUtc.AddDays(-40)).ToUnixTimeSeconds();
                long dailyEnd = new DateTimeOffset(endDateUtc).ToUnixTimeSeconds();
                long intraStart = new DateTimeOffset(startDateUtc.AddDays(-10)).ToUnixTimeSeconds();
                long intraEnd = new DateTimeOffset(endDateUtc).ToUnixTimeSeconds();

                var dailyCandles = await GetBacktestCandlesAsync(userId, dto, dto.Symbol, "1d", dailyStart, dailyEnd);
                var intradayCandles = await GetBacktestCandlesAsync(userId, dto, dto.Symbol, dto.Interval ?? config.resolution, intraStart, intraEnd);

                dailyCandles = dailyCandles.OrderBy(c => c.Time).ToList();
                intradayCandles = intradayCandles.OrderBy(c => c.Time).ToList();
                var diagnostics = new List<string>();

                if (dto.InitialCapital <= 0m)
                {
                    throw new Exception("Initial capital must be greater than zero.");
                }

                if (dailyCandles.Count == 0)
                {
                    diagnostics.Add($"No daily candles returned for {dto.Symbol}. Check the exchange symbol and date range.");
                }

                if (intradayCandles.Count == 0)
                {
                    diagnostics.Add($"No intraday candles returned for {dto.Symbol} at {dto.Interval ?? config.resolution}. For Delta REST API use symbols like BTCUSD or ETHUSD.");
                }

                // Calculate Heikin Ashi for Daily
                var haOpen = new List<decimal>();
                var haClose = new List<decimal>();
                var haTimes = new List<long>();

                for (int i = 0; i < dailyCandles.Count; i++)
                {
                    decimal c = (dailyCandles[i].Open + dailyCandles[i].High + dailyCandles[i].Low + dailyCandles[i].Close) / 4m;
                    decimal o;
                    if (i == 0)
                    {
                        o = (dailyCandles[i].Open + dailyCandles[i].Close) / 2m;
                    }
                    else
                    {
                        o = (haOpen[i - 1] + haClose[i - 1]) / 2m;
                    }
                    haOpen.Add(o);
                    haClose.Add(c);
                    haTimes.Add(dailyCandles[i].Time);
                }

                var highs = intradayCandles.Select(c => c.High).ToList();
                var lows = intradayCandles.Select(c => c.Low).ToList();
                var closes = intradayCandles.Select(c => c.Close).ToList();

                var emaHigh = CalculateEMAList(highs, config.emaLength);
                var emaLow = CalculateEMAList(lows, config.emaLength);
                var atr = CalculateATRList(intradayCandles, config.atrLength);

                decimal capital = dto.InitialCapital;
                decimal initialCapital = dto.InitialCapital;
                int currentPosition = 0; // 0 = flat, 1 = long, -1 = short
                decimal entryPrice = 0m;
                decimal slPrice = 0m;
                decimal tpPrice = 0m;
                decimal riskAmount = 0m;
                long entryTime = 0;
                
                var tradesLog = new List<BacktestTradeLog>();
                var equityCurve = new List<EquityPoint>();

                var timezoneInfo = GetIstTimezone();

                for (int t = 1; t < intradayCandles.Count; t++)
                {
                    var candle = intradayCandles[t];

                    // Skip candles before start date
                    long startSec = new DateTimeOffset(startDateUtc).ToUnixTimeSeconds();
                    if (candle.Time < startSec) continue;

                    // Convert candle time to IST
                    var candleTimeUtc = DateTimeOffset.FromUnixTimeSeconds(candle.Time).UtcDateTime;
                    var istTime = TimeZoneInfo.ConvertTimeFromUtc(candleTimeUtc, timezoneInfo);

                    int currentTimeVal = istTime.Hour * 60 + istTime.Minute;
                    int sessionStartVal = 9 * 60 + 15;
                    int squareOffVal = config.squareOffHour * 60 + config.squareOffMinute;

                    bool isInSession = currentTimeVal >= sessionStartVal && currentTimeVal <= squareOffVal;
                    bool isPastSquareOff = currentTimeVal >= squareOffVal;

                    // Find daily Heikin Ashi candle for yesterday
                    var todayStartIst = new DateTimeOffset(istTime.Date, timezoneInfo.GetUtcOffset(istTime.Date));
                    var todayStartSec = todayStartIst.ToUnixTimeSeconds();
                    int dailyIndex = haTimes.FindLastIndex(time => time < todayStartSec);

                    int dailyBias = 0;
                    if (dailyIndex >= 0 && dailyIndex < haOpen.Count)
                    {
                        decimal haO = haOpen[dailyIndex];
                        decimal haC = haClose[dailyIndex];
                        if (haC > haO) dailyBias = 1;
                        else if (haC < haO) dailyBias = -1;
                    }

                    // Crossovers
                    bool crossAboveUpper = (closes[t] > emaHigh[t]) && (closes[t - 1] <= emaHigh[t - 1]);
                    bool crossBelowLower = (closes[t] < emaLow[t]) && (closes[t - 1] >= emaLow[t - 1]);

                    // Current position handling
                    if (currentPosition == 0)
                    {
                        if (dailyBias == 1 && crossAboveUpper && isInSession && !isPastSquareOff)
                        {
                            if (candle.Close <= 0m)
                            {
                                continue;
                            }

                            currentPosition = 1;
                            entryPrice = candle.Close;
                            entryTime = candle.Time;

                            if (config.useATRSL && t < atr.Count)
                            {
                                slPrice = entryPrice - (config.atrMultiplier * atr[t]);
                            }
                            else
                            {
                                slPrice = emaLow[t];
                            }
                            riskAmount = entryPrice - slPrice;
                            if (riskAmount <= 0) riskAmount = (t < atr.Count ? atr[t] : entryPrice * 0.01m) * config.atrMultiplier;
                            tpPrice = entryPrice + (riskAmount * config.rrRatio);
                        }
                        else if (dailyBias == -1 && crossBelowLower && isInSession && !isPastSquareOff)
                        {
                            if (candle.Close <= 0m)
                            {
                                continue;
                            }

                            currentPosition = -1;
                            entryPrice = candle.Close;
                            entryTime = candle.Time;

                            if (config.useATRSL && t < atr.Count)
                            {
                                slPrice = entryPrice + (config.atrMultiplier * atr[t]);
                            }
                            else
                            {
                                slPrice = emaHigh[t];
                            }
                            riskAmount = slPrice - entryPrice;
                            if (riskAmount <= 0) riskAmount = (t < atr.Count ? atr[t] : entryPrice * 0.01m) * config.atrMultiplier;
                            tpPrice = entryPrice - (riskAmount * config.rrRatio);
                        }
                    }
                    else if (currentPosition == 1)
                    {
                        bool exitTriggered = false;
                        decimal exitPriceVal = 0m;
                        string exitReason = "";

                        if (isPastSquareOff)
                        {
                            exitTriggered = true;
                            exitPriceVal = candle.Close;
                            exitReason = "Square-Off";
                        }
                        else if (config.exitMode == "Band-Based Exit" && candle.Close < emaLow[t])
                        {
                            exitTriggered = true;
                            exitPriceVal = candle.Close;
                            exitReason = "Band Exit";
                        }
                        else if (config.exitMode == "Fixed Risk-Reward Exit")
                        {
                            if (candle.Low <= slPrice)
                            {
                                exitTriggered = true;
                                exitPriceVal = slPrice;
                                exitReason = "Stop Loss";
                            }
                            else if (candle.High >= tpPrice)
                            {
                                exitTriggered = true;
                                exitPriceVal = tpPrice;
                                exitReason = "Take Profit";
                            }
                        }

                        if (exitTriggered)
                        {
                            if (entryPrice <= 0m || exitPriceVal <= 0m)
                            {
                                currentPosition = 0;
                                continue;
                            }

                            decimal pnlPercent = (exitPriceVal - entryPrice) / entryPrice;
                            decimal tradePnl = capital * pnlPercent;
                            capital += tradePnl;

                            tradesLog.Add(new BacktestTradeLog
                            {
                                side = "Long",
                                entryPrice = entryPrice,
                                exitPrice = exitPriceVal,
                                entryTime = entryTime,
                                exitTime = candle.Time,
                                pnl = tradePnl,
                                pnlPercent = pnlPercent * 100m,
                                reason = exitReason
                            });

                            currentPosition = 0;
                        }
                    }
                    else if (currentPosition == -1)
                    {
                        bool exitTriggered = false;
                        decimal exitPriceVal = 0m;
                        string exitReason = "";

                        if (isPastSquareOff)
                        {
                            exitTriggered = true;
                            exitPriceVal = candle.Close;
                            exitReason = "Square-Off";
                        }
                        else if (config.exitMode == "Band-Based Exit" && candle.Close > emaHigh[t])
                        {
                            exitTriggered = true;
                            exitPriceVal = candle.Close;
                            exitReason = "Band Exit";
                        }
                        else if (config.exitMode == "Fixed Risk-Reward Exit")
                        {
                            if (candle.High >= slPrice)
                            {
                                exitTriggered = true;
                                exitPriceVal = slPrice;
                                exitReason = "Stop Loss";
                            }
                            else if (candle.Low <= tpPrice)
                            {
                                exitTriggered = true;
                                exitPriceVal = tpPrice;
                                exitReason = "Take Profit";
                            }
                        }

                        if (exitTriggered)
                        {
                            if (entryPrice <= 0m || exitPriceVal <= 0m)
                            {
                                currentPosition = 0;
                                continue;
                            }

                            decimal pnlPercent = (entryPrice - exitPriceVal) / entryPrice;
                            decimal tradePnl = capital * pnlPercent;
                            capital += tradePnl;

                            tradesLog.Add(new BacktestTradeLog
                            {
                                side = "Short",
                                entryPrice = entryPrice,
                                exitPrice = exitPriceVal,
                                entryTime = entryTime,
                                exitTime = candle.Time,
                                pnl = tradePnl,
                                pnlPercent = pnlPercent * 100m,
                                reason = exitReason
                            });

                            currentPosition = 0;
                        }
                    }

                    equityCurve.Add(new EquityPoint
                    {
                        time = candle.Time,
                        equity = capital
                    });
                }

                totalReturn = initialCapital > 0m
                    ? ((capital - initialCapital) / initialCapital) * 100m
                    : 0m;
                tradeCount = tradesLog.Count;
                
                // Calculate max drawdown
                decimal peak = initialCapital;
                foreach (var pt in equityCurve)
                {
                    if (pt.equity > peak) peak = pt.equity;
                    if (peak > 0m)
                    {
                        decimal dd = (peak - pt.equity) / peak * 100m;
                        if (dd > maxDrawdown) maxDrawdown = dd;
                    }
                }

                resultJson = JsonSerializer.Serialize(new
                {
                    interval = dto.Interval,
                    initialCapital,
                    finalCapital = capital,
                    dailyCandles = dailyCandles.Count,
                    intradayCandles = intradayCandles.Count,
                    diagnostics,
                    trades = tradesLog,
                    equity = equityCurve
                });
            }
            else if (strategy.LogicType == "FIB-55-EMA")
            {
                var configJson = string.IsNullOrEmpty(dto.ConfigOverrides) ? strategy.DefaultConfig : dto.ConfigOverrides;
                (totalReturn, maxDrawdown, tradeCount, resultJson) = await RunFib55EmaBacktestAsync(userId, dto, configJson, startDateUtc, endDateUtc);
            }
            else if (strategy.LogicType == "PD-LIQUIDITY-SWEEP")
            {
                var configJson = string.IsNullOrEmpty(dto.ConfigOverrides) ? strategy.DefaultConfig : dto.ConfigOverrides;
                (totalReturn, maxDrawdown, tradeCount, resultJson) = await RunPreviousDaySweepBacktestAsync(userId, dto, configJson, startDateUtc, endDateUtc);
            }
            else if (strategy.LogicType == "SMC-DLE-MULTI-TF")
            {
                var configJson = string.IsNullOrEmpty(dto.ConfigOverrides) ? strategy.DefaultConfig : dto.ConfigOverrides;
                (totalReturn, maxDrawdown, tradeCount, resultJson) = await RunSmcDleBacktestAsync(userId, dto, configJson, startDateUtc, endDateUtc);
            }
            else
            {
                resultJson = JsonSerializer.Serialize(new
                {
                    interval = dto.Interval,
                    initialCapital = dto.InitialCapital,
                    finalCapital = dto.InitialCapital,
                    diagnostics = new[]
                    {
                        $"Backtest logic is not implemented yet for strategy type '{strategy.LogicType}'."
                    },
                    trades = Array.Empty<BacktestTradeLog>(),
                    equity = Array.Empty<EquityPoint>()
                });
            }

            var backtest = new Backtest
            {
                UserId = userId,
                StrategyId = dto.StrategyId,
                Symbol = dto.Symbol,
                StartDate = startDateUtc,
                EndDate = endDateUtc,
                TotalReturn = totalReturn,
                MaxDrawdown = maxDrawdown,
                TotalTrades = tradeCount,
                ResultJson = resultJson
            };

            _context.Backtests.Add(backtest);
            await _context.SaveChangesAsync();
            
            await _context.Entry(backtest).Reference(b => b.Strategy).LoadAsync();

            return new BacktestDto
            {
                Id = backtest.Id,
                StrategyId = backtest.StrategyId ?? 0,
                StrategyName = backtest.Strategy != null ? backtest.Strategy.Name : "Unknown",
                Symbol = backtest.Symbol,
                Interval = dto.Interval,
                StartDate = backtest.StartDate,
                EndDate = backtest.EndDate,
                TotalReturn = backtest.TotalReturn ?? 0,
                MaxDrawdown = backtest.MaxDrawdown ?? 0,
                TradeCount = backtest.TotalTrades ?? 0,
                Status = "Completed"
            };
        }

        private async Task<(decimal totalReturn, decimal maxDrawdown, int tradeCount, string resultJson)> RunFib55EmaBacktestAsync(
            int userId,
            RunBacktestDto dto,
            string configJson,
            DateTime startDateUtc,
            DateTime endDateUtc)
        {
            var config = JsonSerializer.Deserialize<Fib55EmaConfig>(configJson) ?? new Fib55EmaConfig();
            var interval = dto.Interval ?? config.resolution;
            var diagnostics = new List<string>
            {
                "Delta candle data does not include volume, so the TradingView volume filter is treated as passed."
            };

            var startSec = new DateTimeOffset(startDateUtc).ToUnixTimeSeconds();
            var endSec = new DateTimeOffset(endDateUtc).ToUnixTimeSeconds();
            var historyStart = new DateTimeOffset(startDateUtc.AddDays(-20)).ToUnixTimeSeconds();
            var htfResolution = NormalizeResolution(config.htfTimeframe);

            var candles = (await GetBacktestCandlesAsync(userId, dto, dto.Symbol, interval, historyStart, endSec)).OrderBy(c => c.Time).ToList();
            var htfCandles = (await GetBacktestCandlesAsync(userId, dto, dto.Symbol, htfResolution, historyStart, endSec)).OrderBy(c => c.Time).ToList();

            if (dto.InitialCapital <= 0m) throw new Exception("Initial capital must be greater than zero.");
            if (candles.Count == 0) diagnostics.Add($"No intraday candles returned for {dto.Symbol} at {interval}.");
            if (htfCandles.Count == 0) diagnostics.Add($"No HTF candles returned for {dto.Symbol} at {htfResolution}.");

            var closes = candles.Select(c => c.Close).ToList();
            var htfCloses = htfCandles.Select(c => c.Close).ToList();
            var ema = CalculateEMAList(closes, config.emaLength);
            var htfEma = CalculateEMAList(htfCloses, config.emaLength);
            var rsi = CalculateRSIList(closes, 14);

            decimal capital = dto.InitialCapital;
            var trades = new List<BacktestTradeLog>();
            var equity = new List<EquityPoint>();
            int position = 0;
            decimal entry = 0m, sl = 0m, tp = 0m;
            long entryTime = 0;
            int lastSignalBar = -999;

            for (int i = Math.Max(config.emaLength + 2, 20); i < candles.Count; i++)
            {
                var candle = candles[i];
                if (candle.Time < startSec) continue;

                var htfIndex = FindLastCandleIndex(htfCandles, candle.Time);
                if (htfIndex < config.emaLength || htfIndex < 0) continue;

                if (position != 0)
                {
                    bool exit = false;
                    decimal exitPrice = candle.Close;
                    string reason = "";

                    if (position == 1)
                    {
                        if (candle.Low <= sl)
                        {
                            exit = true;
                            exitPrice = sl;
                            reason = "Stop Loss";
                        }
                        else if (candle.High >= tp)
                        {
                            exit = true;
                            exitPrice = tp;
                            reason = "Take Profit";
                        }
                    }
                    else
                    {
                        if (candle.High >= sl)
                        {
                            exit = true;
                            exitPrice = sl;
                            reason = "Stop Loss";
                        }
                        else if (candle.Low <= tp)
                        {
                            exit = true;
                            exitPrice = tp;
                            reason = "Take Profit";
                        }
                    }

                    if (exit)
                    {
                        var pnlPct = position == 1 ? (exitPrice - entry) / entry : (entry - exitPrice) / entry;
                        var pnl = capital * pnlPct;
                        capital += pnl;
                        trades.Add(new BacktestTradeLog
                        {
                            side = position == 1 ? "Long" : "Short",
                            entryPrice = entry,
                            exitPrice = exitPrice,
                            entryTime = entryTime,
                            exitTime = candle.Time,
                            pnl = pnl,
                            pnlPercent = pnlPct * 100m,
                            reason = reason
                        });
                        position = 0;
                    }
                }

                if (position == 0)
                {
                    var htfBull = htfCandles[htfIndex].Close > htfEma[htfIndex];
                    var htfBear = htfCandles[htfIndex].Close < htfEma[htfIndex];
                    var ltfBull = candle.Close > ema[i];
                    var ltfBear = candle.Close < ema[i];

                    var hourly = htfCandles[htfIndex];
                    var fibRange = hourly.High - hourly.Low;
                    if (fibRange <= 0m) continue;

                    var s618 = hourly.High - fibRange * config.fib618;
                    var s500 = hourly.High - fibRange * config.fib500;
                    var s382 = hourly.High - fibRange * config.fib381;
                    var b618 = hourly.Low + fibRange * config.fib618;
                    var b500 = hourly.Low + fibRange * config.fib500;
                    var b382 = hourly.Low + fibRange * config.fib381;

                    bool hourlyGreen = hourly.Close >= hourly.Open;
                    bool hourlyRed = hourly.Close < hourly.Open;
                    bool nearSellFib = IsNear(candle.Close, s618, config.zoneBuffer) || IsNear(candle.Close, s500, config.zoneBuffer) || IsNear(candle.Close, s382, config.zoneBuffer);
                    bool nearBuyFib = IsNear(candle.Close, b618, config.zoneBuffer) || IsNear(candle.Close, b500, config.zoneBuffer) || IsNear(candle.Close, b382, config.zoneBuffer);

                    var range = candle.High - candle.Low;
                    var body = Math.Abs(candle.Close - candle.Open);
                    bool strongCandle = range > 0m && body / range * 100m >= config.minBodyPct;
                    bool rejection = candle.Close < candle.Open && candles[i - 1].Close > candles[i - 1].Open && strongCandle;
                    bool bounce = candle.Close > candle.Open && candles[i - 1].Close < candles[i - 1].Open && strongCandle;
                    bool rsiBuy = rsi[i] >= config.rsiBuyMin && rsi[i] < 70m;
                    bool rsiSell = rsi[i] <= config.rsiSellMax && rsi[i] > 30m;
                    bool cooldownOk = i - lastSignalBar >= config.cooldownBars;

                    bool buy = htfBull && ltfBull && hourlyGreen && nearBuyFib && bounce && rsiBuy && cooldownOk;
                    bool sell = htfBear && ltfBear && hourlyRed && nearSellFib && rejection && rsiSell && cooldownOk;

                    if (buy || sell)
                    {
                        position = buy ? 1 : -1;
                        entry = candle.Close;
                        entryTime = candle.Time;
                        lastSignalBar = i;
                        if (position == 1)
                        {
                            sl = entry - entry * config.stopLossPct;
                            tp = entry + (entry - sl) * config.tp2RiskReward;
                        }
                        else
                        {
                            sl = entry + entry * config.stopLossPct;
                            tp = entry - (sl - entry) * config.tp2RiskReward;
                        }
                    }
                }

                equity.Add(new EquityPoint { time = candle.Time, equity = capital });
            }

            return BuildBacktestResultJson(dto, interval, dto.InitialCapital, capital, candles.Count, htfCandles.Count, diagnostics, trades, equity);
        }

        private async Task<(decimal totalReturn, decimal maxDrawdown, int tradeCount, string resultJson)> RunPreviousDaySweepBacktestAsync(
            int userId,
            RunBacktestDto dto,
            string configJson,
            DateTime startDateUtc,
            DateTime endDateUtc)
        {
            var config = JsonSerializer.Deserialize<PreviousDaySweepConfig>(configJson) ?? new PreviousDaySweepConfig();
            var interval = dto.Interval ?? config.resolution;
            var diagnostics = new List<string>();
            var startSec = new DateTimeOffset(startDateUtc).ToUnixTimeSeconds();
            var endSec = new DateTimeOffset(endDateUtc).ToUnixTimeSeconds();
            var historyStart = new DateTimeOffset(startDateUtc.AddDays(-5)).ToUnixTimeSeconds();
            var candles = (await GetBacktestCandlesAsync(userId, dto, dto.Symbol, interval, historyStart, endSec)).OrderBy(c => c.Time).ToList();
            var daily = (await GetBacktestCandlesAsync(userId, dto, dto.Symbol, "1d", historyStart, endSec)).OrderBy(c => c.Time).ToList();

            if (candles.Count == 0) diagnostics.Add($"No intraday candles returned for {dto.Symbol} at {interval}.");
            if (daily.Count == 0) diagnostics.Add($"No daily candles returned for {dto.Symbol}.");

            var tz = GetIstTimezone();
            decimal capital = dto.InitialCapital;
            var trades = new List<BacktestTradeLog>();
            var equity = new List<EquityPoint>();
            int position = 0, tradesToday = 0, lossesToday = 0;
            DateTime currentDay = DateTime.MinValue;
            bool sweptHigh = false, sweptLow = false, shortReady = false, longReady = false;
            decimal shortHigh = 0m, shortLow = 0m, longHigh = 0m, longLow = 0m;
            decimal entry = 0m, sl = 0m, tp1 = 0m, tp2 = 0m;
            long entryTime = 0;

            for (int i = 1; i < candles.Count; i++)
            {
                var c = candles[i];
                if (c.Time < startSec) continue;

                var ist = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(c.Time).UtcDateTime, tz);
                if (ist.Date != currentDay)
                {
                    currentDay = ist.Date;
                    tradesToday = 0;
                    lossesToday = 0;
                    sweptHigh = sweptLow = shortReady = longReady = false;
                }

                var dailyIndex = FindLastCandleIndex(daily, new DateTimeOffset(ist.Date, tz.GetUtcOffset(ist.Date)).ToUnixTimeSeconds() - 1);
                if (dailyIndex < 0) continue;

                var pdh = daily[dailyIndex].High;
                var pdl = daily[dailyIndex].Low;
                bool inSession = !config.useSession || IsInSession(ist, config.tradeSession);

                if (position != 0)
                {
                    bool exit = false;
                    decimal exitPrice = c.Close;
                    string reason = "";
                    if (position == 1)
                    {
                        if (c.Low <= sl)
                        {
                            exit = true;
                            exitPrice = sl;
                            reason = "Stop Loss";
                        }
                        else if (c.High >= tp2)
                        {
                            exit = true;
                            exitPrice = tp2;
                            reason = "TP2";
                        }
                        else if (c.High >= tp1)
                        {
                            exit = true;
                            exitPrice = tp1;
                            reason = "TP1";
                        }
                    }
                    else
                    {
                        if (c.High >= sl)
                        {
                            exit = true;
                            exitPrice = sl;
                            reason = "Stop Loss";
                        }
                        else if (c.Low <= tp2)
                        {
                            exit = true;
                            exitPrice = tp2;
                            reason = "TP2";
                        }
                        else if (c.Low <= tp1)
                        {
                            exit = true;
                            exitPrice = tp1;
                            reason = "TP1";
                        }
                    }

                    if (exit)
                    {
                        var pnlPct = position == 1 ? (exitPrice - entry) / entry : (entry - exitPrice) / entry;
                        var pnl = capital * pnlPct;
                        capital += pnl;
                        if (pnl < 0m) lossesToday++;
                        trades.Add(new BacktestTradeLog { side = position == 1 ? "Long" : "Short", entryPrice = entry, exitPrice = exitPrice, entryTime = entryTime, exitTime = c.Time, pnl = pnl, pnlPercent = pnlPct * 100m, reason = reason });
                        position = 0;
                    }
                }

                bool canTrade = inSession && tradesToday < config.maxTradesPerDay && lossesToday < config.maxLossesPerDay && position == 0;
                if (c.High > pdh + config.minSweepPoints) sweptHigh = true;
                if (c.Low < pdl - config.minSweepPoints) sweptLow = true;

                bool bearish = c.Close < c.Open;
                bool bullish = c.Close > c.Open;

                if (canTrade && config.enableShort && sweptHigh && bearish && c.Close > pdl)
                {
                    shortHigh = c.High;
                    shortLow = c.Low;
                    shortReady = true;
                }
                if (canTrade && config.enableLong && sweptLow && bullish && c.Close < pdh)
                {
                    longHigh = c.High;
                    longLow = c.Low;
                    longReady = true;
                }

                if (canTrade && config.enableShort && shortReady && c.Low < shortLow)
                {
                    entry = shortLow;
                    sl = shortHigh;
                    var risk = sl - entry;
                    if (risk > 0m)
                    {
                        tp1 = entry - risk * config.tp1RiskReward;
                        tp2 = entry - risk * config.tp2RiskReward;
                        position = -1;
                        entryTime = c.Time;
                        tradesToday++;
                        shortReady = false;
                        sweptHigh = false;
                    }
                }
                else if (canTrade && config.enableLong && longReady && c.High > longHigh)
                {
                    entry = longHigh;
                    sl = longLow;
                    var risk = entry - sl;
                    if (risk > 0m)
                    {
                        tp1 = entry + risk * config.tp1RiskReward;
                        tp2 = entry + risk * config.tp2RiskReward;
                        position = 1;
                        entryTime = c.Time;
                        tradesToday++;
                        longReady = false;
                        sweptLow = false;
                    }
                }

                equity.Add(new EquityPoint { time = c.Time, equity = capital });
            }

            return BuildBacktestResultJson(dto, interval, dto.InitialCapital, capital, candles.Count, daily.Count, diagnostics, trades, equity);
        }

        private async Task<(decimal totalReturn, decimal maxDrawdown, int tradeCount, string resultJson)> RunSmcDleBacktestAsync(
            int userId,
            RunBacktestDto dto,
            string configJson,
            DateTime startDateUtc,
            DateTime endDateUtc)
        {
            var config = JsonSerializer.Deserialize<SmcDleConfig>(configJson) ?? new SmcDleConfig();
            var interval = dto.Interval ?? config.resolution;
            var diagnostics = new List<string>();
            var startSec = new DateTimeOffset(startDateUtc).ToUnixTimeSeconds();
            var endSec = new DateTimeOffset(endDateUtc).ToUnixTimeSeconds();
            var historyStart = new DateTimeOffset(startDateUtc.AddDays(-45)).ToUnixTimeSeconds();
            var htf = (await GetBacktestCandlesAsync(userId, dto, dto.Symbol, NormalizeResolution(config.higherTimeframe), historyStart, endSec)).OrderBy(c => c.Time).ToList();
            var mtf = (await GetBacktestCandlesAsync(userId, dto, dto.Symbol, NormalizeResolution(config.mediumTimeframe), historyStart, endSec)).OrderBy(c => c.Time).ToList();
            var ltf = (await GetBacktestCandlesAsync(userId, dto, dto.Symbol, interval, historyStart, endSec)).OrderBy(c => c.Time).ToList();

            if (ltf.Count == 0) diagnostics.Add($"No execution candles returned for {dto.Symbol} at {interval}.");
            if (htf.Count == 0) diagnostics.Add($"No HTF candles returned for {dto.Symbol}.");
            if (mtf.Count == 0) diagnostics.Add($"No MTF candles returned for {dto.Symbol}.");

            decimal capital = dto.InitialCapital;
            var trades = new List<BacktestTradeLog>();
            var equity = new List<EquityPoint>();
            int position = 0, dailyTrades = 0, htfBias = 0;
            DateTime currentDay = DateTime.MinValue;
            decimal lastSwingHigh = 0m, lastSwingLow = 0m, equilibrium = 0m;
            bool hasHigh = false, hasLow = false;
            decimal demandTop = 0m, demandBottom = 0m, supplyTop = 0m, supplyBottom = 0m;
            bool demandMitigated = true, supplyMitigated = true;
            bool longTap = false, shortTap = false;
            decimal entry = 0m, sl = 0m, tp = 0m;
            long entryTime = 0;
            var tz = GetIstTimezone();

            for (int i = 2; i < ltf.Count; i++)
            {
                var c = ltf[i];
                if (c.Time < startSec) continue;

                var ist = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(c.Time).UtcDateTime, tz);
                if (ist.Date != currentDay)
                {
                    currentDay = ist.Date;
                    dailyTrades = 0;
                }

                var htfIndex = FindLastCandleIndex(htf, c.Time);
                if (htfIndex > config.swingLookbackLength * 2)
                {
                    var pivotIndex = htfIndex - config.swingLookbackLength;
                    if (IsPivotHigh(htf, pivotIndex, config.swingLookbackLength))
                    {
                        lastSwingHigh = htf[pivotIndex].High;
                        hasHigh = true;
                    }
                    if (IsPivotLow(htf, pivotIndex, config.swingLookbackLength))
                    {
                        lastSwingLow = htf[pivotIndex].Low;
                        hasLow = true;
                    }
                    if (hasHigh && htf[htfIndex].Close > lastSwingHigh) htfBias = 1;
                    if (hasLow && htf[htfIndex].Close < lastSwingLow) htfBias = -1;
                    if (htfBias != 0 && hasHigh && hasLow) equilibrium = (lastSwingHigh + lastSwingLow) / 2m;
                }

                var mtfIndex = FindLastCandleIndex(mtf, c.Time);
                if (mtfIndex >= 0)
                {
                    var m = mtf[mtfIndex];
                    var mRange = m.High - m.Low;
                    var mBody = Math.Abs(m.Close - m.Open);
                    if (mRange > 0m && mBody > 0m)
                    {
                        var lowerWick = Math.Min(m.Open, m.Close) - m.Low;
                        var upperWick = m.High - Math.Max(m.Open, m.Close);
                        if (m.Close > m.Open && lowerWick / mRange * 100m >= 100m - config.zoneWickTolerancePct)
                        {
                            demandTop = m.Open;
                            demandBottom = m.Low;
                            demandMitigated = false;
                        }
                        if (m.Close < m.Open && upperWick / mRange * 100m >= 100m - config.zoneWickTolerancePct)
                        {
                            supplyTop = m.High;
                            supplyBottom = m.Open;
                            supplyMitigated = false;
                        }
                    }
                }

                if (!demandMitigated && c.Close < demandBottom) demandMitigated = true;
                if (!supplyMitigated && c.Close > supplyTop) supplyMitigated = true;

                if (position != 0)
                {
                    bool exit = false;
                    decimal exitPrice = c.Close;
                    string reason = "";
                    if (position == 1)
                    {
                        if (c.Low <= sl) { exit = true; exitPrice = sl; reason = "Stop Loss"; }
                        else if (c.High >= tp) { exit = true; exitPrice = tp; reason = "Take Profit"; }
                    }
                    else
                    {
                        if (c.High >= sl) { exit = true; exitPrice = sl; reason = "Stop Loss"; }
                        else if (c.Low <= tp) { exit = true; exitPrice = tp; reason = "Take Profit"; }
                    }
                    if (exit)
                    {
                        var pnlPct = position == 1 ? (exitPrice - entry) / entry : (entry - exitPrice) / entry;
                        var pnl = capital * pnlPct;
                        capital += pnl;
                        trades.Add(new BacktestTradeLog { side = position == 1 ? "Long" : "Short", entryPrice = entry, exitPrice = exitPrice, entryTime = entryTime, exitTime = c.Time, pnl = pnl, pnlPercent = pnlPct * 100m, reason = reason });
                        position = 0;
                    }
                }

                bool inDiscount = equilibrium > 0m && c.Close < equilibrium;
                bool inPremium = equilibrium > 0m && c.Close > equilibrium;
                bool validDemand = !demandMitigated && demandTop > 0m && equilibrium > 0m && demandTop <= equilibrium;
                bool validSupply = !supplyMitigated && supplyTop > 0m && equilibrium > 0m && supplyBottom >= equilibrium;
                bool tapDemand = validDemand && c.Low <= demandTop * 1.002m && c.Close >= demandBottom;
                bool tapSupply = validSupply && c.High >= supplyBottom * 0.998m && c.Close <= supplyTop;
                bool biasLong = htfBias == 1 && inDiscount;
                bool biasShort = htfBias == -1 && inPremium;
                if (biasLong && tapDemand) longTap = true;
                if (biasShort && tapSupply) shortTap = true;
                if (!biasLong || !tapDemand) longTap = false;
                if (!biasShort || !tapSupply) shortTap = false;

                bool bullRejection = longTap && ((config.requireEngulfing && IsBullishEngulf(ltf, i)) || (config.requirePinbar && IsBullishPinbar(c, config.pinbarWickToBodyRatio)) || (!config.requireEngulfing && !config.requirePinbar && c.Close > c.Open));
                bool bearRejection = shortTap && ((config.requireEngulfing && IsBearishEngulf(ltf, i)) || (config.requirePinbar && IsBearishPinbar(c, config.pinbarWickToBodyRatio)) || (!config.requireEngulfing && !config.requirePinbar && c.Close < c.Open));
                bool canTrade = dailyTrades < config.maxTradesPerDay && position == 0;

                if (canTrade && bullRejection)
                {
                    sl = c.Low;
                    var risk = c.Close - sl;
                    if (risk <= 0m) risk = c.Close * 0.005m;
                    entry = c.Close;
                    tp = entry + risk * config.riskRewardRatio;
                    position = 1;
                    entryTime = c.Time;
                    dailyTrades++;
                    longTap = false;
                }
                else if (canTrade && bearRejection)
                {
                    sl = c.High;
                    var risk = sl - c.Close;
                    if (risk <= 0m) risk = c.Close * 0.005m;
                    entry = c.Close;
                    tp = entry - risk * config.riskRewardRatio;
                    position = -1;
                    entryTime = c.Time;
                    dailyTrades++;
                    shortTap = false;
                }

                equity.Add(new EquityPoint { time = c.Time, equity = capital });
            }

            return BuildBacktestResultJson(dto, interval, dto.InitialCapital, capital, ltf.Count, htf.Count + mtf.Count, diagnostics, trades, equity);
        }

        private (decimal totalReturn, decimal maxDrawdown, int tradeCount, string resultJson) BuildBacktestResultJson(
            RunBacktestDto dto,
            string interval,
            decimal initialCapital,
            decimal finalCapital,
            int intradayCandles,
            int higherTimeframeCandles,
            List<string> diagnostics,
            List<BacktestTradeLog> trades,
            List<EquityPoint> equity)
        {
            var totalReturn = initialCapital > 0m ? (finalCapital - initialCapital) / initialCapital * 100m : 0m;
            var maxDrawdown = CalculateMaxDrawdown(initialCapital, equity);
            var resultJson = JsonSerializer.Serialize(new
            {
                interval,
                initialCapital,
                finalCapital,
                dailyCandles = higherTimeframeCandles,
                intradayCandles,
                diagnostics,
                trades,
                equity
            });

            return (totalReturn, maxDrawdown, trades.Count, resultJson);
        }

        private async Task<List<CandleDto>> GetBacktestCandlesAsync(
            int userId,
            RunBacktestDto dto,
            string symbol,
            string resolution,
            long startTime,
            long endTime)
        {
            if (!string.Equals(dto.DataSource, "MT5", StringComparison.OrdinalIgnoreCase))
            {
                return await _deltaClient.GetCandlesAsync(symbol, resolution, startTime, endTime);
            }

            if (!dto.Mt5AccountId.HasValue)
            {
                throw new Exception("MT5 account is required when backtest data source is MT5.");
            }

            var account = await _context.Mt5Accounts.FirstOrDefaultAsync(a => a.Id == dto.Mt5AccountId.Value && a.UserId == userId);
            if (account == null)
            {
                throw new Exception("MT5 account not found or does not belong to this user.");
            }

            var brokerSymbol = await ResolveMt5BrokerSymbolAsync(userId, account.Id, symbol);
            var result = await _mt5BridgeClient.GetCandlesAsync(new Mt5BridgeCandlesRequestDto
            {
                Login = account.Login,
                Server = account.Server,
                Password = EncryptionHelper.Decrypt(account.EncryptedPassword),
                Symbol = brokerSymbol,
                Resolution = NormalizeResolution(resolution),
                StartTime = startTime,
                EndTime = endTime
            });

            if (!result.Success)
            {
                throw new Exception($"MT5 candle request failed for {brokerSymbol}: {result.Message}");
            }

            return result.Candles
                .Select(c => new CandleDto
                {
                    Time = c.Time,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close
                })
                .ToList();
        }

        private async Task<string> ResolveMt5BrokerSymbolAsync(int userId, int accountId, string strategySymbol)
        {
            var normalized = (strategySymbol ?? string.Empty).Trim().ToUpperInvariant();
            var mapping = await _context.Mt5SymbolMappings
                .FirstOrDefaultAsync(m => m.UserId == userId && m.Mt5AccountId == accountId && m.StrategySymbol == normalized && m.IsActive);
            return string.IsNullOrWhiteSpace(mapping?.BrokerSymbol) ? strategySymbol : mapping.BrokerSymbol;
        }

        private static decimal CalculateMaxDrawdown(decimal initialCapital, List<EquityPoint> equity)
        {
            decimal peak = initialCapital;
            decimal maxDrawdown = 0m;
            foreach (var point in equity)
            {
                if (point.equity > peak) peak = point.equity;
                if (peak <= 0m) continue;
                var dd = (peak - point.equity) / peak * 100m;
                if (dd > maxDrawdown) maxDrawdown = dd;
            }
            return maxDrawdown;
        }

        private static string NormalizeResolution(string resolution)
        {
            return resolution switch
            {
                "1" => "1m",
                "3" => "3m",
                "5" => "5m",
                "15" => "15m",
                "30" => "30m",
                "60" => "1h",
                "120" => "2h",
                "240" => "4h",
                "D" => "1d",
                "1D" => "1d",
                _ => resolution
            };
        }

        private static int FindLastCandleIndex(List<CandleDto> candles, long time)
        {
            for (int i = candles.Count - 1; i >= 0; i--)
            {
                if (candles[i].Time <= time) return i;
            }
            return -1;
        }

        private static bool IsNear(decimal price, decimal level, decimal buffer)
        {
            if (level <= 0m) return false;
            return price >= level * (1m - buffer) && price <= level * (1m + buffer);
        }

        private static List<decimal> CalculateRSIList(List<decimal> closes, int length)
        {
            var rsi = Enumerable.Repeat(50m, closes.Count).ToList();
            if (closes.Count <= length) return rsi;

            decimal gain = 0m;
            decimal loss = 0m;
            for (int i = 1; i <= length; i++)
            {
                var change = closes[i] - closes[i - 1];
                if (change >= 0m) gain += change;
                else loss -= change;
            }
            gain /= length;
            loss /= length;

            for (int i = length + 1; i < closes.Count; i++)
            {
                var change = closes[i] - closes[i - 1];
                var currentGain = change > 0m ? change : 0m;
                var currentLoss = change < 0m ? -change : 0m;
                gain = (gain * (length - 1) + currentGain) / length;
                loss = (loss * (length - 1) + currentLoss) / length;
                rsi[i] = loss == 0m ? 100m : 100m - (100m / (1m + gain / loss));
            }

            return rsi;
        }

        private static bool IsInSession(DateTime time, string session)
        {
            if (string.IsNullOrWhiteSpace(session) || !session.Contains('-')) return true;
            var parts = session.Split('-');
            if (parts.Length != 2 || parts[0].Length < 4 || parts[1].Length < 4) return true;
            if (!int.TryParse(parts[0][..2], out var startHour) || !int.TryParse(parts[0].Substring(2, 2), out var startMinute)) return true;
            if (!int.TryParse(parts[1][..2], out var endHour) || !int.TryParse(parts[1].Substring(2, 2), out var endMinute)) return true;
            var value = time.Hour * 60 + time.Minute;
            var start = startHour * 60 + startMinute;
            var end = endHour * 60 + endMinute;
            return value >= start && value <= end;
        }

        private static bool IsPivotHigh(List<CandleDto> candles, int index, int length)
        {
            if (index < length || index + length >= candles.Count) return false;
            var value = candles[index].High;
            for (int i = index - length; i <= index + length; i++)
            {
                if (i != index && candles[i].High >= value) return false;
            }
            return true;
        }

        private static bool IsPivotLow(List<CandleDto> candles, int index, int length)
        {
            if (index < length || index + length >= candles.Count) return false;
            var value = candles[index].Low;
            for (int i = index - length; i <= index + length; i++)
            {
                if (i != index && candles[i].Low <= value) return false;
            }
            return true;
        }

        private static bool IsBullishEngulf(List<CandleDto> candles, int index)
        {
            var prev = candles[index - 1];
            var current = candles[index];
            return prev.Close < prev.Open && current.Close > current.Open && current.Close > prev.Close && current.Open <= prev.Open;
        }

        private static bool IsBearishEngulf(List<CandleDto> candles, int index)
        {
            var prev = candles[index - 1];
            var current = candles[index];
            return prev.Close > prev.Open && current.Close < current.Open && current.Close < prev.Close && current.Open >= prev.Open;
        }

        private static bool IsBullishPinbar(CandleDto candle, decimal ratio)
        {
            var body = Math.Abs(candle.Close - candle.Open);
            if (body <= 0m) return false;
            var lowerWick = Math.Min(candle.Close, candle.Open) - candle.Low;
            var upperWick = candle.High - Math.Max(candle.Close, candle.Open);
            return lowerWick >= body * ratio && lowerWick > upperWick && candle.Close > candle.Open;
        }

        private static bool IsBearishPinbar(CandleDto candle, decimal ratio)
        {
            var body = Math.Abs(candle.Close - candle.Open);
            if (body <= 0m) return false;
            var upperWick = candle.High - Math.Max(candle.Close, candle.Open);
            var lowerWick = Math.Min(candle.Close, candle.Open) - candle.Low;
            return upperWick >= body * ratio && upperWick > lowerWick && candle.Close < candle.Open;
        }

        private static string GetIntervalFromResultJson(string? resultJson)
        {
            if (string.IsNullOrWhiteSpace(resultJson))
            {
                return "1h";
            }

            try
            {
                using var document = JsonDocument.Parse(resultJson);
                if (document.RootElement.TryGetProperty("interval", out var intervalElement))
                {
                    var interval = intervalElement.GetString();
                    if (!string.IsNullOrWhiteSpace(interval))
                    {
                        return interval;
                    }
                }
            }
            catch (JsonException)
            {
                // Older/corrupt result rows should not break the backtest page.
            }

            return "1h";
        }

        public async Task DeleteBacktestAsync(int userId, int backtestId)
        {
            var entity = await _context.Backtests
                .FirstOrDefaultAsync(b => b.Id == backtestId && b.UserId == userId);

            if (entity != null)
            {
                _context.Backtests.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteAllBacktestsAsync(int userId)
        {
            var entities = await _context.Backtests
                .Where(b => b.UserId == userId)
                .ToListAsync();

            if (entities.Count == 0)
            {
                return;
            }

            _context.Backtests.RemoveRange(entities);
            await _context.SaveChangesAsync();
        }
    }
}
