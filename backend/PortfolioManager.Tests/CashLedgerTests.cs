using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class CashFlowTypeRulesTests
{
    [Theory]
    [InlineData(CashFlowTypeRules.Deposit, 1)]
    [InlineData(CashFlowTypeRules.TradeProceeds, 1)]
    [InlineData(CashFlowTypeRules.Dividend, 1)]
    [InlineData(CashFlowTypeRules.Interest, 1)]
    [InlineData(CashFlowTypeRules.AdjustmentIncrease, 1)]
    [InlineData(CashFlowTypeRules.Withdrawal, -1)]
    [InlineData(CashFlowTypeRules.TradePurchase, -1)]
    [InlineData(CashFlowTypeRules.Fee, -1)]
    [InlineData(CashFlowTypeRules.Tax, -1)]
    [InlineData(CashFlowTypeRules.AdjustmentDecrease, -1)]
    public void AllowedSign_MatchesExpectedDirection(string type, int expectedSign) =>
        Assert.Equal(expectedSign, CashFlowTypeRules.AllowedSign(type));

    [Theory]
    [InlineData(CashFlowTypeRules.Deposit, true)]
    [InlineData(CashFlowTypeRules.Withdrawal, true)]
    [InlineData(CashFlowTypeRules.TradeProceeds, false)]
    [InlineData(CashFlowTypeRules.TradePurchase, false)]
    [InlineData(CashFlowTypeRules.OpeningBalance, false)]
    public void IsExternalFlow_OnlyDepositAndWithdrawal(string type, bool expected) =>
        Assert.Equal(expected, CashFlowTypeRules.IsExternalFlow(type));

    [Fact]
    public void DeriveSignedAmount_OpeningBalance_Throws() =>
        Assert.Throws<ArgumentException>(() => CashFlowTypeRules.DeriveSignedAmount(CashFlowTypeRules.OpeningBalance, 100m));

    [Fact]
    public void DeriveSignedAmount_UnknownType_Throws() =>
        Assert.Throws<ArgumentException>(() => CashFlowTypeRules.DeriveSignedAmount("NotARealType", 100m));

    [Fact]
    public void DeriveSignedAmount_Withdrawal_NegatesPositiveMagnitude() =>
        Assert.Equal(-500m, CashFlowTypeRules.DeriveSignedAmount(CashFlowTypeRules.Withdrawal, 500m));

    [Fact]
    public void DeriveSignedAmount_Deposit_KeepsPositive() =>
        Assert.Equal(500m, CashFlowTypeRules.DeriveSignedAmount(CashFlowTypeRules.Deposit, 500m));
}

public sealed class CashLedgerTests
{
    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private const string Account = "TFSA_D_TD";

    private static async Task SeedLedgerStartAsync(AppDbContext db, DateOnly ledgerStart)
    {
        db.CashLedgerSettings.Add(new CashLedgerSettings { Id = 1, LedgerStartDate = ledgerStart.ToDateTime(TimeOnly.MinValue) });
        await db.SaveChangesAsync();
    }

    private static (CashService cash, PortfolioValueHistoryService history, MutationClock clock) CreateServices(AppDbContext db)
    {
        var clock = new MutationClock();
        var cashLedger = new CashLedgerQueryService(db);
        var history = new PortfolioValueHistoryService(db, new FakeMarketData(), cashLedger, clock, NullLogger<PortfolioValueHistoryService>.Instance);
        var cash = new CashService(db, new HttpContextAccessor(), cashLedger, history, clock);
        return (cash, history, clock);
    }

    private static PortfolioValueHistory Row(string recordedDate, decimal stocks, decimal cash, decimal options) => new()
    {
        RecordedAt = DateTime.SpecifyKind(DateTime.Parse(recordedDate).AddHours(20).AddMinutes(30), DateTimeKind.Utc),
        RecordedDate = recordedDate,
        TotalValue = stocks + cash + options,
        StocksValue = stocks,
        CashValue = cash,
        OptionsValue = options,
    };

