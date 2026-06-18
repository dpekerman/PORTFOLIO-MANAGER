using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CashController(ICashService cashService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CashItemDto>>> GetAll(CancellationToken ct)
    {
        var items = await cashService.GetAllAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CashItemDto>> GetById(int id, CancellationToken ct)
    {
        var item = await cashService.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CashItemDto>> Add([FromBody] AddCashItemRequest request, CancellationToken ct)
    {
        var item = await cashService.AddAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CashItemDto>> Update(int id, [FromBody] UpdateCashItemRequest request, CancellationToken ct)
    {
        var item = await cashService.UpdateAsync(id, request, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await cashService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
