using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

public sealed class FinalActionCatalogTests
{
    [Fact]
    public void CanonicalId_IgnoresCaseAndSpacingAroundSlash_AndNormalisesRiskControlPercent()
    {
        Assert.Equal("ADD/STAGED ENTRY", FinalActionCatalog.CanonicalId("Add / Staged Entry"));
        Assert.Equal("ADD/STAGED ENTRY", FinalActionCatalog.CanonicalId("ADD/STAGGED ENTRY"));
        Assert.Equal("CUT LOSS/EXIT", FinalActionCatalog.CanonicalId("  cut  loss / exit "));
        Assert.Equal(FinalActionCatalog.CanonicalId("Risk Control: 38% Closed — Hold Runner"),
            FinalActionCatalog.CanonicalId("Risk Control: 25% Closed — Hold Runner"));
    }

    [Fact]
    public void Catalog_HasUniqueIds_AndMetadataForEveryEntry()
    {
        Assert.Equal(FinalActionCatalog.All.Count, FinalActionCatalog.All.Select(d => d.Id).Distinct().Count());
        Assert.All(FinalActionCatalog.All, d =>
        {
            Assert.Contains(d.Category, new[] { "Entry", "TrimExit", "Advisory" });
            Assert.Contains(d.Direction, new[] { "Buy", "Sell", "Neutral" });
        });
        // the business defaults that the rules emit resolve; TRIM/EXIT is not an action the rules produce
        Assert.True(FinalActionCatalog.IsKnown("STARTER ENTRY"));
        Assert.True(FinalActionCatalog.IsKnown("SMALL ADD"));
        Assert.True(FinalActionCatalog.IsKnown("TRIM/REVIEW"));
        Assert.True(FinalActionCatalog.IsKnown("CUT LOSS/EXIT"));
        Assert.False(FinalActionCatalog.IsKnown("TRIM/EXIT"));
    }

    [Fact]
    public void Describe_UnknownIdsAreAdvisoryNeutral()
    {
        var d = FinalActionCatalog.Describe("my own thing");
        Assert.Equal("MY OWN THING", d.Id);
        Assert.Equal("Advisory", d.Category);
        Assert.Equal("Neutral", d.Direction);
    }
}

/// <summary>Maps backend scan results to engine input and wires watchlist + portfolio decisions end to end.</summary>
public sealed class FinalActionServiceTests
{
    private static RsiScanResult BullishScan(string symbol) => new()
    {
        Symbol = symbol, AnalysisTicker = symbol, AnalysisCurrency = "USD", Rsi = 58m, CurrentPrice = 100m, ChangePercent = 0.5m,
        DayHigh = 101m, DayLow = 98m, OpenPrice = 99.5m, PreviousClose = 99.4m, VolumeRatio = 1.6m,
        RsiSignal = 54m, RsiSignalAvailable = true, MacdHistogram = 0.4m, MacdHistDelta = 0.1m,
        Ema9Price = 99m, Ema10Price = 98.5m, Ema20Price = 96m, Sma20Price = 95m, Sma50Price = 90m, Sma200 = 80m,
        Status = SignalStatus.Neutral, ScanType = ScanType.Neutral, Week52High = 110m,
    };

    [Fact]
    public void ToInput_MapsDecimalsExactly_AndTreatsNonEmptyChaseRiskTextAsTrue()
    {
        var scan = BullishScan("AAA");
        scan.Rsi = 37.25m;
        scan.ChaseRisk = "High";
        var input = FinalActionService.ToInput(scan);
        Assert.Equal(37.25, input.Rsi);
        Assert.True(input.ChaseRisk);
        Assert.Equal("Neutral", input.Status);
        Assert.False(FinalActionService.ToInput(BullishScan("B")).ChaseRisk);
    }

