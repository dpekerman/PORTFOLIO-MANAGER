using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Tests;

/// <summary>PM-032: stop-limit validation, closed-position reminders, grouped reconciliation and partial-close coverage (synthetic in-memory data only).</summary>
public class Pm032SimpleRiskTests
{
    private sealed class FixedAccessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }

    private static IHttpContextAccessor Principal(string uid = "u1") => new FixedAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, uid), new Claim(ClaimTypes.Name, "tester"), new Claim(ClaimTypes.Role, "Admin")], "test")),
        },
    };

    private static AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Pm033RiskTests.SimulateRowVersions(db);
        foreach (var n in new[] { "A", "B" }) db.AccountTypes.Add(new AccountType { Name = n, NormalizedName = n.ToUpperInvariant() });
        db.SaveChanges();
        return db;
    }

    private static PortfolioService Svc(AppDbContext db) => new(db, null!, new MutationClock(), Principal());
    private static RiskService Risk(AppDbContext db) => new(db, Principal());

    private static SaveBrokerStopRequest StopLimit(decimal trigger, decimal? limit, decimal? qty, string account = "A", string sym = "ABC.TO",
        bool allow = false, string? reason = null)
        => new(null, account, sym, "StopLimit", trigger, limit, qty, null, null, DateTime.UtcNow, null, reason, allow, null);

    private sealed record LotHandle(int Id);

    private static async Task<LotHandle> Open(AppDbContext db, decimal shares, string account = "A", string sym = "ABC.TO", string role = "Swing", string date = "2026-09-01")
    {
        var dto = await Svc(db).AddAsync(new AddPortfolioItemRequest(sym, sym, shares, 10, "OPEN", account, DateTime.Parse(date), HoldingRole: role));
        return new LotHandle(dto.Id);
    }

    private static async Task Close(AppDbContext db, int lotId, decimal shares, string account = "A", string sym = "ABC.TO", string role = "Swing", string open = "2026-09-01")
        => await Svc(db).UpdateAsync(lotId, new UpdatePortfolioItemRequest(sym, shares, 10, TransactionType: "CLOSE", AccountType: account,
            OpenDate: DateTime.Parse(open), CloseDate: new DateTime(2026, 9, 20), ClosingPrice: 12, HoldingRole: role));

    private static SwingTradingService Trades(AppDbContext db) => new(db, Principal(), new ConfigurationBuilder().Build());

    [Fact]
    public async Task StopLimit_requires_a_limit_that_does_not_exceed_the_trigger()
    {
        await using var db = NewDb();
        await Open(db, 100);
        var risk = Risk(db);
        await ((Func<Task>)(() => risk.SaveBrokerStopAsync(StopLimit(9m, null, 100m)))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => risk.SaveBrokerStopAsync(StopLimit(9m, 9.5m, 100m)))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => risk.SaveBrokerStopAsync(StopLimit(9m, 0m, 100m)))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => risk.SaveBrokerStopAsync(StopLimit(9m, 8.9m, 0m)))).Should().ThrowAsync<RiskValidationException>();
        (await db.BrokerStopRecords.CountAsync()).Should().Be(0, "failed saves write nothing");
        var pos = await risk.SaveBrokerStopAsync(StopLimit(9m, 8.9m, 100m));
        pos.BrokerStops.Single().OrderType.Should().Be("StopLimit");
        pos.BrokerStops.Single().BrokerVerificationStatus.Should().Be(BrokerVerification.Unverified);
        pos.BrokerStops.Single().LastUserVerifiedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Combined_coverage_of_separate_orders_cannot_silently_exceed_open_shares()
    {
        await using var db = NewDb();
        await Open(db, 100);
        var risk = Risk(db);
        await risk.SaveBrokerStopAsync(StopLimit(9m, 8.9m, 60m));
        await ((Func<Task>)(() => risk.SaveBrokerStopAsync(StopLimit(9m, 8.9m, 60m)))).Should().ThrowAsync<RiskValidationException>();
        var pos = await risk.SaveBrokerStopAsync(StopLimit(9m, 8.9m, 40m));
        pos.BrokerStops.Count(s => s.IsActive).Should().Be(2, "two legitimate separate orders are kept separate");
    }

    [Fact]
    public async Task Legacy_records_are_not_rewritten_and_non_stoplimit_types_keep_working()
    {
        await using var db = NewDb();
        await Open(db, 100);
        var risk = Risk(db);
        var pos = await risk.SaveBrokerStopAsync(new SaveBrokerStopRequest(null, "A", "ABC.TO", "StopMarket", 9m, null, 100m, null, null, null, null, null, false, null));
        pos.BrokerStops.Single().OrderType.Should().Be("StopMarket");
        pos.BrokerStops.Single().LastUserVerifiedDate.Should().BeNull("nothing is self-confirmed unless the user did it");
    }

    [Fact]
    public async Task Partial_close_leaves_an_over_covering_order_flagged_never_full_protection()
    {
        await using var db = NewDb();
        var lot = await Open(db, 1500);
        await Risk(db).SaveBrokerStopAsync(StopLimit(9m, 8.9m, 1500m));
        await Close(db, lot.Id, 500);
        var row = (await Trades(db).GetOpenTradesAsync()).Rows.Single();
        row.TotalQuantity.Should().Be(1000);
        row.CoveredQuantity.Should().Be(1500);
        row.CoverageState.Should().Be(CoverageStates.OverCovered);
        row.RiskStatuses.Should().Contain(RiskStatuses.OverCovered);
        row.NeedsAttention.Should().BeTrue();
        (await db.BrokerStopRecords.SingleAsync()).UserStatus.Should().Be(BrokerUserStatuses.UserConfirmedEstablished, "the order is not auto-amended");
    }

    [Fact]
    public async Task Full_close_removes_the_position_from_active_risk_and_groups_one_reminder_per_episode()
    {
        await using var db = NewDb();
        var lot = await Open(db, 100);
        var risk = Risk(db);
        await risk.SaveBrokerStopAsync(StopLimit(9m, 8.9m, 60m));
        await risk.SaveBrokerStopAsync(StopLimit(9m, 8.8m, 40m));
        await Close(db, lot.Id, 100);

        var res = await Trades(db).GetOpenTradesAsync();
        res.Rows.Should().BeEmpty();
        res.Summary.NeedsAttention.Should().Be(0);
        res.Summary.OpenTrades.Should().Be(0);
        res.BrokerOrderReminderGroups.Should().ContainSingle().Which.Orders.Should().HaveCount(2);
        (await db.BrokerStopRecords.CountAsync()).Should().Be(2, "history is never deleted");
    }

    [Fact]
    public async Task Active_order_on_another_roles_open_holding_is_not_a_closed_swing_reminder()
    {
        await using var db = NewDb();
        await Open(db, 100, role: "Strategic");
        db.BrokerStopRecords.Add(new BrokerStopRecord { UserId = "u1", AccountType = "A", Symbol = "ABC.TO", TriggerPrice = 9m, Currency = "CAD" });
        await db.SaveChangesAsync();
        (await Trades(db).GetOpenTradesAsync()).BrokerOrderReminderGroups.Should().BeEmpty();
    }

    private static async Task<(AppDbContext Db, RiskService Risk, List<BrokerStopDto> Retired)> ClosedWithTwoOrdersAsync()
    {
        var db = NewDb();
        var lot = await Open(db, 100);
        var risk = Risk(db);
        await risk.SaveBrokerStopAsync(StopLimit(9m, 8.9m, 60m));
        await risk.SaveBrokerStopAsync(StopLimit(9m, 8.8m, 40m));
        await Close(db, lot.Id, 100);
        var retired = (await risk.GetPositionAsync("A", "ABC.TO")).BrokerStops.ToList();
        return (db, risk, retired);
    }

    private static ResolveBrokerOrdersRequest Resolve(IEnumerable<BrokerStopDto> stops, string status = BrokerUserStatuses.UserReportedCancelled,
        string? requestId = "req-1", bool confirmed = true, string account = "A")
        => new(account, "ABC.TO", status, stops.Select(s => new ResolveBrokerOrderItem(s.Id, s.RowVersion)).ToList(), requestId, confirmed);

    [Fact]
    public async Task Grouped_reconciliation_transitions_only_the_selected_orders_and_audits_each()
    {
        var (db, risk, retired) = await ClosedWithTwoOrdersAsync();
        await using var _ = db;
        var result = await risk.ResolveBrokerOrdersAsync(Resolve([retired[0]]));
        result.Resolved.Should().Be(1);
        var recs = await db.BrokerStopRecords.OrderBy(s => s.Id).ToListAsync();
        recs.Count(r => r.UserStatus == BrokerUserStatuses.UserReportedCancelled).Should().Be(1);
        recs.Count(r => r.UserStatus == BrokerUserStatuses.EpisodeClosed && r.BrokerCancellationUnverified).Should().Be(1, "the unselected order still needs reconciliation");
        recs.Should().OnlyContain(r => r.BrokerVerificationStatus == BrokerVerification.Unverified);
        (await db.RiskAuditEvents.CountAsync(e => e.EventType == "BrokerStatusChanged")).Should().Be(1);

        await risk.ResolveBrokerOrdersAsync(Resolve([retired[1]], BrokerUserStatuses.UserReportedExpired, "req-2"));
        (await Trades(db).GetOpenTradesAsync()).BrokerOrderReminderGroups.Should().BeEmpty("reminder disappears after reconciliation");
        (await db.BrokerStopRecords.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Grouped_reconciliation_rejects_unconfirmed_replayed_resolved_and_wrong_scope_requests_without_writing()
    {
        var (db, risk, retired) = await ClosedWithTwoOrdersAsync();
        await using var _ = db;
        var before = await db.RiskAuditEvents.CountAsync();

        await ((Func<Task>)(() => risk.ResolveBrokerOrdersAsync(Resolve(retired, confirmed: false)))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => risk.ResolveBrokerOrdersAsync(Resolve(retired, status: "Superseded")))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => risk.ResolveBrokerOrdersAsync(Resolve(retired, requestId: null)))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => risk.ResolveBrokerOrdersAsync(Resolve(retired.Concat(retired))))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => risk.ResolveBrokerOrdersAsync(Resolve(retired, account: "B")))).Should().ThrowAsync<RiskValidationException>();
        await ((Func<Task>)(() => risk.ResolveBrokerOrdersAsync(Resolve([new BrokerStopDto(999, Guid.NewGuid(), "StopLimit", 1, 1, "CAD", "x", "Unverified", 1,
            null, null, null, "ManualEntry", null, null, false, false, true, null, DateTime.UtcNow, "AQ==")])))).Should().ThrowAsync<RiskNotFoundException>();
        (await db.RiskAuditEvents.CountAsync()).Should().Be(before);
        (await db.BrokerStopRecords.CountAsync(s => s.UserStatus == BrokerUserStatuses.EpisodeClosed)).Should().Be(2);

        await risk.ResolveBrokerOrdersAsync(Resolve(retired));
        await ((Func<Task>)(() => risk.ResolveBrokerOrdersAsync(Resolve(retired)))).Should().ThrowAsync<RiskConflictException>("same request id replayed");
        await ((Func<Task>)(() => risk.ResolveBrokerOrdersAsync(Resolve(retired, requestId: "req-new")))).Should().ThrowAsync<RiskValidationException>("already resolved");
        (await db.RiskAuditEvents.CountAsync(e => e.EventType == "BrokerStatusChanged")).Should().Be(2);
    }

    [Fact]
    public async Task Reconciliation_never_touches_the_reopened_positions_current_order_and_reopen_shows_no_coverage()
    {
        var (db, risk, retired) = await ClosedWithTwoOrdersAsync();
        await using var _ = db;
        await Open(db, 50, date: "2026-10-15");
        var current = (await risk.SaveBrokerStopAsync(StopLimit(12m, 11.5m, 50m))).BrokerStops.Single(s => s.BelongsToCurrentEpisode);

        var row = (await Trades(db).GetOpenTradesAsync()).Rows.Single();
        row.CoverageState.Should().Be(CoverageStates.Full);
        await ((Func<Task>)(() => risk.ResolveBrokerOrdersAsync(Resolve([current], requestId: "r-cur")))).Should().ThrowAsync<RiskValidationException>();
        (await db.BrokerStopRecords.SingleAsync(s => s.Id == current.Id)).UserStatus.Should().Be(BrokerUserStatuses.UserConfirmedEstablished);

        await risk.ResolveBrokerOrdersAsync(Resolve(retired, requestId: "r-old"));
        (await db.BrokerStopRecords.SingleAsync(s => s.Id == current.Id)).UserStatus.Should().Be(BrokerUserStatuses.UserConfirmedEstablished);
    }

    [Fact]
    public async Task Reopened_ticker_without_a_new_order_has_no_inherited_coverage()
    {
        var (db, _, _) = await ClosedWithTwoOrdersAsync();
        await using var _db = db;
        await Open(db, 50, date: "2026-10-15");
        var row = (await Trades(db).GetOpenTradesAsync()).Rows.Single();
        row.CoverageState.Should().Be(CoverageStates.None);
        row.RiskStatuses.Should().Contain(RiskStatuses.NoBrokerProtection);
    }

    [Fact]
    public async Task Orders_in_a_different_account_are_not_counted_toward_this_positions_coverage()
    {
        await using var db = NewDb();
        await Open(db, 100, "A");
        await Open(db, 100, "B");
        var risk = Risk(db);
        await risk.SaveBrokerStopAsync(StopLimit(9m, 8.9m, 100m, "A"));
        var rows = (await Trades(db).GetOpenTradesAsync()).Rows;
        rows.Single(r => r.Account == "A").CoverageState.Should().Be(CoverageStates.Full);
        rows.Single(r => r.Account == "B").CoverageState.Should().Be(CoverageStates.None);
    }
}
