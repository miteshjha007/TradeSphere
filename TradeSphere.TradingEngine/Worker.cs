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
using TradeSphere.Application.DTOs;
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
                var mt5BridgeClient = scope.ServiceProvider.GetRequiredService<IMt5BridgeClient>();
                
                // Fetch active strategies
                var activeStrategies = await context.UserStrategies
                    .Where(s => s.Status == "Running")
                    .Include(s => s.Strategy)
                    .Include(s => s.Exchange)
                    .Include(s => s.Mt5Account)
                    .ToListAsync(stoppingToken);

                foreach (var strategy in activeStrategies)
                {
                    Trade pendingTrade = null;
                    try
                    {
                        // Check if it's connected to Delta Exchange (or cosmic for test purposes)
                        var exchangeName = strategy.Exchange?.Name ?? "";
                        var isMt5 = strategy.ExecutionProvider.Equals("MT5", StringComparison.OrdinalIgnoreCase);
                        if (!isMt5 && !exchangeName.Contains("Delta Exchange") && !exchangeName.Contains("Cosmic Exchange"))
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

                            long nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            long dailyStart = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();

                            long intradayStart;
                            if (config.resolution == "1d")
                                intradayStart = DateTimeOffset.UtcNow.AddDays(-40).ToUnixTimeSeconds();
                            else if (config.resolution == "1h")
                                intradayStart = DateTimeOffset.UtcNow.AddDays(-10).ToUnixTimeSeconds();
                            else if (config.resolution == "15m")
                                intradayStart = DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeSeconds();
                            else
                                intradayStart = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeSeconds();

                            var brokerSymbol = strategy.Symbol;
                            string? mt5Password = null;
                            if (isMt5)
                            {
                                if (strategy.Mt5Account == null || !strategy.Mt5Account.TradingEnabled)
                                {
                                    _logger.LogWarning("MT5 strategy {UserStrategyId} skipped because MT5 account is missing or trading is disabled.", strategy.Id);
                                    continue;
                                }

                                mt5Password = EncryptionHelper.Decrypt(strategy.Mt5Account.EncryptedPassword);
                                brokerSymbol = await ResolveMt5BrokerSymbolAsync(context, strategy.UserId, strategy.Mt5Account.Id, strategy.Symbol, stoppingToken);
                            }

                            var dailyCandles = isMt5
                                ? await GetMt5CandlesAsync(mt5BridgeClient, strategy.Mt5Account!, mt5Password!, brokerSymbol, "1d", dailyStart, nowSec, stoppingToken)
                                : await deltaClient.GetCandlesAsync(strategy.Symbol, "1d", dailyStart, nowSec, strategy.Exchange?.BaseUrl);
                            var intradayCandles = isMt5
                                ? await GetMt5CandlesAsync(mt5BridgeClient, strategy.Mt5Account!, mt5Password!, brokerSymbol, config.resolution, intradayStart, nowSec, stoppingToken)
                                : await deltaClient.GetCandlesAsync(strategy.Symbol, config.resolution, intradayStart, nowSec, strategy.Exchange?.BaseUrl);

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

                            var currentPrice = isMt5
                                ? await GetMt5ExecutionPriceAsync(mt5BridgeClient, strategy.Mt5Account!, mt5Password!, brokerSymbol, "Buy", stoppingToken)
                                : await deltaClient.GetTickerPriceAsync(strategy.Symbol, strategy.Exchange?.BaseUrl) ?? 0m;
                            if (currentPrice == 0)
                            {
                                _logger.LogWarning($"Could not fetch ticker price for {strategy.Symbol}. Skipping this evaluation.");
                                continue;
                            }

                            LogSignalSnapshot(
                                strategy.Symbol,
                                config.resolution,
                                dailyCandles.Count,
                                intradayCandles.Count,
                                dailyBias,
                                crossAboveUpper,
                                crossBelowLower,
                                isInSession,
                                isPastSquareOff,
                                currentPrice);

                            if (isMt5)
                            {
                                await ProcessMt5HaEmaOrdersAsync(
                                    context,
                                    mt5BridgeClient,
                                    strategy,
                                    brokerSymbol,
                                    mt5Password!,
                                    config,
                                    lastTrade,
                                    currentPosition,
                                    currentPrice,
                                    crossAboveUpper,
                                    crossBelowLower,
                                    dailyBias,
                                    isInSession,
                                    isPastSquareOff,
                                    emaLow[n - 2],
                                    emaHigh[n - 2],
                                    atr[n - 2],
                                    stoppingToken);
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

                            // Resolve symbol product ID and contract value on Delta
                            var productId = await deltaClient.GetProductIdAsync(strategy.Symbol, strategy.Exchange?.BaseUrl);
                            if (productId == null)
                            {
                                _logger.LogError($"Could not resolve Delta Exchange product ID for symbol: {strategy.Symbol}");
                                continue;
                            }
                            var contractValue = await deltaClient.GetContractValueAsync(strategy.Symbol, strategy.Exchange?.BaseUrl) ?? 0.001m;

                            if (currentPosition == 0)
                            {
                                // Entry Logic
                                if (dailyBias == 1 && crossAboveUpper && isInSession && !isPastSquareOff)
                                {
                                    var calculatedQty = CalculateOrderQuantity(config, currentPrice, contractValue, strategy.Symbol);
                                    _logger.LogInformation($"Entering LONG order on Delta Exchange for {strategy.Symbol} at {currentPrice}, Qty: {calculatedQty}");
                                    pendingTrade = CreateTradeAttempt(strategy, "Buy", currentPrice, calculatedQty, "Entry-Long");
                                    string apiResponse = await deltaClient.PlaceMarketOrderAsync(apiKey, apiSecret, productId.Value, "Buy", calculatedQty, strategy.Exchange?.BaseUrl);
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

                                    pendingTrade.Status = "Filled";
                                    pendingTrade.ExecutedAt = DateTime.UtcNow;
                                    pendingTrade.BrokerResponse = apiResponse;
                                    pendingTrade.ExternalOrderId = $"Entry-Long|SL:{slPrice:F2}|TP:{tpPrice:F2}";
                                    context.Trades.Add(pendingTrade);
                                    await context.SaveChangesAsync(stoppingToken);
                                    pendingTrade = null;
                                }
                                else if (dailyBias == -1 && crossBelowLower && isInSession && !isPastSquareOff)
                                {
                                    var calculatedQty = CalculateOrderQuantity(config, currentPrice, contractValue, strategy.Symbol);
                                    _logger.LogInformation($"Entering SHORT order on Delta Exchange for {strategy.Symbol} at {currentPrice}, Qty: {calculatedQty}");
                                    pendingTrade = CreateTradeAttempt(strategy, "Sell", currentPrice, calculatedQty, "Entry-Short");
                                    string apiResponse = await deltaClient.PlaceMarketOrderAsync(apiKey, apiSecret, productId.Value, "Sell", calculatedQty, strategy.Exchange?.BaseUrl);
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

                                    pendingTrade.Status = "Filled";
                                    pendingTrade.ExecutedAt = DateTime.UtcNow;
                                    pendingTrade.BrokerResponse = apiResponse;
                                    pendingTrade.ExternalOrderId = $"Entry-Short|SL:{slPrice:F2}|TP:{tpPrice:F2}";
                                    context.Trades.Add(pendingTrade);
                                    await context.SaveChangesAsync(stoppingToken);
                                    pendingTrade = null;
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
                                    var exitQty = lastTrade != null ? lastTrade.Quantity : 1.0m;
                                    _logger.LogInformation($"Exiting LONG position for {strategy.Symbol} at {currentPrice} due to {exitReason}, Qty: {exitQty}");
                                    pendingTrade = CreateTradeAttempt(strategy, "Sell", currentPrice, exitQty, $"Exit-Long|Reason:{exitReason}");
                                    string apiResponse = await deltaClient.PlaceMarketOrderAsync(apiKey, apiSecret, productId.Value, "Sell", exitQty, strategy.Exchange?.BaseUrl);
                                    _logger.LogInformation($"Delta API Order Success! Response: {apiResponse}");

                                    var entryPriceVal = lastTrade?.Price ?? currentPrice;
                                    decimal pnl = (currentPrice - entryPriceVal) * exitQty * contractValue;

                                    pendingTrade.Status = "Filled";
                                    pendingTrade.ExecutedAt = DateTime.UtcNow;
                                    pendingTrade.Pnl = pnl;
                                    pendingTrade.BrokerResponse = apiResponse;
                                    context.Trades.Add(pendingTrade);
                                    await context.SaveChangesAsync(stoppingToken);
                                    pendingTrade = null;
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
                                    var exitQty = lastTrade != null ? lastTrade.Quantity : 1.0m;
                                    _logger.LogInformation($"Exiting SHORT position for {strategy.Symbol} at {currentPrice} due to {exitReason}, Qty: {exitQty}");
                                    pendingTrade = CreateTradeAttempt(strategy, "Buy", currentPrice, exitQty, $"Exit-Short|Reason:{exitReason}");
                                    string apiResponse = await deltaClient.PlaceMarketOrderAsync(apiKey, apiSecret, productId.Value, "Buy", exitQty, strategy.Exchange?.BaseUrl);
                                    _logger.LogInformation($"Delta API Order Success! Response: {apiResponse}");

                                    var entryPriceVal = lastTrade?.Price ?? currentPrice;
                                    decimal pnl = (entryPriceVal - currentPrice) * exitQty * contractValue;

                                    pendingTrade.Status = "Filled";
                                    pendingTrade.ExecutedAt = DateTime.UtcNow;
                                    pendingTrade.Pnl = pnl;
                                    pendingTrade.BrokerResponse = apiResponse;
                                    context.Trades.Add(pendingTrade);
                                    await context.SaveChangesAsync(stoppingToken);
                                    pendingTrade = null;
                                }
                            }

                            continue;
                        }
                        else if (strategy.Strategy.LogicType == "FIB-55-EMA")
                        {
                            await ProcessFib55EmaStrategyAsync(
                                context,
                                deltaClient,
                                mt5BridgeClient,
                                strategy,
                                isMt5,
                                stoppingToken);
                            continue;
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Strategy logic type {LogicType} is not implemented in the live trading engine. StrategyId={StrategyId}, UserStrategyId={UserStrategyId}, Symbol={Symbol}. Skipping without placing an order.",
                                strategy.Strategy.LogicType,
                                strategy.StrategyId,
                                strategy.Id,
                                strategy.Symbol);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error placing trade on Delta Exchange for strategy {strategy.Id} ({strategy.Symbol})");
                        if (pendingTrade != null)
                        {
                            var friendlyError = NormalizeOrderError(ex.Message);
                            pendingTrade.Status = "Failed";
                            pendingTrade.ErrorReason = friendlyError;
                            pendingTrade.BrokerResponse = ex.Message;
                            pendingTrade.UpdatedAt = DateTime.UtcNow;
                            context.Trades.Add(pendingTrade);
                            await context.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
            }
        }

        private async Task<List<CandleDto>> GetMt5CandlesAsync(
            IMt5BridgeClient mt5BridgeClient,
            Mt5Account account,
            string password,
            string symbol,
            string resolution,
            long startTime,
            long endTime,
            CancellationToken cancellationToken)
        {
            var result = await mt5BridgeClient.GetCandlesAsync(new Mt5BridgeCandlesRequestDto
            {
                Login = account.Login,
                Server = account.Server,
                Password = password,
                Symbol = symbol,
                Resolution = NormalizeResolution(resolution),
                StartTime = startTime,
                EndTime = endTime
            }, cancellationToken);

            if (!result.Success)
            {
                throw new Exception($"MT5 candles failed for {symbol}: {result.Message}");
            }

            return result.Candles.Select(c => new CandleDto
            {
                Time = c.Time,
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close
            }).ToList();
        }

        private async Task<decimal> GetMt5ExecutionPriceAsync(
            IMt5BridgeClient mt5BridgeClient,
            Mt5Account account,
            string password,
            string symbol,
            string side,
            CancellationToken cancellationToken)
        {
            var tick = await mt5BridgeClient.GetTickAsync(new Mt5BridgeTickRequestDto
            {
                Login = account.Login,
                Server = account.Server,
                Password = password,
                Symbol = symbol
            }, cancellationToken);

            if (!tick.Success)
            {
                throw new Exception($"MT5 tick failed for {symbol}: {tick.Message}");
            }

            var price = side.Equals("Buy", StringComparison.OrdinalIgnoreCase)
                ? tick.Ask ?? tick.Last ?? tick.Bid ?? 0m
                : tick.Bid ?? tick.Last ?? tick.Ask ?? 0m;
            if (price <= 0m)
            {
                throw new Exception($"MT5 tick returned no executable price for {symbol}.");
            }

            return price;
        }

        private async Task<string> ResolveMt5BrokerSymbolAsync(ApplicationDbContext context, int userId, int accountId, string strategySymbol, CancellationToken cancellationToken)
        {
            var normalized = (strategySymbol ?? string.Empty).Trim().ToUpperInvariant();
            var mapping = await context.Mt5SymbolMappings
                .FirstOrDefaultAsync(m => m.UserId == userId && m.Mt5AccountId == accountId && m.StrategySymbol == normalized && m.IsActive, cancellationToken);
            return string.IsNullOrWhiteSpace(mapping?.BrokerSymbol) ? strategySymbol : mapping.BrokerSymbol;
        }

        private async Task ProcessMt5HaEmaOrdersAsync(
            ApplicationDbContext context,
            IMt5BridgeClient mt5BridgeClient,
            UserStrategy strategy,
            string brokerSymbol,
            string password,
            StrategyConfig config,
            Trade? lastTrade,
            int currentPosition,
            decimal currentPrice,
            bool crossAboveUpper,
            bool crossBelowLower,
            int dailyBias,
            bool isInSession,
            bool isPastSquareOff,
            decimal emaLow,
            decimal emaHigh,
            decimal atr,
            CancellationToken cancellationToken)
        {
            if (strategy.Mt5Account == null)
            {
                return;
            }

            var slPrice = 0m;
            var tpPrice = 0m;
            if (currentPosition == 0)
            {
                if (dailyBias == 1 && crossAboveUpper && isInSession && !isPastSquareOff)
                {
                    slPrice = config.useATRSL ? currentPrice - (config.atrMultiplier * atr) : emaLow;
                    var riskAmount = currentPrice - slPrice;
                    if (riskAmount <= 0m) riskAmount = Math.Max(atr * config.atrMultiplier, currentPrice * 0.005m);
                    tpPrice = currentPrice + (riskAmount * config.rrRatio);
                    await PlaceMt5OrderAndRecordAsync(context, mt5BridgeClient, strategy, brokerSymbol, password, "Buy", currentPrice, CalculateMt5Volume(config), slPrice, tpPrice, "Entry-Long", cancellationToken);
                }
                else if (dailyBias == -1 && crossBelowLower && isInSession && !isPastSquareOff)
                {
                    slPrice = config.useATRSL ? currentPrice + (config.atrMultiplier * atr) : emaHigh;
                    var riskAmount = slPrice - currentPrice;
                    if (riskAmount <= 0m) riskAmount = Math.Max(atr * config.atrMultiplier, currentPrice * 0.005m);
                    tpPrice = currentPrice - (riskAmount * config.rrRatio);
                    await PlaceMt5OrderAndRecordAsync(context, mt5BridgeClient, strategy, brokerSymbol, password, "Sell", currentPrice, CalculateMt5Volume(config), slPrice, tpPrice, "Entry-Short", cancellationToken);
                }
            }
            else if (currentPosition == 1)
            {
                var shouldExit = isPastSquareOff || (config.exitMode == "Band-Based Exit" && currentPrice < emaLow);
                if (shouldExit)
                {
                    await CloseMt5PositionAndRecordAsync(context, mt5BridgeClient, strategy, brokerSymbol, password, lastTrade, currentPrice, "Sell", "Exit-Long", cancellationToken);
                }
            }
            else if (currentPosition == -1)
            {
                var shouldExit = isPastSquareOff || (config.exitMode == "Band-Based Exit" && currentPrice > emaHigh);
                if (shouldExit)
                {
                    await CloseMt5PositionAndRecordAsync(context, mt5BridgeClient, strategy, brokerSymbol, password, lastTrade, currentPrice, "Buy", "Exit-Short", cancellationToken);
                }
            }
        }

        private async Task ProcessFib55EmaStrategyAsync(
            ApplicationDbContext context,
            IDeltaExchangeClient deltaClient,
            IMt5BridgeClient mt5BridgeClient,
            UserStrategy strategy,
            bool isMt5,
            CancellationToken cancellationToken)
        {
            var config = JsonSerializer.Deserialize<Fib55EmaConfig>(strategy.Config) ?? new Fib55EmaConfig();
            var nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var historyStart = DateTimeOffset.UtcNow.AddDays(-20).ToUnixTimeSeconds();
            var htfResolution = NormalizeResolution(config.htfTimeframe);
            var brokerSymbol = strategy.Symbol;
            string? mt5Password = null;

            if (isMt5)
            {
                if (strategy.Mt5Account == null || !strategy.Mt5Account.TradingEnabled)
                {
                    _logger.LogWarning("MT5 Fib strategy {UserStrategyId} skipped because MT5 account is missing or trading is disabled.", strategy.Id);
                    return;
                }

                mt5Password = EncryptionHelper.Decrypt(strategy.Mt5Account.EncryptedPassword);
                brokerSymbol = await ResolveMt5BrokerSymbolAsync(context, strategy.UserId, strategy.Mt5Account.Id, strategy.Symbol, cancellationToken);
            }

            var candles = isMt5
                ? await GetMt5CandlesAsync(mt5BridgeClient, strategy.Mt5Account!, mt5Password!, brokerSymbol, config.resolution, historyStart, nowSec, cancellationToken)
                : await deltaClient.GetCandlesAsync(strategy.Symbol, NormalizeResolution(config.resolution), historyStart, nowSec, strategy.Exchange?.BaseUrl);
            var htfCandles = isMt5
                ? await GetMt5CandlesAsync(mt5BridgeClient, strategy.Mt5Account!, mt5Password!, brokerSymbol, htfResolution, historyStart, nowSec, cancellationToken)
                : await deltaClient.GetCandlesAsync(strategy.Symbol, htfResolution, historyStart, nowSec, strategy.Exchange?.BaseUrl);

            candles = candles.OrderBy(c => c.Time).ToList();
            htfCandles = htfCandles.OrderBy(c => c.Time).ToList();

            if (candles.Count < config.emaLength + 5 || htfCandles.Count < config.emaLength + 2)
            {
                _logger.LogWarning(
                    "Insufficient Fib candles for {Symbol}. LTF={LtfCount}, HTF={HtfCount}, LTF resolution={Resolution}, HTF={HtfResolution}.",
                    strategy.Symbol,
                    candles.Count,
                    htfCandles.Count,
                    config.resolution,
                    htfResolution);
                return;
            }

            var signal = BuildFib55Signal(candles, htfCandles, config);
            if (signal == null)
            {
                _logger.LogInformation("Fib signal check {Symbol} {Resolution}: no completed candle ready for evaluation.", strategy.Symbol, config.resolution);
                return;
            }

            var lastFilledTrade = await context.Trades
                .Where(t => t.UserStrategyId == strategy.Id && t.Status == "Filled")
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var currentPosition = GetPositionFromTrade(lastFilledTrade, "Fib-Entry-");
            var priceSide = currentPosition == 1
                ? "Sell"
                : currentPosition == -1
                    ? "Buy"
                    : signal.Sell ? "Sell" : "Buy";
            var currentPrice = isMt5
                ? await GetMt5ExecutionPriceAsync(mt5BridgeClient, strategy.Mt5Account!, mt5Password!, brokerSymbol, priceSide, cancellationToken)
                : await deltaClient.GetTickerPriceAsync(strategy.Symbol, strategy.Exchange?.BaseUrl) ?? signal.CurrentPrice;
            if (currentPrice <= 0m)
            {
                _logger.LogWarning("Could not fetch executable price for Fib strategy {Symbol}.", strategy.Symbol);
                return;
            }

            _logger.LogInformation(
                "Fib signal check {Symbol} {Resolution}: price={Price}, htfBull={HtfBull}, htfBear={HtfBear}, ltfBull={LtfBull}, ltfBear={LtfBear}, nearBuyFib={NearBuyFib}, nearSellFib={NearSellFib}, bounce={Bounce}, rejection={Rejection}, rsi={Rsi}, buy={Buy}, sell={Sell}, position={Position}.",
                strategy.Symbol,
                config.resolution,
                currentPrice,
                signal.HtfBull,
                signal.HtfBear,
                signal.LtfBull,
                signal.LtfBear,
                signal.NearBuyFib,
                signal.NearSellFib,
                signal.Bounce,
                signal.Rejection,
                signal.Rsi,
                signal.Buy,
                signal.Sell,
                currentPosition);

            if (currentPosition == 0)
            {
                if (!signal.Buy && !signal.Sell)
                {
                    return;
                }

                var side = signal.Buy ? "Buy" : "Sell";
                var positionText = signal.Buy ? "Long" : "Short";
                var sl = signal.Buy
                    ? currentPrice - currentPrice * config.stopLossPct
                    : currentPrice + currentPrice * config.stopLossPct;
                var risk = Math.Abs(currentPrice - sl);
                if (risk <= 0m)
                {
                    risk = currentPrice * Math.Max(config.stopLossPct, 0.001m);
                }
                var tp = signal.Buy
                    ? currentPrice + risk * config.tp2RiskReward
                    : currentPrice - risk * config.tp2RiskReward;
                var externalId = $"Fib-Entry-{positionText}|SL:{sl:F2}|TP:{tp:F2}";

                if (isMt5)
                {
                    await PlaceMt5OrderAndRecordAsync(context, mt5BridgeClient, strategy, brokerSymbol, mt5Password!, side, currentPrice, CalculateMt5Volume(config), sl, tp, externalId, cancellationToken);
                    return;
                }

                await PlaceDeltaOrderAndRecordAsync(context, deltaClient, strategy, config, side, currentPrice, externalId, cancellationToken);
                return;
            }

            var (slPrice, tpPrice) = ParseRiskLevels(lastFilledTrade?.ExternalOrderId);
            var exitTriggered = false;
            var exitReason = "";
            var exitSide = "";

            if (currentPosition == 1)
            {
                if (slPrice > 0m && currentPrice <= slPrice)
                {
                    exitTriggered = true;
                    exitReason = "Stop-Loss";
                }
                else if (tpPrice > 0m && currentPrice >= tpPrice)
                {
                    exitTriggered = true;
                    exitReason = "Take-Profit";
                }
                exitSide = "Sell";
            }
            else if (currentPosition == -1)
            {
                if (slPrice > 0m && currentPrice >= slPrice)
                {
                    exitTriggered = true;
                    exitReason = "Stop-Loss";
                }
                else if (tpPrice > 0m && currentPrice <= tpPrice)
                {
                    exitTriggered = true;
                    exitReason = "Take-Profit";
                }
                exitSide = "Buy";
            }

            if (!exitTriggered)
            {
                return;
            }

            var exitExternalId = currentPosition == 1
                ? $"Fib-Exit-Long|Reason:{exitReason}"
                : $"Fib-Exit-Short|Reason:{exitReason}";

            if (isMt5)
            {
                await CloseMt5PositionAndRecordAsync(context, mt5BridgeClient, strategy, brokerSymbol, mt5Password!, lastFilledTrade, currentPrice, exitSide, exitExternalId, cancellationToken);
                return;
            }

            await PlaceDeltaOrderAndRecordAsync(context, deltaClient, strategy, config, exitSide, currentPrice, exitExternalId, cancellationToken, lastFilledTrade);
        }

        private async Task PlaceDeltaOrderAndRecordAsync(
            ApplicationDbContext context,
            IDeltaExchangeClient deltaClient,
            UserStrategy strategy,
            Fib55EmaConfig config,
            string side,
            decimal price,
            string externalOrderId,
            CancellationToken cancellationToken,
            Trade? entryTrade = null)
        {
            Trade? trade = null;
            try
            {
                var userExchange = await context.UserExchanges
                    .FirstOrDefaultAsync(ue => ue.UserId == strategy.UserId && ue.ExchangeId == strategy.ExchangeId, cancellationToken);
                if (userExchange == null)
                {
                    throw new Exception($"No connected exchange keys found for UserId {strategy.UserId}, ExchangeId {strategy.ExchangeId}.");
                }

                var apiKey = EncryptionHelper.Decrypt(userExchange.ApiKey);
                var apiSecret = EncryptionHelper.Decrypt(userExchange.ApiSecret);
                var productId = await deltaClient.GetProductIdAsync(strategy.Symbol, strategy.Exchange?.BaseUrl);
                if (productId == null)
                {
                    throw new Exception($"Could not resolve Delta Exchange product ID for symbol: {strategy.Symbol}");
                }

                var contractValue = await deltaClient.GetContractValueAsync(strategy.Symbol, strategy.Exchange?.BaseUrl) ?? 0.001m;
                var quantity = entryTrade?.Quantity ?? CalculateOrderQuantity(ToStrategyConfig(config), price, contractValue, strategy.Symbol);
                trade = CreateTradeAttempt(strategy, side, price, quantity, externalOrderId);

                var apiResponse = await deltaClient.PlaceMarketOrderAsync(apiKey, apiSecret, productId.Value, side, quantity, strategy.Exchange?.BaseUrl);
                trade.Status = "Filled";
                trade.ExecutedAt = DateTime.UtcNow;
                trade.BrokerResponse = apiResponse;
                trade.ExternalOrderId = externalOrderId;

                if (entryTrade != null)
                {
                    var entryPrice = entryTrade.Price ?? price;
                    trade.Pnl = side.Equals("Sell", StringComparison.OrdinalIgnoreCase)
                        ? (price - entryPrice) * quantity * contractValue
                        : (entryPrice - price) * quantity * contractValue;
                }

                context.Trades.Add(trade);
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delta Fib order failed for strategy {StrategyId} {Symbol}.", strategy.Id, strategy.Symbol);
                trade ??= CreateTradeAttempt(strategy, side, price, entryTrade?.Quantity ?? 0m, externalOrderId);
                trade.Status = "Failed";
                trade.ErrorReason = NormalizeOrderError(ex.Message);
                trade.BrokerResponse = ex.Message;
                trade.UpdatedAt = DateTime.UtcNow;
                context.Trades.Add(trade);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task PlaceMt5OrderAndRecordAsync(
            ApplicationDbContext context,
            IMt5BridgeClient mt5BridgeClient,
            UserStrategy strategy,
            string brokerSymbol,
            string password,
            string side,
            decimal price,
            decimal volume,
            decimal? stopLoss,
            decimal? takeProfit,
            string externalOrderId,
            CancellationToken cancellationToken)
        {
            var trade = CreateTradeAttempt(strategy, side, price, volume, externalOrderId);
            try
            {
                var result = await mt5BridgeClient.PlaceMarketOrderAsync(new Mt5BridgeOrderRequestDto
                {
                    Login = strategy.Mt5Account!.Login,
                    Server = strategy.Mt5Account.Server,
                    Password = password,
                    Symbol = brokerSymbol,
                    Side = side,
                    Volume = volume,
                    StopLoss = stopLoss,
                    TakeProfit = takeProfit,
                    Comment = $"TradeSphere {strategy.Strategy.LogicType}"
                }, cancellationToken);

                trade.ExecutedAt = DateTime.UtcNow;
                trade.BrokerResponse = result.RawResponse ?? result.Message;
                if (result.Success)
                {
                    trade.Status = "Filled";
                    trade.Price = result.Price ?? price;
                    trade.ExternalOrderId = string.IsNullOrWhiteSpace(result.OrderId) ? externalOrderId : $"{externalOrderId}|MT5:{result.OrderId}";
                }
                else
                {
                    trade.Status = "Failed";
                    trade.ErrorReason = NormalizeOrderError(result.Message);
                }
            }
            catch (Exception ex)
            {
                trade.Status = "Failed";
                trade.ErrorReason = NormalizeOrderError(ex.Message);
                trade.BrokerResponse = ex.Message;
            }

            context.Trades.Add(trade);
            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task CloseMt5PositionAndRecordAsync(
            ApplicationDbContext context,
            IMt5BridgeClient mt5BridgeClient,
            UserStrategy strategy,
            string brokerSymbol,
            string password,
            Trade? entryTrade,
            decimal price,
            string closeSide,
            string externalOrderId,
            CancellationToken cancellationToken)
        {
            var volume = entryTrade?.Quantity ?? 0m;
            var trade = CreateTradeAttempt(strategy, closeSide, price, volume, externalOrderId);
            try
            {
                if (strategy.Mt5Account == null)
                {
                    throw new Exception("MT5 account is missing for close-position request.");
                }

                var position = await FindOpenMt5PositionAsync(mt5BridgeClient, strategy.Mt5Account, password, brokerSymbol, entryTrade, cancellationToken);
                if (position == null)
                {
                    throw new Exception($"No matching open MT5 position found for {brokerSymbol}. The position may already be closed manually or by SL/TP.");
                }

                volume = volume <= 0m ? position.Volume : Math.Min(volume, position.Volume);
                trade.Quantity = volume;
                var result = await mt5BridgeClient.ClosePositionAsync(new Mt5BridgeClosePositionRequestDto
                {
                    Login = strategy.Mt5Account.Login,
                    Server = strategy.Mt5Account.Server,
                    Password = password,
                    Symbol = brokerSymbol,
                    PositionTicket = position.Ticket,
                    Volume = volume,
                    Comment = $"TradeSphere {strategy.Strategy.LogicType} close"
                }, cancellationToken);

                trade.ExecutedAt = DateTime.UtcNow;
                trade.BrokerResponse = result.RawResponse ?? result.Message;
                if (result.Success)
                {
                    trade.Status = "Filled";
                    trade.Price = result.Price ?? price;
                    trade.ExternalOrderId = string.IsNullOrWhiteSpace(result.OrderId) ? externalOrderId : $"{externalOrderId}|MT5:{result.OrderId}";
                }
                else
                {
                    trade.Status = "Failed";
                    trade.ErrorReason = NormalizeOrderError(result.Message);
                }
            }
            catch (Exception ex)
            {
                trade.Status = "Failed";
                trade.ErrorReason = NormalizeOrderError(ex.Message);
                trade.BrokerResponse = ex.Message;
            }

            context.Trades.Add(trade);
            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task<Mt5BridgePositionDto?> FindOpenMt5PositionAsync(
            IMt5BridgeClient mt5BridgeClient,
            Mt5Account account,
            string password,
            string brokerSymbol,
            Trade? entryTrade,
            CancellationToken cancellationToken)
        {
            var result = await mt5BridgeClient.GetPositionsAsync(new Mt5BridgePositionsRequestDto
            {
                Login = account.Login,
                Server = account.Server,
                Password = password,
                Symbol = brokerSymbol
            }, cancellationToken);

            if (!result.Success)
            {
                throw new Exception($"MT5 positions failed for {brokerSymbol}: {result.Message}");
            }

            var expectedTicket = ExtractMt5Ticket(entryTrade?.ExternalOrderId);
            var expectedType = entryTrade?.ExternalOrderId?.Contains("Entry-Long", StringComparison.OrdinalIgnoreCase) == true ? 0
                : entryTrade?.ExternalOrderId?.Contains("Entry-Short", StringComparison.OrdinalIgnoreCase) == true ? 1
                : -1;

            if (expectedTicket > 0)
            {
                var exact = result.Positions.FirstOrDefault(p => p.Ticket == expectedTicket);
                if (exact != null)
                {
                    return exact;
                }
            }

            return result.Positions
                .Where(p => string.Equals(p.Symbol, brokerSymbol, StringComparison.OrdinalIgnoreCase))
                .Where(p => expectedType < 0 || p.Type == expectedType)
                .Where(p => entryTrade == null || Math.Abs(p.Volume - entryTrade.Quantity) < 0.0001m)
                .OrderByDescending(p => p.Time)
                .FirstOrDefault();
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

        private static decimal CalculateMt5Volume(Fib55EmaConfig config)
        {
            var volume = config.tradeSizeValue <= 0m ? 0.01m : config.tradeSizeValue;
            return Math.Max(0.01m, Math.Round(volume, 2));
        }

        private static decimal CalculateMt5Volume(StrategyConfig config)
        {
            var volume = config.tradeSizeValue <= 0m ? 0.01m : config.tradeSizeValue;
            return Math.Max(0.01m, Math.Round(volume, 2));
        }

        private static StrategyConfig ToStrategyConfig(Fib55EmaConfig config)
        {
            return new StrategyConfig
            {
                tradeSizeType = config.tradeSizeType,
                tradeSizeValue = config.tradeSizeValue,
                leverage = config.leverage
            };
        }

        private static Fib55Signal? BuildFib55Signal(List<CandleDto> candles, List<CandleDto> htfCandles, Fib55EmaConfig config)
        {
            var i = candles.Count - 2; // Last completed candle; last item can still be forming.
            if (i < Math.Max(config.emaLength + 2, 20))
            {
                return null;
            }

            var closes = candles.Select(c => c.Close).ToList();
            var htfCloses = htfCandles.Select(c => c.Close).ToList();
            var ema = CalculateEMAList(closes, config.emaLength);
            var htfEma = CalculateEMAList(htfCloses, config.emaLength);
            var rsi = CalculateRSIList(closes, 14);

            var candle = candles[i];
            var previous = candles[i - 1];
            var htfIndex = FindLastCandleIndex(htfCandles, candle.Time);
            if (htfIndex < config.emaLength || htfIndex < 0)
            {
                return null;
            }

            var hourly = htfCandles[htfIndex];
            var fibRange = hourly.High - hourly.Low;
            if (fibRange <= 0m)
            {
                return null;
            }

            var s618 = hourly.High - fibRange * config.fib618;
            var s500 = hourly.High - fibRange * config.fib500;
            var s382 = hourly.High - fibRange * config.fib381;
            var b618 = hourly.Low + fibRange * config.fib618;
            var b500 = hourly.Low + fibRange * config.fib500;
            var b382 = hourly.Low + fibRange * config.fib381;

            var range = candle.High - candle.Low;
            var body = Math.Abs(candle.Close - candle.Open);
            var strongCandle = range > 0m && body / range * 100m >= config.minBodyPct;

            var signal = new Fib55Signal
            {
                CurrentPrice = candle.Close,
                HtfBull = htfCandles[htfIndex].Close > htfEma[htfIndex],
                HtfBear = htfCandles[htfIndex].Close < htfEma[htfIndex],
                LtfBull = candle.Close > ema[i],
                LtfBear = candle.Close < ema[i],
                NearSellFib = IsNear(candle.Close, s618, config.zoneBuffer) || IsNear(candle.Close, s500, config.zoneBuffer) || IsNear(candle.Close, s382, config.zoneBuffer),
                NearBuyFib = IsNear(candle.Close, b618, config.zoneBuffer) || IsNear(candle.Close, b500, config.zoneBuffer) || IsNear(candle.Close, b382, config.zoneBuffer),
                Bounce = candle.Close > candle.Open && previous.Close < previous.Open && strongCandle,
                Rejection = candle.Close < candle.Open && previous.Close > previous.Open && strongCandle,
                Rsi = rsi[i],
                HourlyGreen = hourly.Close >= hourly.Open,
                HourlyRed = hourly.Close < hourly.Open
            };

            var rsiBuy = signal.Rsi >= config.rsiBuyMin && signal.Rsi < 70m;
            var rsiSell = signal.Rsi <= config.rsiSellMax && signal.Rsi > 30m;
            signal.Buy = signal.HtfBull && signal.LtfBull && signal.HourlyGreen && signal.NearBuyFib && signal.Bounce && rsiBuy;
            signal.Sell = signal.HtfBear && signal.LtfBear && signal.HourlyRed && signal.NearSellFib && signal.Rejection && rsiSell;
            return signal;
        }

        private static int GetPositionFromTrade(Trade? trade, string entryPrefix)
        {
            if (trade?.ExternalOrderId == null)
            {
                return 0;
            }

            if (trade.ExternalOrderId.StartsWith($"{entryPrefix}Long", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (trade.ExternalOrderId.StartsWith($"{entryPrefix}Short", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            return 0;
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
                    decimal.TryParse(part[3..], out stopLoss);
                }
                else if (part.StartsWith("TP:", StringComparison.OrdinalIgnoreCase))
                {
                    decimal.TryParse(part[3..], out takeProfit);
                }
            }

            return (stopLoss, takeProfit);
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

        private static string NormalizeOrderError(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return "Order rejected by broker.";
            }

            var parsedDetails = TryParseDeltaError(errorMessage);
            var searchable = $"{errorMessage} {parsedDetails}".ToLowerInvariant();

            if (searchable.Contains("insufficient") || searchable.Contains("margin") || searchable.Contains("fund"))
            {
                return "Order rejected: insufficient funds or available margin in broker account.";
            }

            if (searchable.Contains("permission") || searchable.Contains("unauthorized") || searchable.Contains("forbidden"))
            {
                return "Order rejected: API key is not authorized for trading.";
            }

            if (searchable.Contains("size") || searchable.Contains("quantity") || searchable.Contains("minimum"))
            {
                return "Order rejected: invalid or below-minimum order quantity.";
            }

            return string.IsNullOrWhiteSpace(parsedDetails)
                ? errorMessage
                : $"Order rejected by broker: {parsedDetails}";
        }

        private static int FindLastCandleIndex(List<CandleDto> candles, long time)
        {
            for (var i = candles.Count - 1; i >= 0; i--)
            {
                if (candles[i].Time <= time)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsNear(decimal price, decimal level, decimal buffer)
        {
            if (level <= 0m)
            {
                return false;
            }

            return price >= level * (1m - buffer) && price <= level * (1m + buffer);
        }

        private static List<decimal> CalculateRSIList(List<decimal> closes, int length)
        {
            var rsi = Enumerable.Repeat(50m, closes.Count).ToList();
            if (closes.Count <= length)
            {
                return rsi;
            }

            var gain = 0m;
            var loss = 0m;
            for (var i = 1; i <= length; i++)
            {
                var change = closes[i] - closes[i - 1];
                if (change >= 0m)
                {
                    gain += change;
                }
                else
                {
                    loss -= change;
                }
            }

            gain /= length;
            loss /= length;

            for (var i = length + 1; i < closes.Count; i++)
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

        private static string TryParseDeltaError(string errorMessage)
        {
            var jsonStart = errorMessage.IndexOf('{');
            if (jsonStart < 0)
            {
                return string.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(errorMessage[jsonStart..]);
                var root = document.RootElement;
                if (!root.TryGetProperty("error", out var error))
                {
                    return string.Empty;
                }

                var parts = new List<string>();
                if (error.TryGetProperty("code", out var code))
                {
                    parts.Add(code.ToString());
                }

                if (error.TryGetProperty("message", out var message))
                {
                    parts.Add(message.ToString());
                }

                return string.Join(": ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private void LogSignalSnapshot(
            string symbol,
            string resolution,
            int dailyCandleCount,
            int intradayCandleCount,
            int dailyBias,
            bool crossAboveUpper,
            bool crossBelowLower,
            bool isInSession,
            bool isPastSquareOff,
            decimal currentPrice)
        {
            var biasText = dailyBias switch
            {
                1 => "Bullish",
                -1 => "Bearish",
                _ => "Neutral"
            };

            var blockedBy = new List<string>();
            if (!isInSession) blockedBy.Add("outside configured session");
            if (isPastSquareOff) blockedBy.Add("past square-off time");
            if (dailyBias == 0) blockedBy.Add("neutral daily HA bias");
            if (dailyBias == 1 && !crossAboveUpper) blockedBy.Add("no long EMA-band crossover");
            if (dailyBias == -1 && !crossBelowLower) blockedBy.Add("no short EMA-band crossover");

            var status = blockedBy.Count == 0 ? "entry signal is eligible" : string.Join(", ", blockedBy);

            _logger.LogInformation(
                "Signal check {Symbol} {Resolution}: price={Price}, dailyCandles={DailyCandles}, intradayCandles={IntradayCandles}, bias={Bias}, crossAboveUpper={CrossAboveUpper}, crossBelowLower={CrossBelowLower}, inSession={InSession}, pastSquareOff={PastSquareOff}. Result: {Status}",
                symbol,
                resolution,
                currentPrice,
                dailyCandleCount,
                intradayCandleCount,
                biasText,
                crossAboveUpper,
                crossBelowLower,
                isInSession,
                isPastSquareOff,
                status);
        }

        private decimal CalculateOrderQuantity(StrategyConfig config, decimal currentPrice, decimal contractValue, string symbol)
        {
            if (currentPrice <= 0 || contractValue <= 0)
            {
                _logger.LogWarning($"Invalid inputs for quantity calculation. Symbol: {symbol}, Price: {currentPrice}, ContractValue: {contractValue}. Defaulting to 1.0.");
                return 1.0m;
            }

            decimal qty;
            const decimal UsdInrRate = 85.0m;

            if (config.tradeSizeType == "USD")
            {
                var contractValueUsd = currentPrice * contractValue;
                qty = config.tradeSizeValue / contractValueUsd;
            }
            else if (config.tradeSizeType == "INR")
            {
                var tradeSizeUsd = config.tradeSizeValue / UsdInrRate;
                var contractValueUsd = currentPrice * contractValue;
                qty = tradeSizeUsd / contractValueUsd;
            }
            else if (config.tradeSizeType == "Margin_USD")
            {
                var notionalUsd = config.tradeSizeValue * config.leverage;
                var contractValueUsd = currentPrice * contractValue;
                qty = notionalUsd / contractValueUsd;
            }
            else if (config.tradeSizeType == "Margin_INR")
            {
                var marginUsd = config.tradeSizeValue / UsdInrRate;
                var notionalUsd = marginUsd * config.leverage;
                var contractValueUsd = currentPrice * contractValue;
                qty = notionalUsd / contractValueUsd;
            }
            else // "Contracts" or default
            {
                qty = config.tradeSizeValue;
            }

            var roundedQty = Math.Round(qty);
            if (roundedQty < 1.0m)
            {
                _logger.LogWarning($"Calculated quantity {qty} (rounded: {roundedQty}) is less than minimum size. Defaulting to 1 contract.");
                roundedQty = 1.0m;
            }
            else
            {
                _logger.LogInformation($"Calculated quantity: {qty} -> Rounded quantity: {roundedQty} contracts for {symbol} (Size Type: {config.tradeSizeType}, Value: {config.tradeSizeValue}, Leverage: {config.leverage})");
            }

            return roundedQty;
        }

        private static Trade CreateTradeAttempt(UserStrategy strategy, string side, decimal price, decimal quantity, string externalOrderId)
        {
            return new Trade
            {
                UserId = strategy.UserId,
                UserStrategyId = strategy.Id,
                ExchangeId = strategy.ExchangeId,
                Symbol = strategy.Symbol,
                Side = side,
                OrderType = "Market",
                Price = price,
                Quantity = quantity,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Pnl = 0m,
                ExternalOrderId = externalOrderId
            };
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

        private class Fib55Signal
        {
            public decimal CurrentPrice { get; set; }
            public bool HtfBull { get; set; }
            public bool HtfBear { get; set; }
            public bool LtfBull { get; set; }
            public bool LtfBear { get; set; }
            public bool HourlyGreen { get; set; }
            public bool HourlyRed { get; set; }
            public bool NearSellFib { get; set; }
            public bool NearBuyFib { get; set; }
            public bool Bounce { get; set; }
            public bool Rejection { get; set; }
            public decimal Rsi { get; set; }
            public bool Buy { get; set; }
            public bool Sell { get; set; }
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
            public string tradeSizeType { get; set; } = "Contracts"; // "Contracts", "USD", "INR", "Margin_USD", "Margin_INR"
            public decimal tradeSizeValue { get; set; } = 1.0m;
            public decimal leverage { get; set; } = 10.0m;
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
