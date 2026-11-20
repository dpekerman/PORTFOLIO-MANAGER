using PortfolioManager.Api.Services.FinalAction;
using Xunit;

namespace PortfolioManager.Tests;

public sealed class Pm034StrategicTacticalTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);
    private static readonly DateTime Now = new(2026, 10, 27, 16, 0, 0, DateTimeKind.Utc);

    private static FinalActionDecision Decision(string action, string trend, string momentum) => new(
        action, trend, "", momentum, "", "Confirmed Sell Signal", "No Gap", 0, 0, "raw reason",
        FinalActionEngine.FinalActionClass(action), "", "", null, null);

    private static PortfolioEvidence Evidence(double? gain, double? size = 2.5, double? limit = 10, bool structural = false, DateOnly? entry = null) =>
        new(gain, entry ?? Today.AddDays(-30), Today, Now, size, limit, false, structural, "Rebalance");

    private static GateOutcome Run(string action, string trend, string momentum, string role, PortfolioEvidence e) =>
        PortfolioEvidenceGate.Apply(Decision(action, trend, momentum), role, e);

    [Fact]
    public void S1_Overbought_Alone_StaysOverboughtCaution()
    {
        var o = Run(FinalActionEngine.OverboughtCaution, "Constructive Extended", "Warning — Overbought Run", "Strategic", Evidence(gain: 5));
        Assert.Equal(FinalActionEngine.OverboughtCaution, o.Decision.FinalAction);
    }

    [Fact]
    public void S2_ExtendedProfitable_NoReversal_IsNotATrim()
    {
        var o = Run(FinalActionEngine.OverboughtCaution, "Constructive Extended", "Warning — Overbought Run", "Strategic", Evidence(gain: 25));
        Assert.Equal(FinalActionEngine.OverboughtCaution, o.Decision.FinalAction);
    }

    [Fact]
    public void S3_BrkSnapshot_EarlyDeterioration_IsProfitProtectionWatch_BelowAllocationWatchThreshold()
    {
        var o = Run("Hold / Technical Caution", "Technical Caution", "Warning — Overbought Run", "Strategic", Evidence(gain: 15.7, size: 2.5));
        Assert.Equal("Profit Protection Watch", o.Decision.FinalAction);
        Assert.Contains(GateReasons.ProfitProtectionWatch, o.ReasonCodes);
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(o.Decision.FinalAction, "Portfolio", 10m));
    }

    [Fact]
    public void S4_ConfirmedSellTrigger_IsConsiderPartialTrim_AndAdvisoryOnly()
    {
        var o = Run("Trim / Review", "Constructive Extended", "Active Sell Trigger", "Strategic", Evidence(gain: 15.7, size: 2.5));
        Assert.Equal("Consider Partial Trim", o.Decision.FinalAction);
        Assert.Contains(GateReasons.TacticalTrimConfirmed, o.ReasonCodes);
        Assert.DoesNotContain(GateReasons.TechnicalOnly, o.ReasonCodes);
        Assert.Equal("Trim / Review", o.OriginalAction);
        Assert.Contains("Advisory only", o.Decision.FinalActionReason);
        Assert.Equal("ma-reduce", o.Decision.FinalActionClass);
    }

    [Fact]
    public void S4b_SellTriggerMaskedByTechnicalCaution_StillConsiderPartialTrim()
    {
        var o = Run("Hold / Technical Caution", "Technical Caution", "Active Sell Trigger", "Strategic", Evidence(gain: 15.7));
        Assert.Equal("Consider Partial Trim", o.Decision.FinalAction);
    }

    [Fact]
    public void S5_AboveConfiguredMaximum_KeepsRebalanceTrim()
    {
        var o = Run("Trim / Review", "Constructive Extended", "Active Sell Trigger", "Strategic", Evidence(gain: 20, size: 12, limit: 10));
        Assert.Equal("Trim / Review", o.Decision.FinalAction);
        Assert.Contains(GateReasons.AllocationBreach, o.ReasonCodes);
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(null)]
    public void S6_SmallOrUnknownGain_NoAutomaticTrim(double? gain)
    {
        var o = Run("Trim / Review", "Constructive Extended", "Active Sell Trigger", "Strategic", Evidence(gain));
        Assert.Equal("Hold / Monitor", o.Decision.FinalAction);
    }

    [Fact]
    public void S7_StructuralBreakdown_PreservesExistingTrim()
    {
        var o = Run("Trim / Review", "Constructive Extended", "Active Sell Trigger", "Strategic", Evidence(gain: 20, structural: true));
        Assert.Equal("Trim / Review", o.Decision.FinalAction);
        Assert.Contains(GateReasons.StructuralBreakdown, o.ReasonCodes);
    }

    [Fact]
    public void EntryDayEvidence_IsNotTacticalEvidence()
    {
        var o = Run("Trim / Review", "Constructive Extended", "Active Sell Trigger", "Strategic", Evidence(gain: 20, entry: Today));
        Assert.Equal("Hold / Monitor", o.Decision.FinalAction);
    }

    [Fact]
    public void OtherRoles_AreUnchanged()
    {
        Assert.Equal("Hold / Monitor", Run("Trim / Review", "Constructive Extended", "Active Sell Trigger", "Core", Evidence(gain: 20)).Decision.FinalAction);
    }

    [Fact]
    public void RaisedThreshold_DisablesTactical()
    {
        var e = Evidence(gain: 15.7) with { TacticalMinGainPct = 20 };
        Assert.Equal("Hold / Monitor", Run("Trim / Review", "Constructive Extended", "Active Sell Trigger", "Strategic", e).Decision.FinalAction);
    }

    [Fact]
    public void Catalog_ClassifiesNewLabels()
    {
        Assert.Equal("TrimExit", FinalActionCatalog.Describe("Consider Partial Trim").Category);
        Assert.Equal("Advisory", FinalActionCatalog.Describe("Profit Protection Watch").Category);
    }
}
