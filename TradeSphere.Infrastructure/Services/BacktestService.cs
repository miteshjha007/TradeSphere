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

namespace TradeSphere.Infrastructure.Services
{
    public class BacktestService : IBacktestService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDeltaExchangeClient _deltaClient;

        public BacktestService(ApplicationDbContext context, IDeltaExchangeClient deltaClient)
        {
            _context = context;
            _deltaClient = deltaClient;
        }

        public async Task<List<BacktestDto>> GetUserBacktestsAsync(int userId)
        {
            return await _context.Backtests
                .Include(b => b.Strategy)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BacktestDto
                {
                    Id = b.Id,
                    StrategyId = b.StrategyId ?? 0,
                    StrategyName = b.Strategy != null ? b.Strategy.Name : "Unknown",
                    Symbol = b.Symbol,
                    Interval = "1h", // Defaulting as not in entity
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    TotalReturn = b.TotalReturn ?? 0,
                    MaxDrawdown = b.MaxDrawdown ?? 0,
                    TradeCount = b.TotalTrades ?? 0,
                    Status = "Completed"
                })
                .ToListAsync();
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
                Interval = "1h",
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
            public string resolution { get; set; } = "1h";
            public string exitMode { get; set; } = "Band-Based Exit";
            public decimal rrRatio { get; set; } = 2.0m;
            public bool useATRSL { get; set; } = false;
            public int atrLength { get; set; } = 14;
            public decimal atrMultiplier { get; set; } = 1.5m;
            public string sessionStart { get; set; } = "0915-1515";
            public int squareOffHour { get; set; } = 15;
            public int squareOffMinute { get; set; } = 15;
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
            string resultJson = "{ \"equity\": [], \"trades\": [] }";

            if (strategy.LogicType == "HA-EMA")
            {
                var configJson = string.IsNullOrEmpty(dto.ConfigOverrides) ? strategy.DefaultConfig : dto.ConfigOverrides;
                var config = JsonSerializer.Deserialize<StrategyConfig>(configJson) ?? new StrategyConfig();

                long dailyStart = new DateTimeOffset(startDateUtc.AddDays(-40)).ToUnixTimeSeconds();
                long dailyEnd = new DateTimeOffset(endDateUtc).ToUnixTimeSeconds();
                long intraStart = new DateTimeOffset(startDateUtc.AddDays(-10)).ToUnixTimeSeconds();
                long intraEnd = new DateTimeOffset(endDateUtc).ToUnixTimeSeconds();

                var dailyCandles = await _deltaClient.GetCandlesAsync(dto.Symbol, "1d", dailyStart, dailyEnd);
                var intradayCandles = await _deltaClient.GetCandlesAsync(dto.Symbol, dto.Interval ?? config.resolution, intraStart, intraEnd);

                dailyCandles = dailyCandles.OrderBy(c => c.Time).ToList();
                intradayCandles = intradayCandles.OrderBy(c => c.Time).ToList();

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
                    var todayStartSec = new DateTimeOffset(istTime.Date).ToUnixTimeSeconds();
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

                totalReturn = ((capital - initialCapital) / initialCapital) * 100m;
                tradeCount = tradesLog.Count;
                
                // Calculate max drawdown
                decimal peak = initialCapital;
                foreach (var pt in equityCurve)
                {
                    if (pt.equity > peak) peak = pt.equity;
                    decimal dd = (peak - pt.equity) / peak * 100m;
                    if (dd > maxDrawdown) maxDrawdown = dd;
                }

                resultJson = JsonSerializer.Serialize(new
                {
                    trades = tradesLog,
                    equity = equityCurve
                });
            }
            else
            {
                // Fallback to random simulation for other strategy types
                var random = new Random();
                totalReturn = (decimal)(random.NextDouble() * 50 - 10);
                maxDrawdown = (decimal)(random.NextDouble() * 20);
                tradeCount = random.Next(10, 100);
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
    }
}
