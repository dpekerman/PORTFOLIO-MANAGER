using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.FinalAction;

/// <summary>Is the market evidence behind a decision usable right now? Independent of any recommendation's lifecycle.</summary>
public enum EvidenceHealth { Fresh, Stale, Missing, Invalid, Failed }

/// <summary>What the user sees for a symbol. A function of evidence health, lifecycle and the evaluation run.</summary>
public enum EffectiveState { CurrentValid, PriorSessionValid, AwaitingData, Evaluating, Expired, HistoricalWarning }

public enum EvaluationRun { None, Running, Interrupted }

public sealed record EvidenceInput(
    /// <summary>Date of the completed daily bar the scan used (<c>RsiScanResult.TradingDate</c>).</summary>
    DateOnly? ScanTradingDate,
    /// <summary>Source quote timestamp (Yahoo regularMarketTime), when the snapshot quote carries one.</summary>
    DateTime? QuoteTimestampUtc,
    decimal Price,
    /// <summary>The symbol's scan failed or was absent in a pass.</summary>
    bool EvaluationFailed,
    /// <summary>First required input missing for the decision rule (see <see cref="FinalActionStatusResolver.MissingRequired"/>).</summary>
    string? MissingRequired);

public sealed record EvidenceAssessment(
    EvidenceHealth Health,
    IReadOnlyList<string> Codes,
    DateOnly ExpectedSession,
    DateOnly? DataAsOf);

/// <summary>Latest persisted recommendation for the symbol/source (a Signal Tracker event or a published snapshot action).</summary>
public sealed record PriorRecommendation(
    string ActionId,
    string Source,
    string? Role,
    string Status,
    DateTime CapturedAtUtc,
    DateTime? LastValidatedAtUtc);

public sealed record ResolveInput(
    /// <summary>Evidence assessment of the evaluation made in this pass; null when no evaluation exists (cache miss / not yet run).</summary>
    EvidenceAssessment? Current,
    /// <summary>Bucket of the freshly produced canonical result, if one exists.</summary>
    DecisionBucket CurrentBucket,
    PriorRecommendation? Prior,
    EvaluationRun Run,
    /// <summary>The latest Final Action pass finished and was complete / failed or incomplete.</summary>
    DateTime? LastCompletePassUtc,
    bool LastAttemptFailed,
    MarketExchange Exchange,
    DateTime UtcNow);

public sealed record EffectiveStatus(
    EffectiveState State,
    bool IsActionable,
    IReadOnlyList<string> Codes,
    int? AgeTradingDays,
    DecisionBucket Bucket,
    bool IsHistoricalWarning);

/// <summary>
/// Pure rules deciding the effective display state of a Final Action. No I/O and no investment thresholds: lifecycle
/// windows come from <see cref="RecommendationLifecycle"/>, session boundaries from <see cref="TradingCalendar"/>.
/// Invariant: missing, stale, invalid or failed evidence can only yield Awaiting Data, Evaluating or a retained prior
/// recommendation — never a Hold, Buy or Sell created from absence of evidence.
/// </summary>
public static class FinalActionStatusResolver
{
    /// <summary>An automation run that has not reported progress for this long is treated as interrupted, not as running.</summary>
    public static readonly TimeSpan HeartbeatWindow = TimeSpan.FromMinutes(15);

    public const string AwaitingNoEvaluation = "AWAITING_NO_EVALUATION";
    public const string AwaitingStaleData = "AWAITING_STALE_DATA";
    public const string AwaitingRefreshFailed = "AWAITING_REFRESH_FAILED";
    public const string AwaitingInvalidPrice = "AWAITING_INVALID_PRICE";
    public const string AwaitingRequiredPrefix = "AWAITING_REQUIRED_";
    public const string EvaluationInterrupted = "EVALUATION_INTERRUPTED";
    public const string RevalidationMissed = "REVALIDATION_MISSED";
    public const string LifecycleWindowElapsed = "LIFECYCLE_WINDOW_ELAPSED";
    public const string QuoteOlderThanScan = "QUOTE_OLDER_THAN_SCAN";
    public const string QuoteTimestampUnavailable = "QUOTE_TIMESTAMP_UNAVAILABLE";

    // ── Evidence ─────────────────────────────────────────────────────────────

    public static EvidenceAssessment AssessEvidence(EvidenceInput e, MarketExchange ex, DateTime utcNow)
    {
        var expected = TradingCalendar.LastCompletedSession(utcNow, ex);
        EvidenceAssessment Result(EvidenceHealth h, params string[] codes) => new(h, codes, expected, e.ScanTradingDate);

        if (e.EvaluationFailed) return Result(EvidenceHealth.Failed, AwaitingRefreshFailed);
        if (e.Price <= 0m) return Result(EvidenceHealth.Invalid, AwaitingInvalidPrice);
        if (e.MissingRequired is not null) return Result(EvidenceHealth.Missing, AwaitingRequiredPrefix + e.MissingRequired);
        if (e.ScanTradingDate is null) return Result(EvidenceHealth.Missing, AwaitingNoEvaluation);
        if (e.ScanTradingDate < expected) return Result(EvidenceHealth.Stale, AwaitingStaleData);

        if (e.QuoteTimestampUtc is { } quoteUtc)
        {
            // A new scan wrapped around an old quote is not fresh evidence.
            var quoteSession = TradingCalendar.TorontoDate(quoteUtc);
            if (quoteSession < expected) return Result(EvidenceHealth.Stale, AwaitingStaleData, QuoteOlderThanScan);
            return Result(EvidenceHealth.Fresh);
        }
        return Result(EvidenceHealth.Fresh, QuoteTimestampUnavailable);
    }

