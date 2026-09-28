using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
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
    ITechnicalSnapshotService technicalSnapshots,
    IPortfolioSnapshotService portfolioSnapshot,
    IWatchlistSnapshotService watchlistSnapshot,
    IDashboardService dashboard,
    AppDbContext db) : IDataRefreshService
{
    public async Task<DataRefreshResultDto> RefreshAllAsync(string userId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // DB reads must be sequential — EF Core DbContext is not thread-safe
        var portfolioItems = await portfolioService.GetAllAsync(ct);
        var watchlistItems = await watchlistService.GetAllAsync(ct);
        var latestEodTradingDate = await db.DailySignals
            .Where(signal => signal.TradingDate != null)
            .MaxAsync(signal => signal.TradingDate, ct);

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
            var technical = await technicalSnapshots.GetSnapshotAsync(item.Symbol, userId, ct);
            var sharedFacts = await ToSharedFactsAsync(technical, item.Symbol, latestEodTradingDate, ct);
            portfolioSummaries.Add(new PortfolioSummaryDto(item, quote, technical.PriceStructure, sharedFacts));
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
            }, PriceStructureResult.None));
        }
        var sortedPortfolio = portfolioSummaries.OrderBy(r => r.Item.Symbol).ToList();

        // Build watchlist summary list
        var watchlistSummaries = new List<WatchlistSummaryDto>();
        foreach (var item in watchlistItems)
        {
            watchlistQuotes.TryGetValue(item.Symbol, out var quote);
            var technical = await technicalSnapshots.GetSnapshotAsync(item.Symbol, userId, ct);
            var sharedFacts = await ToSharedFactsAsync(technical, item.Symbol, latestEodTradingDate, ct);
            watchlistSummaries.Add(new WatchlistSummaryDto(item, quote, technical.PriceStructure, sharedFacts));
        }

        // Persist snapshots atomically — both must succeed or neither is committed.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await portfolioSnapshot.SaveAsync(userId, sortedPortfolio.AsReadOnly(), ct);
            await watchlistSnapshot.SaveAsync(userId, watchlistSummaries.AsReadOnly(), ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        // Rebuild dashboard once from the fresh snapshots, skipping slow earnings fetch
        await dashboard.RebuildAsync(userId, ct, includeEarnings: false);

        sw.Stop();
        
        // Extract symbol lists for progress tracking on frontend
        var portfolioSymbols = sortedPortfolio.Select(p => p.Item.Symbol).ToList().AsReadOnly();
        var watchlistSymbols = watchlistSummaries.Select(w => w.Item.Symbol).ToList().AsReadOnly();
        
        return new DataRefreshResultDto(
            PortfolioSymbolCount: sortedPortfolio.Count,
            WatchlistSymbolCount: watchlistSummaries.Count,
            DashboardRebuilt: true,
            RefreshedAt: DateTime.UtcNow,
            DurationMs: sw.ElapsedMilliseconds,
            PortfolioSummaries: sortedPortfolio.AsReadOnly(),
            WatchlistSummaries: watchlistSummaries.AsReadOnly(),
            PortfolioSymbols: portfolioSymbols,
            WatchlistSymbols: watchlistSymbols);
    }

    private async Task<SharedTechnicalFacts?> ToSharedFactsAsync(
        TechnicalSnapshot snapshot,
        string symbol,
        string? latestEodTradingDate,
        CancellationToken ct)
    {
        if (!snapshot.HasTechnicalData) return null;

        // Fetch latest EOD signal for this symbol
        var eodSignal = await GetLatestEodSignalAsync(symbol, ct);

        return new SharedTechnicalFacts(
            Symbol: snapshot.Symbol,
            Rsi: null,
            MaStructure: snapshot.Analysis.MaStructure,
            MaCrossState: snapshot.Analysis.LastCross,
            MomentumState: snapshot.Analysis.MomentumState,
            PriceStructure: snapshot.PriceStructure,
            BuyScore: null,
            CalculatedAt: snapshot.ComputedAt,
            // ── EOD Signal fields (populated if signal exists) ────────────────
            LatestEodTradingDate: eodSignal?.TradingDate,
            LatestEodSignalDate: eodSignal?.ScannedAt ?? eodSignal?.RecordedAt,
            LatestEodSignalState: eodSignal?.SignalState,
            LatestEodScanType: eodSignal?.ScanType,
            LatestEodRsi: eodSignal?.Rsi,
            LatestEodTrendShift: eodSignal?.TrendShift,
            LatestEodEntryPrice: eodSignal?.EntryPrice,
            LatestEodStopLoss: eodSignal?.StopLossPrice,
            LatestEodRiskPercent: CalculateRiskPercent(eodSignal),
            LatestEodReversalStrength: MapReversalStrength(eodSignal?.ReversalProbability),
            LatestEodVolumeState: MapVolumeState(eodSignal?.VolumeSignal),
            LatestEodIsNew: eodSignal?.TradingDate == latestEodTradingDate,
            LatestEodIsInvalidated: eodSignal != null && eodSignal.SignalState == "Invalidated",
            AnalysisTicker: snapshot.AnalysisTicker,
            AnalysisMarket: snapshot.AnalysisMarket,
            AnalysisCurrency: snapshot.AnalysisCurrency,
            UsesUnderlyingSecurity: snapshot.UsesUnderlyingSecurity);
    }

    private async Task<DailySignal?> GetLatestEodSignalAsync(string symbol, CancellationToken ct)
    {
        return await db.DailySignals
            .Where(s => s.Symbol == symbol && s.SignalState != "Expired" && s.TradingDate != null)
            .OrderByDescending(s => s.TradingDate)
            .ThenByDescending(s => s.ScannedAt ?? s.RecordedAt)
            .FirstOrDefaultAsync(ct);
    }

    private static string? MapReversalStrength(string? reversalProbability)
    {
        return reversalProbability switch
        {
            "High" => "Strong",
            "Medium" => "Medium",
            "Low" => "Low",
            _ => null,
        };
    }

    private static string? MapVolumeState(string? volumeSignal)
    {
        return volumeSignal switch
        {
            "Validated" => "Validated",
            "Neutral" => "Neutral",
            "Low" => "Low",
            _ => null,
        };
    }

    private static decimal? CalculateRiskPercent(DailySignal? signal)
    {
        if (signal is null || !signal.RiskPerShare.HasValue || !signal.EntryPrice.HasValue)
            return null;

        var entryPrice = signal.EntryPrice.Value;
        if (entryPrice == 0) return null;

        return Math.Round((signal.RiskPerShare.Value / entryPrice) * 100, 2);
    }
}
