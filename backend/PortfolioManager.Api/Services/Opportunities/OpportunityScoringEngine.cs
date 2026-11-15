using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Services.Opportunities;

/// <summary>Everything a score may depend on. Ownership, cash and risk plans are deliberately absent: they can never change a score.</summary>
public sealed record OpportunityScoringInput(
    RsiScanResult? Scan,
    DateOnly ExpectedSession,
    SectorRelativeResult? Sector,
    TradeSetupResult Setup,
    ValueScreenerResult? Value,
    DateTime NowUtc,
    bool IdentityResolved = true,
    bool EvaluationFailed = false);

/// <summary>
/// Deterministic pm029-v2 Swing and pm029-v1 Strategic scoring (docs/pm-029-trading-opportunities.md §3). Pure: same input ⇒ same output.
/// Swing scores use the fixed S1–S5 denominator; unavailable evidence remains excluded from the available maximum.
/// </summary>
public static class OpportunityScoringEngine
{
    public static ScoreResult ScoreSwing(OpportunityScoringInput input) => Score(ScorePerspective.Swing, input);

    public static ScoreResult ScoreStrategic(OpportunityScoringInput input) => Score(ScorePerspective.Strategic, input);

    public static ScoreBand BandFor(int? score) => score switch
    {
        null => ScoreBand.None,
        >= OpportunityScoringConfig.StrongMin => ScoreBand.Strong,
        >= OpportunityScoringConfig.AttractiveMin => ScoreBand.Attractive,
        >= OpportunityScoringConfig.ModerateMin => ScoreBand.Moderate,
        >= OpportunityScoringConfig.CautionMin => ScoreBand.Caution,
        _ => ScoreBand.Weak,
    };

    private static ScoreResult Score(ScorePerspective perspective, OpportunityScoringInput input)
    {
        var version = perspective == ScorePerspective.Swing
            ? OpportunityScoringConfig.SwingVersion
            : OpportunityScoringConfig.StrategicVersion;
        if (!input.IdentityResolved) return Unavailable(perspective, version, OpportunityReasons.IdentityUnresolved);
        if (input.EvaluationFailed) return Unavailable(perspective, version, OpportunityReasons.EvaluationFailed);
        if (input.Scan is null) return Unavailable(perspective, version, OpportunityReasons.NoScan);
        if (input.Scan.TradingDate != input.ExpectedSession)
            return Unavailable(perspective, version, OpportunityReasons.StaleScan);
        if (perspective == ScorePerspective.Swing)
        {
            var freshnessReason = SwingFreshnessReason(input);
            if (freshnessReason is not null) return Unavailable(perspective, version, freshnessReason);
            if (input.Scan.CurrentPrice <= 0m)
                return Unavailable(perspective, version, OpportunityReasons.InvalidAnalysisPrice);
        }

        var components = perspective == ScorePerspective.Swing ? SwingComponents(input) : StrategicComponents(input);
        var awarded = components.Sum(c => c.Awarded);
        var available = components.Sum(c => c.AvailableMax);
        var denominator = perspective == ScorePerspective.Swing ? OpportunityScoringConfig.SwingDenominatorMax : available;
        var completeness = perspective == ScorePerspective.Swing
            ? available / OpportunityScoringConfig.SwingDenominatorMax * 100m
            : available;
        int? normalized = denominator > 0m
            ? (int)Math.Round(awarded / denominator * 100m, 0, MidpointRounding.AwayFromZero)
            : null;

        var reasons = new List<string>();
        foreach (var c in components.Where(c => c.Essential && c.State == ComponentState.Missing))
        {
            reasons.Add(OpportunityReasons.MissingEssential(c.Code));
            reasons.AddRange(c.MissingReasons);
        }
        if (completeness < OpportunityScoringConfig.MinCompletenessPct) reasons.Add(OpportunityReasons.LowCompleteness);
        foreach (var c in components.Where(c => !c.Essential && c.State == ComponentState.Missing))
            reasons.AddRange(c.MissingReasons);

        var essentialsOk = components.Where(c => c.Essential).All(c => c.State != ComponentState.Missing);
        var status = !essentialsOk || completeness < OpportunityScoringConfig.MinCompletenessPct ? ScoreStatus.Insufficient
            : completeness >= 100m ? ScoreStatus.Verified : ScoreStatus.Provisional;
        int? headline = status is ScoreStatus.Verified or ScoreStatus.Provisional ? normalized : null;
        return new ScoreResult(perspective, version, headline, normalized, awarded, available, completeness,
            status, BandFor(headline), components, reasons.Distinct(StringComparer.Ordinal).ToList(),
            perspective == ScorePerspective.Swing ? OpportunityScoringConfig.SwingDenominatorMax : null);
    }

