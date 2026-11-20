using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Risk;
using Xunit.Abstractions;

namespace PortfolioManager.Tests;

public sealed class Pm033IsolatedSqlFactAttribute : FactAttribute
{
    public Pm033IsolatedSqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PM033_ISOLATED_SQL_TESTS") != "1")
            Skip = "Requires explicit opt-in and an approved isolated restored SQL catalog.";
    }
}

public class Pm033IsolatedSqlTests(ITestOutputHelper output)
{
    private sealed class TestAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class TwoPlanWriterBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writers;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<RiskPlan>().Any(e => e.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref _writers) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private const string Original = "20261028145833_AddTradeEpisodesAndRiskTracking";
    private const string Corrective = "20261028213913_CorrectPm033EpisodeIntegrity";

    [Pm033IsolatedSqlFact]
    public async Task Approved_restore_preserves_data_through_forward_migration_and_transactional_sql_tests()
    {
        var cs = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PM033_ISOLATED_SQL_CONNECTION")
            ?? throw new InvalidOperationException("An isolated SQL connection is required."));
        cs.DataSource.Should().Be("localhost");
        cs.InitialCatalog.Should().StartWith("PortfolioManager_PM033Test_");
        cs.InitialCatalog.Should().Be(Environment.GetEnvironmentVariable("PM033_APPROVED_SQL_DATABASE"));
        cs.InitialCatalog.Should().NotBe("PortfolioManagerLocal");
        await using var connection = new SqlConnection(cs.ConnectionString);
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
        await using var db = new AppDbContext(options);
        var repository = Environment.GetEnvironmentVariable("PM033_REPOSITORY")
            ?? throw new InvalidOperationException("Repository location is required.");
        var before = await Fingerprints(connection);
        var rollback = await File.ReadAllTextAsync(Path.Combine(repository, "database", "SQL",
            "27_Rollback_TradeEpisodesAndRiskTracking.sql"));
        await using (var command = new SqlCommand(rollback, connection))
        {
            var attempt = () => command.ExecuteNonQueryAsync();
            await attempt.Should().ThrowAsync<SqlException>().Where(e => e.Number == 51033);
        }
        (await Fingerprints(connection)).Should().BeEquivalentTo(before);
        output.WriteLine("Populated original rollback refused with 51033; all captured financial hashes unchanged.");
        await using (var reset = new SqlCommand("SET XACT_ABORT OFF; SET TRANSACTION ISOLATION LEVEL READ COMMITTED;", connection))
            await reset.ExecuteNonQueryAsync();

        var migrator = db.GetService<IMigrator>();
        var script = migrator.GenerateScript(Original, Corrective);
        script.Should().Contain("ADD [TradeEpisodeId] uniqueidentifier NULL");
        script.Should().NotContain("UPDATE [");
        script.Should().NotContain("DELETE FROM [dbo]");
        await File.WriteAllTextAsync(Path.Combine(repository, "database", "SQL", "PM033_CorrectiveMigration.sql"), script);
        await migrator.MigrateAsync(Corrective);
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(Corrective);
        (await Fingerprints(connection)).Should().BeEquivalentTo(before);
        (await db.RiskAuditEvents.CountAsync(a => a.TradeEpisodeId != null)).Should().Be(0,
            "legacy audit attribution must not be inferred or rewritten");
        output.WriteLine("Corrective migration applied ONLY to approved isolated catalog; baseline hashes unchanged.");
        var correctiveRollback = await File.ReadAllTextAsync(Path.Combine(repository, "database", "SQL",
            "PM033_CorrectiveRollback.sql"));
        await using (var command = new SqlCommand(correctiveRollback, connection))
        {
            var attempt = () => command.ExecuteNonQueryAsync();
            await attempt.Should().ThrowAsync<SqlException>().Where(e => e.Number == 51033);
        }
        await using (var reset = new SqlCommand("SET XACT_ABORT OFF; SET TRANSACTION ISOLATION LEVEL READ COMMITTED;", connection))
            await reset.ExecuteNonQueryAsync();
        (await Fingerprints(connection)).Should().BeEquivalentTo(before);

