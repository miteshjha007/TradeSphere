using System;
using System.Text.Json.Serialization;

namespace TradeSphere.Application.DTOs
{
    public class Mt5AccountDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public long Login { get; set; }
        public string LoginPreview { get; set; }
        public string Server { get; set; }
        public string AccountType { get; set; }
        public string Currency { get; set; }
        public int Leverage { get; set; }
        public bool TradingEnabled { get; set; }
        public string Status { get; set; }
        public decimal? Balance { get; set; }
        public decimal? Equity { get; set; }
        public decimal? FreeMargin { get; set; }
        public DateTime? LastSyncedAt { get; set; }
        public string? LastError { get; set; }
    }

    public class ConnectMt5AccountDto
    {
        public string Name { get; set; }
        public long Login { get; set; }
        public string Server { get; set; }
        public string Password { get; set; }
        public string AccountType { get; set; } = "Demo";
        public string Currency { get; set; } = "USD";
        public int Leverage { get; set; }
        public bool TradingEnabled { get; set; }
    }

    public class Mt5ConnectionTestResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public decimal? Balance { get; set; }
        public decimal? Equity { get; set; }
        public decimal? FreeMargin { get; set; }
        public string? BridgeEndpoint { get; set; }
    }

    public class Mt5BridgeAccountRequestDto
    {
        public long Login { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
    }

    public class Mt5BridgeAccountInfoDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public long? Login { get; set; }
        public string? Server { get; set; }
        public string? Currency { get; set; }
        public int? Leverage { get; set; }
        public decimal? Balance { get; set; }
        public decimal? Equity { get; set; }
        public decimal? FreeMargin { get; set; }
    }

    public class Mt5BridgeOrderRequestDto
    {
        public long Login { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
        public string Symbol { get; set; }
        public string Side { get; set; }
        public decimal Volume { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
        public string? Comment { get; set; }
    }

    public class Mt5BridgeOrderResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string? OrderId { get; set; }
        public string? DealId { get; set; }
        public decimal? Price { get; set; }
        public string? RawResponse { get; set; }
    }

    public class Mt5BridgeClosePositionRequestDto
    {
        public long Login { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
        public string Symbol { get; set; }
        public long PositionTicket { get; set; }
        public decimal Volume { get; set; }
        public string? Comment { get; set; }
    }

    public class Mt5BridgeCandlesRequestDto
    {
        public long Login { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
        public string Symbol { get; set; }
        public string Resolution { get; set; }
        public long StartTime { get; set; }
        public long EndTime { get; set; }
    }

    public class Mt5BridgeCandleDto
    {
        public long Time { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }

    public class Mt5BridgeCandlesResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<Mt5BridgeCandleDto> Candles { get; set; } = new();
    }

    public class Mt5BridgeDealsRequestDto
    {
        public long Login { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
        public string? Symbol { get; set; }
        public long StartTime { get; set; }
        public long EndTime { get; set; }
    }

    public class Mt5BridgeDealDto
    {
        public long Ticket { get; set; }
        public long Order { get; set; }
        [JsonPropertyName("position_id")]
        public long Position_Id { get; set; }
        public long Time { get; set; }
        public string Symbol { get; set; }
        public decimal Volume { get; set; }
        public decimal Price { get; set; }
        public decimal Profit { get; set; }
        public decimal Commission { get; set; }
        public decimal Swap { get; set; }
        public int Entry { get; set; }
        public int Type { get; set; }
        public string? Comment { get; set; }
    }

    public class Mt5BridgeDealsResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<Mt5BridgeDealDto> Deals { get; set; } = new();
    }

    public class Mt5BridgePositionsRequestDto
    {
        public long Login { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
        public string? Symbol { get; set; }
    }

    public class Mt5BridgePositionDto
    {
        public long Ticket { get; set; }
        public long Time { get; set; }
        public int Type { get; set; }
        public decimal Volume { get; set; }
        [JsonPropertyName("price_open")]
        public decimal Price_Open { get; set; }
        [JsonPropertyName("price_current")]
        public decimal Price_Current { get; set; }
        public decimal Profit { get; set; }
        public decimal Swap { get; set; }
        public string Symbol { get; set; }
        public string? Comment { get; set; }
    }

    public class Mt5BridgePositionsResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<Mt5BridgePositionDto> Positions { get; set; } = new();
    }

    public class Mt5BridgeTickRequestDto
    {
        public long Login { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
        public string Symbol { get; set; }
    }

    public class Mt5BridgeTickResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Symbol { get; set; }
        public decimal? Bid { get; set; }
        public decimal? Ask { get; set; }
        public decimal? Last { get; set; }
        public long? Time { get; set; }
    }

    public class Mt5SymbolMappingDto
    {
        public int Id { get; set; }
        public int Mt5AccountId { get; set; }
        public string AccountName { get; set; }
        public string StrategySymbol { get; set; }
        public string BrokerSymbol { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
    }

    public class UpsertMt5SymbolMappingDto
    {
        public int Mt5AccountId { get; set; }
        public string StrategySymbol { get; set; }
        public string BrokerSymbol { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }
    }
}
