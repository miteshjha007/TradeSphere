using System;

namespace TradeSphere.Domain.Entities
{
    public class Exchange : BaseEntity
    {
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
