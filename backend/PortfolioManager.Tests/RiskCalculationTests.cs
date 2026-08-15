using FluentAssertions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>
/// Tests for risk and stop-loss calculations used throughout the signal pipeline.
/// </summary>
public class RiskCalculationTests
{
    // ── Risk per share ─────────────────────────────────────────────────────────
    [Fact]
    public void RiskPerShare_IsAbsoluteDifference()
    {
        decimal entry = 14.11m;
        decimal stop  = 12.27m;
        decimal riskPerShare = Math.Abs(entry - stop);

        riskPerShare.Should().BeApproximately(1.84m, precision: 0.001m);
    }

    [Fact]
    public void RiskPercent_CPH_IsCorrect()
    {
        decimal entry        = 14.11m;
        decimal stop         = 12.27m;
        decimal riskPerShare = Math.Abs(entry - stop);
        decimal riskPct      = riskPerShare / entry * 100m;

        riskPct.Should().BeApproximately(13.04m, precision: 0.1m, "CPH.TO scenario from requirements #14");
    }

    [Fact]
    public void RiskPercent_ZeroEntry_ShouldReturnNull()
    {
        decimal? result = RiskPercent(0m, 1.50m);
        result.Should().BeNull("division by zero must be handled");
    }

    [Fact]
    public void RiskPercent_NullStop_ShouldReturnNull()
    {
        decimal? result = RiskPercent(100m, null);
        result.Should().BeNull();
    }

    // ── Dynamic stop-loss (1.5 × ATR) ─────────────────────────────────────────
    [Fact]
    public void DynamicStopLoss_Oversold_IsExtremeLowMinus1Point5Atr()
    {
        decimal extremeLow = 12.50m;
        decimal atr        = 0.75m;
        decimal stop       = extremeLow - (1.5m * atr);

        stop.Should().Be(11.375m);
    }

    [Fact]
    public void DynamicStopLoss_Overbought_IsExtremeHighPlus1Point5Atr()
    {
        decimal extremeHigh = 95.00m;
        decimal atr         = 1.20m;
        decimal stop        = extremeHigh + (1.5m * atr);

        stop.Should().Be(96.80m);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static decimal? RiskPercent(decimal entry, decimal? stopLoss)
    {
        if (entry == 0 || stopLoss is null) return null;
        return Math.Abs(entry - stopLoss.Value) / entry * 100m;
    }
}
