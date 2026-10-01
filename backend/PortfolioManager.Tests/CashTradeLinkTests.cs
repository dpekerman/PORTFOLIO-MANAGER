using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class CashTradeLinkTests
{
    private const string Account = "TFSA_D_TD";

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static async Task SeedLedgerAsync(AppDbContext db, decimal opening = 50000m)
    {
        var start = new DateOnly(2026, 8, 1);
        db.CashLedgerSettings.Add(new CashLedgerSettings { Id = 1, LedgerStartDate = start.ToDateTime(TimeOnly.MinValue) });
        db.CashItems.Add(new CashItem
        {
            Description = "Opening", Amount = opening, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = start.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static CashService CreateCash(AppDbContext db)
    {
        var clock = new MutationClock();
        var ledger = new CashLedgerQueryService(db);
        var history = new PortfolioValueHistoryService(db, new NoQuotesMarketData(), ledger, clock, NullLogger<PortfolioValueHistoryService>.Instance);
        return new CashService(db, new HttpContextAccessor(), ledger, history, clock);
    }

    private static async Task<int> SeedStockAsync(AppDbContext db, bool manual = false)
    {
        var item = new PortfolioItem
        {
            Symbol = manual ? "M_ABC123" : "MCD.TO", CompanyName = "Test", Shares = 500m, AverageCostBasis = 20.17m,
            TransactionType = "OPEN", AccountType = Account, IsManual = manual
        };
        db.PortfolioItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private static async Task<int> SeedOptionAsync(AppDbContext db)
    {
        var item = new OptionItem
        {
            UnderlyingTicker = "T.TO", PositionType = "CALL", ExpirationDate = new DateTime(2026, 12, 18),
            Strike = 30m, Premium = 1.4m, NumberOfContracts = 30, MarketPrice = 1.4m, TransactionType = "OPEN", AccountType = Account
        };
        db.OptionItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    // Same clock as CashService.TodayEt — a UTC date would be 'tomorrow' (future-dated) after 8 PM ET.
    private static DateTime Today
    {
        get
        {
            foreach (var id in new[] { "Eastern Standard Time", "America/New_York" })
            {
                try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(id)).Date; }
                catch (TimeZoneNotFoundException) { /* try next id */ }
            }
            return DateTime.UtcNow.Date;
        }
    }

    [Fact]
    public async Task AddLinked_PortfolioOpen_WritesNegativeTradePurchaseTiedToTheTrade()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var id = await SeedStockAsync(db);

        var dto = await CreateCash(db).AddLinkedAsync(new AddLinkedCashRequest(
            TradeLinkSourceTypes.PortfolioOpen, id, 10085m, "Buy MCD.TO", Account, Today));

        Assert.Equal(-10085m, dto.Amount);
        Assert.Equal(CashFlowTypeRules.TradePurchase, dto.CashFlowType);
        Assert.False(dto.IsExternalFlow);
        Assert.Equal(TradeLinkSourceTypes.PortfolioOpen, dto.SourceType);
        Assert.Equal(id, dto.SourceItemId);
        Assert.Equal(Account, dto.AccountType);
    }

    [Fact]
    public async Task AddLinked_PortfolioClose_WritesPositiveTradeProceeds()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var id = await SeedStockAsync(db);

        var dto = await CreateCash(db).AddLinkedAsync(new AddLinkedCashRequest(
            TradeLinkSourceTypes.PortfolioClose, id, 12000m, null, Account, Today));

        Assert.Equal(12000m, dto.Amount);
        Assert.Equal(CashFlowTypeRules.TradeProceeds, dto.CashFlowType);
        Assert.Equal("CASH", dto.Description); // blank description falls back to the ledger default
    }

    [Fact]
    public async Task AddLinked_OptionOpenAndClose_UseOptionSourceTypes()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var id = await SeedOptionAsync(db);
        var cash = CreateCash(db);

        var open = await cash.AddLinkedAsync(new AddLinkedCashRequest(TradeLinkSourceTypes.OptionOpen, id, 4200m, null, Account, Today));
        var close = await cash.AddLinkedAsync(new AddLinkedCashRequest(TradeLinkSourceTypes.OptionClose, id, 6000m, null, Account, Today));

        Assert.Equal(-4200m, open.Amount);
        Assert.Equal(6000m, close.Amount);
        Assert.Equal(CashFlowTypeRules.TradePurchase, open.CashFlowType);
        Assert.Equal(CashFlowTypeRules.TradeProceeds, close.CashFlowType);
    }

    [Fact]
    public async Task AddLinked_SameLegTwice_IsRejected()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var id = await SeedStockAsync(db);
        var cash = CreateCash(db);
        var request = new AddLinkedCashRequest(TradeLinkSourceTypes.PortfolioOpen, id, 100m, null, Account, Today);

        await cash.AddLinkedAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cash.AddLinkedAsync(request));
        Assert.Equal(1, await db.CashItems.CountAsync(c => c.SourceType != null));
    }

    [Fact]
    public async Task AddLinked_OpenAndCloseLegsOfSamePosition_BothAllowed()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var id = await SeedStockAsync(db);
        var cash = CreateCash(db);

        await cash.AddLinkedAsync(new AddLinkedCashRequest(TradeLinkSourceTypes.PortfolioOpen, id, 100m, null, Account, Today));
        await cash.AddLinkedAsync(new AddLinkedCashRequest(TradeLinkSourceTypes.PortfolioClose, id, 120m, null, Account, Today));

        Assert.Equal(2, await db.CashItems.CountAsync(c => c.SourceItemId == id));
    }

    [Fact]
    public async Task AddLinked_ManualPosition_IsRejected()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var id = await SeedStockAsync(db, manual: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateCash(db).AddLinkedAsync(
            new AddLinkedCashRequest(TradeLinkSourceTypes.PortfolioOpen, id, 100m, null, Account, Today)));
    }

    [Fact]
    public async Task AddLinked_MissingTrade_IsRejected()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateCash(db).AddLinkedAsync(
            new AddLinkedCashRequest(TradeLinkSourceTypes.OptionOpen, 9999, 100m, null, Account, Today)));
    }

    [Theory]
    [InlineData("Deposit", 100)]
    [InlineData("", 100)]
    [InlineData(TradeLinkSourceTypes.PortfolioOpen, 0)]
    [InlineData(TradeLinkSourceTypes.PortfolioOpen, -50)]
    public async Task AddLinked_InvalidSourceTypeOrAmount_Throws(string sourceType, int amount)
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var id = await SeedStockAsync(db);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateCash(db).AddLinkedAsync(
            new AddLinkedCashRequest(sourceType, id, amount, null, Account, Today)));
    }

    [Fact]
    public async Task GetLinked_ReturnsTheRowOrNull()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var id = await SeedStockAsync(db);
        var cash = CreateCash(db);

        Assert.Null(await cash.GetLinkedAsync(TradeLinkSourceTypes.PortfolioOpen, id));

        var created = await cash.AddLinkedAsync(new AddLinkedCashRequest(TradeLinkSourceTypes.PortfolioOpen, id, 100m, null, Account, Today));
        var found = await cash.GetLinkedAsync(TradeLinkSourceTypes.PortfolioOpen, id);

        Assert.Equal(created.Id, found?.Id);
        Assert.Null(await cash.GetLinkedAsync(TradeLinkSourceTypes.PortfolioClose, id));
        Assert.Null(await cash.GetLinkedAsync("Bogus", id));
    }

    [Fact]
    public async Task AddLinked_ReducesTheAccountTotalByExactlyTheTradeAmount()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db, opening: 50000m);
        var id = await SeedStockAsync(db);

        await CreateCash(db).AddLinkedAsync(new AddLinkedCashRequest(TradeLinkSourceTypes.PortfolioOpen, id, 17550m, null, Account, Today));

        var total = await new CashLedgerQueryService(db).GetTotalAsOfAsync(DateOnly.FromDateTime(Today), Account);
        Assert.Equal(32450m, total);
    }

    [Fact]
    public async Task AddLinked_Backdated_PatchesHistoryCashOnly()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db, opening: 50000m);
        var id = await SeedStockAsync(db);
        db.PortfolioValueHistories.Add(new PortfolioValueHistory
        {
            RecordedAt = DateTime.UtcNow, RecordedDate = "2026-09-10",
            StocksValue = 700000m, CashValue = 50000m, OptionsValue = 20000m, TotalValue = 770000m
        });
        await db.SaveChangesAsync();

        await CreateCash(db).AddLinkedAsync(new AddLinkedCashRequest(
            TradeLinkSourceTypes.PortfolioOpen, id, 10000m, null, Account, new DateTime(2026, 9, 10)));

        var row = await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-09-10");
        Assert.Equal(40000m, row.CashValue);
        Assert.Equal(700000m, row.StocksValue);
        Assert.Equal(20000m, row.OptionsValue);
    }

    [Fact]
    public async Task DeleteLinkedRow_RemovesItAndFreesTheLegForRelinking()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var id = await SeedStockAsync(db);
        var cash = CreateCash(db);
        var created = await cash.AddLinkedAsync(new AddLinkedCashRequest(TradeLinkSourceTypes.PortfolioOpen, id, 100m, null, Account, Today));

        Assert.True(await cash.DeleteAsync(created.Id));
        Assert.Null(await cash.GetLinkedAsync(TradeLinkSourceTypes.PortfolioOpen, id));

        var again = await cash.AddLinkedAsync(new AddLinkedCashRequest(TradeLinkSourceTypes.PortfolioOpen, id, 100m, null, Account, Today));
        Assert.Equal(id, again.SourceItemId);
    }

    [Fact]
    public async Task ManualRows_KeepNullSourceFields_AndDoNotBlockLinking()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var id = await SeedStockAsync(db);
        var cash = CreateCash(db);

        var manual = await cash.AddAsync(new AddCashItemRequest("Manual buy", 500m, CashFlowTypeRules.TradePurchase, Account, Today));
        Assert.Null(manual.SourceType);
        Assert.Null(manual.SourceItemId);

        var linked = await cash.AddLinkedAsync(new AddLinkedCashRequest(TradeLinkSourceTypes.PortfolioOpen, id, 500m, null, Account, Today));
        Assert.NotEqual(manual.Id, linked.Id);
    }

    [Fact]
    public async Task TradeLinkIndex_RejectsSecondRowForSameLeg_ButAllowsManyUnlinkedRows()
    {
        // InMemory never enforces unique indexes; SQLite does, matching the SQL Server filtered index.
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            await using var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            db.CashItems.AddRange(
                new CashItem { Description = "a", Amount = -1m, AddedAt = DateTime.UtcNow },
                new CashItem { Description = "b", Amount = -2m, AddedAt = DateTime.UtcNow },
                new CashItem { Description = "linked", Amount = -3m, AddedAt = DateTime.UtcNow, SourceType = "PortfolioOpen", SourceItemId = 7 });
            await db.SaveChangesAsync();

            db.CashItems.Add(new CashItem { Description = "dup", Amount = -4m, AddedAt = DateTime.UtcNow, SourceType = "PortfolioOpen", SourceItemId = 7 });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    internal sealed class NoQuotesMarketData : IMarketDataProvider
    {
        public Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default) => Task.FromResult<StockQuote?>(null);
        public Task<IReadOnlyList<MarketDailyClose>?> GetDailyClosesAsync(string symbol, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MarketDailyClose>?>(null);
        public Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, StockQuote>());
        public Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default) => Task.FromResult(("", ""));
        public Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);
        public Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, decimal>());
        public Task<FundamentalsSnapshot?> GetFundamentalsAsync(string symbol, CancellationToken ct = default) => Task.FromResult<FundamentalsSnapshot?>(null);
        public Task<Dictionary<string, DateTime>> GetEarningsDatesAsync(IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, DateTime>());
        public Task<Dictionary<string, decimal>> GetHistoricalClosingPricesAsync(string dateStr, IEnumerable<string> symbols, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, decimal>());
    }
}
