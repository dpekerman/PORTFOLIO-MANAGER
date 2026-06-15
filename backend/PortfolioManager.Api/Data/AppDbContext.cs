using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PortfolioItem> PortfolioItems => Set<PortfolioItem>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

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
            entity.HasIndex(e => e.Symbol).IsUnique();
        });

        modelBuilder.Entity<WatchlistItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(500).HasDefaultValue("");
            entity.HasIndex(e => e.Symbol).IsUnique();
        });
    }
}
