using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

/// <summary>
/// PM-029 C4: the Final Action refactor (evaluation bundle, single-flight, universe evaluation) must not change a single field of
/// <c>GET api/final-actions</c>. The golden file was captured from the service BEFORE the refactor; set PM029_WRITE_GOLDEN=1 to
/// re-capture deliberately. Only clock-dependent fields are normalised.
/// </summary>
public sealed class Pm029FinalActionGoldenTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] VolatileKeys =
        ["computedAtUtc", "lastSuccessfulEvaluationUtc", "dataAsOfDate", "expectedSession", "entryDate", "portfolioUpdatedAt", "evidenceAsOfUtc", "calculatedAt", "portfolioSnapshotAtUtc"];

    internal sealed class FakeScanner(params RsiScanResult[] results) : IRsiScannerService
    {
        public int PersistingCalls { get; private set; }
        public int ReadOnlyCalls { get; private set; }
        public int PreviewCalls { get; private set; }

        public Task<List<RsiScanResult>> AnalyzeSymbolsAsync(IEnumerable<string> symbols, decimal oversoldThreshold = 30m,
            decimal overboughtThreshold = 75m, string logicMode = "Legacy", string? userId = null, CancellationToken ct = default)
        {
            PersistingCalls++;
            var wanted = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(results.Where(r => wanted.Contains(r.Symbol)).Select(r => r.Copy()).ToList());
        }

        public Task<List<RsiScanResult>> AnalyzeSymbolsReadOnlyAsync(IEnumerable<string> symbols, decimal oversoldThreshold = 30m,
            decimal overboughtThreshold = 75m, string logicMode = "Legacy", string? userId = null, CancellationToken ct = default)
        {
            ReadOnlyCalls++;
            var wanted = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(results.Where(r => wanted.Contains(r.Symbol)).Select(r => r.Copy()).ToList());
        }

        public Task<List<RsiScanResult>> AnalyzeSymbolsPreviewAsync(IEnumerable<string> symbols, decimal oversoldThreshold = 30m,
            decimal overboughtThreshold = 75m, string logicMode = "Legacy", string? userId = null, CancellationToken ct = default)
        {
            PreviewCalls++;
            var wanted = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(results.Where(r => wanted.Contains(r.Symbol)).Select(r => r.Copy()).ToList());
        }

        public Task<ScannerResponse> ScanAsync(IEnumerable<string>? extraSymbols = null, decimal oversoldThreshold = 30m,
            decimal overboughtThreshold = 75m, string logicMode = "Legacy", string? userId = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static DateOnly Expected(string symbol) =>
        TradingCalendar.LastCompletedSession(DateTime.UtcNow, TradingCalendar.ExchangeFor(symbol, null));

    internal static RsiScanResult Scan(string symbol, decimal rsi, decimal price, decimal ema20, decimal sma50, decimal sma200,
        decimal macdHist, decimal macdDelta, decimal volumeRatio = 1.2m, decimal change = 0.8m, string fibZone = "Normal Pullback",
        decimal week52High = 0m, decimal atr = 0m, decimal stop = 0m, string currency = "CAD") => new()
    {
        Symbol = symbol, AnalysisTicker = symbol, AnalysisCurrency = currency, AnalysisMarket = symbol.EndsWith(".TO") ? "CA" : "US",
        Rsi = rsi, CurrentPrice = price, ChangePercent = change, DayHigh = price * 1.01m, DayLow = price * 0.98m, OpenPrice = price * 0.995m,
        PreviousClose = price / (1 + change / 100m), VolumeRatio = volumeRatio, RsiSignal = rsi - 2m, RsiSignalAvailable = true,
        MacdHistogram = macdHist, MacdHistDelta = macdDelta, Ema9Price = price * 0.99m, Ema10Price = price * 0.985m, Ema20Price = ema20,
        Sma20Price = ema20 * 0.99m, Sma50Price = sma50, Sma200 = sma200, Has200Dma = sma200 > 0,
        Status = SignalStatus.Neutral, ScanType = ScanType.Neutral, Week52High = week52High == 0m ? price * 1.1m : week52High,
        FibZone = fibZone, DailyAtr = atr, DynamicStopLoss = stop, TradingDate = Expected(symbol),
        ScannedAt = new DateTime(2026, 1, 2, 21, 0, 0, DateTimeKind.Utc),
    };

    private static PortfolioSummaryDto Holding(int id, string symbol, string role, string account, decimal shares, decimal price, string sector) => new(
        new PortfolioItemDto(id, symbol, symbol, shares, price * 0.9m, sector, "", false, false, null, DateTime.UtcNow.AddDays(-40), "OPEN", account,
            DateTime.UtcNow.AddDays(-30), null, null, role, null, null, null, "Hold", "hold", "INFORMATIONAL", DateTime.UtcNow.AddDays(-1)),
        new StockQuote { Symbol = symbol, CurrentPrice = price, Currency = "CAD" },
        PriceStructureResult.None);

    internal static RsiScanResult[] GoldenScans() =>
    [
        Scan("AAA.TO", 56m, 50m, 48m, 46m, 40m, 0.3m, 0.05m, atr: 1.2m, stop: 47m),
        Scan("BBB.TO", 62m, 120m, 115m, 110m, 95m, 0.6m, 0.1m, atr: 2.5m, stop: 113m),
        Scan("DDD", 74m, 300m, 290m, 280m, 240m, 1.1m, -0.2m, currency: "USD"),
        Scan("EEE.TO", 41m, 30m, 31m, 32m, 33m, -0.2m, -0.05m, change: -1.2m, fibZone: "Deep Pullback"),
        Scan("FFF.TO", 33m, 18m, 19m, 20m, 21m, -0.4m, 0.02m, volumeRatio: 1.8m, change: -2.5m),
    ];

    internal static async Task<(FinalActionService Service, AppDbContext Db, FakeScanner Scanner)> SetUpGoldenAsync(IMemoryCache? cache = null)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var n in new[] { "MARGIN", "TFSA" }) db.AccountTypes.Add(new AccountType { Name = n, NormalizedName = n });
        foreach (var w in new[] { ("AAA.TO", "Swing"), ("BBB.TO", "Strategic"), ("CCC.TO", "Strategic"), ("DDD", "Core") })
            db.WatchlistItems.Add(new WatchlistItem { UserId = "u", Symbol = w.Item1, Role = w.Item2 });
        db.SinglePositionLimits.AddRange(
            new SinglePositionLimit { Role = "Swing", TargetPct = 5m }, new SinglePositionLimit { Role = "Strategic", TargetPct = 10m },
            new SinglePositionLimit { Role = "Core", TargetPct = 15m });
        db.AllocationSectorTargets.AddRange(new AllocationSectorTarget { Sector = "Tech", TargetPct = 20m },
            new AllocationSectorTarget { Sector = "Energy", TargetPct = 10m });
        db.CashItems.Add(new CashItem { UserId = "u", Amount = 20000m, AccountType = "MARGIN", CashFlowType = "Deposit" });
        db.ValueScreenerSnapshots.Add(new ValueScreenerSnapshot
        {
            Origin = "Watchlist", RunAt = DateTime.UtcNow.AddDays(-1),
            ResultsJson = JsonSerializer.Serialize(new List<ValueScreenerResult>
            {
                new() { Symbol = "AAA.TO", Score = 7.5m, ActionTrigger = ActionTrigger.AccumulateValue, Origin = ValueOrigin.Watchlist },
                new() { Symbol = "DDD", Score = 3m, ActionTrigger = ActionTrigger.ValueTrapWarning, Origin = ValueOrigin.Watchlist },
            }),
        });
        var holdings = new List<PortfolioSummaryDto>
        {
            Holding(1, "BBB.TO", "Swing", "MARGIN", 100m, 120m, "Tech"),
            Holding(2, "BBB.TO", "Strategic", "TFSA", 50m, 120m, "Tech"),
            Holding(3, "EEE.TO", "Core", "TFSA", 400m, 30m, "Energy"),
            Holding(4, "FFF.TO", "Swing", "MARGIN", 300m, 18m, "Energy"),
        };
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = "u", SnapshotJson = JsonSerializer.Serialize(holdings, Json) });
        await db.SaveChangesAsync();
        var scanner = new FakeScanner(GoldenScans());
        var service = new FinalActionService(db, scanner, new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance),
            cache ?? new MemoryCache(new MemoryCacheOptions()), NullLogger<FinalActionService>.Instance);
        return (service, db, scanner);
    }

    internal static string Normalise(FinalActionSnapshotDto snapshot)
    {
        var node = JsonSerializer.SerializeToNode(snapshot, Json)!;
        Strip(node);
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        static void Strip(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    foreach (var k in VolatileKeys) o.Remove(k);
                    foreach (var kv in o.ToList()) Strip(kv.Value);
                    break;
                case JsonArray a:
                    foreach (var item in a) Strip(item);
                    break;
            }
        }
    }

    private static string GoldenPath([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "") =>
        Path.Combine(Path.GetDirectoryName(sourceFile)!, "Golden", "pm029-final-actions-golden.json");

    [Fact]
    public async Task FinalActions_AreIdenticalToThePreRefactorGolden()
    {
        var (service, _, _) = await SetUpGoldenAsync();
        var actual = Normalise(await service.ComputeAsync("u"));
        var path = Path.GetFullPath(GoldenPath());
        if (Environment.GetEnvironmentVariable("PM029_WRITE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, actual);
        }
        Assert.True(File.Exists(path), $"Golden file missing: {path}");
        Assert.Equal((await File.ReadAllTextAsync(path)).ReplaceLineEndings(), actual.ReplaceLineEndings());
    }
}
