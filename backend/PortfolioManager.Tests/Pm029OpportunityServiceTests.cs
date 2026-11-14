using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;
using PortfolioManager.Api.Services.Opportunities;
using static PortfolioManager.Tests.Pm029FinalActionGoldenTests;

namespace PortfolioManager.Tests;

/// <summary>PM-029 Phase 4b: orchestration, snapshot reads, refresh safety (§4.3) and the C1 cash invariant.</summary>
public sealed class Pm029OpportunityServiceTests
{
    private static readonly DateOnly E = TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Tsx);

    static Pm029OpportunityServiceTests() => OpportunityService.HistoryBatchDelay = TimeSpan.Zero;

    private sealed class FakeMarketData : IMarketDataProvider
    {
        public Dictionary<string, IReadOnlyList<MarketDailyClose>> Bars { get; } = new(StringComparer.OrdinalIgnoreCase);
        public System.Collections.Concurrent.ConcurrentDictionary<string, int> HistoryCalls { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int Calls;
        public TaskCompletionSource? Gate;

        public async Task<IReadOnlyList<MarketDailyClose>?> GetDailyClosesAsync(string symbol, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            HistoryCalls.AddOrUpdate(symbol, 1, (_, n) => n + 1);
            if (Gate is not null) await Gate.Task;
            return Bars.GetValueOrDefault(symbol);
        }

        public Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default) { Interlocked.Increment(ref Calls); return Task.FromResult<StockQuote?>(null); }
        public Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default) { Interlocked.Increment(ref Calls); return Task.FromResult(new Dictionary<string, StockQuote>()); }
        public Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default) { Interlocked.Increment(ref Calls); return Task.FromResult(("", "")); }
        public Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);
        public Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, decimal>());
        public Task<FundamentalsSnapshot?> GetFundamentalsAsync(string symbol, CancellationToken ct = default) { Interlocked.Increment(ref Calls); return Task.FromResult<FundamentalsSnapshot?>(null); }
        public Task<Dictionary<string, DateTime>> GetEarningsDatesAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, DateTime>());
        public Task<Dictionary<string, decimal>> GetHistoricalClosingPricesAsync(string dateStr, IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, decimal>());
    }

    private sealed class Resolver(params string[] needsInput) : ISecurityAnalysisResolver
    {
        public Task<ResolvedSecurityAnalysis> ResolveAsync(string t, string? userId, CancellationToken ct = default) =>
            Task.FromResult(new ResolvedSecurityAnalysis(t, t, "CA", "CAD", false,
                needsInput.Contains(t) ? UnderlyingResolutionStatus.NeedsUserInput : UnderlyingResolutionStatus.NotApplicable, null));
        public Task<bool> ValidateUnderlyingTickerAsync(string u, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResolvedSecurityAnalysis> SaveUserMappingAsync(string t, string u, string userId, bool use, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RemoveUserMappingAsync(string t, string userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FailingFinalActions : IFinalActionService
    {
        public Task<FinalActionSnapshotDto> ComputeAsync(string userId, bool fresh = false, CancellationToken ct = default) => throw new InvalidOperationException("boom");
        public Task<int> PublishPortfolioActionsAsync(string userId, FinalActionSnapshotDto snapshot, CancellationToken ct = default) => throw new InvalidOperationException("boom");
    }

    private static List<MarketDailyClose> Bars(int sessions, Func<int, decimal> closeAtAge)
    {
        var list = new List<MarketDailyClose>();
        var d = E;
        for (var age = 0; age <= sessions; age++)
        {
            list.Add(new MarketDailyClose(d, closeAtAge(age)));
            d = TradingCalendar.PreviousTradingDay(d, MarketExchange.Tsx);
        }
        list.Reverse();
        return list;
    }

    private sealed record Fixture(OpportunityService Service, AppDbContext Db, FinalActionService FinalActions, FakeMarketData Market, FakeScanner Scanner);

    private static async Task<Fixture> SetUpAsync(params string[] needsInput)
    {
        var (finalActions, db, scanner) = await SetUpGoldenAsync();
        db.IndexConstituents.AddRange(
            new IndexConstituent { Symbol = "EEE.TO", CompanyName = "Eee Energy", EffectiveFrom = new DateTime(2020, 1, 1), Source = "test", SourceAsOfDate = DateTime.UtcNow },
            new IndexConstituent { Symbol = "ZZZ.TO", CompanyName = "Zzz Corp", EffectiveFrom = new DateTime(2020, 1, 1), Source = "test", SourceAsOfDate = DateTime.UtcNow },
            new IndexConstituent { Symbol = "OLD.TO", CompanyName = "Retired", EffectiveFrom = new DateTime(2020, 1, 1), EffectiveTo = new DateTime(2024, 1, 1), Source = "test", SourceAsOfDate = DateTime.UtcNow });
        db.SectorBenchmarkMappings.Add(new SectorBenchmarkMapping
        {
            Sector = "Tech", Market = "Tsx", BenchmarkSymbol = "XIT.TO", BenchmarkName = "Tech ETF", Currency = "CAD", IsActive = true,
        });
        db.RiskPlans.Add(new RiskPlan { UserId = "u", AccountType = "MARGIN", Symbol = "BBB.TO", PlannedStopPrice = 100m, TargetPrice = 140m, Currency = "CAD" });
        db.ValueScreenerScheduleConfigs.Add(new ValueScreenerScheduleConfig());
        await db.SaveChangesAsync();

        var market = new FakeMarketData();
        // BBB.TO: 100 → 110 over 20 sessions (+10 %); XIT.TO: 200 → 210 (+5 %). Sector Heat 20D must be +5.0 pp.
        market.Bars["BBB.TO"] = Bars(80, a => a == 0 ? 110m : 100m);
        market.Bars["XIT.TO"] = Bars(80, a => a == 0 ? 210m : 200m);

        var options = (DbContextOptions<AppDbContext>)db.GetService<IDbContextOptions>();
        var provider = new ServiceCollection().AddScoped(_ => new AppDbContext(options)).BuildServiceProvider();
        var persistence = new ValueScreenerPersistenceService(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ValueScreenerPersistenceService>.Instance);
        var screener = new ValueScreenerService(null!, null!, market, scanner, NullLogger<ValueScreenerService>.Instance);
        var service = new OpportunityService(db, finalActions, market, new Resolver(needsInput), screener, persistence,
            NullLogger<OpportunityService>.Instance);
        return new Fixture(service, db, finalActions, market, scanner);
    }

    private static OpportunityService With(Fixture f, IFinalActionService finalActions) =>
        new(f.Db, finalActions, f.Market, new Resolver(), null!, null!, NullLogger<OpportunityService>.Instance);

    [Fact]
    public async Task Refresh_BuildsTheUniverse_FromWatchlistHoldingsAndCurrentConstituentsOnly()
    {
        var f = await SetUpAsync();
        var snap = await f.Service.RefreshAsync("u");

        Assert.Equal(["AAA.TO", "BBB.TO", "CCC.TO", "DDD", "EEE.TO", "FFF.TO", "ZZZ.TO"], snap.Rows.Select(r => r.Symbol).Order().ToArray());
        Assert.DoesNotContain(snap.Rows, r => r.Symbol == "OLD.TO");
        Assert.Equal(["Portfolio", "TSX60"], snap.Rows.Single(r => r.Symbol == "EEE.TO").Sources);
        Assert.Equal(new OpportunityUniverseCountsDto(4, 3, 2, 7), snap.Counts);
        Assert.Equal("Succeeded", snap.Status);
        Assert.False(snap.IsStale);

        // A symbol without a scan is listed (discoverable) but never scored or given a decision.
        var zzz = snap.Rows.Single(r => r.Symbol == "ZZZ.TO");
        Assert.Equal("NoScan", zzz.RowStatus);
        Assert.Null(zzz.Swing.Score);
        Assert.Null(zzz.Strategic.Score);
        Assert.Equal("Zzz Corp", zzz.CompanyName);
    }

    [Fact]
    public async Task Refresh_CopiesEveryAuthoritativeDecision_AndFlagsConflictsAsMultiple()
    {
        var f = await SetUpAsync();
        var snap = await f.Service.RefreshAsync("u");
        var fa = await f.FinalActions.ComputeAsync("u");

        foreach (var row in snap.Rows.Where(r => r.Symbol != "ZZZ.TO"))
        {
            var detail = await f.Service.GetDetailAsync("u", row.Symbol, snap.SnapshotId);
            Assert.NotNull(detail);
            var expected = fa.Watchlist.Concat(fa.Portfolio).Where(r => r.Symbol == row.Symbol).ToList();
            Assert.Equal(expected.Count, detail!.Decisions.Count);
            Assert.Equal(JsonSerializer.Serialize(expected.OrderBy(d => d.Source).ThenBy(d => d.ItemId).Select(d => (d.FinalAction, d.Reason, d.EvaluationState))),
                JsonSerializer.Serialize(detail.Decisions.OrderBy(d => d.Source).ThenBy(d => d.ItemId).Select(d => (d.FinalAction, d.Reason, d.EvaluationState))));

            var primary = row.Owned ? expected.Where(d => d.Source == "Portfolio").ToList() : expected;
            var labels = primary.Select(d => d.FinalAction).Distinct().ToList();
            Assert.Equal(labels.Count > 1 ? "Multiple" : "Single", row.FinalAction.Kind);
            if (labels.Count == 1) Assert.Equal(labels[0], row.FinalAction.Label);
            else Assert.Null(row.FinalAction.Label);
        }
    }

    [Fact]
    public async Task Refresh_ComputesSectorHeatFromHistories_AndShowsTheSavedPlanSeparately()
    {
        var f = await SetUpAsync();
        var snap = await f.Service.RefreshAsync("u");

        var bbb = snap.Rows.Single(r => r.Symbol == "BBB.TO");
        Assert.Equal("Tech", bbb.Sector);
        Assert.Equal(5.0m, bbb.SectorHeat.DiffPp);
        Assert.Equal(10m, bbb.SectorHeat.StockReturnPct);
        Assert.Equal(5m, bbb.SectorHeat.BenchmarkReturnPct);
        Assert.Equal("XIT.TO", bbb.SectorHeat.BenchmarkSymbol);
        Assert.Equal("Positive", bbb.SectorHeat.Direction);

        // Each history is fetched once per refresh (stock + benchmark), never per row.
        Assert.Equal(1, f.Market.HistoryCalls["BBB.TO"]);
        Assert.Equal(1, f.Market.HistoryCalls["XIT.TO"]);

        var detail = await f.Service.GetDetailAsync("u", "BBB.TO", snap.SnapshotId);
        var plan = Assert.Single(detail!.SavedPlans);
        Assert.Equal("MARGIN", plan.Account);
        Assert.Equal(100m, plan.PlannedStop);
        Assert.Equal(140m, plan.Target);

        var unmapped = snap.Rows.Single(r => r.Symbol == "AAA.TO");
        Assert.Null(unmapped.SectorHeat.DiffPp);
    }

    [Fact]
    public async Task Refresh_MarksUnresolvedIdentity()
    {
        var f = await SetUpAsync(needsInput: "ZZZ.TO");
        var snap = await f.Service.RefreshAsync("u");
        Assert.Equal("IdentityUnresolved", snap.Rows.Single(r => r.Symbol == "ZZZ.TO").RowStatus);
    }

    [Fact]
    public async Task Reads_NeverCallMarketData_AndAreIsolatedPerUser()
    {
        var f = await SetUpAsync();
        var empty = await f.Service.GetLatestAsync("u");
        Assert.Null(empty.SnapshotId);
        Assert.Empty(empty.Rows);

        var snap = await f.Service.RefreshAsync("u");
        var marketCalls = f.Market.Calls;
        var scans = (f.Scanner.PersistingCalls, f.Scanner.ReadOnlyCalls);

        var latest = await f.Service.GetLatestAsync("u");
        var detail = await f.Service.GetDetailAsync("u", "bbb.to", null);
        Assert.Equal(snap.SnapshotId, latest.SnapshotId);
        Assert.Equal(JsonSerializer.Serialize(snap.Rows), JsonSerializer.Serialize(latest.Rows));
        // Grid row and drawer row come from the same snapshot record.
        Assert.Equal(JsonSerializer.Serialize(latest.Rows.Single(r => r.Symbol == "BBB.TO")), JsonSerializer.Serialize(detail!.Row));
        Assert.Equal(marketCalls, f.Market.Calls);
        Assert.Equal(scans, (f.Scanner.PersistingCalls, f.Scanner.ReadOnlyCalls));

        Assert.Empty((await f.Service.GetLatestAsync("other")).Rows);
        Assert.Null(await f.Service.GetDetailAsync("other", "BBB.TO", snap.SnapshotId));
        Assert.Null(await f.Service.GetDetailAsync("u", "BBB.TO", snap.SnapshotId!.Value + 999));
    }

    [Fact]
    public async Task ConcurrentRefresh_IsRejected_WhileOneIsRunning()
    {
        var f = await SetUpAsync();
        f.Market.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = f.Service.RefreshAsync("u-concurrent");
        await Task.Delay(50);
        while (!(await f.Service.GetLatestAsync("u-concurrent")).RefreshInFlight) await Task.Delay(10);

        await Assert.ThrowsAsync<OpportunityRefreshInProgressException>(() => f.Service.RefreshAsync("u-concurrent"));
        f.Market.Gate.SetResult();
        await first;
        Assert.False((await f.Service.GetLatestAsync("u-concurrent")).RefreshInFlight);
    }

    [Fact]
    public async Task FailedRefresh_KeepsTheLastSuccessfulSnapshot()
    {
        var f = await SetUpAsync();
        var ok = await f.Service.RefreshAsync("u");

        var after = await With(f, new FailingFinalActions()).RefreshAsync("u");

        Assert.Equal(ok.SnapshotId, after.SnapshotId);
        Assert.Equal("Succeeded", after.Status);
        Assert.Equal("Failed", after.LastAttempt!.Status);
        var state = await f.Db.OpportunityRefreshStates.AsNoTracking().SingleAsync(s => s.UserId == "u");
        Assert.Equal(ok.SnapshotId, state.LastSuccessfulSnapshotId);
    }

    [Fact]
    public async Task Refresh_KeepsOnlyTheLatestFiveSnapshots()
    {
        var f = await SetUpAsync();
        for (var i = 0; i < 7; i++) await f.Service.RefreshAsync("u");
        Assert.Equal(5, await f.Db.OpportunitySnapshots.CountAsync(s => s.UserId == "u"));
    }

    // ── §4.3: a refresh changes only opportunity snapshots/state and Universe fundamentals ──

    private static readonly JsonSerializerOptions StateJson = new() { ReferenceHandler = ReferenceHandler.IgnoreCycles, MaxDepth = 64 };

    private static async Task<Dictionary<string, List<string>>> StateAsync(AppDbContext db)
    {
        var set = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!;
        var state = new Dictionary<string, List<string>>();
        foreach (var et in db.Model.GetEntityTypes().Where(e => !e.IsOwned() && !e.HasSharedClrType))
        {
            var query = (IQueryable<object>)set.MakeGenericMethod(et.ClrType).Invoke(db, null)!;
            var rows = await query.AsNoTracking().ToListAsync();
            if (et.ClrType == typeof(ValueScreenerSnapshot))
                rows = rows.Where(r => ((ValueScreenerSnapshot)r).Origin != nameof(ValueOrigin.Universe)).ToList();
            state[et.ClrType.Name] = rows.Select(r => JsonSerializer.Serialize(r, et.ClrType, StateJson)).Order().ToList();
        }
        return state;
    }

    [Fact]
    public async Task Refresh_ChangesOnlyOpportunityRows_AndUniverseFundamentals()
    {
        var f = await SetUpAsync();
        var before = await StateAsync(f.Db);

        await f.Service.RefreshAsync("u");
        f.Db.ChangeTracker.Clear();
        var after = await StateAsync(f.Db);

        string[] allowed = [nameof(OpportunitySnapshot), nameof(OpportunityRefreshState)];
        foreach (var (table, rows) in before.Where(t => !allowed.Contains(t.Key)))
            Assert.True(rows.SequenceEqual(after[table]), $"{table} changed during an opportunity refresh");
        Assert.Single(after[nameof(OpportunitySnapshot)]);

        // Universe-only symbols with stale/no fundamentals were screened read-only and stored under Universe only.
        var universe = await f.Db.ValueScreenerSnapshots.AsNoTracking().SingleAsync(s => s.Origin == "Universe");
        Assert.Contains("ZZZ.TO", universe.ResultsJson);
        Assert.DoesNotContain("BBB.TO", universe.ResultsJson);
    }

    // ── C1: external cash changes allocation evidence, never intrinsic scores or Sector Heat ──

    [Fact]
    public async Task CashDeposit_LeavesScoresAndSectorHeatIdentical_ButUpdatesPortfolioFit()
    {
        var f = await SetUpAsync();
        var before = await f.Service.RefreshAsync("u");

        f.Db.CashItems.Add(new CashItem { UserId = "u", Amount = 250000m, AccountType = "MARGIN", CashFlowType = "Deposit" });
        await f.Db.SaveChangesAsync();
        var after = await f.Service.RefreshAsync("u");

        foreach (var b in before.Rows)
        {
            var a = after.Rows.Single(r => r.Symbol == b.Symbol);
            Assert.Equal(JsonSerializer.Serialize(b.Swing), JsonSerializer.Serialize(a.Swing));
            Assert.Equal(JsonSerializer.Serialize(b.Strategic), JsonSerializer.Serialize(a.Strategic));
            Assert.Equal(JsonSerializer.Serialize(b.SectorHeat), JsonSerializer.Serialize(a.SectorHeat));
        }
        var fitBefore = before.Rows.Single(r => r.Symbol == "BBB.TO").Fit;
        var fitAfter = after.Rows.Single(r => r.Symbol == "BBB.TO").Fit;
        Assert.NotNull(fitBefore.WeightPct);
        Assert.True(fitAfter.WeightPct < fitBefore.WeightPct, $"weight {fitBefore.WeightPct} → {fitAfter.WeightPct}");
    }
}
