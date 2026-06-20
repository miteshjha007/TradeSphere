using System;

namespace TradeSphere.Domain.Entities
{
    public class Trade : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; }

        public int? UserStrategyId { get; set; }
        public UserStrategy UserStrategy { get; set; }

        public int ExchangeId { get; set; }
        public Exchange Exchange { get; set; }

        public string Symbol { get; set; }
        public string Side { get; set; } // "Buy", "Sell"
        public string OrderType { get; set; } = "Market";
        public decimal? Price { get; set; }
        public decimal Quantity { get; set; }
        public string Status { get; set; } // Open, Filled, Canceled, Failed
        public DateTime? ExecutedAt { get; set; }
        public decimal Pnl { get; set; }
        public string ExternalOrderId { get; set; }
        public string? ErrorReason { get; set; }
        public string? BrokerResponse { get; set; }
    }
}
