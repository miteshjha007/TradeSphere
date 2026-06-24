using System;

namespace TradeSphere.Domain.Entities
{
    public class StrategyHealthSnapshot : BaseEntity
    {
        public int UserStrategyId { get; set; }
        public UserStrategy UserStrategy { get; set; }

        public string Symbol { get; set; }
        public string Resolution { get; set; }
        public DateTime LastCheckedAt { get; set; }
        public decimal? Price { get; set; }
        public int Position { get; set; }
        public bool IsEntryEligible { get; set; }
        public string? SuggestedSide { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string? DetailsJson { get; set; }
    }
}