        var efDowngrade = () => migrator.MigrateAsync(Original);
        await efDowngrade.Should().ThrowAsync<SqlException>().Where(e => e.Number == 51033);
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(Corrective);
        (await Fingerprints(connection)).Should().BeEquivalentTo(before);
        output.WriteLine("Generated EF downgrade refused populated data (51033); migration and data unchanged.");

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var episodes = new[] {
                new TradeEpisode { AccountType = "PM033_TEST", Symbol = "PM033_SYNTH", OpenDate = DateTime.UtcNow.Date },
                new TradeEpisode { AccountType = "PM033_TEST", Symbol = "PM033_SYNTH", OpenDate = DateTime.UtcNow.Date },
            };
            db.TradeEpisodes.AddRange(episodes);
            await db.SaveChangesAsync();
            var plan = new RiskPlan { AccountType = "PM033_TEST", Symbol = "PM033_SYNTH", TradeEpisodeId = episodes[0].Id };
            db.RiskPlans.AddRange(plan,
                new RiskPlan { AccountType = "PM033_TEST", Symbol = "PM033_SYNTH", TradeEpisodeId = episodes[1].Id },
                new RiskPlan { AccountType = "PM033_TEST", Symbol = "PM033_SYNTH" });
            await db.SaveChangesAsync();
            plan.RowVersion.Should().HaveCount(8);

