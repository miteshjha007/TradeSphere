using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TradeSphere.Infrastructure.Persistence;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TradeSphere.Domain.Entities;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Infrastructure.Security;

namespace TradeSphere.TradingEngine
{
    public class TradingWorker : BackgroundService
    {
        private readonly ILogger<TradingWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public TradingWorker(ILogger<TradingWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Trading Engine Started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessStrategiesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while executing trading strategies.");
                }

                await Task.Delay(5000, stoppingToken); // Run every 5 seconds
            }
        }

        private async Task ProcessStrategiesAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var deltaClient = scope.ServiceProvider.GetRequiredService<IDeltaExchangeClient>();
                
                // Fetch active strategies
                var activeStrategies = await context.UserStrategies
                    .Where(s => s.Status == "Running")
                    .Include(s => s.Strategy)
                    .Include(s => s.Exchange)
                    .ToListAsync(stoppingToken);

                foreach (var strategy in activeStrategies)
                {
                    try
                    {
                        // Check if it's connected to Delta Exchange (or cosmic for test purposes)
                        var exchangeName = strategy.Exchange?.Name ?? "";
                        if (!exchangeName.Contains("Delta Exchange") && !exchangeName.Contains("Cosmic Exchange"))
                        {
                            continue;
                        }

                        // Fetch the last trade for this strategy to apply a trading cooldown (60 seconds)
                        var lastTrade = await context.Trades
                            .Where(t => t.UserStrategyId == strategy.Id)
                            .OrderByDescending(t => t.CreatedAt)
                            .FirstOrDefaultAsync(stoppingToken);

                        if (lastTrade != null && (DateTime.UtcNow - lastTrade.CreatedAt).TotalSeconds < 60)
                        {
                            // Cooldown active, skip processing
                            continue;
                        }

                        if (strategy.Strategy.LogicType == "HA-EMA")
                        {
                            var config = JsonSerializer.Deserialize<StrategyConfig>(strategy.Config) ?? new StrategyConfig();

                            // Fetch standard daily and intraday candles
                            var dailyCandles = await deltaClient.GetCandlesAsync(strategy.Symbol, "1d");
                            var intradayCandles = await deltaClient.GetCandlesAsync(strategy.Symbol, config.resolution);

                            if (dailyCandles.Count < 2 || intradayCandles.Count < config.emaLength + 5)
                            {
                                _logger.LogWarning($"Insufficient candles fetched for symbol {strategy.Symbol}. Skipping this run.");
                                continue;
                            }

                            // Sort chronologically
                            dailyCandles = dailyCandles.OrderBy(c => c.Time).ToList();
                            intradayCandles = intradayCandles.OrderBy(c => c.Time).ToList();

                            // Calculate Daily Heikin Ashi Bias
                            var haOpen = new List<decimal>();
                            var haClose = new List<decimal>();
                            var haTimes = new List<long>();
                            for (int i = 0; i < dailyCandles.Count; i++)
                            {
                                decimal c = (dailyCandles[i].Open + dailyCandles[i].High + dailyCandles[i].Low + dailyCandles[i].Close) / 4m;
                                decimal o = (i == 0) ? (dailyCandles[i].Open + dailyCandles[i].Close) / 2m : (haOpen[i - 1] + haClose[i - 1]) / 2m;
                                haOpen.Add(o);
                                haClose.Add(c);
                                haTimes.Add(dailyCandles[i].Time);
                            }

                            // Calculate current IST Time
                            var timezoneInfo = GetIstTimezone();
                            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezoneInfo);

                            int currentTimeVal = istNow.Hour * 60 + istNow.Minute;
                            int sessionStartVal = 9 * 60 + 15;
                            int squareOffVal = config.squareOffHour * 60 + config.squareOffMinute;

                            bool isInSession = currentTimeVal >= sessionStartVal && currentTimeVal <= squareOffVal;
                            bool isPastSquareOff = currentTimeVal >= squareOffVal;

                            // Find yesterday's daily HA candle
                            var todayStartSec = new DateTimeOffset(istNow.Date).ToUnixTimeSeconds();
                            int dailyIndex = haTimes.FindLastIndex(time => time < todayStartSec);

                            int dailyBias = 0;
                            if (dailyIndex >= 0 && dailyIndex < haOpen.Count)
                            {
                                decimal haO = haOpen[dailyIndex];
                                decimal haC = haClose[dailyIndex];
                                if (haC > haO) dailyBias = 1;
                                else if (haC < haO) dailyBias = -1;
                            }

                            // Calculate intraday indicators
                            var highs = intradayCandles.Select(c => c.High).ToList();
                            var lows = intradayCandles.Select(c => c.Low).ToList();
                            var closes = intradayCandles.Select(c => c.Close).ToList();

                            var emaHigh = CalculateEMAList(highs, config.emaLength);
                            var emaLow = CalculateEMAList(lows, config.emaLength);
                            var atr = CalculateATRList(intradayCandles, config.atrLength);

                            int n = intradayCandles.Count;
                            // Check crossovers on the last completed candle (n - 2) relative to previous (n - 3)
                            bool crossAboveUpper = (closes[n - 2] > emaHigh[n - 2]) && (closes[n - 3] <= emaHigh[n - 3]);
                            bool crossBelowLower = (closes[n - 2] < emaLow[n - 2]) && (closes[n - 3] >= emaLow[n - 3]);

                            // Load position state from lastTrade
                            int currentPosition = 0;
                            decimal slPrice = 0m;
                            decimal tpPrice = 0m;

                            if (lastTrade != null && lastTrade.ExternalOrderId != null && lastTrade.ExternalOrderId.StartsWith("Entry-Long"))
                            {
                                currentPosition = 1;
                                var parts = lastTrade.ExternalOrderId.Split('|');
                                foreach (var part in parts)
                                {
                                    if (part.StartsWith("SL:")) decimal.TryParse(part.Substring(3), out slPrice);
                                    if (part.StartsWith("TP:")) decimal.TryParse(part.Substring(3), out tpPrice);
                                }
                            }
                            else if (lastTrade != null && lastTrade.ExternalOrderId != null && lastTrade.ExternalOrderId.StartsWith("Entry-Short"))
                            {
                                currentPosition = -1;
                                var parts = lastTrade.ExternalOrderId.Split('|');
                                foreach (var part in parts)
                                {
                                    if (part.StartsWith("SL:")) decimal.TryParse(part.Substring(3), out slPrice);
                                    if (part.StartsWith("TP:")) decimal.TryParse(part.Substring(3), out tpPrice);
                                }
                            }

                            var currentPrice = await deltaClient.GetTickerPriceAsync(strategy.Symbol) ?? 0m;
                            if (currentPrice == 0)
                            {
                                _logger.LogWarning($"Could not fetch ticker price for {strategy.Symbol}. Skipping this evaluation.");
                                continue;
                            }

                            // Get API keys from database
                            var userExchange = await context.UserExchanges
                                .FirstOrDefaultAsync(ue => ue.UserId == strategy.UserId && ue.ExchangeId == strategy.ExchangeId, stoppingToken);

                            if (userExchange == null)
                            {
                                _logger.LogError($"No connected exchange keys found for UserId {strategy.UserId}, ExchangeId {strategy.ExchangeId}. Skipping.");
                                continue;
                            }

                            // Decrypt API keys
                            var apiKey = EncryptionHelper.Decrypt(userExchange.ApiKey);
                            var apiSecret = EncryptionHelper.Decrypt(userExchange.ApiSecret);

                            // Resolve symbol product ID on Delta
                            var productId = await deltaClient.GetProductIdAsync(strategy.Symbol);
                            if (productId == null)
                            {
                                _logger.LogError($"Could not resolve Delta Exchange product ID for symbol: {strategy.Symbol}");
                                continue;
                            }

                            if (currentPosition == 0)
                            {
                                // Entry Logic
                                if (dailyBias == 1 && crossAboveUpper && isInSession && !isPastSquareOff)
                                {
                                    _logger.LogInformation($"Entering LONG order on Delta Exchange for {strategy.Symbol} at {currentPrice}");
                                    string apiResponse = await deltaClient.PlaceMarketOrderAsync(apiKey, apiSecret, productId.Value, "Buy", 1.0m);
                                    _logger.LogInformation($"Delta API Order Success! Response: {apiResponse}");

                                    if (config.useATRSL)
                                    {
                                        slPrice = currentPrice - (config.atrMultiplier * atr[n - 2]);
                                    }
                                    else
                                    {
                                        slPrice = emaLow[n - 2];
                                    }
                                    decimal riskAmount = currentPrice - slPrice;
                                    if (riskAmount <= 0) riskAmount = atr[n - 2] * config.atrMultiplier;
                                    tpPrice = currentPrice + (riskAmount * config.rrRatio);

                                    var trade = new Trade
                                    {
                                        UserId = strategy.UserId,
                                        UserStrategyId = strategy.Id,
                                        ExchangeId = strategy.ExchangeId,
                                        Symbol = strategy.Symbol,
                                        Side = "Buy",
                                        OrderType = "Market",
                                        Price = currentPrice,
                                        Quantity = 1.0m,
                                        Status = "Filled",
                                        ExecutedAt = DateTime.UtcNow,
                                        CreatedAt = DateTime.UtcNow,
                                        Pnl = 0m,
                                        ExternalOrderId = $"Entry-Long|SL:{slPrice:F2}|TP:{tpPrice:F2}"
                                    };
                                    context.Trades.Add(trade);
                                    await context.SaveChangesAsync(stoppingToken);
                                }
                                else if (dailyBias == -1 && crossBelowLower && isInSession && !isPastSquareOff)
                                {
                                    _logger.LogInformation($"Entering SHORT order on Delta Exchange for {strategy.Symbol} at {currentPrice}");
                                    string apiResponse = await deltaClient.PlaceMarketOrderAsync(apiKey, apiSecret, productId.Value, "Sell", 1.0m);
                                    _logger.LogInformation($"Delta API Order Success! Response: {apiResponse}");

                                    if (config.useATRSL)
                                    {
                                        slPrice = currentPrice + (config.atrMultiplier * atr[n - 2]);
                                    }
                                    else
                                    {
                                        slPrice = emaHigh[n - 2];
                                    }
                                    decimal riskAmount = slPrice - currentPrice;
                                    if (riskAmount <= 0) riskAmount = atr[n - 2] * config.atrMultiplier;
                                    tpPrice = currentPrice - (riskAmount * config.rrRatio);

                                    var trade = new Trade
                                    {
                                        UserId = strategy.UserId,
                                        UserStrategyId = strategy.Id,
                                        ExchangeId = strategy.ExchangeId,
                                        Symbol = strategy.Symbol,
                                        Side = "Sell",
                                        OrderType = "Market",
                                        Price = currentPrice,
                                        Quantity = 1.0m,
                                        Status = "Filled",
                                        ExecutedAt = DateTime.UtcNow,
                                        CreatedAt = DateTime.UtcNow,
                                        Pnl = 0m,
                                        ExternalOrderId = $"Entry-Short|SL:{slPrice:F2}|TP:{tpPrice:F2}"
                                    };
                                    context.Trades.Add(trade);
                                    await context.SaveChangesAsync(stoppingToken);
                                }
                            }
                            else if (currentPosition == 1) // Long Position Exit check
                            {
                                bool exitTriggered = false;
                                string exitReason = "";

                                if (isPastSquareOff)
                                {
                                    exitTriggered = true;
                                    exitReason = "Square-Off";
                                }
                                else if (config.exitMode == "Band-Based Exit" && closes[n - 2] < emaLow[n - 2])
                                {
                                    exitTriggered = true;
                                    exitReason = "Band-Exit";
                                }
                                else if (config.exitMode == "Fixed Risk-Reward Exit")
                                {
                                    if (currentPrice <= slPrice)
                                    {
                                        exitTriggered = true;
                                        exitReason = "Stop-Loss";
                                    }
                                    else if (currentPrice >= tpPrice)
                                    {
                                        exitTriggered = true;
                                        exitReason = "Take-Profit";
                                    }
                                }

                                if (exitTriggered)
                                {
                                    _logger.LogInformation($"Exiting LONG position for {strategy.Symbol} at {currentPrice} due to {exitReason}");
                                    string apiResponse = await deltaClient.PlaceMarketOrderAsync(apiKey, apiSecret, productId.Value, "Sell", 1.0m);
                                    _logger.LogInformation($"Delta API Order Success! Response: {apiResponse}");

                                    var entryPriceVal = lastTrade?.Price ?? currentPrice;
                                    decimal pnl = (currentPrice - entryPriceVal) * 1.0m;

                                    var trade = new Trade
                                    {
                                        UserId = strategy.UserId,
                                        UserStrategyId = strategy.Id,
                                        ExchangeId = strategy.ExchangeId,
                                        Symbol = strategy.Symbol,
                                        Side = "Sell",
                                        OrderType = "Market",
                                        Price = currentPrice,
                                        Quantity = 1.0m,
                                        Status = "Filled",
                                        ExecutedAt = DateTime.UtcNow,
                                        CreatedAt = DateTime.UtcNow,
                                        Pnl = pnl,
                                        ExternalOrderId = $"Exit-Long|Reason:{exitReason}"
                                    };
                                    context.Trades.Add(trade);
                                    await context.SaveChangesAsync(stoppingToken);
                                }
                            }
                            else if (currentPosition == -1) // Short Position Exit check
                            {
                                bool exitTriggered = false;
                                string exitReason = "";

                                if (isPastSquareOff)
                                {
                                    exitTriggered = true;
                                    exitReason = "Square-Off";
                                }
                                else if (config.exitMode == "Band-Based Exit" && closes[n - 2] > emaHigh[n - 2])
                                {
                                    exitTriggered = true;
                                    exitReason = "Band-Exit";
                                }
                                else if (config.exitMode == "Fixed Risk-Reward Exit")
                                {
                                    if (currentPrice >= slPrice)
                                    {
                                        exitTriggered = true;
                                        exitReason = "Stop-Loss";
                                    }
                                    else if (currentPrice <= tpPrice)
                                    {
                                        exitTriggered = true;
                                        exitReason = "Take-Profit";
                                    }
                                }

                                if (exitTriggered)
                                {
                                    _logger.LogInformation($"Exiting SHORT position for {strategy.Symbol} at {currentPrice} due to {exitReason}");
                                    string apiResponse = await deltaClient.PlaceMarketOrderAsync(apiKey, apiSecret, productId.Value, "Buy", 1.0m);
                                    _logger.LogInformation($"Delta API Order Success! Response: {apiResponse}");

                                    var entryPriceVal = lastTrade?.Price ?? currentPrice;
                                    decimal pnl = (entryPriceVal - currentPrice) * 1.0m;

                                    var trade = new Trade
                                    {
                                        UserId = strategy.UserId,
                                        UserStrategyId = strategy.Id,
                                        ExchangeId = strategy.ExchangeId,
                                        Symbol = strategy.Symbol,
                                        Side = "Buy",
                                        OrderType = "Market",
                                        Price = currentPrice,
                                        Quantity = 1.0m,
                                        Status = "Filled",
                                        ExecutedAt = DateTime.UtcNow,
                                        CreatedAt = DateTime.UtcNow,
                                        Pnl = pnl,
                                        ExternalOrderId = $"Exit-Short|Reason:{exitReason}"
                                    };
                                    context.Trades.Add(trade);
                                    await context.SaveChangesAsync(stoppingToken);
                                }
                            }

                            continue;
                        }
                        else
                        {
                            // Decide trade side (alternate BUY and SELL)
                            var nextSide = (lastTrade == null || lastTrade.Side == "Sell") ? "Buy" : "Sell";

                            // Get API keys from database
                            var userExchange = await context.UserExchanges
                                .FirstOrDefaultAsync(ue => ue.UserId == strategy.UserId && ue.ExchangeId == strategy.ExchangeId, stoppingToken);

                            if (userExchange == null)
                            {
                                _logger.LogError($"No connected exchange keys found for UserId {strategy.UserId}, ExchangeId {strategy.ExchangeId}. Skipping.");
                                continue;
                            }

                            // Decrypt API keys
                            var apiKey = EncryptionHelper.Decrypt(userExchange.ApiKey);
                            var apiSecret = EncryptionHelper.Decrypt(userExchange.ApiSecret);

                            // Resolve symbol product ID on Delta
                            var productId = await deltaClient.GetProductIdAsync(strategy.Symbol);
                            if (productId == null)
                            {
                                _logger.LogError($"Could not resolve Delta Exchange product ID for symbol: {strategy.Symbol}");
                                continue;
                            }

                            // Fetch live symbol price for database trade record
                            var currentPriceMock = await deltaClient.GetTickerPriceAsync(strategy.Symbol) ?? 0m;

                            _logger.LogInformation($"Executing {nextSide} trade on Delta Exchange Testnet for symbol {strategy.Symbol} (Price: {currentPriceMock})");

                            // Trigger order placement on Delta Exchange Testnet
                            string apiResponseMock = await deltaClient.PlaceMarketOrderAsync(apiKey, apiSecret, productId.Value, nextSide, 1.0m);
                            _logger.LogInformation($"Delta API Order Success! Response: {apiResponseMock}");

                            // Calculate mock PnL for Sell order
                            var pnlMock = 0m;
                            if (nextSide == "Sell" && lastTrade != null && lastTrade.Price.HasValue && currentPriceMock > 0)
                            {
                                pnlMock = (currentPriceMock - lastTrade.Price.Value) * 1.0m;
                            }

                            // Record trade in local DB
                            var tradeMock = new Trade
                            {
                                UserId = strategy.UserId,
                                UserStrategyId = strategy.Id,
                                ExchangeId = strategy.ExchangeId,
                                Symbol = strategy.Symbol,
                                Side = nextSide,
                                OrderType = "Market",
                                Price = currentPriceMock > 0 ? currentPriceMock : 50000m,
                                Quantity = 1.0m,
                                Status = "Filled",
                                ExecutedAt = DateTime.UtcNow,
                                CreatedAt = DateTime.UtcNow,
                                Pnl = pnlMock
                            };

                            context.Trades.Add(tradeMock);
                            await context.SaveChangesAsync(stoppingToken);

                            _logger.LogInformation($"Trade record inserted successfully in database. ID: {tradeMock.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error placing trade on Delta Exchange for strategy {strategy.Id} ({strategy.Symbol})");
                    }
                }
            }
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
    }
}
