using PortfolioManager.Api.Models;

namespace PortfolioManager.Tests;

public sealed partial class SignalsAuditServiceTests
{
    [Fact]
    public async Task StrategicAddSafeguard_KeepsThePriorAddInHistory_AndCreatesNoNewRecommendation()
    {
        var h = await WithFinalActionAsync("NVDA.TO", "Add / Staged Entry");
        h.FinalActions.ExtraWatchlist[0] = Result("NVDA.TO", "Add / Staged Entry", SignalSources.Portfolio, 142.10m);
        await h.EodAsync();
        var before = h.Db.SignalAuditEvents.Single();

        h.FinalActions.ExtraWatchlist[0] = Result("NVDA.TO", "REVERSAL WATCH — Wait for Confirmation", SignalSources.Portfolio, 141.00m);
        await h.EodAsync();

        var history = h.Db.SignalAuditEvents.Single();                              // no new event for the advisory label
        Assert.Equal(before.Id, history.Id);
        Assert.Equal("FA:ADD/STAGED ENTRY", history.SignalType);
        Assert.Equal(142.10m, history.SignalPrice);                                // never rewritten
        Assert.Equal(before.SignalDate, history.SignalDate);
        Assert.NotNull(history.EpisodeEndedDate);                                  // closed by the existing lifecycle, not edited
    }
}
