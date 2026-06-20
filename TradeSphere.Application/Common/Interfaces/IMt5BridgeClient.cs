using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IMt5BridgeClient
    {
        Task<Mt5ConnectionTestResultDto> HealthAsync(CancellationToken cancellationToken = default);
        Task<Mt5BridgeAccountInfoDto> GetAccountInfoAsync(Mt5BridgeAccountRequestDto request, CancellationToken cancellationToken = default);
        Task<Mt5BridgeOrderResultDto> PlaceMarketOrderAsync(Mt5BridgeOrderRequestDto request, CancellationToken cancellationToken = default);
        Task<Mt5BridgeOrderResultDto> ClosePositionAsync(Mt5BridgeClosePositionRequestDto request, CancellationToken cancellationToken = default);
        Task<Mt5BridgeCandlesResultDto> GetCandlesAsync(Mt5BridgeCandlesRequestDto request, CancellationToken cancellationToken = default);
        Task<Mt5BridgePositionsResultDto> GetPositionsAsync(Mt5BridgePositionsRequestDto request, CancellationToken cancellationToken = default);
        Task<Mt5BridgeDealsResultDto> GetHistoryDealsAsync(Mt5BridgeDealsRequestDto request, CancellationToken cancellationToken = default);
        Task<Mt5BridgeTickResultDto> GetTickAsync(Mt5BridgeTickRequestDto request, CancellationToken cancellationToken = default);
    }
}
