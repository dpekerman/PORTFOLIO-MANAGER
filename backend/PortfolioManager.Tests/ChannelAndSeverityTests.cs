using PortfolioManager.Api.Services;
using Xunit;
using PortfolioManager.Api.Models;
using System.Reflection;

namespace PortfolioManager.Tests;

public class ChannelAndSeverityTests
{
    [Theory]
    [InlineData("ENTRY CANDIDATE", "REQUIRED")]
    [InlineData("ADD CANDIDATE", "REQUIRED")]
    [InlineData("EXIT REVIEW", "REQUIRED")]
    [InlineData("BUY WATCH", "DEVELOPING")]
    [InlineData("ADD WATCH", "DEVELOPING")]
    [InlineData("WAIT FOR REVERSAL", "DEVELOPING")]
    [InlineData("WAIT FOR PULLBACK", "INFORMATIONAL")]
    [InlineData("HOLD", "INFORMATIONAL")]
    public void ActionSeverity_UsesSharedVocabulary(string action, string expected)
    {
        Assert.Equal(expected, ActionSeverityMapper.Get(action));
    }

    [Theory]
    [InlineData(ScanType.Oversold, SignalStatus.Confirmed, "Waiting", true, false, "ADD CANDIDATE")]
    [InlineData(ScanType.Oversold, SignalStatus.Confirmed, "Waiting", false, true, "REVERSAL WATCH")]
    [InlineData(ScanType.Overbought, SignalStatus.Confirmed, "Waiting", true, false, "TRIM WATCH")]
    [InlineData(ScanType.Overbought, SignalStatus.Confirmed, "Waiting", false, true, "AVOID")]
    [InlineData(ScanType.Overbought, SignalStatus.EarlyWarning, "Bear Turn", false, false, "TECHNICAL CAUTION")]
    public void DashboardSignalAction_DependsOnOwnership(
        ScanType scanType,
        SignalStatus status,
        string trendShift,
        bool isInPortfolio,
        bool isInWatchlist,
        string expected)
    {
        var signal = new RsiScanResult
        {
            ScanType = scanType,
            Status = status,
            TrendShift = trendShift,
        };

        Assert.Equal(expected, DashboardSignalActionInterpreter.Resolve(signal, isInPortfolio, isInWatchlist));
    }

    [Fact]
    public void DashboardSignalAction_AllowsWatchlistEntryCandidateWhenStructureSupportsEntry()
    {
        var signal = new RsiScanResult
        {
            ScanType = ScanType.Oversold,
            Status = SignalStatus.Confirmed,
            TrendShift = "Bull Turn",
            PriceStructure = PriceStructureResult.None with { KeyLevelState = "SUPPORT_RECLAIM" },
        };

        Assert.Equal("ENTRY CANDIDATE", DashboardSignalActionInterpreter.Resolve(signal, false, true));
    }

    [Fact]
    public void AllocationBlock_IsAlwaysRequired()
    {
        Assert.Equal("REQUIRED", ActionSeverityMapper.Get("ENTRY CANDIDATE", allocationBlocked: true));
    }

    [Fact]
    public void TouchDetail_ContainsValidationFields()
    {
        var detail = new ChannelTouchDetail(
            2, new DateTime(2026, 5, 18), 464.80m, 463.69m, 1.52m, true);

        Assert.Equal(2, detail.TouchNumber);
        Assert.Equal(464.80m, detail.RailPrice);
        Assert.Equal(463.69m, detail.ActualLow);
        Assert.Equal(1.52m, detail.BounceATR);
        Assert.True(detail.ConfirmedBounce);
    }

    [Theory]
    [InlineData(2, 0.7, "THIRD_TOUCH_APPROACHING")]
    [InlineData(2, 0.1, "THIRD_TOUCH_TEST")]
    [InlineData(3, 0.1, "LOWER_RAIL_RETEST")]
    [InlineData(4, 0.8, "LOWER_RAIL_APPROACHING")]
    [InlineData(4, -0.7, "CHANNEL_BROKEN")]
    public void ChannelState_UsesTouchMaturityAndRailDistance(
        int confirmedTouches, decimal distanceAtr, string expected)
    {
        Assert.Equal(expected, ChannelAnalysisService.ResolveState(confirmedTouches, distanceAtr).ToString());
    }

