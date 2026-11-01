using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Risk;

public static class PriceBases
{
    public const string Live = "Live";
    public const string Eod = "EOD";
    public const string Stale = "Stale";
    public const string Unavailable = "Unavailable";
}

/// <summary>
/// A provider-timestamped market observation. Legacy capture-only quotes cannot prove session freshness.
/// </summary>
public sealed record PriceEvidence(decimal? Price, DateTime? AsOfUtc, DateOnly? SessionDate, string Basis, string Source)
{
    public bool IsUsable => Price is > 0 && Basis is PriceBases.Live or PriceBases.Eod;
    public static PriceEvidence Unavailable(string source = "none") => new(null, null, null, PriceBases.Unavailable, source);

    /// <summary>Builds evidence from a captured quote using the existing session calendar. No external call.</summary>
    public static PriceEvidence FromQuote(decimal price, long unixSeconds, string? marketState, string symbol, DateTime utcNow,
        long? sourceUnixSeconds = null)
    {
        if (price <= 0 || unixSeconds <= 0 || unixSeconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds()) return Unavailable("quote");
        var captured = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        if (sourceUnixSeconds is not > 0 || sourceUnixSeconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            return new(price, captured, null, PriceBases.Stale, "quote lacks provider price timestamp");
        var source = DateTimeOffset.FromUnixTimeSeconds(sourceUnixSeconds.Value).UtcDateTime;
        if (source > utcNow || captured > utcNow)
            return new(price, source, null, PriceBases.Stale, "quote timestamp is in the future");
        var exchange = TradingCalendar.ExchangeFor(symbol);
        var state = (marketState ?? string.Empty).ToUpperInvariant();

        if (state == "REGULAR")
        {
            var age = utcNow - source;
            var localNow = TradingCalendar.TorontoNow(utcNow);
            var session = DateOnly.FromDateTime(localNow);
            var sourceTime = TimeOnly.FromDateTime(TradingCalendar.TorontoNow(source));
            var regular = TradingCalendar.IsTradingDay(session, exchange) && TradingCalendar.TorontoDate(source) == session
                && sourceTime >= new TimeOnly(9, 30) && sourceTime < TradingCalendar.CloseTime(session, exchange)
                && TimeOnly.FromDateTime(localNow) < TradingCalendar.CloseTime(session, exchange);
            return age <= TimeSpan.FromMinutes(30) && regular
                ? new PriceEvidence(price, source, session, PriceBases.Live, "provider regular-session quote")
                : new PriceEvidence(price, source, session, PriceBases.Stale, "provider quote outside current regular session");
        }

        var expected = TradingCalendar.LastCompletedSession(utcNow, exchange);
        var sourceSession = TradingCalendar.TorontoDate(source);
        // A regular-market timestamp near the session close is needed; capture time alone proves nothing.
        var sourceLocal = TradingCalendar.TorontoNow(source);
        var completed = sourceSession == expected && TimeOnly.FromDateTime(sourceLocal) >= TradingCalendar.CloseTime(sourceSession, exchange)
            && state is "CLOSED" or "POST" or "POSTPOST" or "PRE" or "PREPRE";
        return completed
            ? new PriceEvidence(price, source, sourceSession, PriceBases.Eod, "provider regular-session close")
            : new PriceEvidence(price, source, sourceSession, PriceBases.Stale, "quote does not prove the latest completed-session close");
    }
}

public sealed record StopLite(int Id, decimal TriggerPrice, decimal? CoveredQuantity, string BrokerVerificationStatus);

public sealed record RiskEvalInput(
    PriceEvidence Evidence,
    decimal? PlannedStop,
    IReadOnlyList<StopLite> ActiveStops,
    decimal SwingQuantity,
    decimal TotalQuantity,
    decimal ThresholdPct);

public sealed record RiskEvaluation(
    IReadOnlyList<string> Statuses,
    string Protection,
    string CoverageState,
    decimal? CoveredQuantity,
    string Severity,
    int Rank,
    decimal? GoverningStop,
    string? GoverningStopSource,
    decimal? DistanceAbs,
    decimal? DistancePct,
    bool NeedsAttention,
    string? Observation,
    string PlannedStopState);

/// <summary>
/// Pure stop-risk classification. Advisory only: it describes recorded stops against a market observation and never
/// claims that a brokerage order was triggered, filled, cancelled or verified.
/// </summary>
public static class StopRiskEvaluator
{
    public const decimal DefaultThresholdPct = 5m;

    public static string CoverageFor(IReadOnlyList<StopLite> active, decimal swingQty, decimal totalQty, out decimal? covered)
    {
        covered = null;
        if (active.Count == 0) return CoverageStates.None;
        if (Math.Abs(totalQty - swingQty) > 0.000001m) return CoverageStates.Ambiguous;
        if (active.Any(s => s.CoveredQuantity is null)) return CoverageStates.Unknown;
        covered = active.Sum(s => s.CoveredQuantity!.Value);
        if (covered > totalQty) return CoverageStates.OverCovered;
        if (covered == totalQty) return CoverageStates.Full;
        return CoverageStates.Partial;
    }

