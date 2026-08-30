namespace PortfolioManager.Api.Models;

public record DataRefreshResultDto(
    int PortfolioSymbolCount,
    int WatchlistSymbolCount,
    bool DashboardRebuilt,
    DateTime RefreshedAt,
    long DurationMs,
    IReadOnlyList<PortfolioSummaryDto> PortfolioSummaries,
    IReadOnlyList<WatchlistSummaryDto> WatchlistSummaries,
    IReadOnlyList<string> PortfolioSymbols,
    IReadOnlyList<string> WatchlistSymbols);
