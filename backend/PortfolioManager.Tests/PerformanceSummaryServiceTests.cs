using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>
/// The summary only supplies benchmark returns now, and must start them on the same baseline date as the portfolio card:
/// the prior year-end when a usable one exists, otherwise the first snapshot on/after the cash-ledger start (never a pre-ledger
/// July baseline, and never a silent full-year rebase).
/// </summary>
public sealed class PerformanceSummaryServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_UsesFirstSnapshotOnOrAfterLedgerStart_WhenNoYearEndBaseline()
    {
        var year = DateTime.UtcNow.Year;
        await using var db = CreateDb($"{year}-02-05");
        db.PortfolioValueHistories.AddRange(
            Row($"{year}-01-06", 805803.5387m),   // before the ledger: not comparable
            Row($"{year}-02-05", 776716.4298m),
            Row($"{year}-02-06", 774753.3400m));
        await db.SaveChangesAsync();

        var result = await new PerformanceSummaryService(db, new FakeMarketData()).GetSummaryAsync("user");

        Assert.NotNull(result);
        Assert.False(result!.IsFullYear);
        Assert.Equal($"{year}-02-05", result.BenchmarkStartDate);
    }

    [Fact]
    public async Task GetSummaryAsync_UsesYearEndBaseline_WhenLedgerCoversIt()
    {
        var year = DateTime.UtcNow.Year;
        await using var db = CreateDb($"{year - 1}-12-01");
        db.PortfolioValueHistories.AddRange(
            Row($"{year - 1}-06-15", 500000m),
            Row($"{year - 1}-12-31", 700000m),
            Row($"{year}-01-07", 811995.4929m));
        await db.SaveChangesAsync();

        var result = await new PerformanceSummaryService(db, new FakeMarketData()).GetSummaryAsync("user");

        Assert.NotNull(result);
        Assert.True(result!.IsFullYear);
        Assert.Equal($"{year - 1}-12-31", result.BenchmarkStartDate);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsNull_WhenNoComparableBaselineExists()
    {
        var year = DateTime.UtcNow.Year;
        await using var db = CreateDb($"{year}-02-05");
        db.PortfolioValueHistories.AddRange(Row($"{year}-01-06", 805803.5387m), Row($"{year}-01-07", 811995.4929m));
        await db.SaveChangesAsync();

        Assert.Null(await new PerformanceSummaryService(db, new FakeMarketData()).GetSummaryAsync("user"));
    }

    private static AppDbContext CreateDb(string ledgerStart)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.CashLedgerSettings.Add(new CashLedgerSettings { Id = 1, LedgerStartDate = DateTime.Parse(ledgerStart) });
        db.SaveChanges();
        return db;
    }

    private static PortfolioValueHistory Row(string recordedDate, decimal totalValue) => new()
    {
        RecordedAt = DateTime.SpecifyKind(DateTime.Parse(recordedDate).AddHours(20).AddMinutes(30), DateTimeKind.Utc),
        RecordedDate = recordedDate,
        TotalValue = totalValue,
        StocksValue = totalValue,
        CashValue = 0m,
        OptionsValue = 0m,
    };

    private sealed class FakeMarketData : IMarketDataProvider
    {
        public Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<StockQuote?>(null);

        public Task<IReadOnlyList<MarketDailyClose>?> GetDailyClosesAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MarketDailyClose>?>(null);

        public Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, StockQuote>());

        public Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult(("", ""));

        public Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);

        public Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, decimal>());

        public Task<FundamentalsSnapshot?> GetFundamentalsAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<FundamentalsSnapshot?>(null);

        public Task<Dictionary<string, DateTime>> GetEarningsDatesAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, DateTime>());

        public Task<Dictionary<string, decimal>> GetHistoricalClosingPricesAsync(string dateStr, IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, decimal>());
    }
}
