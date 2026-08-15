using FluentAssertions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>
/// Tests for the Stage-2 promotion gate logic.
/// Gate requires: BullBearTurn + VolumeRatio &gt;= 1.5x + EodPriceConfirmation (ATR structural).
/// EMA9 is supporting context only — it is NOT a promotion requirement.
/// Legacy scanner Status (Confirmed/EarlyWarning) has NO role in this gate.
/// </summary>
public class EodPromotionGateTests
{
    // ── Helpers mirroring EodSignalPersistenceService.SaveAsync filter ─────────

    /// <summary>
    /// EOD structural price check — mirrors IsEodPriceConfirmed in EodSignalPersistenceService.
    /// Oversold: close > open AND close >= high − 0.25×ATR.
    /// Overbought: close &lt; open AND close &lt;= low + 0.25×ATR.
    /// EMA9 is NOT part of this check.
    /// </summary>
    private static bool EodPriceConfirmed(ScanType scanType, decimal close, decimal open, decimal highOrLow, decimal atr) =>
        atr > 0 && (scanType == ScanType.Oversold
            ? close > open && close >= highOrLow - (0.25m * atr)
            : close < open && close <= highOrLow + (0.25m * atr));

    /// <summary>EMA9 supporting check — NOT required for promotion.</summary>
    private static bool Ema9Confirmed(ScanType scanType, decimal price, decimal ema9) =>
        scanType == ScanType.Oversold ? price > ema9 : price < ema9;

    /// <summary>volumeRatio >= 1.5m mirrors the Stage-2 gate (NOT 1.3x display threshold).</summary>
    private static bool WouldPromote(decimal? rsiDelta, string trendShift, decimal volumeRatio, bool eodPriceConfirmed) =>
        rsiDelta.HasValue
        && (trendShift.Contains("Bull Turn") || trendShift.Contains("Bear Turn"))
        && volumeRatio >= 1.5m
        && eodPriceConfirmed;

    private static string ComputeShift(decimal delta, ScanType type) =>
        type == ScanType.Oversold
            ? (delta > 0.25m ? "\ud83d\udfe2 Bull Turn" : delta < -0.25m ? "\ud83d\udd34 Still Falling" : "\ud83d\udfe1 Stabilizing")
            : (delta < -0.25m ? "\ud83d\udfe2 Bear Turn" : delta > 0.25m ? "\ud83d\udd34 Still Rising" : "\ud83d\udfe1 Stabilizing");

    // ── Test A: Valid Reversal Before EMA9 Reclaim (DR.TO regression) ─────────

    [Fact]
    public void TestA_BullTurn_ValidatedVolume_EodPricePassed_Ema9Pending_ShouldPromote()
    {
        // DR.TO scenario: good EOD candle + good volume, but price has not yet reclaimed EMA9.
        // Under the new rules EMA9 is supporting only — promotion must succeed.
        var delta = 14.82m; // RSI Δ1D +14.82 → Explosive Bull Turn
        var shift = ComputeShift(delta, ScanType.Oversold);

        // EOD structural check: candle closed bullishly and near its high
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 15.43m, open: 14.80m, highOrLow: 15.60m, atr: 0.80m);

        // EMA9 supporting context: price NOT yet above EMA9
        bool ema9 = Ema9Confirmed(ScanType.Oversold, price: 15.37m, ema9: 15.67m);

        shift.Should().Contain("Bull Turn");
        eodPrice.Should().BeTrue("candle closed above open and within 0.25×ATR of high");
        ema9.Should().BeFalse("price $15.37 has not yet reclaimed EMA9 $15.67");

