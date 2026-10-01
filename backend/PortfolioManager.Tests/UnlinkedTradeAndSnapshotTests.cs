using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class UnlinkedTradeAndSnapshotTests
{
    private const string Account = "TFSA_D_TD";
    private static readonly DateTime LedgerStart = new(2026, 8, 1);

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static async Task SeedLedgerAsync(AppDbContext db)
    {
        db.CashLedgerSettings.Add(new CashLedgerSettings { Id = 1, LedgerStartDate = LedgerStart });
        await db.SaveChangesAsync();
    }

    private static UnlinkedTradeService CreateDetector(AppDbContext db) =>
        new(db, new HttpContextAccessor(), new CashLedgerQueryService(db));

    private static CashService CreateCash(AppDbContext db)
    {
        var clock = new MutationClock();
        var ledger = new CashLedgerQueryService(db);
        var history = new PortfolioValueHistoryService(db, new CashTradeLinkTests.NoQuotesMarketData(), ledger, clock, NullLogger<PortfolioValueHistoryService>.Instance);
        return new CashService(db, new HttpContextAccessor(), ledger, history, clock);
    }

    private static PortfolioItem Stock(string symbol, decimal shares, decimal cost, DateTime openDate) => new()
    {
        Symbol = symbol, CompanyName = symbol, Shares = shares, AverageCostBasis = cost,
        TransactionType = "OPEN", AccountType = Account, OpenDate = openDate, AddedAt = openDate
    };

    // ── Unlinked-trade detector ──────────────────────────────────────────────

    [Fact]
    public async Task Detector_ReportsTradesWithoutCash_AndSkipsLinkedOnes()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var linked = Stock("AAA.TO", 10m, 5m, new DateTime(2026, 9, 1));
        var missing = Stock("BBB.TO", 100m, 2m, new DateTime(2026, 9, 2));
        db.PortfolioItems.AddRange(linked, missing);
        await db.SaveChangesAsync();
        await CreateCash(db).AddLinkedAsync(new AddLinkedCashRequest(
            TradeLinkSourceTypes.PortfolioOpen, linked.Id, 50m, null, Account, new DateTime(2026, 9, 1)));

        var result = await CreateDetector(db).GetUnlinkedAsync();

        var only = Assert.Single(result);
        Assert.Equal(missing.Id, only.SourceItemId);
        Assert.Equal(TradeLinkSourceTypes.PortfolioOpen, only.SourceType);
        Assert.Equal(200m, only.Amount);
        Assert.Equal("NoCash", only.Reason);
    }

    [Fact]
    public async Task Detector_IgnoresTradesBeforeLedgerStart_AndManualPositions()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        db.PortfolioItems.Add(Stock("OLD.TO", 10m, 5m, LedgerStart.AddDays(-30)));
        var manual = Stock("M_ABC123", 1m, 1000m, new DateTime(2026, 9, 5));
        manual.IsManual = true;
        db.PortfolioItems.Add(manual);
        await db.SaveChangesAsync();

        Assert.Empty(await CreateDetector(db).GetUnlinkedAsync());
    }

    [Fact]
    public async Task Detector_HandEnteredLookAlikeRow_CountsAsCovered_ButOnlyOnce()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        db.PortfolioItems.AddRange(
            Stock("ONE.TO", 10m, 10m, new DateTime(2026, 9, 10)),
            Stock("TWO.TO", 10m, 10m, new DateTime(2026, 9, 10)));
        db.CashItems.Add(new CashItem
        {
            Description = "manual buy", Amount = -100m, AccountType = Account, CashFlowType = CashFlowTypeRules.TradePurchase,
            TransactionDate = new DateTime(2026, 9, 11), AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // One $100 row can vouch for only one of the two identical $100 buys.
        Assert.Single(await CreateDetector(db).GetUnlinkedAsync());
    }

    [Fact]
    public async Task Detector_LookAlikeInWrongAccountOrDirection_DoesNotCount()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        db.PortfolioItems.Add(Stock("ONE.TO", 10m, 10m, new DateTime(2026, 9, 10)));
        db.CashItems.AddRange(
            new CashItem { Description = "other acct", Amount = -100m, AccountType = "RRSP", CashFlowType = CashFlowTypeRules.TradePurchase, TransactionDate = new DateTime(2026, 9, 10), AddedAt = DateTime.UtcNow },
            new CashItem { Description = "deposit", Amount = 100m, AccountType = Account, CashFlowType = CashFlowTypeRules.Deposit, TransactionDate = new DateTime(2026, 9, 10), AddedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        Assert.Single(await CreateDetector(db).GetUnlinkedAsync());
    }

    [Fact]
    public async Task Detector_ClosedPosition_ReportsBothLegs_AndOptionsUseTheMultiplier()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var closed = Stock("CLS.TO", 10m, 10m, new DateTime(2026, 9, 1));
        closed.TransactionType = "CLOSE";
        closed.CloseDate = new DateTime(2026, 9, 8);
        closed.ClosingPrice = 12m;
        db.PortfolioItems.Add(closed);
        db.OptionItems.Add(new OptionItem
        {
            UnderlyingTicker = "T.TO", PositionType = "CALL", ExpirationDate = new DateTime(2026, 12, 18), Strike = 30m,
            Premium = 1.4m, NumberOfContracts = 30, TransactionType = "OPEN", AccountType = Account,
            OpenDate = new DateTime(2026, 9, 3), AddedAt = new DateTime(2026, 9, 3)
        });
        await db.SaveChangesAsync();

        var result = await CreateDetector(db).GetUnlinkedAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal(120m, result.Single(r => r.SourceType == TradeLinkSourceTypes.PortfolioClose).Amount);
        Assert.Equal(100m, result.Single(r => r.SourceType == TradeLinkSourceTypes.PortfolioOpen).Amount);
        Assert.Equal(4200m, result.Single(r => r.SourceType == TradeLinkSourceTypes.OptionOpen).Amount);
    }

    [Fact]
    public async Task Detector_ZeroValueClose_NeedsNoCashEntry()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        db.OptionItems.Add(new OptionItem
        {
            UnderlyingTicker = "T.TO", PositionType = "CALL", ExpirationDate = new DateTime(2026, 12, 18), Strike = 30m,
            Premium = 1m, NumberOfContracts = 5, TransactionType = "CLOSE", ClosingPrice = 0m, AccountType = Account,
            OpenDate = new DateTime(2026, 7, 1), CloseDate = new DateTime(2026, 9, 18), AddedAt = new DateTime(2026, 7, 1)
        });
        await db.SaveChangesAsync();

        Assert.Empty(await CreateDetector(db).GetUnlinkedAsync());
    }

    [Fact]
    public async Task Detector_CloseWithoutClosingPrice_IsFlaggedMissingPrice()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var closed = Stock("CLS.TO", 10m, 10m, new DateTime(2026, 9, 1));
        closed.TransactionType = "CLOSE";
        closed.CloseDate = new DateTime(2026, 9, 8);
        db.PortfolioItems.Add(closed);
        db.CashItems.Add(new CashItem
        {
            Description = "buy", Amount = -100m, AccountType = Account, CashFlowType = CashFlowTypeRules.TradePurchase,
            TransactionDate = new DateTime(2026, 9, 1), AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var only = Assert.Single(await CreateDetector(db).GetUnlinkedAsync());
        Assert.Equal(TradeLinkSourceTypes.PortfolioClose, only.SourceType);
        Assert.Null(only.Amount);
        Assert.Equal("MissingPrice", only.Reason);
    }

    // ── Partial-close purchase split ─────────────────────────────────────────

    [Fact]
    public async Task SplitPurchaseLink_MovesTheRemainderShareToANewRow_AndKeepsNetCashUnchanged()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var stock = Stock("SPL.TO", 4m, 10m, new DateTime(2026, 9, 1));
        db.PortfolioItems.Add(stock);
        await db.SaveChangesAsync();
        var cash = CreateCash(db);
        await cash.AddLinkedAsync(new AddLinkedCashRequest(
            TradeLinkSourceTypes.PortfolioOpen, stock.Id, 40m, "Buy SPL.TO", Account, new DateTime(2026, 9, 1)));

        await cash.SplitPurchaseLinkAsync(stock.Id, 999, remainingShares: 3m, totalShares: 4m);

        var original = await cash.GetLinkedAsync(TradeLinkSourceTypes.PortfolioOpen, stock.Id);
        var remainder = await cash.GetLinkedAsync(TradeLinkSourceTypes.PortfolioOpen, 999);
        Assert.Equal(-10m, original!.Amount);
        Assert.Equal(-30m, remainder!.Amount);
        Assert.Equal(original.AccountType, remainder.AccountType);
        Assert.Equal(original.TransactionDate, remainder.TransactionDate);
        Assert.Equal(-40m, await db.CashItems.SumAsync(c => c.Amount));
    }

    [Fact]
    public async Task SplitPurchaseLink_WithoutAPurchaseLink_OrTwice_DoesNothing()
    {
        await using var db = CreateDb();
        await SeedLedgerAsync(db);
        var stock = Stock("SPL.TO", 4m, 10m, new DateTime(2026, 9, 1));
        db.PortfolioItems.Add(stock);
        await db.SaveChangesAsync();
        var cash = CreateCash(db);

        await cash.SplitPurchaseLinkAsync(stock.Id, 999, 3m, 4m);
        Assert.Empty(db.CashItems);

        await cash.AddLinkedAsync(new AddLinkedCashRequest(
            TradeLinkSourceTypes.PortfolioOpen, stock.Id, 40m, null, Account, new DateTime(2026, 9, 1)));
        await cash.SplitPurchaseLinkAsync(stock.Id, 999, 3m, 4m);
        await cash.SplitPurchaseLinkAsync(stock.Id, 999, 3m, 4m);

        Assert.Equal(2, await db.CashItems.CountAsync());
        Assert.Equal(-40m, await db.CashItems.SumAsync(c => c.Amount));
    }

    // ── Portfolio snapshot sync (stale-snapshot fix) ─────────────────────────

    private static PortfolioItemDto Dto(int id, string symbol, decimal shares, string? type = "OPEN") =>
        new(id, symbol, symbol, shares, 10m, "", "", false, false, null, DateTime.UtcNow, type, Account);

    private static async Task<List<PortfolioSummaryDto>> ReadSnapshotAsync(AppDbContext db, string userId)
    {
        var row = await db.PortfolioSnapshots.AsNoTracking().SingleAsync(s => s.UserId == userId);
        return JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(row.SnapshotJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [Fact]
    public async Task SnapshotUpsert_ReplacesEditedItem_KeepingQuoteAndFinalAction_AndAppendsNewOnes()
    {
        await using var db = CreateDb();
        var svc = new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance);
        var withAction = Dto(1, "AAA", 5m) with { FinalAction = "HOLD", FinalActionSeverity = "Low", FinalActionPriority = "P3" };
        await svc.SaveAsync("u1", [new PortfolioSummaryDto(withAction, new StockQuote { Symbol = "AAA", CurrentPrice = 12m }), new PortfolioSummaryDto(Dto(2, "BBB", 1m), null)]);

        await svc.UpsertItemsAsync("u1", [Dto(1, "AAA", 2m, "CLOSE"), Dto(3, "CCC", 7m)]);

        var list = await ReadSnapshotAsync(db, "u1");
        Assert.Equal(3, list.Count);
        var a = list.Single(s => s.Item.Id == 1);
        Assert.Equal(2m, a.Item.Shares);
        Assert.Equal("CLOSE", a.Item.TransactionType);
        Assert.Equal("HOLD", a.Item.FinalAction);
        Assert.Equal(12m, a.Quote!.CurrentPrice);
        Assert.Null(list.Single(s => s.Item.Id == 3).Quote);
        Assert.Equal(3, (await db.PortfolioSnapshots.SingleAsync()).ItemCount);
    }

    [Fact]
    public async Task SnapshotRemove_DropsTheItem_AndIsNoOpWithoutASnapshot()
    {
        await using var db = CreateDb();
        var svc = new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance);

        await svc.RemoveItemAsync("u1", 1);
        await svc.UpsertItemsAsync("u1", [Dto(1, "AAA", 1m)]);
        Assert.Empty(db.PortfolioSnapshots);

        await svc.SaveAsync("u1", [new PortfolioSummaryDto(Dto(1, "AAA", 1m), null), new PortfolioSummaryDto(Dto(2, "BBB", 1m), null)]);
        await svc.RemoveItemAsync("u1", 1);

        var list = await ReadSnapshotAsync(db, "u1");
        Assert.Equal(2, Assert.Single(list).Item.Id);
        Assert.Equal(1, (await db.PortfolioSnapshots.SingleAsync()).ItemCount);
    }
}
