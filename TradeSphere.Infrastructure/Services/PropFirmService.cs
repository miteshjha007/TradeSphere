using Microsoft.EntityFrameworkCore;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;
using TradeSphere.Domain.Entities;
using TradeSphere.Infrastructure.Persistence;

namespace TradeSphere.Infrastructure.Services
{
    public class PropFirmService : IPropFirmService
    {
        private readonly ApplicationDbContext _context;

        public PropFirmService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<PropFirmDto>> GetFirmsAsync(int userId)
        {
            return await _context.PropFirms
                .Where(f => f.UserId == userId)
                .OrderBy(f => f.Name)
                .Select(f => ToDto(f))
                .ToListAsync();
        }

        public async Task<PropFirmDto> CreateFirmAsync(int userId, CreatePropFirmDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Prop firm name is required.");

            var firm = new PropFirm
            {
                UserId = userId,
                Name = dto.Name.Trim(),
                WebsiteUrl = dto.WebsiteUrl?.Trim(),
                Notes = dto.Notes?.Trim(),
                Status = "Active"
            };

            _context.PropFirms.Add(firm);
            await _context.SaveChangesAsync();
            return ToDto(firm);
        }

        public async Task DeleteFirmAsync(int userId, int id)
        {
            var firm = await _context.PropFirms.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
            if (firm == null)
                throw new InvalidOperationException("Prop firm not found.");

            _context.PropFirms.Remove(firm);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<PropFirmAccountDto>> GetAccountsAsync(int userId)
        {
            return await _context.PropFirmAccounts
                .Include(a => a.PropFirm)
                .Include(a => a.Mt5Account)
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.PropFirm.Name)
                .ThenBy(a => a.Name)
                .Select(a => ToDto(a))
                .ToListAsync();
        }

        public async Task<PropFirmAccountDto> CreateAccountAsync(int userId, CreatePropFirmAccountDto dto)
        {
            var firm = await _context.PropFirms.FirstOrDefaultAsync(f => f.Id == dto.PropFirmId && f.UserId == userId);
            if (firm == null)
                throw new InvalidOperationException("Prop firm not found.");

            Mt5Account? mt5Account = null;
            if (dto.Mt5AccountId.HasValue)
            {
                mt5Account = await _context.Mt5Accounts.FirstOrDefaultAsync(a => a.Id == dto.Mt5AccountId.Value && a.UserId == userId);
                if (mt5Account == null)
                    throw new InvalidOperationException("Linked MT5 account not found.");
            }

            if (dto.AccountSize <= 0)
                throw new ArgumentException("Account size must be greater than zero.");

            var account = new PropFirmAccount
            {
                UserId = userId,
                PropFirmId = dto.PropFirmId,
                Mt5AccountId = dto.Mt5AccountId,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? $"{firm.Name} Challenge" : dto.Name.Trim(),
                AccountSize = dto.AccountSize,
                ProfitTarget = dto.ProfitTarget,
                DailyDrawdownLimit = dto.DailyDrawdownLimit,
                MaxDrawdownLimit = dto.MaxDrawdownLimit,
                MinimumTradingDays = dto.MinimumTradingDays,
                MaxRiskPerTradePercent = dto.MaxRiskPerTradePercent,
                NewsTradingAllowed = dto.NewsTradingAllowed,
                WeekendHoldingAllowed = dto.WeekendHoldingAllowed,
                StartedAt = dto.StartedAt,
                Notes = dto.Notes,
                Status = "Active"
            };

            _context.PropFirmAccounts.Add(account);
            await _context.SaveChangesAsync();
            account.PropFirm = firm;
            account.Mt5Account = mt5Account;
            return ToDto(account);
        }

        public async Task DeleteAccountAsync(int userId, int id)
        {
            var account = await _context.PropFirmAccounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (account == null)
                throw new InvalidOperationException("Prop firm account not found.");

            _context.PropFirmAccounts.Remove(account);
            await _context.SaveChangesAsync();
        }

        private static PropFirmDto ToDto(PropFirm firm)
        {
            return new PropFirmDto
            {
                Id = firm.Id,
                Name = firm.Name,
                WebsiteUrl = firm.WebsiteUrl,
                Status = firm.Status,
                Notes = firm.Notes
            };
        }

        private static PropFirmAccountDto ToDto(PropFirmAccount account)
        {
            return new PropFirmAccountDto
            {
                Id = account.Id,
                PropFirmId = account.PropFirmId,
                PropFirmName = account.PropFirm?.Name ?? "Prop Firm",
                Mt5AccountId = account.Mt5AccountId,
                Mt5AccountName = account.Mt5Account?.Name,
                Name = account.Name,
                AccountSize = account.AccountSize,
                ProfitTarget = account.ProfitTarget,
                DailyDrawdownLimit = account.DailyDrawdownLimit,
                MaxDrawdownLimit = account.MaxDrawdownLimit,
                MinimumTradingDays = account.MinimumTradingDays,
                MaxRiskPerTradePercent = account.MaxRiskPerTradePercent,
                NewsTradingAllowed = account.NewsTradingAllowed,
                WeekendHoldingAllowed = account.WeekendHoldingAllowed,
                Status = account.Status,
                StartedAt = account.StartedAt,
                Notes = account.Notes
            };
        }
    }
}
