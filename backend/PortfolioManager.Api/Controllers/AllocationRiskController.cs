using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/allocation-risk")]
public class AllocationRiskController(IAllocationRiskService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AllocationRiskConfigDto>> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    // Ã¢â€â‚¬Ã¢â€â‚¬ Risk Targets (by Role) Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    [Authorize(Roles = "Admin")]
    [HttpPost("risk-targets")]
    public async Task<ActionResult<AllocationRiskTargetDto>> AddRiskTarget([FromBody] UpsertAllocationRiskTargetRequest request, CancellationToken ct)
        => Ok(await service.UpsertRiskTargetAsync(null, request, ct));

    [Authorize(Roles = "Admin")]
    [HttpPut("risk-targets/{id:int}")]
    public async Task<ActionResult<AllocationRiskTargetDto>> UpdateRiskTarget(int id, [FromBody] UpsertAllocationRiskTargetRequest request, CancellationToken ct)
        => Ok(await service.UpsertRiskTargetAsync(id, request, ct));

    [Authorize(Roles = "Admin")]
    [HttpDelete("risk-targets/{id:int}")]
    public async Task<IActionResult> DeleteRiskTarget(int id, CancellationToken ct)
        => await service.DeleteRiskTargetAsync(id, ct) ? NoContent() : NotFound();

    // Ã¢â€â‚¬Ã¢â€â‚¬ Sector Targets Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    [Authorize(Roles = "Admin")]
    [HttpPost("sector-targets")]
    public async Task<ActionResult<AllocationSectorTargetDto>> AddSectorTarget([FromBody] UpsertAllocationSectorTargetRequest request, CancellationToken ct)
        => Ok(await service.UpsertSectorTargetAsync(null, request, ct));

    [Authorize(Roles = "Admin")]
    [HttpPut("sector-targets/{id:int}")]
    public async Task<ActionResult<AllocationSectorTargetDto>> UpdateSectorTarget(int id, [FromBody] UpsertAllocationSectorTargetRequest request, CancellationToken ct)
        => Ok(await service.UpsertSectorTargetAsync(id, request, ct));

    [Authorize(Roles = "Admin")]
    [HttpDelete("sector-targets/{id:int}")]
    public async Task<IActionResult> DeleteSectorTarget(int id, CancellationToken ct)
        => await service.DeleteSectorTargetAsync(id, ct) ? NoContent() : NotFound();

    // Ã¢â€â‚¬Ã¢â€â‚¬ Single Position Limits (by Role) Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    [Authorize(Roles = "Admin")]
    [HttpPost("position-limits")]
    public async Task<ActionResult<SinglePositionLimitDto>> AddPositionLimit([FromBody] UpsertSinglePositionLimitRequest request, CancellationToken ct)
        => Ok(await service.UpsertPositionLimitAsync(null, request, ct));

    [Authorize(Roles = "Admin")]
    [HttpPut("position-limits/{id:int}")]
    public async Task<ActionResult<SinglePositionLimitDto>> UpdatePositionLimit(int id, [FromBody] UpsertSinglePositionLimitRequest request, CancellationToken ct)
        => Ok(await service.UpsertPositionLimitAsync(id, request, ct));

    [Authorize(Roles = "Admin")]
    [HttpDelete("position-limits/{id:int}")]
    public async Task<IActionResult> DeletePositionLimit(int id, CancellationToken ct)
        => await service.DeletePositionLimitAsync(id, ct) ? NoContent() : NotFound();
}