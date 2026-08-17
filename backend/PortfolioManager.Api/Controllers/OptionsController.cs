using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OptionsController(IOptionService optionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OptionItemDto>>> GetAll(CancellationToken ct)
    {
        var items = await optionService.GetAllAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OptionItemDto>> GetById(int id, CancellationToken ct)
    {
        var item = await optionService.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpPost]
    public async Task<ActionResult<OptionItemDto>> Add([FromBody] AddOptionItemRequest request, CancellationToken ct)
    {
        var item = await optionService.AddAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<OptionItemDto>> Update(int id, [FromBody] UpdateOptionItemRequest request, CancellationToken ct)
    {
        var item = await optionService.UpdateAsync(id, request, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpPatch("{id:int}/notes")]
    public async Task<IActionResult> UpdateNotes(int id, [FromBody] UpdateOptionNotesRequest request, CancellationToken ct)
    {
        var updated = await optionService.UpdateNotesAsync(id, request.Notes, ct);
        return updated ? NoContent() : NotFound();
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await optionService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Returns technical indicators for the underlying ticker used by the option state rules engine.</summary>
    [HttpGet("technical/{symbol}")]
    public async Task<ActionResult<OptionTechnicalDataDto>> GetTechnical(string symbol, CancellationToken ct)
    {
        var data = await optionService.GetTechnicalDataAsync(symbol, ct);
        return data is null ? NotFound() : Ok(data);
    }

    /// <summary>Exports all option items as a JSON backup payload.</summary>
    [HttpGet("backup")]
    public async Task<ActionResult<IReadOnlyList<OptionBackupItem>>> Backup(CancellationToken ct)
    {
        var items = await optionService.BackupAsync(ct);
        return Ok(items);
    }

    /// <summary>Clears all option items and restores from the provided backup payload.</summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreOptionsRequest request, CancellationToken ct)
    {
        var count = await optionService.RestoreAsync(request.Items, ct);
        return Ok(new { restored = count });
    }
}