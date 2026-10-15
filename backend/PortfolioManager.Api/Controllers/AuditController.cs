using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

/// <summary>Read-only audit trail. Non-admins only see their own entries.</summary>
[Authorize]
[ApiController]
[Route("api/audit")]
public sealed class AuditController(IAuditService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AuditPagedResponse>> Get([FromQuery] AuditQuery query, CancellationToken ct) =>
        Ok(await service.QueryAsync(query, ct));

    [HttpGet("filters")]
    public async Task<ActionResult<AuditFiltersDto>> GetFilters(CancellationToken ct) =>
        Ok(await service.GetFiltersAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AuditLogDto>> GetById(long id, CancellationToken ct)
    {
        var item = await service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }
}
