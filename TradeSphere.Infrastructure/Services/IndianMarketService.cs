using Microsoft.EntityFrameworkCore;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;
using TradeSphere.Domain.Entities;
using TradeSphere.Infrastructure.Persistence;
using TradeSphere.Infrastructure.Security;

namespace TradeSphere.Infrastructure.Services
{
    public class IndianMarketService : IIndianMarketService
    {
        private const string DhanExchangeName = "Dhan";
        private readonly ApplicationDbContext _context;
        private readonly IDhanClient _dhanClient;

        private static readonly IReadOnlyList<IndianUnderlyingDto> Underlyings = new List<IndianUnderlyingDto>
        {
            new()
            {
                Symbol = "NIFTY",
                DisplayName = "Nifty 50",
                UnderlyingScrip = 13,
                UnderlyingSegment = "IDX_I",
                LotSize = 75,
                StrikeStep = 50
            },
            new()
            {
                Symbol = "BANKNIFTY",
                DisplayName = "Bank Nifty",
                UnderlyingScrip = 25,
                UnderlyingSegment = "IDX_I",
                LotSize = 35,
                StrikeStep = 100
            }
        };

        public IndianMarketService(ApplicationDbContext context, IDhanClient dhanClient)
        {
            _context = context;
            _dhanClient = dhanClient;
        }

        public IReadOnlyList<IndianUnderlyingDto> GetSupportedUnderlyings()
        {
            return Underlyings;
        }

        public async Task<IReadOnlyList<DhanAccountDto>> GetDhanAccountsAsync(int userId)
        {
            return await _context.UserExchanges
                .Include(ue => ue.Exchange)
                .Where(ue => ue.UserId == userId && ue.Exchange.Name == DhanExchangeName)
                .OrderBy(ue => ue.Name)
                .Select(ue => new DhanAccountDto
                {
                    Id = ue.Id,
                    Name = ue.Name,
                    Status = ue.Status,
                    ClientIdPreview = "****"
                })
                .ToListAsync();
        }

        public async Task<DhanAccountDto> ConnectDhanAccountAsync(int userId, ConnectDhanAccountDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.DhanClientId) || string.IsNullOrWhiteSpace(dto.AccessToken))
            {
                throw new InvalidOperationException("Account name, Dhan client ID, and access token are required.");
            }

            var exchange = await GetDhanExchangeAsync();
            var entity = new UserExchange
            {
                UserId = userId,
                ExchangeId = exchange.Id,
                Name = dto.Name.Trim(),
                ApiKey = EncryptionHelper.Encrypt(dto.DhanClientId.Trim()),
                ApiSecret = EncryptionHelper.Encrypt(dto.AccessToken.Trim()),
                Status = "Active"
            };

            _context.UserExchanges.Add(entity);
            await _context.SaveChangesAsync();

            return new DhanAccountDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Status = entity.Status,
                ClientIdPreview = Mask(dto.DhanClientId)
            };
        }

        public async Task DeleteDhanAccountAsync(int userId, int accountId)
        {
            var account = await _context.UserExchanges
                .Include(ue => ue.Exchange)
                .FirstOrDefaultAsync(ue => ue.Id == accountId && ue.UserId == userId && ue.Exchange.Name == DhanExchangeName);

            if (account == null)
            {
                return;
            }

            _context.UserExchanges.Remove(account);
            await _context.SaveChangesAsync();
        }

        public async Task<DhanConnectionTestResultDto> TestDhanConnectionAsync(int userId, int accountId)
        {
            var credentials = await GetCredentialsAsync(userId, accountId);
            var result = await _dhanClient.TestConnectionAsync(credentials.ClientId, credentials.AccessToken);
            var account = credentials.Account;
            account.Status = result.Success ? "Active" : "Error";
            await _context.SaveChangesAsync();
            return result;
        }

        public async Task<IReadOnlyList<string>> GetExpiriesAsync(int userId, OptionExpiryRequestDto dto)
        {
            var credentials = await GetCredentialsAsync(userId, dto.DhanAccountId);
            var underlying = ResolveUnderlying(dto.Underlying);
            return await _dhanClient.GetOptionExpiriesAsync(credentials.ClientId, credentials.AccessToken, underlying);
        }

        public async Task<OptionChainDto> GetOptionChainAsync(int userId, OptionChainRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Expiry))
            {
                throw new InvalidOperationException("Expiry is required.");
            }

            var credentials = await GetCredentialsAsync(userId, dto.DhanAccountId);
            var underlying = ResolveUnderlying(dto.Underlying);
            return await _dhanClient.GetOptionChainAsync(credentials.ClientId, credentials.AccessToken, underlying, dto.Expiry);
        }

        public async Task<DhanOrderResultDto> PlaceOptionOrderAsync(int userId, DhanOptionOrderRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SecurityId))
            {
                throw new InvalidOperationException("Security ID is required for Dhan option orders.");
            }

            if (dto.Quantity <= 0)
            {
                throw new InvalidOperationException("Quantity must be greater than zero.");
            }

            var credentials = await GetCredentialsAsync(userId, dto.DhanAccountId);
            var result = await _dhanClient.PlaceOptionOrderAsync(credentials.ClientId, credentials.AccessToken, dto);

            var exchange = await GetDhanExchangeAsync();
            _context.Trades.Add(new Trade
            {
                UserId = userId,
                ExchangeId = exchange.Id,
                Symbol = $"{dto.Underlying} {dto.Expiry} {dto.StrikePrice:0} {dto.OptionType.ToUpperInvariant()}",
                Side = dto.TransactionType.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? "Buy" : "Sell",
                OrderType = dto.OrderType,
                Quantity = dto.Quantity,
                Price = dto.Price,
                Status = result.Success ? "Filled" : "Failed",
                ExecutedAt = DateTime.UtcNow,
                ExternalOrderId = result.OrderId ?? string.Empty,
                ErrorReason = result.Success ? $"Dhan:{result.OrderStatus}" : result.Message,
                BrokerResponse = result.RawResponse
            });

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<(UserExchange Account, string ClientId, string AccessToken)> GetCredentialsAsync(int userId, int accountId)
        {
            var account = await _context.UserExchanges
                .Include(ue => ue.Exchange)
                .FirstOrDefaultAsync(ue => ue.Id == accountId && ue.UserId == userId && ue.Exchange.Name == DhanExchangeName);

            if (account == null)
            {
                throw new InvalidOperationException("Dhan account not found.");
            }

            return (account, EncryptionHelper.Decrypt(account.ApiKey), EncryptionHelper.Decrypt(account.ApiSecret));
        }

        private async Task<Exchange> GetDhanExchangeAsync()
        {
            var exchange = await _context.Exchanges.FirstOrDefaultAsync(e => e.Name == DhanExchangeName);
            if (exchange != null)
            {
                return exchange;
            }

            exchange = new Exchange
            {
                Name = DhanExchangeName,
                BaseUrl = "https://api.dhan.co/v2",
                IsActive = true
            };
            _context.Exchanges.Add(exchange);
            await _context.SaveChangesAsync();
            return exchange;
        }

        private static IndianUnderlyingDto ResolveUnderlying(string symbol)
        {
            var normalized = symbol.Trim().Replace(" ", string.Empty).ToUpperInvariant();
            var underlying = Underlyings.FirstOrDefault(u => u.Symbol.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (underlying == null)
            {
                throw new InvalidOperationException($"Unsupported underlying '{symbol}'. Supported: NIFTY, BANKNIFTY.");
            }

            return underlying;
        }

        private static string Mask(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= 4)
            {
                return "****";
            }

            return $"{value[..2]}****{value[^2..]}";
        }
    }
}
