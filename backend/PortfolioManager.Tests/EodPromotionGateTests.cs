using FluentAssertions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>
/// Tests for the Stage-2 promotion gate logic.
/// Gate requires: TrendShift (Bull/Bear Turn) + VolumeRatio >= 1.5x + PriceConfirmation.
/// Legacy scanner Status (Confirmed/EarlyWarning) has NO role in this gate.
/// </summary>
public class EodPromotionGateTests
{
    // ── helpers mirroring EodSignalPersistenceService.SaveAsync filter ─────────
    private static bool PriceConfirmed(ScanType scanType, decimal price, decimal ema9) =>
        scanType == ScanType.Oversold ? price > ema9 : price < ema9;

    // volumeRatio >= 1.5m mirrors the Stage-2 gate (NOT 1.3x display threshold)
    private static bool WouldPromote(decimal? rsiDelta, string trendShift, decimal volumeRatio, bool priceConfirmed) =>
        rsiDelta.HasValue
        && (trendShift.Contains("Bull Turn") || trendShift.Contains("Bear Turn"))
        && volumeRatio >= 1.5m
        && priceConfirmed;

    private static string ComputeShift(decimal delta, ScanType type) =>
        type == ScanType.Oversold
            ? (delta > 0.25m ? "\ud83d\udfe2 Bull Turn" : delta < -0.25m ? "\ud83d\udd34 Still Falling" : "\ud83d\udfe1 Stabilizing")
            : (delta < -0.25m ? "\ud83d\udfe2 Bear Turn" : delta > 0.25m ? "\ud83d\udd34 Still Rising" : "\ud83d\udfe1 Stabilizing");

    // ── Oversold promotions ────────────────────────────────────────────────────
    [Fact]
    public void Oversold_BullTurn_ValidatedVolume_PriceAboveEma9_ShouldPromote()
    {
        var delta = 1.42m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        var priceConf = PriceConfirmed(ScanType.Oversold, price: 15.00m, ema9: 14.50m);

        WouldPromote(delta, shift, volumeRatio: 2.0m, priceConf).Should().BeTrue();
    }

    [Fact]
    public void Oversold_BullTurn_ValidatedVolume_PriceBelowEma9_ShouldBlock()
    {
        var delta = 1.42m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        var priceConf = PriceConfirmed(ScanType.Oversold, price: 180.82m, ema9: 190.24m); // DVA scenario

        shift.Should().Contain("Bull Turn");
        priceConf.Should().BeFalse("price $180.82 is below EMA9 $190.24");
        WouldPromote(delta, shift, volumeRatio: 2.0m, priceConf).Should().BeFalse();
    }

    [Fact]
    public void Oversold_BullTurn_LowVolumeTrap_PriceAboveEma9_ShouldBlock()
    {
        var delta = 0.37m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        var priceConf = PriceConfirmed(ScanType.Oversold, price: 25.00m, ema9: 24.00m);

        WouldPromote(delta, shift, volumeRatio: 0.7m, priceConf).Should().BeFalse("0.7x is below 1.5x threshold");
    }

    [Fact]
    public void Oversold_BullTurn_Volume_1_4x_BelowThreshold_ShouldBlock()
    {
        // 1.4x passes old 1.3x display threshold but NOT the 1.5x Stage-2 gate
        var delta = 0.80m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        var priceConf = PriceConfirmed(ScanType.Oversold, price: 25.00m, ema9: 24.00m);

        WouldPromote(delta, shift, volumeRatio: 1.4m, priceConf).Should().BeFalse("1.4x is below the 1.5x Stage-2 gate");
    }

    [Fact]
    public void Oversold_StillFalling_ValidatedVolume_ShouldBlock()
    {
        var delta = -1.1m;
        var shift = ComputeShift(delta, ScanType.Oversold);

        shift.Should().Contain("Still Falling");
        WouldPromote(delta, shift, volumeRatio: 2.0m, priceConfirmed: true).Should().BeFalse();
    }

    [Fact]
    public void Oversold_Stabilizing_ValidatedVolume_ShouldBlock()
    {
        var delta = 0.10m;
        var shift = ComputeShift(delta, ScanType.Oversold);

        shift.Should().Contain("Stabilizing");
        WouldPromote(delta, shift, volumeRatio: 2.0m, priceConfirmed: true).Should().BeFalse();
    }

    [Fact]
    public void Oversold_NullDelta_Day1_ShouldBlock()
    {
        WouldPromote(null, "Waiting", volumeRatio: 2.0m, priceConfirmed: true).Should().BeFalse("Day 1 has no RSI delta");
    }

    // ── Overbought promotions ─────────────────────────────────────────────────
    [Fact]
    public void Overbought_BearTurn_ValidatedVolume_PriceBelowEma9_ShouldPromote()
    {
        var delta = -2.5m;
        var shift = ComputeShift(delta, ScanType.Overbought);
        var priceConf = PriceConfirmed(ScanType.Overbought, price: 85.00m, ema9: 88.00m);

        shift.Should().Contain("Bear Turn");
        priceConf.Should().BeTrue("price $85 is below EMA9 $88");
        WouldPromote(delta, shift, volumeRatio: 2.0m, priceConf).Should().BeTrue();
    }

    [Fact]
    public void Overbought_BearTurn_ValidatedVolume_PriceAboveEma9_ShouldBlock()
    {
        var delta = -2.5m;
        var shift = ComputeShift(delta, ScanType.Overbought);
        var priceConf = PriceConfirmed(ScanType.Overbought, price: 92.00m, ema9: 88.00m);

        priceConf.Should().BeFalse("price $92 is above EMA9 $88 — overbought needs price below EMA9");
        WouldPromote(delta, shift, volumeRatio: 2.0m, priceConf).Should().BeFalse();
    }