    [Fact]
    public void PriorityTechnicalScore_AddsBoundedConstructiveStructureContext()
    {
        var baseline = new RsiScanResult { ScanType = ScanType.Oversold, Rsi = 30m };
        var constructive = new RsiScanResult
        {
            ScanType = ScanType.Oversold,
            Rsi = 30m,
            MomentumState = "Positive",
            PriceStructure = PriceStructureResult.None with { KeyLevelState = "SUPPORT_TEST" },
        };

        Assert.Equal(14m, PortfolioActionScoreService.ComputeTechnicalScore(baseline));
        Assert.Equal(20m, PortfolioActionScoreService.ComputeTechnicalScore(constructive));
    }

    [Fact]
    public void PriorityTechnicalScore_HardStructureNegativeOverridesConstructiveInputs()
    {
        var scan = new RsiScanResult
        {
            ScanType = ScanType.Oversold,
            Rsi = 20m,
            TrendShift = "🟢 Bull Turn",
            MomentumState = "Accelerating",
            PriceStructure = PriceStructureResult.None with { KeyLevelState = "FAILED_BREAKOUT" },
        };

        Assert.Equal(0m, PortfolioActionScoreService.ComputeTechnicalScore(scan));
    }

    [Fact]
    public void MrvlRegression_WedgeBreakdownRemainsHardNegativeBesideSupportTest()
    {
        var structure = PriceStructureResult.None with
        {
            PrimaryPatternType = "TIGHT_RISING_WEDGE",
            PrimaryPatternState = "BREAKDOWN",
            KeyLevelType = "CHANNEL_RAIL",
            KeyLevelRole = "SUPPORT",
            KeyLevelState = "SUPPORT_TEST",
            KeyLevelPrice = 208.07m,
        };
        var scan = new RsiScanResult
        {
            ScanType = ScanType.Oversold,
            Rsi = 30m,
            MomentumState = "Positive",
            PriceStructure = structure,
        };

        Assert.True(structure.HasHardStructuralNegative);
        Assert.Equal("SUPPORT_TEST", structure.KeyLevelState);
        Assert.Equal(0m, PortfolioActionScoreService.ComputeTechnicalScore(scan));
    }

    [Fact]
    public void AtdRegression_HardStructuralNegativeProducesAvoidDespiteBullTurn()
    {
        var structure = PriceStructureResult.None with
        {
            PrimaryPatternType = "TIGHT_RISING_WEDGE",
            PrimaryPatternState = "BREAKDOWN",
        };
        var scan = new RsiScanResult
        {
            ScanType = ScanType.Oversold,
            Status = SignalStatus.EodConfirm,
            Rsi = 29.2m,
            TrendShift = "🟢 Bull Turn",
            PriceStructure = structure,
        };
        var deriveAction = typeof(PortfolioActionsService).GetMethod(
            "DeriveAction", BindingFlags.Static | BindingFlags.NonPublic)!;

        var result = ((string label, string severity, string priority))deriveAction.Invoke(
            null, [scan, "Strategic", "under", true, structure])!;

        Assert.Equal(("AVOID", "danger", "REQUIRED"), result);
    }

    [Fact]
    public void AtdRegression_PersistedHardNegativeTakesPrecedenceOverScannerLevel()
    {
        var scannerStructure = PriceStructureResult.None with
        {
            KeyLevelType = "EMA20",
            KeyLevelState = "SUPPORT_TEST",
        };
        var persistedStructure = PriceStructureResult.None with
        {
            PrimaryPatternType = "TIGHT_RISING_WEDGE",
            PrimaryPatternState = "BREAKDOWN",
        };

        var selected = scannerStructure.HasHardStructuralNegative
            ? scannerStructure
            : persistedStructure.HasHardStructuralNegative
                ? persistedStructure
                : scannerStructure;

        Assert.True(selected.HasHardStructuralNegative);
        Assert.Equal("BREAKDOWN", selected.PrimaryPatternState);
    }

    [Fact]
    public void AtdRegression_EodContextCannotDowngradeHardNegativeAvoid()
    {
        var structure = PriceStructureResult.None with
        {
            PrimaryPatternType = "TIGHT_RISING_WEDGE",
            PrimaryPatternState = "BREAKDOWN",
        };
        var facts = new SharedTechnicalFacts(
            "ATD.TO", 29.2m, null, null, "Accelerating", structure, null, DateTime.UtcNow,
            LatestEodSignalState: "Active");
        var applyEodContext = typeof(PortfolioActionsService).GetMethod(
            "ApplyEodContext", BindingFlags.Static | BindingFlags.NonPublic)!;

        var result = ((string label, string severity, string priority))applyEodContext.Invoke(
            null, ["AVOID", "danger", "REQUIRED", facts, structure])!;

        Assert.Equal(("AVOID", "danger", "REQUIRED"), result);
    }
}
