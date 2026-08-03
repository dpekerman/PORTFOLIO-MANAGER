using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WatchlistController(IWatchlistService watchlistService, IMarketDataProvider marketData) : ControllerBase
{
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

        return Ok(results);
    }

    /// <summary>Adds a symbol to the watchlist.</summary>
    [HttpPost]
    public async Task<ActionResult<WatchlistItemDto>> Add([FromBody] AddWatchlistItemRequest request, CancellationToken ct)
    {
        var item = await watchlistService.AddAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), item);
    }

    /// <summary>Removes a symbol from the watchlist.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await watchlistService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Updates the role for a watchlist item.</summary>
    [HttpPatch("{id:int}/role")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateWatchlistRoleRequest request, CancellationToken ct)
    {
        var updated = await watchlistService.UpdateRoleAsync(id, request.Role, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Toggles the favourite flag for a watchlist item.</summary>
    [HttpPatch("{id:int}/favorite")]
    public async Task<IActionResult> UpdateFavorite(int id, [FromBody] UpdateWatchlistFavoriteRequest request, CancellationToken ct)
    {
        var updated = await watchlistService.UpdateFavoriteAsync(id, request.IsFavorite, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Updates notes for a watchlist item.</summary>
    [HttpPatch("{id:int}/notes")]
    public async Task<IActionResult> UpdateNotes(int id, [FromBody] UpdateWatchlistNotesRequest request, CancellationToken ct)
    {
        var updated = await watchlistService.UpdateNotesAsync(id, request.Notes, ct);
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
    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreWatchlistRequest request, CancellationToken ct)
    {
        var count = await watchlistService.RestoreAsync(request.Items, ct);
        return Ok(new { restored = count });
    }
}
