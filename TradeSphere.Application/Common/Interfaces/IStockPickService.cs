using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IStockPickService
    {
        Task<StockPickDashboardDto> GetIntradayPicksAsync(CancellationToken cancellationToken = default);
        Task<StockPickDashboardDto> GetLongTermPicksAsync(CancellationToken cancellationToken = default);
        Task<StockAnalysisDto> AnalyzeStockAsync(StockAnalysisRequestDto request, CancellationToken cancellationToken = default);
    }
}


