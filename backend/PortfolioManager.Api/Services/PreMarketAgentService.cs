using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface INewsProvider
{
    Task<NewsLookupResult> GetCompanyNewsAsync(string symbol, DateOnly asOf, CancellationToken ct = default);
}

/// <summary>Real news is not connected yet (Phase 3): reports COVERAGE_INCOMPLETE, never "no news".</summary>
public sealed class NotConnectedNewsProvider : INewsProvider
{
    public Task<NewsLookupResult> GetCompanyNewsAsync(string symbol, DateOnly asOf, CancellationToken ct = default) =>
        Task.FromResult(new NewsLookupResult("COVERAGE_INCOMPLETE", [], "No news provider connected yet."));
}

/// <summary>Deterministic, clearly labelled MOCK news used only for testing the pipeline.</summary>
public sealed class MockNewsProvider : INewsProvider
{
    public Task<NewsLookupResult> GetCompanyNewsAsync(string symbol, DateOnly asOf, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var item = new NewsItemDto(
            $"MOCK-{symbol}-{asOf:yyyyMMdd}", $"[MOCK] {symbol} sample headline", "Mock Publisher",
            "https://example.com/mock", asOf.ToDateTime(new TimeOnly(6, 0)), now,
            "Sample item to exercise the report pipeline. Not real news.", true);
        return Task.FromResult(new NewsLookupResult("NEWS_FOUND", [item]));
    }
}

public interface IPreMarketAgentService
{
    Task<PreMarketAgentConfig> GetConfigAsync(CancellationToken ct = default);
    Task<PreMarketAgentConfig> SaveConfigAsync(PreMarketAgentConfig cfg, CancellationToken ct = default);
    Task<PreMarketAgentRun> RunAsync(PreMarketRunOptions options, CancellationToken ct = default);
}