    [Fact]
    public void Overbought_StillRising_ShouldBlock()
    {
        var delta = 1.5m;
        var shift = ComputeShift(delta, ScanType.Overbought);

        shift.Should().Contain("Still Rising");
        WouldPromote(delta, shift, volumeRatio: 2.0m, priceConfirmed: true).Should().BeFalse();
    }

    // ── DVA regression (requirement #35) ─────────────────────────────────────
    [Fact]
    public void DVA_BullTurn_PriceAndVolumeBothFail_MustNotPromote()
    {
        var delta = 1.0m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        var priceConf = PriceConfirmed(ScanType.Oversold, price: 180.06m, ema9: 190.00m);

        shift.Should().Contain("Bull Turn");
        priceConf.Should().BeFalse("$180.06 < EMA9 $190.00");
        WouldPromote(delta, shift, volumeRatio: 0.36m, priceConf).Should().BeFalse(
            "DVA: price below EMA9 AND volume 0.36x — must stay CONFIRMING, never be promoted");
    }

    // ── Aug 14 Regression Tests (requirement #36-37) ─────────────────────────

    [Fact]
    public void Aug14_ENB_TO_PriceFails_VolumeFails_MustNotPromote()
    {
        // ENB.TO: Price=70.61, EMA9=72.53, Volume=1.40x, RSI=25.22
        var delta = 0.5m; // assume some positive delta (Bull Turn scenario)
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool priceConf = PriceConfirmed(ScanType.Oversold, price: 70.61m, ema9: 72.53m);
        decimal volRatio = 1.40m;

        priceConf.Should().BeFalse("$70.61 < EMA9 $72.53");
        (volRatio >= 1.5m).Should().BeFalse("1.40x does not meet the 1.5x Stage-2 threshold");
        WouldPromote(delta, shift, volRatio, priceConf).Should().BeFalse();
    }

    [Fact]
    public void Aug14_ATS_TO_PriceFails_VolumeFails_MustNotPromote()
    {
        // ATS.TO: Price=28.16, EMA9=30.40, Volume=0.81x, RSI=27.25
        var delta = 0.5m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool priceConf = PriceConfirmed(ScanType.Oversold, price: 28.16m, ema9: 30.40m);
        decimal volRatio = 0.81m;

        priceConf.Should().BeFalse("$28.16 < EMA9 $30.40");
        (volRatio >= 1.5m).Should().BeFalse("0.81x is a Low-Volume Trap");
        WouldPromote(delta, shift, volRatio, priceConf).Should().BeFalse();
    }

    [Fact]
    public void Aug14_DR_TO_PriceFails_VolumePassesButNotPromoted()
    {
        // DR.TO: Price=15.37, EMA9=15.67, Volume=2.79x, RSI=28.38
        // Volume PASSES (2.79 >= 1.5) but price FAILS (15.37 < 15.67)
        var delta = 0.5m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool priceConf = PriceConfirmed(ScanType.Oversold, price: 15.37m, ema9: 15.67m);
        decimal volRatio = 2.79m;

        shift.Should().Contain("Bull Turn");
        priceConf.Should().BeFalse("$15.37 < EMA9 $15.67");
        (volRatio >= 1.5m).Should().BeTrue("2.79x passes volume gate");
        WouldPromote(delta, shift, volRatio, priceConf).Should().BeFalse("price block prevents promotion");
    }

    [Fact]
    public void Aug14_CPH_TO_PriceFails_VolumePassesButNotPromoted()
    {
        // CPH.TO: Price=14.14, EMA9=15.73, Volume=2.04x, RSI=30.05
        var delta = 0.5m;
        var shift = ComputeShift(delta, ScanType.Oversold);
        bool priceConf = PriceConfirmed(ScanType.Oversold, price: 14.14m, ema9: 15.73m);
        decimal volRatio = 2.04m;

        priceConf.Should().BeFalse("$14.14 < EMA9 $15.73");
        (volRatio >= 1.5m).Should().BeTrue("2.04x passes volume gate");
        WouldPromote(delta, shift, volRatio, priceConf).Should().BeFalse("price block prevents promotion");
    }

    [Fact]
    public void Aug14_ZeroPromotions_Expected_WhenNoStockPassesBothPriceAndVolume()
    {
        // The Aug 14 raw set: NO stock simultaneously has Price > EMA9 AND VolumeRatio >= 1.5x.
        // Simulate all five key stocks and verify all result in Promoted=FALSE.
        var delta = 1.0m; // Bull Turn for all

        var cases = new[]
        {
            (Symbol: "ENB.TO", Price: 70.61m, EMA9: 72.53m, Vol: 1.40m),
            (Symbol: "ATS.TO", Price: 28.16m, EMA9: 30.40m, Vol: 0.81m),
            (Symbol: "DR.TO",  Price: 15.37m, EMA9: 15.67m, Vol: 2.79m),
            (Symbol: "CPH.TO", Price: 14.14m, EMA9: 15.73m, Vol: 2.04m),
            (Symbol: "DVA",    Price: 180.06m, EMA9: 190.00m, Vol: 0.36m),
        };

        foreach (var c in cases)
        {
            var shift = ComputeShift(delta, ScanType.Oversold);
            bool price = PriceConfirmed(ScanType.Oversold, c.Price, c.EMA9);
            bool promoted = WouldPromote(delta, shift, c.Vol, price);
            promoted.Should().BeFalse($"{c.Symbol} must not promote: Price={price}, Vol={c.Vol:F2}x");
        }
    }
}
