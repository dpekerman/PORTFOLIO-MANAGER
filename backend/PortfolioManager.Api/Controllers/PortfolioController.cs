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

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PortfolioItemDto>> Update(int id, [FromBody] UpdatePortfolioItemRequest request, CancellationToken ct)
    {
        var item = await portfolioService.UpdateAsync(id, request, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await portfolioService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
