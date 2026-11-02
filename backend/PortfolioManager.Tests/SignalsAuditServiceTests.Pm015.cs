using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>PM-015: lifecycle-first Signal Tracker query — exact totals, SQL paging, partial-evaluation metadata.</summary>
public sealed partial class SignalsAuditServiceTests
{
    private const int BigCount = 5_100;
    private static readonly string[] BigRoles = ["Core", "Strategic", "Swing", "Speculative"];

    private static Harness BigHarness()
    {
        var h = new Harness([]);
        for (var i = 0; i < BigCount; i++)
        {
            var status = i % 11 == 0 ? SignalStatuses.Dismissed : i % 7 == 0 ? SignalStatuses.Invalidated : i % 5 == 0 ? SignalStatuses.Acted : SignalStatuses.Active;
            var ev = Eff(h, $"S{i % 97:D2}", Day(i % 400), status: status, role: BigRoles[i % 4],
                source: i % 2 == 0 ? SignalSources.Portfolio : SignalSources.Watchlist,
                myAction: status == SignalStatuses.Acted ? SignalMyActions.Actioned : SignalMyActions.Pending, save: false);
            ev.SignalPrice = 50m + i % 13;
        }
        // An Active recommendation far older than any date window must still be listed.
        Eff(h, "OLDACT", Day(399), save: false);
        h.Db.SaveChanges();
        return h;
    }

    private static IEnumerable<long> ExpectedIds(IEnumerable<SignalAuditEvent> all, string sort, bool desc)
    {
        var ord = StringComparer.Ordinal;
        IOrderedEnumerable<SignalAuditEvent> o = sort switch
        {
            "symbol" => desc ? all.OrderByDescending(e => e.Symbol, ord) : all.OrderBy(e => e.Symbol, ord),
            "role" => desc ? all.OrderByDescending(e => e.Role ?? "", ord) : all.OrderBy(e => e.Role ?? "", ord),
            "source" => desc ? all.OrderByDescending(e => e.Source, ord) : all.OrderBy(e => e.Source, ord),
            "signalPrice" => desc ? all.OrderByDescending(e => e.SignalPrice) : all.OrderBy(e => e.SignalPrice),
            "myAction" => desc ? all.OrderByDescending(e => e.MyAction, ord) : all.OrderBy(e => e.MyAction, ord),
            "lifecycleStatus" => desc ? all.OrderByDescending(e => e.Status, ord) : all.OrderBy(e => e.Status, ord),
            "daysSince" => desc ? all.OrderBy(e => e.SignalDate) : all.OrderByDescending(e => e.SignalDate),
            _ => desc ? all.OrderByDescending(e => e.SignalDate) : all.OrderBy(e => e.SignalDate),
        };
        return o.ThenByDescending(e => e.SignalDate).ThenByDescending(e => e.Id).Select(e => e.Id);
    }

    private static async Task<List<long>> AllPagesAsync(Harness h, SignalsAuditQuery q)
    {
        var ids = new List<long>();
        var first = await h.Service.QueryAsync(User, q with { Page = 1, PageSize = 200 });
        ids.AddRange(first.Items.Select(i => i.Id));
        for (var p = 2; p <= (first.Total + 199) / 200; p++)
            ids.AddRange((await h.Service.QueryAsync(User, q with { Page = p, PageSize = 200 })).Items.Select(i => i.Id));
        return ids;
    }

    [Theory]
    [InlineData("signalDate")]
    [InlineData("symbol")]
    [InlineData("role")]
    [InlineData("source")]
    [InlineData("signalPrice")]
    [InlineData("myAction")]
    [InlineData("lifecycleStatus")]
    [InlineData("daysSince")]
    public async Task SqlBackedSorts_PageTheCompleteFilteredSet_WithStableOrder_AndNoPartialFlag(string sort)
    {
        var h = BigHarness();
        var all = await h.Db.SignalAuditEvents.AsNoTracking().ToListAsync();
        Assert.Equal(BigCount + 1, all.Count);

        foreach (var dir in new[] { "desc", "asc" })
        {
            var q = new SignalsAuditQuery(SortBy: sort, SortDir: dir, IncludeUnaudited: true);
            var ids = await AllPagesAsync(h, q);
            Assert.Equal(ExpectedIds(all, sort, dir == "desc").ToList(), ids);
            Assert.Equal(ids.Count, ids.Distinct().Count());

            var page = await h.Service.QueryAsync(User, q with { Page = 2, PageSize = 25 });
            Assert.Equal(BigCount + 1, page.Total);
            Assert.False(page.Partial);
            Assert.Equal(BigCount + 1, page.Evaluated);
        }
    }

