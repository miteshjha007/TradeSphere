using System;
using System.Collections.Generic;

namespace TradeSphere.Application.DTOs
{
    public class TradeDto
    {
        public int Id { get; set; }
        public string StrategyName { get; set; }
        public string ExchangeName { get; set; }
        public string ExecutionProvider { get; set; }
        public string ExecutionAccount { get; set; }
        public string Symbol { get; set; }
        public string Side { get; set; }
        public string OrderType { get; set; }
        public decimal? Price { get; set; }
        public decimal Quantity { get; set; }
        public string Status { get; set; }
        public DateTime? ExecutedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal Pnl { get; set; }
        public string ExternalOrderId { get; set; }
        public string? ErrorReason { get; set; }
        public string? BrokerTicket { get; set; }
        public string ActivityType { get; set; }
    }

    public class PositionDto
    {
        public string ExchangeName { get; set; }
        public string Symbol { get; set; }
        public string Side { get; set; }
        public decimal Size { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal MarkPrice { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public decimal RealizedPnl { get; set; }
        public decimal Margin { get; set; }
        public string Status { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class TradingOverviewDto
    {
        public List<TradeDto> Trades { get; set; }
        public List<PositionDto> Positions { get; set; }
    }
}
