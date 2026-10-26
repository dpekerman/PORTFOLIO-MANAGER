using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Api.Services;

public sealed record OpenDecisionDto(
    long Id,
    string Symbol,
    string Context,          // Holding | Watchlist
    string? Role,
    string ActionLabel,
    string ActionId,
    string Bucket,           // EntryAdd | TrimExit | RiskReview
    string Urgency,          // Urgent | High | Normal
    string? Rationale,
    string Evidence,
    string SignalDate,
    DateTime? LastValidatedAtUtc,
    bool IsStale,
    string Status,
    string MyAction,
    decimal SignalPrice,
    string Origin);

public sealed record ActionSummaryDto(int EntryAdd, int TrimExit, int RiskReview, int TotalOpen, DateTime AsOfUtc, DateTime? LastValidatedAtUtc);

public sealed record OpenDecisionsResponse(ActionSummaryDto Summary, IReadOnlyList<OpenDecisionDto> Items);

public interface IOpenDecisionsService
{
    /// <summary>
    /// The single read-model behind the Dashboard summary and the Action Center. Both call this, so counts always
    /// reconcile. <paramref name="filter"/>: All | Entry | ReduceExit | Risk | Holdings | Watchlist.
    /// </summary>
    Task<OpenDecisionsResponse> GetAsync(string userId, string? filter, CancellationToken ct = default);
}

/// <summary>
/// Reads Active, validated recommendations from the Signal Tracker record. It evaluates nothing itself: eligibility
/// comes from <see cref="DecisionEligibility"/>, lifecycle from <see cref="SignalsAuditService"/>. It ignores
/// <c>PerformanceAudited</c> (analytics only).
/// </summary>
public sealed class OpenDecisionsService(AppDbContext db) : IOpenDecisionsService
{
    private static readonly string[] Filters = ["All", "Entry", "ReduceExit", "Risk", "Holdings", "Watchlist"];

    public async Task<OpenDecisionsResponse> GetAsync(string userId, string? filter, CancellationToken ct = default)
    {
        var f = Filters.FirstOrDefault(x => x.Equals(filter, StringComparison.OrdinalIgnoreCase)) ?? "All";
        var rows = await db.SignalAuditEvents.AsNoTracking()
            .Where(e => e.UserId == userId && e.Status == SignalStatuses.Active && e.EpisodeEndedDate == null
                        && e.LastValidatedAtUtc != null && e.SignalType.StartsWith(SignalTypeCatalog.FinalActionPrefix))
            .ToListAsync(ct);

        var today = SignalsAuditService.TorontoToday();
        var all = new List<OpenDecisionDto>();
        foreach (var e in rows)
        {
            var id = e.SignalType[SignalTypeCatalog.FinalActionPrefix.Length..];
            var bucket = DecisionEligibility.Classify(id, e.Source, 1m);
            if (bucket == DecisionBucket.None) continue;
            var holding = e.Source == SignalSources.Portfolio;
            var stale = RecommendationLifecycle.WeekdaysBetween(DateOnly.FromDateTime(e.LastValidatedAtUtc!.Value), today) > 1;
            var urgency = bucket == DecisionBucket.RiskReview ? "Urgent" : holding ? "High" : "Normal";
            all.Add(new OpenDecisionDto(
                e.Id, e.Symbol, holding ? "Holding" : "Watchlist", e.Role, FinalActionCatalog.Describe(id).Label, id,
                bucket.ToString(), urgency, Rationale(e.ContextJson), "Signal Tracker / EOD",
                e.SignalDate.ToString("yyyy-MM-dd"), e.LastValidatedAtUtc, stale, e.Status, e.MyAction, e.SignalPrice, e.Origin));
        }

        // Counts and list come from the same distinct set, so the summary always equals the Action Center total.
        var summary = new ActionSummaryDto(
            all.Count(d => d.Bucket == nameof(DecisionBucket.EntryAdd)),
            all.Count(d => d.Bucket == nameof(DecisionBucket.TrimExit)),
            all.Count(d => d.Bucket == nameof(DecisionBucket.RiskReview)),
            all.Count, DateTime.UtcNow, all.Count == 0 ? null : all.Max(d => d.LastValidatedAtUtc));

        var filtered = f switch
        {
            "Entry" => all.Where(d => d.Bucket == nameof(DecisionBucket.EntryAdd)),
            "ReduceExit" => all.Where(d => d.Bucket == nameof(DecisionBucket.TrimExit)),
            "Risk" => all.Where(d => d.Bucket == nameof(DecisionBucket.RiskReview)),
            "Holdings" => all.Where(d => d.Context == "Holding"),
            "Watchlist" => all.Where(d => d.Context == "Watchlist"),
            _ => all.AsEnumerable(),
        };

        // Urgent holding risk first, then other holding decisions, then watchlist entries.
        var ordered = filtered
            .OrderBy(d => d.Urgency == "Urgent" ? 0 : d.Context == "Holding" ? 1 : 2)
            .ThenBy(d => d.Bucket == nameof(DecisionBucket.TrimExit) ? 0 : 1)
            .ThenByDescending(d => d.SignalDate)
            .ThenBy(d => d.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new OpenDecisionsResponse(summary, ordered);
    }

    private static string? Rationale(string? contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(contextJson);
            return doc.RootElement.TryGetProperty("finalActionReason", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString()
                : null;
        }
        catch (JsonException) { return null; }
    }
}
