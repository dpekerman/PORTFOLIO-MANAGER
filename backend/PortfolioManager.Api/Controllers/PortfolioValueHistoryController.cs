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
    [Authorize(Roles = "Admin")]
    [HttpPost("record-now")]
    public async Task<ActionResult<PortfolioValueHistoryDto>> RecordNow(CancellationToken ct)
    {
        var dto = await historyService.RecordCurrentValueAsync(ct);
        return Ok(dto);
    }

    /// <summary>
    /// Returns the list of weekday dates within the past lookbackDays that have no snapshot,
    /// without modifying the database.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("missing-days")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetMissingDays(
        [FromQuery] int lookbackDays = 30, CancellationToken ct = default)
    {
        var missing = await historyService.GetMissingDatesAsync(Math.Clamp(lookbackDays, 1, 365), ct);
        return Ok(missing);
    }

    /// <summary>
    /// Backfills any missing weekday snapshots within the past <paramref name="lookbackDays"/> days
    /// by fetching historical closing prices from Yahoo Finance.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("backfill")]
    public async Task<ActionResult<IReadOnlyList<PortfolioValueHistoryDto>>> Backfill(
        [FromQuery] int lookbackDays = 14, CancellationToken ct = default)
    {
        var filled = await historyService.BackfillMissingAsync(Math.Clamp(lookbackDays, 1, 90), ct);
        return Ok(filled);
    }
}