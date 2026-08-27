namespace PortfolioManager.Api.Models;

public record DataRefreshResultDto(
    int PortfolioSymbolCount,
    int WatchlistSymbolCount,
    bool DashboardRebuilt,
    DateTime RefreshedAt,
    long DurationMs);
