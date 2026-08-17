using FluentAssertions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>
/// Tests for TrendShift momentum classification (StagedSignalService.ComputeTrendShift).
/// Verifies Oversold and Overbought directional logic with edge cases.
/// </summary>
public class TrendShiftTests
{
    private const decimal Threshold = 0.25m;

    private static string Shift(decimal? delta, ScanType type) =>
        StagedSignalService.ComputeTrendShift(delta, type, Threshold);

    // ── Waiting / Day 1 ───────────────────────────────────────────────────────
    [Fact]
    public void NullDelta_ReturnsWaiting() =>
        Shift(null, ScanType.Oversold).Should().Be("Waiting");

    // ── Oversold (want RSI to rise = Bull Turn) ───────────────────────────────
    [Theory]
    [InlineData(0.26)]
    [InlineData(1.42)]
    [InlineData(14.82)]
    public void Oversold_PositiveDeltaAboveThreshold_ReturnsBullTurn(double delta)
    {
        Shift((decimal)delta, ScanType.Oversold).Should().Contain("Bull Turn");
    }

    [Theory]
    [InlineData(-0.26)]
    [InlineData(-5.0)]
    public void Oversold_NegativeDeltaBelowThreshold_ReturnsStillFalling(double delta)
    {
        Shift((decimal)delta, ScanType.Oversold).Should().Contain("Still Falling");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.24)]
    [InlineData(-0.24)]
    public void Oversold_SmallDelta_ReturnsStabilizing(double delta)
    {
        Shift((decimal)delta, ScanType.Oversold).Should().Contain("Stabilizing");
    }

    // ── Overbought (want RSI to fall = Bear Turn) ─────────────────────────────
    [Theory]
    [InlineData(-0.26)]
    [InlineData(-2.5)]
    [InlineData(-10.0)]
    public void Overbought_NegativeDeltaBelowThreshold_ReturnsBearTurn(double delta)
    {
        Shift((decimal)delta, ScanType.Overbought).Should().Contain("Bear Turn");
    }

    [Theory]
    [InlineData(0.26)]
    [InlineData(3.0)]
    public void Overbought_PositiveDeltaAboveThreshold_ReturnsStillRising(double delta)
    {
        Shift((decimal)delta, ScanType.Overbought).Should().Contain("Still Rising");
    }

    // ── Boundary exactly at threshold ─────────────────────────────────────────
    [Fact]
    public void Oversold_DeltaExactlyAtThreshold_ReturnsStabilizing()
    {
        // threshold is exclusive (> not >=) — value at boundary stays Stabilizing
        Shift(Threshold, ScanType.Oversold).Should().Contain("Stabilizing");
    }

    [Fact]
    public void Oversold_DeltaJustAboveThreshold_ReturnsBullTurn()
    {
        Shift(Threshold + 0.001m, ScanType.Oversold).Should().Contain("Bull Turn");
    }
}
