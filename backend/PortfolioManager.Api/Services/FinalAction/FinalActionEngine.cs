using System.Globalization;

namespace PortfolioManager.Api.Services.FinalAction;

/// <summary>
/// Scan inputs the Final Action rules read. Doubles on purpose: the original rules run on JavaScript
/// numbers, and threshold comparisons must behave identically.
/// </summary>
public sealed record FinalActionInput
{
    public string Symbol { get; init; } = "";
    public double Rsi { get; init; }
    public double CurrentPrice { get; init; }
    public double ChangePercent { get; init; }
    public double DayHigh { get; init; }
    public double DayLow { get; init; }
    public double OpenPrice { get; init; }
    public double PreviousClose { get; init; }
    public double VolumeRatio { get; init; }
    public double? RsiSignal { get; init; }
    public bool RsiSignalAvailable { get; init; }
    public double MacdHistogram { get; init; }
    public double MacdHistDelta { get; init; }
    public double Ema9 { get; init; }
    public double Ema10 { get; init; }
    public double Ema20 { get; init; }
    public double Sma20 { get; init; }
    public double Sma50 { get; init; }
    public double Sma200 { get; init; }
    /// <summary>Confirmed | EodConfirm | EarlyWarning | Neutral</summary>
    public string Status { get; init; } = "";
    /// <summary>Oversold | Overbought</summary>
    public string ScanType { get; init; } = "";
    public double AnalystTargetPrice { get; init; }
    public double Fib50 { get; init; }
    public double Fib61_8 { get; init; }
    public double Fib78_6 { get; init; }
    public string FibZone { get; init; } = "";
    public string FibStatus { get; init; } = "";
    public double? RsiDelta1D { get; init; }
    public string TrendSetup200 { get; init; } = "";
    public string TrendShift { get; init; } = "";
    public bool ChaseRisk { get; init; }
    public double Week52High { get; init; }
    public FinalActionPriceStructure? PriceStructure { get; init; }
}

public sealed record FinalActionPriceStructure(
    bool HasHardStructuralNegative,
    string KeyLevelRole,
    string KeyLevelState,
    string PrimaryPatternState,
    string PrimaryPatternType);

/// <summary>Optional Value Screener context for the Watchlist rules.</summary>
public sealed record WatchlistValueContext(double? ValueScore = null, bool ValueTrapWarning = false, int? BuyScore = null);

/// <summary>Account / position facts used by the Portfolio profit-taking overrides.</summary>
public sealed record PortfolioItemContext(
    string? AccountType = null,
    double? UnrealizedGainPct = null,
    double? HoldingDays = null,
    double? DistanceFrom52WeekHighPct = null,
    double? PositionSizePct = null,
    double? RiskControlClosePct = null,
    string? DecisionSource = null);

public sealed record GapResult(string Status, int Score, double GapPct);

public sealed record WatchlistEntryDecision(
    string FinalAction,
    string FinalActionReason,
    bool HasHardStructuralNegative,
    string? HardNegativeReason,
    string PriceStructureState,
    string MomentumState,
    string MaStructure,
    int? BuyScore,
    string Role,
    bool EntryBlockedByResistance,
    bool EntryBlockedByHardNegative,
    bool EntryBlockedByMomentum,
    bool EntryBlockedByRoleConfirmation);

/// <summary>Canonical result of one Final Action evaluation.</summary>
public sealed record FinalActionDecision(
    string FinalAction,
    string TrendSetup,
    string TrendSetupReason,
    string MomentumShift,
    string MomentumShiftReason,
    string BaseAction,
    string GapStatus,
    int GapScore,
    double GapPct,
    string HoverDescription,
    string FinalActionClass,
    string TrendSetupClass,
    string MomentumShiftClass,
    string? FinalActionReason,
    WatchlistEntryDecision? WatchlistDiagnostics);

/// <summary>
/// Backend port of the Angular DecisionEngineService. Pure and stateless. The rules, thresholds and
/// strings are copied verbatim from <c>decision-engine.service.ts</c>, which stays the authoritative
/// specification until the parity suite has proven equivalence. Do not "improve" rules here.
/// </summary>
public static class FinalActionEngine
{
    // ── Public entry points ───────────────────────────────────────────────────

    public static FinalActionDecision TranslateForWatchlist(FinalActionInput r, string? role, WatchlistValueContext? valueCtx = null)
    {
        var dec = CalculateDecision(r, role);
        var effectiveRole = role ?? "Strategic";
        int? buyScore = valueCtx?.BuyScore ?? CalcBuyScore(r);
        var valueTrapWarning = valueCtx?.ValueTrapWarning ?? false;
        var valueScore = valueCtx?.ValueScore;

        var isValueTrap = valueTrapWarning || (valueScore is not null && valueScore < 5);
        var gap = CalcGapStatus(r.OpenPrice, r.PreviousClose, r.CurrentPrice, r.DayHigh, r.DayLow);
        var maStructure = DeriveMaStructure(r);
        var wd = EvaluateWatchlistEntry(new WatchlistEntryContext(
            Role: effectiveRole,
            Rsi: r.Rsi,
            BuyScore: buyScore,
            TrendSetup: dec.TrendSetup,
            MomentumShift: dec.MomentumShift,
            MomentumState: dec.MomentumShift,
            MaStructure: maStructure,
            PriceStructure: r.PriceStructure,
            ChaseRisk: r.ChaseRisk || gap.Status != "No Gap",
            TrendDamage: r.FibZone == "Trend Damage",
            ValueTrap: isValueTrap));

        return new FinalActionDecision(
            wd.FinalAction, dec.TrendSetup, dec.TrendSetupReason, dec.MomentumShift, dec.MomentumShiftReason,
            dec.BaseAction, gap.Status, gap.Score, gap.GapPct,
            wd.FinalActionReason, FinalActionClass(wd.FinalAction),
            TrendSetupClass(dec.TrendSetup), MomentumShiftClass(dec.MomentumShift),
            wd.FinalActionReason, wd);
    }

