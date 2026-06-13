using System;

namespace TradeSphere.Domain.Entities
{
    public class Coin : BaseEntity
    {
        public string Symbol { get; set; }
        public string Name { get; set; }
        public int? ExchangeId { get; set; }
        public Exchange Exchange { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
