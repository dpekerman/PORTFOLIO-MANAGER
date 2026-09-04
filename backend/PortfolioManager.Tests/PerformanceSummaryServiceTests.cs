using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>
/// Regression tests guarding against the same "fallback to the wrong baseline" bug class
/// already fixed in DashboardService: GetSummaryAsync used to fall back to the earliest
/// available history row when nothing existed on/before Jan 1, silently mislabeling a
/// partial-year return as "Portfolio YTD" (found live in production — history only went
/// back to July 2026, so "Start of year" was showing July instead of no data at all).
/// </summary>
public sealed class PerformanceSummaryServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ReturnsNull_WhenNoSnapshotExistsAtOrBeforeJanFirst()
    {
        // History exists (>= 2 rows) but none of it reaches back to Jan 1 of the current year —
        // this is exactly the production scenario that produced a bogus YTD baseline.
        var year = DateTime.UtcNow.Year;
        await using var db = CreateDb();
        db.PortfolioValueHistories.AddRange(
            Row($"{year}-07-06", 805803.5387m),
            Row($"{year}-07-07", 811995.4929m));
        await db.SaveChangesAsync();

        var service = new PerformanceSummaryService(db, new FakeMarketData());

        var result = await service.GetSummaryAsync("user");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSummaryAsync_UsesGenuineYearStartBaseline_NotEarliestRow()
    {
        var year = DateTime.UtcNow.Year;
        await using var db = CreateDb();
        db.PortfolioValueHistories.AddRange(
            Row($"{year - 1}-06-15", 500000m),  // earliest row overall — must NOT be picked
            Row($"{year - 1}-12-31", 700000m),  // genuine prior year-end close — expected baseline
            Row($"{year}-07-07", 811995.4929m)); // latest
        db.PortfolioSnapshots.Add(new PortfolioSnapshot
        {
            UserId = "user",
            SnapshotJson = JsonSerializer.Serialize(new List<PortfolioSummaryDto>()),
        });
        await db.SaveChangesAsync();

        var service = new PerformanceSummaryService(db, new FakeMarketData());

        var result = await service.GetSummaryAsync("user");

        Assert.NotNull(result);
        Assert.Equal($"{year - 1}-12-31", result!.PortfolioStartDate);
        Assert.Equal(700000m, result.PortfolioStartValue);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

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
