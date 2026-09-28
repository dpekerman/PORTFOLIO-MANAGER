using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class DashboardEodSummaryServiceTests
{
    [Fact]
    public async Task GetLatestAsync_UsesOneLatestSessionAndCanonicalizesOwnership()
    {
        await using var db = CreateDb();
        db.DailySignals.AddRange(
            Signal("bank.to", "2026-08-31", 31m, scannedAt: new DateTime(2026, 9, 1, 1, 0, 0)),
            Signal("BANK.TO", "2026-08-31", 30m, signalType: "Confirmed", scannedAt: new DateTime(2026, 9, 1, 2, 0, 0)),
            Signal(" BANK.TO ", "2026-08-31", 29m, signalType: "EarlyWarning", scannedAt: new DateTime(2026, 9, 1, 3, 0, 0)),
            Signal("ATD.TO", "2026-08-31", 29.2m),
            Signal("OLD.TO", "2026-08-28", 22m, signalState: "Active"));
        db.PortfolioItems.Add(new PortfolioItem { Symbol = "ATD.TO" });
        db.WatchlistItems.Add(new WatchlistItem { UserId = "user", Symbol = "ATD.TO" });
        await db.SaveChangesAsync();

        var service = new DashboardEodSummaryService(db, new StubPortfolioActionsService([AtdAvoidAction]));

        var result = await service.GetLatestAsync("user");

        Assert.Equal("2026-08-31", result.TradingDate);
        Assert.Equal(4, result.RawRecordCount);
        Assert.Equal(2, result.UniqueTickerCount);
        Assert.Equal(2, result.Rows.Count);
        var bank = Assert.Single(result.Rows, row => row.Symbol == "BANK.TO");
        Assert.Equal(29m, bank.Rsi);
        var atd = Assert.Single(result.Rows, row => row.Symbol == "ATD.TO");
        Assert.Equal("Portfolio", atd.Ownership);
        Assert.Equal("AVOID", atd.Action);
        Assert.Equal("Resolved", atd.ActionResolutionStatus);
        Assert.DoesNotContain(result.Rows, row => row.Symbol == "OLD.TO");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DailySignal Signal(
        string symbol,
        string tradingDate,
        decimal rsi,
        string signalType = "EodConfirm",
        string signalState = "Active",
        DateTime? scannedAt = null) => new()
        {
            Symbol = symbol,
            CompanyName = symbol,
            ScanType = "Oversold",
            SignalType = signalType,
            SignalState = signalState,
            Rsi = rsi,
            SignalDate = tradingDate,
            TradingDate = tradingDate,
            RecordedAt = new DateTime(2026, 9, 1),
            ScannedAt = scannedAt,
        };

    private static readonly PortfolioActionDto AtdAvoidAction = new(
        "ATD.TO", "Alimentation Couche-Tard", "Strategic", "Oversold", 29.2m, "Bull Turn", "", "", "under",
        "AVOID", "danger", "REQUIRED", true, true, "NONE", "NONE", 0, 0, 0m, 0m, 0m, 0m, null, null, []);

    private sealed class StubPortfolioActionsService(IReadOnlyList<PortfolioActionDto> actions) : IPortfolioActionsService
    {
        public Task<IReadOnlyList<PortfolioActionDto>> GetActionsAsync(string userId, CancellationToken ct = default)
            => Task.FromResult(actions);
    }
}