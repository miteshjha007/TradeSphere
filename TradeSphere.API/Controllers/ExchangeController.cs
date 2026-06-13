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
    public class ExchangeController : ControllerBase
    {
        private readonly IExchangeService _exchangeService;

        public ExchangeController(IExchangeService exchangeService)
        {
            _exchangeService = exchangeService;
        }

        [HttpGet("supported")]
        public async Task<IActionResult> GetSupportedExchanges()
        {
            return Ok(await _exchangeService.GetSupportedExchangesAsync());
        }

        [HttpGet]
        public async Task<IActionResult> GetUserExchanges()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            return Ok(await _exchangeService.GetUserExchangesAsync(userId));
        }

        [HttpPost]
        public async Task<IActionResult> ConnectExchange([FromBody] ConnectExchangeDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var result = await _exchangeService.ConnectExchangeAsync(userId, dto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExchange(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            await _exchangeService.DeleteUserExchangeAsync(userId, id);
            return NoContent();
        }
    }
}
