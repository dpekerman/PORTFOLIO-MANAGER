using System.Diagnostics;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IDataRefreshService
{
    Task<DataRefreshResultDto> RefreshAllAsync(string userId, CancellationToken ct = default);
}

public sealed class DataRefreshService(
    IPortfolioService portfolioService,
    IWatchlistService watchlistService,
    IMarketDataProvider marketData,
    IPortfolioSnapshotService portfolioSnapshot,
    IWatchlistSnapshotService watchlistSnapshot,
    IDashboardService dashboard) : IDataRefreshService
{
    public async Task<DataRefreshResultDto> RefreshAllAsync(string userId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // DB reads must be sequential — EF Core DbContext is not thread-safe
        var portfolioItems = await portfolioService.GetAllAsync(ct);
        var watchlistItems = await watchlistService.GetAllAsync(ct);

        // Separate manual positions — they don't need a Yahoo call
        var tickerItems  = portfolioItems.Where(i => !i.IsManual).ToList();
        var manualItems  = portfolioItems.Where(i => i.IsManual).ToList();
        var watchSymbols = watchlistItems.Select(i => i.Symbol);

        // Yahoo Finance HTTP calls have no DbContext — safe to run in parallel
        Task<Dictionary<string, StockQuote>> portfolioQuotesTask = tickerItems.Count > 0
            ? marketData.GetBatchQuotesAsync(tickerItems.Select(i => i.Symbol), ct)
            : Task.FromResult(new Dictionary<string, StockQuote>());
        Task<Dictionary<string, StockQuote>> watchlistQuotesTask = watchlistItems.Count > 0
            ? marketData.GetBatchQuotesAsync(watchSymbols, ct)
            : Task.FromResult(new Dictionary<string, StockQuote>());

        await Task.WhenAll(portfolioQuotesTask, watchlistQuotesTask);

        var portfolioQuotes  = portfolioQuotesTask.Result;
        var watchlistQuotes  = watchlistQuotesTask.Result;

        // Build portfolio summary list (same logic as StocksController)
        var portfolioSummaries = new List<PortfolioSummaryDto>();
        foreach (var item in tickerItems)
        {
            portfolioQuotes.TryGetValue(item.Symbol, out var quote);
            if (quote is not null) quote.CompanyName = item.CompanyName;
            portfolioSummaries.Add(new PortfolioSummaryDto(item, quote));
        }
        foreach (var item in manualItems)
        {
            var mv = item.ManualMarketValue ?? item.AverageCostBasis;
            portfolioSummaries.Add(new PortfolioSummaryDto(item, new StockQuote
            {
                Symbol        = item.Symbol,
                CompanyName   = item.CompanyName,
                CurrentPrice  = mv,
                Change        = 0m,
                ChangePercent = 0m,
                Sector        = item.Sector,
                Industry      = item.Industry,
                MarketState   = "MANUAL",
                Timestamp     = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            }));
        }
        var sortedPortfolio = portfolioSummaries.OrderBy(r => r.Item.Symbol).ToList();

        // Build watchlist summary list
        var watchlistSummaries = watchlistItems.Select(item =>
        {
            watchlistQuotes.TryGetValue(item.Symbol, out var quote);
            return new WatchlistSummaryDto(item, quote);
        }).ToList();

        // Persist snapshots (no dashboard rebuild yet — done once below)
        await portfolioSnapshot.SaveAsync(userId, sortedPortfolio.AsReadOnly(), ct);
        await watchlistSnapshot.SaveAsync(userId, watchlistSummaries.AsReadOnly(), ct);

        // Rebuild dashboard once from the fresh snapshots, skipping slow earnings fetch
        await dashboard.RebuildAsync(userId, ct, includeEarnings: false);

        sw.Stop();
        return new DataRefreshResultDto(
            PortfolioSymbolCount: sortedPortfolio.Count,
            WatchlistSymbolCount: watchlistSummaries.Count,
            DashboardRebuilt: true,
            RefreshedAt: DateTime.UtcNow,
            DurationMs: sw.ElapsedMilliseconds);
    }
}
