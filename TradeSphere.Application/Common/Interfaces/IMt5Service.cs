using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IMt5Service
    {
        Task<IReadOnlyList<Mt5AccountDto>> GetAccountsAsync(int userId);
        Task<Mt5AccountDto> ConnectAccountAsync(int userId, ConnectMt5AccountDto dto);
        Task DeleteAccountAsync(int userId, int id);
        Task<Mt5ConnectionTestResultDto> TestConnectionAsync(int userId, int id);
        Task<IReadOnlyList<Mt5SymbolMappingDto>> GetSymbolMappingsAsync(int userId);
        Task<Mt5SymbolMappingDto> UpsertSymbolMappingAsync(int userId, UpsertMt5SymbolMappingDto dto);
        Task DeleteSymbolMappingAsync(int userId, int id);
    }
}
