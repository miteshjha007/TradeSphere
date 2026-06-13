using System;

namespace TradeSphere.Domain.Entities
{
    public class AiScreenerResult : BaseEntity
    {
        public string Symbol { get; set; }
        public string Timeframe { get; set; }
        public string Signal { get; set; } // Bullish, Bearish, Neutral
        public decimal ConfidenceScore { get; set; }
        public string AnalysisData { get; set; } // JSON
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