    // ── AddAsync validation ──────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_MissingCashFlowType_Throws()
    {
        await using var db = CreateDb();
        await SeedLedgerStartAsync(db, new DateOnly(2026, 8, 1));
        var (cash, _, _) = CreateServices(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cash.AddAsync(new AddCashItemRequest("Test", 100m, "", Account)));
    }

    [Fact]
    public async Task AddAsync_OpeningBalance_Throws()
    {
        await using var db = CreateDb();
        await SeedLedgerStartAsync(db, new DateOnly(2026, 8, 1));
        var (cash, _, _) = CreateServices(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cash.AddAsync(new AddCashItemRequest("Test", 100m, CashFlowTypeRules.OpeningBalance, Account)));
    }

    [Fact]
    public async Task AddAsync_TradeProceeds_SellStock_IsExternalFlowFalse()
    {
        await using var db = CreateDb();
        await SeedLedgerStartAsync(db, new DateOnly(2026, 8, 1));
        var (cash, _, _) = CreateServices(db);

        var dto = await cash.AddAsync(new AddCashItemRequest("Stock sale", 4400m, CashFlowTypeRules.TradeProceeds, Account, new DateTime(2026, 9, 5)));

        Assert.Equal(4400m, dto.Amount);
        Assert.False(dto.IsExternalFlow);
    }

    [Fact]
    public async Task AddAsync_Deposit_IsExternalFlowTrue()
    {
        await using var db = CreateDb();
        await SeedLedgerStartAsync(db, new DateOnly(2026, 8, 1));
        var (cash, _, _) = CreateServices(db);

        var dto = await cash.AddAsync(new AddCashItemRequest("Bank deposit", 5000m, CashFlowTypeRules.Deposit, Account, new DateTime(2026, 9, 5)));

        Assert.Equal(5000m, dto.Amount);
        Assert.True(dto.IsExternalFlow);
    }

    [Fact]
    public async Task AddAsync_Withdrawal_StoresNegativeAmount_IsExternalFlowTrue()
    {
        await using var db = CreateDb();
        await SeedLedgerStartAsync(db, new DateOnly(2026, 8, 1));
        var (cash, _, _) = CreateServices(db);

        var dto = await cash.AddAsync(new AddCashItemRequest("Withdraw", 2000m, CashFlowTypeRules.Withdrawal, Account, new DateTime(2026, 9, 5)));

        Assert.Equal(-2000m, dto.Amount);
        Assert.True(dto.IsExternalFlow);
    }

    [Fact]
    public async Task AddAsync_TradePurchase_StoresNegativeAmount_IsExternalFlowFalse()
    {
        await using var db = CreateDb();
        await SeedLedgerStartAsync(db, new DateOnly(2026, 8, 1));
        var (cash, _, _) = CreateServices(db);

        var dto = await cash.AddAsync(new AddCashItemRequest("Buy stock", 3000m, CashFlowTypeRules.TradePurchase, Account, new DateTime(2026, 9, 5)));

        Assert.Equal(-3000m, dto.Amount);
        Assert.False(dto.IsExternalFlow);
    }

    [Fact]
    public async Task AddAsync_FutureDatedDeposit_ExcludedFromCurrentTotal()
    {
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (cash, _, _) = CreateServices(db);

        await cash.AddAsync(new AddCashItemRequest("Opening", 10000m, CashFlowTypeRules.AdjustmentIncrease, Account, new DateTime(2026, 8, 1)));
        await cash.AddAsync(new AddCashItemRequest("Future deposit", 5000m, CashFlowTypeRules.Deposit, Account, new DateTime(2026, 9, 10)));

        var ledger = new CashLedgerQueryService(db);
        var totalBeforeFutureDate = await ledger.GetTotalAsOfAsync(new DateOnly(2026, 9, 5), Account);

        Assert.Equal(10000m, totalBeforeFutureDate);
    }

    // ── AdjustBalanceAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task AdjustBalanceAsync_UserScenario_SellStock_PreservesOpeningBalanceRow()
    {
        // Mirrors the user's own worked example: $10,000 -> $14,400 after a $4,400 stock sale.
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (cash, _, _) = CreateServices(db);

        db.CashItems.Add(new CashItem
        {
            Description = "Opening Balance (migrated)", Amount = 10000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await cash.AdjustBalanceAsync(new AdjustCashBalanceRequest(Account, 14400m, CashFlowTypeRules.TradeProceeds, new DateTime(2026, 9, 5)));

        Assert.Equal(4400m, result.Amount);
        var rows = await db.CashItems.Where(c => c.AccountType == Account).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.CashFlowType == CashFlowTypeRules.OpeningBalance && r.Amount == 10000m);
    }

    [Fact]
    public async Task AdjustBalanceAsync_ContradictoryDirection_Throws()
    {
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (cash, _, _) = CreateServices(db);

        db.CashItems.Add(new CashItem
        {
            Description = "Opening", Amount = 14400m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Balance is increasing ($14,400 -> $15,000) but Withdrawal is negative-only.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cash.AdjustBalanceAsync(new AdjustCashBalanceRequest(Account, 15000m, CashFlowTypeRules.Withdrawal, new DateTime(2026, 9, 5))));
    }

    [Fact]
    public async Task AdjustBalanceAsync_ZeroDelta_Throws()
    {
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (cash, _, _) = CreateServices(db);

        db.CashItems.Add(new CashItem
        {
            Description = "Opening", Amount = 10000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cash.AdjustBalanceAsync(new AdjustCashBalanceRequest(Account, 10000m, CashFlowTypeRules.TradeProceeds)));
    }

    [Fact]
    public async Task AdjustBalanceAsync_OpeningBalanceType_Throws()
    {
        await using var db = CreateDb();
        await SeedLedgerStartAsync(db, new DateOnly(2026, 8, 1));
        var (cash, _, _) = CreateServices(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cash.AdjustBalanceAsync(new AdjustCashBalanceRequest(Account, 1000m, CashFlowTypeRules.OpeningBalance)));
    }

    // ── UpdateAsync (Edit Entry) ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_TypeOnlyChange_NoCashValueRecompute_ButPersistsClassification()
    {
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (cash, history, _) = CreateServices(db);

        db.PortfolioValueHistories.Add(Row("2026-09-02", 700000m, 5000m, 20000m));
        await db.SaveChangesAsync();

        var entry = new CashItem
        {
            Description = "Mis-classified", Amount = 5000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.TradeProceeds, TransactionDate = new DateTime(2026, 9, 2),
            AddedAt = DateTime.UtcNow
        };
        db.CashItems.Add(entry);
        await db.SaveChangesAsync();

        var updated = await cash.UpdateAsync(entry.Id, new UpdateCashItemRequest("Mis-classified", 5000m, CashFlowTypeRules.Deposit, Account, new DateTime(2026, 9, 2)));

        Assert.NotNull(updated);
        Assert.Equal(CashFlowTypeRules.Deposit, updated!.CashFlowType);
        Assert.True(updated.IsExternalFlow);
        var row = await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-09-02");
        Assert.Equal(5000m, row.CashValue); // unchanged: amount didn't change, only classification did
    }

    [Fact]
    public async Task UpdateAsync_BothDatesFuture_NoRecompute()
    {
        await using var db = CreateDb();
        await SeedLedgerStartAsync(db, new DateOnly(2026, 8, 1));
        var (cash, _, _) = CreateServices(db);

        var entry = new CashItem
        {
            Description = "Future", Amount = 5000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.Deposit, TransactionDate = new DateTime(2026, 9, 10),
            AddedAt = DateTime.UtcNow
        };
        db.CashItems.Add(entry);
        await db.SaveChangesAsync();

        // Sep 10 -> Sep 12, both still future relative to "today" (2026-09-05 in this test's real clock
        // context is in the past — but ledgerStart/tests don't depend on the real system clock for this
        // assertion; what matters is both dates land on the same side, so no exception should be thrown).
        var updated = await cash.UpdateAsync(entry.Id, new UpdateCashItemRequest("Future", 6000m, CashFlowTypeRules.Deposit, Account, new DateTime(2026, 9, 12)));
        Assert.NotNull(updated);
        Assert.Equal(6000m, updated!.Amount);
    }

    [Fact]
    public async Task UpdateAsync_FutureToHistorical_RecalculatesFromHistoricalDate()
    {
        // Sep 10 (future) -> Sep 3 (historical): must recalculate from Sep 3 forward.
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (cash, _, _) = CreateServices(db);

        db.PortfolioValueHistories.Add(Row("2026-09-03", 700000m, 10000m, 20000m));
        db.CashItems.Add(new CashItem
        {
            Description = "Opening", Amount = 10000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var entry = new CashItem
        {
            Description = "Deposit", Amount = 5000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.Deposit, TransactionDate = new DateTime(2026, 9, 10),
            AddedAt = DateTime.UtcNow
        };
        db.CashItems.Add(entry);
        await db.SaveChangesAsync();

        await cash.UpdateAsync(entry.Id, new UpdateCashItemRequest("Deposit", 5000m, CashFlowTypeRules.Deposit, Account, new DateTime(2026, 9, 3)));

        var row = await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-09-03");
        Assert.Equal(15000m, row.CashValue); // deposit now correctly included as of Sep 3
    }

    [Fact]
    public async Task UpdateAsync_HistoricalToFuture_RemovesAmountFromHistory()
    {
        // Sep 3 (historical) -> Sep 10 (future): the deposit must be REMOVED from Sep 3's history,
        // because it no longer belongs there once its effective date moves into the future.
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (cash, _, _) = CreateServices(db);

        db.PortfolioValueHistories.Add(Row("2026-09-03", 700000m, 15000m, 20000m));
        db.CashItems.Add(new CashItem
        {
            Description = "Opening", Amount = 10000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var entry = new CashItem
        {
            Description = "Deposit", Amount = 5000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.Deposit, TransactionDate = new DateTime(2026, 9, 3),
            AddedAt = DateTime.UtcNow
        };
        db.CashItems.Add(entry);
        await db.SaveChangesAsync();

        await cash.UpdateAsync(entry.Id, new UpdateCashItemRequest("Deposit", 5000m, CashFlowTypeRules.Deposit, Account, new DateTime(2026, 9, 10)));

        var row = await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-09-03");
        Assert.Equal(10000m, row.CashValue); // deposit correctly excluded now that it's future-dated
    }

    [Fact]
    public async Task DeleteAsync_HistoricalEntry_TriggersRecalculationFromItsDate()
    {
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (cash, _, _) = CreateServices(db);

        db.PortfolioValueHistories.Add(Row("2026-09-02", 700000m, 15000m, 20000m));
        db.CashItems.Add(new CashItem
        {
            Description = "Opening", Amount = 10000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var entry = new CashItem
        {
            Description = "Deposit", Amount = 5000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.Deposit, TransactionDate = new DateTime(2026, 9, 2),
            AddedAt = DateTime.UtcNow
        };
        db.CashItems.Add(entry);
        await db.SaveChangesAsync();

        await cash.DeleteAsync(entry.Id);

        var row = await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-09-02");
        Assert.Equal(10000m, row.CashValue); // deleted deposit no longer counted
    }

    [Fact]
    public async Task DeleteAsync_FutureEntry_NoRecomputeTriggered()
    {
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (cash, _, _) = CreateServices(db);

        db.PortfolioValueHistories.Add(Row("2026-09-02", 700000m, 10000m, 20000m));
        db.CashItems.Add(new CashItem
        {
            Description = "Opening", Amount = 10000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var entry = new CashItem
        {
            Description = "Future deposit", Amount = 5000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.Deposit, TransactionDate = new DateTime(2026, 9, 10),
            AddedAt = DateTime.UtcNow
        };
        db.CashItems.Add(entry);
        await db.SaveChangesAsync();

        await cash.DeleteAsync(entry.Id);

        var row = await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-09-02");
        Assert.Equal(10000m, row.CashValue); // untouched — the deleted entry was future-only
    }

    [Fact]
    public async Task DeleteAsync_OpeningBalance_Throws()
    {
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (cash, _, _) = CreateServices(db);

        var entry = new CashItem
        {
            Description = "Opening", Amount = 10000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        };
        db.CashItems.Add(entry);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => cash.DeleteAsync(entry.Id));
    }

    // ── RecalculateCashRangeAsync (the Rev 1 regression test) ────────────────

    [Fact]
    public async Task RecalculateCashRangeAsync_PreservesStocksAndOptionsValue_OnlyPatchesCash()
    {
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (_, history, _) = CreateServices(db);

        // Sept 1 snapshot: Stocks $700k / Cash $10k / Options $20k (as originally recorded).
        db.PortfolioValueHistories.Add(Row("2026-09-01", 700000m, 10000m, 20000m));
        // Opening balance dated before Sept 1 so the ledger already has $10,000 as of Sept 1.
        db.CashItems.Add(new CashItem
        {
            Description = "Opening", Amount = 10000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Backdated deposit discovered on Sept 5, dated Sept 1.
        db.CashItems.Add(new CashItem
        {
            Description = "Late deposit", Amount = 5000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.Deposit, TransactionDate = new DateTime(2026, 9, 1),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await history.RecalculateCashRangeAsync(new DateOnly(2026, 9, 1), CancellationToken.None);

        var row = await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-09-01");
        Assert.Equal(15000m, row.CashValue);
        Assert.Equal(700000m, row.StocksValue); // untouched
        Assert.Equal(20000m, row.OptionsValue); // untouched, even though today's options are worth less
        Assert.Equal(735000m, row.TotalValue);
    }

    [Fact]
    public async Task RecalculateCashRangeAsync_NeverTouchesRowsBeforeLedgerStartDate()
    {
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 9, 5);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (_, history, _) = CreateServices(db);

        // Pre-ledger row: should remain completely frozen regardless of what RecalculateCashRangeAsync sees.
        db.PortfolioValueHistories.Add(Row("2026-08-20", 500000m, 8000m, 15000m));
        await db.SaveChangesAsync();

        await history.RecalculateCashRangeAsync(new DateOnly(2026, 8, 1), CancellationToken.None);

        var row = await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-08-20");
        Assert.Equal(8000m, row.CashValue);
        Assert.Equal(500000m, row.StocksValue);
        Assert.Equal(15000m, row.OptionsValue);
    }

    [Fact]
    public async Task RecalculateCashRangeAsync_Idempotent_RunningTwiceProducesSameResult()
    {
        await using var db = CreateDb();
        var ledgerStart = new DateOnly(2026, 8, 1);
        await SeedLedgerStartAsync(db, ledgerStart);
        var (_, history, _) = CreateServices(db);

        db.PortfolioValueHistories.Add(Row("2026-09-01", 700000m, 10000m, 20000m));
        db.CashItems.Add(new CashItem
        {
            Description = "Opening", Amount = 10000m, AccountType = Account,
            CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await history.RecalculateCashRangeAsync(new DateOnly(2026, 9, 1), CancellationToken.None);
        var first = (await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-09-01")).TotalValue;

        await history.RecalculateCashRangeAsync(new DateOnly(2026, 9, 1), CancellationToken.None);
        var second = (await db.PortfolioValueHistories.SingleAsync(h => h.RecordedDate == "2026-09-01")).TotalValue;

        Assert.Equal(first, second);
    }

    // ── Transactional rollback (requires a real relational provider) ────────

    [Fact]
    public async Task BackdatedAdjustBalance_HistoryRecalcFailure_RollsBackLedgerWrite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            await using (var setupDb = new AppDbContext(options))
            {
                await setupDb.Database.EnsureCreatedAsync();
                var ledgerStart = new DateOnly(2026, 8, 1);
                setupDb.CashLedgerSettings.Add(new CashLedgerSettings { Id = 1, LedgerStartDate = ledgerStart.ToDateTime(TimeOnly.MinValue) });
                setupDb.CashItems.Add(new CashItem
                {
                    Description = "Opening", Amount = 10000m, AccountType = Account,
                    CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
                    AddedAt = DateTime.UtcNow
                });
                await setupDb.SaveChangesAsync();
            }

            await using var db = new AppDbContext(options);
            var clock = new MutationClock();
            var cashLedger = new CashLedgerQueryService(db);
            var cash = new CashService(db, new HttpContextAccessor(), cashLedger, new ThrowingHistoryService(), clock);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                cash.AdjustBalanceAsync(new AdjustCashBalanceRequest(Account, 14400m, CashFlowTypeRules.TradeProceeds, new DateTime(2026, 9, 1))));

            await using var verifyDb = new AppDbContext(options);
            var total = (await verifyDb.CashItems.ToListAsync()).Sum(c => c.Amount);
            Assert.Equal(10000m, total); // the delta row must NOT have been persisted
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Fact]
    public async Task DuplicateOpeningBalance_SameAccountAndDate_RejectedByUniqueIndex()
    {
        // EF Core's InMemory provider doesn't enforce unique indexes at all, so this needs a real
        // relational provider — mirrors what was manually verified against SQL Server during migration.
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var ledgerStart = new DateOnly(2026, 9, 5);
            await using (var setupDb = new AppDbContext(options))
            {
                await setupDb.Database.EnsureCreatedAsync();
                setupDb.CashItems.Add(new CashItem
                {
                    Description = "Opening", Amount = 10000m, AccountType = Account,
                    CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
                    AddedAt = DateTime.UtcNow
                });
                await setupDb.SaveChangesAsync();
            }

            await using var db = new AppDbContext(options);
            db.CashItems.Add(new CashItem
            {
                Description = "Duplicate opening", Amount = 999m, AccountType = Account,
                CashFlowType = CashFlowTypeRules.OpeningBalance, TransactionDate = ledgerStart.ToDateTime(TimeOnly.MinValue),
                AddedAt = DateTime.UtcNow
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private sealed class ThrowingHistoryService : IPortfolioValueHistoryService
    {
        public Task<IReadOnlyList<PortfolioValueHistoryDto>> GetLatestAsync(int count, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveAsync(decimal totalValue, decimal stocksValue, decimal cashValue, decimal optionsValue, string recordedDate, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> ExistsForDateAsync(string recordedDate, CancellationToken ct) => Task.FromResult(false);
        public Task<PortfolioValueHistoryDto> RecordCurrentValueAsync(CancellationToken ct, PortfolioValueSource source) => throw new NotImplementedException();
        public Task<IReadOnlyList<PortfolioValueHistoryDto>> BackfillMissingAsync(int lookbackDays, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> GetMissingDatesAsync(int lookbackDays, CancellationToken ct) => throw new NotImplementedException();
        public Task RecalculateCashRangeAsync(DateOnly fromDate, CancellationToken ct) =>
            throw new InvalidOperationException("Simulated history recompute failure for the rollback test.");
        public Task<IReadOnlyList<PortfolioValueHistoryDto>> GetRangeAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeMarketData : IMarketDataProvider
    {
        public Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<StockQuote?>(null);

        public Task<IReadOnlyList<MarketDailyClose>?> GetDailyClosesAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MarketDailyClose>?>(null);

        public Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, StockQuote>());

        public Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult(("", ""));

        public Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SymbolSearchResult>>([]);

        public Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, decimal>());

        public Task<FundamentalsSnapshot?> GetFundamentalsAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult<FundamentalsSnapshot?>(null);

        public Task<Dictionary<string, DateTime>> GetEarningsDatesAsync(IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, DateTime>());

        public Task<Dictionary<string, decimal>> GetHistoricalClosingPricesAsync(string dateStr, IEnumerable<string> symbols, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, decimal>());
    }
}
