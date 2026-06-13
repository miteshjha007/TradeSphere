using System;

namespace TradeSphere.Domain.Entities
{
    public class Backtest : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; }

        public int? StrategyId { get; set; }
        public Strategy Strategy { get; set; }

        public string Symbol { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal InitialCapital { get; set; } = 1000;
        public decimal? TotalReturn { get; set; }
        public decimal? MaxDrawdown { get; set; }
        public int? TotalTrades { get; set; }
        public decimal? WinRate { get; set; }
        public string ResultJson { get; set; } // Detailed chart data
        public DateTime RanAt { get; set; } = DateTime.UtcNow;
    }
}