            await using var other = new AppDbContext(options);
            await other.Database.UseTransactionAsync(transaction.GetDbTransaction());
            other.RiskPlans.Add(new RiskPlan { AccountType = "PM033_TEST", Symbol = "PM033_SYNTH" });
            var duplicate = () => other.SaveChangesAsync();
            await duplicate.Should().ThrowAsync<DbUpdateException>()
                .Where(e => e.InnerException is SqlException && (((SqlException)e.InnerException).Number == 2601 ||
                    ((SqlException)e.InnerException).Number == 2627));
            other.ChangeTracker.Clear();
            var stale = await other.RiskPlans.SingleAsync(p => p.Id == plan.Id);
            var oldVersion = (stale.RowVersion ?? throw new InvalidOperationException("SQL rowversion was not generated.")).ToArray();
            plan.RiskNotes = "first isolated writer";
            await db.SaveChangesAsync();
            plan.RowVersion.Should().NotEqual(oldVersion);
            stale.RiskNotes = "stale isolated writer";
            var staleSave = () => other.SaveChangesAsync();
            await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
            await transaction.RollbackAsync();
        }
        (await Fingerprints(connection)).Should().BeEquivalentTo(before);
        output.WriteLine("NULL-owner/episode uniqueness and real SQL rowversion conflicts verified; synthetic writes rolled back.");
        var originalOrders = await db.BrokerStopRecords.AsNoTracking()
            .Where(s => s.ImportKey != null && s.ImportKey.StartsWith("user-stop-sheet:2026-10-28:")).ToListAsync();
        var admin = new TestAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, originalOrders.First(s => s.UserId != null).UserId!),
                        new Claim(ClaimTypes.Role, "Admin")], "isolated-readonly-audit")),
            },
        };
        var workspace = await new SwingTradingService(db, admin, new ConfigurationBuilder().Build()).GetOpenTradesAsync();
        var visibleOrderIds = workspace.Rows.SelectMany(row => row.BrokerStops).Select(s => s.Id)
            .Concat(workspace.BrokerOrderReminders.Select(r => r.Stop.Id)).ToHashSet();
        originalOrders.Where(s => BrokerUserStatuses.IsActive(s.UserStatus))
            .Should().OnlyContain(s => visibleOrderIds.Contains(s.Id), "active original orders must remain visible");
        output.WriteLine("Original active imported orders remain visible in current rows or explicit broker reminders.");
        await EmptyRollbackTests(cs, correctiveRollback, rollback);
    }

    private async Task EmptyRollbackTests(SqlConnectionStringBuilder restored, string corrective, string original)
    {
        var temporary = new SqlConnectionStringBuilder(restored.ConnectionString)
        {
            InitialCatalog = restored.InitialCatalog + "_Rollback_" + Guid.NewGuid().ToString("N"),
        };
        temporary.InitialCatalog.Should().StartWith("PortfolioManager_PM033Test_");
        await using var empty = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(temporary.ConnectionString).Options);
        var created = await empty.Database.EnsureCreatedAsync();
        created.Should().BeTrue("the generated isolated catalog must not already exist");
        try
        {
            await empty.Database.OpenConnectionAsync();
            var connection = (SqlConnection)empty.Database.GetDbConnection();
            await SqlLifecycleTests(empty);
            await using (var ledger = new SqlCommand("""
                CREATE TABLE dbo.__EFMigrationsHistory(MigrationId nvarchar(150) NOT NULL PRIMARY KEY, ProductVersion nvarchar(32) NOT NULL);
                """, connection))
                await ledger.ExecuteNonQueryAsync();
            foreach (var id in empty.GetService<IMigrationsAssembly>().Migrations.Keys
                .Where(k => string.CompareOrdinal(k, Corrective) <= 0))
            {
                await using var insert = new SqlCommand(
                    "INSERT dbo.__EFMigrationsHistory VALUES (@id, '8.0.10')", connection);
                insert.Parameters.AddWithValue("@id", id);
                await insert.ExecuteNonQueryAsync();
            }
            await empty.GetService<IMigrator>().MigrateAsync(Original);
            (await empty.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE '%CorrectPm033%'")
                .SingleAsync()).Should().Be(0);
            (await empty.Database.SqlQueryRaw<int>(
                "SELECT COALESCE(COL_LENGTH('dbo.RiskAuditEvents','TradeEpisodeId'),0) AS [Value]")
                .SingleAsync()).Should().Be(0, "EF downgrade of an empty catalog is allowed");
            output.WriteLine("Generated EF Down succeeded only on an empty generated catalog.");
            foreach (var script in new[] { corrective, corrective, original, original })
            {
                await using var command = new SqlCommand(script, connection);
                await command.ExecuteNonQueryAsync();
            }

            async Task SqlLifecycleTests(AppDbContext db)
            {
                db.AccountTypes.Add(new AccountType { Name = "PM033_TEST", NormalizedName = "PM033_TEST" });
                await db.SaveChangesAsync();
                static IHttpContextAccessor Caller(string user) => new TestAccessor
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, user), new Claim(ClaimTypes.Role, "Trader")], "sql-test")),
                    },
                };
                var caller = Caller("pm033-sql-one");
                var portfolio = new PortfolioService(db, null!, new MutationClock(), caller);
                var second = new PortfolioService(db, null!, new MutationClock(), Caller("pm033-sql-two"));
                var request = new AddPortfolioItemRequest("PM033_SYNTH", "Synthetic", 100, 10, "OPEN", "PM033_TEST",
                    new DateTime(2026, 9, 1), HoldingRole: "Swing");
                var swing = await portfolio.AddAsync(request);
                var other = await second.AddAsync(request);
                var firstEpisode = (await db.PortfolioItems.FindAsync(swing.Id))!.TradeEpisodeId;
                var otherEpisode = (await db.PortfolioItems.FindAsync(other.Id))!.TradeEpisodeId;
                (firstEpisode == otherEpisode).Should().BeFalse();
                await portfolio.AddAsync(request with { HoldingRole = "Strategic" });
                var risk = new RiskService(db, caller);
                await risk.SaveBrokerStopAsync(new SaveBrokerStopRequest(null, "PM033_TEST", "PM033_SYNTH",
                    "StopMarket", 9, null, 100, null, null, null, null, null, false, null));
                await portfolio.UpdateAsync(swing.Id, new UpdatePortfolioItemRequest("Synthetic", 100, 10,
                    TransactionType: "CLOSE", AccountType: "PM033_TEST", OpenDate: request.OpenDate,
                    CloseDate: new DateTime(2026, 9, 10), ClosingPrice: 11, HoldingRole: "Swing"));
                (await db.TradeEpisodes.FindAsync(firstEpisode))!.Status.Should().Be(EpisodeStatuses.Closed);
                (await db.TradeEpisodes.FindAsync(otherEpisode))!.Status.Should().Be(EpisodeStatuses.Open);
                var stop = await db.BrokerStopRecords.SingleAsync();
                stop.UserStatus.Should().Be(BrokerUserStatuses.UserConfirmedEstablished);
                stop.TradeEpisodeId.Should().Be(firstEpisode);
                var reopened = await portfolio.AddAsync(request with { OpenDate = new DateTime(2026, 9, 11) });
                ((await db.PortfolioItems.FindAsync(reopened.Id))!.TradeEpisodeId == firstEpisode).Should().BeFalse();
                var position = await risk.GetPositionAsync("PM033_TEST", "PM033_SYNTH");
                position.BrokerStops.Single().BelongsToCurrentEpisode.Should().BeFalse();
                position.Facts!.AverageCost.Should().Be(10);
                var barrier = new TwoPlanWriterBarrier();
                async Task<bool> CreatePlan(string thesis)
                {
                    await using var racing = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                        .UseSqlServer(db.Database.GetConnectionString()).AddInterceptors(barrier).Options);
                    try
                    {
                        await new RiskService(racing, Caller("pm033-sql-one")).SavePlanAsync(new SaveRiskPlanRequest(
                            "PM033_TEST", "PM033_SYNTH", 9, null, "Fixed", null, thesis, null, null, null));
                        return true;
                    }
                    catch (RiskConcurrencyException) { return false; }
                }
                var outcomes = await Task.WhenAll(CreatePlan("first racing writer"), CreatePlan("second racing writer"));
                outcomes.Count(won => won).Should().Be(1);
                (await db.RiskPlans.CountAsync()).Should().Be(1);
                (await db.RiskAuditEvents.CountAsync(e => e.EventType == "PlanCreated")).Should().Be(1,
                    "the rejected create must not leave an audit event");
                output.WriteLine("Simultaneous plan creates: one committed, one explicit RiskConcurrencyException, no orphan audit.");

                var count = await db.TradeEpisodes.CountAsync();
                var failure = () => portfolio.AddAsync(request with { Symbol = "PM033_BAD", CompanyName = new string('X', 5000) });
                await failure.Should().ThrowAsync<DbUpdateException>();
                db.ChangeTracker.Clear();
                (await db.TradeEpisodes.CountAsync()).Should().Be(count, "failed writes must not leave an episode");
                (await db.PortfolioItems.CountAsync(p => p.Symbol == "PM033_BAD")).Should().Be(0);

                string[] users = ["pm033-sql-one", "pm033-sql-two"];
                await db.RiskAuditEvents.Where(e => users.Contains(e.UserId!)).ExecuteDeleteAsync();
                await db.AuditLogs.Where(e => users.Contains(e.UserId!)).ExecuteDeleteAsync();
                await db.BrokerStopRecords.Where(e => users.Contains(e.UserId!)).ExecuteDeleteAsync();
                await db.RiskPlans.Where(e => users.Contains(e.UserId!)).ExecuteDeleteAsync();
                await db.PortfolioItems.Where(e => users.Contains(e.UserId!)).ExecuteDeleteAsync();
                await db.TradeEpisodes.Where(e => users.Contains(e.UserId!)).ExecuteDeleteAsync();
                output.WriteLine("Real SQL portfolio transactions verified: owner separation, mixed-role close/reopen, unchanged order, failure atomicity.");
            }
            await using var check = new SqlCommand("""
                SELECT CASE WHEN OBJECT_ID('dbo.TradeEpisodes') IS NULL
                    AND OBJECT_ID('dbo.RiskPlans') IS NULL AND OBJECT_ID('dbo.BrokerStopRecords') IS NULL
                    AND OBJECT_ID('dbo.RiskAuditEvents') IS NULL
                    AND OBJECT_ID('dbo.PortfolioItems') IS NOT NULL
                    AND COL_LENGTH('dbo.PortfolioItems', 'TradeEpisodeId') IS NULL
                    AND NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE '%Pm033%'
                        OR MigrationId LIKE '%TradeEpisodesAndRiskTracking%') THEN 1 ELSE 0 END
                """, connection);
            (await check.ExecuteScalarAsync()).Should().Be(1);
            output.WriteLine("Both guarded rollbacks succeeded on a separate EMPTY catalog, including repeat execution.");
        }
        finally
        {
            await empty.Database.CloseConnectionAsync();
            await empty.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<Dictionary<string, string>> Fingerprints(SqlConnection connection)
    {
        string[] tables = ["TradeEpisodes", "PortfolioItems", "RiskPlans", "BrokerStopRecords", "RiskAuditEvents",
            "AuditLogs", "AccountTypes", "CashItems", "OptionItems", "PortfolioSnapshots", "PortfolioValueHistories",
            "CashLedgerSettings", "TransactionContextSnapshots", "UnlinkedTradeBaselines"];
        var result = new Dictionary<string, string>();
        foreach (var table in tables)
        {
            await using var command = new SqlCommand($"SELECT * FROM dbo.[{table}]", connection);
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<string>();
            while (await reader.ReadAsync())
            {
                var values = new SortedDictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    if (table == "RiskAuditEvents" && reader.GetName(i) == "TradeEpisodeId") continue;
                    values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(JsonSerializer.Serialize(values));
            }
            rows.Sort(StringComparer.Ordinal);
            result[table] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", rows))));
        }
        return result;
    }
}
