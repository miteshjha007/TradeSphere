using System;

namespace TradeSphere.Application.DTOs
{
    public class BacktestDto
    {
        public int Id { get; set; }
        public int StrategyId { get; set; }
        public string StrategyName { get; set; }
        public string Symbol { get; set; }
        public string Interval { get; set; } // 1h, 4h, 1d
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalReturn { get; set; }
        public decimal MaxDrawdown { get; set; }
        public int TradeCount { get; set; }
        public string Status { get; set; } // Completed, Failed
    }

    public class BacktestResultDetailsDto : BacktestDto
    {
        public string ResultJson { get; set; } // Full trade list and equity curve
    }

    public class RunBacktestDto
    {
        public int StrategyId { get; set; }
        public string Symbol { get; set; }
        public string Interval { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal InitialCapital { get; set; } = 10000;
        public string? ConfigOverrides { get; set; } // Optional JSON to override strategy defaults
        public string DataSource { get; set; } = "Delta"; // Delta, MT5, CoinDCX
        public int? Mt5AccountId { get; set; }
    }
}
