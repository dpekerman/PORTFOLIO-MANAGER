using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PortfolioItem> PortfolioItems => Set<PortfolioItem>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<AdhocAnalysisSession> AdhocAnalysisSessions => Set<AdhocAnalysisSession>();
    public DbSet<CashItem> CashItems => Set<CashItem>();
    public DbSet<OptionItem> OptionItems => Set<OptionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PortfolioItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Shares).HasColumnType("decimal(18,6)");
            entity.Property(e => e.AverageCostBasis).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Sector).HasMaxLength(100).HasDefaultValue("");
            entity.Property(e => e.Industry).HasMaxLength(100).HasDefaultValue("");
            entity.Property(e => e.SectorIsOverridden).HasDefaultValue(false);
            entity.Property(e => e.IsManual).HasDefaultValue(false);
            entity.Property(e => e.ManualMarketValue).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TransactionType).HasMaxLength(10);
            entity.Property(e => e.AccountType).HasMaxLength(30);
            entity.Property(e => e.ClosingPrice).HasColumnType("decimal(18,4)");
            entity.HasIndex(e => e.Symbol); // non-unique: same ticker can exist across multiple accounts
        });

        modelBuilder.Entity<WatchlistItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(500).HasDefaultValue("");
            entity.HasIndex(e => e.Symbol).IsUnique();
        });

        modelBuilder.Entity<AdhocAnalysisSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionKey).IsRequired().HasMaxLength(100).HasDefaultValue("default");
            entity.Property(e => e.Symbols).IsRequired().HasDefaultValue("[]");
            entity.Property(e => e.OversoldThreshold).HasColumnType("decimal(5,2)").HasDefaultValue(30m);
            entity.Property(e => e.OverboughtThreshold).HasColumnType("decimal(5,2)").HasDefaultValue(75m);
            entity.Property(e => e.LogicMode).HasMaxLength(20).HasDefaultValue("Legacy");
            entity.HasIndex(e => new { e.SessionKey, e.UpdatedAt });
        });

        modelBuilder.Entity<CashItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200).HasDefaultValue("CASH");
            entity.Property(e => e.Amount).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<OptionItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnderlyingTicker).IsRequired().HasMaxLength(20);
            entity.Property(e => e.PositionType).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Strike).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Premium).HasColumnType("decimal(18,4)");
            entity.Property(e => e.MarketPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TransactionType).HasMaxLength(10);
            entity.Property(e => e.AccountType).HasMaxLength(30);
            entity.Property(e => e.ClosingPrice).HasColumnType("decimal(18,4)");
        });
    }
}
