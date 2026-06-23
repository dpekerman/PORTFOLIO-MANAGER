using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

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
}