    /// <remarks>Angular only ever calls this with isOwned = true; the unowned branch is unreachable and not ported.</remarks>
    public static FinalActionDecision TranslateForPortfolio(FinalActionInput r, string? role, PortfolioItemContext? context = null)
    {
        var dec = CalculateDecision(r, role);
        var effectiveRole = role ?? "Strategic";
        var rawAction = PortfolioFinalAction(dec, effectiveRole, r);

        if (TfsaProfitTakeTriggered(r, context)) rawAction = "Take Partial Profit / No Chase";

        var profitAction = ProfitTakingAction(dec, context);
        if (profitAction is not null) rawAction = profitAction;

        if (context?.DecisionSource == "Risk Control - Trim") rawAction = "Hold Runner";

        if (profitAction is not null && context?.RiskControlClosePct is double rc && rc > 0)
            rawAction = RiskControlAdjustedAction(profitAction, rc);

        var gap = CalcGapStatus(r.OpenPrice, r.PreviousClose, r.CurrentPrice, r.DayHigh, r.DayLow);
        var finalAction = AccumulateStarterGuard(rawAction, dec.TrendSetup, dec.MomentumShift, r.ChangePercent);
        finalAction = ApplyGapRules(finalAction, gap, null, null);
        finalAction = ApplyFibonacciModifier(finalAction, r, role);

        return new FinalActionDecision(
            finalAction, dec.TrendSetup, dec.TrendSetupReason, dec.MomentumShift, dec.MomentumShiftReason,
            dec.BaseAction, gap.Status, gap.Score, gap.GapPct,
            PortfolioHover(dec, effectiveRole), FinalActionClass(finalAction),
            TrendSetupClass(dec.TrendSetup), MomentumShiftClass(dec.MomentumShift), null, null);
    }

    public static FinalActionDecision TranslateForRsiScanner(FinalActionInput r)
    {
        var dec = CalculateDecision(r, null);
        var gap = CalcGapStatus(r.OpenPrice, r.PreviousClose, r.CurrentPrice, r.DayHigh, r.DayLow);
        var finalAction = ApplyGapRules(dec.BaseAction, gap, null, null);
        finalAction = ApplyFibonacciModifier(finalAction, r, null);
        return new FinalActionDecision(
            finalAction, dec.TrendSetup, dec.TrendSetupReason, dec.MomentumShift, dec.MomentumShiftReason,
            dec.BaseAction, gap.Status, gap.Score, gap.GapPct,
            dec.MomentumShiftReason, BaseActionClass(dec.BaseAction),
            TrendSetupClass(dec.TrendSetup), MomentumShiftClass(dec.MomentumShift), null, null);
    }

    // ── Core decision (trend setup / momentum shift / base action) ───────────

    private sealed record Decision(string TrendSetup, string TrendSetupReason, string MomentumShift, string MomentumShiftReason, string BaseAction);

    private static Decision CalculateDecision(FinalActionInput r, string? role)
    {
        var ctx = BuildContext(r);
        var trendSetup = CalcTrendSetup(r, role, ctx);
        var momentumShift = CalcMomentumShift(r, ctx, trendSetup);
        return new Decision(trendSetup, TrendSetupReason(trendSetup, r), momentumShift, MomentumShiftReason(momentumShift, r),
            CalcBaseAction(momentumShift));
    }

    private sealed record Ctx(
        double DayHigh, double DayLow, double NormalizedClose, bool TopHalfClose, bool BottomHalfClose,
        double Vol, bool HighVolume, bool MacdImproving, bool MacdWeakening, bool OversoldContext, bool OverboughtContext, double Sig);

    private static Ctx BuildContext(FinalActionInput r)
    {
        var close = r.CurrentPrice;
        var rsi = r.Rsi;
        var sig = r.RsiSignal ?? rsi;

        var dayHigh = r.DayHigh > 0 ? r.DayHigh : 0;
        var dayLow = r.DayLow > 0 ? r.DayLow : 0;

        if (dayHigh == 0 || dayLow == 0)
        {
            var chgPct = r.ChangePercent;
            if (chgPct > 1.0)
            {
                dayHigh = close;
                dayLow = close * (1 - Math.Abs(chgPct) / 100 * 1.5);
            }
            else if (chgPct < -1.0)
            {
                dayLow = close;
                dayHigh = close * (1 + Math.Abs(chgPct) / 100 * 1.5);
            }
            else
            {
                dayHigh = close;
                dayLow = close;
            }
        }

        var range = dayHigh - dayLow;
        double normalizedClose;
        bool topHalf, bottomHalf;
        if (range > 0)
        {
            normalizedClose = (close - dayLow) / range;
            topHalf = normalizedClose > 0.5;
            bottomHalf = normalizedClose <= 0.5;
        }
        else
        {
            normalizedClose = 0.5;
            topHalf = false;
            bottomHalf = false;
        }

        var vol = r.VolumeRatio;
        return new Ctx(
            dayHigh, dayLow, normalizedClose, topHalf, bottomHalf, vol, vol >= 1.3,
            r.MacdHistDelta > 0, r.MacdHistDelta < 0,
            rsi < 35 || (r.Status == "Confirmed" && r.ScanType == "Oversold"),
            rsi > 65 || (r.Status == "Confirmed" && r.ScanType == "Overbought"),
            sig);
    }

    private static int? CalcBuyScore(FinalActionInput r)
    {
        var close = r.CurrentPrice;
        var ema9 = r.Ema9;
        var rsi = r.Rsi;
        var rsiSig = r.RsiSignal ?? rsi;
        var macdImproving = r.MacdHistDelta > 0;
        var dayH = r.DayHigh > 0 ? r.DayHigh : close;
        var dayL = r.DayLow > 0 ? r.DayLow : close;
        var range = dayH - dayL;
        var closeLocation = range > 0 ? (close - dayL) / range : 0.5;
        var vol = r.VolumeRatio;

        var score = 0;
        if (ema9 > 0 && close > ema9) score++;
        if (rsi > rsiSig) score++;
        if (macdImproving) score++;
        if (closeLocation >= 0.5) score++;
        if (vol >= 1.0) score++;
        return score;
    }

