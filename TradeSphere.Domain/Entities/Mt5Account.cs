using System;

namespace TradeSphere.Domain.Entities
{
    public class Mt5Account : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; }

        public string Name { get; set; }
        public long Login { get; set; }
        public string Server { get; set; }
        public string EncryptedPassword { get; set; }
        public string AccountType { get; set; } = "Demo";
        public string Currency { get; set; } = "USD";
        public int Leverage { get; set; }
        public bool TradingEnabled { get; set; }
        public string Status { get; set; } = "PendingBridge";
        public decimal? Balance { get; set; }
        public decimal? Equity { get; set; }
        public decimal? FreeMargin { get; set; }
        public DateTime? LastSyncedAt { get; set; }
        public string? LastError { get; set; }
    }
}
