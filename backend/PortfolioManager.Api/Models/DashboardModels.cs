namespace PortfolioManager.Api.Models;

public sealed class DashboardSnapshot
{
    public string UserId { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed record DashboardResponse(
    DateTime UpdatedAt,
    DashboardSummary Summary,
    IReadOnlyList<DashboardMover> TopMovers,   // top 10
    IReadOnlyList<DashboardMover> BottomMovers, // bottom 10
    IReadOnlyList<DashboardChartPoint> ValueHistory,
    IReadOnlyList<MarketIndexDto> MarketIndices,
    IReadOnlyList<DashboardAllocation> Allocation,
    IReadOnlyList<DashboardEarning> NextSevenDayEarnings,
    DashboardRsiSection? RsiSection = null,
    IReadOnlyList<DashboardAllocation>? RoleAllocation = null);

public sealed record DashboardSummary(
    decimal TotalValue,
    decimal TodayChange,
    decimal TodayChangePercent,
    decimal TodayStocksChange,
    decimal TodayCashChange,
    decimal TodayOptionsChange,
    decimal WeekChange,
    decimal WeekChangePercent,
    decimal MonthChange,
    decimal MonthChangePercent,
    int OversoldCount,
    int OverboughtCount);

public sealed record DashboardMover(
    string Symbol,
    string CompanyName,
    decimal ChangePercent,
    bool IsPortfolio,
    bool IsWatchlist);

public sealed record DashboardChartPoint(string Date, decimal TotalValue);

/// <summary>Sector allocation row with optional target comparison.</summary>
public sealed record DashboardAllocation(
    string Label,
    decimal Value,
    decimal Percent,
    decimal TargetPercent = 0m,
    decimal Delta = 0m,
    string Status = ""); // good | watch-over | watch-under | over | under | no-target

public sealed record DashboardEarning(string Symbol, string CompanyName, DateTime EarningsDate, string Source);

/// <summary>Single signal row shown in the Dashboard RSI panel.</summary>
public sealed record DashboardRsiSignal(
    string Symbol,
    string CompanyName,
    decimal Rsi,
    string MomentumShift,
    string VolumeSignal,
    decimal ReturnPct,
    string Action,
    string SignalStatus); // Confirmed | EodConfirm | EarlyWarning

/// <summary>Aggregated RSI market-signals section for the dashboard.</summary>
public sealed record DashboardRsiSection(
    int OversoldCount,
    int OverboughtCount,
    int NewTodayCount,
    int ActionRequiredCount,
    IReadOnlyList<DashboardRsiSignal> OversoldSignals,
    IReadOnlyList<DashboardRsiSignal> OverboughtSignals);

/// <summary>An existing holding or watchlist item with an active RSI signal, plus a role-aware action recommendation.</summary>
public sealed record PortfolioActionDto(
    string Symbol,
    string CompanyName,
    string HoldingRole,
    string ScanType,
    decimal Rsi,
    string TrendShift,
    string FibZone,
    string ChaseRisk,
    string AllocationStatus,   // "over" | "under" | "on-target" | ""
    string ActionLabel,
    string ActionSeverity,     // "buy" | "trim" | "hold" | "review" | "wait" | "danger"
    string ActionPriority,     // "REQUIRED" | "DEVELOPING" | "INFORMATIONAL"
    bool IsInPortfolio,
    bool IsInWatchlist,
    string ChannelState,
    string ChannelDirection,
    int ChannelQuality,
    int PriorConfirmedLowerTouches,
    decimal LowerRailToday,
    decimal DistanceToLowerRailPercent,
    decimal DistanceToLowerRailATR,
    DateTime? LastLowerTouchDate,
    decimal? NearestOpenGapAbove);

/// <summary>An EOD signal whose lifecycle state changed today.</summary>
public sealed record StateChangeDto(
    int SignalId,
    string Symbol,
    string CompanyName,
    string ScanType,
    string PreviousState,
    string NewState,
    decimal Rsi,
    string TrendShift,
    DateTime ChangedAt);

