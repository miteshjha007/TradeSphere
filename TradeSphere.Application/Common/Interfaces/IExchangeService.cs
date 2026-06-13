using System.Collections.Generic;
using System.Threading.Tasks;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IExchangeService
    {
        Task<List<ExchangeDto>> GetSupportedExchangesAsync();
        Task<List<UserExchangeDto>> GetUserExchangesAsync(int userId);
        Task<UserExchangeDto> ConnectExchangeAsync(int userId, ConnectExchangeDto dto);
        Task DeleteUserExchangeAsync(int userId, int userExchangeId);
    }
}
