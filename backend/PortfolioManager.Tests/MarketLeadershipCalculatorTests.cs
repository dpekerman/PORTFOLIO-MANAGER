using FluentAssertions;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public class MarketLeadershipCalculatorTests
{
    [Fact]
    public void Analyze_UsesNonOverlappingReturnPeriods()
    {
        var closes = Enumerable.Repeat(100m, 200).ToList();
        closes[^41] = 80m;
        closes[^21] = 90m;
        closes[^11] = 95m;
        closes[^6] = 100m;
        closes[^1] = 110m;

        var analysis = MarketLeadershipCalculator.Analyze(closes);

        analysis.TwentyDayReturnPct.Should().Be(22.22m);
        analysis.PreviousTwentyDayReturnPct.Should().Be(12.5m);
        analysis.FiveDayReturnPct.Should().Be(10m);
        analysis.PreviousFiveDayReturnPct.Should().Be(5.26m);
    }

    [Fact]
    public void Analyze_IdentifiesEmergingEarlyRecoveryWithNegativeTwentyDayReturn()
    {
        var closes = Enumerable.Repeat(100m, 200).ToList();
        for (var index = closes.Count - 50; index < closes.Count; index++)
            closes[index] = 90m;
        closes[^41] = 110m;
        closes[^21] = 100m;
        closes[^11] = 92m;
        closes[^6] = 94m;
        closes[^1] = 98m;

        var analysis = MarketLeadershipCalculator.Analyze(closes);

        analysis.TwentyDayReturnPct.Should().BeNegative();
        analysis.TwentyDayReturnPct.Should().BeGreaterThan(analysis.PreviousTwentyDayReturnPct);
        analysis.MomentumState.Should().Be("Accelerating");
        analysis.LeadershipSignal.Should().Be("Emerging");
    }

    [Fact]
    public void Analyze_ReportsUnavailableWhenThereIsNotEnoughHistory()
    {
        var analysis = MarketLeadershipCalculator.Analyze(Enumerable.Repeat(100m, 199).ToList());

        analysis.HasTechnicalData.Should().BeFalse();
        analysis.TrendState.Should().Be("Unavailable");
    }

    [Theory]
    [InlineData(4d, 1d, 8d, 4d, "Accelerating")]
    [InlineData(2d, 3d, 8d, 6d, "Positive")]
    [InlineData(-2d, 1d, 12d, 6d, "Weakening")]
    [InlineData(-3d, 1d, -6d, -2d, "Declining")]
    public void Analyze_ClassifiesMomentumWithRequiredPrecedence(
        double currentFiveDay,
        double previousFiveDay,
        double currentTwentyDay,
        double previousTwentyDay,
        string expected)
    {
        MarketLeadershipCalculator.ClassifyMomentum(
            (decimal)currentFiveDay,
            (decimal)previousFiveDay,
            (decimal)currentTwentyDay,
            (decimal)previousTwentyDay).Should().Be(expected);
    }

    [Fact]
    public void ClassifySignal_CoolingScenarioAboveSma200WithNegativeFiveDayAndPositiveTwentyDay()
    {
        MarketLeadershipCalculator.ClassifySignal(
            105m,
            110m,
            100m,
            -3m,
            12m,
            6m,
            "Constructive",
            "Weakening").Should().Be("Cooling");
    }

    [Theory]
    [InlineData(110d, 105d, 100d, 2d, 5d, 3d, "Bullish", "Positive", "Leading")]
    [InlineData(105d, 110d, 100d, -3d, 12d, 6d, "Constructive", "Weakening", "Cooling")]
    [InlineData(110d, 105d, 100d, 4d, 6d, 4d, "Recovering", "Accelerating", "Emerging")]
    public void ClassifySignal_UsesRequiredLeadingCoolingAndEmergingRules(
        double price,
        double sma50,
        double sma200,
        double fiveDay,
        double twentyDay,
        double previousTwentyDay,
        string trend,
        string momentum,
        string expected)
    {
        MarketLeadershipCalculator.ClassifySignal(
            (decimal)price, (decimal)sma50, (decimal)sma200, (decimal)fiveDay,
            (decimal)twentyDay, (decimal)previousTwentyDay, trend, momentum).Should().Be(expected);
    }
}