public sealed class PreMarketAgentService(
    AppDbContext db,
    IMarketDataProvider market,
    IServiceProvider sp,
    EmailNotificationService email,
    ILogger<PreMarketAgentService> logger) : IPreMarketAgentService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static DateOnly TorontoToday(DateTime? utcNow = null)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, tz));
    }

    public async Task<PreMarketAgentConfig> GetConfigAsync(CancellationToken ct = default)
    {
        var cfg = await db.PreMarketAgentConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = new PreMarketAgentConfig();
            db.PreMarketAgentConfigs.Add(cfg);
            await db.SaveChangesAsync(ct);
        }
        return cfg;
    }

    public async Task<PreMarketAgentConfig> SaveConfigAsync(PreMarketAgentConfig input, CancellationToken ct = default)
    {
        var cfg = await GetConfigAsync(ct);
        cfg.ScheduleEnabled = input.ScheduleEnabled;
        cfg.StartTimeLocal = TimeOnly.TryParse(input.StartTimeLocal, out _) ? input.StartTimeLocal : cfg.StartTimeLocal;
        cfg.DeadlineLocal = TimeOnly.TryParse(input.DeadlineLocal, out _) ? input.DeadlineLocal : cfg.DeadlineLocal;
        cfg.FirstRunDate = input.FirstRunDate;
        cfg.AsOfDate = input.AsOfDate;
        cfg.ShadowMode = true; // no automated trading in this feature
        cfg.UserId = input.UserId;
        await db.SaveChangesAsync(ct);
        return cfg;
    }

    public async Task<PreMarketAgentRun> RunAsync(PreMarketRunOptions o, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var cfg = await GetConfigAsync(ct);
        var asOf = o.AsOfDate ?? cfg.AsOfDate ?? TorontoToday();
        var key = $"{asOf:yyyy-MM-dd}|{(o.IsTest ? "TEST" : "REAL")}";

        var run = await db.PreMarketAgentRuns.FirstOrDefaultAsync(r => r.RunKey == key, ct);
        if (run is not null && !o.Force && run.Status is "COMPLETE" or "PARTIAL" && run.ReportJson is not null)
            return run; // idempotent: do not duplicate a finished run

        if (run is null)
        {
            run = new PreMarketAgentRun { RunKey = key, AsOfDate = asOf, IsTestRun = o.IsTest };
            db.PreMarketAgentRuns.Add(run);
        }
        run.Status = "RUNNING"; run.StartedAtUtc = started; run.Error = null; run.EmailStatus = "NOT_SENT";
        await db.SaveChangesAsync(ct);

        try
        {
            INewsProvider news = o.UseMockNews ? new MockNewsProvider() : sp.GetRequiredService<INewsProvider>();
            var report = await BuildReportAsync(cfg, asOf, o.IsTest, news, ct);
            run.ReportJson = JsonSerializer.Serialize(report, Json);
            run.ReportHtml = PreMarketReportRenderer.Render(report);
            run.Status = report.IsPartial ? "PARTIAL" : "COMPLETE";

            if (o.SendEmail && !o.DryRun)
            {
                var subject = PreMarketReportRenderer.Subject(report);
                var (ok, msg, _) = await email.SendAutomationSummaryAsync(subject, run.ReportHtml, ct);
                run.EmailStatus = ok ? "SMTP_ACCEPTED" : "FAILED";
                run.EmailMessage = msg;
                run.EmailSubmittedAtUtc = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pre-market run {Key} failed", key);
            run.Status = "FAILED";
            run.Error = ex.Message;
        }
        run.CompletedAtUtc = DateTime.UtcNow;
        run.DurationMs = (int)(run.CompletedAtUtc.Value - started).TotalMilliseconds;
        await db.SaveChangesAsync(ct);
        return run;
    }

    private async Task<PreMarketReportDto> BuildReportAsync(
        PreMarketAgentConfig cfg, DateOnly asOf, bool isTest, INewsProvider news, CancellationToken ct)
    {
        var holdQ = db.PortfolioItems.AsNoTracking()
            .Where(p => p.TransactionType != "CLOSE" && p.Shares > 0);
        var watchQ = db.WatchlistItems.AsNoTracking();
        if (!string.IsNullOrEmpty(cfg.UserId))
        {
            holdQ = holdQ.Where(p => p.UserId == cfg.UserId);
            watchQ = watchQ.Where(w => w.UserId == cfg.UserId);
        }
        var holdings = await holdQ.ToListAsync(ct);
        var watch = await watchQ.ToListAsync(ct);

        var warnings = new List<string>
        {
            "Prices are daily closes before the as-of date; no currency conversion is applied.",
            "AI analysis is not enabled in this version: actions come from deterministic RSI rules only.",
        };

        // One row per symbol; merge multiple lots into a weighted-average position.
        var held = holdings.GroupBy(h => h.Symbol.ToUpperInvariant()).Select(g =>
        {
            var shares = g.Sum(x => x.Shares);
            var cost = shares == 0 ? 0 : g.Sum(x => x.Shares * x.AverageCostBasis) / shares;
            var first = g.First();
            return (Symbol: g.Key, first.CompanyName, first.Sector, Shares: shares, Cost: cost);
        }).ToList();
        var heldSet = held.Select(h => h.Symbol).ToHashSet();

        var holdAudits = new List<StockAuditDto>();
        foreach (var h in held)
            holdAudits.Add(await AuditAsync(h.Symbol, h.CompanyName, "HOLDING", h.Sector, h.Shares, h.Cost, asOf, news, ct));

        var watchAudits = new List<StockAuditDto>();
        foreach (var sym in watch.Select(w => w.Symbol.ToUpperInvariant()).Distinct().Where(s => !heldSet.Contains(s)))
            watchAudits.Add(await AuditAsync(sym, "", "WATCHLIST", "", null, null, asOf, news, ct));

        var all = holdAudits.Concat(watchAudits).ToList();
        if (all.Count == 0) warnings.Add("No holdings or watchlist symbols were found for the configured user.");
        var missing = all.Count(a => a.Action == "INSUFFICIENT_DATA");
        if (missing > 0) warnings.Add($"{missing} symbol(s) lack enough price history.");
        if (all.Any(a => a.NewsStatus == "COVERAGE_INCOMPLETE"))
            warnings.Add("News coverage is incomplete: absence of news is NOT evidence that nothing happened.");

        return new PreMarketReportDto(asOf, DateTime.UtcNow, missing > 0, isTest, cfg.ShadowMode,
            "UNAVAILABLE (deterministic mode)", holdAudits, watchAudits, warnings);
    }

    private async Task<StockAuditDto> AuditAsync(
        string symbol, string name, string kind, string sector, decimal? shares, decimal? cost,
        DateOnly asOf, INewsProvider news, CancellationToken ct)
    {
        var warns = new List<string>();
        IReadOnlyList<MarketDailyClose>? closes = null;
        try { closes = await market.GetDailyClosesAsync(symbol, ct); }
        catch (Exception ex) { warns.Add($"Price retrieval failed: {ex.Message}"); }

        // Look-ahead guard: only bars strictly before the as-of (pre-market) date.
        var series = closes?.Where(c => c.Date < asOf).OrderBy(c => c.Date).ToList() ?? [];
        var prices = series.Select(c => c.Close).ToList();
        var last = series.LastOrDefault();
        var rsi14 = PreMarketMath.Rsi(prices, 14);
        var rsi9 = PreMarketMath.Rsi(prices, 9);
        decimal? chg5 = prices.Count >= 6 && prices[^6] != 0 ? Math.Round((prices[^1] / prices[^6] - 1) * 100, 2) : null;
        decimal? pnl = cost is > 0 && last is not null ? Math.Round((last.Close / cost.Value - 1) * 100, 2) : null;

        if (last is not null && asOf.DayNumber - last.Date.DayNumber > 5)
            warns.Add($"Latest price is stale ({last.Date:yyyy-MM-dd}).");

        var n = await news.GetCompanyNewsAsync(symbol, asOf, ct);
        var (action, priority, why) = PreMarketMath.Decide(kind == "HOLDING", rsi14, pnl, last is not null);
        return new StockAuditDto(symbol, name, kind, sector, shares, cost, last?.Close, last?.Date, pnl,
            rsi14, rsi9, chg5, n.Status, n.Items, action, priority, why, warns);
    }
}

