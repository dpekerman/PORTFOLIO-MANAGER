using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Controllers;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class AccountTypeTests
{
    private static async Task<AppDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection, contextOwnsConnection: true).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static PortfolioItem Stock(string name, string? user = null) => new()
    {
        Symbol = "TEST", CompanyName = "Test", Shares = 5m, AverageCostBasis = 20m,
        AccountType = name, TransactionType = "CLOSE", UserId = user
    };

    [Fact]
    public async Task Seed_AndDelete_DoNotReviveDefaults()
    {
        await using var db = await CreateDbAsync();
        var service = new AccountTypeService(db);
        var items = await service.GetAllAsync(default);
        Assert.Equal(7, items.Count);
        await service.DeleteAsync(items[0].Id, items[0].Version, default);
        await service.InitializeAsync(default);
        Assert.Equal(6, (await service.GetAllAsync(default)).Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901")]
    public async Task InvalidNames_AreRejected(string name)
    {
        await using var db = await CreateDbAsync();
        var error = await Assert.ThrowsAsync<AccountTypeException>(() => new AccountTypeService(db).AddAsync(name, default));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Names_Trim_Boundaries_AndDuplicates()
    {
        await using var db = await CreateDbAsync();
        var service = new AccountTypeService(db);
        Assert.Equal("A", (await service.AddAsync(" A ", default)).Name);
        Assert.Equal(120, (await service.AddAsync(new string('x', 120), default)).Name.Length);
        await Assert.ThrowsAsync<AccountTypeException>(() => service.AddAsync(new string('y', 121), default));
        var error = await Assert.ThrowsAsync<AccountTypeException>(() => service.AddAsync(" a ", default));
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task UsedType_CannotBeDeleted_EvenWhenClosedOrCashNetIsZero()
    {
        await using var db = await CreateDbAsync();
        var service = new AccountTypeService(db);
        var item = await service.AddAsync("Used", default);
        db.PortfolioItems.Add(Stock(item.Name, "another-user"));
        db.CashItems.AddRange(
            new CashItem { Description = "Deposit", AccountType = item.Name, Amount = 100m },
            new CashItem { Description = "Withdrawal", AccountType = item.Name, Amount = -100m });
        await db.SaveChangesAsync();
        var usage = (await service.GetAllAsync(default)).Single(x => x.Id == item.Id);
        Assert.Equal(1, usage.StockCount);
        Assert.Equal(2, usage.CashCount);
        Assert.Equal(409, (await Assert.ThrowsAsync<AccountTypeException>(
            () => service.DeleteAsync(item.Id, item.Version, default))).StatusCode);
    }

    [Fact]
    public async Task Rename_UpdatesAllOwnersAndSnapshots_WithoutChangingValues()
    {
        await using var db = await CreateDbAsync();
        var service = new AccountTypeService(db);
        var item = await service.AddAsync("Before", default);
        var stock = Stock(item.Name, "other");
        db.PortfolioItems.Add(stock);
        db.OptionItems.Add(new OptionItem { UnderlyingTicker = "TEST", PositionType = "PUT", AccountType = item.Name, Premium = 3m, UserId = "third" });
        db.CashItems.Add(new CashItem { Description = "Linked purchase", AccountType = item.Name, Amount = -100m, SourceType = "PortfolioOpen", SourceItemId = 1 });
        await db.SaveChangesAsync();
        var dto = new PortfolioItemDto(stock.Id, stock.Symbol, stock.CompanyName, stock.Shares,
            stock.AverageCostBasis, "", "", false, false, null, DateTime.UtcNow, "CLOSE", item.Name);
        db.PortfolioSnapshots.Add(new PortfolioSnapshot
        {
            UserId = "viewer", SnapshotJson = JsonSerializer.Serialize(new[] {
                new PortfolioSummaryDto(dto with { FinalAction = "HOLD" }, new StockQuote { CurrentPrice = 25m })
            })
        });
        await db.SaveChangesAsync();
        var renamed = await service.RenameAsync(item.Id, new("After", item.Version), default);
        Assert.Equal("Before", renamed.OldName);
        Assert.Equal("After", (await db.PortfolioItems.SingleAsync()).AccountType);
        Assert.Equal("After", (await db.OptionItems.SingleAsync()).AccountType);
        Assert.Equal("After", (await db.CashItems.SingleAsync()).AccountType);
        Assert.Equal(-100m, (await db.CashItems.Select(x => x.Amount).ToListAsync()).Sum());
        Assert.Equal(100m, stock.Shares * stock.AverageCostBasis);
        var snapshot = JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(
            (await db.PortfolioSnapshots.SingleAsync()).SnapshotJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal("After", snapshot[0].Item.AccountType);
        Assert.Equal("HOLD", snapshot[0].Item.FinalAction);
        Assert.Equal(25m, snapshot[0].Quote!.CurrentPrice);
        Assert.Equal(409, (await Assert.ThrowsAsync<AccountTypeException>(
            () => service.RenameAsync(item.Id, new("Again", item.Version), default))).StatusCode);
    }

    [Fact]
    public async Task SnapshotFailure_RollsBackTheWholeRename()
    {
        await using var db = await CreateDbAsync();
        var service = new AccountTypeService(db);
        var item = await service.AddAsync("Before", default);
        db.PortfolioItems.Add(Stock(item.Name));
        await db.SaveChangesAsync();
        // Simulates corruption already present in a legacy database.
        await db.Database.ExecuteSqlRawAsync("INSERT INTO PortfolioSnapshots (UserId, SnapshotJson, UpdatedAt, ItemCount) VALUES ('broken', 'invalid', '2026-10-01', 1)");
        await Assert.ThrowsAsync<AccountTypeException>(() => service.RenameAsync(item.Id, new("After", item.Version), default));
        db.ChangeTracker.Clear();
        Assert.Equal("Before", (await db.AccountTypes.SingleAsync(x => x.Id == item.Id)).Name);
        Assert.Equal("Before", (await db.PortfolioItems.SingleAsync()).AccountType);
    }

    [Fact]
    public async Task MissingRestoreType_IsRejectedBeforeAnyDeletion_AndNullRemainsAllowed()
    {
        await using var db = await CreateDbAsync();
        var stock = Stock("TFSA_D_TD");
        db.PortfolioItems.Add(stock);
        await db.SaveChangesAsync();
        db.PortfolioItems.Remove(stock);
        db.PortfolioItems.Add(Stock("Missing"));
        await Assert.ThrowsAsync<AccountTypeException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(stock.Id, (await db.PortfolioItems.SingleAsync()).Id);
        db.CashItems.Add(new CashItem { Description = "No account", AccountType = null });
        await db.SaveChangesAsync();
        Assert.Null((await db.CashItems.SingleAsync()).AccountType);
    }

    [Fact]
    public async Task StaleSelection_CannotRecreateRenamedOrDeletedType()
    {
        await using var db = await CreateDbAsync();
        var service = new AccountTypeService(db);
        var item = await service.AddAsync("Before", default);
        await service.RenameAsync(item.Id, new("After", item.Version), default);
        db.PortfolioItems.Add(Stock("Before"));
        await Assert.ThrowsAsync<AccountTypeException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var current = (await service.GetAllAsync(default)).Single(x => x.Id == item.Id);
        await service.DeleteAsync(current.Id, current.Version, default);
        db.PortfolioItems.Add(Stock("After"));
        await Assert.ThrowsAsync<AccountTypeException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task LegacyValues_AreImported_AndAmbiguousNamesReported()
    {
        await using var db = await CreateDbAsync();
        // Synchronous setup simulates records written before catalog validation existed.
        db.PortfolioItems.Add(Stock("Legacy"));
        db.SaveChanges();
        var service = new AccountTypeService(db);
        await service.InitializeAsync(default);
        Assert.Contains(await service.GetAllAsync(default), x => x.Name == "Legacy");
        db.PortfolioItems.Add(Stock("legacy"));
        db.SaveChanges();
        await Assert.ThrowsAsync<AccountTypeException>(() => service.InitializeAsync(default));
    }

    [Fact]
    public void Controller_ReadsRequireAuth_AndMutationsRequireAdmin()
    {
        Assert.NotNull(typeof(AccountTypesController).GetCustomAttribute<AuthorizeAttribute>());
        foreach (var method in new[] { "Add", "Rename", "Delete" })
            Assert.Equal("Admin", typeof(AccountTypesController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public async Task AllocationRestore_MissingType_PreservesCashAndOptions()
    {
        await using var db = await CreateDbAsync();
        db.CashItems.Add(new CashItem { Description = "Original", Amount = 123m, AccountType = "TFSA_D_TD" });
        db.OptionItems.Add(new OptionItem { UnderlyingTicker = "OLD", PositionType = "PUT", AccountType = "TFSA_D_TD" });
        await db.SaveChangesAsync();
        var clock = new MutationClock();
        var ledger = new CashLedgerQueryService(db);
        var history = new PortfolioValueHistoryService(db, new CashTradeLinkTests.NoQuotesMarketData(), ledger,
            clock, NullLogger<PortfolioValueHistoryService>.Instance);
        var cash = new CashService(db, new HttpContextAccessor(), ledger, history, clock);
        using var http = new HttpClient();
        var options = new OptionService(db, http, NullLogger<OptionService>.Instance, clock, new HttpContextAccessor());
        var service = new AllocationRestoreService(db, options, cash);
        var backup = new OptionBackupItem("NEW", "PUT", DateTime.UtcNow.AddMonths(1), 1m, 1m, 1, 1m,
            "OPEN", "Missing", null, null, null, null, DateTime.UtcNow);
        await Assert.ThrowsAsync<AccountTypeException>(() => service.RestoreAsync(
            new([new CashBackupItem("Replacement", 999m, DateTime.UtcNow)], [backup]), default));
        db.ChangeTracker.Clear();
        Assert.Equal("OLD", (await db.OptionItems.SingleAsync()).UnderlyingTicker);
        Assert.Equal(123m, (await db.CashItems.SingleAsync()).Amount);
    }

    [Fact]
    public async Task StaleSnapshotSave_UsesAuthoritativeAccountAfterRename()
    {
        await using var db = await CreateDbAsync();
        var service = new AccountTypeService(db);
        var item = await service.AddAsync("Before", default);
        var stock = Stock(item.Name);
        db.PortfolioItems.Add(stock);
        await db.SaveChangesAsync();
        await service.RenameAsync(item.Id, new("After", item.Version), default);
        var stale = new PortfolioItemDto(stock.Id, stock.Symbol, stock.CompanyName, stock.Shares,
            stock.AverageCostBasis, "", "", false, false, null, DateTime.UtcNow, "CLOSE", "Before");
        var snapshotService = new PortfolioSnapshotService(db, NullLogger<PortfolioSnapshotService>.Instance);
        await snapshotService.SaveAsync("user", [new(stale, null)]);
        var snapshot = await snapshotService.GetLatestAsync("user");
        Assert.Equal("After", Assert.Single(snapshot!).Item.AccountType);
    }
}
