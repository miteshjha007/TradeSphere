using System.Collections.Generic;

namespace TradeSphere.Application.DTOs
{
    public class DashboardDto
    {
        public decimal TotalBalance { get; set; }
        public decimal TotalPnl { get; set; }
        public int ActiveStrategies { get; set; }
        public int ConnectedExchanges { get; set; }
        public List<RecentTradeDto> RecentTrades { get; set; }
        public List<StrategyPerformanceDto> TopStrategies { get; set; }
    }

    public class RecentTradeDto
    {
        public string Symbol { get; set; }
        public string Side { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public string Status { get; set; }
        public string TimeAgo { get; set; }
    }

    public class StrategyPerformanceDto
    {
        public string Name { get; set; }
        public decimal Pnl { get; set; }
        public decimal WinRate { get; set; }
    }
}
