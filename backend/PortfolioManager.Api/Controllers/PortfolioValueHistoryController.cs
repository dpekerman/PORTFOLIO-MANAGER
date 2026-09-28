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

    /// <summary>Returns snapshots within [fromDate..toDate] (most-recent-first), enriched with
    /// ExternalCashFlow/SnapshotStatus/HasMismatch. Defaults to the last 90 days when omitted.</summary>
    [HttpGet("range")]
    public async Task<ActionResult<IReadOnlyList<PortfolioValueHistoryDto>>> GetRange(
        [FromQuery] DateOnly? fromDate = null, [FromQuery] DateOnly? toDate = null, CancellationToken ct = default)
    {
        var items = await historyService.GetRangeAsync(fromDate, toDate, ct);
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
        var dto = await historyService.RecordCurrentValueAsync(ct, PortfolioValueSource.ManualRecordNow);
        return Ok(dto);
    }

    /// <summary>
    /// Admin/debug: cash-only recalculation of today's already-recorded snapshot (preserves Stocks/
    /// Options). Fails with 400 if no snapshot exists yet for today — use Record Today Now first.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("reconcile-today")]
    public async Task<ActionResult<PortfolioValueHistoryDto>> ReconcileToday(CancellationToken ct)
    {
        var todayEt = DateOnly.FromDateTime(NowEt());
        var todayStr = todayEt.ToString("yyyy-MM-dd");

        if (!await historyService.ExistsForDateAsync(todayStr, ct))
            return BadRequest(new { message = "No snapshot exists for today. Use Record Today Now first." });

        await historyService.RecalculateCashRangeAsync(todayEt, ct);
        var items = await historyService.GetRangeAsync(todayEt, todayEt, ct);
        return Ok(items.FirstOrDefault());
    }

    /// <summary>
    /// Admin/debug: cash-only recalculation of PortfolioValueHistories from <paramref name="fromDate"/>
    /// through today (preserves Stocks/Options on every affected row).
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("recalculate-cash")]
    public async Task<ActionResult<IReadOnlyList<PortfolioValueHistoryDto>>> RecalculateCash(
        [FromQuery] DateOnly fromDate, CancellationToken ct)
    {
        var todayEt = DateOnly.FromDateTime(NowEt());
        if (fromDate > todayEt)
            return BadRequest(new { message = "fromDate cannot be in the future." });

        await historyService.RecalculateCashRangeAsync(fromDate, ct);
        var items = await historyService.GetRangeAsync(fromDate, todayEt, ct);
        return Ok(items);
    }

    private static DateTime NowEt()
    {
        foreach (var id in new[] { "Eastern Standard Time", "America/New_York" })
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(id)); }
            catch { /* try next */ }
        }
        return DateTime.UtcNow;
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