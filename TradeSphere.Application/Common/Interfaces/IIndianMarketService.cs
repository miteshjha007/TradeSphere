using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IIndianMarketService
    {
        IReadOnlyList<IndianUnderlyingDto> GetSupportedUnderlyings();
        Task<IReadOnlyList<DhanAccountDto>> GetDhanAccountsAsync(int userId);
        Task<DhanAccountDto> ConnectDhanAccountAsync(int userId, ConnectDhanAccountDto dto);
        Task DeleteDhanAccountAsync(int userId, int accountId);
        Task<DhanConnectionTestResultDto> TestDhanConnectionAsync(int userId, int accountId);
        Task<IReadOnlyList<string>> GetExpiriesAsync(int userId, OptionExpiryRequestDto dto);
        Task<OptionChainDto> GetOptionChainAsync(int userId, OptionChainRequestDto dto);
        Task<DhanOrderResultDto> PlaceOptionOrderAsync(int userId, DhanOptionOrderRequestDto dto);
    }
}
