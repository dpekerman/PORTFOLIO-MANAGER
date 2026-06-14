namespace PortfolioManager.Api.Models;

// DTOs for API request/response

public record AddPortfolioItemRequest(
    string Symbol,
    string CompanyName,
    decimal Shares,
    decimal AverageCostBasis);

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
    string Industry = "");

public record PortfolioItemDto(
    int Id,
    string Symbol,
    string CompanyName,
    decimal Shares,
    decimal AverageCostBasis,
    string Sector,
    string Industry,
    bool IsManual,
    decimal? ManualMarketValue,
    DateTime AddedAt);

public record PortfolioSummaryDto(
    PortfolioItemDto Item,
    StockQuote? Quote);

// ── Watchlist ──────────────────────────────────────────────────────────────────
public record AddWatchlistItemRequest(string Symbol, string Notes = "");

public record WatchlistItemDto(int Id, string Symbol, string Notes, DateTime AddedAt);

public record WatchlistSummaryDto(WatchlistItemDto Item, StockQuote? Quote);

