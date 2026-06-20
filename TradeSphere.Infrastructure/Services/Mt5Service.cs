using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;
using TradeSphere.Domain.Entities;
using TradeSphere.Infrastructure.Persistence;
using TradeSphere.Infrastructure.Security;

namespace TradeSphere.Infrastructure.Services
{
    public class Mt5Service : IMt5Service
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMt5BridgeClient _bridgeClient;

        public Mt5Service(ApplicationDbContext context, IConfiguration configuration, IMt5BridgeClient bridgeClient)
        {
            _context = context;
            _configuration = configuration;
            _bridgeClient = bridgeClient;
        }

        public async Task<IReadOnlyList<Mt5AccountDto>> GetAccountsAsync(int userId)
        {
            return await _context.Mt5Accounts
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Name)
                .Select(a => ToDto(a))
                .ToListAsync();
        }

        public async Task<Mt5AccountDto> ConnectAccountAsync(int userId, ConnectMt5AccountDto dto)
        {
            if (dto.Login <= 0)
                throw new ArgumentException("MT5 login is required.");
            if (string.IsNullOrWhiteSpace(dto.Server))
                throw new ArgumentException("MT5 broker server is required.");
            if (string.IsNullOrWhiteSpace(dto.Password))
                throw new ArgumentException("MT5 password is required.");

            var account = new Mt5Account
            {
                UserId = userId,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? $"{dto.Server} - {dto.Login}" : dto.Name.Trim(),
                Login = dto.Login,
                Server = dto.Server.Trim(),
                EncryptedPassword = EncryptionHelper.Encrypt(dto.Password),
                AccountType = string.IsNullOrWhiteSpace(dto.AccountType) ? "Demo" : dto.AccountType.Trim(),
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "USD" : dto.Currency.Trim().ToUpperInvariant(),
                Leverage = dto.Leverage,
                TradingEnabled = dto.TradingEnabled,
                Status = "PendingBridge",
                LastError = "MT5 account saved. Start/connect the MT5 bridge to test live account connectivity."
            };

            _context.Mt5Accounts.Add(account);
            await _context.SaveChangesAsync();

            await EnsureDefaultMappingsAsync(userId, account.Id);
            return ToDto(account);
        }

        public async Task DeleteAccountAsync(int userId, int id)
        {
            var account = await _context.Mt5Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (account == null)
                throw new InvalidOperationException("MT5 account not found.");

            _context.Mt5Accounts.Remove(account);
            await _context.SaveChangesAsync();
        }

        public async Task<Mt5ConnectionTestResultDto> TestConnectionAsync(int userId, int id)
        {
            var account = await _context.Mt5Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (account == null)
                throw new InvalidOperationException("MT5 account not found.");

            var bridgeEndpoint = _configuration["Mt5Bridge:BaseUrl"];
            var bridgeResult = await _bridgeClient.GetAccountInfoAsync(new Mt5BridgeAccountRequestDto
            {
                Login = account.Login,
                Server = account.Server,
                Password = EncryptionHelper.Decrypt(account.EncryptedPassword)
            });

            account.LastSyncedAt = DateTime.UtcNow;
            if (bridgeResult.Success)
            {
                account.Status = "Connected";
                account.LastError = null;
                account.Balance = bridgeResult.Balance;
                account.Equity = bridgeResult.Equity;
                account.FreeMargin = bridgeResult.FreeMargin;
                account.Currency = string.IsNullOrWhiteSpace(bridgeResult.Currency) ? account.Currency : bridgeResult.Currency;
                account.Leverage = bridgeResult.Leverage ?? account.Leverage;
            }
            else
            {
                account.Status = "Error";
                account.LastError = bridgeResult.Message;
            }

            await _context.SaveChangesAsync();

            return new Mt5ConnectionTestResultDto
            {
                Success = bridgeResult.Success,
                Status = account.Status,
                Message = bridgeResult.Message,
                Balance = account.Balance,
                Equity = account.Equity,
                FreeMargin = account.FreeMargin,
                BridgeEndpoint = bridgeEndpoint
            };
        }

        public async Task<IReadOnlyList<Mt5SymbolMappingDto>> GetSymbolMappingsAsync(int userId)
        {
            return await _context.Mt5SymbolMappings
                .Include(m => m.Mt5Account)
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.Mt5Account.Name)
                .ThenBy(m => m.StrategySymbol)
                .Select(m => ToDto(m))
                .ToListAsync();
        }

        public async Task<Mt5SymbolMappingDto> UpsertSymbolMappingAsync(int userId, UpsertMt5SymbolMappingDto dto)
        {
            var account = await _context.Mt5Accounts.FirstOrDefaultAsync(a => a.Id == dto.Mt5AccountId && a.UserId == userId);
            if (account == null)
                throw new InvalidOperationException("MT5 account not found.");

            var strategySymbol = NormalizeSymbol(dto.StrategySymbol);
            var brokerSymbol = dto.BrokerSymbol?.Trim();
            if (string.IsNullOrWhiteSpace(strategySymbol) || string.IsNullOrWhiteSpace(brokerSymbol))
                throw new ArgumentException("Strategy symbol and broker symbol are required.");

            var mapping = await _context.Mt5SymbolMappings
                .FirstOrDefaultAsync(m => m.UserId == userId && m.Mt5AccountId == dto.Mt5AccountId && m.StrategySymbol == strategySymbol);

            if (mapping == null)
            {
                mapping = new Mt5SymbolMapping
                {
                    UserId = userId,
                    Mt5AccountId = dto.Mt5AccountId,
                    StrategySymbol = strategySymbol,
                    BrokerSymbol = brokerSymbol,
                    IsActive = dto.IsActive,
                    Notes = dto.Notes
                };
                _context.Mt5SymbolMappings.Add(mapping);
            }
            else
            {
                mapping.BrokerSymbol = brokerSymbol;
                mapping.IsActive = dto.IsActive;
                mapping.Notes = dto.Notes;
                mapping.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            mapping.Mt5Account = account;
            return ToDto(mapping);
        }

        public async Task DeleteSymbolMappingAsync(int userId, int id)
        {
            var mapping = await _context.Mt5SymbolMappings.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            if (mapping == null)
                throw new InvalidOperationException("Symbol mapping not found.");

            _context.Mt5SymbolMappings.Remove(mapping);
            await _context.SaveChangesAsync();
        }

        private async Task EnsureDefaultMappingsAsync(int userId, int accountId)
        {
            var defaults = new[] { "BTCUSD", "ETHUSD", "XAUUSD" };
            foreach (var symbol in defaults)
            {
                _context.Mt5SymbolMappings.Add(new Mt5SymbolMapping
                {
                    UserId = userId,
                    Mt5AccountId = accountId,
                    StrategySymbol = symbol,
                    BrokerSymbol = symbol,
                    Notes = "Default mapping. Change broker symbol if your MT5 broker uses suffixes like BTCUSDm."
                });
            }

            await _context.SaveChangesAsync();
        }

        private static string NormalizeSymbol(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static Mt5AccountDto ToDto(Mt5Account account)
        {
            return new Mt5AccountDto
            {
                Id = account.Id,
                Name = account.Name,
                Login = account.Login,
                LoginPreview = MaskLogin(account.Login),
                Server = account.Server,
                AccountType = account.AccountType,
                Currency = account.Currency,
                Leverage = account.Leverage,
                TradingEnabled = account.TradingEnabled,
                Status = account.Status,
                Balance = account.Balance,
                Equity = account.Equity,
                FreeMargin = account.FreeMargin,
                LastSyncedAt = account.LastSyncedAt,
                LastError = account.LastError
            };
        }

        private static Mt5SymbolMappingDto ToDto(Mt5SymbolMapping mapping)
        {
            return new Mt5SymbolMappingDto
            {
                Id = mapping.Id,
                Mt5AccountId = mapping.Mt5AccountId,
                AccountName = mapping.Mt5Account?.Name ?? "MT5 Account",
                StrategySymbol = mapping.StrategySymbol,
                BrokerSymbol = mapping.BrokerSymbol,
                IsActive = mapping.IsActive,
                Notes = mapping.Notes
            };
        }

        private static string MaskLogin(long login)
        {
            var text = login.ToString();
            return text.Length <= 4 ? "****" : $"{new string('*', text.Length - 4)}{text[^4..]}";
        }
    }
}
