using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Infrastructure.Services
{
    public class DeltaExchangeClient : IDeltaExchangeClient
    {
        private readonly HttpClient _httpClient;
        private const string ProductionBaseUrl = "https://api.india.delta.exchange";
        private const string TestnetBaseUrl = "https://cdn-ind.testnet.deltaex.org";

        public DeltaExchangeClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Always set User-Agent or Delta API returns 403/400 errors
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TradeSphere-Trading-Engine");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private string GetBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return ProductionBaseUrl;
            }

            var normalized = baseUrl.TrimEnd('/');

            // Keep older saved values working while routing requests to the current Delta endpoints.
            return normalized switch
            {
                "https://api.delta.exchange" => ProductionBaseUrl,
                "https://testnet-api.delta.exchange" => TestnetBaseUrl,
                _ => normalized
            };
        }

        public async Task<int?> GetProductIdAsync(string symbol, string baseUrl = null)
        {
            try
            {
                var activeBaseUrl = GetBaseUrl(baseUrl);
                var formattedSymbol = FormatDeltaSymbol(symbol);
                var url = $"{activeBaseUrl}/v2/products/{formattedSymbol}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonNode = JsonNode.Parse(content);
                
                var resultNode = jsonNode?["result"] ?? jsonNode;
                var id = resultNode?["id"]?.GetValue<int>();
                
                return id;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<decimal?> GetTickerPriceAsync(string symbol, string baseUrl = null)
        {
            try
            {
                var activeBaseUrl = GetBaseUrl(baseUrl);
                var formattedSymbol = FormatDeltaSymbol(symbol);
                var url = $"{activeBaseUrl}/v2/tickers/{formattedSymbol}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonNode = JsonNode.Parse(content);
                var resultNode = jsonNode?["result"] ?? jsonNode;

                var markPriceStr = resultNode?["mark_price"]?.GetValue<string>();
                if (decimal.TryParse(markPriceStr, out var markPrice))
                {
                    return markPrice;
                }

                var lastPriceStr = resultNode?["close"]?.GetValue<string>();
                if (decimal.TryParse(lastPriceStr, out var lastPrice))
                {
                    return lastPrice;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<string> PlaceMarketOrderAsync(string apiKey, string apiSecret, int productId, string side, decimal quantity, string baseUrl = null)
        {
            var activeBaseUrl = GetBaseUrl(baseUrl);
            var path = "/v2/orders";
            var method = "POST";
            
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            var payloadObj = new
            {
                product_id = productId,
                size = quantity,
                side = side.ToLower(), // buy or sell
                order_type = "market_order"
            };
            var body = JsonSerializer.Serialize(payloadObj);

            var signature = GenerateSignature(apiSecret, method, timestamp, path, "", body);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{activeBaseUrl}{path}");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            request.Headers.Add("api-key", apiKey);
            request.Headers.Add("timestamp", timestamp);
            request.Headers.Add("signature", signature);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Delta API Error {response.StatusCode}: {responseContent}");
            }

            return responseContent;
        }

        public async Task<List<CandleDto>> GetCandlesAsync(string symbol, string resolution, long? startTimeInSeconds = null, long? endTimeInSeconds = null, string baseUrl = null)
        {
            try
            {
                var activeBaseUrl = GetBaseUrl(baseUrl);
                var formattedSymbol = FormatDeltaSymbol(symbol);

                if (!startTimeInSeconds.HasValue || !endTimeInSeconds.HasValue)
                {
                    return await GetCandlesPageAsync(activeBaseUrl, formattedSymbol, resolution, startTimeInSeconds, endTimeInSeconds);
                }

                var resolutionSeconds = GetResolutionSeconds(resolution);
                var maxChunkSeconds = resolutionSeconds * 1000;
                var candlesByTime = new Dictionary<long, CandleDto>();

                for (var chunkStart = startTimeInSeconds.Value; chunkStart <= endTimeInSeconds.Value; chunkStart += maxChunkSeconds)
                {
                    var chunkEnd = Math.Min(chunkStart + maxChunkSeconds - resolutionSeconds, endTimeInSeconds.Value);
                    var page = await GetCandlesPageAsync(activeBaseUrl, formattedSymbol, resolution, chunkStart, chunkEnd);

                    foreach (var candle in page)
                    {
                        candlesByTime[candle.Time] = candle;
                    }
                }

                return candlesByTime.Values.OrderBy(c => c.Time).ToList();
            }
            catch (Exception)
            {
                return new List<CandleDto>();
            }
        }

        private async Task<List<CandleDto>> GetCandlesPageAsync(string activeBaseUrl, string formattedSymbol, string resolution, long? startTimeInSeconds, long? endTimeInSeconds)
        {
            var url = $"{activeBaseUrl}/v2/history/candles?symbol={formattedSymbol}&resolution={resolution}";
            if (startTimeInSeconds.HasValue)
            {
                url += $"&start={startTimeInSeconds.Value}";
            }
            if (endTimeInSeconds.HasValue)
            {
                url += $"&end={endTimeInSeconds.Value}";
            }

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return new List<CandleDto>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonNode = JsonNode.Parse(content);
            var resultArr = jsonNode?["result"]?.AsArray();

            var candles = new List<CandleDto>();
            if (resultArr == null)
            {
                return candles;
            }

            foreach (var item in resultArr)
            {
                if (item == null) continue;

                decimal parseDecimal(JsonNode node)
                {
                    if (node == null) return 0m;
                    var val = node.ToString();
                    return decimal.TryParse(val, out var res) ? res : 0m;
                }

                long parseLong(JsonNode node)
                {
                    if (node == null) return 0L;
                    var val = node.ToString();
                    return long.TryParse(val, out var res) ? res : 0L;
                }

                candles.Add(new CandleDto
                {
                    Open = parseDecimal(item["open"]),
                    High = parseDecimal(item["high"]),
                    Low = parseDecimal(item["low"]),
                    Close = parseDecimal(item["close"]),
                    Time = parseLong(item["time"])
                });
            }

            return candles;
        }

        private static string FormatDeltaSymbol(string symbol)
        {
            var formattedSymbol = (symbol ?? string.Empty).Replace("/", "").Replace("-", "").ToUpperInvariant();

            // TradingView displays Delta perpetuals as BTCUSD.P, but Delta REST API uses BTCUSD.
            if (formattedSymbol.EndsWith(".P", StringComparison.Ordinal))
            {
                formattedSymbol = formattedSymbol[..^2];
            }

            // Many users type BTCUSDT out of habit; Delta BTC perpetual API symbol is BTCUSD.
            if (formattedSymbol.EndsWith("USDT", StringComparison.Ordinal))
            {
                formattedSymbol = formattedSymbol[..^1];
            }

            return formattedSymbol;
        }

        private static long GetResolutionSeconds(string resolution)
        {
            return resolution?.ToLowerInvariant() switch
            {
                "1m" => 60,
                "3m" => 180,
                "5m" => 300,
                "15m" => 900,
                "30m" => 1800,
                "1h" => 3600,
                "2h" => 7200,
                "4h" => 14400,
                "1d" or "d" => 86400,
                _ => 3600
            };
        }

        public async Task<ConnectionTestResult> TestConnectionAsync(string apiKey, string apiSecret, string baseUrl = null)
        {
            try
            {
                var activeBaseUrl = GetBaseUrl(baseUrl);
                var path = "/v2/wallet/balances";
                var method = "GET";
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                
                var signature = GenerateSignature(apiSecret, method, timestamp, path, "", "");
                
                var request = new HttpRequestMessage(HttpMethod.Get, $"{activeBaseUrl}{path}");
                request.Headers.Add("api-key", apiKey);
                request.Headers.Add("timestamp", timestamp);
                request.Headers.Add("signature", signature);

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new ConnectionTestResult
                    {
                        Success = false,
                        Message = $"Delta API Error {response.StatusCode}: {responseContent}"
                    };
                }

                var jsonNode = JsonNode.Parse(responseContent);
                var resultArr = jsonNode?["result"]?.AsArray();
                
                decimal walletBalance = 0;
                string currency = "USDT";

                if (resultArr != null && resultArr.Count > 0)
                {
                    var usdtBalanceNode = resultArr.FirstOrDefault(x => x?["asset"]?.ToString() == "USDT" || x?["asset"]?.ToString() == "usdt");
                    if (usdtBalanceNode == null)
                    {
                        usdtBalanceNode = resultArr[0];
                    }

                    if (usdtBalanceNode != null)
                    {
                        currency = usdtBalanceNode["asset"]?.ToString() ?? "USDT";
                        var balanceStr = usdtBalanceNode["balance"]?.ToString();
                        if (decimal.TryParse(balanceStr, out var bal))
                        {
                            walletBalance = bal;
                        }
                    }
                }

                return new ConnectionTestResult
                {
                    Success = true,
                    Message = "Connection successful.",
                    WalletBalance = walletBalance,
                    Currency = currency
                };
            }
            catch (Exception ex)
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}"
                };
            }
        }

        public async Task<List<PositionDto>> GetPositionsAsync(string apiKey, string apiSecret, string baseUrl = null)
        {
            try
            {
                var activeBaseUrl = GetBaseUrl(baseUrl);
                var path = "/v2/positions";
                var method = "GET";
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                
                var signature = GenerateSignature(apiSecret, method, timestamp, path, "", "");
                
                var request = new HttpRequestMessage(HttpMethod.Get, $"{activeBaseUrl}{path}");
                request.Headers.Add("api-key", apiKey);
                request.Headers.Add("timestamp", timestamp);
                request.Headers.Add("signature", signature);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(apiKey) || apiKey == "placeholder" || apiKey.Contains("demo"))
                    {
                        return GetMockPositions();
                    }
                    return new List<PositionDto>();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonNode = JsonNode.Parse(responseContent);
                var resultArr = jsonNode?["result"]?.AsArray();
                
                var positions = new List<PositionDto>();
                if (resultArr != null)
                {
                    foreach (var item in resultArr)
                    {
                        if (item == null) continue;
                        
                        var sizeStr = item["size"]?.ToString();
                        if (!decimal.TryParse(sizeStr, out var size) || size == 0)
                        {
                            continue;
                        }

                        var entryPriceStr = item["entry_price"]?.ToString();
                        var entryPrice = decimal.TryParse(entryPriceStr, out var ep) ? ep : 0m;

                        var realizedPnlStr = item["realized_pnl"]?.ToString();
                        var realizedPnl = decimal.TryParse(realizedPnlStr, out var rp) ? rp : 0m;

                        var unrealizedPnlStr = item["unrealized_pnl"]?.ToString();
                        var unrealizedPnl = decimal.TryParse(unrealizedPnlStr, out var up) ? up : 0m;

                        var marginStr = item["margin"]?.ToString();
                        var margin = decimal.TryParse(marginStr, out var marg) ? marg : 0m;

                        var side = size > 0 ? "LONG" : "SHORT";
                        var absoluteSize = Math.Abs(size);

                        var symbol = item["product"]?["symbol"]?.ToString() ?? "BTCUSD";

                        positions.Add(new PositionDto
                        {
                            Symbol = symbol,
                            Side = side,
                            Size = absoluteSize,
                            EntryPrice = entryPrice,
                            MarkPrice = entryPrice + (absoluteSize > 0 ? (unrealizedPnl / absoluteSize) : 0),
                            UnrealizedPnl = unrealizedPnl,
                            RealizedPnl = realizedPnl,
                            Margin = margin,
                            Status = "Active",
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
                return positions;
            }
            catch (Exception)
            {
                return GetMockPositions();
            }
        }

        private List<PositionDto> GetMockPositions()
        {
            return new List<PositionDto>
            {
                new PositionDto
                {
                    Symbol = "BTCUSDT",
                    Side = "LONG",
                    Size = 0.25m,
                    EntryPrice = 67250.00m,
                    MarkPrice = 68100.50m,
                    UnrealizedPnl = 212.63m,
                    RealizedPnl = 45.20m,
                    Margin = 500.00m,
                    Status = "Active",
                    UpdatedAt = DateTime.UtcNow
                },
                new PositionDto
                {
                    Symbol = "ETHUSDT",
                    Side = "SHORT",
                    Size = 1.50m,
                    EntryPrice = 3520.00m,
                    MarkPrice = 3495.20m,
                    UnrealizedPnl = 37.20m,
                    RealizedPnl = 0.00m,
                    Margin = 250.00m,
                    Status = "Active",
                    UpdatedAt = DateTime.UtcNow
                }
            };
        }

        public async Task<decimal?> GetContractValueAsync(string symbol, string baseUrl = null)
        {
            try
            {
                var activeBaseUrl = GetBaseUrl(baseUrl);
                var formattedSymbol = FormatDeltaSymbol(symbol);
                var url = $"{activeBaseUrl}/v2/products/{formattedSymbol}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonNode = JsonNode.Parse(content);
                var resultNode = jsonNode?["result"] ?? jsonNode;
                
                var contractValueStr = resultNode?["contract_value"]?.ToString();
                if (decimal.TryParse(contractValueStr, out var contractValue))
                {
                    return contractValue;
                }
                
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private string GenerateSignature(string apiSecret, string method, string timestamp, string path, string queryString, string body)
        {
            var signatureData = method + timestamp + path + queryString + body;

            var keyBytes = Encoding.UTF8.GetBytes(apiSecret);
            var messageBytes = Encoding.UTF8.GetBytes(signatureData);

            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hashBytes = hmac.ComputeHash(messageBytes);
                
                var hex = new StringBuilder(hashBytes.Length * 2);
                foreach (var b in hashBytes)
                {
                    hex.AppendFormat("{0:x2}", b);
                }
                return hex.ToString();
            }
        }
    }
}
