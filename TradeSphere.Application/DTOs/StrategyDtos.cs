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
        public string ExecutionProvider { get; set; }
        public int? Mt5AccountId { get; set; }
        public string? Mt5AccountName { get; set; }
        public string Symbol { get; set; }
        public string Config { get; set; }
        public string Status { get; set; } // Running, Stopped
        public DateTime? StartedAt { get; set; }
        public StrategyHealthSnapshotDto? Health { get; set; }
    }

    public class StrategyHealthSnapshotDto
    {
        public DateTime LastCheckedAt { get; set; }
        public string Symbol { get; set; }
        public string Resolution { get; set; }
        public decimal? Price { get; set; }
        public int Position { get; set; }
        public bool IsEntryEligible { get; set; }
        public string? SuggestedSide { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string? DetailsJson { get; set; }
    }

    public class DeployStrategyDto
    {
        public int StrategyId { get; set; }
        public string ExecutionProvider { get; set; } = "Delta";
        public int? UserExchangeId { get; set; }  // ID of the user's connected exchange account
        public int? Mt5AccountId { get; set; }
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
