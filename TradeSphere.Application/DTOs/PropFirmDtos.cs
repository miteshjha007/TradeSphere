using System;

namespace TradeSphere.Application.DTOs
{
    public class PropFirmDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? WebsiteUrl { get; set; }
        public string Status { get; set; }
        public string? Notes { get; set; }
    }

    public class CreatePropFirmDto
    {
        public string Name { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? Notes { get; set; }
    }

    public class PropFirmAccountDto
    {
        public int Id { get; set; }
        public int PropFirmId { get; set; }
        public string PropFirmName { get; set; }
        public int? Mt5AccountId { get; set; }
        public string? Mt5AccountName { get; set; }
        public string Name { get; set; }
        public decimal AccountSize { get; set; }
        public decimal ProfitTarget { get; set; }
        public decimal DailyDrawdownLimit { get; set; }
        public decimal MaxDrawdownLimit { get; set; }
        public int MinimumTradingDays { get; set; }
        public decimal MaxRiskPerTradePercent { get; set; }
        public bool NewsTradingAllowed { get; set; }
        public bool WeekendHoldingAllowed { get; set; }
        public string Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public string? Notes { get; set; }
    }

    public class CreatePropFirmAccountDto
    {
        public int PropFirmId { get; set; }
        public int? Mt5AccountId { get; set; }
        public string Name { get; set; }
        public decimal AccountSize { get; set; }
        public decimal ProfitTarget { get; set; }
        public decimal DailyDrawdownLimit { get; set; }
        public decimal MaxDrawdownLimit { get; set; }
        public int MinimumTradingDays { get; set; }
        public decimal MaxRiskPerTradePercent { get; set; } = 1m;
        public bool NewsTradingAllowed { get; set; }
        public bool WeekendHoldingAllowed { get; set; }
        public DateTime? StartedAt { get; set; }
        public string? Notes { get; set; }
    }
}
