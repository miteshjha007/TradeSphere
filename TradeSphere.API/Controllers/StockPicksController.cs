using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeSphere.Application.Common.Interfaces;
using TradeSphere.Application.DTOs;

namespace TradeSphere.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/indian-market/stock-picks")]
    public class StockPicksController : ControllerBase
    {
        private readonly IStockPickService _stockPickService;

        public StockPicksController(IStockPickService stockPickService)
        {
            _stockPickService = stockPickService;
        }

        [HttpGet("intraday")]
        public async Task<IActionResult> GetIntradayPicks(CancellationToken cancellationToken)
        {
            return Ok(await _stockPickService.GetIntradayPicksAsync(cancellationToken));
        }

        [HttpGet("long-term")]
        public async Task<IActionResult> GetLongTermPicks(CancellationToken cancellationToken)
        {
            return Ok(await _stockPickService.GetLongTermPicksAsync(cancellationToken));
        }
        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeStock([FromBody] StockAnalysisRequestDto request, CancellationToken cancellationToken)
        {
            return Ok(await _stockPickService.AnalyzeStockAsync(request, cancellationToken));
        }
    }
}


