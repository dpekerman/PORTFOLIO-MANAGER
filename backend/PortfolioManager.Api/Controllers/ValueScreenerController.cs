using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValueScreenerController(ValueScreenerService screener, ILogger<ValueScreenerController> logger) : ControllerBase
{
    /// <summary>
    /// POST /api/valuescreener/analyze
    /// Body: { "includePortfolio": true, "includeWatchlist": true, "adHocSymbols": ["AAPL"] }
    /// Returns scored, sorted list of ValueScreenerResult.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<List<ValueScreenerResult>>> Analyze(
        [FromBody] ValueScreenerRequest request,
        CancellationToken ct)
    {
        try
        {
            var results = await screener.RunAsync(request, ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Value screener analysis failed");
            return StatusCode(500, "Value screener failed. Check logs.");
        }
    }
}
