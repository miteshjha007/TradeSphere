using System.Globalization;
using System.Net.Http.Json;
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

        private sealed record StockUniverseItem(string Symbol, string Name);
        private sealed record StockCandle(decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);
        private sealed record StockCandleSeries(string Symbol, string Name, List<StockCandle> Candles);
    }
}
