using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Infrastructure.Services
{
    public class Mt5BridgeClient : IMt5BridgeClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public Mt5BridgeClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _baseUrl = (configuration["Mt5Bridge:BaseUrl"] ?? "http://127.0.0.1:8765").TrimEnd('/');
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<Mt5ConnectionTestResultDto> HealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health", cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new Mt5ConnectionTestResultDto
                {
                    Success = response.IsSuccessStatusCode,
                    Status = response.IsSuccessStatusCode ? "BridgeOnline" : "BridgeError",
                    Message = response.IsSuccessStatusCode ? "MT5 bridge is reachable." : $"MT5 bridge returned {(int)response.StatusCode}: {body}",
                    BridgeEndpoint = _baseUrl
                };
            }
            catch (Exception ex)
            {
                return new Mt5ConnectionTestResultDto
                {
                    Success = false,
                    Status = "BridgeOffline",
                    Message = $"MT5 bridge is not reachable at {_baseUrl}. Start mt5-bridge/start-mt5-bridge.cmd. Details: {ex.Message}",
                    BridgeEndpoint = _baseUrl
                };
            }
        }

        public async Task<Mt5BridgeAccountInfoDto> GetAccountInfoAsync(Mt5BridgeAccountRequestDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["login"] = request.Login,
                    ["password"] = request.Password,
                    ["server"] = request.Server
                };
                var response = await PostJsonAsync($"{_baseUrl}/account-info", payload, cancellationToken);
                return await ReadJsonAsync<Mt5BridgeAccountInfoDto>(response, cancellationToken);
            }
            catch (Exception ex)
            {
                return new Mt5BridgeAccountInfoDto
                {
                    Success = false,
                    Message = $"MT5 bridge account-info request failed: {ex.Message}"
                };
            }
        }

        public async Task<Mt5BridgeOrderResultDto> PlaceMarketOrderAsync(Mt5BridgeOrderRequestDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["login"] = request.Login,
                    ["password"] = request.Password,
                    ["server"] = request.Server,
                    ["symbol"] = request.Symbol,
                    ["side"] = request.Side,
                    ["volume"] = request.Volume,
                    ["stopLoss"] = request.StopLoss,
                    ["takeProfit"] = request.TakeProfit,
                    ["comment"] = request.Comment
                };
                var response = await PostJsonAsync($"{_baseUrl}/order/market", payload, cancellationToken);
                return await ReadJsonAsync<Mt5BridgeOrderResultDto>(response, cancellationToken);
            }
            catch (Exception ex)
            {
                return new Mt5BridgeOrderResultDto
                {
                    Success = false,
                    Message = $"MT5 bridge order request failed: {ex.Message}"
                };
            }
        }

        public async Task<Mt5BridgeOrderResultDto> ClosePositionAsync(Mt5BridgeClosePositionRequestDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["login"] = request.Login,
                    ["password"] = request.Password,
                    ["server"] = request.Server,
                    ["symbol"] = request.Symbol,
                    ["positionTicket"] = request.PositionTicket,
                    ["volume"] = request.Volume,
                    ["comment"] = request.Comment
                };
                var response = await PostJsonAsync($"{_baseUrl}/position/close", payload, cancellationToken);
                return await ReadJsonAsync<Mt5BridgeOrderResultDto>(response, cancellationToken);
            }
            catch (Exception ex)
            {
                return new Mt5BridgeOrderResultDto
                {
                    Success = false,
                    Message = $"MT5 bridge close-position request failed: {ex.Message}"
                };
            }
        }

        public async Task<Mt5BridgeOrderResultDto> ModifyPositionAsync(Mt5BridgeModifyPositionRequestDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["login"] = request.Login,
                    ["password"] = request.Password,
                    ["server"] = request.Server,
                    ["symbol"] = request.Symbol,
                    ["positionTicket"] = request.PositionTicket,
                    ["stopLoss"] = request.StopLoss,
                    ["takeProfit"] = request.TakeProfit,
                    ["comment"] = request.Comment
                };
                var response = await PostJsonAsync($"{_baseUrl}/position/modify", payload, cancellationToken);
                return await ReadJsonAsync<Mt5BridgeOrderResultDto>(response, cancellationToken);
            }
            catch (Exception ex)
            {
                return new Mt5BridgeOrderResultDto
                {
                    Success = false,
                    Message = $"MT5 bridge modify-position request failed: {ex.Message}"
                };
            }
        }

        public async Task<Mt5BridgeCandlesResultDto> GetCandlesAsync(Mt5BridgeCandlesRequestDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["login"] = request.Login,
                    ["password"] = request.Password,
                    ["server"] = request.Server,
                    ["symbol"] = request.Symbol,
                    ["resolution"] = request.Resolution,
                    ["startTime"] = request.StartTime,
                    ["endTime"] = request.EndTime
                };
                var response = await PostJsonAsync($"{_baseUrl}/candles", payload, cancellationToken);
                return await ReadJsonAsync<Mt5BridgeCandlesResultDto>(response, cancellationToken);
            }
            catch (Exception ex)
            {
                return new Mt5BridgeCandlesResultDto
                {
                    Success = false,
                    Message = $"MT5 bridge candles request failed: {ex.Message}"
                };
            }
        }

        public async Task<Mt5BridgeDealsResultDto> GetHistoryDealsAsync(Mt5BridgeDealsRequestDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["login"] = request.Login,
                    ["password"] = request.Password,
                    ["server"] = request.Server,
                    ["symbol"] = request.Symbol,
                    ["startTime"] = request.StartTime,
                    ["endTime"] = request.EndTime
                };
                var response = await PostJsonAsync($"{_baseUrl}/history/deals", payload, cancellationToken);
                return await ReadJsonAsync<Mt5BridgeDealsResultDto>(response, cancellationToken);
            }
            catch (Exception ex)
            {
                return new Mt5BridgeDealsResultDto
                {
                    Success = false,
                    Message = $"MT5 bridge history request failed: {ex.Message}"
                };
            }
        }

        public async Task<Mt5BridgePositionsResultDto> GetPositionsAsync(Mt5BridgePositionsRequestDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["login"] = request.Login,
                    ["password"] = request.Password,
                    ["server"] = request.Server,
                    ["symbol"] = request.Symbol
                };
                var response = await PostJsonAsync($"{_baseUrl}/positions", payload, cancellationToken);
                return await ReadJsonAsync<Mt5BridgePositionsResultDto>(response, cancellationToken);
            }
            catch (Exception ex)
            {
                return new Mt5BridgePositionsResultDto
                {
                    Success = false,
                    Message = $"MT5 bridge positions request failed: {ex.Message}"
                };
            }
        }

        public async Task<Mt5BridgeTickResultDto> GetTickAsync(Mt5BridgeTickRequestDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["login"] = request.Login,
                    ["password"] = request.Password,
                    ["server"] = request.Server,
                    ["symbol"] = request.Symbol
                };
                var response = await PostJsonAsync($"{_baseUrl}/tick", payload, cancellationToken);
                return await ReadJsonAsync<Mt5BridgeTickResultDto>(response, cancellationToken);
            }
            catch (Exception ex)
            {
                return new Mt5BridgeTickResultDto
                {
                    Success = false,
                    Message = $"MT5 bridge tick request failed: {ex.Message}"
                };
            }
        }

        private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) where T : new()
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<T>(body, options) ?? new T();
                return result;
            }
            catch
            {
                if (typeof(T) == typeof(Mt5BridgeAccountInfoDto))
                {
                    return (T)(object)new Mt5BridgeAccountInfoDto
                    {
                        Success = false,
                        Message = $"MT5 bridge returned invalid response {(int)response.StatusCode}: {body}"
                    };
                }

                if (typeof(T) == typeof(Mt5BridgeCandlesResultDto))
                {
                    return (T)(object)new Mt5BridgeCandlesResultDto
                    {
                        Success = false,
                        Message = $"MT5 bridge returned invalid response {(int)response.StatusCode}: {body}"
                    };
                }

                if (typeof(T) == typeof(Mt5BridgeDealsResultDto))
                {
                    return (T)(object)new Mt5BridgeDealsResultDto
                    {
                        Success = false,
                        Message = $"MT5 bridge returned invalid response {(int)response.StatusCode}: {body}"
                    };
                }

                if (typeof(T) == typeof(Mt5BridgePositionsResultDto))
                {
                    return (T)(object)new Mt5BridgePositionsResultDto
                    {
                        Success = false,
                        Message = $"MT5 bridge returned invalid response {(int)response.StatusCode}: {body}"
                    };
                }

                if (typeof(T) == typeof(Mt5BridgeTickResultDto))
                {
                    return (T)(object)new Mt5BridgeTickResultDto
                    {
                        Success = false,
                        Message = $"MT5 bridge returned invalid response {(int)response.StatusCode}: {body}"
                    };
                }

                return (T)(object)new Mt5BridgeOrderResultDto
                {
                    Success = false,
                    Message = $"MT5 bridge returned invalid response {(int)response.StatusCode}: {body}"
                };
            }
        }

        private async Task<HttpResponseMessage> PostJsonAsync(string url, object payload, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.ContentLength = Encoding.UTF8.GetByteCount(json);
            return await _httpClient.PostAsync(url, content, cancellationToken);
        }
    }
}