    private static string CalcTrendSetup(FinalActionInput r, string? role, Ctx ctx)
    {
        double rsi = r.Rsi, sig = ctx.Sig, close = r.CurrentPrice;
        double sma50 = r.Sma50, ema9 = r.Ema9, ema10 = r.Ema10, ema20 = r.Ema20;
        var rsiAvail = r.RsiSignalAvailable;

        if (rsi > 55 && rsi <= 70 && ema10 > 0 && ema20 > 0)
        {
            var pullbackIndicators = ctx.MacdWeakening || (ema9 > 0 && close < ema9) || ema10 < ema20 * 1.005;
            if (pullbackIndicators)
            {
                var bs = CalcBuyScore(r);
                var cl = ctx.NormalizedClose;
                var priceBelowEma9 = ema9 > 0 && close < ema9;
                if (bs is not null)
                {
                    if (bs <= 2 || priceBelowEma9 || cl < 0.5) return "Overbought-Weak";
                    if (bs == 3) return "Overbought-Resetting";
                    if (bs >= 4) return "Reclaim";
                }
            }
        }

        if (rsi < 35 && rsiAvail && rsi < sig && sma50 > 0 && close < sma50) return "Waterfall / Falling Knife";
        if (rsi < 35 && rsiAvail && rsi >= sig) return "Oversold Reversal Watch";
        if (ema10 > 0 && ema20 > 0 && sma50 > 0 && ema10 > ema20 && close > sma50 && rsi > 70) return "Constructive Extended";
        var isQualityRole = role is "Core" or "Strategic" or "Swing";
        if (isQualityRole && ema10 > 0 && ema20 > 0 && sma50 > 0 && ema10 > ema20 && close > sma50 && rsi >= 50 && rsi <= 65)
            return "Quality Trend Entry";
        if (ema10 > 0 && ema20 > 0 && sma50 > 0 && ema10 > ema20 && close > sma50 && rsi <= 70) return "Confirmed Constructive";
        if (ema10 > 0 && ema20 > 0 && sma50 > 0 && ema10 > ema20 && close < sma50 && rsi >= 30 && rsi <= 55) return "Early Reversal";
        if (ema10 > 0 && ema20 > 0 && sma50 > 0 && ema10 < ema20 && close > sma50) return "Cooling";
        if (ema10 > 0 && ema20 > 0 && sma50 > 0 && ema10 < ema20 && close < sma50 && rsi >= 35) return "Technical Caution";
        return "Neutral / No Setup";
    }

    private static string TrendSetupReason(string ts, FinalActionInput r) => ts switch
    {
        "Waterfall / Falling Knife" => $"Deeply oversold (RSI {F1(r.Rsi)}) with RSI still below signal line and price below SMA50. Selling active — avoid.",
        "Oversold Reversal Watch" => $"RSI {F1(r.Rsi)} crossed above signal from oversold. Potential reversal — watch for candle confirmation.",
        "Constructive Extended" => $"Trend intact (EMA10>EMA20, price>SMA50) but RSI {F1(r.Rsi)}>70. Extended — do not chase.",
        "Quality Trend Entry" => $"Core/Strategic: healthy uptrend, RSI {F1(r.Rsi)} in 50-65 constructive zone. Suitable for staged entry.",
        "Confirmed Constructive" => $"EMA10>EMA20, price above SMA50, RSI {F1(r.Rsi)} — trend intact and not extended.",
        "Early Reversal" => "Short-term momentum turning up (EMA10>EMA20) but price still below SMA50. Watch for reclaim.",
        "Cooling" => "Short-term momentum fading (EMA10<EMA20), price still above SMA50. Pause on new buying.",
        "Technical Caution" => "EMA10<EMA20 and price<SMA50. RSI above 35 — no panic, but technically weak.",
        "Overbought-Weak" => "Overbought Pullback – Weak: BuyScore low, price below EMA9 or weak close. Stand by — no new buying.",
        "Overbought-Resetting" => "Overbought Resetting: BuyScore moderate, price near EMA9. Watch for confirmation before entering.",
        "Reclaim" => "Reclaim: BuyScore strong, price above EMA9, RSI constructive, MACD improving. Accumulate starter position.",
        _ => "Insufficient indicator data for setup classification.",
    };

    private static string CalcMomentumShift(FinalActionInput r, Ctx ctx, string ts)
    {
        double rsi = r.Rsi, sig = ctx.Sig, close = r.CurrentPrice;
        double ema9 = r.Ema9, ema10 = r.Ema10, ema20 = r.Ema20, sma20 = r.Sma20, sma50 = r.Sma50;
        var rsiAvail = r.RsiSignalAvailable;

        var waterfallBlock = ts == "Waterfall / Falling Knife" && rsiAvail && rsi < sig && !ctx.HighVolume;
        if (ctx.OversoldContext && ctx.TopHalfClose && (ctx.HighVolume || ctx.MacdImproving) && !waterfallBlock)
            return "Active Buy Trigger";

        if (ctx.OverboughtContext && ctx.BottomHalfClose && (ctx.HighVolume || ctx.MacdWeakening))
            return "Active Sell Trigger";

        if (rsi < 35 && rsiAvail && rsi < sig) return "Warning";
        if (rsi < 35 && rsiAvail && rsi >= sig) return "Bullish Shift";
        if (rsi > 65 && rsiAvail && rsi <= sig) return "Bearish Shift";
        if (ctx.OverboughtContext) return "Warning — Overbought Run";
        if (ema9 > 0 && close < ema9 && rsi < 40) return "Breakdown";

        if (sma50 > 0 && close > sma50 && ema10 > 0 && ema20 > 0 && ema10 > ema20 && rsi >= 40 && rsi <= 55
            && sma20 > 0 && Math.Abs(close - sma20) / sma20 <= 0.01 && ctx.TopHalfClose && ctx.Vol >= 1.2)
            return "Consolidation / Dip-Buy";

        if (ema9 > 0 && close > ema9 && rsi >= 50 && rsi <= 65 && rsiAvail && rsi > sig && ctx.MacdImproving)
            return "Uptrend";

        if (sma50 > 0 && close > sma50 && rsi >= 50 && rsi <= 65) return "Neutral / Weakening";
        return "Neutral";
    }

