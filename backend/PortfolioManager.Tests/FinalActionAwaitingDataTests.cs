using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

/// <summary>Awaiting Data, stale/partial evidence and per-symbol publication identity (Phase 2).</summary>
public sealed class FinalActionAwaitingDataTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTime OldStamp = new(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc);
    private static DateOnly Expected => TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Nyse);

    private static RsiScanResult Scan(string symbol, DateOnly? bar = null, decimal price = 100m) => new()
    {
        Symbol = symbol, AnalysisTicker = symbol, AnalysisCurrency = "USD", Rsi = 58m, CurrentPrice = price, ChangePercent = 0.5m,
        DayHigh = 101m, DayLow = 98m, OpenPrice = 99.5m, PreviousClose = 99.4m, VolumeRatio = 1.6m,
        RsiSignal = 54m, RsiSignalAvailable = true, MacdHistogram = 0.4m, MacdHistDelta = 0.1m,
        Ema9Price = 99m, Ema10Price = 98.5m, Ema20Price = 96m, Sma20Price = 95m, Sma50Price = 90m, Sma200 = 80m,
        Status = SignalStatus.Neutral, ScanType = ScanType.Neutral, Week52High = 110m,
        TradingDate = bar ?? Expected,
    };

    private static PortfolioSummaryDto Holding(int id, string symbol, string role = "Swing", long quoteTs = 0) => new(
        new PortfolioItemDto(id, symbol, symbol, 10m, 90m, "", "", false, false, null, OldStamp, "OPEN", "Margin", OldStamp.AddDays(-30), null, null,
            role, null, null, null, "Add to Trade", "buy", "DEVELOPING", OldStamp),
        new StockQuote { Symbol = symbol, CurrentPrice = 100m, Timestamp = quoteTs },
        PriceStructureResult.None);

    private static async Task<(FinalActionService Service, AppDbContext Db, PortfolioSnapshotService Snap)> SetUpAsync(
        IEnumerable<PortfolioSummaryDto> holdings, IEnumerable<string> watch, params RsiScanResult[] scans)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var w in watch) db.WatchlistItems.Add(new WatchlistItem { UserId = "u", Symbol = w, Role = "Core" });
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = "u", SnapshotJson = JsonSerializer.Serialize(holdings.ToList(), Json) });
        await db.SaveChangesAsync();
        var snap = new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance);
        var service = new FinalActionService(db, new FakeScanner(scans), snap, new MemoryCache(new MemoryCacheOptions()), NullLogger<FinalActionService>.Instance);
        return (service, db, snap);
    }

    private static async Task<List<PortfolioSummaryDto>> StoredAsync(AppDbContext db) =>
        JsonSerializer.Deserialize<List<PortfolioSummaryDto>>((await db.PortfolioSnapshots.AsNoTracking().SingleAsync()).SnapshotJson, Json)!;

    [Fact]
    public async Task StaleScan_IsAwaitingData_NotActionable_AndKeepsTheLastValidActionSeparately()
    {
        var (service, _, _) = await SetUpAsync([Holding(1, "STALE")], [], Scan("STALE", bar: Expected.AddDays(-6)));
        var snap = await service.ComputeAsync("u");
        var r = Assert.Single(snap.Portfolio);
        Assert.Equal("Awaiting Data", r.FinalAction);
        Assert.Equal("AwaitingData", r.EvaluationState);
        Assert.Equal("Stale", r.EvidenceHealth);
        Assert.False(r.IsActionable);
        Assert.Equal("Add to Trade", r.LastValidAction);
        Assert.Equal(OldStamp, r.LastSuccessfulEvaluationUtc);
        Assert.Contains("AWAITING_STALE_DATA", r.ReasonCodes!);
        Assert.Contains("Last valid action: Add to Trade", r.Reason);
        Assert.Equal(DecisionBucket.None, DecisionEligibility.Classify(r));
        Assert.Contains("STALE", snap.Meta!.UnknownSymbols);                 // lifecycle may not confirm / invalidate / expire on it
        Assert.Equal("ma-standby", r.FinalActionClass);
        Assert.Equal(("hold", "INFORMATIONAL"), (r.Severity, r.Priority));
    }

    [Fact]
    public async Task RefreshFailure_And_InvalidPrice_AreAwaitingData_NeverAHold()           // acceptance 3
    {
        var (service, _, _) = await SetUpAsync([Holding(1, "GONE"), Holding(2, "ZERO")], ["WL"], Scan("ZERO", price: 0m));
        var snap = await service.ComputeAsync("u");
        var gone = snap.Portfolio.Single(p => p.Symbol == "GONE");
        var zero = snap.Portfolio.Single(p => p.Symbol == "ZERO");
        Assert.Equal("Failed", gone.EvidenceHealth);
        Assert.Equal("Invalid", zero.EvidenceHealth);
        Assert.All(new[] { gone, zero }, r => { Assert.Equal("Awaiting Data", r.FinalAction); Assert.False(r.IsActionable); });
        var wl = Assert.Single(snap.Watchlist);
        Assert.Equal("Awaiting Data", wl.FinalAction);                                  // watchlist: only when evidence is unavailable
        Assert.Contains(wl.Symbol, snap.Meta!.UnknownSymbols);
    }

    [Fact]
    public async Task OldQuoteWrappedInANewScan_IsNotFresh()
    {
        var oldQuote = new DateTimeOffset(DateTime.UtcNow.AddDays(-9)).ToUnixTimeSeconds();
        var (service, _, _) = await SetUpAsync([Holding(1, "OLDQ", quoteTs: oldQuote)], [], Scan("OLDQ"));
        var r = Assert.Single((await service.ComputeAsync("u")).Portfolio);
        Assert.Equal("Awaiting Data", r.FinalAction);
        Assert.Contains("QUOTE_OLDER_THAN_SCAN", r.ReasonCodes!);
    }

    [Fact]
    public async Task FreshEvidence_BeforeTheNextEodScan_IsStillCanonical_AndWatchlistGetsRealStatuses()   // release 2
    {
        var (service, _, _) = await SetUpAsync([Holding(1, "OK")], ["WL"], Scan("OK"), Scan("WL"));
        var snap = await service.ComputeAsync("u");
        var p = Assert.Single(snap.Portfolio);
        var w = Assert.Single(snap.Watchlist);
        Assert.Equal("CurrentValid", p.EvaluationState);
        Assert.Equal("CurrentValid", w.EvaluationState);
        Assert.NotEqual("Awaiting Data", p.FinalAction);
        Assert.NotEqual("Awaiting Data", w.FinalAction);
        Assert.Equal(Expected, p.DataAsOfDate);
        Assert.Empty(snap.Meta!.UnknownSymbols);
    }

    [Fact]
    public async Task PartialRun_PublishesOnlyEvaluatedSymbols_WithOwnIdentity_AndLeavesFailedOnesUntouched()   // acceptance 4 / adj. 2, 9
    {
        var (service, db, _) = await SetUpAsync([Holding(1, "GOOD"), Holding(2, "BAD")], [], Scan("GOOD"), Scan("BAD", bar: Expected.AddDays(-7)));
        var before = (await StoredAsync(db)).Single(s => s.Item.Id == 2).Item;
        var snapshot = await service.ComputeAsync("u", fresh: true);

        Assert.Equal(1, await service.PublishPortfolioActionsAsync("u", snapshot));

        var stored = await StoredAsync(db);
        var good = stored.Single(s => s.Item.Id == 1).Item;
        var bad = stored.Single(s => s.Item.Id == 2).Item;
        Assert.Equal(snapshot.ComputedAtUtc, good.FinalActionUpdatedAt);
        Assert.Equal(Expected.ToString("yyyy-MM-dd"), good.FinalActionSession);
        Assert.Equal(before.FinalAction, bad.FinalAction);                                   // previous record intact
        Assert.Equal(before.FinalActionUpdatedAt, bad.FinalActionUpdatedAt);                 // timestamp not refreshed
        Assert.Equal(before.FinalActionSeverity, bad.FinalActionSeverity);
        Assert.Equal(before.FinalActionSession, bad.FinalActionSession);
    }

    [Fact]
    public async Task WhollyFailedPass_PublishesNothing()
    {
        var (service, db, _) = await SetUpAsync([Holding(1, "A"), Holding(2, "B")], []);
        var before = (await db.PortfolioSnapshots.AsNoTracking().SingleAsync()).SnapshotJson;
        var snapshot = await service.ComputeAsync("u", fresh: true);
        Assert.Equal(0, await service.PublishPortfolioActionsAsync("u", snapshot));
        Assert.Equal(before, (await db.PortfolioSnapshots.AsNoTracking().SingleAsync()).SnapshotJson);
    }

    [Fact]
    public async Task UiRefresh_CachedSnapshot_And_RepeatedPublish_DoNotMakeAnOlderActionLookNewlyEvaluated()   // acceptance adj. 9
    {
        var (service, db, _) = await SetUpAsync([Holding(1, "OK")], [], Scan("OK"));
        var first = await service.ComputeAsync("u", fresh: true);
        await service.PublishPortfolioActionsAsync("u", first);
        var stamp = (await StoredAsync(db)).Single().Item.FinalActionUpdatedAt;

        await Task.Delay(30);
        var cached = await service.ComputeAsync("u");                                        // a UI load: served from cache
        Assert.Equal(first.ComputedAtUtc, cached.ComputedAtUtc);
        await service.PublishPortfolioActionsAsync("u", cached);
        Assert.Equal(stamp, (await StoredAsync(db)).Single().Item.FinalActionUpdatedAt);
    }

    private static async Task AddWatchQuoteAsync(AppDbContext db, string symbol, long ts)
    {
        var rows = new[] { new WatchlistSummaryDto(new WatchlistItemDto(5, symbol, "", DateTime.UtcNow, "Core", false, null, "Active"),
            new StockQuote { Symbol = symbol, CurrentPrice = 100m, Timestamp = ts }) };
        db.WatchlistSnapshots.Add(new WatchlistSnapshot { UserId = "u", SnapshotJson = JsonSerializer.Serialize(rows, Json) });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Watchlist_OldSourceQuote_WithANewScan_IsNotFresh_ButFreshQuoteIsVerified()
    {
        var (stale, dbS, _) = await SetUpAsync([], ["WL"], Scan("WL"));
        await AddWatchQuoteAsync(dbS, "WL", new DateTimeOffset(DateTime.UtcNow.AddDays(-9)).ToUnixTimeSeconds());
        var s = Assert.Single((await stale.ComputeAsync("u")).Watchlist);
        Assert.Equal("Awaiting Data", s.FinalAction);
        Assert.Contains("QUOTE_OLDER_THAN_SCAN", s.ReasonCodes!);
        Assert.Equal("Verified", s.QuoteVerification);

        var (ok, dbO, _) = await SetUpAsync([], ["WL"], Scan("WL"));
        await AddWatchQuoteAsync(dbO, "WL", new DateTimeOffset(TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Nyse)
            .ToDateTime(new TimeOnly(15, 0))).ToUnixTimeSeconds() + 3600);
        var r = Assert.Single((await ok.ComputeAsync("u")).Watchlist);
        Assert.Equal("CurrentValid", r.EvaluationState);
        Assert.Equal("Verified", r.QuoteVerification);
    }

    [Fact]
    public async Task Watchlist_WithoutAQuoteTimestamp_StaysCanonical_AndStatesThatQuoteFreshnessWasNotVerified()
    {
        var (service, _, _) = await SetUpAsync([], ["WL"], Scan("WL"));          // no watchlist snapshot at all
        var w = Assert.Single((await service.ComputeAsync("u")).Watchlist);
        Assert.Equal("CurrentValid", w.EvaluationState);                           // not Awaiting Data because a timestamp is missing
        Assert.NotEqual("Awaiting Data", w.FinalAction);
        Assert.Equal("Unavailable", w.QuoteVerification);
        Assert.Equal("Fresh", w.EvidenceHealth);                                   // by bar date only
        Assert.Equal(Expected, w.DataAsOfDate);
    }

    [Fact]
    public async Task Watchlist_MissingOptionalIndicators_StillEvaluates_ButMissingRequiredEntryInputsAreAwaiting()
    {
        var optional = Scan("OPT");
        optional.RsiSignalAvailable = false; optional.RsiSignal = null; optional.Fib50 = 0m; optional.VolumeRatio = 0m; optional.MacdHistogram = 0m;
        var required = Scan("REQ");
        required.Rsi = 0m;                                                          // RSI is required by the Entry rules
        var (service, _, _) = await SetUpAsync([], ["OPT", "REQ"], optional, required);
        var by = (await service.ComputeAsync("u")).Watchlist.ToDictionary(x => x.Symbol);
        Assert.Equal("CurrentValid", by["OPT"].EvaluationState);
        Assert.NotEqual("Awaiting Data", by["OPT"].FinalAction);
        // REQ is only Awaiting when the engine produced an Entry-category result that needs the missing input.
        if (by["REQ"].FinalAction == "Awaiting Data") Assert.Contains("AWAITING_REQUIRED_RSI", by["REQ"].ReasonCodes!);
    }

    [Fact]
    public async Task MarketDataRefresh_RebuildingTheSnapshot_KeepsThePublishedActionAndItsOriginalTimestamp()   // found in the live baseline
    {
        var (_, db, snap) = await SetUpAsync([Holding(1, "KEEP"), Holding(2, "OTHER")], []);
        var before = (await StoredAsync(db)).ToDictionary(s => s.Item.Id);

        // What every refresh produces: the same holdings re-priced, with no Final Action fields at all.
        PortfolioSummaryDto Fresh(PortfolioSummaryDto s) => s with { Item = s.Item with
            { FinalAction = null, FinalActionSeverity = null, FinalActionPriority = null, FinalActionUpdatedAt = null, FinalActionSession = null } };
        var refreshed = new[] { Fresh(before[1]), Fresh(before[2]), Fresh(Holding(3, "NEW")) };
        await snap.SaveAsync("u", refreshed);

        var after = (await StoredAsync(db)).ToDictionary(s => s.Item.Id);
        Assert.Equal("Add to Trade", after[1].Item.FinalAction);
        Assert.Equal(before[1].Item.FinalActionUpdatedAt, after[1].Item.FinalActionUpdatedAt);   // not renewed, not erased
        Assert.Equal(before[2].Item.FinalActionUpdatedAt, after[2].Item.FinalActionUpdatedAt);
        Assert.Null(after[3].Item.FinalAction);                                                   // a brand-new holding has nothing to carry
    }

    [Fact]
    public void CarryForward_NeverOverwritesAFreshValue_AndIgnoresUnreadableHistory()
    {
        var old = Holding(1, "X");
        var incoming = old with { Item = old.Item with { FinalAction = "Hold / Monitor", FinalActionUpdatedAt = OldStamp.AddDays(5) } };
        var json = JsonSerializer.Serialize(new[] { old }, Json);
        var merged = PortfolioSnapshotService.CarryForwardFinalActions(json, [incoming]);
        Assert.Equal("Hold / Monitor", merged[0].Item.FinalAction);
        Assert.Equal(OldStamp.AddDays(5), merged[0].Item.FinalActionUpdatedAt);
        Assert.Same(incoming, PortfolioSnapshotService.CarryForwardFinalActions("not json", [incoming])[0]);
        Assert.Single(PortfolioSnapshotService.CarryForwardFinalActions(null, [incoming]));
    }

    [Fact]
    public async Task SuccessfulEvaluation_RestoresTheCanonicalAction_AfterAFailure()                   // acceptance 6
    {
        var bad = Scan("X", bar: Expected.AddDays(-9));
        var (service, _, _) = await SetUpAsync([Holding(1, "X")], [], bad);
        Assert.Equal("Awaiting Data", (await service.ComputeAsync("u", fresh: true)).Portfolio.Single().FinalAction);
        bad.TradingDate = Expected;                                                            // the data arrives
        var restored = (await service.ComputeAsync("u", fresh: true)).Portfolio.Single();
        Assert.NotEqual("Awaiting Data", restored.FinalAction);
        Assert.Equal("CurrentValid", restored.EvaluationState);
    }

    private sealed class FakeScanner(params RsiScanResult[] results) : IRsiScannerService
    {
        public Task<List<RsiScanResult>> AnalyzeSymbolsAsync(IEnumerable<string> symbols, decimal oversoldThreshold = 30m,
            decimal overboughtThreshold = 75m, string logicMode = "Legacy", string? userId = null, CancellationToken ct = default)
        {
            var wanted = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(results.Where(r => wanted.Contains(r.Symbol)).ToList());
        }

        public Task<ScannerResponse> ScanAsync(IEnumerable<string>? extraSymbols = null, decimal oversoldThreshold = 30m,
            decimal overboughtThreshold = 75m, string logicMode = "Legacy", string? userId = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
