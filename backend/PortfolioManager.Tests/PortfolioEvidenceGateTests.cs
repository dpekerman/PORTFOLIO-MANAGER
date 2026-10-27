using PortfolioManager.Api.Services.FinalAction;
using Xunit;

namespace PortfolioManager.Tests;

public sealed class PortfolioEvidenceGateTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);
    private static readonly DateTime Now = new(2026, 10, 27, 16, 0, 0, DateTimeKind.Utc);

    private static FinalActionDecision Decision(string action) => new(
        action, "Cooling", "", "Active Sell Trigger", "", "Confirmed Sell Signal", "No Gap", 0, 0,
        "Technical pressure building. Stop new buying and review stop-loss levels.",
        FinalActionEngine.FinalActionClass(action), "", "", null, null);

    private static PortfolioEvidence Evidence(
        double? gain = 5, DateOnly? entry = null, double? size = 3.5, double? limit = 10, bool sectorOver = false,
        bool structural = false, string? source = "Rebalance") =>
        new(gain, entry ?? Today.AddDays(-60), Today, Now, size, limit, sectorOver, structural, source);

    private static GateOutcome Run(string action, string role, PortfolioEvidence e) =>
        PortfolioEvidenceGate.Apply(Decision(action), role, e);

    // ── BRK-type: Strategic holding, profitable, small allocation ──
    [Fact]
    public void Strategic_TechnicalSellTrigger_WithoutPortfolioTrigger_BecomesHoldMonitor_AndIsNotActionable()
    {
        var o = Run("Trim / Review", "Strategic", Evidence());
        Assert.Equal("Hold / Monitor", o.Decision.FinalAction);
        Assert.Equal("Trim / Review", o.OriginalAction);
        Assert.Contains(GateReasons.TechnicalOnly, o.ReasonCodes);
        Assert.Contains("Review stop-loss levels", o.Decision.HoverDescription);
        Assert.Equal("ma-hold", o.Decision.FinalActionClass);
        Assert.Equal(("hold", "INFORMATIONAL"), FinalActionEngine.ActionCenterClassification(o.Decision.FinalActionClass));
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(o.Decision.FinalAction, "Portfolio", 10m));
    }

    [Fact]
    public void Strategic_AllocationBreach_KeepsTrim()
    {
        var o = Run("Trim / Review", "Strategic", Evidence(size: 12, limit: 10));
        Assert.Equal("Trim / Review", o.Decision.FinalAction);
        Assert.Contains(GateReasons.AllocationBreach, o.ReasonCodes);
        Assert.Contains("exceeds the configured", o.Decision.FinalActionReason);
    }

    [Fact]
    public void Strategic_AtLimit_IsNotABreach()
    {
        Assert.Equal("Hold / Monitor", Run("Trim / Review", "Strategic", Evidence(size: 10, limit: 10)).Decision.FinalAction);
    }

    [Fact]
    public void Strategic_NoConfiguredLimit_NeverInventsABreach()
    {
        Assert.Equal("Hold / Monitor", Run("Trim / Review", "Strategic", Evidence(size: 90, limit: null)).Decision.FinalAction);
    }

    [Fact]
    public void Strategic_LossAlone_IsNotASellTrigger_ButConfirmedBreakdownIs()
    {
        Assert.Equal("Hold / Monitor", Run("Trim / Review", "Strategic", Evidence(gain: -8)).Decision.FinalAction);
        var o = Run("Trim / Review", "Strategic", Evidence(gain: -8, structural: true));
        Assert.Equal("Trim / Review", o.Decision.FinalAction);
        Assert.Contains(GateReasons.StructuralBreakdown, o.ReasonCodes);
    }

    [Fact]
    public void Core_And_UnknownRoles_UseTheLongHorizonRule()
    {
        Assert.Equal("Hold / Monitor", Run("Trim / Review", "Core", Evidence()).Decision.FinalAction);
        Assert.Equal("Hold / Monitor", Run("Reduce / Review", "Income", Evidence()).Decision.FinalAction);
    }

    // ── CLS-type: Swing holding bought today on a documented catalyst ──
    [Fact]
    public void Swing_EntryDay_TechnicalCutLoss_WithGain_BecomesCatalystMonitoring()
    {
        var o = Run("Exit / Cut Loss", "Swing", Evidence(gain: 1.6, entry: Today, source: "Earnings Catalyst"));
        Assert.Equal("Hold / Catalyst Monitoring", o.Decision.FinalAction);
        Assert.Contains(GateReasons.EntryDayEvidence, o.ReasonCodes);
        Assert.Contains("2026-10-27", o.Decision.FinalActionReason);
        Assert.Contains("Exit / Cut Loss", o.Decision.FinalActionReason);
    }

    [Fact]
    public void Swing_EntryDay_WithMissingCatalystMetadata_StillHolds()
    {
        var o = Run("Exit / Cut Loss", "Swing", Evidence(gain: 0.5, entry: Today, source: null));
        Assert.Equal("Hold / Monitor", o.Decision.FinalAction);
    }

    [Fact]
    public void Swing_RecentPurchase_RealLoss_StillExits()
    {
        var o = Run("Exit / Cut Loss", "Swing", Evidence(gain: -4, entry: Today, source: "Earnings Catalyst"));
        Assert.Equal("Exit / Cut Loss", o.Decision.FinalAction);
        Assert.Contains(GateReasons.LossBreach, o.ReasonCodes);
    }

    [Fact]
    public void Swing_PostEntryDay_CutLossWithoutLoss_IsBlocked_ButWithLossOrBreakdownPasses()
    {
        var e = Evidence(gain: 2, entry: Today.AddDays(-1));
        Assert.Equal("Hold / Monitor", Run("Exit / Cut Loss", "Swing", e).Decision.FinalAction);
        Assert.Equal("Exit / Cut Loss", Run("Exit / Cut Loss", "Swing", e with { UnrealizedGainPct = -1 }).Decision.FinalAction);
        Assert.Equal("Exit / Cut Loss", Run("Exit / Cut Loss", "Swing", e with { HardStructuralNegative = true }).Decision.FinalAction);
    }

    [Fact]
    public void Swing_PostEntry_TechnicalTakeProfit_Passes_ButNotOnEntryDay()
    {
        Assert.Equal("Exit / Take Profit", Run("Exit / Take Profit", "Swing", Evidence(entry: Today.AddDays(-2))).Decision.FinalAction);
        Assert.Equal("Hold / Monitor", Run("Exit / Take Profit", "Swing", Evidence(entry: Today)).Decision.FinalAction);
    }

    [Fact]
    public void ConfirmedBreakdown_OnEntryDay_IsNotPostEntryEvidence_ButRealLossIs()
    {
        var o = Run("Cut Loss / Exit", "Speculative", Evidence(gain: 3, entry: Today, structural: true));
        Assert.Equal("Hold / Monitor", o.Decision.FinalAction);
        Assert.Equal("Cut Loss / Exit", Run("Cut Loss / Exit", "Speculative", Evidence(gain: -3, entry: Today, structural: true)).Decision.FinalAction);
    }

    [Fact]
    public void UnknownEntryDate_DoesNotSuppressShortHorizonExits()
    {
        var e = Evidence() with { EntryDate = null };
        var o = Run("Exit / Take Profit", "Swing", e);
        Assert.Equal("Exit / Take Profit", o.Decision.FinalAction);
        Assert.Contains(GateReasons.EntryDateUnknown, o.ReasonCodes);
    }

    // ── Untouched cases ──
    [Theory]
    [InlineData("Take Partial Profit / Protect Gain")]
    [InlineData("Sell Majority / Keep Small Runner")]
    [InlineData("Trim 33–50% / Trail Remainder")]
    [InlineData("Hold / Review")]
    [InlineData("Trim / Exit Watch")]
    [InlineData("Hold")]
    public void ProfitTakingAndAdvisoryActions_AreNeverChanged(string action)
    {
        var o = Run(action, "Strategic", Evidence(entry: Today));
        Assert.Equal(action, o.Decision.FinalAction);
        Assert.Empty(o.ReasonCodes);
    }

    // ── Add side ──
    [Fact]
    public void Add_AtOrOverPositionLimit_BecomesNoAdd()
    {
        Assert.Equal("Hold / No Add", Run("Add / Staged Entry", "Strategic", Evidence(size: 10, limit: 10)).Decision.FinalAction);
        var o = Run("Add to Position", "Core", Evidence(size: 11, limit: 10));
        Assert.Equal("Hold / No Add", o.Decision.FinalAction);
        Assert.Equal("ma-hold", o.Decision.FinalActionClass);
        Assert.Contains(GateReasons.AddBlockedPositionLimit, o.ReasonCodes);
    }

    [Fact]
    public void Add_SectorOverTarget_BecomesNoAdd_AndUnderLimitStaysAdd()
    {
        Assert.Equal("Hold / No Add", Run("Add on Pullback", "Core", Evidence(sectorOver: true)).Decision.FinalAction);
        Assert.Equal("Add on Pullback", Run("Add on Pullback", "Core", Evidence(size: 4, limit: 10)).Decision.FinalAction);
    }

    // ── Whole-book matrix: every role × every technical reduction × every evidence combination ──
    public static IEnumerable<object[]> Matrix()
    {
        string[] roles = ["Core", "Strategic", "Swing", "Speculative", "Options", "Other"];
        string[] actions = ["Trim / Review", "Trail Stop / Trim", "Trim / Trail Stop", "Exit / Take Profit", "Exit / Cut Loss",
            "Cut Loss / Exit", "Cut Loss / Thesis Review", "Take Profit / Exit", "Close / Take Profit", "Close / Protect",
            "Close / Watch for Reversal", "Reduce / Review", "Confirmed Sell Signal"];
        foreach (var r in roles) foreach (var a in actions) yield return [r, a];
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Matrix_TechnicalOnlyReduction_IsNeverActionable_OnEntryDayWithoutLoss_ForAnyRole(string role, string action)
    {
        var o = Run(action, role, Evidence(gain: 4, entry: Today));
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(o.Decision.FinalAction, "Portfolio", 10m));
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Matrix_AllocationBreach_NeverHidesAPolicyTrim_ForTrimAndProfitExits(string role, string action)
    {
        var o = Run(action, role, Evidence(gain: 4, entry: Today.AddDays(-30), size: 15, limit: 10));
        var lossExit = action.Contains("Cut Loss") || action.Contains("Protect") || action.Contains("Thesis");
        if (!lossExit) Assert.Equal(action, o.Decision.FinalAction);
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Matrix_GateIsIdempotent(string role, string action)
    {
        var e = Evidence(gain: 1, entry: Today);
        var once = Run(action, role, e);
        var twice = PortfolioEvidenceGate.Apply(once.Decision, role, e);
        Assert.Equal(once.Decision.FinalAction, twice.Decision.FinalAction);
    }

    [Fact]
    public void Engine_SwingCooling_ProducesCutLoss_AndTheGateReconcilesIt()
    {
        var input = new FinalActionInput
        {
            Symbol = "T", Rsi = 50, CurrentPrice = 100, Ema9 = 100, Ema10 = 98, Ema20 = 99, Sma20 = 99, Sma50 = 95, Sma200 = 90,
            OpenPrice = 100, PreviousClose = 100, DayHigh = 101, DayLow = 99, VolumeRatio = 1,
        };
        var raw = FinalActionEngine.TranslateForPortfolio(input, "Swing");
        Assert.Equal("Exit / Cut Loss", raw.FinalAction);
        var gated = PortfolioEvidenceGate.Apply(raw, "Swing", Evidence(gain: 1.6, entry: Today, source: "Earnings Catalyst"));
        Assert.Equal("Hold / Catalyst Monitoring", gated.Decision.FinalAction);
    }

    [Theory]
    [InlineData("Hold / Monitor")]
    [InlineData("Hold / Catalyst Monitoring")]
    public void ReplacementActions_AreKnownAdvisoryNonActionable(string action)
    {
        Assert.True(FinalActionCatalog.IsKnown(action));
        Assert.Equal("Advisory", FinalActionCatalog.Describe(action).Category);
        Assert.Equal("ma-hold", FinalActionEngine.FinalActionClass(action));
    }
}
