using System.Collections.Generic;
using System.Threading.Tasks;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IStrategyService
    {
        Task<List<StrategyDto>> GetAvailableStrategiesAsync();
        Task<List<UserStrategyDto>> GetUserStrategiesAsync(int userId);
        Task<UserStrategyDto> DeployStrategyAsync(int userId, DeployStrategyDto dto);
        Task<StrategyDto> CreateStrategyAsync(int userId, CreateStrategyDto dto);
        Task ToggleStrategyStatusAsync(int userId, int userStrategyId, string status); // "Running", "Stopped"
        Task DeleteUserStrategyAsync(int userId, int userStrategyId);
    }
}
