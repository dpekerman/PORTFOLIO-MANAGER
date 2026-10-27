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
        // Runs from the unattended scheduler with no HTTP user, so scope to the owner explicitly:
        // GetAllAsync() would return only shared rows and drop the owner's own holdings and watchlist.
        var portfolioItems = await portfolioService.GetAllForUserAsync(userId, ct);
        var watchlistItems = await watchlistService.GetAllForUserAsync(userId, ct);
        var latestEodTradingDate = await db.DailySignals
            .Where(signal => signal.TradingDate != null)
            .MaxAsync(signal => signal.TradingDate, ct);

        // Separate manual positions — they don't need a Yahoo call
        var tickerItems  = portfolioItems.Where(i => !i.IsManual).ToList();
        var manualItems  = portfolioItems.Where(i => i.IsManual).ToList();
        var watchSymbols = watchlistItems.Select(i => i.Symbol);
        var latestSignals = await LoadLatestEodSignalsAsync(
            tickerItems.Select(i => i.Symbol).Concat(watchSymbols), ct);

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
            var sharedFacts = ToSharedFacts(technical, item.Symbol, latestEodTradingDate, latestSignals);
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
            var sharedFacts = ToSharedFacts(technical, item.Symbol, latestEodTradingDate, latestSignals);
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

    private SharedTechnicalFacts? ToSharedFacts(
        TechnicalSnapshot snapshot,
        string symbol,
        string? latestEodTradingDate,
        IReadOnlyDictionary<string, DailySignal> latestSignals)
    {
        if (!snapshot.HasTechnicalData) return null;

        latestSignals.TryGetValue(symbol, out var eodSignal);

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

    // One query for every symbol instead of one per symbol; same filter and ordering as before.
    private async Task<Dictionary<string, DailySignal>> LoadLatestEodSignalsAsync(
        IEnumerable<string> symbols, CancellationToken ct)
    {
        var list = symbols.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count == 0) return new Dictionary<string, DailySignal>(StringComparer.OrdinalIgnoreCase);

        var signals = await db.DailySignals.AsNoTracking()
            .Where(s => list.Contains(s.Symbol) && s.SignalState != "Expired" && s.TradingDate != null)
            .ToListAsync(ct);

        return signals
            .GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.TradingDate, StringComparer.Ordinal)
                      .ThenByDescending(s => s.ScannedAt ?? s.RecordedAt)
                      .First(),
                StringComparer.OrdinalIgnoreCase);
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
