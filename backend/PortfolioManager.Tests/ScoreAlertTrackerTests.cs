using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public class ScoreAlertTrackerTests
{
    [Fact]
    public void ReturnsOnlyNewSymbolsAndNeverRepeatsWithinADay()
    {
        var tracker = new ScoreAlertTracker();

        Assert.Equal(["IBM", "MSFT"], tracker.GetNewAndSync("u1", "2026-10-20", ["MSFT", "IBM"]));
        Assert.Empty(tracker.GetNewAndSync("u1", "2026-10-20", ["IBM", "MSFT"]));
        Assert.Empty(tracker.GetNewAndSync("u1", "2026-10-20", []));
        Assert.Equal(["AAPL"], tracker.GetNewAndSync("u1", "2026-10-20", ["IBM", "AAPL"]));
    }

    [Fact]
    public void NewDayAndOtherUsersStartClean()
    {
        var tracker = new ScoreAlertTracker();
        tracker.GetNewAndSync("u1", "2026-10-20", ["IBM"]);

        Assert.Equal(["IBM"], tracker.GetNewAndSync("u1", "2026-10-21", ["IBM"]));
        Assert.Equal(["IBM"], tracker.GetNewAndSync("u2", "2026-10-21", ["IBM"]));
    }

    [Fact]
    public void ForgetAllowsRetryAfterFailedSend()
    {
        var tracker = new ScoreAlertTracker();
        tracker.GetNewAndSync("u1", "2026-10-20", ["IBM"]);
        tracker.Forget("u1", ["IBM"]);

        Assert.Equal(["IBM"], tracker.GetNewAndSync("u1", "2026-10-20", ["IBM"]));
    }
}
