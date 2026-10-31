using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Tests;

// Backlog #21: a risk-associated CLOSE row saved without a CloseDate (CLS.TO / SKHY) must accept the
// first valid CloseDate while every other trade-episode / risk-history protection stays in force.
// Synthetic data only, uniquely named EF InMemory database.
public class Pm021CloseDateCompletionTests
{
    private sealed class TestAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static readonly DateTime OpenDate = new(2026, 9, 1);

    private static AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Pm033RiskTests.SimulateRowVersions(db);
        db.AccountTypes.Add(new AccountType { Name = "A", NormalizedName = "A" });
        db.AccountTypes.Add(new AccountType { Name = "B", NormalizedName = "B" });
        db.SaveChanges();
        return db;
    }

    private static IHttpContextAccessor Principal() => new TestAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "u1"), new Claim(ClaimTypes.Role, "Trader")], "test")),
        },
    };

    private static PortfolioService Portfolio(AppDbContext db) =>
        new(db, null!, new MutationClock(), Principal());

    private static UpdatePortfolioItemRequest Request(string type = "CLOSE", decimal shares = 100,
        string account = "A", DateTime? open = null, DateTime? close = null) =>
        new("Synthetic", shares, 10, TransactionType: type, AccountType: account,
            OpenDate: open ?? OpenDate, CloseDate: close, ClosingPrice: 12, HoldingRole: "Swing");

    // Reproduces the production state: OPEN swing lot with episode, plan and broker order, then
    // switched to CLOSE through the edit path with no CloseDate (the edit dialog does not require one).
    private static async Task<(AppDbContext Db, PortfolioService Svc, int Id)> ClosedWithoutDateAsync()
    {
        var db = NewDb();
        var svc = Portfolio(db);
        var risk = new RiskService(db, Principal());
        var added = await svc.AddAsync(new AddPortfolioItemRequest("SYNTH.TO", "Synthetic", 100, 10, "OPEN", "A",
            OpenDate, HoldingRole: "Swing"));
        await risk.SavePlanAsync(new SaveRiskPlanRequest("A", "SYNTH.TO", 9, null, "Fixed", null, "thesis", null, null, null));
        await risk.SaveBrokerStopAsync(new SaveBrokerStopRequest(null, "A", "SYNTH.TO", "StopMarket", 9, null, 100,
            null, null, null, null, null, false, null));
        (await svc.UpdateAsync(added.Id, Request())).Should().NotBeNull();
        (await db.PortfolioItems.FindAsync(added.Id))!.CloseDate.Should().BeNull();
        return (db, svc, added.Id);
    }

    private static async Task<string[]> SnapshotAsync(AppDbContext db) =>
    [
        .. (await db.TradeEpisodes.AsNoTracking().OrderBy(x => x.Id).ToListAsync()).Select(x => $"E|{x.Id}|{x.Status}"),
        .. (await db.RiskPlans.AsNoTracking().OrderBy(x => x.Id).ToListAsync()).Select(x => $"P|{x.Id}|{x.PlannedStopPrice}|{x.TradeEpisodeId}|{x.UpdatedAtUtc:O}"),
        .. (await db.BrokerStopRecords.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            .Select(x => $"B|{x.Id}|{x.UserStatus}|{x.BrokerVerificationStatus}|{x.CoveredQuantity}"),
        .. (await db.RiskAuditEvents.AsNoTracking().OrderBy(x => x.Id).ToListAsync()).Select(x => $"A|{x.Id}|{x.EventType}"),
    ];

    [Fact]
    public async Task First_close_date_on_risk_associated_close_row_is_allowed_without_touching_history()
    {
        var (db, svc, id) = await ClosedWithoutDateAsync();
        await using var _ = db;
        var before = await SnapshotAsync(db);
        var item = await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == id);

        var res = await svc.UpdateAsync(id, Request(close: new DateTime(2026, 9, 10)));

        res.Should().NotBeNull();
        var after = await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == id);
        after.CloseDate.Should().Be(new DateTime(2026, 9, 10), "the user-supplied execution date is stored as is");
        after.TradeEpisodeId.Should().Be(item.TradeEpisodeId);
        after.Shares.Should().Be(item.Shares);
        after.AccountType.Should().Be(item.AccountType);
        after.OpenDate.Should().Be(item.OpenDate);
        after.HoldingRole.Should().Be(item.HoldingRole);
        after.AverageCostBasis.Should().Be(item.AverageCostBasis);
        (await db.PortfolioItems.CountAsync()).Should().Be(1, "no remainder row or new transaction is created");
        (await SnapshotAsync(db)).Should().Equal(before, "no episode, plan, broker order or audit change / duplicate retirement event");
    }

    [Fact]
    public async Task Unchanged_null_close_date_is_still_allowed()
    {
        var (db, svc, id) = await ClosedWithoutDateAsync();
        await using var _ = db;
        (await svc.UpdateAsync(id, Request())).Should().NotBeNull();
    }

    [Fact]
    public async Task Close_date_before_open_date_is_rejected()
    {
        var (db, svc, id) = await ClosedWithoutDateAsync();
        await using var _ = db;
        var act = () => svc.UpdateAsync(id, Request(close: OpenDate.AddDays(-1)));
        await act.Should().ThrowAsync<RiskValidationException>().WithMessage("*cannot close before*");
    }

    [Fact]
    public async Task Existing_close_date_cannot_be_changed_or_cleared()
    {
        var (db, svc, id) = await ClosedWithoutDateAsync();
        await using var _ = db;
        await svc.UpdateAsync(id, Request(close: new DateTime(2026, 9, 10)));

        var change = () => svc.UpdateAsync(id, Request(close: new DateTime(2026, 9, 11)));
        var clear = () => svc.UpdateAsync(id, Request(close: null));
        await change.Should().ThrowAsync<RiskValidationException>().WithMessage("*ambiguous*");
        await clear.Should().ThrowAsync<RiskValidationException>().WithMessage("*ambiguous*");
        (await svc.UpdateAsync(id, Request(close: new DateTime(2026, 9, 10)))).Should().NotBeNull("same date is allowed");
    }

    [Fact]
    public async Task Other_protections_still_apply_when_completing_a_missing_close_date()
    {
        var (db, svc, id) = await ClosedWithoutDateAsync();
        await using var _ = db;
        var d = new DateTime(2026, 9, 10);

        foreach (var request in new[]
                 {
                     Request(close: d, shares: 50),
                     Request(close: d, account: "B"),
                     Request(close: d, open: OpenDate.AddDays(1)),
                     Request(type: "OPEN", close: d),
                 })
        {
            var act = () => svc.UpdateAsync(id, request);
            await act.Should().ThrowAsync<RiskValidationException>().WithMessage("*ambiguous*");
        }
    }

    [Fact]
    public async Task Ordinary_open_to_close_with_date_still_works()
    {
        await using var db = NewDb();
        var svc = Portfolio(db);
        var added = await svc.AddAsync(new AddPortfolioItemRequest("SYNTH.TO", "Synthetic", 100, 10, "OPEN", "A",
            OpenDate, HoldingRole: "Swing"));
        await new RiskService(db, Principal()).SaveBrokerStopAsync(new SaveBrokerStopRequest(null, "A", "SYNTH.TO",
            "StopMarket", 9, null, 100, null, null, null, null, null, false, null));

        (await svc.UpdateAsync(added.Id, Request(close: new DateTime(2026, 9, 10)))).Should().NotBeNull();
        (await db.TradeEpisodes.SingleAsync()).Status.Should().Be(EpisodeStatuses.Closed);
    }
}
