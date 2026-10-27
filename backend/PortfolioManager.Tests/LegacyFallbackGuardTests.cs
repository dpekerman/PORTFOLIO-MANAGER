using System.Reflection;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

/// <summary>Public-behaviour guards that replace the two deleted reflection tests on the removed legacy action rules.</summary>
public sealed class LegacyFallbackGuardTests
{
    [Fact]
    public void ActionCenterRowsService_HasNoDecisionRulesLeft()
    {
        var methods = typeof(PortfolioActionsService).GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(m => m.Name).ToList();
        Assert.DoesNotContain("DeriveAction", methods);
        Assert.DoesNotContain("ApplyEodContext", methods);
        Assert.DoesNotContain("DeriveChannelAction", methods);
    }

    [Theory]
    [InlineData(ScanType.Oversold, SignalStatus.EodConfirm, "🟢 Bull Turn")]
    [InlineData(ScanType.Oversold, SignalStatus.Confirmed, "🟡 Stabilizing")]
    [InlineData(ScanType.Oversold, SignalStatus.Neutral, "")]
    [InlineData(ScanType.Overbought, SignalStatus.Confirmed, "🟢 Bear Turn")]
    [InlineData(ScanType.Overbought, SignalStatus.Neutral, "")]
    public void ScannerOnlyLabels_ForSymbolsNeitherOwnedNorWatched_AreNeverTradeDecisions(ScanType scan, SignalStatus status, string trend)
    {
        var signal = new RsiScanResult { ScanType = scan, Status = status, TrendShift = trend };
        var label = DashboardSignalActionInterpreter.Resolve(signal, isInPortfolio: false, isInWatchlist: false);
        var def = FinalActionCatalog.Find(label);
        // Descriptive watch / caution wording only: none of it is an Entry/Add or Trim/Exit decision, and it is never "required".
        Assert.True(def is null || def.Category == "Advisory", $"'{label}' must not be an actionable decision");
        Assert.DoesNotContain(label, new[] { "ENTRY CANDIDATE", "STARTER ENTRY", "ADD CANDIDATE", "TRIM", "EXIT REVIEW" });
    }

    [Fact]
    public void AllCanonicalAdvisoryStatuses_AreNeverActionable_IncludingAwaitingData()
    {
        foreach (var label in new[] { "Awaiting Data", "Add / Needs Review", "Hold / Monitor", "Hold / Catalyst Monitoring", "Hold / No Add", "AVOID", "WAIT FOR PULLBACK" })
            Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(label, "Portfolio", 10m));
    }

    [Fact]
    public void HardStructuralNegative_OnTheWatchlist_StillProducesAvoid_ThroughTheCanonicalEngine()           // replaces the ATD legacy test
    {
        var structure = new FinalActionPriceStructure(true, "SUPPORT", "SUPPORT_TEST", "BREAKDOWN", "TIGHT_RISING_WEDGE");
        var input = new FinalActionInput
        {
            Symbol = "ATD.TO", Rsi = 29.2, CurrentPrice = 100, Ema9 = 99, Ema10 = 98, Ema20 = 97, Sma20 = 98, Sma50 = 95, Sma200 = 90,
            OpenPrice = 99, PreviousClose = 98, DayHigh = 101, DayLow = 98, VolumeRatio = 1, TrendShift = "🟢 Bull Turn",
            PriceStructure = structure,
        };
        var decision = FinalActionEngine.TranslateForWatchlist(input, "Strategic");
        // The canonical engine (parity-locked with the Angular spec) words a hard structural negative as WAIT FOR RECLAIM / AVOID:
        // never an Entry, whatever the bullish trend shift says.
        Assert.Contains(decision.FinalAction, new[] { "AVOID", "WAIT FOR RECLAIM" });
        Assert.NotEqual("Entry", FinalActionCatalog.Describe(decision.FinalAction).Category);
        Assert.False(DecisionEligibility.IsActionable(new FinalActionResultDto("ATD.TO", "Watchlist", null, "Strategic", decision.FinalAction,
            FinalActionCatalog.CanonicalId(decision.FinalAction), "Advisory", "Neutral", "wait", "DEVELOPING", decision.FinalActionClass, "", "", "",
            "", "", null, true, 100m, "ATD.TO", null)));
    }
}
