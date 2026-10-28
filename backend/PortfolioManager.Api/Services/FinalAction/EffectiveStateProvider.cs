using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.FinalAction;

/// <summary>The current, effective view of one stored Final Action recommendation (separate from its lifecycle Status).</summary>
public sealed record EventEffective(
    EffectiveStatus Status,
    /// <summary>Fresh | Stale | Missing | Invalid | Failed from the latest evaluation of the symbol; Unknown when none is cached.</summary>
    string EvidenceHealth,
    DateOnly? DataAsOf,
    IReadOnlyList<string> Codes);

/// <summary>
/// Read-only bridge between a stored recommendation (Signal Tracker event) and the status resolver. Used by the Action Center
/// and the Signal Tracker so both always report the same effective state. It never scans, publishes, or changes a record or a
/// timestamp: a historically Active recommendation can be Awaiting Data (non-actionable) right now.
/// </summary>
public static class EffectiveStateProvider
{
    public const string UnknownHealth = "Unknown";

    public static EventEffective? ForEvent(SignalAuditEvent e, FinalActionSnapshotDto? snapshot, EvaluationStatus eval, DateTime utcNow)
    {
        if (!e.SignalType.StartsWith(SignalTypeCatalog.FinalActionPrefix, StringComparison.Ordinal)) return null;
        if (e.Status != SignalStatuses.Active) return null;   // closed lifecycle states are history, not a current state

        var id = e.SignalType[SignalTypeCatalog.FinalActionPrefix.Length..];
        var bucket = DecisionEligibility.Classify(id, e.Source, 1m);
        var holding = e.Source == SignalSources.Portfolio;
        var exchange = TradingCalendar.ExchangeFor(e.Symbol);
        var expected = TradingCalendar.LastCompletedSession(utcNow, exchange);

        var results = holding ? snapshot?.Portfolio : snapshot?.Watchlist;
        var canonical = results?.FirstOrDefault(r => string.Equals(r.Symbol, e.Symbol, StringComparison.OrdinalIgnoreCase));

        EvidenceAssessment? current = null;
        var currentBucket = DecisionBucket.None;
        if (canonical is not null && canonical.EvaluationState != nameof(EffectiveState.CurrentValid))
        {
            current = new EvidenceAssessment(
                Enum.TryParse<EvidenceHealth>(canonical.EvidenceHealth, out var h) ? h : EvidenceHealth.Missing,
                canonical.ReasonCodes ?? [], canonical.ExpectedSession ?? expected, canonical.DataAsOfDate);
        }
        else if (canonical is not null && canonical.FinalActionId == id)
        {
            current = new EvidenceAssessment(EvidenceHealth.Fresh, [], canonical.ExpectedSession ?? expected, canonical.DataAsOfDate);
            currentBucket = bucket;
        }

        var prior = new PriorRecommendation(id, e.Source, e.Role, e.Status, e.CapturedAtUtc, e.LastValidatedAtUtc);
        var status = FinalActionStatusResolver.Resolve(new ResolveInput(
            current, currentBucket, prior, eval.Run, eval.LastCompletePassUtc, eval.LastAttemptFailed, exchange, utcNow));

        var health = canonical?.EvidenceHealth ?? UnknownHealth;
        return new EventEffective(status, health, canonical?.DataAsOfDate, status.Codes);
    }
}