    [Fact]
    public async Task DefaultView_IsActiveNewestFirst_AndIncludesTheOldestActive_WithExactTotal()
    {
        var h = BigHarness();
        var expected = (await h.Db.SignalAuditEvents.AsNoTracking().Where(e => e.Status == SignalStatuses.Active).ToListAsync());
        var q = new SignalsAuditQuery(Status: "Active");
        var ids = await AllPagesAsync(h, q);

        Assert.Equal(expected.Count, (await h.Service.QueryAsync(User, q)).Total);
        Assert.Equal(ExpectedIds(expected, "signalDate", true).ToList(), ids);
        Assert.Contains(h.Db.SignalAuditEvents.Single(e => e.Symbol == "OLDACT").Id, ids);
        var rows = (await h.Service.QueryAsync(User, q with { PageSize = 200 })).Items;
        Assert.All(rows, r => Assert.Equal("Active", r.Status));
        Assert.Equal(rows.OrderByDescending(r => r.SignalDate).Select(r => r.SignalDate), rows.Select(r => r.SignalDate));
    }

    [Theory]
    [InlineData("Acted")]
    [InlineData("Invalidated")]
    [InlineData("Dismissed")]
    public async Task StatusFilter_IsAppliedBeforePaging_AndAllReturnsEveryAuthorizedRecord(string status)
    {
        var h = BigHarness();
        var expected = await h.Db.SignalAuditEvents.AsNoTracking().Where(e => e.Status == status).CountAsync();
        var res = await h.Service.QueryAsync(User, new SignalsAuditQuery(Status: status, PageSize: 10, Page: 2));
        Assert.Equal(expected, res.Total);
        Assert.All(res.Items, r => Assert.Equal(status, r.Status));

        var all = await h.Service.QueryAsync(User, new SignalsAuditQuery(Status: "All", PageSize: 10));
        Assert.Equal(BigCount + 1, all.Total);
    }

    [Fact]
    public async Task StatusCombinesWithOtherFilters_AndDateRangeIsOnlyAppliedWhenGiven()
    {
        var h = BigHarness();
        var expected = await h.Db.SignalAuditEvents.AsNoTracking()
            .CountAsync(e => e.Status == SignalStatuses.Active && e.Role == "Swing" && e.Source == SignalSources.Portfolio);
        var res = await h.Service.QueryAsync(User, new SignalsAuditQuery(Status: "Active", Role: ["Swing"], Source: SignalSources.Portfolio));
        Assert.Equal(expected, res.Total);

        var from = Day(30).ToString("yyyy-MM-dd");
        var ranged = await h.Service.QueryAsync(User, new SignalsAuditQuery(Status: "Active", From: from));
        Assert.True(ranged.Total < (await h.Service.QueryAsync(User, new SignalsAuditQuery(Status: "Active"))).Total);
    }

    [Fact]
    public async Task PageBeyondTheEnd_IsEmpty_WithExactTotal()
    {
        var h = BigHarness();
        var res = await h.Service.QueryAsync(User, new SignalsAuditQuery(Page: 999, PageSize: 25));
        Assert.Empty(res.Items);
        Assert.Equal(BigCount + 1, res.Total);
    }

    [Fact]
    public async Task ComputedSorts_AreFlaggedPartial_WithEvaluatedCount_AndSwitchingRestoresCompletePaging()
    {
        var h = BigHarness();
        foreach (var computed in new[] { "return20D", "today", "signalType" })
        {
            var res = await h.Service.QueryAsync(User, new SignalsAuditQuery(SortBy: computed, IncludeUnaudited: true));
            Assert.Equal(BigCount + 1, res.Total);
            Assert.True(res.Partial, computed);
            Assert.Equal(5000, res.Evaluated);
            Assert.Equal(5000, res.EvaluationLimit);

            var sql = await h.Service.QueryAsync(User, new SignalsAuditQuery(SortBy: "signalDate", IncludeUnaudited: true));
            Assert.False(sql.Partial);
            Assert.Equal(BigCount + 1, sql.Evaluated);
        }
    }

    [Fact]
    public async Task ComputedSort_IsNotPartial_WhenEverythingFitsTheEvaluationLimit()
    {
        var h = new Harness([]);
        for (var i = 0; i < 30; i++) Eff(h, $"T{i}", Day(i), save: false);
        h.Db.SaveChanges();
        var res = await h.Service.QueryAsync(User, new SignalsAuditQuery(SortBy: "return20D"));
        Assert.False(res.Partial);
        Assert.Equal(30, res.Total);
        Assert.Equal(30, res.Evaluated);
    }