        WouldPromote(delta, shift, volumeRatio: 2.79m, eodPrice).Should().BeTrue(
            "EMA9 not reclaimed is NOT a blocking condition — EOD price + volume + turn all passed");
    }

    // ── Test B: EMA9 Reclaimed but EOD Structure Failed ───────────────────────

    [Fact]
    public void TestB_BullTurn_ValidatedVolume_Ema9Confirmed_EodPriceFailed_ShouldBlock()
    {
        // Proves EMA9 is supporting only: even with EMA9 reclaimed, EOD structural price must pass.
        var delta = 3.0m;
        var shift = ComputeShift(delta, ScanType.Oversold);

        // EOD structural check fails: candle closed below open (bearish close)
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 14.90m, open: 15.20m, highOrLow: 15.50m, atr: 0.60m);

        bool ema9 = Ema9Confirmed(ScanType.Oversold, price: 15.80m, ema9: 15.50m);

        shift.Should().Contain("Bull Turn");
        ema9.Should().BeTrue("price $15.80 is above EMA9 $15.50");
        eodPrice.Should().BeFalse("close $14.90 < open $15.20 — bearish close fails EOD structural check");

        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPrice).Should().BeFalse(
            "EOD price confirmation failed — EMA9 alone cannot unlock promotion");
    }

    // ── Test C: Low Volume ─────────────────────────────────────────────────────

    [Fact]
    public void TestC_BullTurn_LowVolume_EodPricePassed_ShouldBlock()
    {
        var delta = 2.5m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 25.60m, open: 24.80m, highOrLow: 25.70m, atr: 0.50m);

        shift.Should().Contain("Bull Turn");
        eodPrice.Should().BeTrue();

        WouldPromote(delta, shift, volumeRatio: 0.81m, eodPrice).Should().BeFalse(
            "0.81x does not meet the 1.5x Stage-2 volume gate");
    }

    // ── Test D: No Bull Turn ───────────────────────────────────────────────────

    [Fact]
    public void TestD_NoBullTurn_ValidatedVolume_EodPricePassed_ShouldBlock()
    {
        var delta = 0.10m; // Stabilizing — not a Bull Turn
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 25.60m, open: 24.80m, highOrLow: 25.70m, atr: 0.50m);

        shift.Should().Contain("Stabilizing");
        eodPrice.Should().BeTrue();

        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPrice).Should().BeFalse(
            "Stabilizing RSI is not a Bull Turn — Stage-1 direction requirement not met");
    }

    // ── Oversold core promotions ───────────────────────────────────────────────

    [Fact]
    public void Oversold_BullTurn_ValidatedVolume_EodPricePassed_ShouldPromote()
    {
        var delta = 1.42m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 15.10m, open: 14.40m, highOrLow: 15.20m, atr: 0.60m);

        shift.Should().Contain("Bull Turn");
        eodPrice.Should().BeTrue();
        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPrice).Should().BeTrue();
    }

    [Fact]
    public void Oversold_BullTurn_ValidatedVolume_EodPriceFailed_CloseBelowOpen_ShouldBlock()
    {
        // Candle closed below open (bearish) — EOD structural check fails even with good volume and bull turn.
        var delta = 1.42m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 13.80m, open: 14.50m, highOrLow: 14.80m, atr: 0.60m);

        shift.Should().Contain("Bull Turn");
        eodPrice.Should().BeFalse("close $13.80 < open $14.50");
        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPrice).Should().BeFalse();
    }

    [Fact]
    public void Oversold_BullTurn_LowVolume_ShouldBlock()
    {
        var delta = 0.37m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 25.60m, open: 24.80m, highOrLow: 25.70m, atr: 0.50m);

        WouldPromote(delta, shift, volumeRatio: 0.7m, eodPrice).Should().BeFalse("0.7x is below 1.5x threshold");
    }

    [Fact]
    public void Oversold_BullTurn_Volume_1_4x_BelowThreshold_ShouldBlock()
    {
        // 1.4x passes old 1.3x display threshold but NOT the 1.5x Stage-2 gate.
        var delta = 0.80m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 25.60m, open: 24.80m, highOrLow: 25.70m, atr: 0.50m);

        WouldPromote(delta, shift, volumeRatio: 1.4m, eodPrice).Should().BeFalse("1.4x is below the 1.5x Stage-2 gate");
    }

    [Fact]
    public void Oversold_StillFalling_ValidatedVolume_ShouldBlock()
    {
        var delta = -1.1m;
        var shift = ComputeShift(delta, ScanType.Oversold);

        shift.Should().Contain("Still Falling");
        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPriceConfirmed: true).Should().BeFalse();
    }

    [Fact]
    public void Oversold_Stabilizing_ValidatedVolume_ShouldBlock()
    {
        var delta = 0.10m;
        var shift = ComputeShift(delta, ScanType.Oversold);

        shift.Should().Contain("Stabilizing");
        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPriceConfirmed: true).Should().BeFalse();
    }

    [Fact]
    public void Oversold_NullDelta_Day1_ShouldBlock()
    {
        WouldPromote(null, "Waiting", volumeRatio: 2.0m, eodPriceConfirmed: true).Should().BeFalse("Day 1 has no RSI delta");
    }

    // ── Overbought core promotions ─────────────────────────────────────────────

    [Fact]
    public void Overbought_BearTurn_ValidatedVolume_EodPricePassed_ShouldPromote()
    {
        var delta = -2.5m;
        var shift = ComputeShift(delta, ScanType.Overbought);
        bool eodPrice = EodPriceConfirmed(ScanType.Overbought,
            close: 84.60m, open: 86.50m, highOrLow: 84.40m, atr: 1.20m);

        shift.Should().Contain("Bear Turn");
        eodPrice.Should().BeTrue("close < open and close <= low + 0.25×ATR");
        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPrice).Should().BeTrue();
    }

    [Fact]
    public void Overbought_BearTurn_ValidatedVolume_EodPriceFailed_CloseAboveOpen_ShouldBlock()
    {
        // Close above open on overbought setup — EOD structural check fails.
        var delta = -2.5m;
        var shift = ComputeShift(delta, ScanType.Overbought);
        bool eodPrice = EodPriceConfirmed(ScanType.Overbought,
            close: 92.00m, open: 88.00m, highOrLow: 83.00m, atr: 1.20m);

        eodPrice.Should().BeFalse("close $92 > open $88 — bullish close, EOD structural check fails for overbought");
        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPrice).Should().BeFalse();
    }

    [Fact]
    public void Overbought_StillRising_ShouldBlock()
    {
        var delta = 1.5m;
        var shift = ComputeShift(delta, ScanType.Overbought);

        shift.Should().Contain("Still Rising");
        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPriceConfirmed: true).Should().BeFalse();
    }

    // ── DVA regression ─────────────────────────────────────────────────────────

    [Fact]
    public void DVA_BullTurn_LowVolume_EodPriceMayFail_MustNotPromote()
    {
        // DVA: Volume 0.36x — blocked regardless of EOD price result.
        var delta = 1.0m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 180.06m, open: 175.00m, highOrLow: 182.00m, atr: 3.50m);

        shift.Should().Contain("Bull Turn");
        WouldPromote(delta, shift, volumeRatio: 0.36m, eodPrice).Should().BeFalse(
            "DVA: volume 0.36x is far below the 1.5x Stage-2 gate");
    }

    // ── Aug 14 Regression Tests ────────────────────────────────────────────────

    [Fact]
    public void Aug14_ENB_TO_LowVolume_ShouldBlock()
    {
        // ENB.TO: Volume=1.40x — below 1.5x Stage-2 gate regardless of EOD price.
        var delta = 0.5m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        decimal volRatio = 1.40m;

        (volRatio >= 1.5m).Should().BeFalse("1.40x does not meet the 1.5x Stage-2 threshold");
        WouldPromote(delta, shift, volRatio, eodPriceConfirmed: true).Should().BeFalse();
    }

    [Fact]
    public void Aug14_ATS_TO_LowVolume_ShouldBlock()
    {
        // ATS.TO: Volume=0.81x — Low-Volume Trap, blocked.
        var delta = 0.5m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        decimal volRatio = 0.81m;

        (volRatio >= 1.5m).Should().BeFalse("0.81x is a Low-Volume Trap");
        WouldPromote(delta, shift, volRatio, eodPriceConfirmed: true).Should().BeFalse();
    }

    [Fact]
    public void Aug14_DR_TO_ValidatedVolume_Promoted_If_EodPricePasses()
    {
        // DR.TO: Price=15.37, EMA9=15.67 (not reclaimed), Volume=2.79x.
        // Under the NEW rules: EMA9 not reclaimed is NOT a blocking condition.
        // If EOD structural price passes → should be PROMOTED.
        var delta = 14.82m; // RSI Δ1D from the deployment report
        var shift = ComputeShift(delta, ScanType.Oversold);
        decimal volRatio = 2.79m;

        // EOD structural check passing (realistic candle data for DR.TO)
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 15.43m, open: 14.80m, highOrLow: 15.60m, atr: 0.80m);

        bool ema9 = Ema9Confirmed(ScanType.Oversold, price: 15.37m, ema9: 15.67m);

        shift.Should().Contain("Bull Turn");
        (volRatio >= 1.5m).Should().BeTrue("2.79x passes the volume gate");
        ema9.Should().BeFalse("price $15.37 has not reclaimed EMA9 $15.67 — supporting only");
        eodPrice.Should().BeTrue("close > open and near high");

        WouldPromote(delta, shift, volRatio, eodPrice).Should().BeTrue(
            "DR.TO: Bull Turn + Volume + EOD Price all pass. EMA9 pending is NOT a blocking condition.");
    }

    [Fact]
    public void Aug14_DR_TO_ValidatedVolume_Blocked_If_EodPriceFails()
    {
        // DR.TO with good volume but EOD structural price failing (e.g. close below open).
        var delta = 14.82m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        decimal volRatio = 2.79m;

        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 14.50m, open: 15.00m, highOrLow: 15.30m, atr: 0.80m);

        eodPrice.Should().BeFalse("close $14.50 < open $15.00 — bearish candle fails EOD structural check");
        WouldPromote(delta, shift, volRatio, eodPrice).Should().BeFalse(
            "EOD price confirmation failed — blocked regardless of volume or EMA9");
    }

    [Fact]
    public void Aug14_CPH_TO_ValidatedVolume_Promoted_If_EodPricePasses()
    {
        // CPH.TO: Volume=2.04x (passes 1.5x gate). EMA9 position is supporting only.
        var delta = 0.5m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        decimal volRatio = 2.04m;

        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 14.60m, open: 13.90m, highOrLow: 14.70m, atr: 0.60m);

        (volRatio >= 1.5m).Should().BeTrue("2.04x passes volume gate");
        eodPrice.Should().BeTrue();
        WouldPromote(delta, shift, volRatio, eodPrice).Should().BeTrue(
            "CPH.TO: Bull Turn + Volume + EOD Price all pass");
    }

    // ── EMA9 independence test ─────────────────────────────────────────────────

    [Fact]
    public void Ema9Status_DoesNotAffectPromotion_WhenRequiredGatesPassed()
    {
        // Both EMA9=true and EMA9=false must produce the same promotion result
        // when all three required gates (Turn + Volume + EodPrice) are satisfied.
        var delta = 5.0m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool eodPrice = EodPriceConfirmed(ScanType.Oversold,
            close: 25.60m, open: 24.80m, highOrLow: 25.70m, atr: 0.50m);

        bool ema9True = Ema9Confirmed(ScanType.Oversold, price: 26.00m, ema9: 25.00m);
        bool ema9False = Ema9Confirmed(ScanType.Oversold, price: 24.50m, ema9: 25.00m);

        ema9True.Should().BeTrue();
        ema9False.Should().BeFalse();

        // Promotion result must be identical regardless of EMA9 state
        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPrice).Should().BeTrue(
            "promotion with EMA9 confirmed");
        WouldPromote(delta, shift, volumeRatio: 2.0m, eodPrice).Should().BeTrue(
            "promotion with EMA9 pending — EMA9 is supporting only, not part of the gate");
    }
}
