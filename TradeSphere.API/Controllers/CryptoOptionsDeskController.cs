using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/crypto-options")]
    public class CryptoOptionsDeskController : ControllerBase
    {
        private readonly ICryptoOptionsService _service;
        private readonly ICryptoOptionsBacktestService _backtester;
        private readonly IOptionScanner _scanner;

        public CryptoOptionsDeskController(ICryptoOptionsService service, ICryptoOptionsBacktestService backtester, IOptionScanner scanner)
        {
            _service = service;
            _backtester = backtester;
            _scanner = scanner;
        }

        [HttpGet("configs")]
        public async Task<IActionResult> GetConfigs() => Ok(await _service.GetConfigsAsync(GetUserId()));

        [HttpPost("configs")]
        public async Task<IActionResult> CreateConfig([FromBody] UpsertCryptoOptionStrategyConfigDto dto)
        {
            try { return Ok(await _service.CreateConfigAsync(GetUserId(), dto)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("configs/{id}")]
        public async Task<IActionResult> UpdateConfig(int id, [FromBody] UpsertCryptoOptionStrategyConfigDto dto)
        {
            try { return Ok(await _service.UpdateConfigAsync(GetUserId(), id, dto)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("configs/{id}")]
        public async Task<IActionResult> DeleteConfig(int id)
        {
            try { await _service.DeleteConfigAsync(GetUserId(), id); return NoContent(); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("chain-snapshots")]
        public async Task<IActionResult> GetChainSnapshots([FromQuery] string? exchange, [FromQuery] string? symbol, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
            => Ok(await _service.GetChainSnapshotsAsync(exchange, symbol, from, to));

        [HttpPost("chain-snapshots/import")]
        public async Task<IActionResult> ImportChainSnapshots([FromBody] List<ImportCryptoOptionChainSnapshotDto> snapshots)
        {
            try { return Ok(new { imported = await _service.ImportChainSnapshotsAsync(snapshots) }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }


        [HttpGet("delta/expiries")]
        public async Task<IActionResult> GetDeltaExpiries([FromQuery] string? exchange, [FromQuery] string? underlying, [FromQuery] string? symbol)
        {
            try { return Ok(await _service.GetDeltaExpiriesAsync(exchange, underlying, symbol)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("delta/chain")]
        public async Task<IActionResult> FetchDeltaChain([FromBody] FetchCryptoOptionChainRequestDto dto)
        {
            try { return Ok(await _service.FetchDeltaChainAsync(dto)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }
        [HttpGet("scanner")]
        public async Task<IActionResult> Scan([FromQuery] string exchange, [FromQuery] string symbol, [FromQuery] string scannerMode = "PremiumBased")
            => Ok(await _scanner.ScanAsync(GetUserId(), exchange, symbol, scannerMode));

        [HttpPost("backtest/run")]
        public async Task<IActionResult> RunBacktest([FromBody] CryptoOptionBacktestRequestDto dto)
        {
            try
            {
                var result = await _backtester.RunAsync(GetUserId(), dto);
                return result.Status == "Failed" ? BadRequest(result) : Ok(result);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("backtest/runs")]
        public async Task<IActionResult> GetRuns() => Ok(await _service.GetBacktestRunsAsync(GetUserId()));

        [HttpGet("backtest/runs/{id}")]
        public async Task<IActionResult> GetRun(int id)
        {
            try { return Ok(await _service.GetBacktestRunAsync(GetUserId(), id)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("backtest/runs/{id}/positions")]
        public async Task<IActionResult> GetPositions(int id)
        {
            try { return Ok(await _service.GetBacktestPositionsAsync(GetUserId(), id)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("backtest/runs/{id}/trades")]
        public async Task<IActionResult> GetTrades(int id)
        {
            try { return Ok(await _service.GetBacktestLegsAsync(GetUserId(), id)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("backtest/runs/{id}/daily-pnl")]
        public async Task<IActionResult> GetDailyPnl(int id)
        {
            try { return Ok(await _service.GetDailyPnlAsync(GetUserId(), id)); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("reports/risk")]
        public async Task<IActionResult> GetRiskReport() => Ok(await _service.GetRiskReportAsync(GetUserId()));

        private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }
}

