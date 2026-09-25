using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using Xunit;

namespace PortfolioManager.Tests;

/// <summary>
/// Regression coverage for the Priority Candidates fixes: ownership exclusion must use the open
/// position only (not any portfolio row), Portfolio Need must resolve a real sector for non-owned
/// watchlist candidates instead of a flat neutral default, and Fundamental Quality must scale the
/// true 0-10 Value Screener score into 0-25 (not divide by 4).
/// </summary>
public class PortfolioActionScoreServiceTests
{
    private const string UserId = "user1";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetScoresAsync_ExcludesOpenPosition_ButNotClosedPosition()
    {
        await using var db = CreateDb();
        db.WatchlistItems.AddRange(
            new WatchlistItem { Symbol = "OPENPOS", Role = "Strategic", WatchlistTier = "Active", UserId = UserId },
            new WatchlistItem { Symbol = "CLOSEDPOS", Role = "Strategic", WatchlistTier = "Active", UserId = UserId });
        db.PortfolioSnapshots.Add(new PortfolioSnapshot
        {
            UserId = UserId,
            SnapshotJson = JsonSerializer.Serialize(new List<PortfolioSummaryDto>
            {
                new(Item(1, "OPENPOS", transactionType: null), Quote("OPENPOS", 100m)),
                new(Item(2, "CLOSEDPOS", transactionType: "CLOSE"), Quote("CLOSEDPOS", 100m)),
            }, JsonOpts),
        });
        await db.SaveChangesAsync();

        var scores = await new PortfolioActionScoreService(db).GetScoresAsync(UserId);

        Assert.DoesNotContain(scores, s => s.Symbol == "OPENPOS");
        Assert.Contains(scores, s => s.Symbol == "CLOSEDPOS");
    }

    [Fact]
    public async Task GetScoresAsync_PortfolioNeed_ResolvesRealSectorForNonOwnedCandidate()
    {
        await using var db = CreateDb();
        db.WatchlistItems.Add(new WatchlistItem { Symbol = "NEEDTEST", Role = "Strategic", WatchlistTier = "Active", UserId = UserId });
        db.AllocationSectorTargets.Add(new AllocationSectorTarget { Sector = "Technology", TargetPct = 15m });
        db.ValueScreenerSnapshots.Add(new ValueScreenerSnapshot
        {
            Origin = "Watchlist",
            RunAt = DateTime.UtcNow,
            ResultsJson = JsonSerializer.Serialize(new List<ValueScreenerResult>
            {
                new() { Symbol = "NEEDTEST", Sector = "Technology", Score = 4m },
            }, JsonOpts),
        });
        // No open portfolio holdings at all -> sector actual is 0% vs 15% target -> delta -15 -> underweight -> Need 30.
        // Before the fix this always returned the flat neutral 15, because Need only looked up sector
        // via an existing OPEN holding of the same symbol.
        await db.SaveChangesAsync();

        var scores = await new PortfolioActionScoreService(db).GetScoresAsync(UserId);

        var row = Assert.Single(scores);
        Assert.Equal(30m, row.PortfolioNeedScore);
    }

    [Fact]
    public async Task GetScoresAsync_FundamentalQuality_ScalesZeroToTenScoreIntoZeroToTwentyFive()
    {
        await using var db = CreateDb();
        db.WatchlistItems.Add(new WatchlistItem { Symbol = "FUNDTEST", Role = "Strategic", WatchlistTier = "Active", UserId = UserId });
        db.ValueScreenerSnapshots.Add(new ValueScreenerSnapshot
        {
            Origin = "Watchlist",
            RunAt = DateTime.UtcNow,
            ResultsJson = JsonSerializer.Serialize(new List<ValueScreenerResult>
            {
                new() { Symbol = "FUNDTEST", Sector = "", Score = 10m },
            }, JsonOpts),
        });
        await db.SaveChangesAsync();

        var scores = await new PortfolioActionScoreService(db).GetScoresAsync(UserId);

        // 10 * 2.5 = 25 (was 10 / 4 = 2.5 before the fix).
        var row = Assert.Single(scores);
        Assert.Equal(25.0m, row.FundamentalScore);
    }

    [Fact]
    public async Task GetScoresAsync_FundamentalQuality_MidRangeScoreScalesProportionally()
    {
        await using var db = CreateDb();
        db.WatchlistItems.Add(new WatchlistItem { Symbol = "FUNDMID", Role = "Strategic", WatchlistTier = "Active", UserId = UserId });
        db.ValueScreenerSnapshots.Add(new ValueScreenerSnapshot
        {
            Origin = "Watchlist",
            RunAt = DateTime.UtcNow,
            ResultsJson = JsonSerializer.Serialize(new List<ValueScreenerResult>
            {
                new() { Symbol = "FUNDMID", Sector = "", Score = 4m },
            }, JsonOpts),
        });
        await db.SaveChangesAsync();

        var scores = await new PortfolioActionScoreService(db).GetScoresAsync(UserId);

        // 4 * 2.5 = 10 (was 4 / 4 = 1.0 before the fix).
        var row = Assert.Single(scores);
        Assert.Equal(10.0m, row.FundamentalScore);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static PortfolioItemDto Item(int id, string symbol, string? transactionType) => new(
        Id: id, Symbol: symbol, CompanyName: symbol, Shares: 10m, AverageCostBasis: 100m,
        Sector: "Technology", Industry: "", SectorIsOverridden: false, IsManual: false,
        ManualMarketValue: null, AddedAt: DateTime.UtcNow, TransactionType: transactionType,
        HoldingRole: "Strategic");

    private static StockQuote Quote(string symbol, decimal price) => new() { Symbol = symbol, CurrentPrice = price };
}
