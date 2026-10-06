using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public sealed class AccountTypeSqlServerFactAttribute : FactAttribute
{
    public AccountTypeSqlServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PM_ACCOUNT_TYPES_SQL_TESTS") != "1")
            Skip = "Set PM_ACCOUNT_TYPES_SQL_TESTS=1 to run isolated SQL Server LocalDB integration tests.";
    }
}

public sealed class AccountTypeSqlServerTests
{
    private static DbContextOptions<AppDbContext> Options() => new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database=PmAccountTypesTest_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True")
        .Options;

    [AccountTypeSqlServerFact]
    public async Task Rename_SerializesAgainstStaleWrites()
    {
        var options = Options();
        await using var db = new AppDbContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var service = new AccountTypeService(db);
            var item = await service.AddAsync("Before", default);
            await using var tx = await AccountTypeWriteGuard.BeginAsync(db, default);
            await using var writer = new AppDbContext(options);
            writer.CashItems.Add(new CashItem { Description = "Stale form", AccountType = "Before", Amount = 100m });
            var pending = writer.SaveChangesAsync();
            await service.RenameAsync(item.Id, new("After", item.Version), default);
            await tx!.CommitAsync();
            Assert.Equal(409, (await Assert.ThrowsAsync<AccountTypeException>(() => pending)).StatusCode);
            Assert.Empty(await db.CashItems.AsNoTracking().ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [AccountTypeSqlServerFact]
    public async Task Delete_SerializesAgainstStaleWrites()
    {
        var options = Options();
        await using var db = new AppDbContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var service = new AccountTypeService(db);
            var item = await service.AddAsync("Unused", default);
            await using var tx = await AccountTypeWriteGuard.BeginAsync(db, default);
            await using var writer = new AppDbContext(options);
            writer.CashItems.Add(new CashItem { Description = "Stale form", AccountType = "Unused" });
            var pending = writer.SaveChangesAsync();
            await service.DeleteAsync(item.Id, item.Version, default);
            await tx!.CommitAsync();
            await Assert.ThrowsAsync<AccountTypeException>(() => pending);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [AccountTypeSqlServerFact]
    public async Task GeneratedUpgradeScript_IsIdempotent_AndDoesNotReseedDeletedDefaults()
    {
        var options = Options();
        await using var db = new AppDbContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("""
                DROP TABLE AccountTypes;
                CREATE TABLE __EFMigrationsHistory (MigrationId nvarchar(150) NOT NULL PRIMARY KEY, ProductVersion nvarchar(32) NOT NULL);
                """);
            var script = db.GetService<IMigrator>().GenerateScript(
                "20261002140402_AddAnalysisCurrencyToDailySignals", "20261006143817_AddAccountTypes",
                MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.OpenConnectionAsync();
            foreach (var batch in System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline))
                if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
            Assert.Equal(7, await db.AccountTypes.CountAsync());
            var service = new AccountTypeService(db);
            var first = (await service.GetAllAsync(default))[0];
            await service.DeleteAsync(first.Id, first.Version, default);
            foreach (var batch in System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline))
                if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
            Assert.Equal(6, await db.AccountTypes.CountAsync());
            var masterPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "database", "SQL", "00_MASTER_DeployProduction.sql"));
            var master = await File.ReadAllTextAsync(masterPath);
            var upgrade = master[master.IndexOf("-- Shared account types.", StringComparison.Ordinal)..];
            foreach (var batch in System.Text.RegularExpressions.Regex.Split(upgrade, @"^\s*GO\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline))
                if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
            Assert.Equal(6, await db.AccountTypes.CountAsync());
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
