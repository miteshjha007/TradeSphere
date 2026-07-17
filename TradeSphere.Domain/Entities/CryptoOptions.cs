using System;
using System.Collections.Generic;

namespace TradeSphere.Domain.Entities
{
    public class CryptoOptionStrategyConfig : BaseEntity
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string StrategyType { get; set; } = "ShortStrangle";
        public string Underlying { get; set; } = "BTC";
        public string Symbol { get; set; } = "BTCUSD";
        public string Exchange { get; set; } = "Delta Exchange";
        public string ExpiryType { get; set; } = "Today";
        public string EntryTime { get; set; } = "09:00";
        public string ExitTime { get; set; } = "17:15";
        public string TimeZone { get; set; } = "Asia/Kolkata";
        public decimal TargetPremiumPerLeg { get; set; } = 100m;
        public decimal StopLossPercentPerLeg { get; set; } = 100m;
        public string StrikeSelectionMode { get; set; } = "PremiumBased";
        public decimal StrikeDistancePercent { get; set; } = 1.5m;
        public decimal MaxDailyLoss { get; set; } = 250m;
        public decimal LotSize { get; set; } = 1m;
        public bool UseAtrFilter { get; set; } = true;
        public int AtrLength { get; set; } = 14;
        public decimal MaxAtrPercent { get; set; } = 1.2m;
        public bool UseTrendFilter { get; set; }
        public int EmaLength { get; set; } = 50;
        public decimal MaxTrendDistancePercent { get; set; } = 1m;
        public bool UseSlippage { get; set; } = true;
        public decimal SlippagePercent { get; set; } = 0.5m;
        public decimal BrokeragePerOrder { get; set; }
        public decimal ExchangeFeePercent { get; set; }
        public bool IsActive { get; set; } = true;
        public ICollection<CryptoOptionStrategyLeg> Legs { get; set; } = new List<CryptoOptionStrategyLeg>();
    }

    public class CryptoOptionStrategyLeg : BaseEntity
    {
        public int StrategyConfigId { get; set; }
        public CryptoOptionStrategyConfig? StrategyConfig { get; set; }
        public string LegName { get; set; } = string.Empty;
        public string Action { get; set; } = "Sell";
        public string OptionType { get; set; } = "CE";
        public string ExpiryType { get; set; } = "Today";
        public string StrikeSelectionMode { get; set; } = "PremiumBased";
        public decimal TargetPremium { get; set; } = 100m;
        public decimal StrikeDistancePercent { get; set; } = 1.5m;
        public decimal Quantity { get; set; } = 1m;
        public int SortOrder { get; set; }
    }

    public class CryptoOptionChainSnapshot : BaseEntity
    {
        public string Exchange { get; set; } = string.Empty;
        public string Underlying { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public decimal Strike { get; set; }
        public decimal UnderlyingPrice { get; set; }
        public DateTime SnapshotTime { get; set; }
        public decimal? CallPremium { get; set; }
        public decimal? CallBid { get; set; }
        public decimal? CallAsk { get; set; }
        public decimal? CallVolume { get; set; }
        public decimal? CallOpenInterest { get; set; }
        public decimal? CallIv { get; set; }
        public decimal? CallDelta { get; set; }
        public decimal? CallGamma { get; set; }
        public decimal? CallTheta { get; set; }
        public decimal? CallVega { get; set; }
        public decimal? CallRho { get; set; }
        public decimal? PutPremium { get; set; }
        public decimal? PutBid { get; set; }
        public decimal? PutAsk { get; set; }
        public decimal? PutVolume { get; set; }
        public decimal? PutOpenInterest { get; set; }
        public decimal? PutIv { get; set; }
        public decimal? PutDelta { get; set; }
        public decimal? PutGamma { get; set; }
        public decimal? PutTheta { get; set; }
        public decimal? PutVega { get; set; }
        public decimal? PutRho { get; set; }
    }

    public class CryptoOptionBacktestRun : BaseEntity
    {
        public int UserId { get; set; }
        public int? StrategyConfigId { get; set; }
        public CryptoOptionStrategyConfig? StrategyConfig { get; set; }
        public string StrategyName { get; set; } = string.Empty;
        public string StrategyType { get; set; } = "ShortStrangle";
        public string Symbol { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal InitialCapital { get; set; }
        public decimal TotalPnl { get; set; }
        public decimal GrossPnl { get; set; }
        public decimal Charges { get; set; }
        public int TotalTrades { get; set; }
        public int WinningDays { get; set; }
        public int LosingDays { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal ProfitFactor { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public ICollection<CryptoOptionBacktestPosition> Positions { get; set; } = new List<CryptoOptionBacktestPosition>();
        public ICollection<CryptoOptionDailyPnl> DailyPnls { get; set; } = new List<CryptoOptionDailyPnl>();
    }

    public class CryptoOptionBacktestPosition : BaseEntity
    {
        public int BacktestRunId { get; set; }
        public CryptoOptionBacktestRun? BacktestRun { get; set; }
        public DateTime TradeDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Underlying { get; set; } = string.Empty;
        public decimal UnderlyingEntryPrice { get; set; }
        public decimal UnderlyingExitPrice { get; set; }
        public string Status { get; set; } = "Open";
        public string ExitReason { get; set; } = string.Empty;
        public decimal GrossPnl { get; set; }
        public decimal NetPnl { get; set; }
        public decimal Charges { get; set; }
        public bool IsCircuitBreakerHit { get; set; }
        public ICollection<CryptoOptionBacktestLeg> Legs { get; set; } = new List<CryptoOptionBacktestLeg>();
    }

    public class CryptoOptionBacktestLeg : BaseEntity
    {
        public int BacktestPositionId { get; set; }
        public CryptoOptionBacktestPosition? BacktestPosition { get; set; }
        public string LegType { get; set; } = "CE";
        public string Action { get; set; } = "Sell";
        public decimal Strike { get; set; }
        public DateTime ExpiryDate { get; set; }
        public DateTime EntryTime { get; set; }
        public decimal EntryPremium { get; set; }
        public DateTime? ExitTime { get; set; }
        public decimal? ExitPremium { get; set; }
        public decimal Quantity { get; set; }
        public decimal Pnl { get; set; }
        public string Status { get; set; } = "Open";
        public string ExitReason { get; set; } = string.Empty;
        public bool StopLossHit { get; set; }
        public decimal? EntryDelta { get; set; }
        public decimal? EntryTheta { get; set; }
        public decimal? EntryIv { get; set; }
        public ICollection<CryptoOptionBacktestLegEvent> Events { get; set; } = new List<CryptoOptionBacktestLegEvent>();
    }

    public class CryptoOptionBacktestLegEvent : BaseEntity
    {
        public int BacktestLegId { get; set; }
        public CryptoOptionBacktestLeg? BacktestLeg { get; set; }
        public DateTime EventTime { get; set; }
        public string EventType { get; set; } = string.Empty;
        public decimal Premium { get; set; }
        public decimal Pnl { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class CryptoOptionDailyPnl : BaseEntity
    {
        public int BacktestRunId { get; set; }
        public CryptoOptionBacktestRun? BacktestRun { get; set; }
        public DateTime TradeDate { get; set; }
        public decimal GrossPnl { get; set; }
        public decimal NetPnl { get; set; }
        public decimal Charges { get; set; }
        public decimal MaxIntradayLoss { get; set; }
        public decimal CeLegPnl { get; set; }
        public decimal PeLegPnl { get; set; }
        public bool IsCircuitBreakerHit { get; set; }
        public string? Notes { get; set; }
    }

    public class CryptoOptionScannerResult : BaseEntity
    {
        public int UserId { get; set; }
        public string Exchange { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string ScannerMode { get; set; } = "PremiumBased";
        public DateTime ScanTime { get; set; } = DateTime.UtcNow;
        public string StrategyType { get; set; } = "ShortStrangle";
        public decimal? BestCallStrike { get; set; }
        public decimal? BestCallPremium { get; set; }
        public decimal? BestCallDelta { get; set; }
        public decimal? BestCallTheta { get; set; }
        public decimal? BestCallIv { get; set; }
        public decimal? BestPutStrike { get; set; }
        public decimal? BestPutPremium { get; set; }
        public decimal? BestPutDelta { get; set; }
        public decimal? BestPutTheta { get; set; }
        public decimal? BestPutIv { get; set; }
        public decimal StrategyScore { get; set; }
        public decimal ProbabilityOfProfit { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
