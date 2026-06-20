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
    public class Mt5Controller : ControllerBase
    {
        private readonly IMt5Service _mt5Service;
        private readonly IMt5BridgeClient _bridgeClient;

        public Mt5Controller(IMt5Service mt5Service, IMt5BridgeClient bridgeClient)
        {
            _mt5Service = mt5Service;
            _bridgeClient = bridgeClient;
        }

        [HttpGet("bridge/health")]
        public async Task<IActionResult> GetBridgeHealth()
        {
            var result = await _bridgeClient.HealthAsync();
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts()
        {
            var userId = GetUserId();
            return Ok(await _mt5Service.GetAccountsAsync(userId));
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> ConnectAccount([FromBody] ConnectMt5AccountDto dto)
        {
            try
            {
                var userId = GetUserId();
                return Ok(await _mt5Service.ConnectAccountAsync(userId, dto));
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
                await _mt5Service.DeleteAccountAsync(userId, id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("accounts/{id}/test-connection")]
        public async Task<IActionResult> TestConnection(int id)
        {
            try
            {
                var userId = GetUserId();
                var result = await _mt5Service.TestConnectionAsync(userId, id);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message, status = "Error" });
            }
        }

        [HttpGet("symbol-mappings")]
        public async Task<IActionResult> GetSymbolMappings()
        {
            var userId = GetUserId();
            return Ok(await _mt5Service.GetSymbolMappingsAsync(userId));
        }

        [HttpPost("symbol-mappings")]
        public async Task<IActionResult> UpsertSymbolMapping([FromBody] UpsertMt5SymbolMappingDto dto)
        {
            try
            {
                var userId = GetUserId();
                return Ok(await _mt5Service.UpsertSymbolMappingAsync(userId, dto));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("symbol-mappings/{id}")]
        public async Task<IActionResult> DeleteSymbolMapping(int id)
        {
            try
            {
                var userId = GetUserId();
                await _mt5Service.DeleteSymbolMappingAsync(userId, id);
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
