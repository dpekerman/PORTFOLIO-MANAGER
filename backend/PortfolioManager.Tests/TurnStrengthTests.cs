using FluentAssertions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>
/// Tests for Turn Strength and Stage Status classification (StagedSignalService).
/// </summary>
public class TurnStrengthTests
{
    private const decimal EarlyMin    = 0.25m;
    private const decimal NormalMin   = 1.0m;
    private const decimal StrongMin   = 5.0m;
    private const decimal ExplosiveMin = 10.0m;

    private static string Strength(decimal? delta, ScanType type) =>
        StagedSignalService.ComputeTurnStrength(delta, type, EarlyMin, NormalMin, StrongMin, ExplosiveMin);

    // ── Oversold (positive deltas) ─────────────────────────────────────────────
    [Theory]
    [InlineData(0.26, "Early")]
    [InlineData(0.99, "Early")]
    [InlineData(1.00, "Normal")]
    [InlineData(4.99, "Normal")]
    [InlineData(5.00, "Strong")]
    [InlineData(9.99, "Strong")]
    [InlineData(10.00, "Explosive")]
    [InlineData(14.82, "Explosive")]   // DR.TO scenario
    public void Oversold_PositiveDelta_CorrectStrength(double delta, string expected)
    {
        Strength((decimal)delta, ScanType.Oversold).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.0)]    // no movement
    [InlineData(0.24)]   // below Early threshold
    [InlineData(-1.5)]   // still falling
    public void Oversold_NegativeOrSmallDelta_ReturnsEmpty(double delta)
    {
        Strength((decimal)delta, ScanType.Oversold).Should().BeEmpty(
            "a turn only fires when delta exceeds EarlyMin in the correct direction");
    }

    [Fact]
    public void NullDelta_ReturnsEmpty()
    {
        Strength(null, ScanType.Oversold).Should().BeEmpty();
    }

    // ── Overbought (negative deltas signal a Bear Turn) ───────────────────────
    [Theory]
    [InlineData(-0.26, "Early")]
    [InlineData(-1.42, "Normal")]
    [InlineData(-5.50, "Strong")]
    [InlineData(-10.0, "Explosive")]
    public void Overbought_NegativeDelta_CorrectStrength(double delta, string expected)
    {
        Strength((decimal)delta, ScanType.Overbought).Should().Be(expected);
    }

    // ── Chase Risk ─────────────────────────────────────────────────────────────
    [Fact]
    public void ExplosiveTurn_SetsChaseRiskElevated()
    {
        var strength = Strength(14.82m, ScanType.Oversold);
        var chaseRisk = strength == "Explosive" ? "Elevated" : string.Empty;

        strength.Should().Be("Explosive");
        chaseRisk.Should().Be("Elevated");
    }

    [Fact]
    public void NonExplosiveTurn_ChaseRiskIsEmpty()
    {
        var strength = Strength(1.5m, ScanType.Oversold);
        var chaseRisk = strength == "Explosive" ? "Elevated" : string.Empty;

        strength.Should().Be("Normal");
        chaseRisk.Should().BeEmpty();
    }

    // ── Stage Status ──────────────────────────────────────────────────────────
    [Fact]
    public void NullDelta_StageStatusIsStaged()
    {
        StagedSignalService.ComputeStageStatus(null, "Waiting").Should().Be("STAGED");
    }

    [Fact]
    public void BullTurn_StageStatusIsConfirming()
    {
        StagedSignalService.ComputeStageStatus(1.42m, "🟢 Bull Turn").Should().Be("CONFIRMING");
    }

    [Fact]
    public void BearTurn_StageStatusIsConfirming()
    {
        StagedSignalService.ComputeStageStatus(-2.5m, "🟢 Bear Turn").Should().Be("CONFIRMING");
    }

    [Fact]
    public void StillFalling_StageStatusIsTracking()
    {
        StagedSignalService.ComputeStageStatus(-1.1m, "🔴 Still Falling").Should().Be("TRACKING");
    }

    [Fact]
    public void Stabilizing_StageStatusIsTracking()
    {
        StagedSignalService.ComputeStageStatus(0.10m, "🟡 Stabilizing").Should().Be("TRACKING");
    }
}
