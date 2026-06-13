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
            return await _context.UserStrategies
                .Include(us => us.Strategy)
                .Include(us => us.Exchange)
                .Where(us => us.UserId == userId)
                .Select(us => new UserStrategyDto
                {
                    Id = us.Id,
                    StrategyId = us.StrategyId,
                    StrategyName = us.Strategy.Name,
                    ExchangeId = us.ExchangeId,
                    ExchangeName = us.Exchange.Name,
                    Symbol = us.Symbol,
                    Config = us.Config,
                    Status = us.Status,
                    StartedAt = us.StartedAt
                })
                .ToListAsync();
        }

        public async Task<UserStrategyDto> DeployStrategyAsync(int userId, DeployStrategyDto dto)
        {
            var userStrategy = new UserStrategy
            {
                UserId = userId,
                StrategyId = dto.StrategyId,
                ExchangeId = dto.ExchangeId,
                Symbol = dto.Symbol,
                Config = dto.Config,
                Status = "Stopped",
                StartedAt = null
            };

            _context.UserStrategies.Add(userStrategy);
            await _context.SaveChangesAsync();

            await _context.Entry(userStrategy).Reference(us => us.Strategy).LoadAsync();
            await _context.Entry(userStrategy).Reference(us => us.Exchange).LoadAsync();

            return new UserStrategyDto
            {
                Id = userStrategy.Id,
                StrategyId = userStrategy.StrategyId,
                StrategyName = userStrategy.Strategy.Name,
                ExchangeId = userStrategy.ExchangeId,
                ExchangeName = userStrategy.Exchange.Name,
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
                _context.UserStrategies.Remove(strategy);
                await _context.SaveChangesAsync();
            }
        }
    }
}
