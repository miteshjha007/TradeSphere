using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeSphere.Application.Common.Interfaces;

namespace TradeSphere.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/ipo")]
    public class IpoController : ControllerBase
    {
        private readonly IIpoService _ipoService;

        public IpoController(IIpoService ipoService)
        {
            _ipoService = ipoService;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        {
            return Ok(await _ipoService.GetDashboardAsync(cancellationToken));
        }
    }
}
