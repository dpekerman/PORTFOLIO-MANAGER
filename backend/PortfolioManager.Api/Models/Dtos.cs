using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Models;

// DTOs for API request/response

public record AddPortfolioItemRequest(
    string Symbol,
    string CompanyName,
    decimal Shares,
    decimal AverageCostBasis,
    string? TransactionType = null,
    string? AccountType = null,
    DateTime? OpenDate = null,
    DateTime? CloseDate = null,
    decimal? ClosingPrice = null,
    string? DecisionSource = null,
    string? HoldingRole = null,
    string? DecisionSourceClosed = null);

/// <summary>
/// Request to add a manual (non-ticker) position such as Cash, Options, Bonds, etc.
/// Name is stored as Sector; Description as Industry. Shares is always 1.
/// </summary>
public record AddManualPositionRequest(
    string Name,
    string Description,
    decimal AverageCost,
    decimal MarketValue);

public record UpdatePortfolioItemRequest(
    string CompanyName,
    decimal Shares,
    decimal AverageCostBasis,
    string Sector = "",
    string Industry = "",
    bool OverrideSector = false,
    string? TransactionType = null,
    string? AccountType = null,
    DateTime? OpenDate = null,
    DateTime? CloseDate = null,
    decimal? ClosingPrice = null,
    string? HoldingRole = null,
    string? DecisionSource = null,
    string? DecisionSourceClosed = null);

public record PortfolioItemDto(
    int Id,
    string Symbol,
    string CompanyName,
    decimal Shares,
    decimal AverageCostBasis,
    string Sector,
    string Industry,
    bool SectorIsOverridden,
    bool IsManual,
    decimal? ManualMarketValue,
    DateTime AddedAt,
    string? TransactionType = null,
    string? AccountType = null,
    DateTime? OpenDate = null,
    DateTime? CloseDate = null,
    decimal? ClosingPrice = null,
    string? HoldingRole = null,
    string? Notes = null,
    string? DecisionSource = null,
    string? DecisionSourceClosed = null);

public record PortfolioSummaryDto(
    PortfolioItemDto Item,
    StockQuote? Quote,
    PriceStructureResult? PriceStructure = null,
    SharedTechnicalFacts? TechnicalFacts = null);

public sealed record SharedTechnicalFacts(
    string Symbol,
    decimal? Rsi,
    string? MaStructure,
    string? MaCrossState,
    string? MomentumState,
    PriceStructureResult PriceStructure,
    int? BuyScore,
    DateTime CalculatedAt,
    // ── Latest EOD Signal (populated from DailySignals table) ────────────────
    string? LatestEodTradingDate = null,
    DateTime? LatestEodSignalDate = null,
    string? LatestEodSignalState = null,        // "Active" | "Invalidated" | "FollowThrough" | etc.
    string? LatestEodScanType = null,           // "Oversold" | "Overbought"
    decimal? LatestEodRsi = null,
    string? LatestEodTrendShift = null,         // "Bull Turn" | "Bear Turn" | "Stabilizing" | "Bull Turn — Early"
    decimal? LatestEodEntryPrice = null,
    decimal? LatestEodStopLoss = null,
    decimal? LatestEodRiskPercent = null,
    string? LatestEodReversalStrength = null,   // "Low" | "Medium" | "Strong"
    string? LatestEodVolumeState = null,        // "Validated" | "Neutral" | "Low"
    bool LatestEodIsNew = false,                // true if signal created in latest trading session
    bool LatestEodIsInvalidated = false,
    string? AnalysisTicker = null,
    string? AnalysisMarket = null,
    string? AnalysisCurrency = null,
    bool UsesUnderlyingSecurity = false);

// ── Watchlist ──────────────────────────────────────────────────────────────────
public record AddWatchlistItemRequest(string Symbol, string Notes = "", string Role = "Strategic", string WatchlistTier = "Strategic");

public record UpdateWatchlistRoleRequest(string Role);
public record UpdateWatchlistTierRequest(string WatchlistTier);
public record UpdatePortfolioHoldingRoleRequest(string HoldingRole);
public record UpdatePortfolioNotesRequest(string? Notes);

public record WatchlistItemDto(int Id, string Symbol, string Notes, DateTime AddedAt, string Role = "Strategic", bool IsFavorite = false, DateTime? EarningsDate = null, string WatchlistTier = "Strategic");

public record WatchlistSummaryDto(
    WatchlistItemDto Item,
    StockQuote? Quote,
    PriceStructureResult? PriceStructure = null,
    SharedTechnicalFacts? TechnicalFacts = null);

public record UpdateWatchlistFavoriteRequest(bool IsFavorite);
public record UpdateWatchlistNotesRequest(string Notes);
public record UpdateWatchlistEarningsDateRequest(DateTime? EarningsDate);

// ── Security analysis mappings ───────────────────────────────────────────────
public record SecurityAnalysisMappingDto(
    string TradingTicker,
    string AnalysisTicker,
    string AnalysisMarket,
    string AnalysisCurrency,
    bool UsesUnderlyingSecurity,
    UnderlyingResolutionStatus ResolutionStatus,
    SecurityAnalysisMappingSource? MappingSource,
    string? DataError = null);

public record SaveSecurityAnalysisMappingRequest(string UnderlyingTicker, bool UseUnderlyingForAnalysis = true);

// ── Sector / Industry Lists ─────────────────────────────────────────────────────
public record SectorIndustryListsDto(List<string> Sectors, List<string> Industries, List<string>? DecisionSources = null);
public record UpdateSectorIndustryListsRequest(List<string> Sectors, List<string> Industries, List<string>? DecisionSources = null);

