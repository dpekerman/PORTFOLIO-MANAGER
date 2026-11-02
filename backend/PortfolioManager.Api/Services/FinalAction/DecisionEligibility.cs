using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.FinalAction;

public enum DecisionBucket { None, EntryAdd, TrimExit, RiskReview }

/// <summary>
/// Classifies an existing Final Action result into an Action Center bucket. It adds no rules of its own: the
/// catalog Category decides what is an Entry or Trim/Exit; WATCH / AVOID / WAIT / HOLD / generic REVIEW stay Advisory.
/// Risk Review is limited to the catalog's explicit risk-control exits on holdings. The engine exposes no holding
/// stop level (DynamicStopLoss is a staging stop for new signals), so no stop-breach rule is invented here.
/// </summary>
public static class DecisionEligibility
{
    private static readonly HashSet<string> RiskReviewIds = new(StringComparer.Ordinal)
    {
        "CUT LOSS/THESIS REVIEW",
        "CLOSE/PROTECT",
    };

    // Catalog TrimExit rows that only say "watch": passive, not a decision.
    private static readonly HashSet<string> PassiveTrimIds = new(StringComparer.Ordinal) { "TRIM/EXIT WATCH" };

    public static DecisionBucket Classify(FinalActionResultDto r)
    {
        if (r.AnalysisPrice <= 0m) return DecisionBucket.None;
        var id = FinalActionCatalog.CanonicalId(r.FinalActionId);
        var def = FinalActionCatalog.Describe(id);
        if (def.Category == "Entry") return DecisionBucket.EntryAdd;
        if (def.Category != "TrimExit" || PassiveTrimIds.Contains(id)) return DecisionBucket.None;
        // Selling needs a holding; a watchlist-only symbol cannot be trimmed.
        if (r.Source != SignalSources.Portfolio) return DecisionBucket.None;
        return RiskReviewIds.Contains(id) ? DecisionBucket.RiskReview : DecisionBucket.TrimExit;
    }

    public static bool IsActionable(FinalActionResultDto r) => Classify(r) != DecisionBucket.None;

    public static DecisionBucket Classify(string actionId, string source, decimal price) =>
        Classify(new FinalActionResultDto(string.Empty, source, null, null, actionId, actionId, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            null, false, price, string.Empty, null));
}

/// <summary>Per-security outcome of one evaluation pass. Only Confirmed / Invalidated may change a recommendation.</summary>
public enum EvaluationOutcome { Confirmed, Invalidated, Unknown }

/// <summary>
/// Completeness of a Final Action pass. A symbol listed in <see cref="UnknownSymbols"/> (scan failed, absent,
/// no usable price) can neither confirm nor invalidate anything. A null meta (legacy caller) means no knowledge.
/// </summary>
public sealed record FinalActionEvaluationMeta(
    IReadOnlySet<string> UnknownSymbols,
    DateTime? PortfolioSnapshotAtUtc,
    bool PassComplete)
{
    public bool IsUnknown(string symbol) => !PassComplete || UnknownSymbols.Contains(symbol.ToUpperInvariant());
}
