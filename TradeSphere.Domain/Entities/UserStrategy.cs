using System;

namespace TradeSphere.Domain.Entities
{
    public class UserStrategy : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; }

        public int StrategyId { get; set; }
        public Strategy Strategy { get; set; }

        public int ExchangeId { get; set; }
        public Exchange Exchange { get; set; }

        public int? UserExchangeId { get; set; }
        public UserExchange UserExchange { get; set; }

        public string ExecutionProvider { get; set; } = "Delta"; // Delta, MT5
        public int? Mt5AccountId { get; set; }
        public Mt5Account? Mt5Account { get; set; }

        public string Symbol { get; set; }
        public string Config { get; set; } // JSON as string
        public string Status { get; set; } = "Stopped"; // Running, Stopped, Error
        public DateTime? StartedAt { get; set; }
        public DateTime? StoppedAt { get; set; }
    }
}
