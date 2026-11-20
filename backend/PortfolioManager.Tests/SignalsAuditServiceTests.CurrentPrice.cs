using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Tests;

/// <summary>PM-019: Current Price comes from cached quote evidence only and never from the signal or horizon price.</summary>
public sealed partial class SignalsAuditServiceTests
{
    private sealed class FakeWatchlistSnapshots(params (string Symbol, StockQuote? Quote)[] quotes) : IWatchlistSnapshotService
    {
        public int Reads { get; private set; }
        public Task SaveAsync(string userId, IReadOnlyList<WatchlistSummaryDto> data, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WatchlistSummaryDto>?> GetLatestAsync(string userId, CancellationToken ct = default)
        {
            Reads++;
            IReadOnlyList<WatchlistSummaryDto>? list = quotes
                .Select((q, i) => new WatchlistSummaryDto(new WatchlistItemDto(i + 1, q.Symbol, "", DateTime.UtcNow), q.Quote)).ToList();
            return Task.FromResult(list);
        }
    }

    private static SignalsAuditService ServiceWithQuotes(Harness h, FakeWatchlistSnapshots snapshots) => new(
        h.Db, h.Scores, h.FinalActions, new FakeResolver(), h.Market, new MemoryCache(new MemoryCacheOptions()), new FakeAudit(),
        NullLogger<SignalsAuditService>.Instance, null, snapshots);

    private static StockQuote Quote(string symbol, decimal price, long? sourceSecondsAgo, string state = "CLOSED", string? currency = "USD") => new()
    {
        Symbol = symbol,
        CurrentPrice = price,
        MarketState = state,
        Currency = currency,
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        SourcePriceTimestamp = sourceSecondsAgo is null ? null : DateTimeOffset.UtcNow.ToUnixTimeSeconds() - sourceSecondsAgo,
    };

    [Fact]
    public async Task CurrentPrice_UsesCachedQuote_NeverSignalPrice_AndLeavesSignalPriceUnchanged()
    {
        var h = new Harness([]);
        var d = SignalsAuditService_TorontoToday();
        h.AddEvent("HAS", d, "Core", SignalMyActions.Pending, price: 100m);
        h.AddEvent("NONE", d.AddDays(-1), "Core", SignalMyActions.Pending, price: 55m);
        h.AddEvent("ZERO", d.AddDays(-2), "Core", SignalMyActions.Pending, price: 77m);
        h.AddEvent("OLD", d.AddDays(-3), "Core", SignalMyActions.Pending, price: 88m);
        var snapshots = new FakeWatchlistSnapshots(
            ("HAS", Quote("HAS", 123.45m, sourceSecondsAgo: 30 * 24 * 3600, currency: "CAD")),
            ("ZERO", Quote("ZERO", 0m, 60)),
            ("OLD", Quote("OLD", 90m, null)));
        var svc = ServiceWithQuotes(h, snapshots);

        var rows = (await svc.QueryAsync(User, new SignalsAuditQuery(PageSize: 50))).Items.ToDictionary(r => r.Symbol);

        Assert.Equal(123.45m, rows["HAS"].CurrentPrice);
        Assert.Equal("CAD", rows["HAS"].CurrentPriceCurrency);
        Assert.Equal(PriceBases.Stale, rows["HAS"].CurrentPriceStatus);
        Assert.NotNull(rows["HAS"].CurrentPriceAsOfUtc);
        Assert.Equal(100m, rows["HAS"].SignalPrice);

        Assert.Null(rows["NONE"].CurrentPrice);
        Assert.Equal(PriceBases.Unavailable, rows["NONE"].CurrentPriceStatus);
        Assert.Equal(55m, rows["NONE"].SignalPrice);

        Assert.Null(rows["ZERO"].CurrentPrice);
        Assert.Equal(PriceBases.Unavailable, rows["ZERO"].CurrentPriceStatus);

        // A quote without a provider timestamp cannot prove freshness, but it is still not the signal price.
        Assert.Equal(90m, rows["OLD"].CurrentPrice);
        Assert.Equal(PriceBases.Stale, rows["OLD"].CurrentPriceStatus);
    }

    [Fact]
    public async Task CurrentPrice_WithoutSnapshotServices_IsUnavailable()
    {
        var h = new Harness([]);
        h.AddEvent("AAA", SignalsAuditService_TorontoToday(), "Core", SignalMyActions.Pending, price: 100m);

        var row = Assert.Single((await h.Service.QueryAsync(User, new SignalsAuditQuery())).Items);

        Assert.Null(row.CurrentPrice);
        Assert.Equal(PriceBases.Unavailable, row.CurrentPriceStatus);
        Assert.Equal(100m, row.SignalPrice);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task CurrentPrice_SortsTheWholeResultSet_WithUnavailableAlwaysLast(string dir)
    {
        var h = new Harness([]);
        var d = SignalsAuditService_TorontoToday();
        var prices = new (string Symbol, decimal? Price)[] { ("A", 30m), ("B", null), ("C", 10m), ("D", 20m), ("E", null) };
        for (var i = 0; i < prices.Length; i++)
            h.AddEvent(prices[i].Symbol, d.AddDays(-i), "Core", SignalMyActions.Pending, price: 1000m - i);
        var snapshots = new FakeWatchlistSnapshots(prices.Where(p => p.Price is not null)
            .Select(p => (p.Symbol, (StockQuote?)Quote(p.Symbol, p.Price!.Value, null))).ToArray());
        var svc = ServiceWithQuotes(h, snapshots);

        // Page size 2 forces the ranking to happen before paging.
        var page1 = await svc.QueryAsync(User, new SignalsAuditQuery(SortBy: "currentPrice", SortDir: dir, PageSize: 2, Page: 1));
        var page2 = await svc.QueryAsync(User, new SignalsAuditQuery(SortBy: "currentPrice", SortDir: dir, PageSize: 2, Page: 2));
        var page3 = await svc.QueryAsync(User, new SignalsAuditQuery(SortBy: "currentPrice", SortDir: dir, PageSize: 2, Page: 3));

        var ordered = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(r => r.CurrentPrice).ToList();
        Assert.Equal(dir == "asc" ? [10m, 20m, 30m, null, null] : [30m, 20m, 10m, null, null], ordered);
        Assert.False(page1.Partial);
    }

    [Fact]
    public async Task CurrentPrice_ReadsSnapshotsOncePerQuery_NotPerRow()
    {
        var h = new Harness([]);
        var d = SignalsAuditService_TorontoToday();
        for (var i = 0; i < 6; i++) h.AddEvent($"S{i}", d.AddDays(-i), "Core", SignalMyActions.Pending);
        var snapshots = new FakeWatchlistSnapshots(Enumerable.Range(0, 6).Select(i => ($"S{i}", (StockQuote?)Quote($"S{i}", 10 + i, null))).ToArray());
        var svc = ServiceWithQuotes(h, snapshots);

        await svc.QueryAsync(User, new SignalsAuditQuery(PageSize: 50));

        Assert.Equal(1, snapshots.Reads);
    }
}
