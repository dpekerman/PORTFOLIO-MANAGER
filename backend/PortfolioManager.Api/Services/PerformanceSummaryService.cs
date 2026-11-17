using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;

namespace PortfolioManager.Api.Services;

public sealed record BenchmarkReturn(string Name, string Symbol, decimal YtdReturnPct);

/// <summary>
/// Benchmark returns over the same period the Dashboard's upper performance card reports for the portfolio.
/// The portfolio's own return is NOT computed here: it comes from <see cref="IPortfolioPerformanceService"/> via the dashboard
/// (cash-flow adjusted), so there is a single calculation path.
/// </summary>
public sealed record PerformanceSummaryResponse(
    // Date the benchmark return starts from (the portfolio baseline snapshot date).
    string BenchmarkStartDate,
    // True when that baseline is the prior year-end (a real year-to-date period); false for the shorter
    // "since cash-ledger start" period the portfolio card falls back to. The frontend labels accordingly.
    bool IsFullYear,
    IReadOnlyList<BenchmarkReturn> Benchmarks);

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
        var history = await db.PortfolioValueHistories.AsNoTracking().OrderBy(h => h.RecordedDate).ToListAsync(ct);
        if (history.Count < 2) return null;

        var settings = await db.CashLedgerSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);
        DateOnly? ledgerStart = settings is null ? null : DateOnly.FromDateTime(settings.LedgerStartDate);
        var today = DateOnly.FromDateTime(TradingCalendar.TorontoNow(DateTime.UtcNow));
        var janFirst = new DateOnly(today.Year, 1, 1);

        // Same baseline rule as the portfolio card: a usable pre-Jan-1 snapshot means true YTD, otherwise the first
        // snapshot on/after the cash-ledger start. Never a silent rebase of the benchmark.
        var ytdBase = history.LastOrDefault(h => DateOnly.Parse(h.RecordedDate) < janFirst);
        var isFullYear = PortfolioPerformanceService.CheckBaseline(ytdBase, janFirst, ledgerStart, updating: false) is null;
        var baseline = isFullYear ? ytdBase : PortfolioPerformanceService.SinceStartBaseline(history, ledgerStart, today);
        if (baseline is null) return null;

        var benchmarkReturns = new List<BenchmarkReturn>();
        try
        {
            var symbols = Benchmarks.Select(b => b.symbol).ToList();
            var startPrices = await GetClosingPricesOnOrBeforeAsync(symbols, baseline.RecordedDate, ct);
            var currentQuotes = await marketData.GetBatchQuotesAsync(symbols, ct);
            foreach (var (sym, name) in Benchmarks)
            {
                var ok = currentQuotes.TryGetValue(sym, out var q) && startPrices.TryGetValue(sym, out var start) && start > 0;
                benchmarkReturns.Add(new BenchmarkReturn(name, sym,
                    ok ? Math.Round((q!.CurrentPrice - startPrices[sym]) / startPrices[sym] * 100m, 2) : 0m));
            }
        }
        catch
        {
            // Non-critical: the portfolio card still renders; benchmarks show N/A.
            benchmarkReturns.Clear();
            foreach (var (sym, name) in Benchmarks) benchmarkReturns.Add(new BenchmarkReturn(name, sym, 0m));
        }

        return new PerformanceSummaryResponse(baseline.RecordedDate, isFullYear, benchmarkReturns.AsReadOnly());
    }

    /// <summary>Closing prices on the baseline date, or the nearest earlier trading day (up to 7 days back).</summary>
    private async Task<Dictionary<string, decimal>> GetClosingPricesOnOrBeforeAsync(
        IReadOnlyList<string> symbols, string baselineDate, CancellationToken ct)
    {
        var date = DateOnly.Parse(baselineDate);
        for (var offset = 0; offset <= 7; offset++)
        {
            var prices = await marketData.GetHistoricalClosingPricesAsync(date.AddDays(-offset).ToString("yyyy-MM-dd"), symbols, ct);
            if (prices.Count > 0) return prices;
        }
        return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    }
}
