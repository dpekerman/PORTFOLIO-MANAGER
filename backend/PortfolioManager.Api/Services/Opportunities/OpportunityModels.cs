namespace PortfolioManager.Api.Services.Opportunities;

/// <summary>Verified | Provisional | Insufficient | Unavailable (docs/pm-029-trading-opportunities.md §3.3).</summary>
public enum ScoreStatus { Verified, Provisional, Insufficient, Unavailable }

/// <summary>Strong | Attractive | Moderate | Caution | Weak | None (N/A).</summary>
public enum ScoreBand { None, Weak, Caution, Moderate, Attractive, Strong }

public enum ComponentState { Observed, Partial, Missing }

public enum ScorePerspective { Swing, Strategic }

/// <summary>One scored sub-item. <see cref="Available"/> false removes its max from the available total.</summary>
public sealed record ScoreSubItem(string Code, string Label, decimal Max, decimal Points, bool Available, string? Detail = null);

public sealed record ScoreComponent(
    string Code,
    string Name,
    decimal Max,
    bool Essential,
    ComponentState State,
    decimal Awarded,
    decimal AvailableMax,
    IReadOnlyList<ScoreSubItem> Items,
    IReadOnlyList<string> MissingReasons,
    string? Note = null);

/// <summary>
/// Headline result for one perspective. <see cref="Score"/> is only set for Verified/Provisional. For Swing-v2,
/// <see cref="ScoreDenominatorMax"/> is the fixed 80-point technical pool and <see cref="AvailableMax"/> is observed evidence.
/// </summary>
public sealed record ScoreResult(
    ScorePerspective Perspective,
    string ScoringVersion,
    int? Score,
    int? NormalizedScore,
    decimal AwardedTotal,
    decimal AvailableMax,
    decimal CompletenessPct,
    ScoreStatus Status,
    ScoreBand Band,
    IReadOnlyList<ScoreComponent> Components,
    IReadOnlyList<string> ReasonCodes,
    decimal? ScoreDenominatorMax = null);

public static class OpportunityReasons
{
    public const string NoValidStop = "NoValidStop";
    public const string DynamicStopMissing = "DynamicStopMissing";
    public const string StopAtOrAboveEntry = "StopAtOrAboveEntry";
    public const string StopOutsideVolatilityBand = "StopOutsideVolatilityBand";
    public const string NoStructuralTarget = "NoStructuralTarget";
    public const string NoEntryPrice = "NoEntryPrice";
    public const string StaleScan = "StaleScan";
    public const string NoScan = "NoScan";
    public const string LowCompleteness = "LowCompleteness";
    public const string IdentityUnresolved = "IdentityUnresolved";
    public const string EvaluationFailed = "EvaluationFailed";
    public const string ExpectedSessionMismatch = "ExpectedSessionMismatch";
    public const string InvalidAnalysisPrice = "InvalidAnalysisPrice";
    public const string InvalidScanTimestamp = "InvalidScanTimestamp";
    public const string ScanTimestampInFuture = "ScanTimestampInFuture";
    public const string ScanBeforeSessionClose = "ScanBeforeSessionClose";
    public const string InvalidRsi = "InvalidRsi";
    public const string InvalidRsiSignal = "InvalidRsiSignal";
    public const string InvalidMovingAverage = "InvalidMovingAverage";
    public const string InvalidSma200 = "InvalidSma200";
    public const string InvalidEma20 = "InvalidEma20";
    public const string InvalidVolume = "InvalidVolume";
    public const string InvalidVolumeRatio = "InvalidVolumeRatio";
    public const string InvalidChangePercent = "InvalidChangePercent";
    public const string InvalidAtr = "InvalidAtr";
    public const string FundamentalsStale = "FundamentalsStale";
    public const string FundamentalsMissing = "FundamentalsMissing";
    /// <summary>Value Screener snapshot written before PM-029 (no per-factor availability): fail-closed.</summary>
    public const string FundamentalsLegacy = "FundamentalsLegacy";
    public const string FundamentalsInsufficientFactors = "FundamentalsInsufficientFactors";
    public static string MissingEssential(string component) => $"MissingEssential:{component}";
}

