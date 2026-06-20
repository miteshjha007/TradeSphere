using System.Collections.Generic;
using System.Threading.Tasks;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public class CandleDto
    {
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Time { get; set; } // Unix timestamp in seconds
    }

    public interface IDeltaExchangeClient
    {
        Task<int?> GetProductIdAsync(string symbol, string baseUrl = null);
        Task<decimal?> GetTickerPriceAsync(string symbol, string baseUrl = null);
        Task<string> PlaceMarketOrderAsync(string apiKey, string apiSecret, int productId, string side, decimal quantity, string baseUrl = null);
        Task<List<CandleDto>> GetCandlesAsync(string symbol, string resolution, long? startTimeInSeconds = null, long? endTimeInSeconds = null, string baseUrl = null);
        Task<ConnectionTestResult> TestConnectionAsync(string apiKey, string apiSecret, string baseUrl = null);
        Task<List<PositionDto>> GetPositionsAsync(string apiKey, string apiSecret, string baseUrl = null);
        Task<decimal?> GetContractValueAsync(string symbol, string baseUrl = null);
    }
}

