using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.Infrastructure.Services
{
    public class StockPickService : IStockPickService
    {
        private readonly HttpClient _httpClient;

        private static readonly StockUniverseItem[] IntradayUniverse =
        {
            new("RELIANCE", "Reliance Industries"),
            new("HDFCBANK", "HDFC Bank"),
            new("ICICIBANK", "ICICI Bank"),
            new("INFY", "Infosys"),
            new("TCS", "Tata Consultancy Services"),
            new("SBIN", "State Bank of India"),
            new("AXISBANK", "Axis Bank"),
            new("KOTAKBANK", "Kotak Mahindra Bank"),
            new("LT", "Larsen & Toubro"),
            new("BHARTIARTL", "Bharti Airtel"),
            new("TATAMOTORS", "Tata Motors"),
            new("MARUTI", "Maruti Suzuki"),
            new("BAJFINANCE", "Bajaj Finance"),
            new("HINDUNILVR", "Hindustan Unilever"),
            new("ITC", "ITC"),
            new("SUNPHARMA", "Sun Pharma"),
            new("ADANIENT", "Adani Enterprises"),
            new("TATASTEEL", "Tata Steel"),
            new("JSWSTEEL", "JSW Steel"),
            new("POWERGRID", "Power Grid")
        };

        private static readonly StockUniverseItem[] LongTermUniverse =
        {
            new("RELIANCE", "Reliance Industries"),
            new("HDFCBANK", "HDFC Bank"),
            new("ICICIBANK", "ICICI Bank"),
            new("INFY", "Infosys"),
            new("TCS", "Tata Consultancy Services"),
            new("LT", "Larsen & Toubro"),
            new("BHARTIARTL", "Bharti Airtel"),
            new("HINDUNILVR", "Hindustan Unilever"),
            new("ITC", "ITC"),
            new("SUNPHARMA", "Sun Pharma"),
            new("ASIANPAINT", "Asian Paints"),
            new("TITAN", "Titan"),
            new("NESTLEIND", "Nestle India"),
            new("ULTRACEMCO", "UltraTech Cement"),
            new("BAJFINANCE", "Bajaj Finance"),
            new("DMART", "Avenue Supermarts"),
            new("PIDILITIND", "Pidilite Industries"),
            new("DIVISLAB", "Divi's Laboratories"),
            new("HCLTECH", "HCL Technologies"),
            new("WIPRO", "Wipro")
        };

        public StockPickService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(12);
        }

        public async Task<StockPickDashboardDto> GetIntradayPicksAsync(CancellationToken cancellationToken = default)
        {
            var dashboard = CreateDashboard(
                "NSE liquid large-cap/F&O watchlist",
                "Yahoo Finance public daily candles",
                "Ranks stocks by 5-day momentum, 20-day volume expansion, price location vs SMA20, and tradable volatility.");

            var candles = await FetchUniverseAsync(IntradayUniverse, "3mo", cancellationToken);
            dashboard.Picks = candles
                .Select(BuildIntradayPick)
                .Where(p => p != null)
                .Cast<StockPickDto>()
                .OrderByDescending(p => p.Score)
                .Take(10)
                .Select((p, index) =>
                {
                    p.Rank = index + 1;
                    return p;
                })
                .ToList();

            if (dashboard.Picks.Count == 0)
            {
                dashboard.Warnings.Add("No intraday picks could be generated. Public candle source may be unavailable or throttled.");
            }

            dashboard.Warnings.Add("Intraday picks are scanner watchlist ideas, not automatic trade calls. Confirm live market structure, VWAP, and risk before entry.");
            return dashboard;
        }

        public async Task<StockPickDashboardDto> GetLongTermPicksAsync(CancellationToken cancellationToken = default)
        {
            var dashboard = CreateDashboard(
                "NSE quality large/mid-cap watchlist",
                "Yahoo Finance public daily candles",
                "Ranks stocks by 1-year trend, 6-month relative strength, drawdown control, and volatility stability. Fundamentals can be added later.");

            var candles = await FetchUniverseAsync(LongTermUniverse, "2y", cancellationToken);
            dashboard.Picks = candles
                .Select(BuildLongTermPick)
                .Where(p => p != null)
                .Cast<StockPickDto>()
                .OrderByDescending(p => p.Score)
                .Take(5)
                .Select((p, index) =>
                {
                    p.Rank = index + 1;
                    return p;
                })
                .ToList();

            if (dashboard.Picks.Count == 0)
            {
                dashboard.Warnings.Add("No long-term picks could be generated. Public candle source may be unavailable or throttled.");
            }

            dashboard.Warnings.Add("Long-term picks currently use price quality and stability only. Add fundamentals API/feed before using as investment advice.");
            return dashboard;
        }

        public async Task<StockAnalysisDto> AnalyzeStockAsync(StockAnalysisRequestDto request, CancellationToken cancellationToken = default)
        {
            var symbol = NormalizeSymbol(request.Symbol);
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("Stock symbol is required.");
            }

            var horizon = IsLongTerm(request.Horizon) ? "Long Term" : "Short Term";
            var range = horizon == "Long Term" ? "2y" : "6mo";
            var series = await FetchCandlesAsync(new StockUniverseItem(symbol, symbol), range, cancellationToken);
            if (series == null || series.Candles.Count < (horizon == "Long Term" ? 180 : 60))
            {
                return new StockAnalysisDto
                {
                    LastUpdatedAt = DateTime.UtcNow,
                    Symbol = symbol,
                    Name = symbol,
                    Horizon = horizon,
                    Verdict = "No Data",
                    Recommendation = "Could not generate analysis because candle data is unavailable or insufficient.",
                    Risk = "Unknown",
                    Warnings = { "Free market-data source did not return enough NSE candles. Try NSE symbol without .NS, for example RELIANCE, HDFCBANK, TCS." }
                };
            }

            var analysis = horizon == "Long Term"
                ? BuildLongTermAnalysis(series)
                : BuildShortTermAnalysis(series);

            var fundamentals = await FetchFundamentalsAsync(symbol, cancellationToken);
            ApplyFundamentalSignals(analysis, fundamentals, horizon);
            analysis.OverallScore = Math.Round(analysis.FundamentalScore > 0
                ? analysis.TechnicalScore * 0.62m + analysis.FundamentalScore * 0.38m
                : analysis.TechnicalScore, 2);
            analysis.Verdict = BuildVerdict(analysis.OverallScore, horizon);
            analysis.Recommendation = BuildRecommendation(analysis, horizon);
            return analysis;
        }

        private static StockAnalysisDto BuildShortTermAnalysis(StockCandleSeries series)
        {
            var candles = series.Candles;
            var last = candles[^1];
            var prev = candles[^2];
            var sma20 = Average(candles.TakeLast(20).Select(c => c.Close));
            var sma50 = Average(candles.TakeLast(50).Select(c => c.Close));
            var atr14 = AverageTrueRange(candles, 14);
            var recentHigh = candles.TakeLast(20).Max(c => c.High);
            var recentLow = candles.TakeLast(20).Min(c => c.Low);
            var change1D = Percent(last.Close, prev.Close);
            var change5D = Percent(last.Close, candles[^6].Close);
            var change20D = Percent(last.Close, candles[^21].Close);
            var volumeRatio = Average(candles.TakeLast(20).SkipLast(1).Select(c => c.Volume)) > 0
                ? last.Volume / Average(candles.TakeLast(20).SkipLast(1).Select(c => c.Volume))
                : 0;
            var trendStrength = Percent(last.Close, sma20);
            var volatility = last.Close > 0 ? atr14 / last.Close * 100 : 0;

            var score = 42m
                + Math.Clamp(change5D * 2m, -16m, 22m)
                + Math.Clamp(trendStrength * 1.5m, -14m, 18m)
                + Math.Clamp((volumeRatio - 1m) * 14m, -8m, 16m)
                + (last.Close > sma20 ? 8m : -6m)
                + (sma20 > sma50 ? 8m : -5m)
                - Math.Clamp(volatility * 0.8m, 0m, 8m);

            var entryLow = Math.Max(sma20, last.Close - atr14 * 0.45m);
            var entryHigh = last.Close + atr14 * 0.25m;
            var stop = Math.Min(recentLow, last.Close - atr14 * 1.15m);
            var target1 = last.Close + atr14 * 1.2m;
            var target2 = Math.Max(recentHigh, last.Close + atr14 * 2m);

            var dto = CreateAnalysis(series, "Short Term", score, last.Close, change1D, change5D, change20D, volatility);
            dto.EntryZone = $"Rs. {entryLow:0.##} - Rs. {entryHigh:0.##}; prefer breakout above Rs. {recentHigh:0.##} only if volume supports.";
            dto.StopLoss = $"Below Rs. {stop:0.##}";
            dto.Target1 = $"Rs. {target1:0.##}";
            dto.Target2 = $"Rs. {target2:0.##}";
            dto.Risk = volatility >= 3 ? "High" : volatility >= 1.8m ? "Medium" : "Low/Medium";
            dto.TechnicalSignals.Add($"Price vs SMA20: {trendStrength:0.##}% ({(last.Close > sma20 ? "bullish" : "weak")}).");
            dto.TechnicalSignals.Add($"SMA20 {(sma20 > sma50 ? "above" : "below")} SMA50, trend structure {(sma20 > sma50 ? "positive" : "not confirmed")}.");
            dto.TechnicalSignals.Add($"5D momentum {change5D:0.##}%, 20D momentum {change20D:0.##}%.");
            dto.TechnicalSignals.Add($"ATR volatility {volatility:0.##}% and volume ratio {volumeRatio:0.##}x.");
            return dto;
        }

        private static StockAnalysisDto BuildLongTermAnalysis(StockCandleSeries series)
        {
            var candles = series.Candles;
            var last = candles[^1];
            var sma50 = Average(candles.TakeLast(50).Select(c => c.Close));
            var sma200 = Average(candles.TakeLast(Math.Min(200, candles.Count)).Select(c => c.Close));
            var high52 = candles.TakeLast(Math.Min(252, candles.Count)).Max(c => c.High);
            var low52 = candles.TakeLast(Math.Min(252, candles.Count)).Min(c => c.Low);
            var atr20 = AverageTrueRange(candles, 20);
            var change1D = Percent(last.Close, candles[^2].Close);
            var change5D = Percent(last.Close, candles[^6].Close);
            var change20D = Percent(last.Close, candles[^21].Close);
            var change6M = Percent(last.Close, candles[^Math.Min(126, candles.Count)].Close);
            var change1Y = Percent(last.Close, candles[^Math.Min(252, candles.Count)].Close);
            var drawdown = high52 > 0 ? (high52 - last.Close) / high52 * 100 : 0;
            var trendStrength = Percent(last.Close, sma200);
            var volatility = last.Close > 0 ? atr20 / last.Close * 100 : 0;

            var score = 44m
                + Math.Clamp(change1Y * 0.42m, -18m, 24m)
                + Math.Clamp(change6M * 0.5m, -12m, 20m)
                + (last.Close > sma50 && sma50 > sma200 ? 18m : 0m)
                - Math.Clamp(drawdown * 0.35m, 0m, 14m)
                - Math.Clamp(volatility * 1.4m, 0m, 10m);

            var dto = CreateAnalysis(series, "Long Term", score, last.Close, change1D, change5D, change20D, volatility);
            dto.EntryZone = $"Accumulate in 3-4 parts near Rs. {last.Close:0.##}; better add zone Rs. {Math.Min(last.Close, sma50):0.##} - Rs. {Math.Max(last.Close, sma50):0.##}.";
            dto.StopLoss = $"Long-term thesis review below SMA200 Rs. {sma200:0.##} or 52W low Rs. {low52:0.##}.";
            dto.Target1 = $"52W high retest Rs. {high52:0.##}";
            dto.Target2 = $"Trail if quarterly earnings and sector trend stay strong; avoid fixed blind target.";
            dto.Risk = volatility >= 3 ? "High" : drawdown <= 12 ? "Low/Medium" : "Medium";
            dto.TechnicalSignals.Add($"1Y trend {change1Y:0.##}%, 6M trend {change6M:0.##}%.");
            dto.TechnicalSignals.Add(last.Close > sma50 && sma50 > sma200 ? "Price is above SMA50 and SMA50 is above SMA200." : "Moving-average trend is not fully confirmed yet.");
            dto.TechnicalSignals.Add($"Drawdown from 52W high {drawdown:0.##}%; trend vs SMA200 {trendStrength:0.##}%.");
            dto.TechnicalSignals.Add($"ATR volatility {volatility:0.##}%.");
            return dto;
        }

        private async Task<StockFundamentals> FetchFundamentalsAsync(string symbol, CancellationToken cancellationToken)
        {
            var fundamentals = new StockFundamentals();
            var yahooSymbol = $"{symbol}.NS";

            await TryFetchQuoteSummaryFundamentalsAsync(yahooSymbol, fundamentals, cancellationToken);
            await TryFetchQuoteFundamentalsAsync(yahooSymbol, fundamentals, cancellationToken);
            await TryFetchScreenerFundamentalsAsync(symbol, fundamentals, cancellationToken);

            fundamentals.HasAnyData = fundamentals.MarketCap > 0
                || fundamentals.TrailingPe > 0
                || fundamentals.ForwardPe > 0
                || fundamentals.PriceToBook > 0
                || fundamentals.RoePercent != 0
                || fundamentals.RocePercent != 0
                || fundamentals.RevenueGrowthPercent != 0
                || fundamentals.ProfitMarginsPercent != 0
                || fundamentals.TrailingEps != 0;

            return fundamentals;
        }

        private async Task TryFetchQuoteSummaryFundamentalsAsync(string yahooSymbol, StockFundamentals fundamentals, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"https://query1.finance.yahoo.com/v10/finance/quoteSummary/{yahooSymbol}?modules=financialData,defaultKeyStatistics,summaryDetail,price";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 TradeSphere/1.0");
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var root = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
                var result = root?["quoteSummary"]?["result"]?[0];
                fundamentals.LongName = FirstText(fundamentals.LongName, ReadString(result?["price"]?["longName"]));
                fundamentals.MarketCap = FirstPositive(fundamentals.MarketCap, ReadRawDecimal(result?["price"]?["marketCap"]));
                fundamentals.TrailingPe = FirstPositive(fundamentals.TrailingPe, ReadRawDecimal(result?["summaryDetail"]?["trailingPE"]));
                fundamentals.ForwardPe = FirstPositive(fundamentals.ForwardPe, ReadRawDecimal(result?["summaryDetail"]?["forwardPE"]));
                fundamentals.PriceToBook = FirstPositive(fundamentals.PriceToBook, ReadRawDecimal(result?["defaultKeyStatistics"]?["priceToBook"]));
                fundamentals.TrailingEps = FirstNonZero(fundamentals.TrailingEps, ReadRawDecimal(result?["defaultKeyStatistics"]?["trailingEps"]));
                fundamentals.ForwardEps = FirstNonZero(fundamentals.ForwardEps, ReadRawDecimal(result?["defaultKeyStatistics"]?["forwardEps"]));
                fundamentals.DividendYieldPercent = FirstNonZero(fundamentals.DividendYieldPercent, ReadRawDecimal(result?["summaryDetail"]?["dividendYield"]) * 100);
                fundamentals.RoePercent = FirstNonZero(fundamentals.RoePercent, ReadRawDecimal(result?["financialData"]?["returnOnEquity"]) * 100);
                fundamentals.DebtToEquity = FirstPositive(fundamentals.DebtToEquity, ReadRawDecimal(result?["financialData"]?["debtToEquity"]));
                fundamentals.RevenueGrowthPercent = FirstNonZero(fundamentals.RevenueGrowthPercent, ReadRawDecimal(result?["financialData"]?["revenueGrowth"]) * 100);
                fundamentals.ProfitMarginsPercent = FirstNonZero(fundamentals.ProfitMarginsPercent, ReadRawDecimal(result?["financialData"]?["profitMargins"]) * 100);
                fundamentals.Source = FirstText(fundamentals.Source, "Yahoo quoteSummary");
            }
            catch
            {
                // Public fundamentals are best-effort; candle-based analysis should still work.
            }
        }

        private async Task TryFetchQuoteFundamentalsAsync(string yahooSymbol, StockFundamentals fundamentals, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={yahooSymbol}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 TradeSphere/1.0");
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var root = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
                var result = root?["quoteResponse"]?["result"]?[0];
                fundamentals.LongName = FirstText(fundamentals.LongName, ReadString(result?["longName"]), ReadString(result?["shortName"]));
                fundamentals.MarketCap = FirstPositive(fundamentals.MarketCap, ReadDecimal(result?["marketCap"]));
                fundamentals.TrailingPe = FirstPositive(fundamentals.TrailingPe, ReadDecimal(result?["trailingPE"]));
                fundamentals.ForwardPe = FirstPositive(fundamentals.ForwardPe, ReadDecimal(result?["forwardPE"]));
                fundamentals.PriceToBook = FirstPositive(fundamentals.PriceToBook, ReadDecimal(result?["priceToBook"]));
                fundamentals.TrailingEps = FirstNonZero(fundamentals.TrailingEps, ReadDecimal(result?["epsTrailingTwelveMonths"]));
                fundamentals.ForwardEps = FirstNonZero(fundamentals.ForwardEps, ReadDecimal(result?["epsForward"]));
                fundamentals.BookValue = FirstPositive(fundamentals.BookValue, ReadDecimal(result?["bookValue"]));
                fundamentals.DividendYieldPercent = FirstNonZero(fundamentals.DividendYieldPercent, ReadDecimal(result?["trailingAnnualDividendYield"]) * 100);
                fundamentals.FiftyTwoWeekLow = FirstPositive(fundamentals.FiftyTwoWeekLow, ReadDecimal(result?["fiftyTwoWeekLow"]));
                fundamentals.FiftyTwoWeekHigh = FirstPositive(fundamentals.FiftyTwoWeekHigh, ReadDecimal(result?["fiftyTwoWeekHigh"]));
                fundamentals.Source = string.IsNullOrWhiteSpace(fundamentals.Source) ? "Yahoo quote" : $"{fundamentals.Source} + quote fallback";
            }
            catch
            {
                // Public fundamentals are best-effort; candle-based analysis should still work.
            }
        }
        private async Task TryFetchScreenerFundamentalsAsync(string symbol, StockFundamentals fundamentals, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"https://www.screener.in/company/{symbol}/consolidated/";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 TradeSphere/1.0");
                request.Headers.Referrer = new Uri("https://www.screener.in/");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var text = NormalizeHtmlText(html);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                fundamentals.LongName = FirstText(fundamentals.LongName, ReadScreenerCompanyName(text));
                var marketCapCrore = ReadScreenerMetric(text, "Market Cap");
                if (marketCapCrore > 0)
                {
                    fundamentals.MarketCap = FirstPositive(fundamentals.MarketCap, marketCapCrore * 10000000m);
                }

                fundamentals.TrailingPe = FirstPositive(fundamentals.TrailingPe, ReadScreenerMetric(text, "Stock P/E", "P/E"));
                fundamentals.PriceToBook = FirstPositive(fundamentals.PriceToBook, ReadScreenerMetric(text, "Price to book value", "Price to Book", "P/B"));
                fundamentals.BookValue = FirstPositive(fundamentals.BookValue, ReadScreenerMetric(text, "Book Value"));
                fundamentals.DividendYieldPercent = FirstNonZero(fundamentals.DividendYieldPercent, ReadScreenerMetric(text, "Dividend Yield"));
                fundamentals.RocePercent = FirstNonZero(fundamentals.RocePercent, ReadScreenerMetric(text, "ROCE"));
                fundamentals.RoePercent = FirstNonZero(fundamentals.RoePercent, ReadScreenerMetric(text, "ROE"));
                fundamentals.DebtToEquity = FirstPositive(fundamentals.DebtToEquity, ReadScreenerMetric(text, "Debt to equity", "Debt / Equity"));
                fundamentals.RevenueGrowthPercent = FirstNonZero(fundamentals.RevenueGrowthPercent, ReadScreenerMetric(text, "Sales growth"));
                fundamentals.ProfitMarginsPercent = FirstNonZero(fundamentals.ProfitMarginsPercent, ReadScreenerMetric(text, "OPM", "Operating Profit Margin"));
                fundamentals.Source = string.IsNullOrWhiteSpace(fundamentals.Source) ? "Screener public page" : $"{fundamentals.Source} + Screener fallback";
            }
            catch
            {
                // Screener is a public webpage fallback. If it blocks/throttles, keep the rest of the analyzer alive.
            }
        }
        private static void ApplyFundamentalSignals(StockAnalysisDto analysis, StockFundamentals fundamentals, string horizon)
        {
            if (!string.IsNullOrWhiteSpace(fundamentals.LongName))
            {
                analysis.Name = fundamentals.LongName;
            }

            if (!fundamentals.HasAnyData)
            {
                analysis.FundamentalScore = horizon == "Long Term" ? 42 : 50;
                analysis.FundamentalSignals.Add("Live fundamental ratios were not returned by Yahoo/Screener public feeds. Use the Deep Fundamental Checks links for manual verification.");
                analysis.FundamentalSignals.Add($"Verify manually: Screener https://www.screener.in/company/{analysis.Symbol}/consolidated/ and NSE https://www.nseindia.com/get-quotes/equity?symbol={analysis.Symbol}.");
                analysis.Warnings.Add("For long-term investing, verify quarterly results, debt, promoter holding, cash flow, and valuation on Screener/NSE before buying.");
                return;
            }

            var score = 42m;
            if (fundamentals.RoePercent >= 20) score += 16; else if (fundamentals.RoePercent >= 15) score += 12; else if (fundamentals.RoePercent > 0) score += 5;
            if (fundamentals.RocePercent >= 20) score += 14; else if (fundamentals.RocePercent >= 15) score += 10; else if (fundamentals.RocePercent > 0) score += 4;
            if (fundamentals.RevenueGrowthPercent >= 15) score += 14; else if (fundamentals.RevenueGrowthPercent >= 8) score += 8; else if (fundamentals.RevenueGrowthPercent < 0) score -= 8;
            if (fundamentals.ProfitMarginsPercent >= 15) score += 11; else if (fundamentals.ProfitMarginsPercent >= 8) score += 7; else if (fundamentals.ProfitMarginsPercent < 3 && fundamentals.ProfitMarginsPercent != 0) score -= 5;
            if (fundamentals.DebtToEquity > 0 && fundamentals.DebtToEquity <= 50) score += 10; else if (fundamentals.DebtToEquity > 0 && fundamentals.DebtToEquity <= 100) score += 5; else if (fundamentals.DebtToEquity > 150) score -= 10;
            if (fundamentals.TrailingPe > 0 && fundamentals.TrailingPe <= 35) score += 8; else if (fundamentals.TrailingPe > 35 && fundamentals.TrailingPe <= 55) score += 3; else if (fundamentals.TrailingPe > 80) score -= 8;
            if (fundamentals.PriceToBook > 0 && fundamentals.PriceToBook <= 5) score += 5; else if (fundamentals.PriceToBook > 12) score -= 5;
            if (fundamentals.TrailingEps > 0 && fundamentals.ForwardEps > fundamentals.TrailingEps) score += 5;
            if (fundamentals.DividendYieldPercent > 0) score += Math.Min(fundamentals.DividendYieldPercent, 3);

            analysis.FundamentalScore = Math.Round(Math.Clamp(score, 0, 100), 2);
            if (!string.IsNullOrWhiteSpace(fundamentals.Source)) analysis.FundamentalSignals.Add($"Fundamental data source: {fundamentals.Source} public feed.");
            if (fundamentals.MarketCap > 0) analysis.FundamentalSignals.Add($"Market cap approx Rs. {fundamentals.MarketCap / 10000000m:0.##} Cr.");
            if (fundamentals.TrailingPe > 0 || fundamentals.ForwardPe > 0) analysis.FundamentalSignals.Add($"Valuation: trailing PE {FormatMetric(fundamentals.TrailingPe)}, forward PE {FormatMetric(fundamentals.ForwardPe)}, P/B {FormatMetric(fundamentals.PriceToBook)}.");
            if (fundamentals.TrailingEps != 0 || fundamentals.ForwardEps != 0) analysis.FundamentalSignals.Add($"Earnings: EPS TTM {FormatMetric(fundamentals.TrailingEps)}, forward EPS {FormatMetric(fundamentals.ForwardEps)}.");
            if (fundamentals.RoePercent != 0) analysis.FundamentalSignals.Add($"Profitability: ROE {fundamentals.RoePercent:0.##}%.");
            if (fundamentals.RocePercent != 0) analysis.FundamentalSignals.Add($"Efficiency: ROCE {fundamentals.RocePercent:0.##}%.");
            if (fundamentals.RevenueGrowthPercent != 0) analysis.FundamentalSignals.Add($"Growth: revenue growth {fundamentals.RevenueGrowthPercent:0.##}%.");
            if (fundamentals.ProfitMarginsPercent != 0) analysis.FundamentalSignals.Add($"Margins: profit margin {fundamentals.ProfitMarginsPercent:0.##}%.");
            if (fundamentals.DebtToEquity > 0) analysis.FundamentalSignals.Add($"Balance sheet: debt/equity {fundamentals.DebtToEquity:0.##}.");
            if (fundamentals.DividendYieldPercent > 0) analysis.FundamentalSignals.Add($"Dividend yield approx {fundamentals.DividendYieldPercent:0.##}%.");
            if (fundamentals.FiftyTwoWeekLow > 0 && fundamentals.FiftyTwoWeekHigh > 0) analysis.FundamentalSignals.Add($"52W range Rs. {fundamentals.FiftyTwoWeekLow:0.##} - Rs. {fundamentals.FiftyTwoWeekHigh:0.##}.");
            analysis.FundamentalSignals.Add($"Manual verification links: Screener https://www.screener.in/company/{analysis.Symbol}/consolidated/ | NSE https://www.nseindia.com/get-quotes/equity?symbol={analysis.Symbol}");
        }
        private static StockAnalysisDto CreateAnalysis(StockCandleSeries series, string horizon, decimal technicalScore, decimal lastPrice, decimal change1D, decimal change5D, decimal change20D, decimal volatility)
        {
            return new StockAnalysisDto
            {
                LastUpdatedAt = DateTime.UtcNow,
                Symbol = series.Symbol,
                Name = series.Name,
                Horizon = horizon,
                LastPrice = Math.Round(lastPrice, 2),
                TechnicalScore = Math.Round(Math.Clamp(technicalScore, 0, 100), 2),
                Change1DPercent = Math.Round(change1D, 2),
                Change5DPercent = Math.Round(change5D, 2),
                Change20DPercent = Math.Round(change20D, 2),
                VolatilityPercent = Math.Round(volatility, 2)
            };
        }

        private static string BuildVerdict(decimal score, string horizon)
        {
            if (score >= 72) return horizon == "Long Term" ? "Buy / Accumulate" : "Buy Watch";
            if (score >= 58) return "Wait for Pullback / Confirmation";
            if (score >= 45) return "Neutral";
            return "Avoid for now";
        }

        private static string BuildRecommendation(StockAnalysisDto analysis, string horizon)
        {
            if (analysis.OverallScore >= 72)
            {
                return horizon == "Long Term"
                    ? "Can be considered for phased accumulation if fundamentals are verified and market trend remains supportive."
                    : "Can be considered only near the entry zone with strict stop-loss; avoid chasing gaps."
                    ;
            }

            if (analysis.OverallScore >= 58)
            {
                return "Setup is decent, but wait for price confirmation near the entry zone or better risk-reward.";
            }

            if (analysis.OverallScore >= 45)
            {
                return "No clear edge right now. Keep on watchlist, but do not force entry.";
            }

            return "Avoid fresh buy for now; technical/fundamental score is weak or data quality is insufficient.";
        }

        private static string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).Trim().ToUpperInvariant().Replace(".NS", string.Empty).Replace("NSE:", string.Empty);
        }

        private static bool IsLongTerm(string horizon)
        {
            return (horizon ?? string.Empty).Contains("long", StringComparison.OrdinalIgnoreCase);
        }


        private static string NormalizeHtmlText(string html)
        {
            var withoutScripts = Regex.Replace(html, "<script[\\s\\S]*?</script>|<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
            var text = Regex.Replace(withoutScripts, "<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);
            return Regex.Replace(text, "\\s+", " ").Trim();
        }

        private static string ReadScreenerCompanyName(string text)
        {
            var match = Regex.Match(text, @"^\s*([A-Za-z0-9&.,'() \-]+?)\s+share price", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static decimal ReadScreenerMetric(string text, params string[] labels)
        {
            foreach (var label in labels)
            {
                var pattern = $"{Regex.Escape(label)}\\s*(?:₹|Rs\\.?|:)?\\s*([-+]?\\d[\\d,]*(?:\\.\\d+)?)";
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", string.Empty), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                {
                    return value;
                }
            }

            return 0;
        }
        private static string ReadString(JsonNode? node)
        {
            return node?.ToString() ?? string.Empty;
        }

        private static decimal ReadRawDecimal(JsonNode? node)
        {
            var raw = node?["raw"] ?? node;
            return ReadDecimal(raw);
        }
        private static decimal FirstPositive(params decimal[] values)
        {
            return values.FirstOrDefault(value => value > 0);
        }

        private static decimal FirstNonZero(params decimal[] values)
        {
            return values.FirstOrDefault(value => value != 0);
        }

        private static string FirstText(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static string FormatMetric(decimal value)
        {
            return value == 0 ? "-" : value.ToString("0.##", CultureInfo.InvariantCulture);
        }
        private async Task<List<StockCandleSeries>> FetchUniverseAsync(IEnumerable<StockUniverseItem> universe, string range, CancellationToken cancellationToken)
        {
            var tasks = universe.Select(item => FetchCandlesAsync(item, range, cancellationToken));
            var results = await Task.WhenAll(tasks);
            return results.Where(r => r != null && r.Candles.Count >= 30).Cast<StockCandleSeries>().ToList();
        }

        private async Task<StockCandleSeries?> FetchCandlesAsync(StockUniverseItem item, string range, CancellationToken cancellationToken)
        {
            try
            {
                var yahooSymbol = $"{item.Symbol}.NS";
                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{yahooSymbol}?range={range}&interval=1d";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 TradeSphere/1.0");
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var root = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
                var result = root?["chart"]?["result"]?[0];
                var timestamps = result?["timestamp"]?.AsArray();
                var quote = result?["indicators"]?["quote"]?[0];
                if (timestamps == null || quote == null)
                {
                    return null;
                }

                var opens = quote["open"]?.AsArray();
                var highs = quote["high"]?.AsArray();
                var lows = quote["low"]?.AsArray();
                var closes = quote["close"]?.AsArray();
                var volumes = quote["volume"]?.AsArray();
                if (opens == null || highs == null || lows == null || closes == null || volumes == null)
                {
                    return null;
                }

                var candles = new List<StockCandle>();
                for (var i = 0; i < timestamps.Count; i++)
                {
                    var close = ReadDecimal(closes[i]);
                    var high = ReadDecimal(highs[i]);
                    var low = ReadDecimal(lows[i]);
                    var open = ReadDecimal(opens[i]);
                    var volume = ReadDecimal(volumes[i]);
                    if (close <= 0 || high <= 0 || low <= 0)
                    {
                        continue;
                    }

                    candles.Add(new StockCandle(open, high, low, close, volume));
                }

                return candles.Count == 0 ? null : new StockCandleSeries(item.Symbol, item.Name, candles);
            }
            catch
            {
                return null;
            }
        }

        private static StockPickDto? BuildIntradayPick(StockCandleSeries series)
        {
            var candles = series.Candles;
            if (candles.Count < 30)
            {
                return null;
            }

            var last = candles[^1];
            var prev = candles[^2];
            var sma20 = Average(candles.TakeLast(20).Select(c => c.Close));
            var avgVolume20 = Average(candles.TakeLast(20).SkipLast(1).Select(c => c.Volume));
            var atr14 = AverageTrueRange(candles, 14);
            var change1D = Percent(last.Close, prev.Close);
            var change5D = Percent(last.Close, candles[^6].Close);
            var change20D = Percent(last.Close, candles[^21].Close);
            var volumeRatio = avgVolume20 > 0 ? last.Volume / avgVolume20 : 0;
            var trendStrength = Percent(last.Close, sma20);
            var volatility = last.Close > 0 ? atr14 / last.Close * 100 : 0;

            var score = 45m
                + Math.Clamp(change5D * 2.2m, -18m, 24m)
                + Math.Clamp(trendStrength * 1.4m, -14m, 18m)
                + Math.Clamp((volumeRatio - 1m) * 18m, -8m, 22m)
                + Math.Clamp(volatility * 2.4m, 0m, 16m);

            var bias = last.Close >= sma20 ? "Buy Watch" : "Reversal Watch";
            var stop = Math.Max(last.Close - atr14, last.Close * 0.985m);
            var target1 = last.Close + atr14;
            var target2 = last.Close + atr14 * 1.8m;

            var pick = CreatePick(series, bias, "Intraday", score, last.Close, change1D, change5D, change20D, volumeRatio, volatility, trendStrength);
            pick.EntryZone = last.Close >= sma20
                ? $"Above Rs. {last.Close:0.##} or pullback near SMA20 Rs. {sma20:0.##}"
                : $"Only above reclaim level Rs. {sma20:0.##}";
            pick.StopLoss = $"Below Rs. {stop:0.##}";
            pick.Target1 = $"Rs. {target1:0.##}";
            pick.Target2 = $"Rs. {target2:0.##}";
            pick.Risk = volatility >= 3 ? "High" : volatility >= 1.8m ? "Medium" : "Low";
            pick.Reasons.Add($"5D momentum {change5D:0.##}% and 20D trend {trendStrength:0.##}%.");
            pick.Reasons.Add($"Volume ratio {volumeRatio:0.##}x vs 20-day average.");
            pick.Reasons.Add($"ATR volatility {volatility:0.##}% gives intraday movement potential.");
            return pick;
        }

        private static StockPickDto? BuildLongTermPick(StockCandleSeries series)
        {
            var candles = series.Candles;
            if (candles.Count < 180)
            {
                return null;
            }

            var last = candles[^1];
            var sma50 = Average(candles.TakeLast(50).Select(c => c.Close));
            var sma200 = Average(candles.TakeLast(Math.Min(200, candles.Count)).Select(c => c.Close));
            var high52 = candles.TakeLast(Math.Min(252, candles.Count)).Max(c => c.High);
            var low52 = candles.TakeLast(Math.Min(252, candles.Count)).Min(c => c.Low);
            var atr20 = AverageTrueRange(candles, 20);
            var change1D = Percent(last.Close, candles[^2].Close);
            var change5D = Percent(last.Close, candles[^6].Close);
            var change20D = Percent(last.Close, candles[^21].Close);
            var change6M = Percent(last.Close, candles[^Math.Min(126, candles.Count)].Close);
            var change1Y = Percent(last.Close, candles[^Math.Min(252, candles.Count)].Close);
            var drawdown = high52 > 0 ? (high52 - last.Close) / high52 * 100 : 0;
            var trendStrength = Percent(last.Close, sma200);
            var volatility = last.Close > 0 ? atr20 / last.Close * 100 : 0;

            var score = 45m
                + Math.Clamp(change1Y * 0.45m, -18m, 24m)
                + Math.Clamp(change6M * 0.55m, -12m, 20m)
                + (last.Close > sma50 && sma50 > sma200 ? 16m : 0m)
                - Math.Clamp(drawdown * 0.45m, 0m, 18m)
                - Math.Clamp(volatility * 2m, 0m, 12m);

            var pick = CreatePick(series, "Long Term Watch", "1-3 Years", score, last.Close, change1D, change5D, change20D, 0, volatility, trendStrength);
            pick.EntryZone = $"Accumulate in phases near Rs. {last.Close:0.##}; better on pullback near Rs. {sma50:0.##}";
            pick.StopLoss = $"Review thesis below Rs. {low52:0.##} or if trend breaks SMA200 Rs. {sma200:0.##}";
            pick.Target1 = $"52W high retest Rs. {high52:0.##}";
            pick.Target2 = "Trail with quarterly results and sector trend";
            pick.Risk = volatility >= 3 ? "High" : drawdown <= 12 ? "Low/Medium" : "Medium";
            pick.Reasons.Add($"1Y trend {change1Y:0.##}% and 6M trend {change6M:0.##}%.");
            pick.Reasons.Add(last.Close > sma50 && sma50 > sma200
                ? "Price is above SMA50 and SMA50 is above SMA200."
                : "Trend is improving but needs confirmation above key moving averages.");
            pick.Reasons.Add($"Drawdown from 52W high is {drawdown:0.##}%; ATR volatility is {volatility:0.##}%.");
            return pick;
        }

        private static StockPickDashboardDto CreateDashboard(string universe, string source, string methodology)
        {
            return new StockPickDashboardDto
            {
                LastUpdatedAt = DateTime.UtcNow,
                Universe = universe,
                Source = source,
                Methodology = methodology
            };
        }

        private static StockPickDto CreatePick(
            StockCandleSeries series,
            string bias,
            string horizon,
            decimal score,
            decimal lastPrice,
            decimal change1D,
            decimal change5D,
            decimal change20D,
            decimal volumeRatio,
            decimal volatility,
            decimal trendStrength)
        {
            return new StockPickDto
            {
                Symbol = series.Symbol,
                Name = series.Name,
                Bias = bias,
                Horizon = horizon,
                LastPrice = Math.Round(lastPrice, 2),
                Score = Math.Round(Math.Clamp(score, 0, 100), 2),
                Change1DPercent = Math.Round(change1D, 2),
                Change5DPercent = Math.Round(change5D, 2),
                Change20DPercent = Math.Round(change20D, 2),
                VolumeRatio = Math.Round(volumeRatio, 2),
                VolatilityPercent = Math.Round(volatility, 2),
                TrendStrengthPercent = Math.Round(trendStrength, 2)
            };
        }

        private static decimal Average(IEnumerable<decimal> values)
        {
            var list = values.Where(v => v > 0).ToList();
            return list.Count == 0 ? 0 : list.Average();
        }

        private static decimal AverageTrueRange(List<StockCandle> candles, int period)
        {
            var recent = candles.TakeLast(period + 1).ToList();
            if (recent.Count < 2)
            {
                return 0;
            }

            var ranges = new List<decimal>();
            for (var i = 1; i < recent.Count; i++)
            {
                var highLow = recent[i].High - recent[i].Low;
                var highPrevClose = Math.Abs(recent[i].High - recent[i - 1].Close);
                var lowPrevClose = Math.Abs(recent[i].Low - recent[i - 1].Close);
                ranges.Add(Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose)));
            }

            return ranges.Count == 0 ? 0 : ranges.Average();
        }

        private static decimal Percent(decimal current, decimal previous)
        {
            return previous == 0 ? 0 : (current - previous) / previous * 100;
        }

        private static decimal ReadDecimal(JsonNode? node)
        {
            if (node == null)
            {
                return 0;
            }

            return decimal.TryParse(node.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private sealed class StockFundamentals
        {
            public bool HasAnyData { get; set; }
            public string Source { get; set; } = string.Empty;
            public string LongName { get; set; } = string.Empty;
            public decimal MarketCap { get; set; }
            public decimal TrailingPe { get; set; }
            public decimal ForwardPe { get; set; }
            public decimal PriceToBook { get; set; }
            public decimal TrailingEps { get; set; }
            public decimal ForwardEps { get; set; }
            public decimal BookValue { get; set; }
            public decimal DividendYieldPercent { get; set; }
            public decimal FiftyTwoWeekLow { get; set; }
            public decimal FiftyTwoWeekHigh { get; set; }
            public decimal RoePercent { get; set; }
            public decimal RocePercent { get; set; }
            public decimal DebtToEquity { get; set; }
            public decimal RevenueGrowthPercent { get; set; }
            public decimal ProfitMarginsPercent { get; set; }
        }
        private sealed record StockUniverseItem(string Symbol, string Name);
        private sealed record StockCandle(decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);
        private sealed record StockCandleSeries(string Symbol, string Name, List<StockCandle> Candles);
    }
}












