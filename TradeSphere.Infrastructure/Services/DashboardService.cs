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
                    ErrorReason = t.ErrorReason,
                    TimeAgo = GetTimeAgo(t.CreatedAt)
                })
                .ToListAsync();

            var totalPnl = await _context.Trades
                .Where(t => t.UserId == userId && t.Status == "Filled")
                .SumAsync(t => t.Pnl);

            var strategyTrades = await _context.Trades
                .Include(t => t.UserStrategy)
                    .ThenInclude(us => us.Strategy)
                .Where(t => t.UserId == userId && t.Status == "Filled" && t.UserStrategyId != null)
                .ToListAsync();

            var topStrategies = strategyTrades
                .GroupBy(t => t.UserStrategy?.Strategy?.Name ?? "Unknown Strategy")
                .Select(g =>
                {
                    var totalTrades = g.Count();
                    var winningTrades = g.Count(t => t.Pnl > 0);

                    return new StrategyPerformanceDto
                    {
                        Name = g.Key,
                        Pnl = g.Sum(t => t.Pnl),
                        WinRate = totalTrades > 0 ? Math.Round((decimal)winningTrades / totalTrades * 100m, 2) : 0m
                    };
                })
                .Where(s => s.Pnl != 0 || s.WinRate != 0)
                .OrderByDescending(s => s.Pnl)
                .Take(5)
                .ToList();

            return new DashboardDto
            {
                TotalBalance = 0m,
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
