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
    IPortfolioActionScoreService actionScore,
    PortfolioManager.Api.Services.FinalAction.IFinalActionService finalActions) : ControllerBase
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

    /// <summary>
    /// Every scored watchlist candidate with its canonical Final Action and Action-Center eligibility, computed by
    /// the backend engine. The Priority Candidates widget shows only the eligible rows; no client-side rules.
    /// </summary>
    [HttpGet("priority-candidates")]
    public async Task<ActionResult<IReadOnlyList<PriorityCandidateRowDto>>> GetPriorityCandidates(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var scores = await actionScore.GetScoresAsync(userId, ct);
        var snapshot = await finalActions.ComputeAsync(userId, fresh: false, ct);
        var byTicker = snapshot.Watchlist.GroupBy(r => r.Symbol.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());

        return Ok(scores.Select(s =>
        {
            byTicker.TryGetValue(s.Symbol.ToUpperInvariant(), out var decision);
            var e = PortfolioManager.Api.Services.FinalAction.PriorityCandidateEligibility.Resolve(decision, s.TotalScore);
            return new PriorityCandidateRowDto(s, e.Eligible, e.PriorityLevel, e.ReasonsExcluded,
                decision?.FinalAction, decision?.FinalActionId, decision?.Reason, decision?.HoverDescription);
        }).ToList());
    }
}

public sealed record PriorityCandidateRowDto(
    ActionScoreDto Score,
    bool Eligible,
    string PriorityLevel,
    IReadOnlyList<string> ReasonsExcluded,
    string? FinalAction,
    string? FinalActionId,
    string? FinalActionReason,
    string? HoverDescription);