    private static ScoreResult Unavailable(ScorePerspective perspective, string version, string reason) =>
        new(perspective, version, null, null, 0m, 0m, 0m, ScoreStatus.Unavailable, ScoreBand.None, [], [reason],
            perspective == ScorePerspective.Swing ? OpportunityScoringConfig.SwingDenominatorMax : null);

    private static string? SwingFreshnessReason(OpportunityScoringInput input)
    {
        var scan = input.Scan!;
        var nowUtc = AsUtc(input.NowUtc);
        var exchange = TradingCalendar.ExchangeFor(scan.AnalysisTicker, scan.AnalysisMarket);
        if (input.ExpectedSession != TradingCalendar.LastCompletedSession(nowUtc, exchange))
            return OpportunityReasons.ExpectedSessionMismatch;
        if (scan.ScannedAt == default) return OpportunityReasons.InvalidScanTimestamp;

        var scannedAtUtc = AsUtc(scan.ScannedAt);
        if (scannedAtUtc > nowUtc) return OpportunityReasons.ScanTimestampInFuture;
        if (scannedAtUtc < TradingCalendar.SessionCloseUtc(scan.TradingDate, exchange))
            return OpportunityReasons.ScanBeforeSessionClose;
        return null;
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // ── Swing (§3.1) ─────────────────────────────────────────────────────────

    private static List<ScoreComponent> SwingComponents(OpportunityScoringInput input)
    {
        var s = input.Scan!;
        return
        [
            S1Trend(s),
            S2RelativeStrength(input.Sector),
            S3Momentum(s),
            S4Volume(s),
            S5EntryQuality(s),
        ];
    }

    private static ScoreComponent S1Trend(RsiScanResult s)
    {
        const string code = "S1"; const string name = "Trend & Structure"; const decimal max = 25m;
        if (s.Ema20Price < 0m || s.Sma50Price < 0m)
            return Missing(code, name, max, true, OpportunityReasons.InvalidMovingAverage);
        if (s.Ema20Price == 0m || s.Sma50Price == 0m)
            return Missing(code, name, max, true, "MovingAveragesUnavailable");
        var p = s.CurrentPrice;
        var has200 = Has200(s);
        var sma200Reason = s.Sma200 < 0m || s.Has200Dma && s.Sma200 == 0m
            ? OpportunityReasons.InvalidSma200
            : "Sma200Unavailable";
        var items = new List<ScoreSubItem>
        {
            Item("PriceAboveEma20", "Price > EMA20", 7m, p > s.Ema20Price ? 7m : 0m),
            Item("Ema20AboveSma50", "EMA20 > SMA50", 6m, s.Ema20Price > s.Sma50Price ? 6m : 0m),
            Item("PriceAboveSma50", "Price > SMA50", 6m, p > s.Sma50Price ? 6m : 0m),
            has200 ? Item("PriceAboveSma200", "Price > SMA200", 6m, p > s.Sma200 ? 6m : 0m)
                   : Absent("PriceAboveSma200", "Price > SMA200", 6m, sma200Reason),
        };
        var hard = s.PriceStructure?.HasHardStructuralNegative == true;
        return Build(code, name, max, true, items, hard ? 0m : null, hard ? "Hard structural negative ⇒ 0" : null);
    }

    private static ScoreComponent S2RelativeStrength(SectorRelativeResult? sector)
    {
        const string code = "S2"; const string name = "Relative Strength"; const decimal max = 20m;
        var d20 = sector?.D20; var d60 = sector?.D60;
        if (d20?.IsAvailable != true && d60?.IsAvailable != true)
            return Missing(code, name, max, false, $"SectorHeat:{sector?.Status.ToString() ?? "NotEvaluated"}");
        var items = new List<ScoreSubItem>
        {
            d20?.IsAvailable == true
                ? Item("Rs20", "20D vs sector", 14m, Tier(d20.DisplayDiffPp!.Value, 14m, 11m, 8m, 5m, 2m), $"{d20.DisplayDiffPp:+0.0;-0.0;0.0} pp")
                : Absent("Rs20", "20D vs sector", 14m, $"SectorHeat20D:{d20?.Status}"),
            d60?.IsAvailable == true
                ? Item("Rs60", "60D vs sector", 6m, Rs60Swing(d60.DisplayDiffPp!.Value), $"{d60.DisplayDiffPp:+0.0;-0.0;0.0} pp")
                : Absent("Rs60", "60D vs sector", 6m, $"SectorHeat60D:{d60?.Status}"),
        };
        return Build(code, name, max, false, items);
    }

    /// <summary>≥+5 → a · ≥+2 → b · ≥+0.5 → c · neutral (|d| &lt; 0.5) → d · ≥−2 → e · else 0. Input is the displayed 0.1 pp value.</summary>
    internal static decimal Tier(decimal d, decimal a, decimal b, decimal c, decimal neutral, decimal e) =>
        d >= 5.0m ? a : d >= 2.0m ? b : d >= OpportunityScoringConfig.NeutralBandPp ? c
        : d > -OpportunityScoringConfig.NeutralBandPp ? neutral : d >= -2.0m ? e : 0m;

    private static decimal Rs60Swing(decimal d) =>
        d >= 2.0m ? 6m : d >= OpportunityScoringConfig.NeutralBandPp ? 4m : d > -OpportunityScoringConfig.NeutralBandPp ? 2m : 0m;

    private static ScoreComponent S3Momentum(RsiScanResult s)
    {
        const string code = "S3"; const string name = "RSI/MACD Momentum"; const decimal max = 15m;
        if (s.Rsi is < 0m or > 100m)
            return Missing(code, name, max, true, OpportunityReasons.InvalidRsi);

        var rsi = s.Rsi;
        var rsiPts = rsi is >= 50m and <= 65m ? 6m
            : rsi is >= 45m and < 50m || rsi is > 65m and <= 70m ? 4m
            : rsi is >= 40m and < 45m ? 2m
            : rsi > 70m ? 1m : 0m;
        var items = new List<ScoreSubItem>
        {
            Item("RsiZone", "RSI(14) zone", 6m, rsiPts, $"RSI {rsi:0.0}"),
            s.RsiSignalAvailable && s.RsiSignal is { } sig && sig is >= 0m and <= 100m
                ? Item("RsiAboveSignal", "RSI > RSI signal", 3m, rsi > sig ? 3m : 0m)
                : Absent("RsiAboveSignal", "RSI > RSI signal", 3m,
                    s.RsiSignalAvailable ? OpportunityReasons.InvalidRsiSignal : "RsiSignalUnavailable"),
            Item("Macd", "MACD histogram", 6m, MacdPoints(s.MacdHistogram, s.MacdHistDelta)),
        };
        return Build(code, name, max, true, items);
    }

    private static decimal MacdPoints(decimal hist, decimal delta) =>
        hist > 0m && delta > 0m ? 6m : hist > 0m ? 4m : delta > 0m ? 2m : 0m;

    private static ScoreComponent S4Volume(RsiScanResult s)
    {
        const string code = "S4"; const string name = "Volume Confirmation"; const decimal max = 10m;
        if (s.Volume < 0m) return Missing(code, name, max, false, OpportunityReasons.InvalidVolume);
        if (s.Volume == 0m) return Missing(code, name, max, false, "VolumeUnavailable");
        if (s.VolumeRatio < 0m) return Missing(code, name, max, false, OpportunityReasons.InvalidVolumeRatio);
        if (s.ChangePercent <= -100m) return Missing(code, name, max, false, OpportunityReasons.InvalidChangePercent);
        if (s.VolumeRatio <= 0m) return Missing(code, name, max, false, "VolumeUnavailable");
        var vr = s.VolumeRatio;
        var up = s.ChangePercent > 0m; var down = s.ChangePercent < 0m;
        var volPts = up ? (vr >= 1.5m ? 8m : vr >= 1.0m ? 6m : vr >= 0.7m ? 4m : 2m)
            : down ? (vr >= 1.5m ? 0m : vr >= 1.0m ? 2m : 4m)
            : 4m;
        var items = new List<ScoreSubItem>
        {
            Item("VolumeDirection", "Volume vs price direction", 8m, volPts, $"{vr:0.00}× avg, {(up ? "up" : down ? "down" : "flat")} day"),
            Item("VolumeValidated", "Validated volume on up day", 2m, up && s.VolumeSignal == "Validated" ? 2m : 0m),
        };
        return Build(code, name, max, false, items);
    }

    private static ScoreComponent S5EntryQuality(RsiScanResult s)
    {
        const string code = "S5"; const string name = "Entry Quality"; const decimal max = 10m;
        if (s.DailyAtr < 0m) return Missing(code, name, max, false, OpportunityReasons.InvalidAtr);
        if (s.DailyAtr <= 0m) return Missing(code, name, max, false, "AtrUnavailable");
        var items = new List<ScoreSubItem>();
        if (s.Ema20Price > 0m)
        {
            var x = (s.CurrentPrice - s.Ema20Price) / s.DailyAtr;
            var pts = x is >= -0.5m and <= 1.0m ? 6m : x is > 1.0m and <= 2.0m ? 3m : x > 2.0m ? 0m : x >= -1.5m ? 4m : 1m;
            items.Add(Item("Extension", "Distance to EMA20 (ATR)", 6m, pts, $"{x:0.00} ATR"));
        }
        else items.Add(Absent("Extension", "Distance to EMA20 (ATR)", 6m,
            s.Ema20Price < 0m ? OpportunityReasons.InvalidEma20 : "Ema20Unavailable"));
        items.Add(Item("ChaseRisk", "No elevated chase risk", 2m, s.ChaseRisk == "Elevated" ? 0m : 2m));
        items.Add(FibItem(s.FibZone, 2m, 1m));
        return Build(code, name, max, false, items);
    }

    private static ScoreSubItem FibItem(string? zone, decimal good, decimal shallow) => zone switch
    {
        "Normal Pullback" or "Value Zone" or "Key Fib Support" => Item("FibZone", "Fibonacci zone", good, good, zone),
        "Shallow Pullback" => Item("FibZone", "Fibonacci zone", good, shallow, zone),
        "Deep Pullback" or "Trend Damage" => Item("FibZone", "Fibonacci zone", good, 0m, zone),
        _ => Absent("FibZone", "Fibonacci zone", good, "FibUnavailable"),
    };

    // ── Strategic (§3.2) ─────────────────────────────────────────────────────

    private static List<ScoreComponent> StrategicComponents(OpportunityScoringInput input)
    {
        var s = input.Scan!;
        var (fundamentalsOk, fundamentalsReason) = FundamentalsUsable(input.Value, input.NowUtc);
        return
        [
            T1LongTermTrend(s),
            T2RelativeStrength(input.Sector),
            T3Momentum(s),
            T4Volume(s),
            T5EntryTiming(s),
            T6DownsideRisk(s),
            T7Valuation(input.Value, fundamentalsOk, fundamentalsReason),
            T8Quality(input.Value, fundamentalsOk, fundamentalsReason),
        ];
    }

    private static ScoreComponent T1LongTermTrend(RsiScanResult s)
    {
        const string code = "T1"; const string name = "Long-Term Trend"; const decimal max = 20m;
        if (!Has200(s)) return Missing(code, name, max, true, "Sma200Unavailable");
        var dev = s.Dma200Deviation;
        var items = new List<ScoreSubItem>
        {
            Item("PriceAboveSma200", "Price > SMA200", 8m, s.CurrentPrice > s.Sma200 ? 8m : 0m),
            s.Sma50Price > 0m
                ? Item("Sma50AboveSma200", "SMA50 > SMA200", 7m, s.Sma50Price > s.Sma200 ? 7m : 0m)
                : Absent("Sma50AboveSma200", "SMA50 > SMA200", 7m, "Sma50Unavailable"),
            Item("Sma200Deviation", "Distance above SMA200", 5m, dev < 0m ? 0m : dev <= 25m ? 5m : 2m, $"{dev:+0.0;-0.0;0.0} %"),
        };
        return Build(code, name, max, true, items);
    }

    private static ScoreComponent T2RelativeStrength(SectorRelativeResult? sector)
    {
        const string code = "T2"; const string name = "Relative Strength"; const decimal max = 10m;
        var d60 = sector?.D60;
        if (d60?.IsAvailable != true) return Missing(code, name, max, false, $"SectorHeat60D:{d60?.Status.ToString() ?? "NotEvaluated"}");
        return Build(code, name, max, false,
            [Item("Rs60", "60D vs sector", 10m, Tier(d60.DisplayDiffPp!.Value, 10m, 8m, 6m, 4m, 2m), $"{d60.DisplayDiffPp:+0.0;-0.0;0.0} pp")]);
    }

    private static ScoreComponent T3Momentum(RsiScanResult s)
    {
        var ms = s.MomentumState;
        var items = new List<ScoreSubItem>
        {
            Item("MacdPositive", "MACD histogram > 0", 3m, s.MacdHistogram > 0m ? 3m : 0m),
            string.IsNullOrWhiteSpace(ms)
                ? Absent("MomentumState", "Momentum state", 2m, "MomentumStateUnavailable")
                : Item("MomentumState", "Momentum state", 2m,
                    ms.Contains("Accelerating", StringComparison.Ordinal) || ms is "Positive" or "Active Buy Trigger" ? 2m : 0m, ms),
        };
        return Build("T3", "Momentum", 5m, false, items);
    }

    private static ScoreComponent T4Volume(RsiScanResult s)
    {
        const string code = "T4"; const string name = "Volume"; const decimal max = 5m;
        if (s.VolumeRatio <= 0m) return Missing(code, name, max, false, "VolumeUnavailable");
        var vr = s.VolumeRatio;
        // Spec: ≥1.0 on an up day → 5; <1.0 on any day → 3; ≥1.0 on a down day → 0. A flat day with ≥1.0 is treated as neutral (3).
        var pts = vr >= 1.0m ? (s.ChangePercent > 0m ? 5m : s.ChangePercent < 0m ? 0m : 3m) : 3m;
        return Build(code, name, max, false, [Item("VolumeDirection", "Volume vs price direction", 5m, pts, $"{vr:0.00}× avg")]);
    }

    private static ScoreComponent T5EntryTiming(RsiScanResult s)
    {
        var rsi = s.Rsi;
        var items = new List<ScoreSubItem>
        {
            Item("RsiZone", "RSI not extended", 4m, rsi is >= 40m and <= 60m ? 4m : rsi is >= 35m and < 40m || rsi is > 60m and <= 70m ? 2m : 0m, $"RSI {rsi:0.0}"),
        };
        if (s.Week52High > 0m)
        {
            var ratio = s.CurrentPrice / s.Week52High;
            items.Add(Item("Off52WHigh", "Price / 52-week high", 3m, ratio > 0.95m ? 1m : ratio >= 0.75m ? 3m : 2m, $"{ratio:0.00}"));
        }
        else items.Add(Absent("Off52WHigh", "Price / 52-week high", 3m, "Week52HighUnavailable"));
        items.Add(FibItem(s.FibZone, 3m, 1m));
        return Build("T5", "Entry Timing", 10m, false, items);
    }

    private static ScoreComponent T6DownsideRisk(RsiScanResult s)
    {
        const string code = "T6"; const string name = "Downside Risk"; const decimal max = 10m;
        if (s.DailyAtr <= 0m) return Missing(code, name, max, false, "AtrUnavailable");
        var items = new List<ScoreSubItem>();
        if (Has200(s))
        {
            var x = (s.CurrentPrice - s.Sma200) / s.DailyAtr;
            items.Add(Item("Sma200Cushion", "Distance to SMA200 (ATR)", 5m, x < 0m ? 0m : x <= 3m ? 5m : x <= 8m ? 3m : 1m, $"{x:0.0} ATR"));
        }
        else items.Add(Absent("Sma200Cushion", "Distance to SMA200 (ATR)", 5m, "Sma200Unavailable"));
        var atrPct = s.CurrentPrice > 0m ? s.DailyAtr / s.CurrentPrice * 100m : 0m;
        items.Add(Item("AtrPct", "ATR % of price", 5m, atrPct <= 2m ? 5m : atrPct <= 3.5m ? 3m : atrPct <= 5m ? 1m : 0m, $"{atrPct:0.00} %"));
        var hard = s.PriceStructure?.HasHardStructuralNegative == true;
        return Build(code, name, max, false, items, hard ? 0m : null, hard ? "Hard structural negative ⇒ 0" : null);
    }

    internal static (bool Ok, string? Reason) FundamentalsUsable(ValueScreenerResult? v, DateTime nowUtc)
    {
        if (v is null || v.FundamentalsDataAvailable == false) return (false, OpportunityReasons.FundamentalsMissing);
        if (v.F1Available is null || v.F4Available is null) return (false, OpportunityReasons.FundamentalsLegacy);
        var analyzed = v.AnalyzedAt.Kind == DateTimeKind.Local ? v.AnalyzedAt.ToUniversalTime() : DateTime.SpecifyKind(v.AnalyzedAt, DateTimeKind.Utc);
        if (nowUtc - analyzed > OpportunityScoringConfig.FundamentalsMaxAge) return (false, OpportunityReasons.FundamentalsStale);
        return (true, null);
    }

    private static ScoreComponent T7Valuation(ValueScreenerResult? v, bool ok, string? reason)
    {
        const string code = "T7"; const string name = "Valuation"; const decimal max = 25m;
        if (!ok) return Missing(code, name, max, true, reason!);
        var fallback = v!.MarketCapFallbackUsed == true;
        var isReit = v.Sector.Contains("Real Estate", StringComparison.OrdinalIgnoreCase) || v.Sector.Contains("REIT", StringComparison.OrdinalIgnoreCase);
        // FCF yield divides by market cap; the REIT P/FFO proxy estimates shares from it. An estimated market cap makes those unusable.
        var f1 = v.F1Available == true;
        var f2 = v.F2Available == true && !fallback;
        var f3 = v.F3Available == true && !(fallback && isReit);
        var factors = new List<ScoreSubItem>
        {
            f1 ? Item("F1", $"Earnings yield ({v.EarningsYieldBasis ?? "basis n/a"})", OpportunityScoringConfig.F1Max, v.ScoreEarningsYield)
               : Absent("F1", "Earnings yield", OpportunityScoringConfig.F1Max, "F1Unavailable"),
            f2 ? Item("F2", $"FCF yield ({v.FcfYieldBasis ?? "basis n/a"})", OpportunityScoringConfig.F2Max, v.ScoreFcfYield)
               : Absent("F2", "FCF yield", OpportunityScoringConfig.F2Max, fallback ? "MarketCapEstimated" : "F2Unavailable"),
            f3 ? Item("F3", isReit ? "Price / FFO proxy" : "Price / book", OpportunityScoringConfig.F3Max, v.ScorePriceToBook)
               : Absent("F3", "Price / book", OpportunityScoringConfig.F3Max, fallback && isReit ? "MarketCapEstimated" : "F3Unavailable"),
        };
        return Rescaled(code, name, max, true, factors, OpportunityScoringConfig.MinValuationFactors);
    }

    private static ScoreComponent T8Quality(ValueScreenerResult? v, bool ok, string? reason)
    {
        const string code = "T8"; const string name = "Business Quality"; const decimal max = 15m;
        if (!ok) return Missing(code, name, max, false, reason!);
        var f4 = v!.F4Available == true && (v.PiotroskiSignalsAvailable ?? 0) >= OpportunityScoringConfig.MinPiotroskiSignals;
        if (!f4) return Missing(code, name, max, false, "PiotroskiInsufficientSignals");
        var f5 = v.F5Available == true && v.CapitalEfficiencyBasis is "ROIC" or "ROE";
        var factors = new List<ScoreSubItem>
        {
            Item("F4", $"Fundamental health indicators ({v.PiotroskiSignalsAvailable}; proxy-based)", OpportunityScoringConfig.F4Max, v.ScorePiotroski),
            f5 ? Item("F5", v.CapitalEfficiencyBasis!, OpportunityScoringConfig.F5Max, v.ScoreRoic)
               : Absent("F5", "Capital efficiency", OpportunityScoringConfig.F5Max, v.CapitalEfficiencyBasis == "Proxy" ? "ProxyNotAccepted" : "F5Unavailable"),
        };
        return Rescaled(code, name, max, false, factors, 1);
    }

    /// <summary>Σfactor points / Σavailable factor max × component max. The component counts its whole max when enough factors exist.</summary>
    private static ScoreComponent Rescaled(string code, string name, decimal max, bool essential, List<ScoreSubItem> factors, int minFactors)
    {
        var available = factors.Where(f => f.Available).ToList();
        if (available.Count < minFactors)
            return new ScoreComponent(code, name, max, essential, ComponentState.Missing, 0m, 0m, factors,
                [OpportunityReasons.FundamentalsInsufficientFactors]);
        var factorMax = available.Sum(f => f.Max);
        var awarded = factorMax > 0m ? available.Sum(f => Math.Clamp(f.Points, 0m, f.Max)) / factorMax * max : 0m;
        var state = available.Count == factors.Count ? ComponentState.Observed : ComponentState.Partial;
        return new ScoreComponent(code, name, max, essential, state, awarded, max, factors, [],
            state == ComponentState.Partial ? $"Rescaled from {available.Count} of {factors.Count} factors" : null);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool Has200(RsiScanResult s) => s.Has200Dma && s.Sma200 > 0m;

    private static ScoreSubItem Item(string code, string label, decimal max, decimal points, string? detail = null) =>
        new(code, label, max, points, true, detail);

    private static ScoreSubItem Absent(string code, string label, decimal max, string reason) =>
        new(code, label, max, 0m, false, reason);

    private static ScoreComponent Missing(string code, string name, decimal max, bool essential, params string[] reasons) =>
        new(code, name, max, essential, ComponentState.Missing, 0m, 0m, [], reasons);

    private static ScoreComponent Build(string code, string name, decimal max, bool essential, List<ScoreSubItem> items,
        decimal? overrideAwarded = null, string? note = null)
    {
        var availableMax = items.Where(i => i.Available).Sum(i => i.Max);
        var awarded = overrideAwarded ?? Math.Min(items.Where(i => i.Available).Sum(i => i.Points), max);
        var state = items.All(i => i.Available) ? ComponentState.Observed : availableMax > 0m ? ComponentState.Partial : ComponentState.Missing;
        var missing = items.Where(i => !i.Available).Select(i => i.Detail ?? i.Code).ToList();
        return new ScoreComponent(code, name, max, essential, state, state == ComponentState.Missing ? 0m : awarded,
            state == ComponentState.Missing ? 0m : availableMax, items, missing, note);
    }
}