    /// <summary>
    /// Required evidence depends on the decision rule. Optional indicators (RSI signal line, Fibonacci, analyst target,
    /// volume ratio, MACD) never invalidate a decision; only inputs the rule cannot be evaluated without are listed.
    /// </summary>
    public static string? MissingRequired(RsiScanResult scan, string category)
    {
        if (scan.CurrentPrice <= 0m) return "PRICE";
        if (category == "Entry")
        {
            if (scan.Rsi <= 0m) return "RSI";
            if (scan.Ema20Price <= 0m || scan.Sma50Price <= 0m) return "TREND_AVERAGES";
        }
        return null;
    }

    public static EvaluationRun RunState(string? overallStatus, DateTime? lastHeartbeatUtc, DateTime startedUtc, DateTime utcNow)
    {
        if (!string.Equals(overallStatus, AutomationStatuses.Running, StringComparison.Ordinal)) return EvaluationRun.None;
        var last = lastHeartbeatUtc ?? startedUtc;
        return utcNow - last <= HeartbeatWindow ? EvaluationRun.Running : EvaluationRun.Interrupted;
    }

    // ── Effective state ──────────────────────────────────────────────────────

    public static EffectiveStatus Resolve(ResolveInput i)
    {
        var priorBucket = i.Prior is null ? DecisionBucket.None : DecisionEligibility.Classify(i.Prior.ActionId, i.Prior.Source, 1m);
        var longHorizonOrRisk = i.Prior is not null && (priorBucket == DecisionBucket.RiskReview
            || (priorBucket == DecisionBucket.TrimExit && i.Prior.Source == SignalSources.Portfolio
                && !PortfolioEvidenceGate.IsShortHorizon(i.Prior.Role)));

        // 1. A fresh evaluation made in this pass is the current truth.
        if (i.Current is { Health: EvidenceHealth.Fresh } fresh)
            return new EffectiveStatus(EffectiveState.CurrentValid, i.CurrentBucket != DecisionBucket.None,
                fresh.Codes, 0, i.CurrentBucket, false);

        // 2. An evaluation was attempted but its evidence is unusable: never a Hold, never a new trade decision.
        if (i.Current is { } bad)
            return Unavailable(i, bad.Codes, priorBucket, longHorizonOrRisk);

        // 3. No evaluation exists yet.
        if (i.Run == EvaluationRun.Running)
            return new EffectiveStatus(EffectiveState.Evaluating, false, [], null, priorBucket, false);
        if (i.Run == EvaluationRun.Interrupted)
            return Unavailable(i, [EvaluationInterrupted], priorBucket, longHorizonOrRisk);

        if (i.Prior is null) return Unavailable(i, [AwaitingNoEvaluation], DecisionBucket.None, false);
        if (i.Prior.Status == SignalStatuses.Expired)
            return new EffectiveStatus(EffectiveState.Expired, false, [LifecycleWindowElapsed], null, priorBucket, false);
        if (i.Prior.Status != SignalStatuses.Active)
            return Unavailable(i, [AwaitingNoEvaluation], DecisionBucket.None, false);

        var expected = TradingCalendar.LastCompletedSession(i.UtcNow, i.Exchange);
        var validatedSession = TradingCalendar.LastCompletedSession(i.Prior.LastValidatedAtUtc ?? i.Prior.CapturedAtUtc, i.Exchange);
        var age = TradingCalendar.TradingDaysBetween(validatedSession, expected, i.Exchange);

        // A complete pass ran for the latest session but did not revalidate this symbol: it was not evaluated.
        var passSession = i.LastCompletePassUtc is { } pass ? TradingCalendar.LastCompletedSession(pass, i.Exchange) : (DateOnly?)null;
        var missed = age > 0 && passSession is { } ps && ps >= expected;
        if (i.LastAttemptFailed || missed)
            return Unavailable(i, [missed ? RevalidationMissed : AwaitingRefreshFailed], priorBucket, longHorizonOrRisk, age);

        // A non-actionable prior (a Hold shown on a row) has no lifecycle window: it is only valid for the latest session.
        var window = priorBucket == DecisionBucket.None ? 0
            : RecommendationLifecycle.ExpiryWindowDays(priorBucket == DecisionBucket.EntryAdd ? SignalCategories.Entry : SignalCategories.TrimExit);
        if (!longHorizonOrRisk && age > window)
            return Unavailable(i, [LifecycleWindowElapsed], priorBucket, false, age);

        // Valid prior-session recommendation: no newer failure, still inside its lifecycle.
        return new EffectiveStatus(EffectiveState.PriorSessionValid, priorBucket != DecisionBucket.None, [], age, priorBucket, false);
    }

    private static EffectiveStatus Unavailable(
        ResolveInput i, IReadOnlyList<string> codes, DecisionBucket priorBucket, bool keepAsHistoricalWarning, int? age = null)
    {
        // Confirmed historical risk (Strategic/Core exits, Risk Review) stays visibly listed, never counted as actionable.
        if (keepAsHistoricalWarning && i.Prior is { Status: SignalStatuses.Active })
            return new EffectiveStatus(EffectiveState.HistoricalWarning, false, codes, age, priorBucket, true);
        return new EffectiveStatus(EffectiveState.AwaitingData, false, codes, age, priorBucket, false);
    }
}
