using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

/// <summary>Read-only audit trail; only Admins may delete entries. Non-admins only see their own entries.</summary>
[Authorize]
[ApiController]
[Route("api/audit")]
public sealed class AuditController(IAuditService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AuditPagedResponse>> Get([FromQuery] AuditQuery query, CancellationToken ct) =>
        Ok(await service.QueryAsync(query, ct));

    /// <summary>CSV download of the current filter (max 10,000 rows).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] AuditQuery query, CancellationToken ct)
    {
        var csv = await service.ExportCsvAsync(query, ct);
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv", $"audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    /// <summary>Deletes several audit entries (Admin only, max 500). The deletion is itself logged.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("delete-batch")]
    public async Task<ActionResult<object>> DeleteBatch([FromBody] DeleteAuditBatchRequest request, CancellationToken ct) =>
        Ok(new { deleted = await service.DeleteManyAsync(request.Ids ?? [], ct) });

    [HttpGet("filters")]
    public async Task<ActionResult<AuditFiltersDto>> GetFilters(CancellationToken ct) =>
        Ok(await service.GetFiltersAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AuditLogDto>> GetById(long id, CancellationToken ct)
    {
        var item = await service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Deletes one audit entry (Admin only). The deletion is itself logged.</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
