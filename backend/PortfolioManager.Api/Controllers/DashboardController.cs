using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(
    IDashboardService dashboard,
    IPortfolioActionsService portfolioActions,
    IDashboardEodSummaryService eodSummary,
    IMarketLeadershipService marketLeadership) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken ct)
    {
        var snapshot = await dashboard.GetLatestAsync(CurrentUserId(), ct);
        return snapshot is null ? NoContent() : Ok(snapshot);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<DashboardResponse>> Refresh(CancellationToken ct)
        => Ok(await dashboard.RebuildAsync(CurrentUserId(), ct));

    [HttpGet("portfolio-actions")]
    public async Task<ActionResult<IReadOnlyList<PortfolioActionDto>>> GetPortfolioActions(CancellationToken ct)
        => Ok(await portfolioActions.GetActionsAsync(CurrentUserId(), ct));

    [HttpGet("eod-summary")]
    public async Task<ActionResult<DashboardEodSummary>> GetEodSummary(CancellationToken ct)
        => Ok(await eodSummary.GetLatestAsync(CurrentUserId(), ct));

    [HttpGet("market-leadership")]
    public async Task<ActionResult<MarketLeadershipResponse>> GetMarketLeadership(CancellationToken ct)
        => Ok(await marketLeadership.GetLeadershipAsync(CurrentUserId(), ct));

    [HttpPost("market-leadership/trackers")]
    public async Task<ActionResult<MarketLeadershipTrackerDto>> AddMarketLeadershipTracker(
        CreateMarketLeadershipTrackerRequest request,
        CancellationToken ct)
    {
        try
        {
            var tracker = await marketLeadership.AddTrackerAsync(CurrentUserId(), request, ct);
            return CreatedAtAction(nameof(GetMarketLeadership), tracker);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpDelete("market-leadership/trackers/{trackerId:int}")]
    public async Task<IActionResult> RemoveMarketLeadershipTracker(int trackerId, CancellationToken ct)
        => await marketLeadership.RemoveTrackerAsync(CurrentUserId(), trackerId, ct) ? NoContent() : NotFound();

    [HttpPut("market-leadership/trackers/{trackerId:int}")]
    public async Task<ActionResult<MarketLeadershipTrackerDto>> UpdateMarketLeadershipTracker(
        int trackerId,
        CreateMarketLeadershipTrackerRequest request,
        CancellationToken ct)
    {
        try
        {
            var tracker = await marketLeadership.UpdateTrackerAsync(CurrentUserId(), trackerId, request, ct);
            return tracker is null ? NotFound() : Ok(tracker);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
