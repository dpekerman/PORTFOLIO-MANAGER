using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using System.Security.Claims;

namespace PortfolioManager.Api.Services;

public interface IAnalyticsService
{
    Task<AnalyticsDecisionPerformanceResponse> GetDecisionPerformanceAsync(string userId, CancellationToken ct = default);
}

public sealed class AnalyticsService(AppDbContext db, IHttpContextAccessor httpCtx) : IAnalyticsService
{
    private string? CurrentUserId() => httpCtx.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool IsAdmin() => httpCtx.HttpContext?.User.IsInRole("Admin") ?? false;

    public async Task<AnalyticsDecisionPerformanceResponse> GetDecisionPerformanceAsync(string userId, CancellationToken ct = default)
    {
        var uid = CurrentUserId();
        var query = db.PortfolioItems.AsNoTracking()
            .Where(p => p.TransactionType == "CLOSE" && p.ClosingPrice.HasValue && p.AverageCostBasis > 0);

        if (!IsAdmin())
            query = query.Where(p => p.UserId == uid || p.UserId == null);

        var closed = await query.ToListAsync(ct);

        var rows = closed
            .GroupBy(p => string.IsNullOrWhiteSpace(p.DecisionSource) ? "Unspecified" : p.DecisionSource)
            .Select(g =>
            {
                var wins = g.Count(p => p.ClosingPrice > p.AverageCostBasis);
                var winRate = g.Count() > 0 ? (double)wins / g.Count() * 100 : 0;
                var avgReturn = g.Count() > 0
                    ? g.Average(p => (double)((p.ClosingPrice!.Value - p.AverageCostBasis) / p.AverageCostBasis * 100))
                    : 0;
                var avgDays = g.Where(p => p.OpenDate.HasValue && p.CloseDate.HasValue)
                    .Select(p => (p.CloseDate!.Value - p.OpenDate!.Value).TotalDays)
                    .DefaultIfEmpty(0)
                    .Average();
                return new DecisionPerformanceRow(g.Key, g.Count(), wins,
                    Math.Round(winRate, 1), Math.Round(avgReturn, 2), Math.Round(avgDays, 1));
            })
            .OrderByDescending(r => r.TradeCount)
            .ToList();

        var total = closed.Count;
        var overallWins = closed.Count(p => p.ClosingPrice > p.AverageCostBasis);
        var overallWinRate = total > 0 ? Math.Round((double)overallWins / total * 100, 1) : 0;
        var overallAvgReturn = total > 0
            ? Math.Round(closed.Average(p => (double)((p.ClosingPrice!.Value - p.AverageCostBasis) / p.AverageCostBasis * 100)), 2)
            : 0;

        return new AnalyticsDecisionPerformanceResponse(rows, total, overallWinRate, overallAvgReturn);
    }
}
