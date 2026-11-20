using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class Pm029StagedSignalPreviewTests
{
    [Fact]
    public async Task PreviewMatchesPersistedTransitionsWithoutChangingRelationalState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var today = TodayEt();
        db.StagedSignals.AddRange(
            Active("SAME", ScanType.Oversold, today.AddDays(-2), today, 25m, 27m, 100m, 90m),
            Active("NEXT", ScanType.Oversold, today.AddDays(-2), today.AddDays(-1), 26m, 28m, 95m, 98m),
            Active("OVER", ScanType.Overbought, today.AddDays(-2), today, 75m, 78m, 100m, high: 120m),
            Active("EXPIRED", ScanType.Oversold, today.AddDays(-8), today.AddDays(-1), 24m, 25m, 90m, 85m),
            Active("CANCELLED", ScanType.Oversold, today.AddDays(-2), today, 25m, 27m, 100m, 90m, active: false));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var inputs = new[]
        {
            Scan("NEW", ScanType.Oversold, 28m, 100m, 101m, 98m, 2m),
            Scan("SAME", ScanType.Oversold, 29m, 103m, 104m, 85m, 2m),
            Scan("NEXT", ScanType.Oversold, 30m, 102m, 103m, 97m, 2m),
            Scan("OVER", ScanType.Overbought, 75m, 101m, 126m, 99m, 2m),
            Scan("EXPIRED", ScanType.Oversold, 28m, 100m, 101m, 98m, 2m),
            Scan("CANCELLED", ScanType.Oversold, 28m, 100m, 101m, 98m, 2m),
        };
        var before = await StateAsync(db);

        await new StagedSignalService(db, NullLogger<StagedSignalService>.Instance)
            .PreviewAndEnrichAsync(inputs);

        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(before, await StateAsync(db));
        var preview = inputs.ToDictionary(s => s.Symbol, Enrichment);

        var normalInputs = inputs.Select(s => s.Copy()).ToArray();
        await new StagedSignalService(db, NullLogger<StagedSignalService>.Instance)
            .UpsertAndEnrichAsync(normalInputs);
        var normal = normalInputs.ToDictionary(s => s.Symbol, Enrichment);
        Assert.Equal(preview, normal);

        var states = await db.StagedSignals.AsNoTracking().ToListAsync();
        Assert.Equal(7, states.Count);
        Assert.Equal(4m, Assert.Single(states, s => s.Symbol == "SAME").RsiDelta1D);
        var next = Assert.Single(states, s => s.Symbol == "NEXT");
        Assert.Equal(28m, next.PreviousRsi);
        Assert.Equal(95m, next.PreviousPrice);
        Assert.Equal(2m, next.RsiDelta1D);
        Assert.Equal(today, next.LastEvaluatedDate);
        Assert.Equal(85m, Assert.Single(states, s => s.Symbol == "SAME").ExtremeLow);
        Assert.Equal(126m, Assert.Single(states, s => s.Symbol == "OVER").ExtremeHigh);
        Assert.False(Assert.Single(states, s => s.Symbol == "EXPIRED").IsActiveWatch);
        Assert.Equal(2, states.Count(s => s.Symbol == "CANCELLED"));
        Assert.Contains(states, s => s.Symbol == "CANCELLED" && s.IsActiveWatch && s.StagedDate == today);
        Assert.Contains(states, s => s.Symbol == "CANCELLED" && !s.IsActiveWatch);
        Assert.Equal(129m, normalInputs.Single(s => s.Symbol == "OVER").DynamicStopLoss);
        Assert.Equal("BOUNCE_CONFIRMED", normalInputs.Single(s => s.Symbol == "NEXT").ChannelState);
    }

    private static StagedSignal Active(
        string symbol,
        ScanType scanType,
        DateOnly stagedDate,
        DateOnly lastEvaluatedDate,
        decimal previousRsi,
        decimal currentRsi,
        decimal currentPrice,
        decimal? low = null,
        decimal? high = null,
        bool active = true) => new()
    {
        Symbol = symbol,
        ScanType = scanType.ToString(),
        BasePrice = currentPrice,
        BaseRsi = currentRsi,
        BaseHigh = high ?? currentPrice,
        BaseLow = low ?? currentPrice,
        PreviousPrice = currentPrice - 5m,
        PreviousRsi = previousRsi,
        CurrentPrice = currentPrice,
        CurrentRsi = currentRsi,
        RsiDelta1D = currentRsi - previousRsi,
        ExtremeLow = low,
        ExtremeHigh = high,
        StagedDate = stagedDate,
        LastEvaluatedDate = lastEvaluatedDate,
        IsActiveWatch = active,
    };

    private static RsiScanResult Scan(
        string symbol,
        ScanType scanType,
        decimal rsi,
        decimal price,
        decimal high,
        decimal low,
        decimal atr) => new()
    {
        Symbol = symbol,
        ScanType = scanType,
        Rsi = rsi,
        CurrentPrice = price,
        DayHigh = high,
        DayLow = low,
        DailyAtr = atr,
        Sma200 = 95m,
        ChannelState = "THIRD_TOUCH_TEST",
    };

    private static object Enrichment(RsiScanResult scan) => new
    {
        scan.RsiDelta1D,
        scan.TrendShift,
        scan.TurnStrength,
        scan.ChaseRisk,
        scan.StageStatus,
        scan.DynamicStopLoss,
        scan.TrendSetup200,
        scan.IsTracked,
        scan.ChannelState,
    };

    private static DateOnly TodayEt()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
    }

    private static async Task<string> StateAsync(AppDbContext db)
    {
        var rows = await db.StagedSignals.AsNoTracking().OrderBy(s => s.Symbol).ToListAsync();
        return JsonSerializer.Serialize(rows);
    }
}