/// <summary>Dedicated payload for the Decision Source picklist endpoint.</summary>
public record DecisionSourcesDto(List<string> Items);
public record UpdateDecisionSourcesRequest(List<string> Items);

// ── Cash ─────────────────────────────────────────────────────────────────────
public record AddCashItemRequest(string Description, decimal Amount, string? AccountType = null, DateTime? TransactionDate = null);
public record UpdateCashItemRequest(string Description, decimal Amount, string? AccountType = null, DateTime? TransactionDate = null);
public record CashItemDto(int Id, string Description, decimal Amount, DateTime AddedAt, string? AccountType = null, DateTime? TransactionDate = null);

// ── Options ───────────────────────────────────────────────────────────────────
public record AddOptionItemRequest(
    string UnderlyingTicker,
    string PositionType,
    DateTime ExpirationDate,
    decimal Strike,
    decimal Premium,
    int NumberOfContracts,
    decimal MarketPrice,
    string? TransactionType = null,
    string? AccountType = null,
    DateTime? OpenDate = null,
    DateTime? CloseDate = null,
    decimal? ClosingPrice = null,
    string? DecisionSource = null);

public record UpdateOptionItemRequest(
    string UnderlyingTicker,
    string PositionType,
    DateTime ExpirationDate,
    decimal Strike,
    decimal Premium,
    int NumberOfContracts,
    decimal MarketPrice,
    string? TransactionType = null,
    string? AccountType = null,
    DateTime? OpenDate = null,
    DateTime? CloseDate = null,
    decimal? ClosingPrice = null,
    string? DecisionSource = null,
    string? DecisionSourceClosed = null);

public record UpdateOptionNotesRequest(string? Notes);

public record OptionItemDto(
    int Id,
    string UnderlyingTicker,
    string PositionType,
    DateTime ExpirationDate,
    decimal Strike,
    decimal Premium,
    int NumberOfContracts,
    decimal MarketPrice,
    DateTime AddedAt,
    string? TransactionType = null,
    string? AccountType = null,
    DateTime? OpenDate = null,
    DateTime? CloseDate = null,
    decimal? ClosingPrice = null,
    string? Notes = null,
    string? DecisionSource = null,
    string? DecisionSourceClosed = null);

/// <summary>Technical indicators for the underlying ticker, used by the frontend option state engine.</summary>
public record OptionTechnicalDataDto(
    string Symbol,
    decimal CurrentPrice,
    decimal PreviousClose,
    decimal YesterdayHigh,
    decimal YesterdayLow,
    decimal Rsi14,
    decimal RsiSignal9,
    bool RsiSignalAvailable,
    decimal Sma20,
    decimal Sma50,
    decimal Ema21,
    decimal Atr14,
    decimal BollingerUpper,
    decimal BollingerLower);

// ── Backup / Restore ───────────────────────────────────────────────────────────

/// <summary>Single watchlist item in a backup export.</summary>
public record WatchlistBackupItem(string Symbol, string Notes, string Role, DateTime AddedAt, DateTime? EarningsDate = null);
/// <summary>Restore request: clears existing watchlist and inserts all provided items.</summary>
public record RestoreWatchlistRequest(List<WatchlistBackupItem> Items);

/// <summary>Single cash item in a backup export.</summary>
public record CashBackupItem(string Description, decimal Amount, DateTime AddedAt);
/// <summary>Restore request: clears existing cash items and inserts all provided items.</summary>
public record RestoreCashRequest(List<CashBackupItem> Items);

/// <summary>Single option item in a backup export.</summary>
public record OptionBackupItem(
    string UnderlyingTicker,
    string PositionType,
    DateTime ExpirationDate,
    decimal Strike,
    decimal Premium,
    int NumberOfContracts,
    decimal MarketPrice,
    string? TransactionType,
    string? AccountType,
    DateTime? OpenDate,
    DateTime? CloseDate,
    decimal? ClosingPrice,
    string? Notes,
    DateTime AddedAt);
/// <summary>Restore request: clears existing options and inserts all provided items.</summary>
public record RestoreOptionsRequest(List<OptionBackupItem> Items);

/// <summary>Single portfolio item in a backup export.</summary>
public record PortfolioBackupItem(
    string Symbol,
    string CompanyName,
    decimal Shares,
    decimal AverageCostBasis,
    string Sector,
    string Industry,
    bool SectorIsOverridden,
    bool IsManual,
    decimal? ManualMarketValue,
    string? TransactionType,
    string? AccountType,
    DateTime? OpenDate,
    DateTime? CloseDate,
    decimal? ClosingPrice,
    string? HoldingRole,
    string? Notes,
    DateTime AddedAt);
/// <summary>Restore request: clears existing portfolio items and inserts all provided items.</summary>
public record RestorePortfolioRequest(List<PortfolioBackupItem> Items);

// ── Allocation & Risk Management ────────────────────────────────────────────────
public record AllocationRiskTargetDto(int Id, string Role, decimal TargetPct, int DisplayOrder);
public record AllocationSectorTargetDto(int Id, string Sector, decimal TargetPct, int DisplayOrder);
public record SinglePositionLimitDto(int Id, string Role, decimal TargetPct, int DisplayOrder);

public record UpsertAllocationRiskTargetRequest(string Role, decimal TargetPct);
public record UpsertAllocationSectorTargetRequest(string Sector, decimal TargetPct);
public record UpsertSinglePositionLimitRequest(string Role, decimal TargetPct);

public record AllocationRiskConfigDto(
    List<AllocationRiskTargetDto> RiskTargets,
    List<AllocationSectorTargetDto> SectorTargets,
    List<SinglePositionLimitDto> PositionLimits);