    private static string MomentumShiftReason(string ms, FinalActionInput r) => ms switch
    {
        "Active Buy Trigger" => "Buyers confirmed: candle closed in upper half of range from oversold with volume or MACD improvement. High-probability entry.",
        "Active Sell Trigger" => "Distribution confirmed: candle closed in lower half of range from overbought with volume or MACD weakening. Bearish reversal in progress.",
        "Warning" => $"RSI {F1(r.Rsi)} deeply oversold but still below signal line — waterfall selling may continue. Avoid entry.",
        "Warning — Overbought Run" => $"RSI {F1(r.Rsi)} elevated/overbought but candle has NOT confirmed bearish reversal (no bottom-half close + confirmation). Extended — do not chase.",
        "Bullish Shift" => $"RSI {F1(r.Rsi)} crossed above signal from oversold. Selling pressure easing — watch for candle confirmation.",
        "Bearish Shift" => $"RSI {F1(r.Rsi)} crossed below signal from overbought. Buying momentum fading — watch for follow-through.",
        "Breakdown" => "Price broke below EMA9 with RSI fading below 40. Short-term structure deteriorated. Defensive risk.",
        "Consolidation / Dip-Buy" => "Price near 20-day SMA, RSI 40-50, volume elevated. Institutional dip-buy zone confirmed.",
        "Uptrend" => "Short-term trend healthy — price above EMA9, RSI constructive, MACD improving. Hold existing. No new chase.",
        "Neutral / Weakening" => "Price above SMA50 but short-term confirmation signals missing (EMA9, RSI9EMA or MACD). Stand by.",
        _ => "RSI neutral zone. No directional confirmation. Stand by.",
    };

    private static string CalcBaseAction(string ms) => ms switch
    {
        "Active Buy Trigger" => "Confirmed Buy Signal",
        "Active Sell Trigger" => "Confirmed Sell Signal",
        "Bullish Shift" => "Early Buy Watch",
        "Bearish Shift" => "Early Sell Watch",
        "Warning" => "Avoid / Wait",
        "Warning — Overbought Run" => "Watch / Do Not Chase",
        "Breakdown" => "Reduce / Review",
        "Consolidation / Dip-Buy" => "Buy / Accumulate",
        "Uptrend" => "Hold Longs",
        _ => "Stand By",
    };

    // ── Gap, guards and Fibonacci modifier ───────────────────────────────────

    public static GapResult CalcGapStatus(double openPrice, double previousClose, double close, double high, double low)
    {
        if (previousClose <= 0) return new GapResult("No Gap", 0, 0);
        var gapPct = (openPrice - previousClose) / previousClose * 100;
        var range = high - low;
        var closeLocation = range > 0 ? (close - low) / range : 0.5;

        if (gapPct < 1.5) return new GapResult("No Gap", 0, gapPct);
        if (close > openPrice && closeLocation >= 0.7) return new GapResult("Gap Up - Strong", 2, gapPct);
        if (close >= openPrice && closeLocation >= 0.5 && closeLocation < 0.7) return new GapResult("Gap Up - Weak", 1, gapPct);
        if (close < openPrice && closeLocation <= 0.4) return new GapResult("Gap Up - Failed", -2, gapPct);
        return new GapResult("Gap Up - Weak", 1, gapPct);
    }

    private static string AccumulateStarterGuard(string action, string ts, string ms, double changePercent)
    {
        if (action != "Accumulate Starter") return action;
        var overbought = ts is "Constructive Extended" or "Overbought-Weak" or "Overbought-Resetting"
            || ms == "Warning — Overbought Run" || changePercent > 5;
        return overbought ? "Watch / No Chase" : action;
    }

    private static string ApplyGapRules(string action, GapResult gap, int? buyScore, double? valueScore)
    {
        if (gap.Status == "No Gap") return action;

        var isBuyAction = action is "Confirmed Buy Signal" or "Buy / Accumulate" or "Accumulate Starter"
            or "Early Buy Watch" or "Watch / Starter OK";

        if (gap.Status == "Gap Up - Failed") return isBuyAction ? "Watch / Gap Failed — No Buy" : action;

        if (gap.Status == "Gap Up - Weak")
            return action is "Confirmed Buy Signal" or "Buy / Accumulate" ? "Watch / Starter Only (Gap Weak)" : action;

        if (gap.Status == "Gap Up - Strong")
        {
            var hasFollowThrough = (buyScore ?? 0) >= 4 && (valueScore is null || valueScore >= 5);
            if (!hasFollowThrough && action is "Confirmed Buy Signal" or "Buy / Accumulate")
                return "Watch / Gap Strong (Confirm Setup)";
            return action;
        }
        return action;
    }

