using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Infrastructure.Services
{
    public class DhanClient : IDhanClient
    {
        private const string BaseUrl = "https://api.dhan.co/v2";
        private readonly HttpClient _httpClient;

        public DhanClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<DhanConnectionTestResultDto> TestConnectionAsync(string clientId, string accessToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/fundlimit");
            AddAuthHeaders(request, clientId, accessToken, includeClientId: false);

            using var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return new DhanConnectionTestResultDto
                {
                    Success = false,
                    Message = $"Dhan connection failed: {(int)response.StatusCode} {content}"
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            return new DhanConnectionTestResultDto
            {
                Success = true,
                Message = "Dhan connection successful.",
                AvailableBalance = GetDecimal(root, "availabelBalance") ?? GetDecimal(root, "availableBalance"),
                UtilizedAmount = GetDecimal(root, "utilizedAmount"),
                WithdrawableBalance = GetDecimal(root, "withdrawableBalance"),
                Currency = "INR"
            };
        }

        public async Task<IReadOnlyList<string>> GetOptionExpiriesAsync(string clientId, string accessToken, IndianUnderlyingDto underlying)
        {
            var body = new
            {
                UnderlyingScrip = underlying.UnderlyingScrip,
                UnderlyingSeg = underlying.UnderlyingSegment
            };

            using var response = await PostJsonAsync($"{BaseUrl}/optionchain/expirylist", clientId, accessToken, body);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Dhan expiry list failed: {(int)response.StatusCode} {content}");
            }

            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return data.EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
        }

        public async Task<OptionChainDto> GetOptionChainAsync(string clientId, string accessToken, IndianUnderlyingDto underlying, string expiry)
        {
            var body = new
            {
                UnderlyingScrip = underlying.UnderlyingScrip,
                UnderlyingSeg = underlying.UnderlyingSegment,
                Expiry = expiry
            };

            using var response = await PostJsonAsync($"{BaseUrl}/optionchain", clientId, accessToken, body);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Dhan option chain failed: {(int)response.StatusCode} {content}");
            }

            using var doc = JsonDocument.Parse(content);
            var data = doc.RootElement.GetProperty("data");
            var result = new OptionChainDto
            {
                Underlying = underlying.Symbol,
                Expiry = expiry,
                UnderlyingLastPrice = GetDecimal(data, "last_price") ?? 0m
            };

            if (!data.TryGetProperty("oc", out var optionChain) || optionChain.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var strikeProperty in optionChain.EnumerateObject())
            {
                if (!decimal.TryParse(strikeProperty.Name, NumberStyles.Any, CultureInfo.InvariantCulture, out var strike))
                {
                    continue;
                }

                var row = new OptionChainRowDto { StrikePrice = strike };
                if (strikeProperty.Value.TryGetProperty("ce", out var ce))
                {
                    row.Call = ParseLeg("CE", ce);
                }

                if (strikeProperty.Value.TryGetProperty("pe", out var pe))
                {
                    row.Put = ParseLeg("PE", pe);
                }

                result.Rows.Add(row);
            }

            result.Rows = result.Rows.OrderBy(r => r.StrikePrice).ToList();
            return result;
        }

        public async Task<DhanOrderResultDto> PlaceOptionOrderAsync(string clientId, string accessToken, DhanOptionOrderRequestDto order)
        {
            var correlationId = $"TS{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var body = new
            {
                dhanClientId = clientId,
                correlationId,
                transactionType = order.TransactionType.ToUpperInvariant(),
                exchangeSegment = "NSE_FNO",
                productType = order.ProductType.ToUpperInvariant(),
                orderType = order.OrderType.ToUpperInvariant(),
                validity = "DAY",
                securityId = order.SecurityId,
                quantity = order.Quantity,
                disclosedQuantity = 0,
                price = order.OrderType.Equals("MARKET", StringComparison.OrdinalIgnoreCase) ? 0 : order.Price ?? 0,
                triggerPrice = 0,
                afterMarketOrder = false,
                amoTime = "",
                boProfitValue = "",
                boStopLossValue = ""
            };

            using var response = await PostJsonAsync($"{BaseUrl}/orders", clientId, accessToken, body, includeClientId: false);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return new DhanOrderResultDto
                {
                    Success = false,
                    Message = $"Dhan order failed: {(int)response.StatusCode} {content}",
                    RawResponse = content
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            return new DhanOrderResultDto
            {
                Success = true,
                Message = "Dhan order accepted.",
                OrderId = GetString(root, "orderId"),
                OrderStatus = GetString(root, "orderStatus"),
                RawResponse = content
            };
        }

        private async Task<HttpResponseMessage> PostJsonAsync(string url, string clientId, string accessToken, object body, bool includeClientId = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddAuthHeaders(request, clientId, accessToken, includeClientId);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            return await _httpClient.SendAsync(request);
        }

        private static void AddAuthHeaders(HttpRequestMessage request, string clientId, string accessToken, bool includeClientId)
        {
            request.Headers.TryAddWithoutValidation("access-token", accessToken);
            if (includeClientId)
            {
                request.Headers.TryAddWithoutValidation("client-id", clientId);
            }
        }

        private static OptionLegDto ParseLeg(string optionType, JsonElement element)
        {
            var greeks = element.TryGetProperty("greeks", out var g) ? g : default;
            return new OptionLegDto
            {
                OptionType = optionType,
                SecurityId = GetString(element, "security_id") ?? string.Empty,
                LastPrice = GetDecimal(element, "last_price") ?? 0m,
                AveragePrice = GetDecimal(element, "average_price") ?? 0m,
                ImpliedVolatility = GetDecimal(element, "implied_volatility") ?? 0m,
                OpenInterest = GetLong(element, "oi") ?? 0,
                PreviousOpenInterest = GetLong(element, "previous_oi") ?? 0,
                Volume = GetLong(element, "volume") ?? GetLong(element, "previous_volume") ?? 0,
                TopBidPrice = GetDecimal(element, "top_bid_price") ?? 0m,
                TopBidQuantity = GetLong(element, "top_bid_quantity") ?? 0,
                TopAskPrice = GetDecimal(element, "top_ask_price") ?? 0m,
                TopAskQuantity = GetLong(element, "top_ask_quantity") ?? 0,
                Delta = greeks.ValueKind == JsonValueKind.Object ? GetDecimal(greeks, "delta") ?? 0m : 0m,
                Theta = greeks.ValueKind == JsonValueKind.Object ? GetDecimal(greeks, "theta") ?? 0m : 0m,
                Gamma = greeks.ValueKind == JsonValueKind.Object ? GetDecimal(greeks, "gamma") ?? 0m : 0m,
                Vega = greeks.ValueKind == JsonValueKind.Object ? GetDecimal(greeks, "vega") ?? 0m : 0m
            };
        }

        private static string? GetString(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            };
        }

        private static decimal? GetDecimal(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
                JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var number) => number,
                _ => null
            };
        }

        private static long? GetLong(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt64(out var number) => number,
                JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var number) => number,
                _ => null
            };
        }
    }
}
