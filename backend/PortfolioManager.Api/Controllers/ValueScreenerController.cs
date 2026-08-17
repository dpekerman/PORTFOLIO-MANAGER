using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ValueScreenerController(
    ValueScreenerService screener,
    ValueScreenerPersistenceService persistence,
    ILogger<ValueScreenerController> logger) : ControllerBase
{
    /// <summary>
    /// POST /api/valuescreener/analyze
    /// Runs a live analysis and persists the results to the database.
    /// Body: { "includePortfolio": true, "includeWatchlist": true, "adHocSymbols": ["AAPL"] }
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("analyze")]
    public async Task<ActionResult<List<ValueScreenerResult>>> Analyze(
        [FromBody] ValueScreenerRequest request,
        CancellationToken ct)
    {
        try
        {
            var results = await screener.RunAsync(request, ct);

            // Persist if this is a portfolio or watchlist run (not ad-hoc)
            if (request.IncludePortfolio && !request.IncludeWatchlist && request.AdHocSymbols.Count == 0)
                await persistence.SaveAsync("Portfolio", results, ct);
            else if (request.IncludeWatchlist && !request.IncludePortfolio && request.AdHocSymbols.Count == 0)
                await persistence.SaveAsync("Watchlist", results, ct);

            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Value screener analysis failed");
            return StatusCode(500, "Value screener failed. Check logs.");
        }
    }

    /// <summary>
    /// GET /api/valuescreener/latest
    /// Returns the latest persisted results for Portfolio and Watchlist without calling Yahoo Finance.
    /// </summary>
    [HttpGet("latest")]
    public async Task<ActionResult<ValueScreenerLatestDto>> GetLatest(CancellationToken ct)
    {
        try
        {
            var dto = await persistence.GetLatestAsync(ct);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Value screener: failed to load latest persisted results");
            return StatusCode(500, "Could not load latest results.");
        }
    }

    /// <summary>
    /// POST /api/valuescreener/refresh
    /// Re-runs the full screener for Portfolio and Watchlist and persists results.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("refresh")]
    public async Task<ActionResult<ValueScreenerLatestDto>> Refresh(CancellationToken ct)
    {
        try
        {
            var portfolioResults = await screener.RunAsync(
                new ValueScreenerRequest { IncludePortfolio = true, IncludeWatchlist = false }, ct);
            await persistence.SaveAsync("Portfolio", portfolioResults, ct);

            var watchlistResults = await screener.RunAsync(
                new ValueScreenerRequest { IncludePortfolio = false, IncludeWatchlist = true }, ct);
            await persistence.SaveAsync("Watchlist", watchlistResults, ct);

            return Ok(await persistence.GetLatestAsync(ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Value screener refresh failed");
            return StatusCode(500, "Refresh failed. Check logs.");
        }
    }

    /// <summary>
    /// GET /api/valuescreener/schedule
    /// Returns the current schedule configuration.
    /// </summary>
    [HttpGet("schedule")]
    public async Task<ActionResult<ValueScreenerScheduleConfig>> GetSchedule(CancellationToken ct)
    {
        var cfg = await persistence.GetOrCreateScheduleConfigAsync(ct);
        return Ok(cfg);
    }

    /// <summary>
    /// PUT /api/valuescreener/schedule
    /// Updates the scheduled run time and enabled flag.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("schedule")]
    public async Task<IActionResult> UpdateSchedule(
        [FromBody] ValueScreenerScheduleConfigDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.ScheduledTimeEt) ||
            !TimeSpan.TryParse(dto.ScheduledTimeEt, out _))
        {
            return BadRequest("ScheduledTimeEt must be a valid HH:mm string.");
        }

        await persistence.UpdateScheduleConfigAsync(dto.ScheduledTimeEt, dto.Enabled, ct);
        return NoContent();
    }
    /// <summary>
    /// DELETE /api/valuescreener/data?origin=Portfolio|Watchlist
    /// Clears all persisted snapshots (optionally filtered by origin).
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("data")]
    public async Task<IActionResult> ClearData([FromQuery] string? origin, CancellationToken ct)
    {
        try
        {
            await persistence.ClearAsync(origin, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Value screener: failed to clear data");
            return StatusCode(500, "Failed to clear data.");
        }
    }
}

public class ValueScreenerScheduleConfigDto
{
    public string ScheduledTimeEt { get; set; } = "17:00";
    public bool Enabled { get; set; } = true;
}