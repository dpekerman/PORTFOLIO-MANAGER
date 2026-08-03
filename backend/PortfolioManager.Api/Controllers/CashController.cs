using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
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

    /// <summary>Exports all cash items as a JSON backup payload.</summary>
    [HttpGet("backup")]
    public async Task<ActionResult<IReadOnlyList<CashBackupItem>>> Backup(CancellationToken ct)
    {
        var items = await cashService.BackupAsync(ct);
        return Ok(items);
    }

    /// <summary>Clears all cash items and restores from the provided backup payload.</summary>
    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreCashRequest request, CancellationToken ct)
    {
        var count = await cashService.RestoreAsync(request.Items, ct);
        return Ok(new { restored = count });
    }
}
