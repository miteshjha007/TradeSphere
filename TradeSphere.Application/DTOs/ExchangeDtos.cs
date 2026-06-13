namespace TradeSphere.Application.DTOs
{
    public class ExchangeDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string BaseUrl { get; set; }
    }

    public class UserExchangeDto
    {
        public int Id { get; set; }
        public string ExchangeName { get; set; }
        public string Name { get; set; } // Nickname
        public string Status { get; set; }
        public string ApiKeyPreview { get; set; } // Show partial key
    }

    public class ConnectExchangeDto
    {
        public int ExchangeId { get; set; }
        public string Name { get; set; }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
    }
}