    private static string ApplyFibonacciModifier(string action, FinalActionInput r, string? role)
    {
        double fib61_8 = r.Fib61_8, fib78_6 = r.Fib78_6, fib50 = r.Fib50;
        if (fib61_8 == 0) return action;

        var price = r.CurrentPrice;
        var zone = r.FibZone;
        var status = r.FibStatus;
        var rsiDelta = r.RsiDelta1D ?? 0;
        var rsiImproving = rsiDelta > 0;
        var bullTurn = r.TrendShift.Contains("Bull Turn", StringComparison.Ordinal);
        var aboveSma200 = r.Sma200 > 0 && price > r.Sma200;

        var isStrategicOrCore = role is "Strategic" or "Core";
        var isSwingTactical = role is "Swing" or "Speculative";

        if (r.TrendShift.Contains("Still Falling", StringComparison.Ordinal) && price < fib61_8
            && action is "Confirmed Buy Signal" or "Buy / Accumulate" or "Accumulate Starter")
            return "Wait";

        if (price < fib78_6 && rsiDelta < 0
            && action is "Confirmed Buy Signal" or "Buy / Accumulate" or "Accumulate Starter" or "Early Buy Watch" or "Watch / Starter OK")
            return "Avoid / Trend Damage";

        if (action == "Buy / Accumulate" && price < fib78_6 && !rsiImproving) return "Wait";

        if ((action == "Watch / Starter OK" || action == "Early Buy Watch")
            && (zone == "Value Zone" || zone == "Key Fib Support") && rsiImproving)
            return isStrategicOrCore ? "Accumulate Starter" : action;

        if (action == "Accumulate Starter" && bullTurn && status == "Reclaimed 61.8") return "Buy / Accumulate";

        if (fib50 > 0 && price >= fib61_8 && price <= fib50 && rsiImproving && aboveSma200
            && (action == "Accumulate Starter" || action == "Watch / Starter OK"))
            return isSwingTactical ? "Buy / Accumulate" : "Accumulate Starter";

        if (bullTurn && status == "Reclaimed 61.8" && aboveSma200
            && action is "Accumulate Starter" or "Watch / Starter OK" or "Early Buy Watch")
            return isSwingTactical ? "Buy / Accumulate" : "Accumulate Starter";

        if (isStrategicOrCore
            && (zone == "Value Zone" || zone == "Key Fib Support") && bullTurn && aboveSma200
            && action is "Watch / Starter OK" or "Stand By")
            return "Accumulate Starter";

        if (isSwingTactical
            && bullTurn && (zone == "Value Zone" || zone == "Key Fib Support" || status == "Reclaimed 61.8")
            && action is "Watch / Starter OK" or "Accumulate Starter")
            return "Buy / Accumulate";

        return action;
    }

    // ── Watchlist entry status ───────────────────────────────────────────────

    private sealed record WatchlistEntryContext(
        string? Role, double? Rsi, int? BuyScore, string? TrendSetup, string? MomentumShift, string? MomentumState,
        string? MaStructure, FinalActionPriceStructure? PriceStructure, bool ChaseRisk, bool TrendDamage, bool ValueTrap);

    private static readonly string[] HardLevelStates =
    [
        "FAILED_BREAKOUT", "BREAKOUT_FAILED", "SUPPORT_BROKEN", "BREAKDOWN_CONFIRMED", "CHANNEL_BROKEN",
        "CHANNEL_SUPPORT_BROKEN", "WEDGE_BREAKDOWN", "TIGHT_WEDGE_BREAKDOWN", "RISING_WEDGE_BREAKDOWN",
        "TIGHT_RISING_WEDGE_BREAKDOWN",
    ];

    private static WatchlistEntryDecision EvaluateWatchlistEntry(WatchlistEntryContext c)
    {
        var structure = c.PriceStructure;
        var levelState = structure?.KeyLevelState ?? "NONE";
        var patternState = structure?.PrimaryPatternState ?? "NONE";
        var patternType = structure?.PrimaryPatternType ?? "NONE";
        var role = c.Role ?? "Strategic";
        var momentum = c.MomentumState ?? c.MomentumShift ?? "Neutral";
        var buyScore = c.BuyScore;

        var hardNegative = structure?.HasHardStructuralNegative == true
            || HardLevelStates.Contains(levelState)
            || patternState == "CHANNEL_BROKEN"
            || (patternState == "BREAKDOWN" && patternType.Contains("WEDGE", StringComparison.Ordinal));
        string? hardNegativeReason = hardNegative
            ? (levelState != "NONE" && HardLevelStates.Contains(levelState) ? levelState : $"{patternType}_{patternState}")
            : null;

        var declining = new[] { "Declining", "Warning", "Breakdown", "Active Sell Trigger", "Bearish Shift" }
            .Any(s => momentum.Contains(s, StringComparison.Ordinal));
        var accelerating = momentum.Contains("Accelerating", StringComparison.Ordinal) || momentum == "Active Buy Trigger";
        var positive = accelerating || momentum.Contains("Positive", StringComparison.Ordinal)
            || momentum.Contains("Bullish Shift", StringComparison.Ordinal) || momentum == "Uptrend";
        var resistance = structure?.KeyLevelRole == "RESISTANCE"
            && levelState is "RESISTANCE_TEST" or "APPROACHING_RESISTANCE" or "BREAKOUT_WATCH";
        var confirmedConstructive = levelState is "BREAKOUT_CONFIRMED" or "SUPPORT_RECLAIM" or "BOUNCE_CONFIRMED"
            || patternState == "BREAKOUT";
        var supportConstructive = structure?.KeyLevelRole == "SUPPORT"
            && levelState is "SUPPORT_TEST" or "SUPPORT_RECLAIM" or "BOUNCE_CONFIRMED" or "APPROACHING_SUPPORT";
        var compression = patternType.Contains("FALLING_WEDGE", StringComparison.Ordinal)
            && patternState is "DEVELOPING" or "TIGHTENING" or "NEAR_APEX";
        var extended = c.ChaseRisk || c.TrendSetup == "Constructive Extended" || c.MomentumShift == "Warning — Overbought Run";
        var acceptableMa = string.IsNullOrEmpty(c.MaStructure)
            || c.MaStructure.StartsWith("P >", StringComparison.Ordinal) || c.MaStructure == "50 > P > 200";
        var severelyBearishMa = c.MaStructure == "200 > 50 > P";
        var oversold = c.Rsi is not null && c.Rsi <= 35;
        var earlyReversal = oversold && (positive || c.TrendSetup == "Oversold Reversal Watch" || c.TrendSetup == "Early Reversal");
        var roleAllowsStarter = role is "Core" or "Strategic" or "Strategic-Income";
        var roleNeedsMoreConfirmation = role is "Swing" or "Speculative" or "Options";

        string finalAction, reason;
        if (hardNegative)
        {
            finalAction = declining || severelyBearishMa || (buyScore is not null && buyScore <= 2) ? "AVOID" : "WAIT FOR RECLAIM";
            reason = $"{hardNegativeReason} remains active; constructive indicators cannot override confirmed structural damage.";
        }
        else if (resistance)
        {
            finalAction = "WATCH / NO CHASE";
            reason = "Price is at active resistance without a confirmed breakout.";
        }
        else if (extended)
        {
            finalAction = resistance ? "WATCH / NO CHASE" : "WAIT FOR PULLBACK";
            reason = "Trend may be healthy, but current price location is extended for a new entry.";
        }
        else if (declining)
        {
            finalAction = c.TrendDamage ? "WAIT FOR RECLAIM" : "WAIT FOR REVERSAL";
            reason = "Momentum is declining and reversal confirmation is not complete.";
        }
        else if (confirmedConstructive && accelerating && (buyScore ?? 0) >= 4 && acceptableMa && !c.TrendDamage && !c.ValueTrap)
        {
            finalAction = role == "Speculative" && !(structure?.KeyLevelState.Contains("BREAKOUT", StringComparison.Ordinal) ?? false)
                ? "BUY WATCH" : "ENTRY CANDIDATE";
            reason = "Structure and momentum are confirmed with a strong Buy Score and acceptable MA structure.";
        }
        else if (roleAllowsStarter && (buyScore ?? 0) >= 4 && (confirmedConstructive || supportConstructive) && positive && acceptableMa)
        {
            finalAction = "STARTER ENTRY";
            reason = "Constructive support and momentum allow a staged initial position for this role.";
        }
        else if ((buyScore ?? 0) >= 4 && (supportConstructive || compression || confirmedConstructive) && !c.TrendDamage)
        {
            finalAction = "BUY WATCH";
            reason = "The setup is promising, but one or more entry confirmations are still missing.";
        }
        else if (earlyReversal)
        {
            finalAction = "REVERSAL WATCH";
            reason = "Oversold conditions are improving, but bullish structure is not yet confirmed.";
        }
        else if (c.TrendDamage || severelyBearishMa)
        {
            finalAction = buyScore is not null && buyScore <= 2 ? "AVOID" : "WAIT FOR RECLAIM";
            reason = "Damaged trend or MA structure requires a confirmed recovery before entry.";
        }
        else if (positive && acceptableMa)
        {
            finalAction = "WAIT FOR PULLBACK";
            reason = "The trend is constructive but there is no attractive current decision level.";
        }
        else
        {
            finalAction = "WATCH";
            reason = "No meaningful confirmed entry setup is active.";
        }

        return new WatchlistEntryDecision(
            finalAction, reason, hardNegative, hardNegativeReason,
            levelState != "NONE" ? levelState : $"{patternType}_{patternState}",
            momentum, c.MaStructure ?? "Unavailable", buyScore, role,
            resistance, hardNegative, declining, roleNeedsMoreConfirmation && finalAction == "BUY WATCH");
    }

