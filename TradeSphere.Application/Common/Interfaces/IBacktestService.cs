using System.Collections.Generic;
using System.Threading.Tasks;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IBacktestService
    {
        Task<List<BacktestDto>> GetUserBacktestsAsync(int userId);
        Task<BacktestResultDetailsDto> GetBacktestDetailsAsync(int userId, int backtestId);
        Task<BacktestDto> RunBacktestAsync(int userId, RunBacktestDto dto);
        Task DeleteBacktestAsync(int userId, int backtestId);
    }
}
