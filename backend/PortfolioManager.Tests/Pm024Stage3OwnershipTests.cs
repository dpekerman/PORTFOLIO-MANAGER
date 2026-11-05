using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

/// <summary>
/// PM-024 Stage 3: Watchlist ownership. An Entry on an owned ticker is an ADD judged against every eligible open lot,
/// never against whichever lot happens to come first. Mixed roles mean the ADD context is ambiguous: Needs Review.
/// In-memory databases and synthetic rows only.
/// </summary>
public sealed class Pm024Stage3OwnershipTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTime Stamp = new(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc);
    private static DateOnly Expected => TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Nyse);

    private sealed class FakeScanner(params RsiScanResult[] results) : IRsiScannerService
    {
        public Task<List<RsiScanResult>> AnalyzeSymbolsAsync(IEnumerable<string> symbols, decimal oversoldThreshold = 30m, decimal overboughtThreshold = 75m,
            string logicMode = "Legacy", string? userId = null, CancellationToken ct = default)
        {
            var wanted = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(results.Where(r => wanted.Contains(r.Symbol)).ToList());
        }

        public Task<ScannerResponse> ScanAsync(IEnumerable<string>? extraSymbols = null, decimal oversoldThreshold = 30m, decimal overboughtThreshold = 75m,
            string logicMode = "Legacy", string? userId = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>BIP-UN.TO-style Starter Entry evidence.</summary>
    private static RsiScanResult Entry(string symbol) => new()
    {
        Symbol = symbol, AnalysisTicker = symbol, AnalysisCurrency = "CAD", Rsi = 57.5m, CurrentPrice = 100m, ChangePercent = 0.5m, DayHigh = 101m, DayLow = 98m,
        OpenPrice = 99.5m, PreviousClose = 99.5m, VolumeRatio = 1.8m, RsiSignal = 53m, RsiSignalAvailable = true, MacdHistogram = 0.4m, MacdHistDelta = 0.1m,
        Ema9Price = 99m, Ema10Price = 98.5m, Ema20Price = 97m, Sma20Price = 96m, Sma50Price = 94m, Sma200 = 95m, Status = SignalStatus.Neutral,
        ScanType = ScanType.Neutral, Week52High = 101m, TradingDate = Expected,
        PriceStructure = PriceStructureResult.None with { KeyLevelRole = "SUPPORT", KeyLevelState = "SUPPORT_RECLAIM" },
    };

    private static PortfolioSummaryDto Lot(int id, string symbol, string role, string account, decimal shares, decimal cost, string type = "OPEN", string sector = "Tech") => new(
        new PortfolioItemDto(id, symbol, symbol, shares, cost, sector, "", false, false, null, Stamp, type, account, Stamp.AddDays(-30), null, null,
            role, null, null, null, "Hold", "hold", "INFORMATIONAL", Stamp),
        new StockQuote { Symbol = symbol, CurrentPrice = 100m },
        PriceStructureResult.None);

    private static async Task<FinalActionResultDto> EvaluateAsync(
        IEnumerable<PortfolioSummaryDto> lots, decimal strategicLimit = 70m, decimal swingLimit = 10m, decimal? sectorTarget = null)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.WatchlistItems.Add(new WatchlistItem { UserId = "u", Symbol = "MIX.TO", Role = "Strategic" });
        db.SinglePositionLimits.Add(new SinglePositionLimit { Role = "Strategic", TargetPct = strategicLimit });
        db.SinglePositionLimits.Add(new SinglePositionLimit { Role = "Core", TargetPct = strategicLimit });
        db.SinglePositionLimits.Add(new SinglePositionLimit { Role = "Swing", TargetPct = swingLimit });
        if (sectorTarget is { } t) db.AllocationSectorTargets.Add(new AllocationSectorTarget { Sector = "Tech", TargetPct = t });
        foreach (var n in new[] { "MARGIN", "TFSA" }) db.AccountTypes.Add(new AccountType { Name = n, NormalizedName = n });
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = "u", SnapshotJson = JsonSerializer.Serialize(lots.ToList(), Json) });
        await db.SaveChangesAsync();
        var service = new FinalActionService(db, new FakeScanner(Entry("MIX.TO"), Entry("OTHER.TO")),
            new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance), new MemoryCache(new MemoryCacheOptions()), NullLogger<FinalActionService>.Instance);
        return (await service.ComputeAsync("u", fresh: true)).Watchlist.Single(w => w.Symbol == "MIX.TO");
    }

    private static PortfolioSummaryDto Other() => Lot(9, "OTHER.TO", "Strategic", "TFSA", 100m, 90m, sector: "Health");

    [Fact]
    public async Task MixedRoles_AreAmbiguous_NeedsReview_NeverAnArbitraryAdd()
    {
        var r = await EvaluateAsync([Lot(1, "MIX.TO", "Strategic", "TFSA", 100m, 80m), Lot(2, "MIX.TO", "Swing", "MARGIN", 50m, 95m), Other()]);

        Assert.Equal("Add / Needs Review", r.FinalAction);
        Assert.Equal("STARTER ENTRY", r.OriginalAction);
        Assert.Contains("ADD_ROLE_POLICY_AMBIGUOUS", r.ReasonCodes!);
        Assert.Contains("ALREADY_OWNED", r.ReasonCodes!);
        Assert.Contains("Strategic, Swing", r.Reason!);
        Assert.False(r.IsActionable);
        Assert.Equal(2, r.Ownership!.OpenLots);
        Assert.True(r.Ownership.MixedRoles);
    }

    [Fact]
    public async Task MixedRoles_ResultDoesNotDependOnLotOrder()
    {
        var strategic = Lot(1, "MIX.TO", "Strategic", "TFSA", 100m, 80m);
        var swing = Lot(2, "MIX.TO", "Swing", "MARGIN", 50m, 95m);
        var a = await EvaluateAsync([strategic, swing, Other()]);
        var b = await EvaluateAsync([swing, strategic, Other()]);

        Assert.Equal(a.FinalAction, b.FinalAction);
        Assert.Equal(a.OriginalAction, b.OriginalAction);
        Assert.Equal(a.ReasonCodes, b.ReasonCodes);
        Assert.Equal(a.Reason, b.Reason);
        Assert.Equal(a.Ownership!.ExposurePct, b.Ownership!.ExposurePct);
    }

    [Fact]
    public async Task SingleRole_AcrossAccountsAndCosts_AggregatesEveryOpenLot_InAnyOrder()
    {
        // Core lots: the Portfolio's own decision ("Hold / Add If Underweight") permits an add, so the Watchlist entry stands.
        var a = Lot(1, "MIX.TO", "Core", "TFSA", 100m, 92m);
        var b = Lot(2, "MIX.TO", "Core", "MARGIN", 50m, 95m);
        var first = await EvaluateAsync([a, b, Other()], strategicLimit: 70m);
        var second = await EvaluateAsync([b, a, Other()], strategicLimit: 70m);

        Assert.Equal("STARTER ENTRY", first.FinalAction);                       // 60% exposure, under the 70% Strategic limit
        Assert.Equal(first.FinalAction, second.FinalAction);
        Assert.Equal(2, first.Ownership!.OpenLots);
        Assert.Equal(60.0, first.Ownership.ExposurePct!.Value, 1);              // both lots count toward one exposure
        Assert.Equal(new[] { "MARGIN", "TFSA" }, first.Ownership.Accounts);
        Assert.False(first.Ownership.MixedRoles);
    }

    [Fact]
    public async Task SingleRole_OverItsConfiguredLimit_StillBlocksTheAdd()
    {
        var r = await EvaluateAsync([Lot(1, "MIX.TO", "Strategic", "TFSA", 100m, 80m), Lot(2, "MIX.TO", "Strategic", "MARGIN", 50m, 95m), Other()], strategicLimit: 40m);
        Assert.Equal("Hold / No Add", r.FinalAction);
        Assert.Contains("ADD_BLOCKED_POSITION_LIMIT", r.ReasonCodes!);
        Assert.Equal("STARTER ENTRY", r.OriginalAction);
    }

    [Fact]
    public async Task SingleRole_SectorOverTarget_StillBlocksTheAdd()
    {
        var r = await EvaluateAsync([Lot(1, "MIX.TO", "Strategic", "TFSA", 100m, 80m), Other()], strategicLimit: 90m, sectorTarget: 10m);
        Assert.Equal("Hold / No Add", r.FinalAction);
        Assert.Contains("ADD_BLOCKED_SECTOR_OVER", r.ReasonCodes!);
    }

    [Fact]
    public async Task ClosedLotOfAnotherRole_IsNotEligible_AndDoesNotMakeTheAddAmbiguous()
    {
        var r = await EvaluateAsync([Lot(1, "MIX.TO", "Core", "TFSA", 100m, 92m), Lot(2, "MIX.TO", "Swing", "MARGIN", 500m, 90m, type: "CLOSE"), Other()]);
        Assert.Equal("STARTER ENTRY", r.FinalAction);
        Assert.DoesNotContain("ADD_ROLE_POLICY_AMBIGUOUS", r.ReasonCodes!);
        Assert.Equal(1, r.Ownership!.OpenLots);
        Assert.False(r.Ownership.MixedRoles);
    }

    [Fact]
    public async Task NotOwned_IsUnaffected()
    {
        var r = await EvaluateAsync([Other()]);
        Assert.Equal("STARTER ENTRY", r.FinalAction);
        Assert.False(r.Ownership!.Owned);
        Assert.DoesNotContain("ALREADY_OWNED", r.ReasonCodes!);
    }
}
