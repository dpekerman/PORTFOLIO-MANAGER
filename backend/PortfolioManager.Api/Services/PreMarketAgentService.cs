using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

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
    IAiProvider ai,
    EmailNotificationService email,
    ILogger<PreMarketAgentService> logger) : IPreMarketAgentService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly string[] MaterialWords =
        ["earnings", "guidance", "downgrade", "lawsuit", "investigation", "bankruptcy", "trading halt", "recall",
         "merger", "acquisition", "to acquire", "takeover", "profit warning", "restructuring", "fraud", "resigns", "delist"];

    private static TimeZoneInfo Toronto => TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    public static DateOnly TorontoToday(DateTime? utcNow = null) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, Toronto));

    /// <summary>UTC instant of a Toronto-local HH:mm on the given date (DST-safe).</summary>
    public static DateTime TorontoToUtc(DateOnly date, string hhmm)
    {
        var t = TimeOnly.TryParse(hhmm, out var parsed) ? parsed : new TimeOnly(9, 25);
        return TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(t, DateTimeKind.Unspecified), Toronto);
    }

    public static bool DeadlinePassed(PreMarketAgentConfig cfg, DateOnly asOf, DateTime nowUtc) =>
        nowUtc >= TorontoToUtc(asOf, cfg.DeadlineLocal);

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
        if (TimeOnly.TryParse(input.StartTimeLocal, out _)) cfg.StartTimeLocal = input.StartTimeLocal;
        if (TimeOnly.TryParse(input.DeadlineLocal, out _)) cfg.DeadlineLocal = input.DeadlineLocal;
        cfg.FirstRunDate = input.FirstRunDate;
        cfg.AsOfDate = input.AsOfDate;
        cfg.ShadowMode = true; // no automated trading in this feature
        cfg.UserId = string.IsNullOrWhiteSpace(input.UserId) ? null : input.UserId;
        var wantsDiscovery = input.DiscoveryMode is "SMART_DISCOVERY" or "FULL_DISCOVERY";
        var discoveryReady = wantsDiscovery && await db.NewsSources.AnyAsync(
            s => s.Category == "DISCOVERY" && s.Enabled && !s.IsArchived && s.ApprovalStatus == "APPROVED", ct);
        // Discovery only works once the user has approved and enabled the discovery source.
        cfg.DiscoveryMode = discoveryReady ? input.DiscoveryMode : "APPROVED_ONLY";
        cfg.AiEnabled = input.AiEnabled;
        cfg.AiMaxCalls = Math.Clamp(input.AiMaxCalls, 0, 60);
        await db.SaveChangesAsync(ct);
        return cfg;
    }

    public async Task<PreMarketAgentRun> RunAsync(PreMarketRunOptions o, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var started = DateTime.UtcNow;
        var cfg = await GetConfigAsync(ct);
        var asOf = o.AsOfDate ?? cfg.AsOfDate ?? TorontoToday();
        var key = $"{asOf:yyyy-MM-dd}|{(o.IsTest ? "TEST" : "REAL")}";

        var run = await db.PreMarketAgentRuns.FirstOrDefaultAsync(r => r.RunKey == key, ct);
        // Real runs never overlap (scheduler + Task Scheduler can both fire); a RUNNING row older than 3 h is treated as an interrupted run and recovered.
        if (run is not null && !o.IsTest && run.Status == "RUNNING" && run.StartedAtUtc > DateTime.UtcNow.AddHours(-3))
            return run;
        if (run is not null && !o.Force && run.Status is "COMPLETE" or "PARTIAL" && run.ReportJson is not null)
            return run; // idempotent: never duplicate a finished run

        if (run is null)
        {
            run = new PreMarketAgentRun { RunKey = key, AsOfDate = asOf, IsTestRun = o.IsTest };
            db.PreMarketAgentRuns.Add(run);
        }
        run.Status = "RUNNING"; run.StartedAtUtc = started; run.Error = null; run.EmailStatus = "NOT_SENT"; run.EmailMessage = null;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Another process inserted the same run key a moment earlier: that one owns the run.
            db.ChangeTracker.Clear();
            return await db.PreMarketAgentRuns.AsNoTracking().FirstAsync(r => r.RunKey == key, ct);
        }

        try
        {
            INewsProvider news = o.UseMockNews ? new MockNewsProvider() : sp.GetRequiredService<RssNewsProvider>();
            var (report, m) = await BuildReportAsync(cfg, asOf, o.IsTest, news, started, ct);
            run.ReportJson = JsonSerializer.Serialize(report, Json);
            run.ReportHtml = PreMarketReportRenderer.Render(report);
            run.Status = report.IsPartial ? "PARTIAL" : "COMPLETE";
            run.AiModel = m.AiModel; run.AiCalls = m.AiCalls; run.AiDurationMs = m.AiDurationMs;
            run.NewsDurationMs = m.NewsDurationMs; run.ArticlesRetrieved = m.ArticlesRetrieved;

            if (o.SendEmail && !o.DryRun)
            {
                if (!o.IsTest && DeadlinePassed(cfg, asOf, DateTime.UtcNow))
                {
                    run.EmailStatus = "SKIPPED_DEADLINE";
                    run.EmailMessage = "SMTP cutoff passed; report saved but not emailed. Use manual resend.";
                }
                else
                {
                    // Bounded retries with backoff; stop retrying once the SMTP cutoff has passed.
                    var subject = PreMarketReportRenderer.Subject(report);
                    var ok = false; var msg = "";
                    for (var attempt = 1; attempt <= 3 && !ok; attempt++)
                    {
                        (ok, msg, _) = await email.SendHtmlWithTextAsync(subject, run.ReportHtml, PreMarketReportRenderer.RenderText(report), ct);
                        if (ok || (!o.IsTest && DeadlinePassed(cfg, asOf, DateTime.UtcNow))) break;
                        if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(attempt * 10), ct);
                    }
                    run.EmailStatus = ok ? "SMTP_ACCEPTED" : "FAILED";
                    run.EmailMessage = msg;
                    run.EmailSubmittedAtUtc = DateTime.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pre-market run {Key} failed", key);
            run.Status = "FAILED";
            run.Error = ex.Message;
        }
        run.CompletedAtUtc = DateTime.UtcNow;
        run.DurationMs = (int)sw.ElapsedMilliseconds;
        await db.SaveChangesAsync(ct);
        return run;
    }

    private async Task<(PreMarketReportDto Report, RunMetricsDto Metrics)> BuildReportAsync(
        PreMarketAgentConfig cfg, DateOnly asOf, bool isTest, INewsProvider news, DateTime startedUtc, CancellationToken ct)
    {
        var holdQ = db.PortfolioItems.AsNoTracking().Where(p => p.TransactionType != "CLOSE" && p.Shares > 0);
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
            "Portfolio weights are not shown: holdings can be in mixed currencies and no FX conversion is applied.",
            "Free news coverage can be incomplete (especially TSX/TSXV). A missing article is not proof that nothing happened.",
        };

        var held = holdings.GroupBy(h => h.Symbol.ToUpperInvariant()).Select(g =>
        {
            var shares = g.Sum(x => x.Shares);
            var cost = shares == 0 ? 0 : g.Sum(x => x.Shares * x.AverageCostBasis) / shares;
            var first = g.First();
            return (Symbol: g.Key, first.CompanyName, first.Sector, Shares: shares, Cost: cost);
        }).ToList();
        var heldSet = held.Select(h => h.Symbol).ToHashSet();
        var watchSyms = watch.Select(w => w.Symbol.ToUpperInvariant()).Distinct().Where(s => !heldSet.Contains(s)).ToList();

        // 1) Global news once, classified into events once, mapped to every affected holding.
        var newsSw = Stopwatch.StartNew();
        var global = await news.GetGlobalNewsAsync(asOf, ct);
        var watchSectors = await ResolveSectorsAsync(watchSyms, ct);
        var tracked = held.Select(h => (h.Symbol, h.Sector))
            .Concat(watchSyms.Select(s => (Symbol: s, Sector: watchSectors.GetValueOrDefault(s, ""))))
            .ToList();
        var events = GlobalEventClassifier.Classify(global.Items, tracked).ToList();
        if (global.Note is not null) warnings.Add("Global news: " + global.Note);
        if (global.Status is "SOURCE_UNAVAILABLE" or "COVERAGE_INCOMPLETE") warnings.Add($"Global news status: {global.Status}.");
        var missingSectors = watchSyms.Count(s => !watchSectors.ContainsKey(s));
        if (missingSectors > 0) warnings.Add($"{missingSectors} watchlist symbol(s) have no known sector and are not mapped to global events.");

        // Exchange calendars are independent: a holiday on one exchange never suppresses the other.
        foreach (var ex in new[] { MarketExchange.Tsx, MarketExchange.Us })
            if (!TradingCalendar.IsOpen(ex, asOf))
                warnings.Add($"{(ex == MarketExchange.Tsx ? "TSX/TSXV" : "NYSE/Nasdaq")} is closed on {asOf:yyyy-MM-dd} (holiday or weekend); its symbols are listed with last available data.");

        var mode = cfg.DiscoveryMode;
        var disc = new DiscoveryBudget { Remaining = mode == "FULL_DISCOVERY" ? 25 : mode == "SMART_DISCOVERY" ? 10 : 0 };
        if (mode == "FULL_DISCOVERY")
            for (var i = 0; i < events.Count && disc.Remaining > 0; i++)
            {
                disc.Remaining--;
                var d = await news.DiscoverAsync(events[i].Title, asOf, ct);
                if (d.Items.Count > 0) events[i] = events[i] with { Evidence = events[i].Evidence.Concat(d.Items).ToList() };
            }

        // 2) Company audits.
        var holdAudits = new List<StockAuditDto>();
        foreach (var h in held)
            holdAudits.Add(await AuditAsync(h.Symbol, h.CompanyName, "HOLDING", h.Sector, h.Shares, h.Cost, asOf, news, events, disc, mode, ct));
        var watchAudits = new List<StockAuditDto>();
        foreach (var sym in watchSyms)
            watchAudits.Add(await AuditAsync(sym, "", "WATCHLIST", watchSectors.GetValueOrDefault(sym, ""), null, null, asOf, news, events, disc, mode, ct));
        if (mode != "APPROVED_ONLY")
            warnings.Add($"Discovery mode {mode}: items marked [discovered] come from an approved search feed but their publishers are not individually approved. Verify before acting.");
        var newsMs = (int)newsSw.ElapsedMilliseconds;
        var articles = global.Items.Count + holdAudits.Concat(watchAudits).Sum(a => a.News.Count);

        // What changed since the previous report, and upcoming earnings from the watchlist (both deterministic).
        var prevRun = await db.PreMarketAgentRuns.AsNoTracking()
            .Where(r => r.ReportJson != null && r.AsOfDate < asOf && r.Status != "FAILED")
            .OrderByDescending(r => r.AsOfDate).ThenByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        Dictionary<string, (string Action, string Priority)>? prev = null;
        if (prevRun?.ReportJson is not null)
            try
            {
                var pr = JsonSerializer.Deserialize<PreMarketReportDto>(prevRun.ReportJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                prev = pr?.Holdings.Concat(pr.Watchlist).GroupBy(a => a.Symbol).ToDictionary(g => g.Key, g => (g.First().Action, g.First().Priority));
            }
            catch (JsonException) { prev = null; }
        var earnings = watch.Where(w => w.EarningsDate != null).GroupBy(w => w.Symbol.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Max(w => w.EarningsDate!.Value));
        StockAuditDto Annotate(StockAuditDto a)
        {
            int? days = earnings.TryGetValue(a.Symbol, out var ed) ? (int)(ed.Date - asOf.ToDateTime(TimeOnly.MinValue)).TotalDays : null;
            if (days is < 0 or > 7) days = null;
            string? change = null;
            if (prev is not null)
                change = !prev.TryGetValue(a.Symbol, out var p) ? "NEW"
                    : PreMarketMath.PriorityRank(a.Priority) > PreMarketMath.PriorityRank(p.Priority) ? "ESCALATED"
                    : PreMarketMath.PriorityRank(a.Priority) < PreMarketMath.PriorityRank(p.Priority) ? "DE-ESCALATED"
                    : p.Action != a.Action ? "ACTION CHANGED" : "UNCHANGED";
            var res = a with { Change = change, EarningsInDays = days };
            if (days is <= 3 && PreMarketMath.PriorityRank(a.Priority) < 2)
                res = res with { Priority = "MEDIUM", Rationale = a.Rationale + $" Earnings in {days} day(s): expect higher volatility." };
            return res;
        }
        holdAudits = holdAudits.Select(Annotate).ToList();
        watchAudits = watchAudits.Select(Annotate).ToList();

        // Concentration within each currency bucket (no FX conversion): CAD = TSX/TSXV/NEO suffixes, otherwise USD.
        static string Ccy(string s) => s.EndsWith(".TO") || s.EndsWith(".V") || s.EndsWith(".CN") || s.EndsWith(".NE") ? "CAD" : "USD";
        decimal? Value(StockAuditDto a) => a.Shares is { } sh && a.LastClose is { } p ? sh * p : null;
        var totals = holdAudits.Where(a => Value(a) is not null).GroupBy(a => Ccy(a.Symbol))
            .ToDictionary(g => g.Key, g => g.Sum(a => Value(a)!.Value));
        holdAudits = holdAudits.Select(a => Value(a) is { } v && totals.TryGetValue(Ccy(a.Symbol), out var t) && t > 0
            ? a with { WeightPct = Math.Round(v / t * 100, 1), Currency = Ccy(a.Symbol) } : a with { Currency = Ccy(a.Symbol) }).ToList();
        warnings.RemoveAll(w => w.StartsWith("Portfolio weights are not shown"));
        warnings.Add("Weights are the share of each currency bucket (CAD for .TO/.V symbols, otherwise USD); no FX conversion and cash are not included.");

        // 3) Local AI (sequential, bounded by call count and deadline).
        var aiStatus = "DISABLED";
        var calls = 0; var aiMs = 0; string? model = null; var aiSkipped = false; var aiFailed = 0; var aiConsecutive = 0; string? aiFirstError = null;
        if (cfg.AiEnabled && cfg.AiMaxCalls > 0)
        {
            var (healthy, msg) = await ai.HealthAsync(ct, autoStart: true);
            if (!healthy) aiStatus = "UNAVAILABLE: " + msg;
            else
            {
                model = ai.ModelName;
                var budget = isTest ? TimeSpan.FromMinutes(30)
                    : TorontoToUtc(asOf, cfg.DeadlineLocal).AddMinutes(-10) - DateTime.UtcNow;
                var stop = DateTime.UtcNow + (budget < TimeSpan.Zero ? TimeSpan.Zero : budget);

                bool Budget() => calls < cfg.AiMaxCalls && DateTime.UtcNow < stop && aiConsecutive < 3;
                void Track(AiResult res) { calls++; aiMs += res.DurationMs; if (res.Ok) aiConsecutive = 0; else { aiFailed++; aiConsecutive++; aiFirstError ??= res.Error; } }
                for (var i = 0; i < events.Count && Budget(); i++)
                {
                    var e = events[i];
                    var allowed = e.Evidence.Select(x => x.EvidenceId).ToHashSet();
                    var r = await ai.GenerateJsonAsync(AiAnalysis.System, AiAnalysis.EventPrompt(e, e.AffectedSymbols), AiAnalysis.EventSchema, 300, ct);
                    Track(r);
                    var parsed = r.Ok && r.Json is not null ? AiAnalysis.ParseEvent(r.Json, allowed) : null;
                    if (parsed is { } p) events[i] = e with { AiSummary = p.Summary, AiPortfolioNote = p.Note };
                }
                var order = holdAudits.Concat(watchAudits)
                    .Where(a => a.News.Count > 0 || a.EventIds is { Count: > 0 })
                    .OrderBy(a => a.Priority == "HIGH" ? 0 : a.Priority == "MEDIUM" ? 1 : 2).ToList();
                var byEvent = events.ToDictionary(e => e.EventId);
                var results = new Dictionary<string, AiAnalysisDto>();
                foreach (var a in order)
                {
                    if (!Budget()) { aiSkipped = true; break; }
                    var evs = (a.EventIds ?? []).Select(id => byEvent[id]).ToList();
                    var allowed = a.News.Select(n => n.EvidenceId).Concat(evs.SelectMany(e => e.Evidence.Select(x => x.EvidenceId))).ToHashSet();
                    var r = await ai.GenerateJsonAsync(AiAnalysis.System, AiAnalysis.StockPrompt(a, evs), AiAnalysis.StockSchema, 350, ct);
                    Track(r);
                    if (r.Ok && r.Json is not null && AiAnalysis.ParseStock(r.Json, allowed) is { } parsed) results[a.Symbol] = parsed;
                }
                holdAudits = holdAudits.Select(a => results.TryGetValue(a.Symbol, out var x) ? a with { Ai = x } : a).ToList();
                watchAudits = watchAudits.Select(a => results.TryGetValue(a.Symbol, out var x) ? a with { Ai = x } : a).ToList();
                aiStatus = aiFailed > 0
                    ? $"DEGRADED ({model}): {aiFailed} of {calls} calls failed - {aiFirstError}" + (aiConsecutive >= 3 ? " (AI stopped after 3 consecutive failures)" : "")
                    : $"OK ({model}, {calls} calls)" + (aiSkipped ? " - time/call budget reached; some securities not AI-analysed" : "");
            }
        }
        if (aiStatus.StartsWith("UNAVAILABLE") || aiStatus == "DISABLED" || aiStatus.StartsWith("DEGRADED"))
            warnings.Add($"AI analysis {aiStatus}. Actions are from deterministic rules only.");
        if (aiSkipped) warnings.Add("AI budget reached: remaining securities have deterministic analysis only.");

        var all = holdAudits.Concat(watchAudits).ToList();
        if (all.Count == 0) warnings.Add("No holdings or watchlist symbols were found for the configured user.");
        var missing = all.Count(a => a.Action == "INSUFFICIENT_DATA");
        if (missing > 0) warnings.Add($"{missing} symbol(s) lack enough price history.");
        var partial = missing > 0 || aiSkipped || global.Status == "SOURCE_UNAVAILABLE";

        var metrics = new RunMetricsDto(articles, events.Count, calls, aiMs, newsMs, model);
        return (new PreMarketReportDto(asOf, DateTime.UtcNow, partial, isTest, cfg.ShadowMode, aiStatus,
            holdAudits, watchAudits, warnings, events, metrics), metrics);
    }

    private async Task<StockAuditDto> AuditAsync(
        string symbol, string name, string kind, string sector, decimal? shares, decimal? cost,
        DateOnly asOf, INewsProvider news, IReadOnlyList<GlobalEventDto> events, DiscoveryBudget disc, string mode, CancellationToken ct)
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
        if (last is not null && asOf.DayNumber - last.Date.DayNumber > 5) warns.Add($"Latest price is stale ({last.Date:yyyy-MM-dd}).");

        var n = await news.GetCompanyNewsAsync(symbol, asOf, ct);
        // SMART: only when approved sources found nothing or the price moved a lot; FULL: every holding. Bounded by the budget.
        var unexplainedMove = chg5 is { } c5 && Math.Abs(c5) >= 8;
        if (kind == "HOLDING" && disc.Remaining > 0 && (mode == "FULL_DISCOVERY" || (mode == "SMART_DISCOVERY" && (n.Items.Count == 0 || unexplainedMove))))
        {
            disc.Remaining--;
            var d = await news.DiscoverAsync($"{(string.IsNullOrWhiteSpace(name) ? symbol : name)} stock", asOf, ct);
            if (d.Items.Count > 0) n = new NewsLookupResult("NEWS_FOUND", n.Items.Concat(d.Items).ToList(), n.Note);
        }
        if (n.Status is "SOURCE_UNAVAILABLE" or "COVERAGE_INCOMPLETE") warns.Add($"News {n.Status}: {n.Note}");
        var eventIds = events.Where(e => e.AffectedSymbols.Contains(symbol)).Select(e => e.EventId).ToList();

        var (action, priority, why) = PreMarketMath.Decide(kind == "HOLDING", rsi14, pnl, last is not null);
        (action, priority, why) = PreMarketMath.Escalate(kind == "HOLDING", action, priority, why, n.Items, eventIds.Count);
        return new StockAuditDto(symbol, name, kind, sector, shares, cost, last?.Close, last?.Date, pnl,
            rsi14, rsi9, chg5, n.Status, n.Items, action, priority, why, warns, eventIds);
    }

    public static bool IsMaterial(NewsItemDto n) =>
        MaterialWords.Any(w => n.Headline.Contains(w, StringComparison.OrdinalIgnoreCase));

    /// <summary>Sector per symbol from the SQL cache, looking up (and caching) at most 200 unknown symbols per run.</summary>
    private async Task<Dictionary<string, string>> ResolveSectorsAsync(IReadOnlyList<string> symbols, CancellationToken ct)
    {
        var cached = await db.SecuritySectorCaches.Where(c => symbols.Contains(c.Symbol)).ToDictionaryAsync(c => c.Symbol, c => c.Sector, ct);
        foreach (var s in symbols.Where(s => !cached.ContainsKey(s)).Take(200))
        {
            try
            {
                var (sector, _) = await market.GetSectorAsync(s, ct);
                await Task.Delay(150, ct);
                if (string.IsNullOrWhiteSpace(sector)) continue;
                cached[s] = sector;
                db.SecuritySectorCaches.Add(new SecuritySectorCache { Symbol = s, Sector = sector });
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogDebug("Sector lookup failed for {Symbol}: {Message}", s, ex.Message); }
        }
        await db.SaveChangesAsync(ct);
        return cached;
    }
}

