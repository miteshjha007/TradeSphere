using System;
using System.Collections.Generic;

namespace TradeSphere.Application.DTOs
{
    public class StrategyDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string LogicType { get; set; }
        public string DefaultConfig { get; set; } // JSON
        public bool IsPublic { get; set; }
        public int? CreatedBy { get; set; }
    }

    public class UserStrategyDto
    {
        public int Id { get; set; }
        public int StrategyId { get; set; }
        public string StrategyName { get; set; }
        public int ExchangeId { get; set; }
        public string ExchangeName { get; set; }
        public string Symbol { get; set; }
        public string Config { get; set; }
        public string Status { get; set; } // Running, Stopped
        public DateTime? StartedAt { get; set; }
    }

    public class DeployStrategyDto
    {
        public int StrategyId { get; set; }
        public int ExchangeId { get; set; }
        public string Symbol { get; set; }
        public string Config { get; set; }
    }

    public class CreateStrategyDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string LogicType { get; set; }
        public string DefaultConfig { get; set; }
        public bool IsPublic { get; set; } = false;
    }
}
