using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/sector-industry")]
public class SectorIndustryController(SectorIndustryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SectorIndustryListsDto>> GetLists(CancellationToken ct)
        => Ok(await service.GetListsAsync(ct));

    [Authorize(Roles = "Admin")]
    [HttpPut]
    public async Task<IActionResult> SaveLists([FromBody] UpdateSectorIndustryListsRequest request, CancellationToken ct)
    {
        await service.SaveListsAsync(request, ct);
        return Ok(await service.GetListsAsync(ct));
    }

    // ── Dedicated Decision Sources endpoints ────────────────────────────────────

    [HttpGet("decision-sources")]
    public async Task<ActionResult<DecisionSourcesDto>> GetDecisionSources(CancellationToken ct)
        => Ok(await service.GetDecisionSourcesAsync(ct));

    [Authorize(Roles = "Admin")]
    [HttpPut("decision-sources")]
    public async Task<ActionResult<DecisionSourcesDto>> SaveDecisionSources(
        [FromBody] UpdateDecisionSourcesRequest request, CancellationToken ct)
        => Ok(await service.SaveDecisionSourcesAsync(request, ct));
}
