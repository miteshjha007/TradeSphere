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
    public class StrategyController : ControllerBase
    {
        private readonly IStrategyService _strategyService;

        public StrategyController(IStrategyService strategyService)
        {
            _strategyService = strategyService;
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableStrategies()
        {
            return Ok(await _strategyService.GetAvailableStrategiesAsync());
        }

        [HttpGet("my-strategies")]
        public async Task<IActionResult> GetUserStrategies()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            return Ok(await _strategyService.GetUserStrategiesAsync(userId));
        }

        [HttpPost("deploy")]
        public async Task<IActionResult> DeployStrategy([FromBody] DeployStrategyDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var result = await _strategyService.DeployStrategyAsync(userId, dto);
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateStrategy([FromBody] CreateStrategyDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var result = await _strategyService.CreateStrategyAsync(userId, dto);
            return Ok(result);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ToggleStatus(int id, [FromBody] string status)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                await _strategyService.ToggleStrategyStatusAsync(userId, id, status);
                return NoContent();
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStrategy(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                await _strategyService.DeleteUserStrategyAsync(userId, id);
                return NoContent();
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
