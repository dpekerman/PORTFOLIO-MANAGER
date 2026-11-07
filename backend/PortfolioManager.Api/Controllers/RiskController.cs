using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Api.Controllers;

/// <summary>Risk plans and user-recorded broker stop records. Never talks to a brokerage.</summary>
[Authorize]
[ApiController]
[Route("api/risk")]
public sealed class RiskController(IRiskService risk, IStopSheetImportService import) : ControllerBase
{
    private async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (RiskValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (RiskNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (RiskConcurrencyException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpGet("position")]
    public Task<ActionResult<RiskPositionDto>> Position([FromQuery] string account, [FromQuery] string symbol, CancellationToken ct)
        => Run(() => risk.GetPositionAsync(account, symbol, ct));

    [Authorize(Roles = "Admin,Trader")]
    [HttpPut("plan")]
    public Task<ActionResult<RiskPositionDto>> SavePlan([FromBody] SaveRiskPlanRequest request, CancellationToken ct)
        => Run(() => risk.SavePlanAsync(request, ct));

    [Authorize(Roles = "Admin,Trader")]
    [HttpPost("broker-stops")]
    public Task<ActionResult<RiskPositionDto>> AddBrokerStop([FromBody] SaveBrokerStopRequest request, CancellationToken ct)
        => Run(() => risk.SaveBrokerStopAsync(request with { Id = null }, ct));

    [Authorize(Roles = "Admin,Trader")]
    [HttpPut("broker-stops/{id:int}")]
    public Task<ActionResult<RiskPositionDto>> UpdateBrokerStop(int id, [FromBody] SaveBrokerStopRequest request, CancellationToken ct)
        => Run(() => risk.SaveBrokerStopAsync(request with { Id = id }, ct));

    [Authorize(Roles = "Admin,Trader")]
    [HttpPost("broker-stops/{id:int}/status")]
    public Task<ActionResult<RiskPositionDto>> Transition(int id, [FromBody] BrokerStopTransitionRequest request, CancellationToken ct)
        => Run(() => risk.TransitionBrokerStopAsync(id, request, ct));

    /// <summary>Dry run by default. Applying requires an administrator and is a separately approved operation.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("import-stop-sheet")]
    public Task<ActionResult<StopSheetResultDto>> ImportStopSheet([FromBody] StopSheetRequest request, CancellationToken ct)
        => Run(() => import.ImportAsync(request, ct));

    [Authorize(Roles = "Admin")]
    [HttpPost("episodes/backfill")]
    public Task<ActionResult<BackfillReportDto>> Backfill([FromQuery] bool dryRun = true, CancellationToken ct = default)
        => Run(() => risk.BackfillEpisodesAsync(dryRun, ct));
}

[Authorize]
[ApiController]
[Route("api/swing-trading")]
public sealed class SwingTradingController(ISwingTradingService service) : ControllerBase
{
    [HttpGet("open-trades")]
    public async Task<ActionResult<OpenTradesResponse>> OpenTrades(CancellationToken ct) => Ok(await service.GetOpenTradesAsync(ct));

    [HttpGet("journal")]
    public async Task<ActionResult<IReadOnlyList<JournalRowDto>>> Journal(CancellationToken ct) => Ok(await service.GetJournalAsync(ct));
}
