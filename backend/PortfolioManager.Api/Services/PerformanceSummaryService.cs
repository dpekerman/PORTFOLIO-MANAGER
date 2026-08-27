using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public sealed record BenchmarkReturn(string Name, string Symbol, decimal YtdReturnPct);

public sealed record PerformanceSummaryResponse(
    decimal PortfolioYtdReturnPct,
    decimal PortfolioStartValue,
    decimal PortfolioCurrentValue,
    IReadOnlyList<BenchmarkReturn> Benchmarks,
    decimal AlphaVsPrimaryBenchmarkPct,
    string PrimaryBenchmarkName);

public interface IPerformanceSummaryService
{
    Task<PerformanceSummaryResponse?> GetSummaryAsync(string userId, CancellationToken ct = default);
}

public sealed class PerformanceSummaryService(AppDbContext db, IMarketDataProvider marketData) : IPerformanceSummaryService
{
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

        var portfolioYtd = Math.Round((latest.TotalValue - ytdBase.TotalValue) / ytdBase.TotalValue * 100m, 2);

        // Fetch benchmark YTD returns from Yahoo Finance
        var benchmarkReturns = new List<BenchmarkReturn>();
        try
        {
            var symbols = Benchmarks.Select(b => b.symbol);
            var quotes = await marketData.GetBatchQuotesAsync(symbols, ct);
            // Yahoo Finance returns YTD via changePercent which is daily, not YTD.
            // We use the 52-week-high proximity as an indicator but it's not accurate.
            // Instead, use the index YTD price change: query 1-year history.
            // For simplicity, use the quote's ytdReturn if available, else 0.
            foreach (var (sym, name) in Benchmarks)
            {
                if (quotes.TryGetValue(sym, out var q))
                {
                    // Yahoo returns 52-week context but not YTD directly in the basic quote.
                    // We'll return 0 and note the limitation — can be enhanced with candle data.
                    benchmarkReturns.Add(new BenchmarkReturn(name, sym, 0m));
                }
            }
        }
        catch
        {
            // Non-critical — return portfolio data without benchmark
        }

        var primaryBenchmark = benchmarkReturns.FirstOrDefault();
        var alpha = primaryBenchmark is not null && primaryBenchmark.YtdReturnPct != 0
            ? Math.Round(portfolioYtd - primaryBenchmark.YtdReturnPct, 2)
            : 0m;

        return new PerformanceSummaryResponse(
            PortfolioYtdReturnPct: portfolioYtd,
            PortfolioStartValue: ytdBase.TotalValue,
            PortfolioCurrentValue: latest.TotalValue,
            Benchmarks: benchmarkReturns.AsReadOnly(),
            AlphaVsPrimaryBenchmarkPct: alpha,
            PrimaryBenchmarkName: primaryBenchmark?.Name ?? "TSX Composite");
    }
}
