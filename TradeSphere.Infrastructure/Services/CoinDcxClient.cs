using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Infrastructure.Services
{
    public class CoinDcxClient : ICoinDcxClient
    {
        private const string ApiBaseUrl = "https://api.coindcx.com";
        private const string PublicBaseUrl = "https://public.coindcx.com";
        private readonly HttpClient _httpClient;

        public CoinDcxClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ConnectionTestResult> TestConnectionAsync(string apiKey, string apiSecret, string baseUrl = null)
        {
            try
            {
                var body = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var response = await SendSignedAsync(HttpMethod.Post, ResolveApiBaseUrl(baseUrl), "/exchange/v1/users/balances", apiKey, apiSecret, body);
                var content = await response.Content.ReadAsStringAsync();
                var (coinsBalance, coinsCurrency) = response.IsSuccessStatusCode
                    ? await EstimateSpotWalletBalanceAsync(content)
                    : (null, null);
                var (futuresBalance, futuresCurrency) = response.IsSuccessStatusCode
                    ? await GetFuturesWalletBalanceAsync(apiKey, apiSecret, baseUrl)
                    : (null, null);

                return new ConnectionTestResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode
                        ? "CoinDCX connection successful."
                        : $"CoinDCX API Error {(int)response.StatusCode}: {content}",
                    WalletBalance = futuresBalance ?? coinsBalance,
                    Currency = futuresCurrency ?? coinsCurrency ?? "USDT",
                    CoinsBalance = coinsBalance,
                    CoinsCurrency = coinsCurrency ?? "USDT",
                    FuturesBalance = futuresBalance,
                    FuturesCurrency = futuresCurrency ?? "USDT"
                };
            }
            catch (Exception ex)
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = $"CoinDCX connection failed: {ex.Message}"
                };
            }
        }

        public async Task<List<CandleDto>> GetCandlesAsync(string symbol, string resolution, long? startTimeInSeconds = null, long? endTimeInSeconds = null)
        {
            var pair = FormatFuturesPair(symbol);
            var normalizedResolution = NormalizeResolution(resolution);
            var from = startTimeInSeconds ?? DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeSeconds();
            var to = endTimeInSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var url = $"{PublicBaseUrl}/market_data/candlesticks?pair={Uri.EscapeDataString(pair)}&from={from}&to={to}&resolution={Uri.EscapeDataString(normalizedResolution)}&pcode=f";

            using var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"CoinDCX candle API error {(int)response.StatusCode}: {content}");
            }

            return ParseCandles(content);
        }

        public async Task<decimal?> GetTickerPriceAsync(string symbol)
        {
            var end = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var start = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();
            var candles = await GetCandlesAsync(symbol, "1m", start, end);
            return candles.OrderByDescending(c => c.Time).FirstOrDefault()?.Close;
        }

        public async Task<string> PlaceMarketOrderAsync(
            string apiKey,
            string apiSecret,
            string symbol,
            string side,
            decimal quantity,
            decimal leverage,
            decimal? takeProfitPrice = null,
            decimal? stopLossPrice = null,
            string baseUrl = null)
        {
            var order = new Dictionary<string, object?>
            {
                ["side"] = side.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? "buy" : "sell",
                ["pair"] = FormatFuturesPair(symbol),
                ["order_type"] = "market_order",
                ["price"] = "0",
                ["total_quantity"] = quantity,
                ["leverage"] = leverage <= 0m ? 1m : leverage,
                ["notification"] = "no_notification",
                ["time_in_force"] = "immediate_or_cancel",
                ["hidden"] = false,
                ["post_only"] = false
            };

            if (takeProfitPrice.HasValue && takeProfitPrice.Value > 0m)
            {
                order["take_profit_price"] = takeProfitPrice.Value;
            }

            if (stopLossPrice.HasValue && stopLossPrice.Value > 0m)
            {
                order["stop_loss_price"] = stopLossPrice.Value;
            }

            var body = new Dictionary<string, object?>
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["order"] = order
            };

            var response = await SendSignedAsync(HttpMethod.Post, ResolveApiBaseUrl(baseUrl), "/exchange/v1/derivatives/futures/orders/create", apiKey, apiSecret, body);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"CoinDCX order API error {(int)response.StatusCode}: {content}");
            }

            return content;
        }

        public async Task<List<PositionDto>> GetPositionsAsync(string apiKey, string apiSecret, string baseUrl = null)
        {
            var body = new Dictionary<string, object?>
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var response = await SendSignedAsync(HttpMethod.Post, ResolveApiBaseUrl(baseUrl), "/exchange/v1/derivatives/futures/positions", apiKey, apiSecret, body);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return new List<PositionDto>();
            }

            return ParsePositions(content);
        }

        private async Task<(decimal? balance, string? currency)> GetFuturesWalletBalanceAsync(string apiKey, string apiSecret, string? baseUrl)
        {
            try
            {
                var body = new Dictionary<string, object?>
                {
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var response = await SendSignedAsync(HttpMethod.Get, ResolveApiBaseUrl(baseUrl), "/exchange/v1/derivatives/futures/wallets", apiKey, apiSecret, body);
                var content = await response.Content.ReadAsStringAsync();
                return response.IsSuccessStatusCode ? ParseFuturesWalletBalance(content) : (null, null);
            }
            catch
            {
                return (null, null);
            }
        }

        public Task<decimal?> GetContractValueAsync(string symbol)
        {
            return Task.FromResult<decimal?>(1m);
        }

        private async Task<(decimal? balance, string? currency)> EstimateSpotWalletBalanceAsync(string balancesContent)
        {
            var assets = ParseWalletAssets(balancesContent);
            if (assets.Count == 0)
            {
                return (0m, "USDT");
            }

            var directUsdt = assets
                .Where(a => a.Currency.Equals("USDT", StringComparison.OrdinalIgnoreCase))
                .Sum(a => a.Balance);

            var estimatedUsdt = directUsdt;
            var tickers = await GetTickerMapAsync();
            foreach (var asset in assets.Where(a => !a.Currency.Equals("USDT", StringComparison.OrdinalIgnoreCase)))
            {
                if (asset.Balance <= 0m)
                {
                    continue;
                }

                if (asset.Currency.Equals("INR", StringComparison.OrdinalIgnoreCase))
                {
                    estimatedUsdt += tickers.TryGetValue("USDTINR", out var usdtInr) && usdtInr > 0m
                        ? asset.Balance / usdtInr
                        : 0m;
                    continue;
                }

                var market = $"{asset.Currency}USDT";
                if (tickers.TryGetValue(market, out var usdtPrice))
                {
                    estimatedUsdt += asset.Balance * usdtPrice;
                }
            }

            return (estimatedUsdt, "USDT");
        }

        private async Task<Dictionary<string, decimal>> GetTickerMapAsync()
        {
            try
            {
                using var response = await _httpClient.GetAsync($"{ApiBaseUrl}/exchange/ticker");
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                }

                var root = JsonNode.Parse(content) as JsonArray ?? new JsonArray();
                return root
                    .OfType<JsonObject>()
                    .Select(t => new
                    {
                        Market = ReadString(t, "market", "symbol", "pair").Replace("_", "").Replace("-", "").ToUpperInvariant(),
                        Price = ReadDecimal(t, "last_price", "last", "close", "price")
                    })
                    .Where(t => !string.IsNullOrWhiteSpace(t.Market) && t.Price > 0m)
                    .GroupBy(t => t.Market, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Price, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string ResolveApiBaseUrl(string? baseUrl)
        {
            return string.IsNullOrWhiteSpace(baseUrl) ? ApiBaseUrl : baseUrl.TrimEnd('/');
        }

        private async Task<HttpResponseMessage> SendSignedAsync(HttpMethod method, string baseUrl, string path, string apiKey, string apiSecret, object body)
        {
            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var signature = Sign(json, apiSecret);

            using var request = new HttpRequestMessage(method, $"{baseUrl}{path}");
            request.Headers.Add("X-AUTH-APIKEY", apiKey);
            request.Headers.Add("X-AUTH-SIGNATURE", signature);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return await _httpClient.SendAsync(request);
        }

        private static string Sign(string payload, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string FormatFuturesPair(string symbol)
        {
            var raw = (symbol ?? string.Empty).Trim().ToUpperInvariant();
            if (raw.StartsWith("B-", StringComparison.OrdinalIgnoreCase))
            {
                return raw;
            }

            var normalized = raw
                .Trim()
                .Replace("/", "")
                .Replace("-", "")
                .Replace("_", "")
                .Replace(".P", "");

            if (normalized.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
            {
                return $"B-{normalized[..^4]}_USDT";
            }

            if (normalized.EndsWith("USD", StringComparison.OrdinalIgnoreCase))
            {
                return $"B-{normalized[..^3]}_USDT";
            }

            return $"B-{normalized}_USDT";
        }

        private static string NormalizeResolution(string resolution)
        {
            var value = (resolution ?? "5m").Trim();
            return value switch
            {
                "1m" => "1",
                "3m" => "3",
                "5m" => "5",
                "15m" => "15",
                "30m" => "30",
                "1h" => "60",
                "2h" => "120",
                "4h" => "240",
                "1d" => "1D",
                "D" => "1D",
                _ => value
            };
        }

        private static List<CandleDto> ParseCandles(string content)
        {
            var root = JsonNode.Parse(content);
            var candlesNode = root as JsonArray
                ?? root?["data"] as JsonArray
                ?? root?["result"] as JsonArray
                ?? new JsonArray();

            var candles = new List<CandleDto>();
            foreach (var item in candlesNode)
            {
                if (item == null)
                {
                    continue;
                }

                CandleDto? candle = item switch
                {
                    JsonObject obj => ParseCandleObject(obj),
                    JsonArray arr => ParseCandleArray(arr),
                    _ => null
                };

                if (candle != null && candle.Time > 0 && candle.Close > 0m)
                {
                    candles.Add(candle);
                }
            }

            return candles;
        }

        private static (decimal? balance, string? currency) ParseWalletBalance(string content)
        {
            var root = JsonNode.Parse(content);
            var balancesNode = root as JsonArray
                ?? root?["data"] as JsonArray
                ?? root?["result"] as JsonArray
                ?? root?["balances"] as JsonArray
                ?? new JsonArray();

            if (balancesNode.Count == 0)
            {
                return (0m, "USDT");
            }

            var preferred = balancesNode
                .OfType<JsonObject>()
                .FirstOrDefault(x => IsCurrency(x, "USDT") || IsCurrency(x, "INR"))
                ?? balancesNode.OfType<JsonObject>().FirstOrDefault();

            if (preferred == null)
            {
                return (0m, "USDT");
            }

            var currency = ReadString(preferred, "currency", "asset", "coin", "symbol");
            var balance = ReadDecimal(preferred, "balance", "available_balance", "available", "free", "amount");
            if (balance == 0m)
            {
                balance = ReadDecimal(preferred, "total_balance", "total", "equity", "wallet_balance");
            }

            return (balance, string.IsNullOrWhiteSpace(currency) ? "USDT" : currency.ToUpperInvariant());
        }

        private static List<WalletAsset> ParseWalletAssets(string content)
        {
            var root = JsonNode.Parse(content);
            var balancesNode = root as JsonArray
                ?? root?["data"] as JsonArray
                ?? root?["result"] as JsonArray
                ?? root?["balances"] as JsonArray
                ?? new JsonArray();

            return balancesNode
                .OfType<JsonObject>()
                .Select(item =>
                {
                    var currency = ReadString(item, "currency", "asset", "coin", "symbol").ToUpperInvariant();
                    var balance = ReadDecimal(item, "balance", "available_balance", "available", "free", "amount");
                    if (balance == 0m)
                    {
                        balance = ReadDecimal(item, "total_balance", "total", "equity", "wallet_balance");
                    }

                    return new WalletAsset(currency, balance);
                })
                .Where(asset => !string.IsNullOrWhiteSpace(asset.Currency) && asset.Balance > 0m)
                .ToList();
        }

        private static (decimal? balance, string? currency) ParseFuturesWalletBalance(string content)
        {
            var root = JsonNode.Parse(content);
            var walletsNode = root as JsonArray
                ?? root?["data"] as JsonArray
                ?? root?["result"] as JsonArray
                ?? root?["wallets"] as JsonArray
                ?? new JsonArray();

            if (walletsNode.Count == 0)
            {
                return (0m, "USDT");
            }

            var preferred = walletsNode
                .OfType<JsonObject>()
                .FirstOrDefault(x => IsFuturesCurrency(x, "USDT"))
                ?? walletsNode.OfType<JsonObject>().FirstOrDefault(x => IsFuturesCurrency(x, "INR"))
                ?? walletsNode.OfType<JsonObject>().FirstOrDefault();

            if (preferred == null)
            {
                return (0m, "USDT");
            }

            var currency = ReadString(preferred, "currency_short_name", "currency", "asset", "coin", "symbol");
            var balance = ReadDecimal(preferred, "balance", "available_balance", "available", "free", "wallet_balance");
            var lockedBalance = ReadDecimal(preferred, "locked_balance");

            return (balance + lockedBalance, string.IsNullOrWhiteSpace(currency) ? "USDT" : currency.ToUpperInvariant());
        }

        private static bool IsFuturesCurrency(JsonObject obj, string currency)
        {
            return string.Equals(ReadString(obj, "currency_short_name", "currency", "asset", "coin", "symbol"), currency, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCurrency(JsonObject obj, string currency)
        {
            return string.Equals(ReadString(obj, "currency", "asset", "coin", "symbol"), currency, StringComparison.OrdinalIgnoreCase);
        }

        private static CandleDto? ParseCandleObject(JsonObject obj)
        {
            var time = ReadLong(obj, "time", "timestamp", "t", "start_time");
            if (time > 9_999_999_999)
            {
                time /= 1000;
            }

            return new CandleDto
            {
                Time = time,
                Open = ReadDecimal(obj, "open", "o"),
                High = ReadDecimal(obj, "high", "h"),
                Low = ReadDecimal(obj, "low", "l"),
                Close = ReadDecimal(obj, "close", "c")
            };
        }

        private static CandleDto? ParseCandleArray(JsonArray arr)
        {
            if (arr.Count < 5)
            {
                return null;
            }

            var time = ReadLong(arr[0]);
            if (time > 9_999_999_999)
            {
                time /= 1000;
            }

            return new CandleDto
            {
                Time = time,
                Open = ReadDecimal(arr[1]),
                High = ReadDecimal(arr[2]),
                Low = ReadDecimal(arr[3]),
                Close = ReadDecimal(arr[4])
            };
        }

        private static List<PositionDto> ParsePositions(string content)
        {
            var root = JsonNode.Parse(content);
            var positionsNode = root as JsonArray
                ?? root?["data"] as JsonArray
                ?? root?["positions"] as JsonArray
                ?? new JsonArray();

            var positions = new List<PositionDto>();
            foreach (var item in positionsNode.OfType<JsonObject>())
            {
                var quantity = ReadDecimal(item, "active_pos", "quantity", "size", "total_quantity");
                if (quantity == 0m)
                {
                    continue;
                }

                var side = ReadString(item, "side", "position_side");
                positions.Add(new PositionDto
                {
                    ExchangeName = "CoinDCX",
                    Symbol = ReadString(item, "pair", "symbol"),
                    Side = string.IsNullOrWhiteSpace(side)
                        ? quantity > 0m ? "Buy" : "Sell"
                        : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(side.ToLowerInvariant()),
                    Size = Math.Abs(quantity),
                    EntryPrice = ReadDecimal(item, "avg_price", "entry_price", "average_price"),
                    MarkPrice = ReadDecimal(item, "mark_price", "current_price"),
                    UnrealizedPnl = ReadDecimal(item, "unrealized_profit", "unrealized_pnl", "pnl"),
                    Margin = ReadDecimal(item, "margin", "locked_margin"),
                    Status = "Open",
                    UpdatedAt = DateTime.UtcNow
                });
            }

            return positions;
        }

        private static string ReadString(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetPropertyValue(name, out var node))
                {
                    return node?.ToString() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static long ReadLong(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetPropertyValue(name, out var node))
                {
                    return ReadLong(node);
                }
            }

            return 0;
        }

        private static long ReadLong(JsonNode? node)
        {
            if (node == null)
            {
                return 0;
            }

            if (long.TryParse(node.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return 0;
        }

        private static decimal ReadDecimal(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetPropertyValue(name, out var node))
                {
                    return ReadDecimal(node);
                }
            }

            return 0m;
        }

        private static decimal ReadDecimal(JsonNode? node)
        {
            if (node == null)
            {
                return 0m;
            }

            if (decimal.TryParse(node.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return 0m;
        }

        private sealed record WalletAsset(string Currency, decimal Balance);
    }
}
