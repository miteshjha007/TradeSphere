using TradeSphere.Application.Common.Interfaces;

namespace TradeSphere.Infrastructure.Services
{
    public class OptionRiskManager : IOptionRiskManager
    {
        public bool IsLegStopLossHit(decimal entryPremium, decimal currentPremium, decimal stopLossPercent)
        {
            if (entryPremium <= 0 || currentPremium <= 0)
                return false;

            var triggerPremium = entryPremium * (1 + stopLossPercent / 100m);
            return currentPremium >= triggerPremium;
        }

        public bool IsDailyCircuitBreakerHit(decimal currentDailyPnl, decimal maxDailyLoss)
        {
            return maxDailyLoss > 0 && currentDailyPnl <= -Math.Abs(maxDailyLoss);
        }
    }

    public class OptionAnalyticsService : IOptionAnalyticsService
    {
        public decimal CalculateShortOptionPnl(decimal entryPremium, decimal exitPremium, decimal quantity)
        {
            return (entryPremium - exitPremium) * quantity;
        }

        public decimal EstimateProbabilityOfProfit(decimal? callDelta, decimal? putDelta)
        {
            var callRisk = Math.Abs(callDelta ?? 0.15m);
            var putRisk = Math.Abs(putDelta ?? 0.15m);
            var pop = 1m - Math.Min(0.95m, callRisk + putRisk);
            return Math.Round(Math.Max(0.01m, pop) * 100m, 2);
        }
    }
}