    private static string? DeriveMaStructure(FinalActionInput r)
    {
        var price = r.CurrentPrice;
        var sma50 = r.Sma50;
        var sma200 = r.Sma200;
        if (price <= 0 || sma50 <= 0 || sma200 <= 0) return null;
        if (price > sma50 && sma50 > sma200) return "P > 50 > 200";
        if (price > sma200 && sma200 > sma50) return "P > 200 > 50";
        if (sma200 > price && price > sma50) return "200 > P > 50";
        if (sma50 > price && price > sma200) return "50 > P > 200";
        return sma50 > sma200 ? "50 > 200 > P" : "200 > 50 > P";
    }

    // ── Portfolio role actions and profit-taking ─────────────────────────────

    private static string PortfolioFinalAction(Decision dec, string role, FinalActionInput r)
    {
        var ts = dec.TrendSetup;
        var ms = dec.MomentumShift;
        return role switch
        {
            "Core" => CorePortfolioAction(ts, ms, r),
            "Strategic" => StrategicPortfolioAction(ts, ms, r),
            "Swing" => SwingPortfolioAction(ts, ms, r),
            "Options" => OptionsPortfolioAction(ts, ms),
            "Speculative" => SpeculativePortfolioAction(ts, ms),
            _ => dec.BaseAction,
        };
    }

    private static string CorePortfolioAction(string ts, string ms, FinalActionInput r)
    {
        var rsi = r.Rsi;
        if (ts == "Quality Trend Entry" && ms == "Uptrend")
        {
            if (rsi >= 50 && rsi <= 64.99) return "Hold / Add If Underweight";
            if (rsi >= 65 && rsi < 70) return "Hold / No Chase / Profit Watch";
            if (rsi >= 70) return "No Chase / Extended / Profit Watch";
        }
        if (ts == "Confirmed Constructive" && ms == "Uptrend") return "Hold / Add If Underweight";
        if (ms == "Consolidation / Dip-Buy") return "Hold / Add on Pullback";
        if (ms == "Active Buy Trigger") return "Add to Position";
        if (ms == "Bullish Shift") return "Hold / Watch for Add";
        if (ts == "Cooling") return "Hold / No New Buy";
        if (ts == "Technical Caution" || ms == "Breakdown" || ms == "Active Sell Trigger") return "Hold / Review";
        if (ts == "Waterfall / Falling Knife" || ms == "Warning") return "Hold / Risk Review";
        if (ms == "Uptrend") return "Hold / No Chase";
        return "Hold";
    }

