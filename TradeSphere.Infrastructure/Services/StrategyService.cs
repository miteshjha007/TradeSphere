using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;
using TradeSphere.Domain.Entities;
using TradeSphere.Infrastructure.Persistence;

namespace TradeSphere.Infrastructure.Services
{
    public class StrategyService : IStrategyService
    {
        private readonly ApplicationDbContext _context;

        public StrategyService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<StrategyDto>> GetAvailableStrategiesAsync()
        {
            return await _context.Strategies
                .Select(s => new StrategyDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    LogicType = s.LogicType,
                    DefaultConfig = s.DefaultConfig,
                    IsPublic = s.IsPublic,
                    CreatedBy = s.CreatedBy
                })
                .ToListAsync();
        }

        public async Task<List<UserStrategyDto>> GetUserStrategiesAsync(int userId)
        {
            var strategies = await _context.UserStrategies
                .Include(us => us.Strategy)
                .Include(us => us.Exchange)
                .Include(us => us.Mt5Account)
                .Where(us => us.UserId == userId)
                .ToListAsync();

            var strategyIds = strategies.Select(s => s.Id).ToList();
            var healthByStrategyId = await _context.StrategyHealthSnapshots
                .Where(h => strategyIds.Contains(h.UserStrategyId))
                .ToDictionaryAsync(h => h.UserStrategyId);

            return strategies
                .Select(us =>
                {
                    healthByStrategyId.TryGetValue(us.Id, out var health);

                    return new UserStrategyDto
                    {
                        Id = us.Id,
                        StrategyId = us.StrategyId,
                        StrategyName = us.Strategy.Name,
                        ExchangeId = us.ExchangeId,
                        ExchangeName = us.ExecutionProvider == "MT5" && us.Mt5Account != null ? "MT5" : us.Exchange.Name,
                        ExecutionProvider = us.ExecutionProvider,
                        Mt5AccountId = us.Mt5AccountId,
                        Mt5AccountName = us.Mt5Account != null ? us.Mt5Account.Name : null,
                        Symbol = us.Symbol,
                        Config = us.Config,
                        Status = us.Status,
                        StartedAt = us.StartedAt,
                        Health = health == null
                            ? null
                            : new StrategyHealthSnapshotDto
                            {
                                LastCheckedAt = health.LastCheckedAt,
                                Symbol = health.Symbol,
                                Resolution = health.Resolution,
                                Price = health.Price,
                                Position = health.Position,
                                IsEntryEligible = health.IsEntryEligible,
                                SuggestedSide = health.SuggestedSide,
                                Status = health.Status,
                                Reason = health.Reason,
                                DetailsJson = health.DetailsJson
                            }
                    };
                })
                .ToList();
        }

