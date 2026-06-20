namespace TradeSphere.Application.DTOs
{
    public class ConnectionTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal? WalletBalance { get; set; }
        public string Currency { get; set; }
    }
}
