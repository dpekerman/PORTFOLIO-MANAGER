using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using Xunit;

namespace PortfolioManager.Tests;

/// <summary>
/// Regression tests for the RSI 2-Stage promotion engine.
///
/// Each test exercises the helper logic that determines whether a staged signal
/// should be promoted to DailySignals, remain active, or be flagged with a warning.
///
/// The helpers under test live in StagedSignalService and are public only for
/// test visibility — they are not part of the public interface.
/// </summary>
public class StagedSignalEngineTests
{
    // ── Shared thresholds matching default ScannerRuntimeConfig values ────────
    private const decimal TrendShiftThreshold = 0.25m;
    private const decimal EarlyMin    = 0.25m;
    private const decimal NormalMin   = 1.0m;
    private const decimal StrongMin   = 5.0m;
    private const decimal ExplosiveMin = 10.0m;

    // ── Test 1: ATS Low-Volume Trap — Bull Turn + Volume Fail ────────────────
    [Fact]
    public void Test1_ATS_LowVolumeTrap_ShouldBeConfirmingButNotPromoted()
    {
        // Given: RSI Δ1D +0.37 → Bull Turn; VolumeSignal = Low-Volume Trap
        decimal? delta = 0.37m;
        string trendShift = ComputeShift(delta, ScanType.Oversold);
        string stageStatus = StagedSignalService.ComputeStageStatus(delta, trendShift);
        string turnStrength = StagedSignalService.ComputeTurnStrength(delta, ScanType.Oversold, EarlyMin, NormalMin, StrongMin, ExplosiveMin);

        // TrendShift must be Bull Turn
        Assert.Contains("Bull Turn", trendShift);

        // Stage status must be CONFIRMING (not TRACKING or STAGED)
        Assert.Equal("CONFIRMING", stageStatus);

        // Turn strength for +0.37 is Early (> EarlyMin, < NormalMin)
        Assert.Equal("Early", turnStrength);

        // Verify that the EodPersistence promotion gate blocks it:
        // Promotion requires VolumeSignal == "Validated" — Low-Volume Trap must block.
        bool wouldPromote = WouldPromote(delta, trendShift, volumeSignal: "Low-Volume Trap");
        Assert.False(wouldPromote, "ATS: Low-Volume Trap must prevent promotion even with Bull Turn");
    }

    // ── Test 2: Standard CPH Reversal — Full confirmation ───────────────────
    [Fact]
    public void Test2_CPH_StandardReversal_ShouldPromote()
    {
        // Given: RSI Δ1D +1.42 → Bull Turn; VolumeSignal = Validated
        decimal? delta = 1.42m;
        string trendShift = ComputeShift(delta, ScanType.Oversold);
        string stageStatus = StagedSignalService.ComputeStageStatus(delta, trendShift);
        string turnStrength = StagedSignalService.ComputeTurnStrength(delta, ScanType.Oversold, EarlyMin, NormalMin, StrongMin, ExplosiveMin);

        Assert.Contains("Bull Turn", trendShift);
        Assert.Equal("CONFIRMING", stageStatus);
        Assert.Equal("Normal", turnStrength);

        bool wouldPromote = WouldPromote(delta, trendShift, volumeSignal: "Validated");
        Assert.True(wouldPromote, "CPH: Bull Turn + Validated volume must promote");
    }

    // ── Test 3: DR Explosive Reversal — promote with Chase Risk flag ─────────
    [Fact]
    public void Test3_DR_ExplosiveReversal_ShouldPromoteWithChaseRiskFlag()
    {
        // Given: RSI Δ1D +14.82 → Bull Turn — Explosive; VolumeSignal = Validated
        decimal? delta = 14.82m;
        string trendShift = ComputeShift(delta, ScanType.Oversold);
        string turnStrength = StagedSignalService.ComputeTurnStrength(delta, ScanType.Oversold, EarlyMin, NormalMin, StrongMin, ExplosiveMin);
        string chaseRisk   = turnStrength == "Explosive" ? "Elevated" : string.Empty;

        Assert.Contains("Bull Turn", trendShift);
        Assert.Equal("Explosive", turnStrength);
        Assert.Equal("Elevated",  chaseRisk);

        bool wouldPromote = WouldPromote(delta, trendShift, volumeSignal: "Validated");
        Assert.True(wouldPromote, "DR: Explosive Bull Turn + Validated volume must promote");
    }

    // ── Test 4: Failed Bull Turn — reverts to TRACKING ──────────────────────
    [Fact]
    public void Test4_FailedBullTurn_ShouldRevertToTracking()
    {
        // Day 2: RSI +0.5 → Bull Turn (volume failed → not promoted)
        decimal? day2Delta = 0.5m;
        string day2Shift = ComputeShift(day2Delta, ScanType.Oversold);
        string day2Status = StagedSignalService.ComputeStageStatus(day2Delta, day2Shift);
        Assert.Equal("CONFIRMING", day2Status);

        // Day 3: RSI -1.1 → Bull Turn failed, now Still Falling
        decimal? day3Delta = -1.1m;
        string day3Shift  = ComputeShift(day3Delta, ScanType.Oversold);
        string day3Status = StagedSignalService.ComputeStageStatus(day3Delta, day3Shift);

        Assert.Contains("Still Falling", day3Shift);
        Assert.Equal("TRACKING", day3Status);

        bool wouldPromote = WouldPromote(day3Delta, day3Shift, volumeSignal: "Validated");
        Assert.False(wouldPromote, "After failed Bull Turn, no promotion must occur");
    }

    // ── Test 5: RSI Leaves Oversold Before Confirmation — still eligible ─────
    [Fact]
    public void Test5_RsiLeavesOversoldBeforeConfirmation_CanStillPromote()
    {
        // Setup originated as Oversold (RSI was 27).
        // Current RSI = 32 (above 30), but setup ScanType is still Oversold.
        // RSI Δ1D = +3.2 → Bull Turn (Normal strength)
        decimal? delta = 3.2m;
        string trendShift = ComputeShift(delta, ScanType.Oversold);
        string turnStrength = StagedSignalService.ComputeTurnStrength(delta, ScanType.Oversold, EarlyMin, NormalMin, StrongMin, ExplosiveMin);

        Assert.Contains("Bull Turn", trendShift);
        Assert.Equal("Normal", turnStrength);

        // Promotion gate does not check current RSI — only TrendShift + Volume
        bool wouldPromote = WouldPromote(delta, trendShift, volumeSignal: "Validated");
        Assert.True(wouldPromote, "Signal with origin Oversold can promote even when current RSI > 30");
    }

    // ── Helper: mirrors EodSignalPersistenceService promotion gate ────────────
    private static bool WouldPromote(decimal? rsiDelta, string trendShift, string volumeSignal)
        => rsiDelta.HasValue
           && (trendShift.Contains("Bull Turn") || trendShift.Contains("Bear Turn"))
           && volumeSignal == "Validated";

    private static string ComputeShift(decimal? delta, ScanType type)
    {
        if (!delta.HasValue) return "Waiting";
        if (type == ScanType.Oversold)
        {
            return delta.Value > TrendShiftThreshold  ? "\ud83d\udfe2 Bull Turn"
                 : delta.Value < -TrendShiftThreshold ? "\ud83d\udd34 Still Falling"
                 :                                      "\ud83d\udfe1 Stabilizing";
        }
        return delta.Value < -TrendShiftThreshold ? "\ud83d\udfe2 Bear Turn"
             : delta.Value > TrendShiftThreshold  ? "\ud83d\udd34 Still Rising"
             :                                      "\ud83d\udfe1 Stabilizing";
    }
}