    private static string StrategicPortfolioAction(string ts, string ms, FinalActionInput r)
    {
        var rsi = r.Rsi;
        if (ts == "Quality Trend Entry" && ms == "Uptrend")
        {
            if (rsi >= 50 && rsi <= 64.99) return "Hold / No Chase";
            if (rsi >= 65 && rsi < 70) return "Hold / No Chase / Profit Watch";
            if (rsi >= 70) return "No Chase / Extended / Profit Watch";
        }
        if (ts == "Confirmed Constructive" && ms == "Uptrend") return "Hold / No Chase";
        if (ms == "Active Buy Trigger") return "Add / Staged Entry";
        if (ms == "Consolidation / Dip-Buy") return "Add on Pullback";
        if (ms == "Bullish Shift") return "Hold / Watch for Add";
        if (ts == "Cooling") return "Hold / No Add";
        if (ts == "Technical Caution" || ms == "Breakdown") return "Hold / Technical Caution";
        if (ts == "Waterfall / Falling Knife" || ms == "Warning") return "Hold / Risk Review";
        if (ms == "Active Sell Trigger") return "Trim / Review";
        if (ms == "Uptrend") return "Hold / No Chase";
        return "Hold";
    }

    private static string SwingPortfolioAction(string ts, string ms, FinalActionInput r)
    {
        var rsi = r.Rsi;
        if (ms == "Active Buy Trigger") return "Add to Trade";
        if (ms == "Active Sell Trigger") return "Exit / Take Profit";
        if (ms == "Bullish Shift") return "Hold Swing / Watch";
        if (ms == "Bearish Shift") return "Trim / Exit Watch";
        if ((ts == "Confirmed Constructive" || ts == "Quality Trend Entry") && ms == "Uptrend")
        {
            if (rsi >= 65) return "No Chase / Profit Watch";
            return "Hold / Trail Stop";
        }
        if (ms == "Consolidation / Dip-Buy") return "Hold / Trail Stop";
        if (ts == "Constructive Extended" || ms == "Warning — Overbought Run") return "Trail Stop / Trim";
        if (ts == "Cooling" || ms == "Breakdown") return "Exit / Cut Loss";
        if (ts == "Waterfall / Falling Knife" || ms == "Warning") return "Cut Loss / Exit";
        if (ms == "Uptrend") return "Hold / Trail Stop";
        return "Hold Swing";
    }

    private static string SpeculativePortfolioAction(string ts, string ms)
    {
        if (ms == "Active Buy Trigger") return "Small Add";
        if (ms == "Active Sell Trigger") return "Take Profit / Exit";
        if (ms == "Bullish Shift") return "Hold Spec / Watch";
        if ((ts == "Confirmed Constructive" || ts == "Quality Trend Entry") && ms == "Uptrend") return "Hold Spec / Trail Stop";
        if (ts == "Constructive Extended" || ms == "Warning — Overbought Run") return "Trim / Trail Stop";
        if (ts == "Cooling" || ms == "Breakdown") return "Cut Loss / Thesis Review";
        if (ts == "Waterfall / Falling Knife" || ms == "Warning") return "Cut Loss / Exit";
        if (ms == "Uptrend") return "Hold Spec / Trail Stop";
        return "Hold Spec";
    }

    private static string OptionsPortfolioAction(string ts, string ms)
    {
        if (ms == "Active Sell Trigger") return "Close / Take Profit";
        if (ms == "Bearish Shift") return "Close / Watch for Reversal";
        if (ms == "Active Buy Trigger") return "Roll / Extend";
        if (ms == "Warning — Overbought Run") return "Trail / Protect Gains";
        if (ms == "Uptrend") return "Hold / Trail Stop";
        if (ts == "Cooling" || ms == "Breakdown") return "Close / Protect";
        return "Hold Option";
    }

    private static bool TfsaProfitTakeTriggered(FinalActionInput r, PortfolioItemContext? context)
    {
        if (context is null) return false;
        var account = (context.AccountType ?? "").ToUpperInvariant();
        if (!account.Contains("TFSA", StringComparison.Ordinal)) return false;
        if ((context.UnrealizedGainPct ?? 0) < 20) return false;
        if ((context.HoldingDays ?? double.PositiveInfinity) > 183) return false;
        if (r.Rsi < 65) return false;
        if (r.AnalystTargetPrice <= 0) return false;
        if (r.CurrentPrice < r.AnalystTargetPrice) return false;
        return true;
    }

    private static string? ProfitTakingAction(Decision dec, PortfolioItemContext? context)
    {
        if (context is null) return null;
        var pnl = context.UnrealizedGainPct ?? 0;
        var dist52H = context.DistanceFrom52WeekHighPct;
        var posSize = context.PositionSizePct ?? 0;
        var ms = dec.MomentumShift;
        var nearHigh = dist52H is not null && dist52H >= -2;

        if (pnl >= 50 && ms == "Active Sell Trigger") return "Sell Majority / Keep Small Runner";
        if (pnl >= 30 && ms == "Active Sell Trigger" && posSize >= 2) return "Trim 33–50% / Trail Remainder";
        if (pnl >= 20 && ms == "Active Sell Trigger") return "Take Partial Profit / Protect Gain";
        if (pnl >= 50 && nearHigh && posSize >= 4) return "Sell 50% / Keep Runner / Rebuy on Pullback";
        if (pnl >= 50 && nearHigh && posSize >= 2 && posSize < 4) return "Trim 33–50% / Keep Runner";
        if (pnl >= 50 && nearHigh && posSize < 2) return "Trim 25% / Keep Runner";
        if (pnl >= 30 && pnl < 50 && nearHigh && posSize >= 4) return "Trim 25–40% / Hold Runner";
        if (pnl >= 30 && pnl < 50 && nearHigh && posSize >= 2 && posSize < 4) return "Trim 20–33% / Hold Runner";
        if (pnl >= 30 && pnl < 50 && nearHigh && posSize < 2) return "Trim 10–20% / Hold Runner";
        if (pnl >= 20 && nearHigh) return "Profit Watch / No Add / Trail Stop";
        return null;
    }

    private static string RiskControlAdjustedAction(string originalAction, double closePct)
    {
        var isTrimOrSell = new[] { "Trim", "Sell", "Take Partial" }.Any(t => originalAction.Contains(t, StringComparison.Ordinal));
        if (!isTrimOrSell) return originalAction;
        // JS Math.round rounds halves toward +infinity.
        var pct = Math.Floor(closePct + 0.5).ToString(CultureInfo.InvariantCulture);
        if (closePct >= 50) return $"Risk Control: {pct}% Closed — Hold or Exit Remaining";
        if (closePct >= 25) return $"Risk Control: {pct}% Closed — Hold Runner";
        return $"Risk Control Started: {pct}% Closed — Monitor Position";
    }

