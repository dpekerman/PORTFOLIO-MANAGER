using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Api.Services.Opportunities;

// PM-029 API contracts. Every value is computed server-side once per refresh and stored in an immutable snapshot; the grid,
// the drawer and the export all read the same snapshotId. Nothing here is a prototype literal.

public sealed record OpportunityScoreSummaryDto(
    int? Score,
    decimal CompletenessPct,
    string Status,
    string Band,
    IReadOnlyList<string> ReasonCodes);

public sealed record OpportunitySectorHeatDto(
    /// <summary>Displayed 20D difference (0.1 pp, signed); null when unavailable.</summary>
    decimal? DiffPp,
    decimal? Diff60Pp,
    decimal? StockReturnPct,
    decimal? BenchmarkReturnPct,
    string Status,
    string Direction,
    int Intensity,
    string? BenchmarkSymbol);

public sealed record OpportunitySetupDto(
    string Kind,
    decimal? Entry,
    decimal? Stop,
    decimal? Target,
    string? TargetSource,
    decimal? RewardRisk,
    decimal? RiskPct,
    decimal? RewardPct,
    decimal? StopDistanceAtr,
    string? Currency,
    IReadOnlyList<string> ReasonCodes);

public sealed record OpportunityRiskDto(string Category, decimal? RiskToStopPct, decimal? AtrPct, bool HardStructuralNegative, string Basis);

public sealed record OpportunityFitDto(
    string Status,
    IReadOnlyList<string> ReasonCodes,
    double? WeightPct,
    double? PositionLimitPct,
    double? PositionHeadroomPct,
    double? SectorWeightPct,
    double? SectorTargetPct,
    double? SectorHeadroomPct);

/// <summary>
/// Grid summary of the authoritative Final Action decisions. <see cref="Kind"/> = Single | Multiple | None. Multiple means the
/// symbol's account/lot decisions differ: the grid shows a neutral "Multiple (n)" chip and the drawer lists every original decision.
/// </summary>
public sealed record OpportunityFinalActionDto(
    string Kind,
    string? Label,
    string? FinalActionId,
    string? Category,
    string? FinalActionClass,
    bool IsActionable,
    string? EvaluationState,
    string? EvidenceHealth,
    string? Reason,
    int DecisionCount,
    IReadOnlyList<string> DistinctLabels);

public sealed record OpportunityIndicatorsDto(
    decimal? Price,
    decimal? ChangePct,
    decimal? Rsi14,
    decimal? RsiSignal,
    decimal? MacdHistogram,
    decimal? Ema20,
    decimal? Sma50,
    decimal? Sma200,
    decimal? Atr,
    decimal? VolumeRatio,
    decimal? Week52High,
    decimal? Week52Low,
    decimal? DynamicStopLoss,
    string? TrendShift,
    string? MaStructure,
    string? FibZone,
    string? ChannelState,
    decimal? Rs20Pp,
    decimal? Rs60Pp);

public sealed record OpportunityFreshnessDto(
    DateOnly? DataAsOfDate,
    DateOnly ExpectedSession,
    DateTime? ScannedAtUtc,
    bool IsStale,
    DateTime? FundamentalsAsOfUtc,
    string? FundamentalsOrigin);

public sealed record OpportunityRowDto(
    string Symbol,
    string CompanyName,
    string? AnalysisTicker,
    bool UsesUnderlying,
    string Exchange,
    string? Currency,
    IReadOnlyList<string> Sources,
    bool Owned,
    IReadOnlyList<string> Accounts,
    IReadOnlyList<string> Roles,
    string? WatchlistRole,
    string? Sector,
    string? Industry,
    string SectorSource,
    OpportunityScoreSummaryDto Swing,
    OpportunityScoreSummaryDto Strategic,
    OpportunitySectorHeatDto SectorHeat,
    OpportunitySetupDto Setup,
    OpportunityRiskDto Risk,
    OpportunityFitDto Fit,
    OpportunityFinalActionDto FinalAction,
    OpportunityIndicatorsDto Indicators,
    OpportunityFreshnessDto Freshness,
    /// <summary>Ok | IdentityUnresolved | NoScan | StaleScan.</summary>
    string RowStatus,
    string? InstrumentType = null,
    SetupQualityResult? SetupQuality = null,
    SetupQualityResult? StrategicSetupQuality = null);

/// <summary>"Your saved risk plan" — display only, never scored, in the trading-security currency.</summary>
public sealed record SavedRiskPlanDto(
    string Account,
    decimal? AverageCost,
    decimal? PlannedStop,
    decimal? Target,
    decimal? RewardRisk,
    string StopType,
    string? Currency,
    DateTime UpdatedAtUtc);

