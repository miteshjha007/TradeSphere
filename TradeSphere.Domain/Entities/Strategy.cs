using System;

namespace TradeSphere.Domain.Entities
{
    public class Strategy : BaseEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string LogicType { get; set; } // 'MovingAverage', 'RSI', 'Custom'
        public string DefaultConfig { get; set; } // JSON as string
        public bool IsPublic { get; set; } = true;
        public int? CreatedBy { get; set; } // Null for system strategies
        public User Creator { get; set; }
    }
}
