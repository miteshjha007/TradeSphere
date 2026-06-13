using System;

namespace TradeSphere.Domain.Entities
{
    public class UserExchange : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; }

        public int ExchangeId { get; set; }
        public Exchange Exchange { get; set; }

        public string Name { get; set; }
        public string ApiKey { get; set; } // Encrypted
        public string ApiSecret { get; set; } // Encrypted
        public string Status { get; set; } = "Active"; // Active, Error, Expired
    }
}
