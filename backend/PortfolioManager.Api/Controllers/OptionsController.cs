using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

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

    [HttpPost]
    public async Task<ActionResult<OptionItemDto>> Add([FromBody] AddOptionItemRequest request, CancellationToken ct)
    {
        var item = await optionService.AddAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OptionItemDto>> Update(int id, [FromBody] UpdateOptionItemRequest request, CancellationToken ct)
    {
        var item = await optionService.UpdateAsync(id, request, ct);
        return item is null ? NotFound() : Ok(item);
    }

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
}