// ── Sector Heat ──────────────────────────────────────────────────────────────

public enum SectorHeatStatus { Matched, Unmapped, Stale, InsufficientObservations, CurrencyMismatch, PossibleCorporateAction, Unclassified }

public enum SectorHeatDirection { None, Positive, Neutral, Negative }

public sealed record SectorWindowResult(
    int Sessions,
    SectorHeatStatus Status,
    DateOnly? StartSession,
    DateOnly? EndSession,
    decimal? StockReturnPct,
    decimal? BenchmarkReturnPct,
    /// <summary>Full precision percentage-point difference (stock − benchmark).</summary>
    decimal? DiffPp,
    /// <summary>DiffPp rounded half-away-from-zero to 0.1 — the value shown and used for the neutral band and scoring.</summary>
    decimal? DisplayDiffPp,
    SectorHeatDirection Direction,
    int Intensity,
    int StockObservations,
    int BenchmarkObservations,
    int ExpectedObservations)
{
    public bool IsAvailable => Status == SectorHeatStatus.Matched && DisplayDiffPp is not null;
}

public sealed record SectorRelativeResult(
    SectorHeatStatus Status,
    string? BenchmarkSymbol,
    string? BenchmarkName,
    DateOnly ExpectedSession,
    SectorWindowResult D20,
    SectorWindowResult D60,
    string Methodology);

// ── Trade setup ──────────────────────────────────────────────────────────────

public enum SetupKind { ValidCandidate, NotActionable, Unavailable }

public sealed record TradeSetupResult(
    SetupKind Kind,
    decimal? Entry,
    decimal? Stop,
    decimal? Target,
    string? TargetSource,
    decimal? RewardRisk,
    decimal? RiskPct,
    decimal? RewardPct,
    decimal? StopDistanceAtr,
    string? Currency,
    IReadOnlyList<string> ReasonCodes)
{
    /// <summary>A geometrically valid candidate (valid or not actionable) independent of Swing technical scoring.</summary>
    public bool IsScorable => Kind is SetupKind.ValidCandidate or SetupKind.NotActionable;
}

public enum RiskCategory { Unavailable, Low, Medium, High }

public sealed record RiskAssessment(RiskCategory Category, decimal? RiskToStopPct, decimal? AtrPct, bool HardStructuralNegative, string Basis);

// ── Portfolio Fit ────────────────────────────────────────────────────────────

public enum FitStatus { Eligible, Review, NoAdd, Unavailable }

/// <summary>
/// Portfolio evidence for one symbol, taken from the authoritative Final Action evaluation (PortfolioEvidence / GrandTotal
/// denominators). Fit never recomputes a valuation.
/// </summary>
public sealed record FitInput(
    bool Owned,
    bool AllocationReliable,
    double? PositionSizePct,
    double? PositionLimitPct,
    bool RolePolicyAmbiguous,
    bool SectorKnown,
    double? SectorPct,
    double? SectorTargetPct);

public sealed record FitResult(
    FitStatus Status,
    IReadOnlyList<string> ReasonCodes,
    double? WeightPct,
    double? PositionLimitPct,
    double? PositionHeadroomPct,
    double? SectorWeightPct,
    double? SectorTargetPct,
    double? SectorHeadroomPct);

public static class FitReasons
{
    public const string AllocationUnverifiable = "AllocationUnverifiable";
    public const string ExposureUnavailable = "ExposureUnavailable";
    public const string PositionAtLimit = "PositionAtLimit";
    public const string PositionNearLimit = "PositionNearLimit";
    public const string NoPositionLimit = "NoPositionLimit";
    public const string RolePolicyAmbiguous = "RolePolicyAmbiguous";
    public const string SectorOverTarget = "SectorOverTarget";
    public const string SectorAboveTarget = "SectorAboveTarget";
    public const string SectorUnknown = "SectorUnknown";
    public const string SectorNoTarget = "SectorNoTarget";
    public const string SectorExposureUnavailable = "SectorExposureUnavailable";
}
