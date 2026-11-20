using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.FinalAction;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Tests;

public sealed class Pm029PhaseZeroRefreshSafetyTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions StateJson = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        MaxDepth = 64,
    };

    static Pm029PhaseZeroRefreshSafetyTests() => OpportunityService.HistoryBatchDelay = TimeSpan.Zero;

    private sealed record Bar(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume);

    private sealed class StaticChartHandler(string body, System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private sealed class SyntheticMarketData : IMarketDataProvider
    {
        public Task<IReadOnlyList<MarketDailyClose>?> GetDailyClosesAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MarketDailyClose>?>([]);

        public Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<StockQuote?>(new StockQuote
            {
                Symbol = symbol,
                CompanyName = symbol == "WRAP.TO" ? "Synthetic Canadian Depositary Receipt" : "Synthetic Signal Corporation",
                CurrentPrice = 270m,
                Currency = "CAD",
            });

        public Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase));

        public Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult(("", ""));

        public Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);

        public Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));

        public Task<FundamentalsSnapshot?> GetFundamentalsAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<FundamentalsSnapshot?>(null);

        public Task<Dictionary<string, DateTime>> GetEarningsDatesAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, decimal>> GetHistoricalClosingPricesAsync(string dateStr, IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OpportunityRefresh_UsesRealScannerPreviewAndLeavesProtectedStateUnchanged()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var userId = $"phase0-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;
        var holdingDto = new PortfolioItemDto(
            1, "SIGNAL.TO", "Synthetic Signal Corporation", 10m, 250m, "Technology", "", false, false, null,
            now.AddDays(-30), TransactionType: "OPEN", AccountType: "MARGIN", OpenDate: now.AddDays(-30), HoldingRole: "Swing");
        var holdings = new List<PortfolioSummaryDto>
        {
            new(holdingDto, new StockQuote { Symbol = "SIGNAL.TO", CurrentPrice = 270m, Currency = "CAD" }, PriceStructureResult.None),
        };

        db.AccountTypes.Add(new AccountType { Name = "MARGIN", NormalizedName = "MARGIN" });
        db.WatchlistItems.AddRange(
            new WatchlistItem { UserId = userId, Symbol = "SIGNAL.TO", Role = "Swing" },
            new WatchlistItem { UserId = userId, Symbol = "WRAP.TO", Role = "Strategic" });
        db.PortfolioItems.Add(new PortfolioItem
        {
            Id = 1,
            UserId = userId,
            Symbol = "SIGNAL.TO",
            CompanyName = "Synthetic Signal Corporation",
            Shares = 10m,
            AverageCostBasis = 250m,
            Sector = "Technology",
            TransactionType = "OPEN",
            AccountType = "MARGIN",
            HoldingRole = "Swing",
            OpenDate = now.AddDays(-30),
        });
        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = userId, SnapshotJson = JsonSerializer.Serialize(holdings, Json), ItemCount = 1 });
        db.WatchlistSnapshots.Add(new WatchlistSnapshot { UserId = userId, SnapshotJson = "[]", ItemCount = 0 });
        db.CashItems.Add(new CashItem { UserId = userId, Amount = 500m, AccountType = "MARGIN", CashFlowType = "Deposit" });
        db.OptionItems.Add(new OptionItem
        {
            UserId = userId,
            UnderlyingTicker = "SIGNAL.TO",
            PositionType = "CALL",
            ExpirationDate = now.AddMonths(3),
            Strike = 270m,
            Premium = 5m,
            NumberOfContracts = 1,
            MarketPrice = 4m,
            TransactionType = "OPEN",
            AccountType = "MARGIN",
        });
        db.RiskPlans.Add(new RiskPlan
        {
            UserId = userId,
            AccountType = "MARGIN",
            Symbol = "SIGNAL.TO",
            PlannedStopPrice = 250m,
            TargetPrice = 300m,
            Currency = "CAD",
        });
        db.BrokerStopRecords.Add(new BrokerStopRecord
        {
            UserId = userId,
            AccountType = "MARGIN",
            Symbol = "SIGNAL.TO",
            TriggerPrice = 250m,
            Currency = "CAD",
        });
        await db.SaveChangesAsync();

        var market = new SyntheticMarketData();
        using var http = new HttpClient(new StaticChartHandler(ChartJson()))
        {
            BaseAddress = new Uri("https://query1.finance.yahoo.com/"),
        };
        var resolver = new SecurityAnalysisResolver(db, market);
        var scanner = new RsiScannerService(
            http,
            NullLogger<RsiScannerService>.Instance,
            market,
            new ScannerRuntimeConfig(),
            new StagedSignalService(db, NullLogger<StagedSignalService>.Instance),
            new ChannelAnalysisService(),
            new TechnicalChannelPersistenceService(db),
            resolver);

        var before = await StateHashesAsync(db);
        var readOnlyScans = await scanner.AnalyzeSymbolsReadOnlyAsync(["SIGNAL.TO", "WRAP.TO"], userId: userId);
        var afterReadOnly = await StateHashesAsync(db);
        Assert.Equal(before, afterReadOnly);
        var readOnlyScan = Assert.Single(readOnlyScans);
        Assert.Equal(ScanType.Oversold, readOnlyScan.ScanType);
        Assert.Equal(0m, readOnlyScan.DynamicStopLoss);

        var previewScans = await scanner.AnalyzeSymbolsPreviewAsync(["SIGNAL.TO", "WRAP.TO"], userId: userId);
        var afterScannerPreview = await StateHashesAsync(db);
        Assert.Equal(before, afterScannerPreview);
        var previewScan = Assert.Single(previewScans);
        Assert.Equal(ScanType.Oversold, previewScan.ScanType);
        Assert.True(previewScan.DynamicStopLoss > 0m);
        Assert.DoesNotContain(await db.SecurityAnalysisMappings.AsNoTracking().ToListAsync(),
            mapping => mapping.TradingTicker == "WRAP.TO");

        var finalActions = new FinalActionService(
            db,
            scanner,
            new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FinalActionService>.Instance);
        var concurrentPreviews = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(_ => finalActions.EvaluatePreviewAsync(userId)));
        Assert.All(concurrentPreviews, result =>
            Assert.Equal(Pm029FinalActionGoldenTests.Normalise(concurrentPreviews[0].Snapshot),
                Pm029FinalActionGoldenTests.Normalise(result.Snapshot)));
        Assert.Null(finalActions.TryGetCached(userId));
        Assert.Equal(before, await StateHashesAsync(db));

        var opportunities = new OpportunityService(
            db,
            finalActions,
            market,
            resolver,
            null!,
            null!,
            NullLogger<OpportunityService>.Instance);

        await opportunities.RefreshAsync(userId);

        var afterRefresh = await StateHashesAsync(db);
        var changed = before.Keys.Where(table => before[table] != afterRefresh[table]).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new HashSet<string>([nameof(OpportunitySnapshot), nameof(OpportunityRefreshState)], StringComparer.Ordinal),
            changed);
        Assert.Empty(await db.StagedSignals.AsNoTracking().ToListAsync());
        Assert.DoesNotContain(await db.SecurityAnalysisMappings.AsNoTracking().ToListAsync(),
            mapping => mapping.TradingTicker == "WRAP.TO");

        string[] protectedTables =
        [
            nameof(PortfolioItem), nameof(PortfolioSnapshot), nameof(WatchlistItem), nameof(WatchlistSnapshot),
            nameof(CashItem), nameof(OptionItem), nameof(RiskPlan), nameof(BrokerStopRecord), nameof(AccountType),
        ];
        foreach (var table in protectedTables)
            Assert.Equal(before[table], afterRefresh[table]);

        var persistedEvaluation = await finalActions.EvaluateAsync(userId, fresh: true);
        var persistedScan = persistedEvaluation.Scans["SIGNAL.TO"];
        Assert.Equal(Pm029FinalActionGoldenTests.Normalise(concurrentPreviews[0].Snapshot),
            Pm029FinalActionGoldenTests.Normalise(persistedEvaluation.Snapshot));
        Assert.Equal(previewScan.Rsi, persistedScan.Rsi);
        Assert.Equal(previewScan.CurrentPrice, persistedScan.CurrentPrice);
        Assert.Equal(previewScan.RsiDelta1D, persistedScan.RsiDelta1D);
        Assert.Equal(previewScan.TrendShift, persistedScan.TrendShift);
        Assert.True(persistedScan.DynamicStopLoss > 0m);
        Assert.Single(await db.StagedSignals.AsNoTracking().ToListAsync());
        Assert.Contains(await db.SecurityAnalysisMappings.AsNoTracking().ToListAsync(),
            mapping => mapping.TradingTicker == "WRAP.TO" && mapping.MappingSource == SecurityAnalysisMappingSource.AUTO);
    }

    [Fact]
    public async Task ScannerPreview_OnRelationalDatabaseIsReadOnly_WhileNormalScanPersistsLifecycle()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options);
        await db.Database.EnsureCreatedAsync();

        var userId = $"phase0c-sqlite-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;
        db.AccountTypes.Add(new AccountType { Name = "MARGIN", NormalizedName = "MARGIN" });
        db.WatchlistItems.Add(new WatchlistItem { UserId = userId, Symbol = "SIGNAL.TO", Role = "Swing" });
        db.PortfolioItems.Add(new PortfolioItem
        {
            Id = 1,
            UserId = userId,
            Symbol = "SIGNAL.TO",
            CompanyName = "Synthetic Signal Corporation",
            Shares = 10m,
            AverageCostBasis = 250m,
            Sector = "Technology",
            TransactionType = "OPEN",
            AccountType = "MARGIN",
            HoldingRole = "Swing",
            OpenDate = now.AddDays(-30),
        });
        db.CashItems.Add(new CashItem { UserId = userId, Amount = 500m, AccountType = "MARGIN", CashFlowType = "Deposit" });
        db.OptionItems.Add(new OptionItem
        {
            UserId = userId,
            UnderlyingTicker = "SIGNAL.TO",
            PositionType = "CALL",
            ExpirationDate = now.AddMonths(3),
            Strike = 270m,
            Premium = 5m,
            NumberOfContracts = 1,
            MarketPrice = 4m,
            TransactionType = "OPEN",
            AccountType = "MARGIN",
        });
        db.RiskPlans.Add(new RiskPlan
        {
            UserId = userId,
            AccountType = "MARGIN",
            Symbol = "SIGNAL.TO",
            PlannedStopPrice = 250m,
            TargetPrice = 300m,
            Currency = "CAD",
        });
        db.BrokerStopRecords.Add(new BrokerStopRecord
        {
            UserId = userId,
            AccountType = "MARGIN",
            Symbol = "SIGNAL.TO",
            TriggerPrice = 250m,
            Currency = "CAD",
        });
        await db.SaveChangesAsync();

        var market = new SyntheticMarketData();
        using var http = new HttpClient(new StaticChartHandler(ChartJson()))
        {
            BaseAddress = new Uri("https://query1.finance.yahoo.com/"),
        };
        var scanner = new RsiScannerService(
            http,
            NullLogger<RsiScannerService>.Instance,
            market,
            new ScannerRuntimeConfig(),
            new StagedSignalService(db, NullLogger<StagedSignalService>.Instance),
            new ChannelAnalysisService(),
            new TechnicalChannelPersistenceService(db),
            new SecurityAnalysisResolver(db, market));

        var before = await StateHashesAsync(db);
        var preview = await scanner.AnalyzeSymbolsPreviewAsync(["SIGNAL.TO", "WRAP.TO"], userId: userId);
        Assert.Single(preview);
        Assert.True(preview[0].DynamicStopLoss > 0m);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(before, await StateHashesAsync(db));
        Assert.DoesNotContain(await db.SecurityAnalysisMappings.AsNoTracking().ToListAsync(),
            mapping => mapping.TradingTicker == "WRAP.TO");

        using var failedHttp = new HttpClient(new StaticChartHandler("", System.Net.HttpStatusCode.ServiceUnavailable))
        {
            BaseAddress = new Uri("https://query1.finance.yahoo.com/"),
        };
        var failedScanner = new RsiScannerService(
            failedHttp,
            NullLogger<RsiScannerService>.Instance,
            market,
            new ScannerRuntimeConfig(),
            new StagedSignalService(db, NullLogger<StagedSignalService>.Instance),
            new ChannelAnalysisService(),
            new TechnicalChannelPersistenceService(db),
            new SecurityAnalysisResolver(db, market));
        Assert.Empty(await failedScanner.AnalyzeSymbolsPreviewAsync(["FAILED.TO"], userId: userId));
        Assert.Equal(before, await StateHashesAsync(db));

        await scanner.AnalyzeSymbolsAsync(["SIGNAL.TO", "WRAP.TO"], userId: userId);
        var afterNormalScan = await StateHashesAsync(db);
        var changed = before.Keys.Where(table => before[table] != afterNormalScan[table]).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(new HashSet<string>([nameof(StagedSignal), nameof(SecurityAnalysisMapping)], StringComparer.Ordinal), changed);

        foreach (var table in new[]
        {
            nameof(PortfolioItem), nameof(WatchlistItem), nameof(CashItem), nameof(OptionItem),
            nameof(RiskPlan), nameof(BrokerStopRecord), nameof(AccountType),
        })
            Assert.Equal(before[table], afterNormalScan[table]);
    }

    [Fact]
    public async Task ScannerPreview_DoesNotStageNeutralMarketResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options);
        await db.Database.EnsureCreatedAsync();

        var market = new SyntheticMarketData();
        using var http = new HttpClient(new StaticChartHandler(ChartJson(neutral: true)))
        {
            BaseAddress = new Uri("https://query1.finance.yahoo.com/"),
        };
        var scanner = new RsiScannerService(
            http,
            NullLogger<RsiScannerService>.Instance,
            market,
            new ScannerRuntimeConfig(),
            new StagedSignalService(db, NullLogger<StagedSignalService>.Instance),
            new ChannelAnalysisService(),
            new TechnicalChannelPersistenceService(db),
            new SecurityAnalysisResolver(db, market));
        var before = await StateHashesAsync(db);

        var result = Assert.Single(await scanner.AnalyzeSymbolsPreviewAsync(["NEUTRAL.TO"]));

        Assert.Equal(ScanType.Neutral, result.ScanType);
        Assert.Equal(before, await StateHashesAsync(db));
        Assert.Empty(await db.StagedSignals.AsNoTracking().ToListAsync());
    }

    private static async Task<Dictionary<string, string>> StateHashesAsync(AppDbContext db)
    {
        db.ChangeTracker.Clear();
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!;
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entityType in db.Model.GetEntityTypes().Where(type => !type.IsOwned() && !type.HasSharedClrType))
        {
            var query = (IQueryable<object>)setMethod.MakeGenericMethod(entityType.ClrType).Invoke(db, null)!;
            var rows = await query.AsNoTracking().ToListAsync();
            var serialized = rows
                .Select(row => JsonSerializer.Serialize(row, entityType.ClrType, StateJson))
                .Order(StringComparer.Ordinal);
            var payload = Encoding.UTF8.GetBytes(string.Join('\n', serialized));
            hashes[entityType.ClrType.Name] = Convert.ToHexString(SHA256.HashData(payload));
        }

        return hashes;
    }

    private static string ChartJson(bool neutral = false)
    {
        var session = TradingCalendar.LastCompletedSession(DateTime.UtcNow, MarketExchange.Tsx);
        var dates = new List<DateOnly>();
        while (dates.Count < 260)
        {
            dates.Add(session);
            session = TradingCalendar.PreviousTradingDay(session, MarketExchange.Tsx);
        }
        dates.Reverse();

        var bars = dates.Select((date, index) =>
        {
            var close = neutral ? 100m + (index % 2) : 400m - index * 0.5m;
            return new Bar(date, close + 0.1m, close + 0.3m, close - 0.3m, close, 100_000);
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            chart = new
            {
                result = new[]
                {
                    new
                    {
                        meta = new { regularMarketPrice = bars[^1].Close, regularMarketTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                        timestamp = bars.Select(bar => new DateTimeOffset(bar.Date.ToDateTime(new TimeOnly(14, 30)), TimeSpan.Zero).ToUnixTimeSeconds()).ToArray(),
                        indicators = new
                        {
                            quote = new[]
                            {
                                new
                                {
                                    open = bars.Select(bar => (decimal?)bar.Open).ToArray(),
                                    high = bars.Select(bar => (decimal?)bar.High).ToArray(),
                                    low = bars.Select(bar => (decimal?)bar.Low).ToArray(),
                                    close = bars.Select(bar => (decimal?)bar.Close).ToArray(),
                                    volume = bars.Select(bar => (long?)bar.Volume).ToArray(),
                                },
                            },
                        },
                    },
                },
            },
        }, Json);
    }
}
