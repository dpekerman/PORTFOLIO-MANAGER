using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public sealed record EvaluationRow(
    string Symbol, string Action, string Priority, decimal? ReferenceClose, DateOnly? ReferenceDate,
    decimal? Ret1, decimal? Ret5, decimal? Ret20, decimal? Excess1, decimal? Excess5, decimal? Excess20);

public sealed record EvaluationResult(int RunId, DateOnly AsOfDate, string Benchmark, IReadOnlyList<EvaluationRow> Rows, string Note);

/// <summary>
/// Outcome evaluation of a past report after 1, 5 and 20 trading sessions. The reference price is the last close
/// before the as-of date (what the report used); outcomes only use bars on or after the as-of date, so the
/// original recommendation is never rewritten with hindsight.
/// </summary>
public sealed class PreMarketEvaluationService(IMarketDataProvider market, AppDbContext db)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Percent return after n sessions; null when fewer than n sessions have occurred.</summary>
    public static decimal? ReturnAfter(decimal reference, DateOnly asOf, IReadOnlyList<MarketDailyClose> closes, int sessions)
    {
        if (reference <= 0) return null;
        var after = closes.Where(c => c.Date >= asOf).OrderBy(c => c.Date).ToList();
        return after.Count >= sessions ? Math.Round((after[sessions - 1].Close / reference - 1) * 100, 2) : null;
    }

    public async Task<EvaluationResult?> EvaluateAsync(int runId, CancellationToken ct = default)
    {
        var run = await db.PreMarketAgentRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run?.ReportJson is null) return null;
        var report = JsonSerializer.Deserialize<PreMarketReportDto>(run.ReportJson, Json)!;
        var rows = new List<EvaluationRow>();
        var cache = new Dictionary<string, IReadOnlyList<MarketDailyClose>?>();

        async Task<IReadOnlyList<MarketDailyClose>?> Get(string sym)
        {
            if (cache.TryGetValue(sym, out var c)) return c;
            try { c = await market.GetDailyClosesAsync(sym, ct); } catch { c = null; }
            return cache[sym] = c;
        }

        foreach (var a in report.Holdings.Concat(report.Watchlist).Where(a => a.Action != "INSUFFICIENT_DATA" && a.LastClose is not null))
        {
            var closes = await Get(a.Symbol);
            var b = await Get(a.Symbol.EndsWith(".TO") ? "XIU.TO" : "SPY");
            var refBench = b?.Where(c => c.Date < run.AsOfDate).OrderBy(c => c.Date).LastOrDefault()?.Close ?? 0;
            decimal? R(IReadOnlyList<MarketDailyClose>? s, decimal reference, int n) => s is null ? null : ReturnAfter(reference, run.AsOfDate, s, n);
            decimal? X(decimal? r, int n) => r is null ? null : R(b, refBench, n) is { } br ? Math.Round(r.Value - br, 2) : null;
            var r1 = R(closes, a.LastClose!.Value, 1); var r5 = R(closes, a.LastClose.Value, 5); var r20 = R(closes, a.LastClose.Value, 20);
            rows.Add(new EvaluationRow(a.Symbol, a.Action, a.Priority, a.LastClose, a.PriceDate, r1, r5, r20, X(r1, 1), X(r5, 5), X(r20, 20)));
        }
        return new EvaluationResult(runId, run.AsOfDate, "SPY (US) / XIU.TO (Canada)", rows,
            "Unadjusted closes. A positive subsequent move is not proof the suggestion was good; use as a sanity check only.");
    }
}
