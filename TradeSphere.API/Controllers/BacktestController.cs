using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BacktestController : ControllerBase
    {
        private readonly IBacktestService _backtestService;

        public BacktestController(IBacktestService backtestService)
        {
            _backtestService = backtestService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyBacktests()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            return Ok(await _backtestService.GetUserBacktestsAsync(userId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBacktestDetails(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var result = await _backtestService.GetBacktestDetailsAsync(userId, id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("run")]
        public async Task<IActionResult> RunBacktest([FromBody] RunBacktestDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var result = await _backtestService.RunBacktestAsync(userId, dto);
            return Ok(result);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAllBacktests()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            await _backtestService.DeleteAllBacktestsAsync(userId);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBacktest(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            await _backtestService.DeleteBacktestAsync(userId, id);
            return NoContent();
        }
    }
}
