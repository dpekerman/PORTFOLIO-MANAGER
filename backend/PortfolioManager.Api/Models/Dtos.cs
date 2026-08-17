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
    StockQuote? Quote);

// ── Watchlist ──────────────────────────────────────────────────────────────────
public record AddWatchlistItemRequest(string Symbol, string Notes = "", string Role = "Strategic");

public record UpdateWatchlistRoleRequest(string Role);
public record UpdatePortfolioHoldingRoleRequest(string HoldingRole);
public record UpdatePortfolioNotesRequest(string? Notes);

public record WatchlistItemDto(int Id, string Symbol, string Notes, DateTime AddedAt, string Role = "Strategic", bool IsFavorite = false);

public record WatchlistSummaryDto(WatchlistItemDto Item, StockQuote? Quote);

public record UpdateWatchlistFavoriteRequest(bool IsFavorite);
public record UpdateWatchlistNotesRequest(string Notes);

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
public record WatchlistBackupItem(string Symbol, string Notes, string Role, DateTime AddedAt);
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

