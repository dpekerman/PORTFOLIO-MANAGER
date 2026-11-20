using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

/// <summary>
/// PM-024 Stage 4: Swing exit policy. A sell trigger needs bearish confirmation beyond volume; a profitable position with a
/// verified-clear planned stop is managed by the stop; mild weakness plus a loss is not by itself an exit; "Take Profit" is
/// never shown at a loss. Synthetic fixtures only.
/// </summary>
public sealed class Pm024Stage4SwingExitPolicyTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);
    private static readonly DateTime Now = new(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc);

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

    // NOVO.TO 2026-11-04: RSI 65.49 above its signal 52.64, MACD rising, 6.29x volume, close in the bottom of the range after a gap-up.
    private static FinalActionInput NovoLike(Func<FinalActionInput, FinalActionInput>? f = null) => Base(i =>
    {
        i = i with
        {
            Rsi = 65.49, RsiSignal = 52.64, CurrentPrice = 18.28, OpenPrice = 18.46, PreviousClose = 17.45, DayHigh = 18.69, DayLow = 18.24, VolumeRatio = 6.29,
            MacdHistDelta = 0.0569, Ema9 = 17.33, Ema10 = 17.30, Ema20 = 17.155, Sma20 = 17.4, Sma50 = 17.58, Sma200 = 18.6,
        };
        return f is null ? i : f(i);
    });

    private static FinalActionDecision Swing(FinalActionInput i, double gain) =>
        FinalActionEngine.TranslateForPortfolio(i, "Swing", new PortfolioItemContext("TFSA", gain, 20, null, null));

    private static StopRiskEvidence Stop(string state, bool breached = false) =>
        new(state, 16.2m, 16.2m, breached, false, 18.28m, Now, "EOD", "Unknown", "None", [], true, "TFSA", new DateTime(2026, 10, 15));

    private static PortfolioEvidence Ev(double gain, StopRiskEvidence? risk = null, bool structural = false, double? size = null, double? limit = null) =>
        new(gain, new DateOnly(2026, 10, 15), Today, Now, size, limit, false, structural, "Manual", Risk: risk);

    private static GateOutcome Gate(FinalActionDecision d, PortfolioEvidence e, string role = "Swing") => PortfolioEvidenceGate.Apply(d, role, e);

    // ── Engine: Active Sell Trigger confirmation ─────────────────────────────

    [Fact]
    public void HighVolumeAlone_WhileRsiIsAboveItsSignalAndMacdRises_IsNotASellTrigger()
    {
        var d = Swing(NovoLike(), 7.53);
        Assert.NotEqual("Active Sell Trigger", d.MomentumShift);
        Assert.Equal("Warning — Overbought Run", d.MomentumShift);
        Assert.NotEqual("Exit / Take Profit", d.FinalAction);
        Assert.Equal("Hold / Trail Stop — No Add", d.FinalAction);
    }

    [Fact]
    public void HighVolume_WithRsiAtOrBelowItsSignal_IsStillASellTrigger()
    {
        var d = Swing(NovoLike(i => i with { RsiSignal = 66.0 }), 7.53);
        Assert.Equal("Active Sell Trigger", d.MomentumShift);
        Assert.Equal("Exit / Take Profit", d.FinalAction);
    }

    [Fact]
    public void WeakeningMacd_ConfirmsASellTrigger_WithoutNeedingVolume()
    {
        var d = Swing(NovoLike(i => i with { VolumeRatio = 0.9, MacdHistDelta = -0.05 }), 7.53);
        Assert.Equal("Active Sell Trigger", d.MomentumShift);
    }

    [Fact]
    public void HighVolume_WithoutAnRsiSignalLine_IsNotConfirmation()
    {
        var d = Swing(NovoLike(i => i with { RsiSignalAvailable = false, RsiSignal = null }), 7.53);
        Assert.NotEqual("Active Sell Trigger", d.MomentumShift);
    }

    // ── Gate: profit protected by a verified-clear stop ──────────────────────

    private static FinalActionDecision ConfirmedSell() => Swing(NovoLike(i => i with { RsiSignal = 66.0 }), 7.53);

    [Fact]
    public void ConfirmedSell_OnAProfitablePosition_WithAClearPlannedStop_BecomesAStopAdvisory()
    {
        var o = Gate(ConfirmedSell(), Ev(7.53, Stop("Clear")));
        Assert.Equal("Hold / Trail Stop — No Add", o.Decision.FinalAction);
        Assert.Equal("Exit / Take Profit", o.OriginalAction);
        Assert.Contains(GateReasons.ProfitProtectedByStop, o.ReasonCodes);
        Assert.Contains("above the Planned Stop", o.Decision.FinalActionReason);
        Assert.Contains("not a confirmed broker order", o.Decision.FinalActionReason);
        Assert.Contains("places no orders", o.Decision.FinalActionReason);
        Assert.Contains("no order is implied", o.Decision.HoverDescription);
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(FinalActionCatalog.Describe(o.Decision.FinalAction).Id, "Portfolio", 18m));
    }

    [Fact]
    public void ConfirmedSell_WithoutAVerifiedClearStop_KeepsTheExit()
    {
        foreach (var risk in new StopRiskEvidence?[] { null, Stop("NoPlan"), Stop("Unverified") })
        {
            var o = Gate(ConfirmedSell(), Ev(7.53, risk));
            Assert.Equal("Exit / Take Profit", o.Decision.FinalAction);
            Assert.Contains(GateReasons.TechnicalExitPostEntry, o.ReasonCodes);
        }
    }

    [Fact]
    public void ConfirmedSell_WithAStopBreach_KeepsTheIndependentExit_AndAddsTheBreachEvidence()
    {
        var o = Gate(ConfirmedSell(), Ev(7.53, Stop("Breached", breached: true)));
        Assert.Equal("Exit / Take Profit", o.Decision.FinalAction);
        Assert.Contains(GateReasons.PlannedStopBreached, o.ReasonCodes);
        Assert.Contains(GateReasons.TechnicalExitPostEntry, o.ReasonCodes);
        Assert.DoesNotContain(GateReasons.ProfitProtectedByStop, o.ReasonCodes);
    }

    [Fact]
    public void ConfirmedSell_WithStructuralBreakdown_OrAllocationBreach_StillExits()
    {
        var structural = Gate(ConfirmedSell(), Ev(7.53, Stop("Clear"), structural: true));
        Assert.Equal("Exit / Take Profit", structural.Decision.FinalAction);
        Assert.Contains(GateReasons.StructuralBreakdown, structural.ReasonCodes);

        var allocation = Gate(ConfirmedSell(), Ev(7.53, Stop("Clear"), size: 12, limit: 10));
        Assert.Equal("Exit / Take Profit", allocation.Decision.FinalAction);
        Assert.Contains(GateReasons.AllocationBreach, allocation.ReasonCodes);
    }

    [Fact]
    public void Speculative_ConfirmedSell_WithAClearStop_UsesTheSpeculativeAdvisoryLabel()
    {
        var d = FinalActionEngine.TranslateForPortfolio(NovoLike(i => i with { RsiSignal = 66.0 }), "Speculative", new PortfolioItemContext("TFSA", 7.5, 20, null, null));
        Assert.Equal("Take Profit / Exit", d.FinalAction);
        var o = Gate(d, Ev(7.5, Stop("Clear")), "Speculative");
        Assert.Equal("Hold Spec / Trail Stop — No Add", o.Decision.FinalAction);
    }

    // ── Gate: mild weakness + loss ───────────────────────────────────────────

    private static FinalActionInput Cooling() => Base(i => i with { Rsi = 50, RsiSignal = 49, Ema10 = 99, Ema20 = 99.4, Sma50 = 95, CurrentPrice = 99.1, Ema9 = 99.3, MacdHistDelta = 0.0 });
    private static FinalActionInput Breakdown() => Base(i => i with { Rsi = 38, RsiSignal = 39, CurrentPrice = 94, Ema9 = 96, Ema10 = 97, Ema20 = 99, Sma50 = 100, DayHigh = 95, DayLow = 93.5, OpenPrice = 94.8, PreviousClose = 95, MacdHistDelta = -0.1 });
    private static FinalActionInput Waterfall() => Base(i => i with { Rsi = 28, RsiSignal = 32, CurrentPrice = 80, Ema9 = 85, Ema10 = 86, Ema20 = 90, Sma20 = 90, Sma50 = 95, DayHigh = 82, DayLow = 79.5, OpenPrice = 81.5, PreviousClose = 84, MacdHistDelta = -0.2, VolumeRatio = 0.8 });

    [Theory]
    [InlineData(-1.5)]
    [InlineData(-6)]
    public void MildCooling_WithALoss_IsNotAnExit_WithoutAnIndependentRiskEvent(double gain)
    {
        var o = Gate(Swing(Cooling(), gain), Ev(gain, Stop("Clear")));
        Assert.Equal("Hold / Monitor", o.Decision.FinalAction);
        Assert.Equal("Exit / Cut Loss", o.OriginalAction);
        Assert.Contains(GateReasons.LossNotIndependent, o.ReasonCodes);
        Assert.Contains("not an independent exit reason", o.Decision.FinalActionReason);
    }

    [Fact]
    public void MildBreakdown_WithALoss_IsNotAnExit_ButANoStopPlanStillAddsNoInventedBreach()
    {
        var o = Gate(Swing(Breakdown(), -4), Ev(-4, null));
        Assert.Equal("Hold / Monitor", o.Decision.FinalAction);
        Assert.Contains(GateReasons.LossNotIndependent, o.ReasonCodes);
    }

    [Fact]
    public void MildCooling_WithALoss_AndAVerifiedStopBreach_IsReviewStopBreach()
    {
        var o = Gate(Swing(Cooling(), -9), Ev(-9, Stop("Breached", breached: true)));
        Assert.Equal(PortfolioEvidenceGate.ReviewStopBreach, o.Decision.FinalAction);
        Assert.Contains(GateReasons.PlannedStopBreached, o.ReasonCodes);
    }

    [Fact]
    public void MildCooling_WithALoss_AndStructuralBreakdown_StillExits()
    {
        var o = Gate(Swing(Cooling(), -2), Ev(-2, null, structural: true));
        Assert.Equal("Exit / Cut Loss", o.Decision.FinalAction);
        Assert.Contains(GateReasons.StructuralBreakdown, o.ReasonCodes);
    }

    [Fact]
    public void WaterfallWarning_WithALoss_IsStillAnIndependentTechnicalExit()
    {
        var d = Swing(Waterfall(), -6);
        Assert.Equal("Waterfall / Falling Knife", d.TrendSetup);
        var o = Gate(d, Ev(-6, null));
        Assert.Equal("Cut Loss / Exit", o.Decision.FinalAction);
        Assert.Contains(GateReasons.LossBreach, o.ReasonCodes);
    }

    [Fact]
    public void ConfirmedSell_WithALoss_StillExits()
    {
        var o = Gate(Swing(NovoLike(i => i with { RsiSignal = 66.0 }), -1.0), Ev(-1.0, Stop("Clear")));
        Assert.Equal("Exit / Cut Loss", o.Decision.FinalAction);
        Assert.Contains(GateReasons.LossBreach, o.ReasonCodes);
    }

    // ── Gate: never "Take Profit" at a loss ──────────────────────────────────

    [Theory]
    [InlineData("Swing", "Exit / Take Profit", "Exit / Cut Loss")]
    [InlineData("Speculative", "Take Profit / Exit", "Cut Loss / Exit")]
    public void TakeProfitLabel_IsCorrected_WhenThePositionIsAtALoss(string role, string rawLabel, string expected)
    {
        var raw = FinalActionEngine.TranslateForPortfolio(NovoLike(i => i with { RsiSignal = 66.0 }), role, new PortfolioItemContext("TFSA", -2, 20, null, null));
        Assert.Equal(rawLabel, raw.FinalAction);
        var o = Gate(raw, Ev(-2, null), role);
        Assert.Equal(expected, o.Decision.FinalAction);
        Assert.Equal(rawLabel, o.OriginalAction);
        Assert.Contains(GateReasons.LabelCorrectedForLoss, o.ReasonCodes);
        Assert.DoesNotContain("Take Profit", o.Decision.FinalAction);
    }

    [Fact]
    public void TakeProfitLabel_IsUntouched_WhenInProfit()
    {
        var o = Gate(ConfirmedSell(), Ev(7.53, null));
        Assert.Equal("Exit / Take Profit", o.Decision.FinalAction);
        Assert.DoesNotContain(GateReasons.LabelCorrectedForLoss, o.ReasonCodes);
    }

    // ── Control: long-horizon roles and entries are unaffected ───────────────

    [Fact]
    public void Strategic_Cooling_WithALoss_StaysAHold()
    {
        var d = FinalActionEngine.TranslateForPortfolio(Cooling(), "Strategic", new PortfolioItemContext("TFSA", -2, 20, null, null));
        Assert.Equal("Hold / No Add", Gate(d, Ev(-2, null), "Strategic").Decision.FinalAction);
    }
}
