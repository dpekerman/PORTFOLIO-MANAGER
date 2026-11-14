using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Opportunities;

/// <summary>
/// Market-derived "Calculated candidate" long setup (docs/pm-029-trading-opportunities.md §5). Uses only the scanner's completed
/// session in the analysis security's price basis — never ownership, saved risk plans or a fabricated fallback level (U2).
/// The result is analysis only: it never says Buy/Enter; FINAL ACTION remains the only action.
/// </summary>
public static class TradeSetupResolver
{
    public static TradeSetupResult Resolve(RsiScanResult? scan, DateOnly expectedSession)
    {
        if (scan is null) return Unavailable(null, OpportunityReasons.NoScan);
        var currency = string.IsNullOrWhiteSpace(scan.AnalysisCurrency) ? null : scan.AnalysisCurrency;
        if (scan.TradingDate != expectedSession) return Unavailable(currency, OpportunityReasons.StaleScan);

        var entry = scan.CurrentPrice;
        if (entry <= 0m) return Unavailable(currency, OpportunityReasons.NoEntryPrice);

        var stop = scan.DynamicStopLoss;
        if (stop <= 0m || stop >= entry) return Unavailable(currency, OpportunityReasons.NoValidStop, entry);

        var atr = scan.DailyAtr;
        var risk = entry - stop;
        var riskPct = risk / entry * 100m;
        if (atr <= 0m) return Unavailable(currency, OpportunityReasons.StopOutsideVolatilityBand, entry);
        var stopAtr = risk / atr;
        if (stopAtr < OpportunityScoringConfig.MinStopAtr || stopAtr > OpportunityScoringConfig.MaxStopAtr || riskPct > OpportunityScoringConfig.MaxRiskPct)
            return Unavailable(currency, OpportunityReasons.StopOutsideVolatilityBand, entry);

        var floor = entry + OpportunityScoringConfig.MinTargetAtr * atr;
        var candidates = new List<(decimal Level, string Source)>();
        if (!string.Equals(scan.ChannelDirection, "NONE", StringComparison.OrdinalIgnoreCase) && scan.UpperRailToday > 0m)
            candidates.Add((scan.UpperRailToday, "Upper channel rail"));
        if (scan.NearestOpenGapAbove is > 0m and var gap) candidates.Add((gap, "Open gap above"));
        if (scan.Week52High > 0m) candidates.Add((scan.Week52High, "52-week high"));
        var eligible = candidates.Where(c => c.Level > floor).OrderBy(c => c.Level).ThenBy(c => c.Source, StringComparer.Ordinal).ToList();
        if (eligible.Count == 0) return Unavailable(currency, OpportunityReasons.NoStructuralTarget, entry);

        var (target, source) = eligible[0];
        var rr = (target - entry) / risk;
        var kind = rr >= OpportunityScoringConfig.ActionableRewardRisk ? SetupKind.ValidCandidate : SetupKind.NotActionable;
        return new TradeSetupResult(kind, entry, stop, target, source, rr, riskPct, (target - entry) / entry * 100m, stopAtr, currency, []);
    }

    /// <summary>Worse of risk-to-stop % and ATR % of price; a hard structural negative is High; nothing measurable ⇒ Unavailable.</summary>
    public static RiskAssessment AssessRisk(RsiScanResult? scan, TradeSetupResult setup, DateOnly expectedSession)
    {
        var fresh = scan is not null && scan.TradingDate == expectedSession && scan.CurrentPrice > 0m;
        var hardNegative = fresh && scan!.PriceStructure?.HasHardStructuralNegative == true;
        decimal? atrPct = fresh && scan!.DailyAtr > 0m ? scan.DailyAtr / scan.CurrentPrice * 100m : null;
        var riskPct = setup.IsScorable ? setup.RiskPct : null;

        if (riskPct is null && atrPct is null)
            return new RiskAssessment(hardNegative ? RiskCategory.High : RiskCategory.Unavailable, null, null, hardNegative,
                hardNegative ? "Hard structural negative" : "No setup and no ATR");

        var byRisk = riskPct is { } r ? Band(r, OpportunityScoringConfig.RiskLowMaxPct, OpportunityScoringConfig.RiskMediumMaxPct) : RiskCategory.Unavailable;
        var byAtr = atrPct is { } a ? Band(a, OpportunityScoringConfig.AtrLowMaxPct, OpportunityScoringConfig.AtrMediumMaxPct) : RiskCategory.Unavailable;
        var worst = (RiskCategory)Math.Max((int)byRisk, (int)byAtr);
        if (hardNegative) worst = RiskCategory.High;
        var basis = hardNegative ? "Hard structural negative"
            : riskPct is not null && atrPct is not null ? "Risk to stop and ATR %"
            : riskPct is not null ? "Risk to stop" : "ATR % (no valid setup)";
        return new RiskAssessment(worst, riskPct, atrPct, hardNegative, basis);
    }

    private static RiskCategory Band(decimal pct, decimal lowMax, decimal mediumMax) =>
        pct <= lowMax ? RiskCategory.Low : pct <= mediumMax ? RiskCategory.Medium : RiskCategory.High;

    private static TradeSetupResult Unavailable(string? currency, string reason, decimal? entry = null, decimal? stop = null) =>
        new(SetupKind.Unavailable, entry, stop, null, null, null, null, null, null, currency, [reason]);
}
