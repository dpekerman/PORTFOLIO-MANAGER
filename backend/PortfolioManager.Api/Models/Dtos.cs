namespace PortfolioManager.Api.Models;

// DTOs for API request/response

public record AddPortfolioItemRequest(
    string Symbol,
    string CompanyName,
    decimal Shares,
    decimal AverageCostBasis);

public record UpdatePortfolioItemRequest(
    string CompanyName,
    decimal Shares,
    decimal AverageCostBasis);

public record PortfolioItemDto(
    int Id,
    string Symbol,
    string CompanyName,
    decimal Shares,
    decimal AverageCostBasis,
    DateTime AddedAt);

public record PortfolioSummaryDto(
    PortfolioItemDto Item,
    StockQuote? Quote);