public sealed class DiscoveryBudget { public int Remaining; }

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

    /// <summary>News and global-event exposure raise attention; they never create a buy/sell conclusion by themselves.</summary>
    public static (string Action, string Priority, string Rationale) Escalate(
        bool held, string action, string priority, string why, IReadOnlyList<NewsItemDto> news, int eventCount)
    {
        var material = news.Where(PreMarketAgentService.IsMaterial).ToList();
        if (material.Count > 0)
        {
            // One material headline asks for a look; two or more on a holding is treated as high priority.
            var p = held ? (material.Count >= 2 ? "HIGH" : "MEDIUM") : (material.Count >= 2 ? "MEDIUM" : "LOW");
            var a = action is "HOLD" or "WATCH" or "INSUFFICIENT_DATA" ? "INVESTIGATE" : action;
            return (a, Max(priority, p), why + $" Material news detected ({material.Count}): review the linked sources.");
        }
        if (held && eventCount > 0)
        {
            var a = action == "HOLD" ? "INVESTIGATE" : action;
            return (a, Max(priority, "MEDIUM"), why + $" Exposed to {eventCount} global event(s) via sector; effect is uncertain.");
        }
        return (action, priority, why);
    }

    private static int Rank(string p) => p switch { "HIGH" => 3, "MEDIUM" => 2, "LOW" => 1, _ => 0 };
    public static int PriorityRank(string p) => Rank(p);
    private static string Max(string a, string b) => Rank(a) >= Rank(b) ? a : b;
}
