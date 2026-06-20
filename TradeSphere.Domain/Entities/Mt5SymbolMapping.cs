namespace TradeSphere.Domain.Entities
{
    public class Mt5SymbolMapping : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; }

        public int Mt5AccountId { get; set; }
        public Mt5Account Mt5Account { get; set; }

        public string StrategySymbol { get; set; }
        public string BrokerSymbol { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }
    }
}
