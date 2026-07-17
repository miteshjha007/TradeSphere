using TradeSphere.Application.DTOs;
using TradeSphere.Domain.Entities;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IOptionStrategy
    {
        string StrategyType { get; }
        IReadOnlyList<CryptoOptionStrategyLeg> BuildDefaultLegs(CryptoOptionStrategyConfig config);
    }

    public interface IOptionScanner
    {
        Task<IReadOnlyList<CryptoOptionScannerResultDto>> ScanAsync(int userId, string exchange, string symbol, string scannerMode);
    }

    public interface IOptionBacktester
    {
        Task<CryptoOptionBacktestRunDto> RunAsync(int userId, CryptoOptionBacktestRequestDto request);
    }

    public interface IOptionPaperTrader
    {
        Task TrackAsync(int userId, int strategyConfigId, CancellationToken cancellationToken = default);
    }

    public interface IOptionExecutionService
    {
        Task ExecuteAsync(int userId, int strategyConfigId, CancellationToken cancellationToken = default);
    }

    public interface IOptionRiskManager
    {
        bool IsLegStopLossHit(decimal entryPremium, decimal currentPremium, decimal stopLossPercent);
        bool IsDailyCircuitBreakerHit(decimal currentDailyPnl, decimal maxDailyLoss);
    }

    public interface IOptionChainProvider
    {
        Task<IReadOnlyList<CryptoOptionChainSnapshotDto>> GetSnapshotsAsync(string exchange, string symbol, DateTime from, DateTime to);
    }

    public interface IOptionAnalyticsService
    {
        decimal CalculateShortOptionPnl(decimal entryPremium, decimal exitPremium, decimal quantity);
        decimal EstimateProbabilityOfProfit(decimal? callDelta, decimal? putDelta);
    }

    public interface ICryptoOptionsService
    {
        Task<IReadOnlyList<CryptoOptionStrategyConfigDto>> GetConfigsAsync(int userId);
        Task<CryptoOptionStrategyConfigDto> CreateConfigAsync(int userId, UpsertCryptoOptionStrategyConfigDto dto);
        Task<CryptoOptionStrategyConfigDto> UpdateConfigAsync(int userId, int id, UpsertCryptoOptionStrategyConfigDto dto);
        Task DeleteConfigAsync(int userId, int id);
        Task<IReadOnlyList<CryptoOptionChainSnapshotDto>> GetChainSnapshotsAsync(string? exchange, string? symbol, DateTime? from, DateTime? to);
        Task<IReadOnlyList<CryptoOptionExpiryDto>> GetDeltaExpiriesAsync(string? exchange, string? underlying, string? symbol);
        Task<CryptoOptionChainFetchResultDto> FetchDeltaChainAsync(FetchCryptoOptionChainRequestDto request);
        Task<int> ImportChainSnapshotsAsync(IReadOnlyList<ImportCryptoOptionChainSnapshotDto> snapshots);
        Task<IReadOnlyList<CryptoOptionBacktestRunDto>> GetBacktestRunsAsync(int userId);
        Task<CryptoOptionBacktestRunDto> GetBacktestRunAsync(int userId, int id);
        Task<IReadOnlyList<CryptoOptionBacktestPositionDto>> GetBacktestPositionsAsync(int userId, int runId);
        Task<IReadOnlyList<CryptoOptionBacktestLegDto>> GetBacktestLegsAsync(int userId, int runId);
        Task<IReadOnlyList<CryptoOptionDailyPnlDto>> GetDailyPnlAsync(int userId, int runId);
        Task<CryptoOptionRiskReportDto> GetRiskReportAsync(int userId);
    }

    public interface ICryptoOptionsBacktestService : IOptionBacktester
    {
    }
}

