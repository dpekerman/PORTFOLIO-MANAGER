using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

/// <summary>
/// PM-024 Stage 2: Watchlist precedence. Structural damage outranks a gap-driven "extended" reading, a failed gap is not an
/// extension, and the reason text names the evidence that actually decided the result. Synthetic inputs only.
/// </summary>
public sealed class Pm024Stage2WatchlistPrecedenceTests
{
    private const string TrendExtendedReason = "Trend may be healthy, but current price location is extended for a new entry.";
    private const string GapHoldingReason = "Price gapped up and the gap is holding; do not chase it for a new entry.";

    private static FinalActionInput Base(Func<FinalActionInput, FinalActionInput>? f = null)
    {
        var i = new FinalActionInput
        {
            Symbol = "SYN", Rsi = 50, CurrentPrice = 100, ChangePercent = 0.2, DayHigh = 101, DayLow = 99, OpenPrice = 99.8, PreviousClose = 99.8, VolumeRatio = 1.1,
            RsiSignal = 48, RsiSignalAvailable = true, MacdHistDelta = 0.1, Ema9 = 99.5, Ema10 = 99, Ema20 = 97, Sma20 = 97, Sma50 = 92, Sma200 = 85,
            Status = "Neutral", ScanType = "Neutral", FibZone = "", FibStatus = "", TrendShift = "Waiting", Week52High = 110,
            PriceStructure = new FinalActionPriceStructure(false, "TRANSITION", "NONE", "NONE", "NONE"),
        };
        return f is null ? i : f(i);
    }

    private static FinalActionInput Damaged(Func<FinalActionInput, FinalActionInput>? f = null) => Base(i =>
    {
        i = i with
        {
            Rsi = 38, RsiSignal = 40, CurrentPrice = 95, OpenPrice = 95.2, PreviousClose = 95.1, DayHigh = 95.8, DayLow = 94.5,
            Ema9 = 99, Ema10 = 99.5, Ema20 = 101, Sma20 = 101, Sma50 = 104, Sma200 = 120, FibZone = "Trend Damage", FibStatus = "Below 78.6", MacdHistDelta = -0.2,
        };
        return f is null ? i : f(i);
    });

    private static FinalActionDecision Eval(FinalActionInput i, string role = "Strategic") => FinalActionEngine.TranslateForWatchlist(i, role);

    private static readonly string[] DamagedOutcomes = ["AVOID", "WAIT FOR RECLAIM"];

    // ── B: damaged trend + gap ───────────────────────────────────────────────

    [Fact]
    public void B_DamagedTrend_WithAFailedGapUp_IsNotWaitForPullback()
    {
        var d = Eval(Damaged(i => i with { OpenPrice = 103.5, PreviousClose = 100, DayHigh = 104, DayLow = 94.5 }));
        Assert.Equal("Gap Up - Failed", d.GapStatus);
        Assert.Contains(d.FinalAction, DamagedOutcomes);
        Assert.DoesNotContain("extended", d.FinalActionReason!);
    }

    [Fact]
    public void B2_DamagedTrend_WithAHoldingGapUp_IsNotWaitForPullback()
    {
        var d = Eval(Damaged(i => i with { CurrentPrice = 103.6, OpenPrice = 103.5, PreviousClose = 100, DayHigh = 104, DayLow = 103.4, MacdHistDelta = 0.1, Rsi = 45, RsiSignal = 40 }));
        Assert.Equal("Gap Up - Weak", d.GapStatus);
        Assert.Contains(d.FinalAction, DamagedOutcomes);
        Assert.Equal("Damaged trend or MA structure requires a confirmed recovery before entry.", d.FinalActionReason);
    }

    [Fact]
    public void B3_SeverelyBearishMa_WithoutTheFibZone_AlsoOutranksAGap()
    {
        var d = Eval(Damaged(i => i with { FibZone = "", CurrentPrice = 103.6, OpenPrice = 103.5, PreviousClose = 100, DayHigh = 104, DayLow = 103.4, MacdHistDelta = 0.1, Rsi = 45, RsiSignal = 40 }));
        Assert.Contains(d.FinalAction, DamagedOutcomes);
    }

    // ── C: damaged trend, no gap (unchanged) ─────────────────────────────────

    [Fact]
    public void C_DamagedTrend_WithoutAGap_StaysCautious()
    {
        var d = Eval(Damaged());
        Assert.Equal("No Gap", d.GapStatus);
        Assert.Equal("WAIT FOR RECLAIM", d.FinalAction);
    }

    // ── Failed gap on an otherwise healthy chart ─────────────────────────────

    [Fact]
    public void FailedGap_OnAHealthyTrend_IsNotCalledExtended_AndConfirmsNoEntry()
    {
        var d = Eval(Base(i => i with { Rsi = 57.5, RsiSignal = 53.8, Ema9 = 99.5, Ema10 = 99, Ema20 = 98.5, Sma50 = 96, Sma200 = 97, MacdHistDelta = 0.04, VolumeRatio = 1.8,
            CurrentPrice = 99.9, OpenPrice = 103.5, PreviousClose = 100, DayHigh = 104, DayLow = 99.5,
            PriceStructure = new FinalActionPriceStructure(false, "SUPPORT", "SUPPORT_RECLAIM", "LOWER_RAIL_APPROACHING", "RISING_CHANNEL") }));
        Assert.Equal("Gap Up - Failed", d.GapStatus);
        Assert.NotEqual("STARTER ENTRY", d.FinalAction);
        Assert.NotEqual("ENTRY CANDIDATE", d.FinalAction);
        Assert.NotEqual(TrendExtendedReason, d.FinalActionReason);
        Assert.DoesNotContain("extended", d.FinalActionReason!);
    }

