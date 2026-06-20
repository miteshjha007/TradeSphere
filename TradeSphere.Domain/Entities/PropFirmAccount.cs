using System;

namespace TradeSphere.Domain.Entities
{
    public class PropFirmAccount : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; }

        public int PropFirmId { get; set; }
        public PropFirm PropFirm { get; set; }

        public int? Mt5AccountId { get; set; }
        public Mt5Account? Mt5Account { get; set; }

        public string Name { get; set; }
        public decimal AccountSize { get; set; }
        public decimal ProfitTarget { get; set; }
        public decimal DailyDrawdownLimit { get; set; }
        public decimal MaxDrawdownLimit { get; set; }
        public int MinimumTradingDays { get; set; }
        public decimal MaxRiskPerTradePercent { get; set; } = 1m;
        public bool NewsTradingAllowed { get; set; }
        public bool WeekendHoldingAllowed { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime? StartedAt { get; set; }
        public string? Notes { get; set; }
    }
}
