using PortfolioManager.Api.Services;
using Xunit;
using PortfolioManager.Api.Models;

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
    [InlineData(ScanType.Oversold, SignalStatus.Confirmed, "Waiting", false, true, "ENTRY CANDIDATE")]
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
}
