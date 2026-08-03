using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PortfolioValueHistoryController(IPortfolioValueHistoryService historyService) : ControllerBase
{
    [HttpGet("latest")]
    public async Task<ActionResult<IReadOnlyList<PortfolioValueHistoryDto>>> GetLatest(
        [FromQuery] int count = 30, CancellationToken ct = default)
    {
        var items = await historyService.GetLatestAsync(Math.Clamp(count, 1, 365), ct);
        return Ok(items);
    }

    /// <summary>
    /// Immediately records the current portfolio value for today's date.
    /// If a record already exists for today it is replaced.
    /// Use this to seed historical data when the background service has not yet fired.
    /// </summary>
    [HttpPost("record-now")]
    public async Task<ActionResult<PortfolioValueHistoryDto>> RecordNow(CancellationToken ct)
    {
        var dto = await historyService.RecordCurrentValueAsync(ct);
        return Ok(dto);
    }
}
