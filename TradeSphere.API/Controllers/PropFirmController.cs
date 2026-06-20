using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PropFirmController : ControllerBase
    {
        private readonly IPropFirmService _propFirmService;

        public PropFirmController(IPropFirmService propFirmService)
        {
            _propFirmService = propFirmService;
        }

        [HttpGet("firms")]
        public async Task<IActionResult> GetFirms()
        {
            var userId = GetUserId();
            return Ok(await _propFirmService.GetFirmsAsync(userId));
        }

        [HttpPost("firms")]
        public async Task<IActionResult> CreateFirm([FromBody] CreatePropFirmDto dto)
        {
            try
            {
                var userId = GetUserId();
                return Ok(await _propFirmService.CreateFirmAsync(userId, dto));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("firms/{id}")]
        public async Task<IActionResult> DeleteFirm(int id)
        {
            try
            {
                var userId = GetUserId();
                await _propFirmService.DeleteFirmAsync(userId, id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts()
        {
            var userId = GetUserId();
            return Ok(await _propFirmService.GetAccountsAsync(userId));
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> CreateAccount([FromBody] CreatePropFirmAccountDto dto)
        {
            try
            {
                var userId = GetUserId();
                return Ok(await _propFirmService.CreateAccountAsync(userId, dto));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("accounts/{id}")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            try
            {
                var userId = GetUserId();
                await _propFirmService.DeleteAccountAsync(userId, id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        }
    }
}
