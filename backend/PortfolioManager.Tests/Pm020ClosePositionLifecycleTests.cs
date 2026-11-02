using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Tests;

// Backlog #20: the Swing "Close Position" shortcut records a CLOSE through the existing UpdateAsync path.
// These tests pin the existing lifecycle it relies on. Synthetic data, EF InMemory.
public class Pm020ClosePositionLifecycleTests
{
    private sealed class TestAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static readonly DateTime OpenDate = new(2026, 10, 1);
    private static readonly DateTime CloseDate = new(2026, 10, 20);

    private static IHttpContextAccessor Principal() => new TestAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "u1"), new Claim(ClaimTypes.Role, "Trader")], "test")),
        },
    };

    private static async Task<(AppDbContext Db, PortfolioService Svc, int[] Ids)> SetupAsync(params decimal[] lotShares)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Pm033RiskTests.SimulateRowVersions(db);
        db.AccountTypes.Add(new AccountType { Name = "A", NormalizedName = "A" });
        db.SaveChanges();
        var svc = new PortfolioService(db, null!, new MutationClock(), Principal());
        var risk = new RiskService(db, Principal());
        var ids = new List<int>();
        foreach (var shares in lotShares)
            ids.Add((await svc.AddAsync(new AddPortfolioItemRequest("SYNTH.TO", "Synthetic", shares, 10, "OPEN", "A",
                OpenDate, HoldingRole: "Swing"))).Id);
        await risk.SavePlanAsync(new SaveRiskPlanRequest("A", "SYNTH.TO", 9, null, "Fixed", null, "thesis", null, null, null));
        await risk.SaveBrokerStopAsync(new SaveBrokerStopRequest(null, "A", "SYNTH.TO", "StopMarket", 9, null, lotShares.Sum(),
            null, null, null, null, null, false, null));
        return (db, svc, [.. ids]);
    }

    private static UpdatePortfolioItemRequest Close(decimal shares, DateTime? close = null) =>
        new("Synthetic", shares, 10, TransactionType: "CLOSE", AccountType: "A", OpenDate: OpenDate,
            CloseDate: close ?? CloseDate, ClosingPrice: 11.5m, HoldingRole: "Swing", DecisionSourceClosed: "Risk Control");

    [Fact]
    public async Task Partial_close_keeps_cost_episode_and_broker_stop_and_leaves_the_remainder_open()
    {
        var (db, svc, ids) = await SetupAsync(100);
        await using var _ = db;
        var before = await db.PortfolioItems.AsNoTracking().SingleAsync();

        var res = await svc.UpdateAsync(ids[0], Close(40));

        res!.NewOpenItem.Should().NotBeNull();
        res.NewOpenItem!.Shares.Should().Be(60);
        var closed = await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == ids[0]);
        var remainder = await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == res.NewOpenItem.Id);
        closed.TransactionType.Should().Be("CLOSE");
        closed.Shares.Should().Be(40);
        closed.AverageCostBasis.Should().Be(before.AverageCostBasis);
        closed.OpenDate.Should().Be(before.OpenDate);
        closed.TradeEpisodeId.Should().Be(before.TradeEpisodeId);
        remainder.TransactionType.Should().Be("OPEN");
        remainder.TradeEpisodeId.Should().Be(before.TradeEpisodeId);
        remainder.AverageCostBasis.Should().Be(before.AverageCostBasis);
        remainder.OpenDate.Should().Be(before.OpenDate);
        (await db.TradeEpisodes.AsNoTracking().SingleAsync()).Status.Should().Be(EpisodeStatuses.Open);
        (await db.BrokerStopRecords.AsNoTracking().SingleAsync()).UserStatus.Should()
            .NotBe(BrokerUserStatuses.EpisodeClosed, "a partial close must not retire the remaining exposure");
    }

    [Fact]
    public async Task Full_close_of_the_last_lot_closes_the_episode_and_retires_the_stop_without_claiming_it_filled()
    {
        var (db, svc, ids) = await SetupAsync(100);
        await using var _ = db;

        var res = await svc.UpdateAsync(ids[0], Close(100));

        res!.NewOpenItem.Should().BeNull();
        var ep = await db.TradeEpisodes.AsNoTracking().SingleAsync();
        ep.Status.Should().Be(EpisodeStatuses.Closed);
        ep.ClosedDate.Should().Be(CloseDate);
        var stop = await db.BrokerStopRecords.AsNoTracking().SingleAsync();
        stop.UserStatus.Should().Be(BrokerUserStatuses.EpisodeClosed);
        stop.UserStatus.Should().NotBe(BrokerUserStatuses.UserReportedFilled);
        stop.BrokerCancellationUnverified.Should().BeTrue("the broker order may still be open and must be verified by the user");
        (await db.RiskAuditEvents.AsNoTracking().ToListAsync()).Should().Contain(e => e.EventType == "EpisodeClosedBrokerOrderRetired");
    }

    [Fact]
    public async Task Closing_one_of_two_lots_leaves_the_other_lot_the_episode_and_the_stop_untouched()
    {
        var (db, svc, ids) = await SetupAsync(100, 50);
        await using var _ = db;

        await svc.UpdateAsync(ids[0], Close(100));

        var other = await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == ids[1]);
        other.TransactionType.Should().Be("OPEN");
        other.Shares.Should().Be(50);
        (await db.TradeEpisodes.AsNoTracking().SingleAsync()).Status.Should().Be(EpisodeStatuses.Open);
        (await db.BrokerStopRecords.AsNoTracking().SingleAsync()).UserStatus.Should().NotBe(BrokerUserStatuses.EpisodeClosed);
    }

    [Fact]
    public async Task A_close_without_a_date_is_rejected_and_nothing_changes()
    {
        var (db, svc, ids) = await SetupAsync(100);
        await using var _ = db;

        var act = () => svc.UpdateAsync(ids[0], Close(100) with { CloseDate = null });

        await act.Should().ThrowAsync<RiskValidationException>();
        db.ChangeTracker.Clear();
        (await db.PortfolioItems.AsNoTracking().SingleAsync()).TransactionType.Should().Be("OPEN");
        (await db.TradeEpisodes.AsNoTracking().SingleAsync()).Status.Should().Be(EpisodeStatuses.Open);
        (await db.BrokerStopRecords.AsNoTracking().SingleAsync()).UserStatus.Should().NotBe(BrokerUserStatuses.EpisodeClosed);
    }

    [Fact]
    public async Task Recording_a_close_never_marks_a_broker_stop_filled()
    {
        var (db, svc, ids) = await SetupAsync(100);
        await using var _ = db;
        await svc.UpdateAsync(ids[0], Close(100));
        (await db.BrokerStopRecords.AsNoTracking().ToListAsync())
            .Should().OnlyContain(s => s.UserStatus != BrokerUserStatuses.UserReportedFilled);
    }
}
