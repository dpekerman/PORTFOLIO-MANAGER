using PortfolioManager.Api.Services.FinalAction;
using Xunit;

namespace PortfolioManager.Tests;

/// <summary>PM-024 Stage 1/3: profit-taking and trailing stops are role-aware and can never become a Trim without an independent reason. Synthetic fixtures only.</summary>
public sealed class FinalActionProfitRoleTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);
    private static readonly DateTime Now = new(2026, 10, 27, 16, 0, 0, DateTimeKind.Utc);

    /// <summary>Uptrend (EMA10 &gt; EMA20, price &gt; SMA50) with RSI above 70 and a top-half close: "Constructive Extended".</summary>
    private static FinalActionInput Overbought(double rsi = 75.7, double dayLow = 98, double close = 100) => new()
    {
        Symbol = "SYN", Rsi = rsi, CurrentPrice = close, ChangePercent = 0.5, DayHigh = 101, DayLow = dayLow, OpenPrice = 99.5,
        PreviousClose = 99.4, VolumeRatio = 1.0, RsiSignal = rsi - 1.5, RsiSignalAvailable = true, MacdHistDelta = 0.1,
        Ema9 = 99, Ema10 = 98, Ema20 = 96, Sma20 = 95, Sma50 = 90, Sma200 = 80, Week52High = 101,
    };

    /// <summary>Same trend, closing in the bottom half with weakening MACD from an overbought level: "Active Sell Trigger".</summary>
    private static FinalActionInput SellTrigger() => Overbought() with { CurrentPrice = 98.5, DayLow = 98, DayHigh = 101, MacdHistDelta = -0.2, VolumeRatio = 1.5 };

    private static PortfolioItemContext Context(double gain = 27, string account = "Margin", double? size = 3.5, double dist52 = -1) =>
        new(account, gain, 400, dist52, size);

    private static PortfolioEvidence Evidence(
        double? gain = 27, double? size = 3.5, double? limit = 10, bool structural = false, DateOnly? entry = null, ValuationCheck? valuation = null) =>
        new(gain, entry ?? Today.AddDays(-400), Today, Now, size, limit, false, structural, "Rebalance", Valuation: valuation);

    private static GateOutcome Run(FinalActionInput input, string role, PortfolioItemContext ctx, PortfolioEvidence ev)
    {
        var raw = FinalActionEngine.TranslateForPortfolio(input, role, ctx);
        return PortfolioEvidenceGate.Apply(raw, role, ev);
    }

    private static void AssertNotAReduction(string action)
    {
        Assert.NotEqual("TrimExit", FinalActionCatalog.Describe(action).Category);
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(action, "Portfolio", 10m));
    }

    // 1. BRK.TO: Strategic, overbought, profitable, near the high, no independent exit reason -> no Trim.
    [Theory]
    [InlineData(27)]   // "Profit Watch / No Add / Trail Stop" rule
    [InlineData(35)]   // "Trim 20–33% / Hold Runner" rule
    [InlineData(55)]   // "Trim 25% / Keep Runner" rule
    public void Brk_StrategicOverboughtProfitable_NoIndependentExit_IsOverboughtCautionNotTrim(double gain)
    {
        var raw = FinalActionEngine.TranslateForPortfolio(Overbought(), "Strategic", Context(gain));
        if (gain >= 30) Assert.Equal("TrimExit", FinalActionCatalog.Describe(raw.FinalAction).Category);   // the legacy rule alone would sell

        var o = PortfolioEvidenceGate.Apply(raw, "Strategic", Evidence(gain));
        Assert.Equal("Hold / Overbought Caution — No Add", o.Decision.FinalAction);
        AssertNotAReduction(o.Decision.FinalAction);
        Assert.Equal("ma-hold", o.Decision.FinalActionClass);
        Assert.Equal(raw.FinalAction, o.OriginalAction);                      // the rule output stays visible as evidence
        Assert.Contains(GateReasons.OverboughtCaution, o.ReasonCodes);
        Assert.Contains("not an independent exit reason", o.Decision.FinalActionReason);
    }

    [Fact]
    public void Brk_WithoutAnyProfitRule_TheEngineItselfSaysOverboughtCaution()
    {
        var d = FinalActionEngine.TranslateForPortfolio(Overbought(), "Strategic", Context(gain: 3, dist52: -20));
        Assert.Equal("Hold / Overbought Caution — No Add", d.FinalAction);
        Assert.Equal("ma-hold", d.FinalActionClass);
        Assert.Equal(("hold", "INFORMATIONAL"), FinalActionEngine.ActionCenterClassification(d.FinalActionClass));
    }

    // 2. Strategic with a genuinely independent exit reason keeps the reduction.
    [Fact]
    public void Strategic_IndependentStructuralBreakdown_KeepsTheReduction()
    {
        var o = Run(SellTrigger(), "Strategic", Context(gain: 3, dist52: -20), Evidence(gain: 3, structural: true));
        Assert.Equal("Trim / Review", o.Decision.FinalAction);
        Assert.Contains(GateReasons.StructuralBreakdown, o.ReasonCodes);
    }

    [Theory]
    [InlineData("Strategic")]
    [InlineData("Core")]
    public void LongRole_ConfiguredAllocationBreach_KeepsAProfitTakingTrim_WithoutHidingIt(string role)
    {
        var raw = FinalActionEngine.TranslateForPortfolio(Overbought(), role, Context(35, size: 12));
        Assert.StartsWith("Trim", raw.FinalAction);
        var o = PortfolioEvidenceGate.Apply(raw, role, Evidence(35, size: 12, limit: 10));
        Assert.Equal(raw.FinalAction, o.Decision.FinalAction);
        Assert.Contains(GateReasons.AllocationBreach, o.ReasonCodes);
    }

    [Fact]
    public void LongRole_ProfitTrim_WithConfirmedStructuralBreakdown_IsKept_ButNotOnTheEntryDay()
    {
        var raw = FinalActionEngine.TranslateForPortfolio(Overbought(), "Strategic", Context(35));
        Assert.Equal(raw.FinalAction, PortfolioEvidenceGate.Apply(raw, "Strategic", Evidence(35, structural: true)).Decision.FinalAction);
        Assert.NotEqual(raw.FinalAction, PortfolioEvidenceGate.Apply(raw, "Strategic", Evidence(35, structural: true, entry: Today)).Decision.FinalAction);
    }

    // 3. TFSA profit-taking rule without an independent reason is advisory for a long-term role.
    [Fact]
    public void Tfsa_ProfitTakeRule_WithoutIndependentReason_IsAdvisoryForLongTermRoles()
    {
        var input = Overbought(rsi: 70) with { AnalystTargetPrice = 95 };
        var ctx = new PortfolioItemContext("TFSA", 25, 90, -30, 3);
        var raw = FinalActionEngine.TranslateForPortfolio(input, "Strategic", ctx);
        Assert.Equal("Take Partial Profit / No Chase", raw.FinalAction);

        var o = PortfolioEvidenceGate.Apply(raw, "Strategic", Evidence(25));
        AssertNotAReduction(o.Decision.FinalAction);
        Assert.Contains(o.Decision.FinalAction, new[] { "Hold / Overbought Caution — No Add", "Hold / Profit Watch — No Add" });
        Assert.Equal("Take Partial Profit / No Chase", o.OriginalAction);
    }

    [Fact]
    public void Tfsa_ProfitTakeRule_StillAppliesWithAnIndependentReason()
    {
        var input = Overbought(rsi: 70) with { AnalystTargetPrice = 95 };
        var raw = FinalActionEngine.TranslateForPortfolio(input, "Strategic", new PortfolioItemContext("TFSA", 25, 90, -30, 12));
        var o = PortfolioEvidenceGate.Apply(raw, "Strategic", Evidence(25, size: 12, limit: 10));
        Assert.Equal("Take Partial Profit / No Chase", o.Decision.FinalAction);
    }

    // 4. DIS.TO: constructive, extended Swing -> trail-stop advisory, never a Trim.
    [Fact]
    public void Dis_SwingConstructiveExtended_IsTrailStopAdvisory_NotTrim()
    {
        var d = FinalActionEngine.TranslateForPortfolio(Overbought(), "Swing", Context(gain: 11, dist52: -20));
        Assert.Equal("Hold / Trail Stop — No Add", d.FinalAction);
        AssertNotAReduction(d.FinalAction);
        Assert.Equal("ma-hold", d.FinalActionClass);

        var gated = PortfolioEvidenceGate.Apply(d, "Swing", Evidence(11, size: 2, entry: Today.AddDays(-20)));
        Assert.Equal("Hold / Trail Stop — No Add", gated.Decision.FinalAction);
        Assert.Empty(gated.ReasonCodes);
    }

    [Fact]
    public void Dis_SwingProfitTaking_WithoutSellTrigger_IsTrailStopAdvisory_ButAConfirmedSellTriggerKeepsTheExit()
    {
        var raw = FinalActionEngine.TranslateForPortfolio(Overbought(), "Swing", Context(35));
        Assert.StartsWith("Trim", raw.FinalAction);
        var advisory = PortfolioEvidenceGate.Apply(raw, "Swing", Evidence(35, entry: Today.AddDays(-20)));
        Assert.Equal("Hold / Trail Stop — No Add", advisory.Decision.FinalAction);

        var sell = FinalActionEngine.TranslateForPortfolio(SellTrigger(), "Swing", Context(10, dist52: -20));
        Assert.Equal("Exit / Take Profit", sell.FinalAction);
        Assert.Equal("Exit / Take Profit", PortfolioEvidenceGate.Apply(sell, "Swing", Evidence(10, entry: Today.AddDays(-20))).Decision.FinalAction);

        var profitOnSell = FinalActionEngine.TranslateForPortfolio(SellTrigger(), "Swing", Context(35));
        Assert.Equal(profitOnSell.FinalAction, PortfolioEvidenceGate.Apply(profitOnSell, "Swing", Evidence(35, entry: Today.AddDays(-20))).Decision.FinalAction);
    }

    // 5. Speculative: overbought alone never reduces.
    [Fact]
    public void Speculative_Overbought_NoIndependentSellEvidence_IsNotAReduction()
    {
        var d = FinalActionEngine.TranslateForPortfolio(Overbought(), "Speculative", Context(gain: 5, dist52: -20));
        Assert.Equal("Hold Spec / Trail Stop — No Add", d.FinalAction);
        AssertNotAReduction(d.FinalAction);

        var profit = FinalActionEngine.TranslateForPortfolio(Overbought(), "Speculative", Context(55));
        var o = PortfolioEvidenceGate.Apply(profit, "Speculative", Evidence(55, entry: Today.AddDays(-30)));
        Assert.Equal("Hold Spec / Trail Stop — No Add", o.Decision.FinalAction);
    }

    // No bypass: no Trim/Sell/Profit-take label of the catalog survives for a long-term role without an independent trigger.
    [Fact]
    public void EveryCatalogReduction_IsAdvisoryForLongRoles_WithoutAnIndependentTrigger()
    {
        var reductions = FinalActionCatalog.All
            .Where(a => a.Category == "TrimExit" && a.Group is "Portfolio" or "Base" or "Profit-taking" && a.Id != "TRIM/EXIT WATCH")
            .ToList();
        Assert.True(reductions.Count >= 20);
        foreach (var role in new[] { "Strategic", "Core" })
            foreach (var a in reductions)
            {
                var decision = new FinalActionDecision(a.Label, "Constructive Extended", "", "Warning — Overbought Run", "", "Stand By", "No Gap", 0, 0, "",
                    FinalActionEngine.FinalActionClass(a.Label), "", "", null, null);
                var o = PortfolioEvidenceGate.Apply(decision, role, Evidence(40));
                Assert.True(FinalActionCatalog.Describe(o.Decision.FinalAction).Category != "TrimExit",
                    $"{role}: '{a.Label}' bypassed the gate and produced '{o.Decision.FinalAction}'");
            }
    }

    [Fact]
    public void EveryProfitTakingReduction_IsAdvisoryForShortRoles_WithoutASellTrigger()
    {
        var profitLabels = FinalActionCatalog.All.Where(a => a.Group == "Profit-taking" && a.Category == "TrimExit");
        foreach (var role in new[] { "Swing", "Speculative" })
            foreach (var a in profitLabels)
            {
                var decision = new FinalActionDecision(a.Label, "Constructive Extended", "", "Warning — Overbought Run", "", "Stand By", "No Gap", 0, 0, "",
                    FinalActionEngine.FinalActionClass(a.Label), "", "", null, null);
                var o = PortfolioEvidenceGate.Apply(decision, role, Evidence(40, entry: Today.AddDays(-20)));
                Assert.NotEqual("TrimExit", FinalActionCatalog.Describe(o.Decision.FinalAction).Category);
            }
    }

    [Fact]
    public void NewAdvisoryLabels_AreKnown_Advisory_AndNeverClassifiedAsBuyOrSell()
    {
        foreach (var l in new[] { "Hold / Overbought Caution — No Add", "Hold / Trail Stop — No Add", "Hold Spec / Trail Stop — No Add",
                     "Hold / Profit Watch — No Add" })
        {
            Assert.True(FinalActionCatalog.IsKnown(l));
            Assert.Equal("Advisory", FinalActionCatalog.Describe(l).Category);
            Assert.Equal("ma-hold", FinalActionEngine.FinalActionClass(l));
            Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(l, "Portfolio", 10m));
        }
        Assert.Equal("Advisory", FinalActionCatalog.Describe("Review Stop Breach").Category);
        Assert.Equal(DecisionBucket.RiskReview, DecisionEligibility.Classify("Review Stop Breach", "Portfolio", 10m));
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify("Review Stop Breach", "Watchlist", 10m));
        // Legacy labels stay known so persisted history and Signal Tracker records still resolve.
        Assert.True(FinalActionCatalog.IsKnown("Trail Stop / Trim"));
        Assert.True(FinalActionCatalog.IsKnown("Trim / Trail Stop"));
    }
}
