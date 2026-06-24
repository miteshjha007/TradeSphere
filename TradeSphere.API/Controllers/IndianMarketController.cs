using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/indian-market")]
    public class IndianMarketController : ControllerBase
    {
        private readonly IIndianMarketService _indianMarketService;

        public IndianMarketController(IIndianMarketService indianMarketService)
        {
            _indianMarketService = indianMarketService;
        }

        [HttpGet("underlyings")]
        public IActionResult GetUnderlyings()
        {
            return Ok(_indianMarketService.GetSupportedUnderlyings());
        }

        [HttpGet("dhan/accounts")]
        public async Task<IActionResult> GetDhanAccounts()
        {
            return Ok(await _indianMarketService.GetDhanAccountsAsync(GetUserId()));
        }

        [HttpPost("dhan/accounts")]
        public async Task<IActionResult> ConnectDhanAccount([FromBody] ConnectDhanAccountDto dto)
        {
            try
            {
                return Ok(await _indianMarketService.ConnectDhanAccountAsync(GetUserId(), dto));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("dhan/accounts/{id:int}")]
        public async Task<IActionResult> DeleteDhanAccount(int id)
        {
            await _indianMarketService.DeleteDhanAccountAsync(GetUserId(), id);
            return NoContent();
        }

        [HttpPost("dhan/accounts/{id:int}/test-connection")]
        public async Task<IActionResult> TestDhanConnection(int id)
        {
            var result = await _indianMarketService.TestDhanConnectionAsync(GetUserId(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("options/expiries")]
        public async Task<IActionResult> GetExpiries([FromBody] OptionExpiryRequestDto dto)
        {
            try
            {
                return Ok(await _indianMarketService.GetExpiriesAsync(GetUserId(), dto));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("options/chain")]
        public async Task<IActionResult> GetOptionChain([FromBody] OptionChainRequestDto dto)
        {
            try
            {
                return Ok(await _indianMarketService.GetOptionChainAsync(GetUserId(), dto));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("options/orders")]
        public async Task<IActionResult> PlaceOptionOrder([FromBody] DhanOptionOrderRequestDto dto)
        {
            try
            {
                var result = await _indianMarketService.PlaceOptionOrderAsync(GetUserId(), dto);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        }
    }
}
