namespace TradeSphere.Application.DTOs
{
    public class ConnectionTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal? WalletBalance { get; set; }
        public string Currency { get; set; }
        public decimal? CoinsBalance { get; set; }
        public string CoinsCurrency { get; set; }
        public decimal? FuturesBalance { get; set; }
        public string FuturesCurrency { get; set; }
    }
}
