using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<PortfolioItem> PortfolioItems => Set<PortfolioItem>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<AdhocAnalysisSession> AdhocAnalysisSessions => Set<AdhocAnalysisSession>();
    public DbSet<CashItem> CashItems => Set<CashItem>();
    public DbSet<OptionItem> OptionItems => Set<OptionItem>();
    public DbSet<DailySignal> DailySignals => Set<DailySignal>();
    public DbSet<StagedSignal> StagedSignals => Set<StagedSignal>();
    public DbSet<AllocationRiskTarget> AllocationRiskTargets => Set<AllocationRiskTarget>();
    public DbSet<AllocationSectorTarget> AllocationSectorTargets => Set<AllocationSectorTarget>();
    public DbSet<SinglePositionLimit> SinglePositionLimits => Set<SinglePositionLimit>();
    public DbSet<ValueScreenerSnapshot> ValueScreenerSnapshots => Set<ValueScreenerSnapshot>();
    public DbSet<ValueScreenerScheduleConfig> ValueScreenerScheduleConfigs => Set<ValueScreenerScheduleConfig>();
    public DbSet<PortfolioValueHistory> PortfolioValueHistories => Set<PortfolioValueHistory>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RsiScanSnapshot> RsiScanSnapshots => Set<RsiScanSnapshot>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<PortfolioSnapshot> PortfolioSnapshots => Set<PortfolioSnapshot>();
    public DbSet<WatchlistSnapshot> WatchlistSnapshots => Set<WatchlistSnapshot>();
    public DbSet<DashboardSnapshot> DashboardSnapshots => Set<DashboardSnapshot>();
    public DbSet<SectorIndustryConfig> SectorIndustryConfigs => Set<SectorIndustryConfig>();
    public DbSet<TransactionContextSnapshot> TransactionContextSnapshots => Set<TransactionContextSnapshot>();
    public DbSet<TechnicalChannel> TechnicalChannels => Set<TechnicalChannel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<PortfolioItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(450);
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
            entity.Property(e => e.HoldingRole).HasMaxLength(20);
            entity.Property(e => e.DecisionSource).HasMaxLength(50);
            entity.Property(e => e.DecisionSourceClosed).HasMaxLength(50);
            entity.HasIndex(e => e.Symbol); // non-unique: same ticker can exist across multiple accounts
        });

        modelBuilder.Entity<CashItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200).HasDefaultValue("CASH");
            entity.Property(e => e.Amount).HasColumnType("decimal(18,4)");
            entity.Property(e => e.AccountType).HasMaxLength(30);
            entity.Property(e => e.TransactionDate).IsRequired(false);
        });

        modelBuilder.Entity<WatchlistItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(500).HasDefaultValue("");
            entity.Property(e => e.Role).HasMaxLength(20).HasDefaultValue("Strategic");
            entity.Property(e => e.IsFavorite).HasDefaultValue(false);
            entity.Property(e => e.EarningsDate).IsRequired(false);
            entity.Property(e => e.WatchlistTier).HasMaxLength(20).HasDefaultValue("Strategic");
            // Per-user duplicate symbols allowed — composite unique index (Symbol, UserId)
            entity.HasIndex(e => new { e.Symbol, e.UserId }).IsUnique();
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

        modelBuilder.Entity<OptionItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.UnderlyingTicker).IsRequired().HasMaxLength(20);
            entity.Property(e => e.PositionType).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Strike).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Premium).HasColumnType("decimal(18,4)");
            entity.Property(e => e.MarketPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TransactionType).HasMaxLength(10);
            entity.Property(e => e.AccountType).HasMaxLength(30);
            entity.Property(e => e.ClosingPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DecisionSource).HasMaxLength(50);
            entity.Property(e => e.DecisionSourceClosed).HasMaxLength(50);
        });

        modelBuilder.Entity<DailySignal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CompanyName).HasMaxLength(200).HasDefaultValue("");
            entity.Property(e => e.ScanType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SignalType).IsRequired().HasMaxLength(30);
            entity.Property(e => e.Rsi).HasColumnType("decimal(7,4)");
            entity.Property(e => e.Price).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TriggerDetails).HasMaxLength(1000).HasDefaultValue("");
            entity.Property(e => e.SignalDate).IsRequired().HasMaxLength(10);
            entity.Property(e => e.RuleVersion).HasMaxLength(20).HasDefaultValue("Legacy");
            entity.Property(e => e.SignalState).HasMaxLength(30).HasDefaultValue("Active");
            entity.Property(e => e.PreviousSignalState).HasMaxLength(30).IsRequired(false);
            entity.Property(e => e.Sector).HasMaxLength(100).HasDefaultValue("");
            entity.Property(e => e.ReversalProbability).HasMaxLength(20).HasDefaultValue("");
            entity.Property(e => e.VolumeSignal).HasMaxLength(30).HasDefaultValue("");
            entity.Property(e => e.TrendShift).HasMaxLength(50);
            entity.Property(e => e.RsiDelta1D).HasColumnType("decimal(18,4)");
            entity.Property(e => e.EntryPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.StopLossPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.RiskPerShare).HasColumnType("decimal(18,4)");
            entity.Property(e => e.PositionSizingShares).HasColumnType("decimal(18,6)");
            entity.Property(e => e.PositionSizingRiskAmount).HasColumnType("decimal(18,4)");
            entity.Property(e => e.PositionSizingPositionValue).HasColumnType("decimal(18,4)");
            entity.Property(e => e.PositionSizingLimitingReason).HasMaxLength(200);
            entity.Property(e => e.Sma200).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Ema9AtEntry).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Fib61_8AtSignal).HasColumnType("decimal(18,4)");
            entity.Property(e => e.FibZoneAtSignal).HasMaxLength(30);
            entity.Property(e => e.FibStatusAtSignal).HasMaxLength(30);
            entity.HasIndex(e => e.Symbol);
            entity.HasIndex(e => e.SignalDate);
            entity.HasIndex(e => new { e.Symbol, e.SignalDate });
        });

        modelBuilder.Entity<StagedSignal>(entity =>
        {
            entity.HasKey(e => e.StagedId);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ScanType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.BasePrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.BaseRsi).HasColumnType("decimal(18,4)");
            entity.Property(e => e.BaseHigh).HasColumnType("decimal(18,4)");
            entity.Property(e => e.BaseLow).HasColumnType("decimal(18,4)");
            entity.Property(e => e.PreviousPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.PreviousRsi).HasColumnType("decimal(18,4)");
            entity.Property(e => e.CurrentPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.CurrentRsi).HasColumnType("decimal(18,4)");
            entity.Property(e => e.RsiDelta1D).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ExtremeLow).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ExtremeHigh).HasColumnType("decimal(18,4)");
            entity.Property(e => e.IsActiveWatch).HasDefaultValue(true);
            entity.HasIndex(e => e.Symbol);
            entity.HasIndex(e => e.IsActiveWatch);
        });

        modelBuilder.Entity<TechnicalChannel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Timeframe).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Direction).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ChannelState).IsRequired().HasMaxLength(30);
            entity.Property(e => e.Slope).HasColumnType("decimal(18,8)");
            entity.Property(e => e.LowerRailCurrent).HasColumnType("decimal(18,4)");
            entity.Property(e => e.UpperRailCurrent).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DistanceToLowerRailPercent).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DistanceToLowerRailATR).HasColumnType("decimal(18,4)");
            entity.Property(e => e.NearestOpenGapAbove).HasColumnType("decimal(18,4)");
            entity.Property(e => e.NearestOpenGapBelow).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DistanceToGapAbovePercent).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DistanceToGapBelowPercent).HasColumnType("decimal(18,4)");
            entity.HasIndex(e => new { e.Ticker, e.Timeframe }).IsUnique();
        });

        modelBuilder.Entity<AllocationRiskTarget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(30);
            entity.Property(e => e.TargetPct).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<AllocationSectorTarget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sector).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TargetPct).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<SinglePositionLimit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(30);
            entity.Property(e => e.TargetPct).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<ValueScreenerSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Origin).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ResultsJson).IsRequired().HasDefaultValue("[]");
            entity.HasIndex(e => new { e.Origin, e.RunAt });
        });

        modelBuilder.Entity<ValueScreenerScheduleConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ScheduledTimeEt).IsRequired().HasMaxLength(10).HasDefaultValue("17:00");
            entity.Property(e => e.Enabled).HasDefaultValue(true);
        });

        modelBuilder.Entity<SectorIndustryConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Single-row upsert table — Id is always explicitly 1, never auto-generated
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.SectorsJson).IsRequired().HasDefaultValue("[]");
            entity.Property(e => e.IndustriesJson).IsRequired().HasDefaultValue("[]");
            entity.Property(e => e.DecisionSourcesJson).IsRequired().HasDefaultValue("[]");
        });

        modelBuilder.Entity<PortfolioValueHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecordedDate).IsRequired().HasMaxLength(10);
            entity.Property(e => e.TotalValue).HasColumnType("decimal(18,4)");
            entity.Property(e => e.StocksValue).HasColumnType("decimal(18,4)");
            entity.Property(e => e.CashValue).HasColumnType("decimal(18,4)");
            entity.Property(e => e.OptionsValue).HasColumnType("decimal(18,4)");
            entity.HasIndex(e => e.RecordedDate);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(64); // hex-encoded SHA-256
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RsiScanSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Single-row upsert table — Id is always explicitly 1, never auto-generated
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.SnapshotJson).IsRequired().HasDefaultValue("{}");
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.PreferenceKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PreferenceValue).IsRequired().HasDefaultValue("");
            entity.HasIndex(e => new { e.UserId, e.PreferenceKey }).IsUnique();
        });

        modelBuilder.Entity<PortfolioSnapshot>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.SnapshotJson).IsRequired().HasDefaultValue("[]");
        });

        modelBuilder.Entity<WatchlistSnapshot>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.SnapshotJson).IsRequired().HasDefaultValue("[]");
        });

        modelBuilder.Entity<DashboardSnapshot>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.SnapshotJson).IsRequired().HasDefaultValue("{}");
        });

        modelBuilder.Entity<TransactionContextSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TrendShiftAtEntry).HasMaxLength(50);
            entity.Property(e => e.FibZoneAtEntry).HasMaxLength(30);
            entity.Property(e => e.VolumeSignalAtEntry).HasMaxLength(30);
            entity.Property(e => e.TurnStrengthAtEntry).HasMaxLength(20);
            entity.Property(e => e.ValueTierAtEntry).HasMaxLength(30);
            entity.Property(e => e.HoldingRoleAtEntry).HasMaxLength(20);
            entity.Property(e => e.SectorAllocationStatusAtEntry).HasMaxLength(20);
            entity.Property(e => e.RsiAtEntry).HasColumnType("decimal(7,4)");
            entity.Property(e => e.ValueScoreAtEntry).HasColumnType("decimal(7,2)");
            entity.HasIndex(e => e.TransactionId);
        });
    }
}
