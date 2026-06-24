using System;
using System.Collections.Generic;

namespace TradeSphere.Application.DTOs
{
    public class DhanAccountDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string ClientIdPreview { get; set; } = string.Empty;
    }

    public class ConnectDhanAccountDto
    {
        public string Name { get; set; } = string.Empty;
        public string DhanClientId { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
    }

    public class DhanConnectionTestResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal? AvailableBalance { get; set; }
        public decimal? UtilizedAmount { get; set; }
        public decimal? WithdrawableBalance { get; set; }
        public string Currency { get; set; } = "INR";
    }

    public class IndianUnderlyingDto
    {
        public string Symbol { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int UnderlyingScrip { get; set; }
        public string UnderlyingSegment { get; set; } = "IDX_I";
        public int LotSize { get; set; }
        public int StrikeStep { get; set; }
    }

    public class OptionChainRequestDto
    {
        public int DhanAccountId { get; set; }
        public string Underlying { get; set; } = "NIFTY";
        public string Expiry { get; set; } = string.Empty;
    }

    public class OptionExpiryRequestDto
    {
        public int DhanAccountId { get; set; }
        public string Underlying { get; set; } = "NIFTY";
    }

    public class OptionChainDto
    {
        public string Underlying { get; set; } = string.Empty;
        public string Expiry { get; set; } = string.Empty;
        public decimal UnderlyingLastPrice { get; set; }
        public List<OptionChainRowDto> Rows { get; set; } = new();
    }

    public class OptionChainRowDto
    {
        public decimal StrikePrice { get; set; }
        public OptionLegDto? Call { get; set; }
        public OptionLegDto? Put { get; set; }
    }

    public class OptionLegDto
    {
        public string OptionType { get; set; } = string.Empty; // CE, PE
        public string SecurityId { get; set; } = string.Empty;
        public decimal LastPrice { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal ImpliedVolatility { get; set; }
        public long OpenInterest { get; set; }
        public long PreviousOpenInterest { get; set; }
        public long Volume { get; set; }
        public decimal TopBidPrice { get; set; }
        public long TopBidQuantity { get; set; }
        public decimal TopAskPrice { get; set; }
        public long TopAskQuantity { get; set; }
        public decimal Delta { get; set; }
        public decimal Theta { get; set; }
        public decimal Gamma { get; set; }
        public decimal Vega { get; set; }
    }

    public class DhanOptionOrderRequestDto
    {
        public int DhanAccountId { get; set; }
        public string Underlying { get; set; } = "NIFTY";
        public string Expiry { get; set; } = string.Empty;
        public decimal StrikePrice { get; set; }
        public string OptionType { get; set; } = "CE";
        public string SecurityId { get; set; } = string.Empty;
        public string TransactionType { get; set; } = "BUY";
        public int Quantity { get; set; }
        public string ProductType { get; set; } = "INTRADAY";
        public string OrderType { get; set; } = "MARKET";
        public decimal? Price { get; set; }
    }

    public class DhanOrderResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? OrderId { get; set; }
        public string? OrderStatus { get; set; }
        public string? RawResponse { get; set; }
    }
}