    [Fact]
    public async Task Summary_KeepsExactTotal_ButMarksKpisPartialWhenOnlyTheNewestRowsWereEvaluated()
    {
        var h = BigHarness();
        var s = await h.Service.GetSummaryAsync(User, new SignalsAuditQuery(IncludeUnaudited: true, IncludeEffectiveness: false));
        Assert.Equal(BigCount + 1, s.Total);
        Assert.True(s.Partial);
        Assert.Equal(5000, s.Evaluated);

        var small = await h.Service.GetSummaryAsync(User, new SignalsAuditQuery(Status: "Acted", IncludeUnaudited: true));
        Assert.False(small.Partial);
        Assert.Equal(small.Total, small.Evaluated);
    }

    [Fact]
    public async Task QueriesAreIsolatedPerUser()
    {
        var h = BigHarness();
        h.AddEvent("OTHER", Day(1), "Core", SignalMyActions.Pending, user: "someone-else");
        var res = await h.Service.QueryAsync(User, new SignalsAuditQuery(PageSize: 200));
        Assert.Equal(BigCount + 1, res.Total);
        Assert.DoesNotContain(res.Items, r => r.Symbol == "OTHER");
        Assert.Equal(1, (await h.Service.QueryAsync("someone-else", new SignalsAuditQuery())).Total);
    }

    [Fact]
    public async Task ReadingFilteringAndSorting_NeverChangeLifecycleOrHistoricalRecords()
    {
        var h = BigHarness();
        static string Fingerprint(IEnumerable<SignalAuditEvent> e) => string.Join("|", e.OrderBy(x => x.Id)
            .Select(x => $"{x.Id}:{x.Status}:{x.StatusChangedAtUtc:O}:{x.StatusReason}:{x.MyAction}:{x.EpisodeEndedDate}:{x.LastValidatedAtUtc:O}:{x.SupersededByEventId}"));
        var before = Fingerprint(await h.Db.SignalAuditEvents.AsNoTracking().ToListAsync());

        foreach (var sort in new[] { "lifecycleStatus", "return20D", "symbol", "signalType" })
        {
            await h.Service.QueryAsync(User, new SignalsAuditQuery(Status: "Active", SortBy: sort));
            await h.Service.QueryAsync(User, new SignalsAuditQuery(Status: "All", SortBy: sort, SortDir: "asc"));
        }
        await h.Service.GetSummaryAsync(User, new SignalsAuditQuery(Status: "Acted"));

        Assert.Equal(before, Fingerprint(await h.Db.SignalAuditEvents.AsNoTracking().ToListAsync()));
        Assert.Equal(BigCount + 1, await h.Db.SignalAuditEvents.CountAsync());
    }

    [Fact]
    public async Task ActiveRecommendation_WithNoCachedEvaluation_IsUnknownHealth_YetActionable_FromItsPriorSession()
    {
        // Documents the verified combination Active + Evidence Health Unknown + Actionable: health describes the cached
        // snapshot, actionability describes the retained prior recommendation. It is expected, not a resolver defect.
        var h = await WithFinalActionAsync("TRM", "Trim / Review");
        h.FinalActions.ExtraWatchlist.Clear();
        h.FinalActions.ExtraWatchlist.Add(Result("TRM", "Cut Loss / Exit", SignalSources.Portfolio, 60m));
        await h.EodAsync();
        h.Db.SignalAuditEvents.Single(e => e.Symbol == "TRM").Role = "Swing";
        await h.Db.SaveChangesAsync();

        // The test double never caches a Final Action snapshot (like the app after a restart), so health is Unknown.
        var row = Assert.Single((await h.Service.QueryAsync(User, new SignalsAuditQuery())).Items.Where(r => r.Symbol == "TRM"));
        Assert.Equal(SignalStatuses.Active, row.Status);
        Assert.Equal("Unknown", row.EvidenceHealth);
        Assert.Equal("PriorSessionValid", row.EffectiveState);
        Assert.True(row.IsActionable);
    }

    [Fact]
    public async Task EffectiveStateOverlay_DoesNotChangePageBoundaries()
    {
        // Rows are enriched after the page is chosen: the same Ids come back whether or not an overlay applies.
        var h = BigHarness();
        var a = (await h.Service.QueryAsync(User, new SignalsAuditQuery(Status: "Active", Page: 3, PageSize: 50))).Items.Select(i => i.Id).ToList();
        var b = (await h.Service.QueryAsync(User, new SignalsAuditQuery(Status: "Active", Page: 3, PageSize: 50, IncludeEffectiveness: false))).Items.Select(i => i.Id).ToList();
        Assert.Equal(a, b);
        Assert.Equal(50, a.Count);
    }
}
