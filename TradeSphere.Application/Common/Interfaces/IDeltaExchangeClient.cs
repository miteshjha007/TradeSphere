using System.Collections.Generic;
using System.Threading.Tasks;

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
        Task<int?> GetProductIdAsync(string symbol);
        Task<decimal?> GetTickerPriceAsync(string symbol);
        Task<string> PlaceMarketOrderAsync(string apiKey, string apiSecret, int productId, string side, decimal quantity);
        Task<List<CandleDto>> GetCandlesAsync(string symbol, string resolution, long? startTimeInSeconds = null, long? endTimeInSeconds = null);
    }
}
