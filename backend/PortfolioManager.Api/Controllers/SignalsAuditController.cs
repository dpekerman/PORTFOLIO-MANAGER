using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;
using System.Security.Claims;

namespace PortfolioManager.Api.Controllers;

/// <summary>Historical recommendation audit: settings, signal list, effectiveness and investor decisions.</summary>
[Authorize]
[ApiController]
[Route("api/signals-audit")]
public sealed class SignalsAuditController(ISignalsAuditService service) : ControllerBase
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("settings")]
    public async Task<ActionResult<SignalsAuditSettingsDto>> GetSettings(CancellationToken ct) =>
        Ok(await service.GetSettingsAsync(CurrentUserId(), ct));

    [HttpPut("settings")]
    public async Task<ActionResult<SignalsAuditSettingsDto>> SaveSettings(
        [FromBody] SaveSignalsAuditSettingsRequest request, CancellationToken ct)
    {
        try { return Ok(await service.SaveSettingsAsync(CurrentUserId(), request, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { title = ex.Message }); }
    }

    [HttpGet("signal-types")]
    public async Task<ActionResult<IReadOnlyList<SignalTypeOptionDto>>> GetSignalTypes(CancellationToken ct) =>
        Ok(await service.GetSignalTypesAsync(CurrentUserId(), ct));

    /// <summary>Every Final Action the rules can emit (canonical ids), for the "Add action" picker.</summary>
    [HttpGet("final-action-catalog")]
    public ActionResult<IReadOnlyList<FinalActionCatalogItemDto>> GetFinalActionCatalog() =>
        Ok(FinalActionCatalog.All
            .Select(d => new FinalActionCatalogItemDto(d.Id, d.Label, d.Category, d.Direction, d.Group))
            .OrderBy(d => d.Group).ThenBy(d => d.Label).ToList());

    [HttpGet]
    public async Task<ActionResult<SignalAuditPagedResponse>> Get([FromQuery] SignalsAuditQuery query, CancellationToken ct) =>
        Ok(await service.QueryAsync(CurrentUserId(), query, ct));

    [HttpGet("summary")]
    public async Task<ActionResult<SignalAuditSummaryDto>> GetSummary([FromQuery] SignalsAuditQuery query, CancellationToken ct) =>
        Ok(await service.GetSummaryAsync(CurrentUserId(), query, ct));

    /// <summary>Rolling-cohort effectiveness, independent of the table's ticker / date / status / My Action filters.</summary>
    [HttpGet("effectiveness")]
    public async Task<ActionResult<SignalEffectivenessResultDto>> GetEffectiveness(
        [FromQuery] SignalEffectivenessQuery query, CancellationToken ct)
    {
        try { return Ok(await service.GetEffectivenessAsync(CurrentUserId(), query, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { title = ex.Message }); }
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SignalAuditDetailDto>> GetById(long id, CancellationToken ct)
    {
        var detail = await service.GetDetailAsync(CurrentUserId(), id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("{id:long}/transaction-candidates")]
    public async Task<ActionResult<IReadOnlyList<SignalTransactionCandidateDto>>> GetCandidates(long id, CancellationToken ct)
    {
        var candidates = await service.GetTransactionCandidatesAsync(CurrentUserId(), id, ct);
        return candidates is null ? NotFound() : Ok(candidates);
    }

    /// <summary>Records the investor's decision on a signal. Never creates or edits a transaction.</summary>
    [HttpPut("{id:long}/action")]
    public async Task<ActionResult<SignalAuditRowDto>> SaveAction(
        long id, [FromBody] SaveSignalActionRequest request, CancellationToken ct)
    {
        try
        {
            var row = await service.SaveActionAsync(CurrentUserId(), id, request, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (ArgumentException ex) { return BadRequest(new { title = ex.Message }); }
    }

    /// <summary>Explicit user dismissal of an Active recommendation. Separate from "Not Actioned".</summary>
    [HttpPost("{id:long}/dismiss")]
    public async Task<ActionResult<SignalAuditRowDto>> Dismiss(long id, [FromBody] DismissSignalRequest? request, CancellationToken ct)
    {
        try
        {
            var row = await service.DismissAsync(CurrentUserId(), id, request?.Note, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (ArgumentException ex) { return BadRequest(new { title = ex.Message }); }
    }

    /// <summary>Runs capture on demand. Non-EOD: can create missing recommendations but never close or revalidate. Admin only.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("capture")]
    public async Task<ActionResult<SignalCaptureResultDto>> Capture(CancellationToken ct) =>
        Ok(await service.CaptureAsync(CurrentUserId(), ct));
}
