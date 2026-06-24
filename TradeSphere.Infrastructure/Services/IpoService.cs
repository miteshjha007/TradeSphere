using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Infrastructure.Services
{
    public class IpoService : IIpoService
    {
        private const string SebiRhpUrl = "https://www.sebi.gov.in/sebiweb/home/HomeAction.do?doListing=yes&sid=3&smid=11&ssid=15";
        private const string ChittorgarhLatestIpoUrl = "https://www.chittorgarh.com/report/latest-ipo-issues-in-india/82/";
        private static readonly string[] DateFormats = { "MMM d, yyyy", "MMM dd, yyyy" };
        private readonly HttpClient _httpClient;

        public IpoService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IpoDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
        {
            var dashboard = new IpoDashboardDto
            {
                LastUpdatedAt = DateTime.UtcNow
            };

            try
            {
                var sebiFilings = await FetchSebiRhpFilingsAsync(cancellationToken);
                var enrichedIpos = await FetchChittorgarhIposAsync(cancellationToken);
                var allIpos = MergeIpoItems(sebiFilings, enrichedIpos);

                foreach (var filing in allIpos)
                {
                    ApplyScore(filing);
                }

                dashboard.RecentFilings = sebiFilings
                    .OrderByDescending(i => i.FilingDate)
                    .Take(25)
                    .ToList();

                dashboard.Current = allIpos
                    .Where(i => i.OpenDate.HasValue && i.CloseDate.HasValue && i.OpenDate.Value.Date <= DateTime.UtcNow.Date && i.CloseDate.Value.Date >= DateTime.UtcNow.Date)
                    .OrderByDescending(i => i.Score)
                    .ThenBy(i => i.CloseDate)
                    .Take(10)
                    .ToList();

                dashboard.Upcoming = allIpos
                    .Where(i => !i.OpenDate.HasValue || i.OpenDate.Value.Date >= DateTime.UtcNow.Date)
                    .OrderByDescending(i => i.Score)
                    .ThenBy(i => i.OpenDate ?? DateTime.MaxValue)
                    .ThenByDescending(i => i.FilingDate)
                    .Take(15)
                    .ToList();

                dashboard.TopUpcoming = dashboard.Upcoming.Take(5).ToList();
                dashboard.TopCurrent = dashboard.Current.Take(5).ToList();
                dashboard.Warnings.Add("SEBI RHP is the official filing source. IPO calendar fields are enriched from free public pages where available.");
                dashboard.Warnings.Add("GMP and subscription are unofficial/live signals and remain optional until a reliable enrichment source is added.");
            }
            catch (Exception ex)
            {
                dashboard.Warnings.Add($"Could not refresh IPO data: {ex.Message}");
            }

            return dashboard;
        }

        private async Task<List<IpoItemDto>> FetchSebiRhpFilingsAsync(CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, SebiRhpUrl);
            request.Headers.UserAgent.ParseAdd("TradeSphere/1.0");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var filings = new List<IpoItemDto>();

            var rowPattern = new Regex(
                @"<tr[^>]*>.*?<td[^>]*>\s*(?<date>[A-Z][a-z]{2}\s+\d{1,2},\s+\d{4})\s*</td>.*?<a(?<attrs>[^>]*)href\s*=\s*[""'](?<href>[^""']+)[""'][^>]*>\s*(?<title>.*?)\s*</a>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in rowPattern.Matches(html))
            {
                var attrTitle = Regex.Match(match.Groups["attrs"].Value, @"title\s*=\s*[""'](?<title>[^""']+)[""']", RegexOptions.IgnoreCase);
                var title = CleanTitle(attrTitle.Success
                    ? attrTitle.Groups["title"].Value
                    : match.Groups["title"].Value);

                if (!title.Contains("RHP", StringComparison.OrdinalIgnoreCase) &&
                    !title.Contains("Red Herring", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                filings.Add(CreateFiling(match.Groups["date"].Value, title, match.Groups["href"].Value));
            }

            if (filings.Count == 0)
            {
                var plainText = ToPlainText(html);
                var fallbackPattern = new Regex(
                    @"(?<date>[A-Z][a-z]{2}\s+\d{1,2},\s+\d{4})\s+(?<title>.*?(?:RHP|Red Herring Prospectus))(?=\s+[A-Z][a-z]{2}\s+\d{1,2},\s+\d{4}|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match match in fallbackPattern.Matches(plainText))
                {
                    filings.Add(CreateFiling(match.Groups["date"].Value, CleanTitle(match.Groups["title"].Value), string.Empty));
                }
            }

            return filings
                .GroupBy(i => i.CompanyName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(i => i.FilingDate).First())
                .OrderByDescending(i => i.FilingDate)
                .ToList();
        }

        private async Task<List<IpoItemDto>> FetchChittorgarhIposAsync(CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ChittorgarhLatestIpoUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 TradeSphere/1.0");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var links = ExtractChittorgarhLinks(html).Take(30).ToList();
            var items = new List<IpoItemDto>();

            foreach (var link in links)
            {
                try
                {
                    var item = await FetchChittorgarhDetailAsync(link, cancellationToken);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
                catch
                {
                    // Public enrichment pages can occasionally throttle or change shape.
                    // Keep SEBI data available instead of failing the whole dashboard.
                }
            }

            return items
                .GroupBy(i => NormalizeCompanyKey(i.CompanyName), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(i => i.OpenDate ?? i.FilingDate ?? DateTime.MinValue).First())
                .ToList();
        }

        private async Task<IpoItemDto?> FetchChittorgarhDetailAsync(IpoLink link, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, link.Url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 TradeSphere/1.0");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var text = ToPlainText(html);
            var companyName = CleanCompanyName(link.Title);

            var item = new IpoItemDto
            {
                CompanyName = companyName,
                Source = "Chittorgarh Calendar",
                Segment = GuessSegment(text),
                DocumentUrl = link.Url,
                MissingSignals = new List<string>
                {
                    "Open/close dates",
                    "Listing date",
                    "Price band",
                    "Subscription",
                    "GMP",
                    "Issue size",
                    "Financial ratios"
                }
            };

            var dateMatch = Regex.Match(
                text,
                @"opens\s+for\s+subscription\s+on\s+(?<open>[A-Z][a-z]{2}\s+\d{1,2},\s+\d{4})\s+and\s+closes\s+on\s+(?<close>[A-Z][a-z]{2}\s+\d{1,2},\s+\d{4})",
                RegexOptions.IgnoreCase);
            if (dateMatch.Success)
            {
                item.OpenDate = ParseIpoDate(dateMatch.Groups["open"].Value);
                item.CloseDate = ParseIpoDate(dateMatch.Groups["close"].Value);
            }

            var listingMatch = Regex.Match(
                text,
                @"tentative\s+listing\s+date\s+fixed\s+as\s+(?<listing>[A-Z][a-z]{2}\s+\d{1,2},\s+\d{4})",
                RegexOptions.IgnoreCase);
            if (listingMatch.Success)
            {
                item.ListingDate = ParseIpoDate(listingMatch.Groups["listing"].Value);
            }

            var issueSizeMatch = Regex.Match(
                text,
                @"(?:book\s+build\s+issue|fixed\s+price\s+issue|IPO\s+is\s+a\s+).*?of\s+₹?\s*(?<size>[\d,.]+)\s*crores?",
                RegexOptions.IgnoreCase);
            if (issueSizeMatch.Success && decimal.TryParse(issueSizeMatch.Groups["size"].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var issueSize))
            {
                item.IssueSizeCrore = issueSize;
            }

            var priceBandMatch = Regex.Match(
                text,
                @"price\s+band\s+(?:at|of|is|set\s+at)?\s*₹?\s*(?<low>[\d,.]+|\[\.?\])\s*(?:to|-)\s*₹?\s*(?<high>[\d,.]+|\[\.?\])\s+per\s+share",
                RegexOptions.IgnoreCase);
            if (priceBandMatch.Success && !priceBandMatch.Value.Contains("[", StringComparison.Ordinal))
            {
                item.PriceBand = $"Rs. {priceBandMatch.Groups["low"].Value}-{priceBandMatch.Groups["high"].Value}";
            }

            item.Status = GetStatus(item);
            RemoveAvailableSignals(item);
            return item;
        }

        private static IpoItemDto CreateFiling(string dateText, string title, string href)
        {
            var filingDate = ParseIpoDate(dateText.Trim());
            var normalizedTitle = CleanTitle(title);
            var companyName = CleanCompanyName(normalizedTitle);

            return new IpoItemDto
            {
                CompanyName = companyName,
                FilingDate = filingDate,
                Status = "RHP Filed",
                Segment = GuessSegment(normalizedTitle),
                Source = "SEBI RHP",
                DocumentUrl = NormalizeUrl(href),
                MissingSignals = new List<string>
                {
                    "Open/close dates",
                    "Listing date",
                    "Price band",
                    "Subscription",
                    "GMP",
                    "Issue size",
                    "Financial ratios"
                }
            };
        }

        private static void ApplyScore(IpoItemDto ipo)
        {
            var score = 35m;
            ipo.Reasons.Clear();

            if (ipo.Source.Contains("SEBI", StringComparison.OrdinalIgnoreCase))
            {
                ipo.Reasons.Add("Official RHP filing found on SEBI.");
            }

            if (ipo.OpenDate.HasValue && ipo.CloseDate.HasValue)
            {
                score += 20;
                ipo.Reasons.Add($"IPO window: {ipo.OpenDate:dd MMM yyyy} to {ipo.CloseDate:dd MMM yyyy}.");
            }

            if (ipo.PriceBand.Length > 0)
            {
                score += 10;
                ipo.Reasons.Add($"Price band available: {ipo.PriceBand}.");
            }

            if (ipo.IssueSizeCrore.HasValue)
            {
                score += 5;
                ipo.Reasons.Add($"Issue size: Rs. {ipo.IssueSizeCrore:0.##} Cr.");
            }

            if (ipo.FilingDate.HasValue)
            {
                var ageDays = Math.Max(0, (DateTime.UtcNow.Date - ipo.FilingDate.Value.Date).Days);
                if (ageDays <= 7)
                {
                    score += 25;
                    ipo.Reasons.Add("Fresh RHP filing in the last 7 days.");
                }
                else if (ageDays <= 30)
                {
                    score += 15;
                    ipo.Reasons.Add("Recent RHP filing in the last 30 days.");
                }
            }

            if (ipo.TotalSubscriptionX.HasValue)
            {
                score += Math.Min(25, ipo.TotalSubscriptionX.Value * 3);
                ipo.Reasons.Add($"Subscription demand: {ipo.TotalSubscriptionX:0.00}x.");
                ipo.MissingSignals.Remove("Subscription");
            }

            if (ipo.QibSubscriptionX.HasValue)
            {
                score += Math.Min(20, ipo.QibSubscriptionX.Value * 2);
                ipo.Reasons.Add($"QIB demand: {ipo.QibSubscriptionX:0.00}x.");
            }

            if (ipo.GmpPercent.HasValue)
            {
                score += Math.Clamp(ipo.GmpPercent.Value / 2, -15, 15);
                ipo.Reasons.Add($"Unofficial GMP sentiment: {ipo.GmpPercent:0.##}%.");
                ipo.MissingSignals.Remove("GMP");
            }

            ipo.Score = Math.Clamp(score, 0, 100);
            ipo.Verdict = ipo.Score >= 75 ? "Strong Watch" : ipo.Score >= 55 ? "Watch" : "Needs Data";
        }

        private static List<IpoItemDto> MergeIpoItems(List<IpoItemDto> officialFilings, List<IpoItemDto> enrichedItems)
        {
            var merged = officialFilings
                .GroupBy(i => NormalizeCompanyKey(i.CompanyName), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var enriched in enrichedItems)
            {
                var key = NormalizeCompanyKey(enriched.CompanyName);
                var officialKey = merged.Keys.FirstOrDefault(k => IsLikelySameCompany(k, key));

                if (officialKey == null)
                {
                    merged[key] = enriched;
                    continue;
                }

                var target = merged[officialKey];
                target.OpenDate ??= enriched.OpenDate;
                target.CloseDate ??= enriched.CloseDate;
                target.ListingDate ??= enriched.ListingDate;
                target.IssueSizeCrore ??= enriched.IssueSizeCrore;
                target.TotalSubscriptionX ??= enriched.TotalSubscriptionX;
                target.QibSubscriptionX ??= enriched.QibSubscriptionX;
                target.NiiSubscriptionX ??= enriched.NiiSubscriptionX;
                target.RetailSubscriptionX ??= enriched.RetailSubscriptionX;

                if (string.IsNullOrWhiteSpace(target.PriceBand))
                {
                    target.PriceBand = enriched.PriceBand;
                }

                if (string.IsNullOrWhiteSpace(target.DocumentUrl))
                {
                    target.DocumentUrl = enriched.DocumentUrl;
                }

                target.Source = target.Source.Contains("Chittorgarh", StringComparison.OrdinalIgnoreCase)
                    ? target.Source
                    : $"{target.Source} + Chittorgarh";
                target.Status = GetStatus(target);
                RemoveAvailableSignals(target);
            }

            foreach (var item in merged.Values)
            {
                item.Status = GetStatus(item);
                RemoveAvailableSignals(item);
            }

            return merged.Values.ToList();
        }

        private static List<IpoLink> ExtractChittorgarhLinks(string html)
        {
            var links = new List<IpoLink>();
            var pattern = new Regex(
                @"<a[^>]+href\s*=\s*[""'](?<href>https://www\.chittorgarh\.com/ipo/[^""']+/)[""'][^>]+title\s*=\s*[""'](?<title>[^""']+IPO)[""']",
                RegexOptions.IgnoreCase);

            foreach (Match match in pattern.Matches(html))
            {
                var title = CleanTitle(match.Groups["title"].Value);
                var url = match.Groups["href"].Value;
                if (title.Contains("IPO", StringComparison.OrdinalIgnoreCase) && links.All(l => !string.Equals(l.Url, url, StringComparison.OrdinalIgnoreCase)))
                {
                    links.Add(new IpoLink(title, url));
                }
            }

            return links;
        }

        private static DateTime? ParseIpoDate(string value)
        {
            return DateTime.TryParseExact(value.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
        }

        private static string GetStatus(IpoItemDto item)
        {
            var today = DateTime.UtcNow.Date;
            if (item.OpenDate.HasValue && item.CloseDate.HasValue)
            {
                if (item.OpenDate.Value.Date <= today && item.CloseDate.Value.Date >= today)
                {
                    return "Open for Subscription";
                }

                if (item.OpenDate.Value.Date > today)
                {
                    return "Upcoming IPO";
                }

                return "Closed";
            }

            return item.Status;
        }

        private static void RemoveAvailableSignals(IpoItemDto item)
        {
            if (item.OpenDate.HasValue || item.CloseDate.HasValue)
            {
                item.MissingSignals.Remove("Open/close dates");
            }

            if (item.ListingDate.HasValue)
            {
                item.MissingSignals.Remove("Listing date");
            }

            if (!string.IsNullOrWhiteSpace(item.PriceBand))
            {
                item.MissingSignals.Remove("Price band");
            }

            if (item.IssueSizeCrore.HasValue)
            {
                item.MissingSignals.Remove("Issue size");
            }
        }

        private static string GuessSegment(string title)
        {
            if (title.Contains("SME", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("NSE SME", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("BSE SME", StringComparison.OrdinalIgnoreCase))
            {
                return "SME";
            }

            return "Mainboard/Pipeline";
        }

        private static string NormalizeUrl(string href)
        {
            if (string.IsNullOrWhiteSpace(href))
            {
                return string.Empty;
            }

            if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return href;
            }

            return href.StartsWith("/")
                ? $"https://www.sebi.gov.in{href}"
                : $"https://www.sebi.gov.in/{href}";
        }

        private static string CleanCompanyName(string value)
        {
            return CleanTitle(value)
                .Replace("- RHP", "", StringComparison.OrdinalIgnoreCase)
                .Replace("-RHP", "", StringComparison.OrdinalIgnoreCase)
                .Replace("RHP", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Red Herring Prospectus", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Abridged Prospectus", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Limited-", "Limited -", StringComparison.OrdinalIgnoreCase)
                .Replace(" IPO", "", StringComparison.OrdinalIgnoreCase)
                .Trim(' ', '-', ':');
        }

        private static string NormalizeCompanyKey(string value)
        {
            var cleaned = CleanCompanyName(value).ToLowerInvariant();
            cleaned = Regex.Replace(cleaned, @"\b(limited|ltd|private|pvt|india|solutions|technologies|industries)\b", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"[^a-z0-9]+", string.Empty);
            return cleaned;
        }

        private static bool IsLikelySameCompany(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return left.Equals(right, StringComparison.OrdinalIgnoreCase) ||
                   left.Contains(right, StringComparison.OrdinalIgnoreCase) ||
                   right.Contains(left, StringComparison.OrdinalIgnoreCase);
        }

        private static string CleanTitle(string value)
        {
            var decoded = WebUtility.HtmlDecode(value);
            var beforeBreak = Regex.Split(decoded, @"<br\s*/?>", RegexOptions.IgnoreCase).FirstOrDefault() ?? decoded;

            var cleaned = Regex.Replace(beforeBreak, "<.*?>", string.Empty)
                .Replace("&nbsp;", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ")
                .Trim();

            return Regex.Replace(cleaned, @"\s+", " ");
        }

        private static string ToPlainText(string html)
        {
            var decoded = WebUtility.HtmlDecode(html);
            var withBreaks = Regex.Replace(decoded, @"</?(tr|td|li|br|p|div|a)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            var plain = Regex.Replace(withBreaks, "<.*?>", " ");
            return Regex.Replace(plain, @"\s+", " ").Trim();
        }

        private sealed record IpoLink(string Title, string Url);
    }
}
