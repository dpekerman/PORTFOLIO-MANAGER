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
    decimal? ClosingPrice = null);

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
    string? HoldingRole = null);

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
    string? HoldingRole = null);

public record PortfolioSummaryDto(
    PortfolioItemDto Item,
    StockQuote? Quote);

// ── Watchlist ──────────────────────────────────────────────────────────────────
public record AddWatchlistItemRequest(string Symbol, string Notes = "", string Role = "Strategic");

public record UpdateWatchlistRoleRequest(string Role);
public record UpdatePortfolioHoldingRoleRequest(string HoldingRole);

public record WatchlistItemDto(int Id, string Symbol, string Notes, DateTime AddedAt, string Role = "Strategic");

public record WatchlistSummaryDto(WatchlistItemDto Item, StockQuote? Quote);

// ── Sector / Industry Lists ─────────────────────────────────────────────────────
public record SectorIndustryListsDto(List<string> Sectors, List<string> Industries);
public record UpdateSectorIndustryListsRequest(List<string> Sectors, List<string> Industries);

// ── Cash ─────────────────────────────────────────────────────────────────────
public record AddCashItemRequest(string Description, decimal Amount);
public record UpdateCashItemRequest(string Description, decimal Amount);
public record CashItemDto(int Id, string Description, decimal Amount, DateTime AddedAt);

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
    decimal? ClosingPrice = null);

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
    decimal? ClosingPrice = null);

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
    decimal? ClosingPrice = null);

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

