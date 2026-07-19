using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

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
}
