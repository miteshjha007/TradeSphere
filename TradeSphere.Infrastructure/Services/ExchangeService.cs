using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;
using TradeSphere.Domain.Entities;
using TradeSphere.Infrastructure.Persistence;
using TradeSphere.Infrastructure.Security;

namespace TradeSphere.Infrastructure.Services
{
    public class ExchangeService : IExchangeService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDeltaExchangeClient _deltaClient;
        private readonly ICoinDcxClient _coinDcxClient;
        private readonly IDhanClient _dhanClient;

        public ExchangeService(ApplicationDbContext context, IDeltaExchangeClient deltaClient, ICoinDcxClient coinDcxClient, IDhanClient dhanClient)
        {
            _context = context;
            _deltaClient = deltaClient;
            _coinDcxClient = coinDcxClient;
            _dhanClient = dhanClient;
        }

        public async Task<List<ExchangeDto>> GetSupportedExchangesAsync()
        {
            return await _context.Exchanges
                .Where(e => e.IsActive)
                .Select(e => new ExchangeDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    BaseUrl = e.BaseUrl
                })
                .ToListAsync();
        }

        public async Task<List<UserExchangeDto>> GetUserExchangesAsync(int userId)
        {
            var exchanges = await _context.UserExchanges
                .Include(ue => ue.Exchange)
                .Where(ue => ue.UserId == userId)
                .ToListAsync();

            return exchanges.Select(ue => new UserExchangeDto
            {
                Id = ue.Id,
                ExchangeName = ue.Exchange.Name,
                Name = ue.Name,
                Status = ue.Status,
                ApiKeyPreview = GetMaskedKey(ue.ApiKey) // Need to decrypt strictly if we stored encrypted, but for preview we might just show stars or decrypt then mask
            }).ToList();
        }

        public async Task<UserExchangeDto> ConnectExchangeAsync(int userId, ConnectExchangeDto dto)
        {
            var encryptedApiKey = EncryptionHelper.Encrypt(dto.ApiKey);
            var encryptedApiSecret = EncryptionHelper.Encrypt(dto.ApiSecret);

            var userExchange = new UserExchange
            {
                UserId = userId,
                ExchangeId = dto.ExchangeId,
                Name = dto.Name,
                ApiKey = encryptedApiKey,
                ApiSecret = encryptedApiSecret,
                Status = "Active" // logic to validate key could go here
            };

            _context.UserExchanges.Add(userExchange);
            await _context.SaveChangesAsync();

            // Reload to get Exchange Name
            await _context.Entry(userExchange).Reference(ue => ue.Exchange).LoadAsync();

            return new UserExchangeDto
            {
                Id = userExchange.Id,
                ExchangeName = userExchange.Exchange.Name,
                Name = userExchange.Name,
                Status = userExchange.Status,
                ApiKeyPreview = MaskKey(dto.ApiKey)
            };
        }

        public async Task DeleteUserExchangeAsync(int userId, int userExchangeId)
        {
            var entity = await _context.UserExchanges
                .FirstOrDefaultAsync(ue => ue.Id == userExchangeId && ue.UserId == userId);

            if (entity != null)
            {
                _context.UserExchanges.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<ConnectionTestResult> TestConnectionAsync(int userId, int userExchangeId)
        {
            var userExchange = await _context.UserExchanges
                .Include(ue => ue.Exchange)
                .FirstOrDefaultAsync(ue => ue.Id == userExchangeId && ue.UserId == userId);

            if (userExchange == null)
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Exchange connection not found."
                };
            }

            try
            {
                var apiKey = EncryptionHelper.Decrypt(userExchange.ApiKey);
                var apiSecret = EncryptionHelper.Decrypt(userExchange.ApiSecret);

                var result = IsDhan(userExchange.Exchange?.Name)
                    ? MapDhanResult(await _dhanClient.TestConnectionAsync(apiKey, apiSecret))
                    : IsCoinDcx(userExchange.Exchange?.Name)
                        ? await _coinDcxClient.TestConnectionAsync(apiKey, apiSecret, userExchange.Exchange?.BaseUrl)
                        : await _deltaClient.TestConnectionAsync(apiKey, apiSecret, userExchange.Exchange?.BaseUrl);
                return result;
            }
            catch (System.Exception ex)
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}"
                };
            }
        }

        private string GetMaskedKey(string encryptedKey)
        {
            try 
            {
                var decrypted = EncryptionHelper.Decrypt(encryptedKey);
                return MaskKey(decrypted);
            }
            catch
            {
                return "****";
            }
        }

        private string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length <= 4) return "****";
            return key.Substring(0, 4) + "****" + key.Substring(key.Length - 4);
        }

        private static bool IsCoinDcx(string? exchangeName)
        {
            return exchangeName?.Contains("CoinDCX", System.StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsDhan(string? exchangeName)
        {
            return exchangeName?.Equals("Dhan", System.StringComparison.OrdinalIgnoreCase) == true;
        }

        private static ConnectionTestResult MapDhanResult(DhanConnectionTestResultDto result)
        {
            return new ConnectionTestResult
            {
                Success = result.Success,
                Message = result.Message,
                WalletBalance = result.AvailableBalance,
                Currency = result.Currency
            };
        }
    }
}
