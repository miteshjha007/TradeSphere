using System;
using System.Collections.Generic;

namespace TradeSphere.Application.DTOs
{
    public class StockPickDashboardDto
    {
        public DateTime LastUpdatedAt { get; set; }
        public string Universe { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Methodology { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
        public List<StockPickDto> Picks { get; set; } = new();
    }

    public class StockPickDto
    {
        public int Rank { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Bias { get; set; } = string.Empty;
        public string Horizon { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public decimal LastPrice { get; set; }
        public decimal Score { get; set; }
        public decimal Change1DPercent { get; set; }
        public decimal Change5DPercent { get; set; }
        public decimal Change20DPercent { get; set; }
        public decimal VolumeRatio { get; set; }
        public decimal VolatilityPercent { get; set; }
        public decimal TrendStrengthPercent { get; set; }
        public string EntryZone { get; set; } = string.Empty;
        public string StopLoss { get; set; } = string.Empty;
        public string Target1 { get; set; } = string.Empty;
        public string Target2 { get; set; } = string.Empty;
        public List<string> Reasons { get; set; } = new();
    }
}