    // ── A: genuine extension / holding gap (preserved) ───────────────────────

    [Fact]
    public void A_ConstructiveExtendedTrend_StillWaitsForPullback_WithTheTrendReason()
    {
        var d = Eval(Base(i => i with { Rsi = 72, RsiSignal = 70, Ema10 = 99, Ema20 = 96, Sma50 = 90, CurrentPrice = 100.5, DayHigh = 100.8, DayLow = 99.5, MacdHistDelta = 0.2 }));
        Assert.Equal("Constructive Extended", d.TrendSetup);
        Assert.Equal("WAIT FOR PULLBACK", d.FinalAction);
        Assert.Equal(TrendExtendedReason, d.FinalActionReason);
    }

    [Fact]
    public void A2_ConstructiveTrend_WithAHoldingGapUp_WaitsForPullback_AndSaysItIsTheGap()
    {
        var d = Eval(Base(i => i with { Rsi = 60, RsiSignal = 55, CurrentPrice = 106, OpenPrice = 104, PreviousClose = 100, ChangePercent = 6, DayHigh = 106.5, DayLow = 103.9, VolumeRatio = 2 }));
        Assert.Equal("Gap Up - Strong", d.GapStatus);
        Assert.Equal("WAIT FOR PULLBACK", d.FinalAction);
        Assert.Equal(GapHoldingReason, d.FinalActionReason);
    }

    // ── D: BIP-UN.TO control ─────────────────────────────────────────────────

    private static FinalActionInput BipLike(Func<FinalActionInput, FinalActionInput>? f = null) => Base(i =>
    {
        i = i with
        {
            Rsi = 57.55, RsiSignal = 53.79, CurrentPrice = 53.19, OpenPrice = 52.72, PreviousClose = 52.45, ChangePercent = 1.41, DayHigh = 53.45, DayLow = 51.84,
            Ema9 = 52.4257, Ema10 = 52.3851, Ema20 = 52.1142, Sma20 = 52.2555, Sma50 = 51.4954, Sma200 = 52.0788, MacdHistDelta = 0.0388, VolumeRatio = 1.83,
            FibZone = "Shallow Pullback", FibStatus = "Above 61.8",
            PriceStructure = new FinalActionPriceStructure(false, "SUPPORT", "SUPPORT_RECLAIM", "LOWER_RAIL_APPROACHING", "RISING_CHANNEL"),
        };
        return f is null ? i : f(i);
    });

    [Fact]
    public void D_BipUnTo_StarterEntry_IsPreserved()
    {
        var d = Eval(BipLike());
        Assert.Equal("No Gap", d.GapStatus);
        Assert.Equal("Quality Trend Entry", d.TrendSetup);
        Assert.Equal("STARTER ENTRY", d.FinalAction);
        Assert.Equal(5, d.WatchlistDiagnostics!.BuyScore);
        Assert.Equal("Constructive support and momentum allow a staged initial position for this role.", d.FinalActionReason);
    }

    // ── Reproductions of the UAT tickers on consistent (close-filled) evidence ──

    [Fact]
    public void Bidu_OnConsistentEvidence_IsDamaged_NotWaitForPullback()
    {
        var d = Eval(Base(i => i with
        {
            Rsi = 45.92, RsiSignal = 34.71, CurrentPrice = 80.43, OpenPrice = 79.62, PreviousClose = 76.97, ChangePercent = 4.5, DayHigh = 81.7, DayLow = 79.5, VolumeRatio = 1.42,
            MacdHistDelta = 0.2655, Ema9 = 78.5294, Ema10 = 78.6397, Ema20 = 80.3308, Sma20 = 80.3, Sma50 = 86.499, Sma200 = 113.3803, FibZone = "Trend Damage", FibStatus = "Below 78.6",
        }), "Swing");
        Assert.Equal("Gap Up - Weak", d.GapStatus);
        Assert.Contains(d.FinalAction, DamagedOutcomes);
        Assert.NotEqual("WAIT FOR PULLBACK", d.FinalAction);
    }

    [Fact]
    public void Amzn_OnConsistentEvidence_KeepsWaitForPullback_BecauseTheGapIsHolding()
    {
        var d = Eval(Base(i => i with
        {
            Rsi = 59.87, RsiSignal = 48.75, CurrentPrice = 266.03, OpenPrice = 264.815, PreviousClose = 250.69, ChangePercent = 6.12, DayHigh = 271.49, DayLow = 263.85, VolumeRatio = 2.17,
            MacdHistDelta = 1.333, Ema9 = 252.5367, Ema10 = 252.4891, Ema20 = 252.8453, Sma20 = 253.8, Sma50 = 253.8908, Sma200 = 243.6709, FibZone = "Shallow Pullback", FibStatus = "Reclaimed 61.8",
        }));
        Assert.Equal("Overbought-Weak", d.TrendSetup);
        Assert.Equal("WAIT FOR PULLBACK", d.FinalAction);
    }

    [Fact]
    public void ReasonText_NamesTheEvidenceThatDecided()
    {
        Assert.Equal("Momentum is declining and reversal confirmation is not complete.",
            Eval(Damaged(i => i with { Rsi = 38, MacdHistDelta = -0.2 })).FinalActionReason);
        Assert.Equal("Damaged trend or MA structure requires a confirmed recovery before entry.",
            Eval(Damaged(i => i with { Rsi = 45, RsiSignal = 40, MacdHistDelta = 0.1 })).FinalActionReason);
    }
}
