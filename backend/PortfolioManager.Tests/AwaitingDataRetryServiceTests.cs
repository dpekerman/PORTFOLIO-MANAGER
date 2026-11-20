using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public class AwaitingDataRetryServiceTests
{
    [Fact]
    public void ParseSymbols_ReadsEveryAffectedSymbol_IncludingAnalysisAndReasonSuffixes()
    {
        var message = "53 holding(s) published; Awaiting Data (not evaluated): ABC.TO [Failed: AWAITING_REFRESH_FAILED], " +
                      "MHO (analysis: MHO, session: 2026-11-18; recovery unverified) [Invalid: AWAITING_INCONSISTENT_DATA], BRK-B";
        Assert.Equal(["ABC.TO", "MHO", "BRK-B"], AwaitingDataRetryService.ParseSymbols(message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Refresh failed: timeout")]
    public void ParseSymbols_ReturnsEmpty_WhenNoAwaitingList(string? message) =>
        Assert.Empty(AwaitingDataRetryService.ParseSymbols(message));

    // 2026-11-18 is a Wednesday (EST, UTC-5): close 16:00 ET + 2h evidence grace + 5 min settle = 18:05 ET = 23:05 UTC.
    [Theory]
    [InlineData(22, 45, false)]
    [InlineData(23, 4, false)]
    [InlineData(23, 5, true)]
    public void RetryWindow_OpensOnlyAfterTheSessionIsFinal(int hour, int minute, bool allowed)
    {
        var result = AwaitingDataRetryService.EvaluateWindow(new DateTime(2026, 11, 18, hour, minute, 0, DateTimeKind.Utc));
        Assert.Equal(allowed, result.Allowed);
        Assert.Equal(!allowed, result.AvailableAtUtc is not null);
    }

    [Fact]
    public void RetryWindow_IsOpenOnNonTradingDays()
    {
        Assert.True(AwaitingDataRetryService.EvaluateWindow(new DateTime(2026, 11, 21, 15, 0, 0, DateTimeKind.Utc)).Allowed);
    }
}
