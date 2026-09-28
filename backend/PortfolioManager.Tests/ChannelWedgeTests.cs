using FluentAssertions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public class ChannelWedgeTests
{
    [Fact]
    public void ValidFallingWedge_RequiresFasterDescendingUpperRailAndContraction() =>
        ChannelAnalysisService.IsValidFallingWedge(-0.50m, -0.20m, 10m, 5m).Should().BeTrue();

    [Fact]
    public void InvalidFallingWedge_RejectsFasterDescendingLowerRail() =>
        ChannelAnalysisService.IsValidFallingWedge(-0.20m, -0.50m, 10m, 5m).Should().BeFalse();

    [Fact]
    public void ValidRisingWedge_RequiresFasterAscendingLowerRailAndContraction() =>
        ChannelAnalysisService.IsValidRisingWedge(0.20m, 0.50m, 10m, 6m).Should().BeTrue();

    [Fact]
    public void FallingWedgeBreakout_UsesQuarterAtrThreshold() =>
        ChannelAnalysisService.IsFallingWedgeBreakout(102m, 100m, 4m).Should().BeTrue();

    [Fact]
    public void RisingWedgeBreakdown_UsesQuarterAtrThreshold() =>
        ChannelAnalysisService.IsRisingWedgeBreakdown(98m, 100m, 4m).Should().BeTrue();

    [Theory]
    [InlineData(2, 2, 8)]
    [InlineData(3, 3, 15)]
    [InlineData(4, 4, 20)]
    [InlineData(8, 8, 20)]
    [InlineData(6, 2, 8)]
    public void WedgeTouchScore_UsesBalancedIndependentTouchesOnly(int upperTouches, int lowerTouches, int expected) =>
        ChannelAnalysisService.CalculateWedgeTouchScore(upperTouches, lowerTouches).Should().Be(expected);

    [Theory]
    [InlineData(0.29, 0)]
    [InlineData(0.30, 8)]
    [InlineData(0.40, 12)]
    [InlineData(0.50, 16)]
    [InlineData(0.65, 20)]
    public void WedgeContractionScore_UsesConfiguredBands(decimal contraction, int expected) =>
        ChannelAnalysisService.CalculateWedgeContractionScore(contraction).Should().Be(expected);

    [Theory]
    [InlineData(15, 10)]
    [InlineData(60, 8)]
    [InlineData(120, 4)]
    [InlineData(180, 2)]
    [InlineData(181, 0)]
    public void WedgeApexScore_DeprioritizesFarAwayApex(int daysToApex, int expected) =>
        ChannelAnalysisService.CalculateWedgeApexScore(daysToApex).Should().Be(expected);

    [Fact]
    public void WedgeQuality_DoesNotRewardRawPivotDensityBeyondIndependentTouchCap()
    {
        var qualityWithFourIndependentTouchesPerRail = ChannelAnalysisService.CalculateWedgeQuality(
            upperFitQuality: 0.95m,
            lowerFitQuality: 0.95m,
            independentUpperTouchCount: 4,
            independentLowerTouchCount: 4,
            contraction: 0.50m,
            daysToApex: 60,
            railViolationCount: 0,
            upperSlope: -0.50m,
            lowerSlope: -0.20m,
            fallingWedge: true);

        var qualityWithClusteredRawPivotNoise = ChannelAnalysisService.CalculateWedgeQuality(
            upperFitQuality: 0.95m,
            lowerFitQuality: 0.95m,
            independentUpperTouchCount: 9,
            independentLowerTouchCount: 11,
            contraction: 0.50m,
            daysToApex: 60,
            railViolationCount: 0,
            upperSlope: -0.50m,
            lowerSlope: -0.20m,
            fallingWedge: true);

        qualityWithClusteredRawPivotNoise.Should().Be(qualityWithFourIndependentTouchesPerRail);
    }

    [Fact]
    public void KeyLevel_ApproachingResistance_UsesAtrDistanceAndUpwardApproach() =>
        ChannelAnalysisService.ResolveKeyLevelState(100m, 10m, 94m, 95m, 92m, 94m, 93m)
            .Should().Be("APPROACHING_RESISTANCE");

    [Fact]
    public void KeyLevel_ResistanceTest_UsesHighButDoesNotConfirmBreakoutFromClose() =>
        ChannelAnalysisService.ResolveKeyLevelState(100m, 10m, 99m, 101m, 96m, 99m, 98m)
            .Should().Be("RESISTANCE_TEST");

    [Fact]
    public void KeyLevel_BreakoutConfirmed_UsesCloseAboveTrigger() =>
        ChannelAnalysisService.ResolveKeyLevelState(100m, 10m, 103m, 104m, 99m, 103m, 99m)
            .Should().Be("BREAKOUT_CONFIRMED");

    [Fact]
    public void KeyLevel_DoesNotTreatExistingSupportAsFreshBreakout() =>
        ChannelAnalysisService.ResolveKeyLevelState(100m, 10m, 104m, 105m, 104m, 104m, 103m)
            .Should().Be("NONE");

    [Fact]
    public void KeyLevel_SupportTest_UsesLowButDoesNotConfirmBreakdownFromClose() =>
        ChannelAnalysisService.ResolveKeyLevelState(100m, 10m, 101m, 102m, 99m, 101m, 102m)
            .Should().Be("SUPPORT_TEST");

    [Fact]
    public void KeyLevel_BreakdownConfirmed_UsesCloseBelowTrigger() =>
        ChannelAnalysisService.ResolveKeyLevelState(100m, 10m, 96m, 101m, 95m, 96m, 101m)
            .Should().Be("BREAKDOWN_CONFIRMED");

    [Fact]
    public void KeyLevel_FailedBreakout_DetectsCloseBackBelowFormerResistance() =>
        ChannelAnalysisService.ResolveKeyLevelState(100m, 10m, 99m, 101m, 98m, 99m, 103m)
            .Should().Be("FAILED_BREAKOUT");

    [Fact]
    public void KeyLevel_SupportReclaim_DetectsCloseBackAboveFormerSupport() =>
        ChannelAnalysisService.ResolveKeyLevelState(100m, 10m, 101m, 102m, 99m, 101m, 96m)
            .Should().Be("SUPPORT_RECLAIM");

    [Fact]
    public void SupportReclaim_ExposesSupportAsCurrentRole() =>
        ChannelAnalysisService.ResolveCurrentRole("RESISTANCE", "SUPPORT_RECLAIM")
            .Should().Be("SUPPORT");

    [Theory]
    [InlineData("BREAKDOWN_CONFIRMED")]
    [InlineData("SUPPORT_BROKEN")]
    [InlineData("FAILED_BREAKOUT")]
    public void ConfirmedSupportFailure_ExposesResistanceAsCurrentRole(string state) =>
        ChannelAnalysisService.ResolveCurrentRole("SUPPORT", state)
            .Should().Be("RESISTANCE");

    [Fact]
    public void KeyLevel_ConfluenceZone_UsesHalfAtrLevelCluster() =>
        ChannelAnalysisService.IsConfluenceZone([100m, 101m, 102m], 10m).Should().BeTrue();

    [Fact]
    public void TightFallingWedge_RequiresStrongerContraction()
    {
        ChannelAnalysisService.IsValidTightFallingWedge(-0.50m, -0.20m, 10m, 4.5m).Should().BeTrue();
        ChannelAnalysisService.IsValidTightFallingWedge(-0.50m, -0.20m, 10m, 6.5m).Should().BeFalse();
    }

    [Fact]
    public void KeyLevel_MoreThanOneAtrAway_IsNotCurrentDecisionLevel() =>
        ChannelAnalysisService.ResolveKeyLevelState(100m, 10m, 116m, 117m, 115m, 116m, 114m, "SUPPORT")
            .Should().Be("NONE");

    [Fact]
    public void ResistanceRole_FlipsOnlyAfterPriorCompletedCloseConfirmsBreakout()
    {
        var candles = new[]
        {
            Candle(0, 98m), Candle(1, 99m), Candle(2, 103m), Candle(3, 104m),
        };

        ChannelAnalysisService.ReplayRole(candles, 100m, 4m, 0, "RESISTANCE")
            .Should().Be("SUPPORT");
    }

    [Fact]
    public void CurrentBreakout_DoesNotPrematurelyFlipResistanceRole()
    {
        var candles = new[]
        {
            Candle(0, 98m), Candle(1, 99m), Candle(2, 100m), Candle(3, 103m),
        };

        ChannelAnalysisService.ReplayRole(candles, 100m, 4m, 0, "RESISTANCE")
            .Should().Be("RESISTANCE");
        ChannelAnalysisService.ResolveKeyLevelState(100m, 4m, 103m, 104m, 99m, 103m, 100m, "RESISTANCE")
            .Should().Be("BREAKOUT_CONFIRMED");
    }

    private static ChannelCandle Candle(int day, decimal close) =>
        new(new DateTime(2026, 1, 2).AddDays(day), close, close + 1m, close - 1m, close);

    [Fact]
    public void TslaRegression_NearFibWinsBecauseDistantConfluenceFailsRelevanceGate()
    {
        ChannelAnalysisService.ResolveKeyLevelState(345.83m, 13.49m, 367.95m, 369m, 366m, 367.95m, 368m, "SUPPORT")
            .Should().Be("NONE");
        ChannelAnalysisService.ResolveKeyLevelState(365.59m, 13.49m, 367.95m, 369m, 365.2m, 367.95m, 368m, "SUPPORT")
            .Should().Be("SUPPORT_TEST");
    }

    [Fact]
    public void TtdRegression_TightWedgeRequiresQualifiedGeometryAndFortyPercentContraction()
    {
        ChannelAnalysisService.IsValidTightFallingWedge(-0.50m, -0.20m, 10m, 5.5m).Should().BeTrue();
        ChannelAnalysisService.IsValidTightFallingWedge(-0.20m, -0.50m, 10m, 5.5m).Should().BeFalse();
    }

    [Fact]
    public void MrvlRegression_RisingWedgeBreakdownRequiresEodCloseBeyondTrigger()
    {
        ChannelAnalysisService.IsRisingWedgeBreakdown(98.9m, 100m, 4m).Should().BeTrue();
        ChannelAnalysisService.IsRisingWedgeBreakdown(100.2m, 100m, 4m).Should().BeFalse();
    }

    [Fact]
    public void GdxRegression_ChannelUsesLowForTestAndCloseForBreak()
    {
        ChannelAnalysisService.ResolveChannelState(2, 0.20m, 0.10m).Should().Be(ChannelState.THIRD_TOUCH_TEST);
        ChannelAnalysisService.ResolveChannelState(3, -0.60m, -0.80m).Should().Be(ChannelState.CHANNEL_BROKEN);
    }

    [Fact]
    public void UraRegression_GenericLevelMayCorrectlyReturnNoneWhenNotRelevant() =>
        ChannelAnalysisService.ResolveKeyLevelState(80m, 5m, 90m, 91m, 89m, 90m, 89m, "SUPPORT")
            .Should().Be("NONE");

    [Fact]
    public void FalsePositiveControl_DoesNotInventCurrentStateBeyondOneAtr() =>
        ChannelAnalysisService.ResolveKeyLevelState(50m, 2m, 53m, 53.5m, 52.5m, 53m, 52.8m, "SUPPORT")
            .Should().Be("NONE");
}