    [Fact]
    public async Task Compute_EvaluatesWatchlistAndPortfolio_WithCanonicalCatalogIds_AndCachesResult()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.WatchlistItems.Add(new WatchlistItem { UserId = "u", Symbol = "WL", Role = "Core" });
        var held = new PortfolioSummaryDto(
            new PortfolioItemDto(1, "HELD", "Held Inc", 10m, 50m, "", "", false, false, null, DateTime.UtcNow.AddDays(-30),
                "OPEN", "TFSA_D_TD", DateTime.UtcNow.AddDays(-30), null, null, "Core", null, null, null),
            new StockQuote { Symbol = "HELD", CurrentPrice = 100m },
            PriceStructureResult.None);
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = "u", SnapshotJson = System.Text.Json.JsonSerializer.Serialize(new[] { held }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) });
        await db.SaveChangesAsync();

        var scanner = new FakeScanner(BullishScan("WL"), BullishScan("HELD"));
        var snapshotService = new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance);
        var service = new FinalActionService(db, scanner, snapshotService, new MemoryCache(new MemoryCacheOptions()), NullLogger<FinalActionService>.Instance);

        var snapshot = await service.ComputeAsync("u");
        var wl = Assert.Single(snapshot.Watchlist);
        var pf = Assert.Single(snapshot.Portfolio);

        // identical to calling the engine directly on the same inputs
        var expectedWl = FinalActionEngine.TranslateForWatchlist(FinalActionService.ToInput(BullishScan("WL")), "Core");
        Assert.Equal(expectedWl.FinalAction, wl.FinalAction);
        Assert.Equal(FinalActionCatalog.CanonicalId(expectedWl.FinalAction), wl.FinalActionId);
        Assert.Equal("Watchlist", wl.Source);
        Assert.Equal("Portfolio", pf.Source);
        Assert.Equal(1, pf.ItemId);
        Assert.True(FinalActionCatalog.IsKnown(pf.FinalAction), $"'{pf.FinalAction}' missing from the catalog");

        await service.ComputeAsync("u");
        Assert.Equal(1, scanner.Calls);                                // second call served from cache
        await service.ComputeAsync("u", fresh: true);
        Assert.Equal(2, scanner.Calls);

        // Publishing writes the canonical portfolio action where the Action Center reads it (no browser involved).
        Assert.Equal(1, await service.PublishPortfolioActionsAsync("u", snapshot));
        var stored = System.Text.Json.JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(
            (await db.PortfolioSnapshots.SingleAsync(s => s.UserId == "u")).SnapshotJson,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Equal(pf.FinalAction, stored[0].Item.FinalAction);
        Assert.Equal(pf.Severity, stored[0].Item.FinalActionSeverity);
        Assert.NotNull(stored[0].Item.FinalActionUpdatedAt);
    }

    private static RsiScanResult CoolingScan(string symbol)
    {
        var s = BullishScan(symbol);
        s.Rsi = 50m; s.RsiSignalAvailable = false; s.Ema10Price = 98m; s.Ema20Price = 99m; s.Ema9Price = 100m; s.Sma50Price = 90m;
        return s;
    }

    [Fact]
    public async Task Compute_TechnicalOnlyExit_IsReconciledWithEntryDateAndPnl_ForEveryHolding()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var etToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York")).Date;

        PortfolioSummaryDto Swing(int id, string sym, decimal cost, DateTime open, string? source) => new(
            new PortfolioItemDto(id, sym, sym, 10m, cost, "", "", false, false, null, open, "OPEN", "Margin", open, null, null,
                "Swing", null, source, null, null, null, null),
            new StockQuote { Symbol = sym, CurrentPrice = 100m }, PriceStructureResult.None);

        var rows = new[]
        {
            Swing(1, "NEWBUY", 98m, etToday, "Earnings Catalyst"),         // entry today, in profit
            Swing(2, "OLDWIN", 98m, etToday.AddDays(-20), null),           // old position, in profit, no loss
            Swing(3, "OLDLOSS", 120m, etToday.AddDays(-20), null),         // real loss
            Swing(4, "NEWLOSS", 120m, etToday, "Earnings Catalyst"),       // bought today, already under cost
        };
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = "u", SnapshotJson = System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) });
        await db.SaveChangesAsync();

        var scanner = new FakeScanner(CoolingScan("NEWBUY"), CoolingScan("OLDWIN"), CoolingScan("OLDLOSS"), CoolingScan("NEWLOSS"));
        var service = new FinalActionService(db, scanner, new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance),
            new MemoryCache(new MemoryCacheOptions()), NullLogger<FinalActionService>.Instance);

        var by = (await service.ComputeAsync("u")).Portfolio.ToDictionary(p => p.Symbol);
        Assert.Equal("Hold / Catalyst Monitoring", by["NEWBUY"].FinalAction);
        Assert.False(by["NEWBUY"].IsActionable);
        Assert.Equal("Exit / Cut Loss", by["NEWBUY"].OriginalAction);
        Assert.Contains("EVIDENCE_NOT_POST_ENTRY", by["NEWBUY"].ReasonCodes!);
        Assert.NotNull(by["NEWBUY"].EvidenceAsOfUtc);

        Assert.Equal("Hold / Monitor", by["OLDWIN"].FinalAction);
        Assert.False(by["OLDWIN"].IsActionable);

        foreach (var sym in new[] { "OLDLOSS", "NEWLOSS" })
        {
            Assert.Equal("Exit / Cut Loss", by[sym].FinalAction);
            Assert.True(by[sym].IsActionable);
            Assert.Contains("POST_ENTRY_LOSS", by[sym].ReasonCodes!);
        }
    }

    [Fact]
    public void PriorityEligibility_MirrorsTheActionCenterRules()
    {
        FinalActionResultDto D(string action, bool hard = false)
        {
            var def = FinalActionCatalog.Describe(action);
            return new FinalActionResultDto("X", "Watchlist", null, "Core", action, def.Id, def.Category, def.Direction, "buy", "DEVELOPING",
                "ma", "ts", "ms", "ba", "No Gap", "h", "r", hard, 10m, "X", null);
        }

        Assert.False(PriorityCandidateEligibility.Resolve(null, 90m).Eligible);
        Assert.Equal(["MISSING_TECHNICAL_DATA"], PriorityCandidateEligibility.Resolve(null, 90m).ReasonsExcluded);

        var high = PriorityCandidateEligibility.Resolve(D("STARTER ENTRY"), 78m);
        Assert.True(high.Eligible);
        Assert.Equal("HIGH", high.PriorityLevel);
        Assert.Equal("WATCH", PriorityCandidateEligibility.Resolve(D("BUY WATCH"), 60m).PriorityLevel);

        Assert.Equal(["FINAL_ACTION_WAIT_FOR_PULLBACK"], PriorityCandidateEligibility.Resolve(D("WAIT FOR PULLBACK"), 80m).ReasonsExcluded);
        Assert.Equal(["HARD_NEGATIVE_STRUCTURE", "SCORE_BELOW_THRESHOLD"],
            PriorityCandidateEligibility.Resolve(D("ENTRY CANDIDATE", hard: true), 40m).ReasonsExcluded);
        Assert.Equal(["FINAL_ACTION_INELIGIBLE"], PriorityCandidateEligibility.Resolve(D("Hold"), 80m).ReasonsExcluded);
    }

    private sealed class FakeScanner(params RsiScanResult[] results) : IRsiScannerService
    {
        public int Calls { get; private set; }
        public Task<List<RsiScanResult>> AnalyzeSymbolsAsync(IEnumerable<string> symbols, decimal oversoldThreshold = 30m,
            decimal overboughtThreshold = 75m, string logicMode = "Legacy", string? userId = null, CancellationToken ct = default)
        {
            Calls++;
            var wanted = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(results.Where(r => wanted.Contains(r.Symbol)).ToList());
        }

        public Task<ScannerResponse> ScanAsync(IEnumerable<string>? extraSymbols = null, decimal oversoldThreshold = 30m,
            decimal overboughtThreshold = 75m, string logicMode = "Legacy", string? userId = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
