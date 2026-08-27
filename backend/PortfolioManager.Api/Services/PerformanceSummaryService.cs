using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public sealed record BenchmarkReturn(string Name, string Symbol, decimal YtdReturnPct);

public sealed record PerformanceSummaryResponse(
    decimal PortfolioYtdReturnPct,
    decimal PortfolioYtdDollar,
    decimal PortfolioStartValue,
    string  PortfolioStartDate,
    decimal PortfolioCurrentValue,
    string  PortfolioCurrentDate,
    IReadOnlyList<BenchmarkReturn> Benchmarks,
    decimal AlphaVsPrimaryBenchmarkPct,
    string PrimaryBenchmarkName);

public interface IPerformanceSummaryService
{
    Task<PerformanceSummaryResponse?> GetSummaryAsync(string userId, CancellationToken ct = default);
}

public sealed class PerformanceSummaryService(AppDbContext db, IMarketDataProvider marketData) : IPerformanceSummaryService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static readonly (string symbol, string name)[] Benchmarks =
    [
        ("^GSPTSE", "TSX Composite"),
        ("^GSPC",   "S&P 500"),
    ];

    public async Task<PerformanceSummaryResponse?> GetSummaryAsync(string userId, CancellationToken ct = default)
    {
        var history = await db.PortfolioValueHistories
            .AsNoTracking()
            .OrderBy(h => h.RecordedDate)
            .ToListAsync(ct);

        if (history.Count < 2) return null;

        var today = DateTime.UtcNow;
        var janFirst = $"{today.Year}-01-01";

        // Find last value on or before Jan 1 (prior year-end close)
        var ytdBase = history.LastOrDefault(h => string.Compare(h.RecordedDate, janFirst, StringComparison.Ordinal) <= 0)
            ?? history.First();
        var latest = history.Last();

        if (ytdBase.TotalValue <= 0) return null;

        // ── Live current value from portfolio snapshot (matches the portfolio hero) ──
        var portfolioSnap = await db.PortfolioSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var snapItems = Deserialize<List<PortfolioSummaryDto>>(portfolioSnap?.SnapshotJson ?? "[]") ?? [];
        var liveStocksValue = snapItems
            .Where(p => !IsClose(p.Item.TransactionType))
            .Sum(p => p.Item.IsManual
                ? (p.Item.ManualMarketValue ?? p.Item.AverageCostBasis * p.Item.Shares)
                : (p.Quote?.CurrentPrice ?? p.Item.AverageCostBasis) * p.Item.Shares);
        var cashTotal = await db.CashItems.AsNoTracking().SumAsync(c => c.Amount, ct);
        var currentValue = liveStocksValue > 0 ? liveStocksValue + cashTotal : latest.TotalValue;

        var portfolioYtd = ytdBase.TotalValue > 0
            ? Math.Round((currentValue - ytdBase.TotalValue) / ytdBase.TotalValue * 100m, 2)
            : 0m;
        var portfolioYtdDollar = Math.Round(currentValue - ytdBase.TotalValue, 2);

        // ── Benchmark YTD returns from Yahoo Finance ──────────────────────────────
        var benchmarkReturns = new List<BenchmarkReturn>();
        try
        {
            var benchmarkSymbols = Benchmarks.Select(b => b.symbol).ToList();

            // Get prior year-end closing price (Dec 31, or nearest trading day before Jan 1)
            var startPrices = await GetLastTradingPricesBeforeYearAsync(benchmarkSymbols, today.Year, ct);

            // Get current quotes
            var currentQuotes = await marketData.GetBatchQuotesAsync(benchmarkSymbols, ct);

            foreach (var (sym, name) in Benchmarks)
            {
                if (currentQuotes.TryGetValue(sym, out var q) &&
                    startPrices.TryGetValue(sym, out var startPrice) &&
                    startPrice > 0)
                {
                    var ytd = Math.Round((q.CurrentPrice - startPrice) / startPrice * 100m, 2);
                    benchmarkReturns.Add(new BenchmarkReturn(name, sym, ytd));
                }
                else
                {
                    benchmarkReturns.Add(new BenchmarkReturn(name, sym, 0m));
                }
            }
        }
        catch
        {
            // Non-critical — return portfolio data without benchmarks
            foreach (var (sym, name) in Benchmarks)
                benchmarkReturns.Add(new BenchmarkReturn(name, sym, 0m));
        }

        var primaryBenchmark = benchmarkReturns.FirstOrDefault(b => b.YtdReturnPct != 0)
            ?? benchmarkReturns.FirstOrDefault();
        var alpha = primaryBenchmark is not null && primaryBenchmark.YtdReturnPct != 0
            ? Math.Round(portfolioYtd - primaryBenchmark.YtdReturnPct, 2)
            : 0m;

        return new PerformanceSummaryResponse(
            PortfolioYtdReturnPct:         portfolioYtd,
            PortfolioYtdDollar:            portfolioYtdDollar,
            PortfolioStartValue:           ytdBase.TotalValue,
            PortfolioStartDate:            ytdBase.RecordedDate,
            PortfolioCurrentValue:         currentValue,
            PortfolioCurrentDate:          latest.RecordedDate,
            Benchmarks:                    benchmarkReturns.AsReadOnly(),
            AlphaVsPrimaryBenchmarkPct:    alpha,
            PrimaryBenchmarkName:          primaryBenchmark?.Name ?? "TSX Composite");
    }

    /// <summary>
    /// Returns prior-year closing prices for the given symbols by trying
    /// Dec 31, Dec 30, … Dec 25 until data is found.
    /// </summary>
    private async Task<Dictionary<string, decimal>> GetLastTradingPricesBeforeYearAsync(
        IEnumerable<string> symbols, int year, CancellationToken ct)
    {
        var syms = symbols.ToList();
        for (var offset = 1; offset <= 7; offset++)
        {
            var dateStr = new DateTime(year, 1, 1).AddDays(-offset).ToString("yyyy-MM-dd");
            var prices = await marketData.GetHistoricalClosingPricesAsync(dateStr, syms, ct);
            if (prices.Count > 0) return prices;
        }
        return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsClose(string? txType)
        => string.Equals(txType, "CLOSE", StringComparison.OrdinalIgnoreCase);

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }
}

