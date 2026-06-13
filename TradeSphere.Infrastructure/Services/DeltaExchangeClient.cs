using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;

namespace TradeSphere.Infrastructure.Services
{
    public class DeltaExchangeClient : IDeltaExchangeClient
    {
        private readonly HttpClient _httpClient;
        private const string TestnetBaseUrl = "https://testnet-api.delta.exchange";

        public DeltaExchangeClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Always set User-Agent or Delta API returns 403/400 errors
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TradeSphere-Trading-Engine");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<int?> GetProductIdAsync(string symbol)
        {
            try
            {
                // Format symbol (ensure uppercase, remove slash if user typed e.g. BTC/USDT)
                var formattedSymbol = symbol.Replace("/", "").ToUpper();
                var url = $"{TestnetBaseUrl}/v2/products/{formattedSymbol}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonNode = JsonNode.Parse(content);
                
                // Delta Exchange products response wraps product details in a "result" field or returns directly
                // Let's check both:
                var resultNode = jsonNode?["result"] ?? jsonNode;
                var id = resultNode?["id"]?.GetValue<int>();
                
                return id;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<decimal?> GetTickerPriceAsync(string symbol)
        {
            try
            {
                var formattedSymbol = symbol.Replace("/", "").ToUpper();
                var url = $"{TestnetBaseUrl}/v2/tickers/{formattedSymbol}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonNode = JsonNode.Parse(content);
                var resultNode = jsonNode?["result"] ?? jsonNode;

                // Tickers endpoint returns mark_price
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

        public async Task<string> PlaceMarketOrderAsync(string apiKey, string apiSecret, int productId, string side, decimal quantity)
        {
            var path = "/v2/orders";
            var method = "POST";
            
            // Generate timestamp in seconds (Delta Exchange expects unix epoch in seconds or milliseconds, 
            // usually seconds. Let's use milliseconds since modern Delta API standard is milliseconds)
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            // Formulate Payload
            var payloadObj = new
            {
                product_id = productId,
                size = quantity,
                side = side.ToLower(), // buy or sell
                order_type = "market_order"
            };
            var body = JsonSerializer.Serialize(payloadObj);

            // Generate signature
            var signature = GenerateSignature(apiSecret, method, timestamp, path, "", body);

            // Create Request
            var request = new HttpRequestMessage(HttpMethod.Post, $"{TestnetBaseUrl}{path}");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            // Authenticated Headers
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

        public async Task<List<CandleDto>> GetCandlesAsync(string symbol, string resolution, long? startTimeInSeconds = null, long? endTimeInSeconds = null)
        {
            try
            {
                var formattedSymbol = symbol.Replace("/", "").ToUpper();
                var url = $"{TestnetBaseUrl}/v2/history/candles?symbol={formattedSymbol}&resolution={resolution}";
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
                if (resultArr != null)
                {
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
                }
                return candles;
            }
            catch (Exception)
            {
                return new List<CandleDto>();
            }
        }

        private string GenerateSignature(string apiSecret, string method, string timestamp, string path, string queryString, string body)
        {
            // Concatenate method + timestamp + path + query_string + body
            var signatureData = method + timestamp + path + queryString + body;

            var keyBytes = Encoding.UTF8.GetBytes(apiSecret);
            var messageBytes = Encoding.UTF8.GetBytes(signatureData);

            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hashBytes = hmac.ComputeHash(messageBytes);
                
                // Convert bytes to hex string without dashes, lowercase
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
