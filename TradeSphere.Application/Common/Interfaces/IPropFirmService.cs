using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IPropFirmService
    {
        Task<IReadOnlyList<PropFirmDto>> GetFirmsAsync(int userId);
        Task<PropFirmDto> CreateFirmAsync(int userId, CreatePropFirmDto dto);
        Task DeleteFirmAsync(int userId, int id);
        Task<IReadOnlyList<PropFirmAccountDto>> GetAccountsAsync(int userId);
        Task<PropFirmAccountDto> CreateAccountAsync(int userId, CreatePropFirmAccountDto dto);
        Task DeleteAccountAsync(int userId, int id);
    }
}