public sealed record OpportunityEvidenceDto(
    string ScoringVersion,
    string Disclaimer,
    string SectorMethodology,
    string HistorySource,
    string? BenchmarkSymbol,
    string? BenchmarkName,
    string SectorSource,
    DateTime? ScanAsOfUtc,
    DateOnly? ScanTradingDate,
    DateOnly ExpectedSession,
    string? FundamentalsOrigin,
    DateTime? FundamentalsAsOfUtc,
    string? EarningsYieldBasis,
    string? FcfYieldBasis,
    string? CapitalEfficiencyBasis,
    string? AllocationStatus,
    DateTime? PortfolioUpdatedAtUtc,
    string FinalActionEvaluationId,
    DateTime FinalActionComputedAtUtc,
    IReadOnlyList<string> Warnings,
    string? BenchmarkCategory = null,
    string? BenchmarkCurrency = null,
    string? BenchmarkMappingSource = null,
    string? BenchmarkMappingRationale = null,
    DateTime? BenchmarkMappingAsOfDate = null);

public sealed record OpportunityDetailDto(
    int SnapshotId,
    OpportunityRowDto Row,
    ScoreResult SwingScore,
    ScoreResult StrategicScore,
    SectorRelativeResult? SectorRelative,
    TradeSetupResult Setup,
    IReadOnlyList<FinalActionResultDto> Decisions,
    IReadOnlyList<SavedRiskPlanDto> SavedPlans,
    OpportunityEvidenceDto Evidence);

public sealed record OpportunityUniverseCountsDto(int Watchlist, int Portfolio, int Tsx60, int Total);

public sealed record OpportunityLastAttemptDto(DateTime? AtUtc, string? Status, string? ErrorSummary);

public sealed record OpportunitySnapshotDto(
    int? SnapshotId,
    string ScoringVersion,
    string Disclaimer,
    DateTime? ComputedAtUtc,
    DateTime? FinalActionComputedAtUtc,
    DateOnly? DataAsOfSession,
    string? Status,
    string? ErrorSummary,
    OpportunityLastAttemptDto? LastAttempt,
    OpportunityUniverseCountsDto Counts,
    /// <summary>The snapshot's session is older than the last completed session.</summary>
    bool IsStale,
    /// <summary>The portfolio changed after the evaluation the snapshot was built from (C1): refresh to see current decisions.</summary>
    bool PortfolioChangedSinceEvaluation,
    bool RefreshInFlight,
    IReadOnlyList<OpportunityRowDto> Rows);

/// <summary>What is persisted in <c>OpportunitySnapshots.SnapshotJson</c>.</summary>
public sealed record OpportunitySnapshotPayload(
    OpportunityUniverseCountsDto Counts,
    DateTime? PortfolioUpdatedAtUtc,
    IReadOnlyList<OpportunityDetailDto> Records);

public sealed class OpportunityRefreshInProgressException() : Exception("An opportunity refresh is already running for this user.");

/// <summary>Published scoring parameters so the UI explains thresholds without duplicating them.</summary>
public sealed record OpportunityConfigDto(
    string Version, string Disclaimer, string SectorMethodology,
    int StrongMin, int AttractiveMin, int ModerateMin, int CautionMin, decimal MinCompletenessPct,
    int SwingDenominatorMax,
    int ShortWindowSessions, int LongWindowSessions, decimal NeutralBandPp, decimal Intensity2Pp, decimal Intensity3Pp,
    decimal ActionableRewardRisk, int FundamentalsMaxAgeDays)
{
    public static OpportunityConfigDto Current { get; } = new(
        OpportunityScoringConfig.Version, OpportunityScoringConfig.Disclaimer, OpportunityScoringConfig.SectorMethodology,
        OpportunityScoringConfig.StrongMin, OpportunityScoringConfig.AttractiveMin, OpportunityScoringConfig.ModerateMin,
        OpportunityScoringConfig.CautionMin, OpportunityScoringConfig.MinCompletenessPct,
        (int)OpportunityScoringConfig.SwingDenominatorMax,
        OpportunityScoringConfig.ShortWindowSessions, OpportunityScoringConfig.LongWindowSessions, OpportunityScoringConfig.NeutralBandPp,
        OpportunityScoringConfig.Intensity2Pp, OpportunityScoringConfig.Intensity3Pp,
        OpportunityScoringConfig.ActionableRewardRisk, (int)OpportunityScoringConfig.FundamentalsMaxAge.TotalDays);
}
