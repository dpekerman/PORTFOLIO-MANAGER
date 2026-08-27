using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/analytics")]
public sealed class AnalyticsController(
    IAnalyticsService analyticsService,
    IPerformanceSummaryService performanceSummary,
    IPortfolioActionScoreService actionScore) : ControllerBase
{
    [HttpGet("decision-performance")]
    public async Task<ActionResult<AnalyticsDecisionPerformanceResponse>> GetDecisionPerformance(CancellationToken ct)
        => Ok(await analyticsService.GetDecisionPerformanceAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, ct));

    [HttpGet("performance-summary")]
    public async Task<ActionResult<PerformanceSummaryResponse>> GetPerformanceSummary(CancellationToken ct)
    {
        var result = await performanceSummary.GetSummaryAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, ct);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("action-scores")]
    public async Task<ActionResult<IReadOnlyList<ActionScoreDto>>> GetActionScores(CancellationToken ct)
        => Ok(await actionScore.GetScoresAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, ct));
}
