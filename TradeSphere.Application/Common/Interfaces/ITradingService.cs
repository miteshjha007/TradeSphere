using System.Collections.Generic;
using System.Threading.Tasks;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface ITradingService
    {
        Task<List<TradeDto>> GetTradesAsync(int userId);
        Task<List<PositionDto>> GetPositionsAsync(int userId);
        Task<TradingOverviewDto> GetOverviewAsync(int userId);
        Task DeleteAllTradesAsync(int userId);
        Task ResumeMt5AutoRiskAsync(int userId, int tradeId);
    }
}