    public static RiskEvaluation Evaluate(RiskEvalInput i)
    {
        var statuses = new List<string>();
        var active = i.ActiveStops;
        var coverage = CoverageFor(active, i.SwingQuantity, i.TotalQuantity, out var covered);
        var brokerStop = active.Count > 0 ? active.Max(s => s.TriggerPrice) : (decimal?)null;

        decimal? governing = brokerStop ?? i.PlannedStop;
        string? governingSource = brokerStop is not null ? "Broker stop (user-recorded)" : i.PlannedStop is not null ? "Planned stop (alert only)" : null;

        var usable = i.Evidence.IsUsable;
        var price = i.Evidence.Price ?? 0m;
        decimal? distAbs = null, distPct = null;
        if (usable && governing is > 0)
        {
            distAbs = Math.Round(price - governing.Value, 4);
            distPct = Math.Round((price - governing.Value) / price * 100m, 2);
        }

        var breached = usable
            && ((brokerStop is not null && price <= brokerStop) || (i.PlannedStop is not null && price <= i.PlannedStop));
        var approaching = usable && !breached && distPct is not null && distPct <= i.ThresholdPct;

        if (!usable) statuses.Add(RiskStatuses.StaleEvidence);
        if (breached) statuses.Add(RiskStatuses.StopBreached);
        if (approaching) statuses.Add(RiskStatuses.ApproachingStop);
        if (i.PlannedStop is null) statuses.Add(RiskStatuses.MissingRiskPlan);
        if (active.Count == 0) statuses.Add(RiskStatuses.NoBrokerProtection);
        switch (coverage)
        {
            case CoverageStates.Partial: statuses.Add(RiskStatuses.PartialProtection); break;
            case CoverageStates.Unknown: statuses.Add(RiskStatuses.UnknownCoverage); break;
            case CoverageStates.Ambiguous: statuses.Add(RiskStatuses.AmbiguousCoverage); break;
            case CoverageStates.OverCovered: statuses.Add(RiskStatuses.OverCovered); break;
        }
        if (i.PlannedStop is not null && active.Any(s => s.TriggerPrice != i.PlannedStop)) statuses.Add(RiskStatuses.StopMismatch);
        if (active.Sum(s => s.CoveredQuantity ?? 0) > i.TotalQuantity && !statuses.Contains(RiskStatuses.OverCovered))
            statuses.Add(RiskStatuses.OverCovered);

        var weakCoverage = coverage is CoverageStates.None or CoverageStates.Unknown or CoverageStates.Partial or CoverageStates.Ambiguous;
        string severity; int rank;
        if (breached && weakCoverage) { severity = "Critical"; rank = 1; }
        else if (breached) { severity = "High"; rank = 2; }
        else if (approaching && weakCoverage) { severity = "High"; rank = 3; }
        else if (statuses.Any(s => s is RiskStatuses.MissingRiskPlan or RiskStatuses.NoBrokerProtection or RiskStatuses.PartialProtection
                     or RiskStatuses.UnknownCoverage or RiskStatuses.AmbiguousCoverage or RiskStatuses.OverCovered or RiskStatuses.StopMismatch
                     or RiskStatuses.ApproachingStop)
                 || (!usable && weakCoverage)) { severity = "Medium"; rank = 4; }
        else if (!usable) { severity = "Low"; rank = 5; }
        else { severity = "None"; rank = 6; }

        var hasProtection = active.Count > 0;
        var verified = hasProtection && active.All(s => s.BrokerVerificationStatus == BrokerVerification.Verified);
        string protection = coverage switch
        {
            CoverageStates.None => i.PlannedStop is not null ? "Alert-Only Stop (no broker order)" : "No Stop",
            CoverageStates.Partial => "Partially Covered",
            CoverageStates.Full when verified => "Fully Protected",
            _ => "Broker Stop Recorded",
        };

        string? observation = null;
        if (breached)
        {
            var stop = brokerStop is not null && price <= brokerStop ? brokerStop : i.PlannedStop;
            observation = $"Price {price:0.####} is at/below the recorded stop {stop:0.####} "
                + $"as of {i.Evidence.SessionDate:yyyy-MM-dd} ({i.Evidence.Basis}). This is a market observation only; "
                + "it does not show that a broker order triggered or filled. Verify with your broker.";
        }

        var plannedState = i.PlannedStop is not > 0 ? PlannedStopStates.NoPlan
            : !usable ? PlannedStopStates.Unverified
            : price <= i.PlannedStop ? PlannedStopStates.Breached
            : PlannedStopStates.Clear;

        var attention = statuses.Count > 0;
        return new RiskEvaluation(statuses, protection, coverage, covered, severity, rank, governing, governingSource,
            distAbs, distPct, attention, observation, plannedState);
    }
}
