using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Phase15Verification;

namespace PortfolioManager.Tests;

public sealed class Phase15EvidenceTests
{
    [Fact]
    public void HistoricalOptionClosesDoNotEstablishVerifiedPositions()
    {
        var export = new LegacyEvidenceExport(DateTime.UtcNow,
            [new(1, 0, 1, 1, "OpeningBalance", null, null)],
            [new(1, "OPEN", 2, 1, 1, "same-contract"), new(2, "CLOSE", 1, 1, 1, "same-contract")],
            224, 0, 0);

        var result = EvidenceManifest.Classify(export);

        Assert.False(result.CompletePortfolioValuationAvailable);
        Assert.Equal(1, result.CandidateGroupsWithOpenAndClose);
        Assert.Equal(0, result.OptionCategories["Verified"]);
        Assert.All(result.Records.Where(row => row.Kind == "Option"),
            row => Assert.Contains("ReconciledOutstandingQuantity", row.MissingEvidence));
        Assert.Contains("CurrencySpecificOpeningOwnership", result.Records[0].MissingEvidence);
    }

    [Fact]
    public void MissingAccountNeedsOwnerAssignmentAndDuplicateIdentitiesAreRejected()
    {
        var export = new LegacyEvidenceExport(DateTime.UtcNow,
            [new(1, 1, 0, 0, null, null, null)], [], 1, 0, 0);
        var result = EvidenceManifest.Classify(export);
        Assert.Equal(1, result.CashCategories["OwnerConfirmationRequired"]);
        Assert.Contains("LedgerEffectiveDate", result.Records[0].MissingEvidence);
        Assert.Contains("LegacyOwnershipConfirmation", result.Records[0].MissingEvidence);
        Assert.Throws<InvalidDataException>(() => EvidenceManifest.Classify(
            export with { Cash = [export.Cash[0], export.Cash[0]] }));
    }

    [Fact]
    public void RehearsalMigrationsAreNotDiscoveredByProductionContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=not-connected.invalid;Database=not-connected;Integrated Security=true").Options;
        using var production = new AppDbContext(options);
        using var rehearsal = new ValuationRehearsalContext(options);
        Assert.DoesNotContain(production.Database.GetMigrations(), name => name.EndsWith("Phase15CurrencyProvenance"));
        Assert.Contains(rehearsal.Database.GetMigrations(), name => name.EndsWith("Phase15CurrencyProvenance"));
    }

    [Fact]
    public async Task CurrencySpecificOpeningsKeepTradeLinksUnique()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ValuationRehearsalContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await InsertCash(db, "CAD", "OpeningBalance");
        await InsertCash(db, "USD", "OpeningBalance");
        await Assert.ThrowsAsync<SqliteException>(() => InsertCash(db, "CAD", "OpeningBalance"));
        await InsertCash(db, "CAD", "TradePurchase", linked: true);
        await Assert.ThrowsAsync<SqliteException>(() => InsertCash(db, "USD", "TradePurchase", linked: true));
        Assert.Equal(3, await db.CashItems.CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("Verified", null)]
    [InlineData("Verified", "EUR")]
    public async Task VerifiedCurrencyCannotBeMissingOrUnsupported(string status, string? currency)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ValuationRehearsalContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO CashItems (Description, Amount, AddedAt, CurrencyVerificationStatus, CurrencyCode)
            VALUES ('synthetic', 10, '2026-11-13', {status}, {currency})
            """));
        Assert.Equal(0, await db.CashItems.CountAsync());
    }

    [Fact]
    public async Task HistoricalEvidenceCannotClaimVerifiedWithoutCurrencyProvenance()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ValuationRehearsalContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO ValuationCurrencyEvidence
                (EntityKind, EntityId, VerificationStatus, EvidenceSource, EffectiveFromUtc, ObservedAtUtc)
            VALUES ('Cash', 1, 'Verified', '', '2026-11-13', '2026-11-13')
            """));
        Assert.Equal(0, await db.CurrencyHistory.CountAsync());
    }

    [Theory]
    [InlineData("SHORT", "Open", 1, 100)]
    [InlineData("LONG", "Open", -1, 100)]
    [InlineData("LONG", "Closed", 1, 100)]
    [InlineData(null, null, 1, 100)]
    [InlineData("LONG", "Open", 1, 0)]
    public async Task InvalidSignedOptionEvidenceCannotBeStored(
        string? side, string? state, int quantity, int multiplier)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ValuationRehearsalContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO OptionItems (UnderlyingTicker, PositionType, ExpirationDate, Strike, Premium,
                NumberOfContracts, MarketPrice, AddedAt, PositionSide, PositionState, SignedOpenContracts, ContractMultiplier)
            VALUES ('TEST', 'CALL', '2026-12-31', 100, 1, 1, 1, '2026-11-13', {side}, {state}, {quantity}, {multiplier})
            """));
        Assert.Equal(0, await db.OptionItems.CountAsync());
    }

    private static Task<int> InsertCash(ValuationRehearsalContext db, string currency, string flow, bool linked = false) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO CashItems (UserId, Description, Amount, AddedAt, AccountType, TransactionDate,
                CashFlowType, ValuationAccountId, CurrencyCode, CurrencyVerificationStatus,
                CurrencyEvidenceSource, CurrencyVerifiedAtUtc, SourceType, SourceItemId)
            VALUES ('synthetic-owner', 'synthetic', 10, '2026-11-13', 'synthetic-account', '2026-11-13',
                {flow}, 'synthetic-account-id', {currency}, 'Verified', 'synthetic statement',
                '2026-11-13', {(linked ? "OptionOpen" : null)}, {(linked ? 42 : (int?)null)})
            """);
}
