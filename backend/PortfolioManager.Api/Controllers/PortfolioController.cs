using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController(IPortfolioService portfolioService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortfolioItemDto>>> GetAll(CancellationToken ct)
    {
        var items = await portfolioService.GetAllAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PortfolioItemDto>> GetById(int id, CancellationToken ct)
    {
        var item = await portfolioService.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<PortfolioItemDto>> Add([FromBody] AddPortfolioItemRequest request, CancellationToken ct)
    {
        var item = await portfolioService.AddAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    /// <summary>
    /// Adds a manual (non-ticker) position such as Cash, Options, Bonds, etc.
    /// No Yahoo Finance call is made. Name → Sector, Description → Industry.
    /// </summary>
    [HttpPost("manual")]
    public async Task<ActionResult<PortfolioItemDto>> AddManual([FromBody] AddManualPositionRequest request, CancellationToken ct)
    {
        var item = await portfolioService.AddManualAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PortfolioItemDto>> Update(int id, [FromBody] UpdatePortfolioItemRequest request, CancellationToken ct)
    {
        var item = await portfolioService.UpdateAsync(id, request, ct);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Updates the holding role for a portfolio item.</summary>
    [HttpPatch("{id:int}/holding-role")]
    public async Task<IActionResult> UpdateHoldingRole(int id, [FromBody] UpdatePortfolioHoldingRoleRequest request, CancellationToken ct)
    {
        var updated = await portfolioService.UpdateHoldingRoleAsync(id, request.HoldingRole, ct);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await portfolioService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Fetches sector/industry from Yahoo Finance quoteSummary for every portfolio item and
    /// persists the results. Returns the number of items that were successfully classified.
    /// </summary>
    [HttpPost("refresh-sectors")]
    public async Task<ActionResult<object>> RefreshSectors(CancellationToken ct)
    {
        var updated = await portfolioService.RefreshSectorsAsync(ct);
        return Ok(new { updated });
    }
}