        public async Task<UserStrategyDto> DeployStrategyAsync(int userId, DeployStrategyDto dto)
        {
            var provider = string.IsNullOrWhiteSpace(dto.ExecutionProvider)
                ? "Delta"
                : dto.ExecutionProvider.Trim();

            UserExchange? userExchange = null;
            Mt5Account? mt5Account = null;
            int exchangeId;

            if (provider.Equals("MT5", StringComparison.OrdinalIgnoreCase))
            {
                if (!dto.Mt5AccountId.HasValue)
                    throw new System.Exception("MT5 account is required for MT5 strategy deployment.");

                mt5Account = await _context.Mt5Accounts
                    .FirstOrDefaultAsync(a => a.Id == dto.Mt5AccountId.Value && a.UserId == userId);

                if (mt5Account == null)
                    throw new System.Exception("MT5 account not found or does not belong to this user.");

                var mt5Exchange = await _context.Exchanges.FirstOrDefaultAsync(e => e.Name == "MT5");
                if (mt5Exchange == null)
                {
                    mt5Exchange = new Exchange
                    {
                        Name = "MT5",
                        BaseUrl = "local-mt5-bridge",
                        IsActive = true
                    };
                    _context.Exchanges.Add(mt5Exchange);
                    await _context.SaveChangesAsync();
                }

                exchangeId = mt5Exchange.Id;
                provider = "MT5";
            }
            else
            {
                if (!dto.UserExchangeId.HasValue)
                    throw new System.Exception("Exchange account is required for exchange strategy deployment.");

                userExchange = await _context.UserExchanges
                    .Include(ue => ue.Exchange)
                    .FirstOrDefaultAsync(ue => ue.Id == dto.UserExchangeId.Value && ue.UserId == userId);

                if (userExchange == null)
                    throw new System.Exception("Exchange account not found or does not belong to this user.");

                exchangeId = userExchange.ExchangeId;
                provider = ResolveExchangeProvider(userExchange.Exchange?.Name, provider);
            }

            var userStrategy = new UserStrategy
            {
                UserId = userId,
                StrategyId = dto.StrategyId,
                ExchangeId = exchangeId,
                UserExchangeId = userExchange?.Id,
                ExecutionProvider = provider,
                Mt5AccountId = mt5Account?.Id,
                Symbol = dto.Symbol,
                Config = dto.Config,
                Status = "Stopped",
                StartedAt = null
            };

            _context.UserStrategies.Add(userStrategy);
            await _context.SaveChangesAsync();

            await _context.Entry(userStrategy).Reference(us => us.Strategy).LoadAsync();
            await _context.Entry(userStrategy).Reference(us => us.Exchange).LoadAsync();
            await _context.Entry(userStrategy).Reference(us => us.Mt5Account).LoadAsync();

            return new UserStrategyDto
            {
                Id = userStrategy.Id,
                StrategyId = userStrategy.StrategyId,
                StrategyName = userStrategy.Strategy.Name,
                ExchangeId = userStrategy.ExchangeId,
                ExchangeName = userStrategy.ExecutionProvider == "MT5" ? "MT5" : userStrategy.Exchange.Name,
                ExecutionProvider = userStrategy.ExecutionProvider,
                Mt5AccountId = userStrategy.Mt5AccountId,
                Mt5AccountName = userStrategy.Mt5Account?.Name,
                Symbol = userStrategy.Symbol,
                Config = userStrategy.Config,
                Status = userStrategy.Status
            };
        }

        public async Task<StrategyDto> CreateStrategyAsync(int userId, CreateStrategyDto dto)
        {
            var strategy = new Strategy
            {
                Name = dto.Name,
                Description = dto.Description,
                LogicType = dto.LogicType,
                DefaultConfig = dto.DefaultConfig,
                IsPublic = dto.IsPublic,
                CreatedBy = userId
            };

            _context.Strategies.Add(strategy);
            await _context.SaveChangesAsync();

            return new StrategyDto
            {
                Id = strategy.Id,
                Name = strategy.Name,
                Description = strategy.Description,
                LogicType = strategy.LogicType,
                DefaultConfig = strategy.DefaultConfig,
                IsPublic = strategy.IsPublic,
                CreatedBy = strategy.CreatedBy
            };
        }

        public async Task ToggleStrategyStatusAsync(int userId, int userStrategyId, string status)
        {
            var strategy = await _context.UserStrategies
                .FirstOrDefaultAsync(us => us.Id == userStrategyId && us.UserId == userId);

            if (strategy != null)
            {
                strategy.Status = status;
                if (status == "Running")
                {
                    strategy.StartedAt = DateTime.UtcNow;
                    strategy.StoppedAt = null;
                }
                else
                {
                    strategy.StoppedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteUserStrategyAsync(int userId, int userStrategyId)
        {
            var strategy = await _context.UserStrategies
                .FirstOrDefaultAsync(us => us.Id == userStrategyId && us.UserId == userId);
            
            if (strategy != null)
            {
                if (strategy.Status == "Running")
                {
                    throw new System.Exception("Cannot delete a strategy that is currently running. Please stop it first.");
                }

                // Disassociate any trades referencing this strategy before deletion
                var associatedTrades = await _context.Trades
                    .Where(t => t.UserStrategyId == userStrategyId)
                    .ToListAsync();

                foreach (var trade in associatedTrades)
                {
                    trade.UserStrategyId = null;
                }

                _context.UserStrategies.Remove(strategy);
                await _context.SaveChangesAsync();
            }
        }

        private static string ResolveExchangeProvider(string? exchangeName, string requestedProvider)
        {
            if (requestedProvider.Equals("CoinDCX", StringComparison.OrdinalIgnoreCase) ||
                exchangeName?.Contains("CoinDCX", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "CoinDCX";
            }

            return "Delta";
        }
    }
}
