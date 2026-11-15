using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Phase15Verification;

public sealed class ValuationRehearsalContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    public DbSet<ValuationCurrencyHistory> CurrencyHistory => Set<ValuationCurrencyHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var type in new[] { typeof(CashItem), typeof(OptionItem) })
        {
            var entity = modelBuilder.Entity(type);
            entity.Property<string>("ValuationAccountId").HasMaxLength(100);
            entity.Property<string>("CurrencyCode").HasColumnType("char(3)");
            entity.Property<string>("CurrencyVerificationStatus").HasMaxLength(20);
            entity.Property<string>("CurrencyEvidenceSource").HasMaxLength(200);
            entity.Property<DateTime?>("CurrencyVerifiedAtUtc");
            entity.Property<DateTime?>("CurrencyVerifiedAtUtc").Metadata.SetValueConverter(
                modelBuilder.Entity<CashItem>().Property(row => row.ModifiedAt).Metadata.GetValueConverter());
            entity.ToTable(table => table.HasCheckConstraint($"CK_{type.Name}s_VerifiedCurrency",
                "[CurrencyVerificationStatus] IS NULL OR [CurrencyVerificationStatus] <> 'Verified' OR " +
                "([CurrencyCode] IS NOT NULL AND [CurrencyCode] IN ('CAD','USD') " +
                "AND [ValuationAccountId] IS NOT NULL AND LTRIM(RTRIM([ValuationAccountId])) <> '' " +
                "AND [CurrencyEvidenceSource] IS NOT NULL AND LTRIM(RTRIM([CurrencyEvidenceSource])) <> '' " +
                "AND [CurrencyVerifiedAtUtc] IS NOT NULL)"));
        }
        var cash = modelBuilder.Entity<CashItem>();
        cash.HasIndex(row => new { row.AccountType, row.TransactionDate })
            .IsUnique().HasFilter("[CashFlowType] = 'OpeningBalance' AND [CurrencyCode] IS NULL")
            .HasDatabaseName("IX_CashItems_Account_OpeningBalance");
        cash.HasIndex("UserId", "ValuationAccountId", "CurrencyCode", "TransactionDate")
            .IsUnique().HasFilter("[CashFlowType] = 'OpeningBalance' AND [CurrencyCode] IS NOT NULL")
            .HasDatabaseName("IX_CashItems_Currency_OpeningBalance");

        var option = modelBuilder.Entity<OptionItem>();
        option.Property<string>("ContractIdentity").HasMaxLength(100);
        option.Property<string>("PositionSide").HasMaxLength(5);
        option.Property<string>("PositionState").HasMaxLength(6);
        option.Property<int?>("SignedOpenContracts");
        option.Property<string>("PositionSource").HasMaxLength(200);
        option.Property<DateTime?>("PositionAsOfUtc");
        option.Property<decimal?>("ContractMultiplier").HasColumnType("decimal(18,6)");
        option.Property<string>("TermsSource").HasMaxLength(200);
        option.Property<DateTime?>("TermsVerifiedAtUtc");
        option.Property<string>("QuoteCurrency").HasColumnType("char(3)");
        option.Property<string>("QuoteSource").HasMaxLength(200);
        option.Property<DateTime?>("QuoteAsOfUtc");
        foreach (var name in new[] { "PositionAsOfUtc", "TermsVerifiedAtUtc", "QuoteAsOfUtc" })
            option.Property<DateTime?>(name).Metadata.SetValueConverter(
                modelBuilder.Entity<CashItem>().Property(row => row.ModifiedAt).Metadata.GetValueConverter());
        option.HasIndex("UserId", "ValuationAccountId", "ContractIdentity")
            .IsUnique().HasFilter("[PositionState] = 'Open' AND [ContractIdentity] IS NOT NULL")
            .HasDatabaseName("IX_OptionItems_ValuationOpenContract");
        option.ToTable(table =>
        {
            table.HasCheckConstraint("CK_OptionItems_PositionSide",
                "[PositionSide] IS NULL OR [PositionSide] IN ('LONG','SHORT')");
            table.HasCheckConstraint("CK_OptionItems_PositionState",
                "[PositionState] IS NULL OR [PositionState] IN ('Open','Closed')");
            table.HasCheckConstraint("CK_OptionItems_Multiplier",
                "[ContractMultiplier] IS NULL OR [ContractMultiplier] > 0");
            table.HasCheckConstraint("CK_OptionItems_SignedPosition",
                "[SignedOpenContracts] IS NULL OR ([PositionState] IS NOT NULL AND " +
                "(([PositionState] = 'Closed' AND [SignedOpenContracts] = 0) " +
                "OR ([PositionState] = 'Open' AND [PositionSide] IS NOT NULL AND " +
                "(([PositionSide] = 'LONG' AND [SignedOpenContracts] > 0) " +
                "OR ([PositionSide] = 'SHORT' AND [SignedOpenContracts] < 0)))))");
            table.HasCheckConstraint("CK_OptionItems_QuoteCurrency",
                "[QuoteCurrency] IS NULL OR ([CurrencyCode] IS NOT NULL AND [QuoteCurrency] = [CurrencyCode])");
        });
        modelBuilder.Entity<ValuationCurrencyHistory>(entity =>
        {
            entity.ToTable("ValuationCurrencyEvidence", table =>
            {
                table.HasCheckConstraint("CK_ValuationEvidence_Dates", "[ObservedAtUtc] >= [EffectiveFromUtc]");
                table.HasCheckConstraint("CK_ValuationEvidence_EntityKind", "[EntityKind] IN ('Cash','Option')");
                table.HasCheckConstraint("CK_ValuationEvidence_VerifiedCurrency",
                    "[VerificationStatus] <> 'Verified' OR ([CurrencyCode] IS NOT NULL " +
                    "AND [CurrencyCode] IN ('CAD','USD') AND [ValuationAccountId] IS NOT NULL " +
                    "AND LTRIM(RTRIM([ValuationAccountId])) <> '' AND LTRIM(RTRIM([EvidenceSource])) <> '')");
            });
            entity.HasKey(row => row.Id);
            entity.Property(row => row.EntityKind).HasMaxLength(10);
            entity.Property(row => row.ValuationAccountId).HasMaxLength(100);
            entity.Property(row => row.CurrencyCode).HasColumnType("char(3)");
            entity.Property(row => row.VerificationStatus).HasMaxLength(20);
            entity.Property(row => row.EvidenceSource).HasMaxLength(200);
            var utcConverter = modelBuilder.Entity<CashItem>().Property(row => row.AddedAt).Metadata.GetValueConverter();
            entity.Property(row => row.EffectiveFromUtc).Metadata.SetValueConverter(utcConverter);
            entity.Property(row => row.ObservedAtUtc).Metadata.SetValueConverter(utcConverter);
            entity.HasOne<ValuationCurrencyHistory>().WithMany()
                .HasForeignKey(row => row.SupersedesEvidenceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(row => new { row.EntityKind, row.EntityId, row.EffectiveFromUtc });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("Phase 15 tooling cannot write financial records.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default) =>
        throw new InvalidOperationException("Phase 15 tooling cannot write financial records.");
}

public sealed class ValuationCurrencyHistory
{
    public long Id { get; set; }
    public string EntityKind { get; set; } = "";
    public int EntityId { get; set; }
    public string? ValuationAccountId { get; set; }
    public string? CurrencyCode { get; set; }
    public string VerificationStatus { get; set; } = "";
    public string EvidenceSource { get; set; } = "";
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public long? SupersedesEvidenceId { get; set; }
}

public sealed class ValuationRehearsalContextFactory : IDesignTimeDbContextFactory<ValuationRehearsalContext>
{
    public ValuationRehearsalContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=PHASE15-NOT-CONNECTED.invalid;Database=PM029_Phase15_Disposable;Integrated Security=true;TrustServerCertificate=false")
            .Options);
}
