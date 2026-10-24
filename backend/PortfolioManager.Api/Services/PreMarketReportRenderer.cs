using System.Net;
using System.Text;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public static class PreMarketReportRenderer
{
    public static string Subject(PreMarketReportDto r) =>
        $"Portfolio Manager | Pre-Market Investment Audit | {r.AsOfDate:yyyy-MM-dd} | {(r.IsPartial ? "Partial" : "Complete")}"
        + (r.IsTestRun ? " | TEST" : "");

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    public sealed record SectorGroup(string Sector, List<StockAuditDto> Items, int Holdings, int Watch, int High, int Medium, List<string> Events);

    private const string Unclassified = "Unclassified";
    public static string SectorOf(StockAuditDto a) => string.IsNullOrWhiteSpace(a.Sector) ? Unclassified : a.Sector;

    /// <summary>Securities grouped by sector, sectors with the most urgent items first.</summary>
    public static List<SectorGroup> BySector(PreMarketReportDto r) =>
        r.Holdings.Concat(r.Watchlist).GroupBy(SectorOf)
            .Select(g => new SectorGroup(
                g.Key,
                g.OrderBy(a => a.Priority == "HIGH" ? 0 : a.Priority == "MEDIUM" ? 1 : 2).ThenBy(a => a.Symbol).ToList(),
                g.Count(a => a.Kind == "HOLDING"), g.Count(a => a.Kind == "WATCHLIST"),
                g.Count(a => a.Priority == "HIGH"), g.Count(a => a.Priority == "MEDIUM"),
                (r.Events ?? []).Where(e => e.AffectedSectors.Any(s => g.Key.Contains(s, StringComparison.OrdinalIgnoreCase))).Select(e => e.Category).ToList()))
            .OrderByDescending(s => s.High).ThenByDescending(s => s.Medium).ThenBy(s => s.Sector == Unclassified ? 1 : 0).ThenBy(s => s.Sector).ToList();

    /// <summary>Plain-text alternative: summary, priorities, events and every symbol with its action.</summary>
    public static string RenderText(PreMarketReportDto r)
    {
        var all = r.Holdings.Concat(r.Watchlist).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"PRE-MARKET INVESTMENT AUDIT {r.AsOfDate:yyyy-MM-dd} ({(r.IsPartial ? "PARTIAL" : "COMPLETE")}{(r.IsTestRun ? ", TEST" : "")})");
        sb.AppendLine("Decision support only. Shadow mode, no trades placed. Not advice.");
        sb.AppendLine($"{r.Holdings.Count} holdings, {r.Watchlist.Count} watchlist, {(r.Events ?? []).Count} global events. AI: {r.AiStatus}");
        sb.AppendLine().AppendLine("HIGHEST-PRIORITY ACTIONS");
        foreach (var a in all.Where(a => a.Priority is "HIGH" or "MEDIUM").OrderBy(a => a.Priority == "HIGH" ? 0 : 1).ThenBy(a => a.Symbol))
            sb.AppendLine($"- {a.Symbol} [{a.Priority}] {a.Action}: {a.Rationale}");
        sb.AppendLine().AppendLine("GLOBAL EVENTS");
        foreach (var e in r.Events ?? [])
        {
            sb.AppendLine($"- {e.Category}: {e.Title}");
            sb.AppendLine($"  Mechanism (hypothesis): {e.Mechanism}");
            sb.AppendLine($"  Counterargument: {e.Counterargument}");
            sb.AppendLine($"  Possibly exposed: {(e.AffectedSymbols.Count == 0 ? "none" : string.Join(", ", e.AffectedSymbols))}");
            foreach (var n in e.Evidence) sb.AppendLine($"  Source: {n.Headline} ({n.Publisher}) {(n.IsMock ? "[MOCK]" : n.Url)}");
        }
        sb.AppendLine().AppendLine("BY SECTOR");
        foreach (var g in BySector(r))
        {
            sb.AppendLine().AppendLine($"{g.Sector.ToUpperInvariant()} ({g.Holdings} holdings, {g.Watch} watchlist, {g.High} high, {g.Medium} medium){(g.Events.Count > 0 ? " - exposed to: " + string.Join(", ", g.Events) : "")}");
            foreach (var a in g.Items)
                sb.AppendLine($"{a.Symbol,-10} {a.Kind,-9} {a.Action,-17} {a.Priority,-6} RSI14 {a.Rsi14?.ToString("0.0") ?? "n/a"}  close {a.LastClose?.ToString("0.00") ?? "n/a"}");
        }
        sb.AppendLine().AppendLine("DATA QUALITY");
        foreach (var d in r.DataQuality) sb.AppendLine("- " + d);
        return sb.ToString();
    }
    private static string N(decimal? v, string f = "0.##") => v?.ToString(f) ?? "n/a";

    private static string Link(NewsItemDto n) =>
        n.IsMock ? $"{E(n.Headline)} <span class=\"m\">(MOCK, no real source)</span>"
                 : $"<a href=\"{E(n.Url)}\">{E(n.Headline)}</a>";

    public static string Render(PreMarketReportDto r)
    {
        var all = r.Holdings.Concat(r.Watchlist).ToList();
        var events = r.Events ?? [];
        var top = all.Where(a => a.Priority is "HIGH" or "MEDIUM")
            .OrderBy(a => a.Priority == "HIGH" ? 0 : 1).ThenBy(a => a.Symbol).ToList();
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<style>body{margin:0;background:#f5f5f5;font-family:Segoe UI,Arial,sans-serif;color:#222}.w{max-width:760px;margin:0 auto;padding:12px}")
          .Append(".h{background:#0d47a1;color:#fff;border-radius:10px;padding:16px}.h h1{margin:0;font-size:18px}.h p{margin:4px 0 0;font-size:12px;opacity:.9}")
          .Append("h2{font-size:13px;text-transform:uppercase;letter-spacing:.08em;border-bottom:2px solid #ddd;padding-bottom:4px;margin:20px 0 8px}h3{font-size:13px;margin:14px 0 4px;color:#0d47a1}")
          .Append("table{width:100%;border-collapse:collapse;background:#fff;font-size:12px}th,td{padding:6px 8px;border-bottom:1px solid #eee;text-align:left;vertical-align:top}th{background:#f0f4ff;font-size:11px}")
          .Append(".warn{background:#fff8e1;border-left:4px solid #f9a825;padding:8px 10px;font-size:12px;margin:6px 0}.HIGH{color:#c62828;font-weight:700}.MEDIUM{color:#e65100;font-weight:700}")
          .Append(".card{background:#fff;border-radius:8px;padding:10px;margin:8px 0;font-size:12px}.m{color:#777;font-size:11px}a{color:#0d47a1}</style></head><body><div class=\"w\">");
        sb.Append($"<div class=\"h\"><h1>Pre-Market Investment Audit</h1><p>As of {r.AsOfDate:yyyy-MM-dd} &middot; generated {r.GeneratedUtc:yyyy-MM-dd HH:mm} UTC &middot; {(r.IsPartial ? "PARTIAL REPORT" : "Complete")}{(r.IsTestRun ? " &middot; TEST RUN" : "")}</p></div>");
        sb.Append("<div class=\"warn\"><b>Decision support only.</b> Shadow mode: no trades are placed. Suggestions are research prompts, not advice or guarantees.</div>");

        sb.Append($"<h2>1. Executive summary</h2><div class=\"card\">{r.Holdings.Count} holdings and {r.Watchlist.Count} watchlist symbols reviewed; {events.Count} global event(s) detected; {top.Count} item(s) need attention. AI: {E(r.AiStatus)}.</div>");

        sb.Append("<h2>2. Highest-priority actions</h2>");
        if (top.Count == 0) sb.Append("<div class=\"card\">No high or medium priority items.</div>");
        else
        {
            sb.Append("<table><tr><th>Symbol</th><th>Action</th><th>Priority</th><th>Why</th></tr>");
            foreach (var a in top)
                sb.Append($"<tr><td><b>{E(a.Symbol)}</b></td><td>{E(a.Action)}</td><td class=\"{a.Priority}\">{a.Priority}</td><td>{E(a.Rationale)}</td></tr>");
            sb.Append("</table>");
        }

        sb.Append("<h2>3. Global events and portfolio impact</h2>");
        if (events.Count == 0) sb.Append("<div class=\"card\">No major global events detected in the approved sources.</div>");
        foreach (var e in events)
        {
            sb.Append($"<div class=\"card\"><b>{E(e.Category.Replace('_', ' '))}</b>: {E(e.Title)}<br>");
            if (e.AiSummary is not null) sb.Append($"<i>AI summary:</i> {E(e.AiSummary)}<br>");
            sb.Append($"<i>Possible mechanism (hypothesis):</i> {E(e.Mechanism)}<br><i>Counterargument:</i> {E(e.Counterargument)}<br>");
            if (e.AiPortfolioNote is not null) sb.Append($"<i>Portfolio note (AI):</i> {E(e.AiPortfolioNote)}<br>");
            sb.Append($"<span class=\"m\">Sectors: {E(string.Join(", ", e.AffectedSectors))} &middot; Possibly exposed: {E(e.AffectedSymbols.Count == 0 ? "none of your holdings" : string.Join(", ", e.AffectedSymbols))}</span>");
            foreach (var n in e.Evidence) sb.Append($"<div>&bull; {Link(n)} <span class=\"m\">{E(n.Publisher)}, {n.PublishedUtc:yyyy-MM-dd HH:mm} UTC</span></div>");
            sb.Append("</div>");
        }

        sb.Append("<h2>4. Sector overview</h2>");
        var groups = BySector(r);
        sb.Append("<table><tr><th>Sector</th><th>Hold</th><th>Watch</th><th>High</th><th>Med</th><th>Exposed to</th></tr>");
        foreach (var g in groups)
            sb.Append($"<tr><td><b>{E(g.Sector)}</b></td><td>{g.Holdings}</td><td>{g.Watch}</td><td class=\"HIGH\">{(g.High == 0 ? "" : g.High)}</td><td class=\"MEDIUM\">{(g.Medium == 0 ? "" : g.Medium)}</td><td>{E(string.Join(", ", g.Events).Replace('_', ' '))}</td></tr>");
        sb.Append("</table>");

        sb.Append("<h2>5. Holdings and watchlist by sector</h2>");
        foreach (var g in groups)
        {
            sb.Append($"<h3>{E(g.Sector)} <span class=\"m\">{g.Holdings} holdings, {g.Watch} watchlist</span></h3>");
            // Every holding is listed; watchlist symbols appear as rows only when they need attention.
            var rows = g.Items.Where(a => a.Kind == "HOLDING" || a.Priority is "HIGH" or "MEDIUM").ToList();
            var rest = g.Items.Where(a => a.Kind == "WATCHLIST" && a.Priority is not ("HIGH" or "MEDIUM")).ToList();
            if (rows.Count > 0) sb.Append(Table(rows));
            if (rest.Count > 0)
                sb.Append($"<div class=\"card m\"><b>Other watchlist ({rest.Count}), no trigger:</b> {E(string.Join(", ", rest.Select(a => a.Symbol + (a.Rsi14 is null ? "" : $" ({a.Rsi14:0})"))))}</div>");
        }

        sb.Append("<h2>6. Individual stock analyses and evidence</h2>");
        // Keep the email compact (Gmail clips messages near 100 KB): full cards only where there is something to review.
        var detailed = all.Where(a => a.Priority is "HIGH" or "MEDIUM" || a.Ai is not null)
            .OrderBy(a => SectorOf(a)).ThenBy(a => a.Priority == "HIGH" ? 0 : 1).ToList();
        sb.Append($"<div class=\"m\">Detailed analysis for {detailed.Count} of {all.Count} securities (priority items and AI-analysed), grouped by sector; all others are in the tables above.</div>");
        string? lastSector = null;
        foreach (var a in detailed)
        {
            if (SectorOf(a) != lastSector) { lastSector = SectorOf(a); sb.Append($"<h3>{E(lastSector)}</h3>"); }
            sb.Append($"<div class=\"card\"><b>{E(a.Symbol)}</b> <span class=\"{a.Priority}\">{E(a.Action)}</span> <span class=\"m\">news: {E(a.NewsStatus)}</span><br>{E(a.Rationale)}");
            if (a.Ai is { } ai)
            {
                foreach (var x in ai.Bullish) sb.Append($"<div>+ {E(x)}</div>");
                foreach (var x in ai.Bearish) sb.Append($"<div>&minus; {E(x)}</div>");
                foreach (var x in ai.Risks) sb.Append($"<div>&#9888; {E(x)}</div>");
                sb.Append($"<div class=\"m\">AI inference (not fact). Evidence: {E(string.Join(", ", ai.EvidenceIds))}</div>");
            }
            foreach (var n in a.News.Take(2)) sb.Append($"<div>&bull; {Link(n)} <span class=\"m\">{E(n.Publisher)}, {n.PublishedUtc:yyyy-MM-dd HH:mm} UTC</span></div>");
            foreach (var w in a.Warnings.Where(w => !w.StartsWith("News COVERAGE") && !w.StartsWith("News SOURCE"))) sb.Append($"<div class=\"m\">&#9888; {E(w)}</div>");
            sb.Append("</div>");
        }

        sb.Append("<h2>7. Data quality and audit</h2>");
        foreach (var d in r.DataQuality) sb.Append($"<div class=\"warn\">{E(d)}</div>");
        if (r.Metrics is { } m)
            sb.Append($"<div class=\"m\">Articles {m.ArticlesRetrieved} &middot; events {m.Events} &middot; AI calls {m.AiCalls} ({m.AiDurationMs} ms, model {E(m.AiModel ?? "none")}) &middot; news {m.NewsDurationMs} ms</div>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static string Table(IReadOnlyList<StockAuditDto> rows)
    {
        if (rows.Count == 0) return "<div class=\"card\">None.</div>";
        var sb = new StringBuilder("<table><tr><th>Symbol</th><th>Shares</th><th>P/L %</th><th>Weight</th>");
        sb.Append("<th>Last close</th><th>RSI14</th><th>5d %</th><th>Action</th></tr>");
        foreach (var a in rows)
        {
            var held = a.Kind == "HOLDING";
            sb.Append($"<tr><td><b>{E(a.Symbol)}</b>{(held ? "" : " <span class=\"m\">watch</span>")}<div class=\"m\">{E(a.CompanyName)}</div></td>");
            sb.Append(held
                ? $"<td>{N(a.Shares)}</td><td>{N(a.UnrealizedPct)}</td><td>{(a.WeightPct is null ? "n/a" : a.WeightPct + "% " + a.Currency)}</td>"
                : "<td>&ndash;</td><td>&ndash;</td><td>&ndash;</td>");
            sb.Append($"<td>{N(a.LastClose, "0.00")}<div class=\"m\">{a.PriceDate:yyyy-MM-dd}</div></td><td>{N(a.Rsi14, "0.0")}</td><td>{N(a.Change5dPct)}</td><td>{E(a.Action)}<div class=\"m {a.Priority}\">{a.Priority}</div>{(a.Change is "NEW" or "ESCALATED" or "ACTION CHANGED" or "DE-ESCALATED" ? $"<div class=\"m\">{a.Change}</div>" : "")}{(a.EarningsInDays is { } ed ? $"<div class=\"m\">Earnings in {ed}d</div>" : "")}</td></tr>");
        }
        return sb.Append("</table>").ToString();
    }
}
