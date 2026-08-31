using FluentAssertions;
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
}