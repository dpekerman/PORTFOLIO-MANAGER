using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class PortfolioPerformanceTests
{
    private const string User = "u1";
    // Wed 2026-09-02: week starts Mon 08-31, month starts 09-01, year starts 01-01.
    private static readonly DateOnly Today = new(2026, 9, 2);

    private static AppDbContext CreateDb(DateOnly? ledgerStart = null)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        db.CashLedgerSettings.Add(new CashLedgerSettings
        {
            Id = 1,
            LedgerStartDate = (ledgerStart ?? new DateOnly(2025, 12, 1)).ToDateTime(TimeOnly.MinValue),
        });
        db.SaveChanges();
        return db;
    }

    private static PortfolioPerformanceService Svc(AppDbContext db, MutationClock? clock = null) =>
        new(db, clock ?? new MutationClock());

    private static PortfolioValueHistory Row(string date, decimal stocks, decimal cash, decimal options = 0m) => new()
    {
        RecordedDate = date, StocksValue = stocks, CashValue = cash, OptionsValue = options,
        TotalValue = stocks + cash + options,
    };

    private static void Cash(AppDbContext db, string type, decimal amount, string date, string account = "TFSA_D_TD", string desc = "CASH")
    {
        db.CashItems.Add(new CashItem
        {
            UserId = User, CashFlowType = type, Amount = amount, AccountType = account, Description = desc,
            TransactionDate = DateTime.Parse(date),
        });
        db.SaveChanges();
    }

    private static List<PortfolioValueHistory> History() =>
    [
        Row("2025-12-31", 90_000m, 10_000m),
        Row("2026-08-28", 90_000m, 10_000m),
        Row("2026-08-31", 90_000m, 10_000m),
        Row("2026-09-01", 90_000m, 10_000m),
    ];

    private static PeriodPerformanceDto Get(IReadOnlyList<PeriodPerformanceDto> all, string period) =>
        all.Single(p => p.Period == period);

    [Fact]
    public async Task Deposit_WithNoMarketMovement_IsZeroInvestmentGain()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Deposit, 10_000m, "2026-09-02");
        var res = await Svc(db).BuildAsync(User, History(), 110_000m, Today, default);

        foreach (var p in res)
        {
            Assert.Equal("Available", p.Status);
            Assert.Equal(10_000m, p.ValueChange);
            Assert.Equal(10_000m, p.NetExternalFlow);
            Assert.Equal(0m, p.InvestmentGainLoss);
            Assert.Null(p.InvestmentGainLossPercent);
        }
    }

    [Fact]
    public async Task Withdrawal_WithNoMarketMovement_IsZeroInvestmentLoss()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Withdrawal, -4_000m, "2026-09-02");
        var day = Get(await Svc(db).BuildAsync(User, History(), 96_000m, Today, default), "Day");

        Assert.Equal(-4_000m, day.ValueChange);
        Assert.Equal(-4_000m, day.NetExternalFlow);
        Assert.Equal(0m, day.InvestmentGainLoss);
    }

    [Fact]
    public async Task DepositPlusMarketMovement_ReportsOnlyTheMarketPart()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Deposit, 10_000m, "2026-09-02");
        var day = Get(await Svc(db).BuildAsync(User, History(), 111_500m, Today, default), "Day");

        Assert.Equal(1_500m, day.InvestmentGainLoss);
        Assert.Null(day.InvestmentGainLossPercent);
    }

    [Fact]
    public async Task NoExternalFlow_KeepsSimplePercent()
    {
        await using var db = CreateDb();
        var day = Get(await Svc(db).BuildAsync(User, History(), 101_000m, Today, default), "Day");

        Assert.Equal(1_000m, day.InvestmentGainLoss);
        Assert.Equal(1.00m, day.InvestmentGainLossPercent);
    }

    [Fact]
    public async Task MultipleFlowsInOnePeriod_AreNetted()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Deposit, 5_000m, "2026-09-01", desc: "A");
        Cash(db, CashFlowTypeRules.Deposit, 2_000m, "2026-09-02", desc: "B");
        Cash(db, CashFlowTypeRules.Withdrawal, -1_000m, "2026-09-02", desc: "C");
        var month = Get(await Svc(db).BuildAsync(User, History(), 106_000m, Today, default), "Month");

        Assert.Equal(6_000m, month.NetExternalFlow);
        Assert.Equal(0m, month.InvestmentGainLoss);
    }

    [Fact]
    public async Task Boundaries_BaselineDateExcluded_EndDateIncluded()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Deposit, 1_000m, "2026-08-31"); // on week baseline? baseline is 08-28 -> inside week, outside day
        Cash(db, CashFlowTypeRules.Deposit, 500m, "2026-08-28");   // on the week baseline date -> already in snapshot
        var res = await Svc(db).BuildAsync(User, History(), 101_000m, Today, default);

        Assert.Equal(0m, Get(res, "Day").NetExternalFlow);   // baseline 09-01, window (09-01, 09-02]
        Assert.Equal(1_000m, Get(res, "Week").NetExternalFlow); // baseline 08-28
        Assert.Equal(0m, Get(res, "Month").NetExternalFlow);    // baseline 08-31, window (08-31, 09-02]
    }

    [Fact]
    public async Task WeekendDeposit_BelongsToNextTradingWindow()
    {
        await using var db = CreateDb();
        var history = new List<PortfolioValueHistory> { Row("2026-09-04", 90_000m, 10_000m) }; // Friday close
        Cash(db, CashFlowTypeRules.Deposit, 10_000m, "2026-09-05"); // Saturday
        var mon = new DateOnly(2026, 9, 7);
        var day = Get(await Svc(db).BuildAsync(User, history, 110_000m, mon, default), "Day");

        Assert.Equal("Available", day.Status);
        Assert.Equal(10_000m, day.NetExternalFlow);
        Assert.Equal(0m, day.InvestmentGainLoss);
    }

    [Fact]
    public async Task DepositAfterEod_StillCountedInToday()
    {
        await using var db = CreateDb();
        // Yesterday's sealed snapshot exists; deposit entered after it, dated today.
        Cash(db, CashFlowTypeRules.Deposit, 10_000m, "2026-09-02");
        var day = Get(await Svc(db).BuildAsync(User, History(), 110_000m, Today, default), "Day");

        Assert.Equal(10_000m, day.NetExternalFlow);
        Assert.Equal(0m, day.InvestmentGainLoss);
    }

    [Fact]
    public async Task InternalTransferBetweenAccounts_IsUnavailable()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Withdrawal, -3_000m, "2026-09-02", account: "TFSA_D_TD");
        Cash(db, CashFlowTypeRules.Deposit, 3_000m, "2026-09-02", account: "Corp_TD");
        var day = Get(await Svc(db).BuildAsync(User, History(), 100_000m, Today, default), "Day");

        Assert.Equal("Unavailable", day.Status);
        Assert.Null(day.InvestmentGainLoss);
    }

    [Fact]
    public async Task DepositLabelledTransfer_IsUnavailable()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Deposit, 3_000m, "2026-09-02", desc: "Transfer in");
        var r = await Svc(db).GetNetExternalFlowAsync(User, new DateOnly(2026, 9, 1), Today, default);

        Assert.False(r.Available);
    }

    [Theory]
    [InlineData("TFSA_USD", "CASH")]
    [InlineData("TFSA_D_TD", "USD deposit")]
    public async Task UsdFlow_IsUnavailable_NotSubtractedFromCad(string account, string desc)
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Deposit, 1_000m, "2026-09-02", account: account, desc: desc);
        var r = await Svc(db).GetNetExternalFlowAsync(User, new DateOnly(2026, 9, 1), Today, default);

        Assert.False(r.Available);
    }

    [Fact]
    public async Task UnclassifiedRow_IsUnavailable()
    {
        await using var db = CreateDb();
        Cash(db, null!, 1_000m, "2026-09-02");
        var r = await Svc(db).GetNetExternalFlowAsync(User, new DateOnly(2026, 9, 1), Today, default);

        Assert.False(r.Available);
    }

    [Fact]
    public async Task FutureDatedRow_IsUnavailable()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Deposit, 1_000m, "2026-09-10");
        var r = await Svc(db).GetNetExternalFlowAsync(User, new DateOnly(2026, 9, 1), Today, default);

        Assert.False(r.Available);
    }

    [Fact]
    public async Task NonExternalTypes_AreExcluded()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.TradeProceeds, 2_000m, "2026-09-02");
        Cash(db, CashFlowTypeRules.Dividend, 50m, "2026-09-02");
        Cash(db, CashFlowTypeRules.AdjustmentIncrease, 75m, "2026-09-02");
        Cash(db, CashFlowTypeRules.Fee, -5m, "2026-09-02");
        var r = await Svc(db).GetNetExternalFlowAsync(User, new DateOnly(2026, 9, 1), Today, default);

        Assert.True(r.Available);
        Assert.Equal(0m, r.NetFlow);
    }

    [Fact]
    public async Task OtherUsersRows_AreOutOfScope()
    {
        await using var db = CreateDb();
        db.CashItems.Add(new CashItem
        {
            UserId = "someone-else", CashFlowType = CashFlowTypeRules.Deposit, Amount = 9_999m,
            TransactionDate = DateTime.Parse("2026-09-02"),
        });
        db.SaveChanges();
        var r = await Svc(db).GetNetExternalFlowAsync(User, new DateOnly(2026, 9, 1), Today, default);

        Assert.Equal(0m, r.NetFlow);
    }

    [Fact]
    public async Task MissingBaseline_IsUnavailable()
    {
        await using var db = CreateDb();
        var day = Get(await Svc(db).BuildAsync(User, [], 100_000m, Today, default), "Day");

        Assert.Equal("Unavailable", day.Status);
        Assert.Null(day.InvestmentGainLoss);
    }

    [Fact]
    public async Task StaleBaseline_IsUnavailable()
    {
        await using var db = CreateDb();
        var res = await Svc(db).BuildAsync(User, [Row("2026-08-01", 90_000m, 10_000m)], 100_000m, Today, default);

        Assert.Equal("Unavailable", Get(res, "Day").Status);
    }

    [Fact]
    public async Task PendingReseal_IsUpdating()
    {
        await using var db = CreateDb();
        var clock = new MutationClock();
        clock.MarkTodayDirty();
        var res = await Svc(db, clock).BuildAsync(User, History(), 100_000m, Today, default);

        Assert.All(res, p => Assert.Equal("Updating", p.Status));
    }

    [Fact]
    public async Task InconsistentBaseline_IsUnavailable()
    {
        await using var db = CreateDb();
        var bad = Row("2026-09-01", 90_000m, 10_000m);
        bad.TotalValue = 1m;
        var day = Get(await Svc(db).BuildAsync(User, [bad], 100_000m, Today, default), "Day");

        Assert.Equal("Unavailable", day.Status);
    }

    [Fact]
    public async Task Ytd_RequiresLedgerCoverageOfBaseline()
    {
        await using var db = CreateDb(new DateOnly(2026, 3, 1));
        var res = await Svc(db).BuildAsync(User, History(), 100_000m, Today, default);

        Assert.Equal("Unavailable", Get(res, "YTD").Status);
        Assert.Equal("Available", Get(res, "Day").Status);
    }

    [Fact]
    public async Task Ytd_AvailableWithBaselineAndCoverage()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Deposit, 20_000m, "2026-03-15");
        var ytd = Get(await Svc(db).BuildAsync(User, History(), 125_000m, Today, default), "YTD");

        Assert.Equal("Available", ytd.Status);
        Assert.Equal("2025-12-31", ytd.BaselineDate);
        Assert.Equal(5_000m, ytd.InvestmentGainLoss);
    }

    [Fact]
    public async Task Calculation_DoesNotWriteAnything()
    {
        await using var db = CreateDb();
        Cash(db, CashFlowTypeRules.Deposit, 10_000m, "2026-09-02");
        var rowsBefore = await db.CashItems.CountAsync();
        await Svc(db).BuildAsync(User, History(), 110_000m, Today, default);

        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(rowsBefore, await db.CashItems.CountAsync());
        Assert.Equal(0, await db.PortfolioValueHistories.CountAsync());
    }
}
