using System;
using System.Collections.Generic;

namespace TradeSphere.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } // "Admin" or "User"

        // Navigation properties
        public ICollection<UserExchange> UserExchanges { get; set; }
        public ICollection<UserStrategy> UserStrategies { get; set; }
        public ICollection<Trade> Trades { get; set; }
    }
}
