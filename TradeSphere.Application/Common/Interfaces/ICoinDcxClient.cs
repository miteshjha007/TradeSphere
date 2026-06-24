using System.Collections.Generic;
using System.Threading.Tasks;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface ICoinDcxClient
    {
        Task<ConnectionTestResult> TestConnectionAsync(string apiKey, string apiSecret, string baseUrl = null);
        Task<List<CandleDto>> GetCandlesAsync(string symbol, string resolution, long? startTimeInSeconds = null, long? endTimeInSeconds = null);
        Task<decimal?> GetTickerPriceAsync(string symbol);
        Task<string> PlaceMarketOrderAsync(string apiKey, string apiSecret, string symbol, string side, decimal quantity, decimal leverage, decimal? takeProfitPrice = null, decimal? stopLossPrice = null, string baseUrl = null);
        Task<List<PositionDto>> GetPositionsAsync(string apiKey, string apiSecret, string baseUrl = null);
        Task<decimal?> GetContractValueAsync(string symbol);
    }
}
