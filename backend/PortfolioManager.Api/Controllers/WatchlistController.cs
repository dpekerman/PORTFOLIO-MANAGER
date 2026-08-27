using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using System.Security.Claims;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WatchlistController(
    IWatchlistService watchlistService,
    IMarketDataProvider marketData,
    IWatchlistSnapshotService watchlistSnapshot,
    IDashboardService dashboard) : ControllerBase
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Returns the latest persisted watchlist snapshot from DB — no Yahoo Finance call. 204 when no snapshot exists yet.</summary>
    [HttpGet("snapshot")]
    public async Task<ActionResult<IReadOnlyList<WatchlistSummaryDto>>> GetWatchlistSnapshot(CancellationToken ct)
    {
        var uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid)) return Unauthorized();
        var snapshot = await watchlistSnapshot.GetLatestAsync(uid, ct);
        if (snapshot is null) return NoContent();
        return Ok(snapshot);
    }

    /// <summary>Gets all watchlist items with live quotes.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WatchlistSummaryDto>>> GetAll(CancellationToken ct)
    {
        var items = await watchlistService.GetAllAsync(ct);
        if (items.Count == 0) return Ok(Array.Empty<WatchlistSummaryDto>());

        var quotes = await marketData.GetBatchQuotesAsync(items.Select(i => i.Symbol), ct);

        var results = items.Select(item =>
        {
            quotes.TryGetValue(item.Symbol, out var quote);
            return new WatchlistSummaryDto(item, quote);
        }).ToList();

        // Persist snapshot so the frontend loads instantly on next page open
        var uid = CurrentUserId();
        if (!string.IsNullOrEmpty(uid))
        {
            await watchlistSnapshot.SaveAsync(uid, results.AsReadOnly(), ct);
            await dashboard.RebuildAsync(uid, ct);
        }

        return Ok(results);
    }

    /// <summary>Adds a symbol to the watchlist.</summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpPost]
    public async Task<ActionResult<WatchlistItemDto>> Add([FromBody] AddWatchlistItemRequest request, CancellationToken ct)
    {
        var item = await watchlistService.AddAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), item);
    }

    /// <summary>Removes a symbol from the watchlist.</summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await watchlistService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Updates the role for a watchlist item.</summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpPatch("{id:int}/role")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateWatchlistRoleRequest request, CancellationToken ct)
    {
        var updated = await watchlistService.UpdateRoleAsync(id, request.Role, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Updates the monitoring tier for a watchlist item.</summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpPatch("{id:int}/tier")]
    public async Task<IActionResult> UpdateTier(int id, [FromBody] UpdateWatchlistTierRequest request, CancellationToken ct)
    {
        var updated = await watchlistService.UpdateTierAsync(id, request.WatchlistTier, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Toggles the favourite flag for a watchlist item.</summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpPatch("{id:int}/favorite")]
    public async Task<IActionResult> UpdateFavorite(int id, [FromBody] UpdateWatchlistFavoriteRequest request, CancellationToken ct)
    {
        var updated = await watchlistService.UpdateFavoriteAsync(id, request.IsFavorite, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Updates notes for a watchlist item.</summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpPatch("{id:int}/notes")]
    public async Task<IActionResult> UpdateNotes(int id, [FromBody] UpdateWatchlistNotesRequest request, CancellationToken ct)
    {
        var updated = await watchlistService.UpdateNotesAsync(id, request.Notes, ct);
        return updated ? NoContent() : NotFound();
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpPost("refresh-earnings")]
    public async Task<IActionResult> RefreshEarnings(CancellationToken ct)
    {
        var items = await watchlistService.GetAllAsync(ct);
        if (items.Count == 0) return Ok(new { refreshed = 0 });
        var earningsDates = await marketData.GetEarningsDatesAsync(items.Select(i => i.Symbol), ct);
        var count = 0;
        foreach (var (symbol, date) in earningsDates)
        {
            var item = items.FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (item is null) continue;
            await watchlistService.UpdateEarningsDateAsync(item.Id, date, ct);
            count++;
        }
        return Ok(new { refreshed = count, total = items.Count });
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpPatch("{id:int}/earnings-date")]
    public async Task<IActionResult> UpdateEarningsDate(int id, [FromBody] UpdateWatchlistEarningsDateRequest request, CancellationToken ct)
    {
        var updated = await watchlistService.UpdateEarningsDateAsync(id, request.EarningsDate, ct);
        if (updated)
        {
            var uid = CurrentUserId();
            if (!string.IsNullOrEmpty(uid)) await dashboard.RebuildAsync(uid, ct);
        }
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Exports all watchlist items as a JSON backup payload.</summary>
    [HttpGet("backup")]
    public async Task<ActionResult<IReadOnlyList<WatchlistBackupItem>>> Backup(CancellationToken ct)
    {
        var items = await watchlistService.BackupAsync(ct);
        return Ok(items);
    }

    /// <summary>Clears the watchlist and restores from the provided backup payload.</summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreWatchlistRequest request, CancellationToken ct)
    {
        var count = await watchlistService.RestoreAsync(request.Items, ct);
        return Ok(new { restored = count });
    }
}