    // ── Presentation strings and classes ─────────────────────────────────────

    private static string PortfolioHover(Decision dec, string role)
    {
        var ts = dec.TrendSetup;
        var ms = dec.MomentumShift;
        // The Angular TFSA hover branch evaluates the rule with an empty scan result and can never fire.
        if ((ts == "Confirmed Constructive" || ts == "Quality Trend Entry") && ms == "Uptrend")
        {
            if (role == "Core") return "Core holding is technically healthy. Continue holding. Add only if under target weight.";
            if (role == "Strategic") return "Strategic holding in healthy trend. Hold — no chasing additional shares.";
        }
        if (ms == "Warning — Overbought Run") return "Trend extended but no bearish reversal confirmed. Maintain position, do not add.";
        if (ts == "Technical Caution" || ms == "Breakdown")
        {
            if (role == "Core") return "Core holding has short-term technical weakness. Review thesis/size. Do NOT sell based solely on momentum.";
            return $"{role} holding shows technical weakness. Review thesis and position size.";
        }
        if (ms == "Active Sell Trigger" && (role == "Core" || role == "Strategic"))
            return "Technical pressure building. Stop new buying and review stop-loss levels.";
        if (ms == "Uptrend") return "Holding in constructive uptrend. Continue with appropriate stop discipline.";
        return dec.TrendSetupReason;
    }

    public static string TrendSetupClass(string ts) => ts switch
    {
        "Waterfall / Falling Knife" => "ts-waterfall",
        "Oversold Reversal Watch" => "ts-reversal",
        "Constructive Extended" => "ts-extended",
        "Quality Trend Entry" => "ts-quality",
        "Confirmed Constructive" => "ts-constructive",
        "Early Reversal" => "ts-early-reversal",
        "Cooling" => "ts-cooling",
        "Technical Caution" => "ts-caution",
        "Overbought-Weak" => "ts-ob-weak",
        "Overbought-Resetting" => "ts-ob-resetting",
        "Reclaim" => "ts-reclaim",
        _ => "ts-neutral",
    };

    public static string MomentumShiftClass(string ms) => ms switch
    {
        "Active Buy Trigger" => "ms-confirmed-buy",
        "Active Sell Trigger" => "ms-confirmed-sell",
        "Bullish Shift" => "ms-bullish",
        "Bearish Shift" => "ms-bearish",
        "Warning" => "ms-warning",
        "Warning — Overbought Run" => "ms-warning",
        "Breakdown" => "ms-breakdown",
        "Consolidation / Dip-Buy" => "ms-consolidation",
        "Uptrend" => "ms-uptrend",
        "Neutral / Weakening" => "ms-neutral-weak",
        _ => "ms-neutral",
    };

    public static string BaseActionClass(string ba) => ba switch
    {
        "Confirmed Buy Signal" => "ma-confirmed-buy",
        "Confirmed Sell Signal" => "ma-confirmed-sell",
        "Early Buy Watch" => "ma-early-warning",
        "Early Sell Watch" => "ma-early-warning",
        "Watch / Do Not Chase" => "ma-hold",
        "Avoid / Wait" => "ma-avoid",
        "Reduce / Review" => "ma-reduce",
        "Buy / Accumulate" => "ma-accumulate",
        "Hold Longs" => "ma-hold",
        _ => "ma-standby",
    };

    public static string FinalActionClass(string action)
    {
        var a = action.ToLowerInvariant();
        if (a.Contains("stand by") && a.Contains("no add")) return "ma-standby-no-add";
        if (a.Contains("partial profit") || a.Contains("profit watch") || a.Contains("protect gain")) return "ma-tfsa-profit";
        if (a.Contains("trim") || a.Contains("sell 50%") || a.Contains("sell majority") || a.Contains("keep runner")
            || a.Contains("keep small runner") || a.Contains("hold runner") || a.Contains("trail remainder")
            || a.Contains("no add / trail stop"))
            return "ma-reduce";
        if (a.Contains("avoid") || a.Contains("caution") || a.Contains("review") || a.Contains("wait")) return "ma-avoid";
        if (a.Contains("buy") || a.Contains("accumulate") || a.Contains("add") || a.Contains("starter") || a.Contains("call entry"))
            return "ma-confirmed-buy";
        if (a.Contains("sell") || a.Contains("exit") || a.Contains("cut") || a.Contains("put entry")) return "ma-confirmed-sell";
        if (a.Contains("hold") || a.Contains("no chase") || a.Contains("watch")) return "ma-hold";
        if (a.Contains("reduce") || a.Contains("trail") || a.Contains("protect")) return "ma-reduce";
        return "ma-standby";
    }

    public static (string Severity, string Priority) ActionCenterClassification(string finalActionClass) => finalActionClass switch
    {
        "ma-tfsa-profit" or "ma-reduce" or "ma-confirmed-sell" => ("trim", "REQUIRED"),
        "ma-avoid" => ("wait", "DEVELOPING"),
        "ma-confirmed-buy" => ("buy", "DEVELOPING"),
        _ => ("hold", "INFORMATIONAL"),
    };

    /// <summary>JavaScript <c>toFixed(1)</c>: exact binary ties (x.25 / x.75) round away from zero.</summary>
    private static string F1(double v)
    {
        var abs = Math.Abs(v);
        var quarters = abs * 4;
        if (quarters == Math.Floor(quarters) && Math.Floor(quarters) % 2 == 1)
        {
            var rounded = Math.Floor(abs * 10 + 0.5) / 10;
            return (v < 0 ? "-" : "") + rounded.ToString("F1", CultureInfo.InvariantCulture);
        }
        return v.ToString("F1", CultureInfo.InvariantCulture);
    }
}