public static class PreMarketMath
{
    /// <summary>Wilder RSI; null when fewer than period+1 closes.</summary>
    public static decimal? Rsi(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count < period + 1) return null;
        decimal gain = 0, loss = 0;
        for (var i = 1; i <= period; i++)
        {
            var d = closes[i] - closes[i - 1];
            if (d >= 0) gain += d; else loss -= d;
        }
        gain /= period; loss /= period;
        for (var i = period + 1; i < closes.Count; i++)
        {
            var d = closes[i] - closes[i - 1];
            gain = (gain * (period - 1) + Math.Max(d, 0)) / period;
            loss = (loss * (period - 1) + Math.Max(-d, 0)) / period;
        }
        if (loss == 0) return 100m;
        return Math.Round(100m - 100m / (1m + gain / loss), 1);
    }

    public static (string Action, string Priority, string Rationale) Decide(bool held, decimal? rsi14, decimal? pnlPct, bool hasPrice)
    {
        if (!hasPrice || rsi14 is null)
            return ("INSUFFICIENT_DATA", "NONE", "Not enough validated price history to assess technicals.");
        var r = rsi14.Value;
        if (held)
        {
            if (r >= 70) return ("REDUCE_CANDIDATE", "MEDIUM", $"RSI(14) {r} is overbought; consider reviewing position size. Not a sell instruction.");
            if (r <= 30) return ("ADD_CANDIDATE", "MEDIUM", $"RSI(14) {r} is oversold; review fundamentals/news before adding.");
            if (pnlPct is <= -15) return ("INVESTIGATE", "HIGH", $"Position is {pnlPct}% below cost; review thesis and risk.");
            return ("HOLD", "LOW", $"RSI(14) {r} is neutral; no technical trigger.");
        }
        if (r <= 30) return ("BUY_CANDIDATE", "MEDIUM", $"RSI(14) {r} is oversold; verify news and fundamentals before acting.");
        if (r >= 70) return ("WATCH", "LOW", $"RSI(14) {r} is overbought; wait for a pullback.");
        return ("WATCH", "LOW", $"RSI(14) {r} is neutral; keep monitoring.");
    }
}
