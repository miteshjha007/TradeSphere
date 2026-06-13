using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;
using TradeSphere.Infrastructure.Persistence;

namespace TradeSphere.Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardDto> GetDashboardDataAsync(int userId)
        {
            var userStrategies = await _context.UserStrategies
                .Where(us => us.UserId == userId && us.Status == "Running")
                .ToListAsync();

            var userExchanges = await _context.UserExchanges
                .Where(ue => ue.UserId == userId && ue.Status == "Active")
                .CountAsync();

            var recentTrades = await _context.Trades
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Select(t => new RecentTradeDto
                {
                    Symbol = t.Symbol,
                    Side = t.Side,
                    Price = t.Price ?? 0,
                    Quantity = t.Quantity,
                    Status = t.Status,
                    TimeAgo = GetTimeAgo(t.CreatedAt)
                })
                .ToListAsync();

            var totalPnl = await _context.Trades
                .Where(t => t.UserId == userId)
                .SumAsync(t => t.Pnl);

            // Mock Data for Top Strategies as we filter from UserStrategies or Backtests
            var topStrategies = new List<StrategyPerformanceDto>
            {
                new StrategyPerformanceDto { Name = "Cosmic Turbo Trend", Pnl = 1250.50m, WinRate = 65.5m },
                new StrategyPerformanceDto { Name = "Gold Mine", Pnl = 850.20m, WinRate = 58.2m }
            };

            return new DashboardDto
            {
                TotalBalance = 10000 + totalPnl, // Mock starting balance + PnL
                TotalPnl = totalPnl,
                ActiveStrategies = userStrategies.Count,
                ConnectedExchanges = userExchanges,
                RecentTrades = recentTrades,
                TopStrategies = topStrategies
            };
        }

        private static string GetTimeAgo(DateTime dateTime)
        {
            var span = DateTime.UtcNow - dateTime;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }
}
