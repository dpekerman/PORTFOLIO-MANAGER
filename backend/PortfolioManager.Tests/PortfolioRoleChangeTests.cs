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

/// <summary>PM-014: Investment Role is a classification; correcting it must not touch transactions, episodes or broker stops.</summary>
public class PortfolioRoleChangeTests
{
    private sealed class FixedAccessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }

    private static IHttpContextAccessor Principal(string uid = "u1", string role = "Admin") => new FixedAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, uid), new Claim(ClaimTypes.Name, "tester"), new Claim(ClaimTypes.Role, role)], "test")),
        },
    };

    private static AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Pm033RiskTests.SimulateRowVersions(db);
        db.AccountTypes.Add(new AccountType { Name = "A", NormalizedName = "A" });
        db.SaveChanges();
        return db;
    }

    private static PortfolioService Svc(AppDbContext db, string uid = "u1") => new(db, null!, new MutationClock(), Principal(uid));

    private static TradeEpisode Episode(string sym, string open = "2026-09-01", string status = EpisodeStatuses.Open, string? uid = "u1") => new()
    {
        UserId = uid, AccountType = "A", Symbol = sym, OpenDate = DateTime.Parse(open), Status = status,
    };

    private static PortfolioItem Lot(int id, string sym, string type, string role, Guid? ep, string open = "2026-09-01", string? close = null,
        decimal shares = 100, string? uid = "u1") => new()
    {
        Id = id, Symbol = sym, CompanyName = sym, Shares = shares, AverageCostBasis = 10m, AccountType = "A", TransactionType = type,
        OpenDate = DateTime.Parse(open), CloseDate = close is null ? null : DateTime.Parse(close), ClosingPrice = close is null ? null : 12m,
        HoldingRole = role, TradeEpisodeId = ep, UserId = uid, DecisionSource = "Rebalance",
    };

    private static BrokerStopRecord Stop(string sym, Guid ep) => new()
    {
        UserId = "u1", AccountType = "A", Symbol = sym, TriggerPrice = 9m, Currency = "CAD", CoveredQuantity = 100,
        TradeEpisodeId = ep, EpisodeOpenDate = new DateTime(2026, 9, 1), ImportKey = "sheet:1:1",
    };

    private static async Task Change(PortfolioService svc, AppDbContext db, bool put, int id, string role)
    {
        if (!put) { (await svc.UpdateHoldingRoleAsync(id, role)).Should().BeTrue(); return; }
        var i = await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == id);
        var r = await svc.UpdateAsync(id, new UpdatePortfolioItemRequest(i.CompanyName, i.Shares, i.AverageCostBasis, i.Sector, i.Industry, false,
            i.TransactionType, i.AccountType, i.OpenDate, i.CloseDate, i.ClosingPrice, role, i.DecisionSource, i.DecisionSourceClosed));
        r.Should().NotBeNull();
    }

    private static string Snapshot(PortfolioItem i) =>
        $"{i.Symbol}|{i.Shares}|{i.AverageCostBasis}|{i.TransactionType}|{i.AccountType}|{i.OpenDate}|{i.CloseDate}|{i.ClosingPrice}|{i.TradeEpisodeId}|{i.DecisionSource}|{i.UserId}";

    private static string StopSnapshot(BrokerStopRecord s) =>
        $"{s.Id}|{s.OrderIdentity}|{s.ImportKey}|{s.UserStatus}|{s.TriggerPrice}|{s.CoveredQuantity}|{s.TradeEpisodeId}|{s.BrokerCancellationUnverified}|{Convert.ToBase64String(s.RowVersion ?? [])}";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Hun_swing_to_speculative_succeeds_and_preserves_everything(bool put)
    {
        await using var db = NewDb();
        var ep = Episode("HUN.TO");
        db.TradeEpisodes.Add(ep);
        db.PortfolioItems.Add(Lot(1, "HUN.TO", "OPEN", "Swing", ep.Id));
        db.BrokerStopRecords.Add(Stop("HUN.TO", ep.Id));
        await db.SaveChangesAsync();
        var lotBefore = Snapshot(await db.PortfolioItems.AsNoTracking().SingleAsync());
        var stopBefore = StopSnapshot(await db.BrokerStopRecords.AsNoTracking().SingleAsync());
        var auditBefore = await db.RiskAuditEvents.CountAsync();

        await Change(Svc(db), db, put, 1, "Speculative");

        db.ChangeTracker.Clear();
        var lot = await db.PortfolioItems.AsNoTracking().SingleAsync();
        lot.HoldingRole.Should().Be("Speculative");
        Snapshot(lot).Should().Be(lotBefore);
        StopSnapshot(await db.BrokerStopRecords.AsNoTracking().SingleAsync()).Should().Be(stopBefore);
        (await db.TradeEpisodes.CountAsync()).Should().Be(1);
        (await db.RiskAuditEvents.CountAsync()).Should().Be(auditBefore + 1);
        (await db.RiskAuditEvents.OrderByDescending(e => e.Id).FirstAsync()).EventType.Should().Be("HoldingRoleCorrected");
        (await db.AuditLogs.CountAsync(a => a.Category == AuditCategories.Risk)).Should().Be(1);

        var rows = (await new SwingTradingService(db, Principal(), new ConfigurationBuilder().Build()).GetOpenTradesAsync()).Rows;
        rows.Should().NotContain(r => r.Symbol == "HUN.TO", "Swing membership is derived from the lot role");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Strategic_to_speculative_without_any_risk_history_succeeds(bool put)
    {
        await using var db = NewDb();
        db.PortfolioItems.Add(Lot(1, "X.TO", "OPEN", "Strategic", null));
        await db.SaveChangesAsync();
        await Change(Svc(db), db, put, 1, "Speculative");
        (await db.PortfolioItems.SingleAsync()).HoldingRole.Should().Be("Speculative");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Speculative_to_swing_with_a_consistent_episode_succeeds_with_correct_days_held(bool put)
    {
        await using var db = NewDb();
        var ep = Episode("S.TO");
        db.TradeEpisodes.Add(ep);
        db.PortfolioItems.Add(Lot(1, "S.TO", "OPEN", "Speculative", ep.Id));
        await db.SaveChangesAsync();
        await Change(Svc(db), db, put, 1, "Swing");
        var row = (await new SwingTradingService(db, Principal(), new ConfigurationBuilder().Build()).GetOpenTradesAsync()).Rows.Single();
        row.Symbol.Should().Be("S.TO");
        row.EpisodeOpenDate.Should().Be(new DateTime(2026, 9, 1));
        row.DaysHeld.Should().Be(TradeEpisodeResolver.DaysHeld(new DateTime(2026, 9, 1), DateTime.UtcNow));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Speculative_to_swing_without_a_safe_episode_is_a_409_and_changes_nothing(bool put)
    {
        await using var db = NewDb();
        db.PortfolioItems.Add(Lot(1, "S.TO", "OPEN", "Speculative", null));
        await db.SaveChangesAsync();
        var act = () => Change(Svc(db), db, put, 1, "Swing");
        await act.Should().ThrowAsync<RiskConflictException>();
        db.ChangeTracker.Clear();
        (await db.PortfolioItems.SingleAsync()).HoldingRole.Should().Be("Speculative");
        (await db.RiskAuditEvents.CountAsync()).Should().Be(0);
        (await db.TradeEpisodes.CountAsync()).Should().Be(0, "no episode is invented");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Open_lot_change_that_would_mix_roles_inside_one_episode_is_a_specific_409(bool put)
    {
        await using var db = NewDb();
        var ep = Episode("M.TO");
        db.TradeEpisodes.Add(ep);
        db.PortfolioItems.AddRange(Lot(1, "M.TO", "OPEN", "Swing", ep.Id), Lot(2, "M.TO", "OPEN", "Swing", ep.Id, "2026-09-10"));
        await db.SaveChangesAsync();
        var act = () => Change(Svc(db), db, put, 1, "Speculative");
        (await act.Should().ThrowAsync<RiskConflictException>()).Which.Message.Should().Contain("different roles");
        db.ChangeTracker.Clear();
        (await db.PortfolioItems.AsNoTracking().ToListAsync()).Should().OnlyContain(x => x.HoldingRole == "Swing", "other lots are never silently reclassified");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Closed_correction_is_allowed_even_when_other_open_lots_share_the_episode(bool put)
    {
        await using var db = NewDb();
        var ep = Episode("C.TO");
        db.TradeEpisodes.Add(ep);
        db.PortfolioItems.AddRange(Lot(1, "C.TO", "CLOSE", "Swing", ep.Id, "2026-09-01", "2026-09-20"), Lot(2, "C.TO", "OPEN", "Swing", ep.Id, "2026-09-01", shares: 50));
        db.RiskAuditEvents.Add(new RiskAuditEvent { AccountType = "A", Symbol = "C.TO", EventType = "Existing", TradeEpisodeId = ep.Id });
        await db.SaveChangesAsync();
        var before = Snapshot(await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == 1));

        await Change(Svc(db), db, put, 1, "Speculative");

        db.ChangeTracker.Clear();
        var closed = await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == 1);
        closed.HoldingRole.Should().Be("Speculative");
        Snapshot(closed).Should().Be(before);
        (await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == 2)).HoldingRole.Should().Be("Swing");
        (await db.RiskAuditEvents.AnyAsync(e => e.EventType == "Existing")).Should().BeTrue("audit history is append-only");
        (await db.TradeEpisodes.SingleAsync()).Status.Should().Be(EpisodeStatuses.Open);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Closed_lot_with_no_role_can_be_assigned_a_role_and_it_persists(bool put)
    {
        await using var db = NewDb();
        db.PortfolioItems.Add(Lot(1, "BDT.TO", "CLOSE", null!, null, "2026-05-01", "2026-07-07"));
        await db.SaveChangesAsync();
        var before = Snapshot(await db.PortfolioItems.AsNoTracking().SingleAsync());

        await Change(Svc(db), db, put, 1, "Core");

        db.ChangeTracker.Clear();
        var lot = await db.PortfolioItems.AsNoTracking().SingleAsync();
        lot.HoldingRole.Should().Be("Core");
        Snapshot(lot).Should().Be(before);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Same_role_is_a_no_op_without_audit(bool put)
    {
        await using var db = NewDb();
        var ep = Episode("N.TO");
        db.TradeEpisodes.Add(ep);
        db.PortfolioItems.Add(Lot(1, "N.TO", "OPEN", "Swing", ep.Id));
        await db.SaveChangesAsync();
        await Change(Svc(db), db, put, 1, "Swing");
        (await db.RiskAuditEvents.CountAsync()).Should().Be(0);
        (await db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cross_user_role_change_is_not_found(bool put)
    {
        await using var db = NewDb();
        db.PortfolioItems.Add(Lot(1, "O.TO", "OPEN", "Strategic", null, uid: "other"));
        await db.SaveChangesAsync();
        var svc = new PortfolioService(db, null!, new MutationClock(), Principal("u1", "Trader"));
        if (put)
            (await svc.UpdateAsync(1, new UpdatePortfolioItemRequest("O.TO", 100, 10m, AccountType: "A", TransactionType: "OPEN", HoldingRole: "Speculative"))).Should().BeNull();
        else
            (await svc.UpdateHoldingRoleAsync(1, "Speculative")).Should().BeFalse();
        (await db.PortfolioItems.SingleAsync()).HoldingRole.Should().Be("Strategic");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Legacy_null_owner_lot_can_be_corrected(bool put)
    {
        await using var db = NewDb();
        db.PortfolioItems.Add(Lot(1, "L.TO", "OPEN", "Core", null, uid: null));
        await db.SaveChangesAsync();
        await Change(Svc(db), db, put, 1, "Speculative");
        (await db.PortfolioItems.SingleAsync()).HoldingRole.Should().Be("Speculative");
    }

    [Fact]
    public async Task Other_changes_on_a_lot_with_risk_history_are_still_guarded()
    {
        await using var db = NewDb();
        var ep = Episode("G.TO");
        db.TradeEpisodes.Add(ep);
        db.PortfolioItems.Add(Lot(1, "G.TO", "OPEN", "Swing", ep.Id));
        await db.SaveChangesAsync();
        var act = () => Svc(db).UpdateAsync(1, new UpdatePortfolioItemRequest("G.TO", 100, 10m, AccountType: "A", TransactionType: "OPEN",
            OpenDate: new DateTime(2026, 8, 1), HoldingRole: "Swing"));
        await act.Should().ThrowAsync<RiskValidationException>();
    }
}
