using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IDhanClient
    {
        Task<DhanConnectionTestResultDto> TestConnectionAsync(string clientId, string accessToken);
        Task<IReadOnlyList<string>> GetOptionExpiriesAsync(string clientId, string accessToken, IndianUnderlyingDto underlying);
        Task<OptionChainDto> GetOptionChainAsync(string clientId, string accessToken, IndianUnderlyingDto underlying, string expiry);
        Task<DhanOrderResultDto> PlaceOptionOrderAsync(string clientId, string accessToken, DhanOptionOrderRequestDto order);
    }
}
