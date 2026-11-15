using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Opportunities;

public sealed record SetupQualityResult(
    string Status, string PolicyVersion, bool PolicyApproved, string? Currency,
    decimal? Entry, decimal? ProposedStop, decimal? Support, DateOnly? SupportSession,
    decimal? Atr, decimal? ProposedTarget, string? TargetSource,
    decimal? CredibleTarget, decimal? Resistance, DateOnly? ResistanceSession,
    decimal? MarketRewardRisk, decimal? ConservativeRewardRisk,
    DateOnly ExpectedSession, DateTime? EvidenceAsOfUtc,
    IReadOnlyList<string> ReasonCodes);

/// <summary>Advisory only. The proposal is not activated by production call sites.</summary>
public static class SetupQualityCalculator
{
    public const string PolicyVersion = "pm029-setup-proposal-v1";
    public const decimal GoodRewardRisk = 2m;
    public const decimal AdequateStopAtr = 1m;

    public static SetupQualityResult Evaluate(
        RsiScanResult? scan, IReadOnlyList<MarketDailyClose>? history,
        DateOnly expectedSession, DateTime asOfUtc, bool classificationApproved = false)
    {
        if (asOfUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Setup evaluation requires a UTC cutoff.", nameof(asOfUtc));
        var setup = TradeSetupResolver.Resolve(scan, expectedSession);
        var reasons = new List<string>();
        decimal? support = null, resistance = null, conservative = null;
        DateOnly? supportSession = null, resistanceSession = null;
        if (scan is null || scan.TradingDate != expectedSession)
            reasons.Add(scan is null ? "NoScan" : "StaleScan");
        if (scan is not null && scan.AnalysisCurrency is not ("CAD" or "USD"))
            reasons.Add("SetupCurrencyUnverified");
        if (scan is not null && scan.TradingDate == expectedSession && (scan.ScannedAt.Kind != DateTimeKind.Utc
            || !TradingCalendar.IsTradingDay(expectedSession, TradingCalendar.ExchangeFor(scan.AnalysisTicker, scan.AnalysisMarket))
            || scan.ScannedAt > asOfUtc || scan.ScannedAt < TradingCalendar.SessionCloseUtc(expectedSession,
                TradingCalendar.ExchangeFor(scan.AnalysisTicker, scan.AnalysisMarket))))
            reasons.Add("SetupTimestampUnverified");
        if (history is null || history.Count < 5 || history.Any(bar => bar.Date > expectedSession)
            || history.GroupBy(bar => bar.Date).Any(group => group.Count() != 1)
            || history.Any(bar => bar.Open <= 0m || bar.High <= 0m || bar.Low <= 0m
                || bar.Close <= 0m || bar.High < Math.Max(bar.Open, bar.Close)
                || bar.Low > Math.Min(bar.Open, bar.Close))
            || history.Max(bar => bar.Date) != expectedSession)
            reasons.Add("StructuralHistoryUnavailable");
        if (scan?.DailyAtr is not > 0m) reasons.Add("AtrUnavailable");
        if (history is not null && history.Any(bar => bar.OriginalOhlcAvailable != true))
            reasons.Add("OriginalOhlcUnverified");
        if (scan is not null && history is not null && history.Any(bar => bar.PriceCurrency != scan.AnalysisCurrency))
            reasons.Add("StructuralCurrencyMismatch");
        if (reasons.Count == 0 && scan is not null && history is not null)
        {
            var ordered = history.OrderBy(bar => bar.Date).ToArray();
            var exchange = TradingCalendar.ExchangeFor(scan.AnalysisTicker, scan.AnalysisMarket);
            if (ordered.Zip(ordered.Skip(1), (a, b) => TradingCalendar.PreviousTradingDay(b.Date, exchange) != a.Date).Any(gap => gap))
                reasons.Add("StructuralSessionGap");
            else
            {
                var levels = ChannelAnalysisService.ConfirmedSwingLevels(ordered.Select(bar =>
                    new ChannelCandle(bar.Date.ToDateTime(TimeOnly.MinValue), bar.Open, bar.High, bar.Low, bar.Close)).ToArray(), scan.DailyAtr);
                var low = levels.Where(level => level.Role == "Support" && level.Price < scan.CurrentPrice
                    && ordered.Where(bar => bar.Date > DateOnly.FromDateTime(level.Date)).All(bar => bar.Close >= level.Price))
                    .OrderByDescending(level => level.Price).ThenByDescending(level => level.Date).FirstOrDefault();
                var high = levels.Where(level => level.Role == "Resistance" && level.Price > scan.CurrentPrice
                    && ordered.Where(bar => bar.Date > DateOnly.FromDateTime(level.Date)).All(bar => bar.Close <= level.Price))
                    .OrderBy(level => level.Price).ThenByDescending(level => level.Date).FirstOrDefault();
                if (low.Price > 0m) { support = low.Price; supportSession = DateOnly.FromDateTime(low.Date); }
                if (high.Price > 0m) { resistance = high.Price; resistanceSession = DateOnly.FromDateTime(high.Date); }
            }
        }
        if (support is null) reasons.Add("ConfirmedSupportUnavailable");
        if (resistance is null) reasons.Add("ConfirmedResistanceUnavailable");
        var structureVerified = reasons.Count == 0;
        if (setup.Entry is null || scan?.DynamicStopLoss is not > 0m || setup.Target is null)
            reasons.AddRange(setup.ReasonCodes.Count > 0 ? setup.ReasonCodes : ["MarketSetupUnavailable"]);
        if (reasons.Count == 0 && setup.Entry is { } entry && setup.Stop is { } stop && setup.Target is { } target && resistance is { } cap)
            conservative = (Math.Min(target, cap) - entry) / (entry - stop);
        var status = "Unavailable";
        if (structureVerified && scan is not null
            && scan.CurrentPrice > 0m && scan.DynamicStopLoss > 0m
            && (scan.DynamicStopLoss >= scan.CurrentPrice
                || (scan.CurrentPrice - scan.DynamicStopLoss) / scan.DailyAtr < OpportunityScoringConfig.MinStopAtr
                || (scan.CurrentPrice - scan.DynamicStopLoss) / scan.DailyAtr > OpportunityScoringConfig.MaxStopAtr
                || (scan.CurrentPrice - scan.DynamicStopLoss) / scan.CurrentPrice * 100m > OpportunityScoringConfig.MaxRiskPct))
            status = "Poor";
        if (reasons.Count == 0 && conservative is { } ratio)
        {
            var distance = (setup.Entry!.Value - setup.Stop!.Value) / scan!.DailyAtr;
            if (ratio < OpportunityScoringConfig.ActionableRewardRisk)
                status = "Poor";
            else if (distance < AdequateStopAtr || setup.Stop > support || setup.Target > resistance || ratio < GoodRewardRisk)
                status = "Caution";
            else status = "Good";
        }
        if (!classificationApproved) { status = "Unavailable"; reasons.Add("SetupPolicyApprovalRequired"); }
        return new(status, PolicyVersion, classificationApproved, setup.Currency, setup.Entry, scan?.DynamicStopLoss > 0m ? scan.DynamicStopLoss : null,
            support, supportSession, scan?.DailyAtr > 0m ? scan.DailyAtr : null, setup.Target, setup.TargetSource,
            resistance is { } credible && setup.Target is { } proposed ? Math.Min(credible, proposed) : null,
            resistance, resistanceSession, setup.RewardRisk, conservative, expectedSession, scan is null ? null : asOfUtc,
            reasons.Distinct(StringComparer.Ordinal).ToArray());
    }
}
