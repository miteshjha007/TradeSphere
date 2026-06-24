using System;
using System.Collections.Generic;

namespace TradeSphere.Application.DTOs
{
    public class IpoDashboardDto
    {
        public DateTime LastUpdatedAt { get; set; }
        public List<IpoItemDto> TopCurrent { get; set; } = new();
        public List<IpoItemDto> TopUpcoming { get; set; } = new();
        public List<IpoItemDto> Current { get; set; } = new();
        public List<IpoItemDto> Upcoming { get; set; } = new();
        public List<IpoItemDto> RecentFilings { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class IpoItemDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string Status { get; set; } = "Upcoming";
        public string Segment { get; set; } = "Unknown";
        public string Source { get; set; } = string.Empty;
        public DateTime? FilingDate { get; set; }
        public DateTime? OpenDate { get; set; }
        public DateTime? CloseDate { get; set; }
        public DateTime? ListingDate { get; set; }
        public string PriceBand { get; set; } = string.Empty;
        public decimal? IssueSizeCrore { get; set; }
        public decimal? GmpPercent { get; set; }
        public decimal? TotalSubscriptionX { get; set; }
        public decimal? QibSubscriptionX { get; set; }
        public decimal? NiiSubscriptionX { get; set; }
        public decimal? RetailSubscriptionX { get; set; }
        public decimal Score { get; set; }
        public string Verdict { get; set; } = "Watch";
        public string DocumentUrl { get; set; } = string.Empty;
        public List<string> Reasons { get; set; } = new();
        public List<string> MissingSignals { get; set; } = new();
    }
}
