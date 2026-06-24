using TradeSphere.Application.DTOs;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IIpoService
    {
        Task<IpoDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    }
}
