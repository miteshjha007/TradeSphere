using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;

namespace TradeSphere.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TradingController : ControllerBase
    {
        private readonly ITradingService _tradingService;

        public TradingController(ITradingService tradingService)
        {
            _tradingService = tradingService;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            return Ok(await _tradingService.GetOverviewAsync(userId));
        }

        [HttpGet("trades")]
        public async Task<IActionResult> GetTrades()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            return Ok(await _tradingService.GetTradesAsync(userId));
        }

        [HttpGet("positions")]
        public async Task<IActionResult> GetPositions()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            return Ok(await _tradingService.GetPositionsAsync(userId));
        }

        [HttpDelete("trades")]
        public async Task<IActionResult> DeleteAllTrades()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            await _tradingService.DeleteAllTradesAsync(userId);
            return NoContent();
        }
    }
}
