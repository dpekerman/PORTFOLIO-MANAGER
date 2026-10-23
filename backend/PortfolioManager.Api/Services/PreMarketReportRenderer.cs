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
    private static string N(decimal? v, string f = "0.##") => v?.ToString(f) ?? "n/a";

    public static string Render(PreMarketReportDto r)
    {
        var all = r.Holdings.Concat(r.Watchlist).ToList();
        var top = all.Where(a => a.Priority is "HIGH" or "MEDIUM")
            .OrderBy(a => a.Priority == "HIGH" ? 0 : 1).ThenBy(a => a.Symbol).ToList();
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<style>body{margin:0;background:#f5f5f5;font-family:Segoe UI,Arial,sans-serif;color:#222}.w{max-width:760px;margin:0 auto;padding:12px}")
          .Append(".h{background:#0d47a1;color:#fff;border-radius:10px;padding:16px}.h h1{margin:0;font-size:18px}.h p{margin:4px 0 0;font-size:12px;opacity:.9}")
          .Append("h2{font-size:13px;text-transform:uppercase;letter-spacing:.08em;border-bottom:2px solid #ddd;padding-bottom:4px;margin:20px 0 8px}")
          .Append("table{width:100%;border-collapse:collapse;background:#fff;font-size:12px}th,td{padding:6px 8px;border-bottom:1px solid #eee;text-align:left;vertical-align:top}th{background:#f0f4ff;font-size:11px}")
          .Append(".warn{background:#fff8e1;border-left:4px solid #f9a825;padding:8px 10px;font-size:12px;margin:6px 0}.HIGH{color:#c62828;font-weight:700}.MEDIUM{color:#e65100;font-weight:700}")
          .Append(".card{background:#fff;border-radius:8px;padding:10px;margin:8px 0;font-size:12px}.m{color:#777;font-size:11px}</style></head><body><div class=\"w\">");
        sb.Append($"<div class=\"h\"><h1>Pre-Market Investment Audit</h1><p>As of {r.AsOfDate:yyyy-MM-dd} &middot; generated {r.GeneratedUtc:yyyy-MM-dd HH:mm} UTC &middot; {(r.IsPartial ? "PARTIAL REPORT" : "Complete")}{(r.IsTestRun ? " &middot; TEST RUN" : "")}</p></div>");
        sb.Append("<div class=\"warn\"><b>Decision support only.</b> Shadow mode: no trades are placed. Suggestions are research prompts, not advice or guarantees.</div>");

        sb.Append($"<h2>1. Executive summary</h2><div class=\"card\">{r.Holdings.Count} holdings and {r.Watchlist.Count} watchlist symbols reviewed. {top.Count} need attention. AI analysis: {E(r.AiStatus)}.</div>");

        sb.Append("<h2>2. Highest-priority actions</h2>");
        if (top.Count == 0) sb.Append("<div class=\"card\">No high or medium priority items from the deterministic rules.</div>");
        else
        {
            sb.Append("<table><tr><th>Symbol</th><th>Action</th><th>Priority</th><th>Why</th></tr>");
            foreach (var a in top)
                sb.Append($"<tr><td><b>{E(a.Symbol)}</b></td><td>{E(a.Action)}</td><td class=\"{a.Priority}\">{a.Priority}</td><td>{E(a.Rationale)}</td></tr>");
            sb.Append("</table>");
        }

        sb.Append("<h2>3. Complete holdings audit</h2>").Append(Table(r.Holdings, true));
        sb.Append("<h2>4. Watchlist</h2>").Append(Table(r.Watchlist, false));

        sb.Append("<h2>5. News and evidence</h2>");
        foreach (var a in all)
        {
            sb.Append($"<div class=\"card\"><b>{E(a.Symbol)}</b> <span class=\"m\">news status: {E(a.NewsStatus)}</span>");
            foreach (var n in a.News)
                sb.Append($"<div>&bull; <a href=\"{E(n.Url)}\">{E(n.Headline)}</a> <span class=\"m\">{E(n.Publisher)}, {n.PublishedUtc:yyyy-MM-dd HH:mm} UTC{(n.IsMock ? " (MOCK)" : "")}</span></div>");
            foreach (var w in a.Warnings) sb.Append($"<div class=\"m\">&#9888; {E(w)}</div>");
            sb.Append("</div>");
        }

        sb.Append("<h2>6. Data quality and audit</h2>");
        foreach (var d in r.DataQuality) sb.Append($"<div class=\"warn\">{E(d)}</div>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static string Table(IReadOnlyList<StockAuditDto> rows, bool held)
    {
        if (rows.Count == 0) return "<div class=\"card\">None.</div>";
        var sb = new StringBuilder("<table><tr><th>Symbol</th>");
        if (held) sb.Append("<th>Shares</th><th>P/L %</th>");
        sb.Append("<th>Last close</th><th>RSI14</th><th>5d %</th><th>Action</th></tr>");
        foreach (var a in rows)
        {
            sb.Append($"<tr><td><b>{E(a.Symbol)}</b><div class=\"m\">{E(a.CompanyName)}</div></td>");
            if (held) sb.Append($"<td>{N(a.Shares)}</td><td>{N(a.UnrealizedPct)}</td>");
            sb.Append($"<td>{N(a.LastClose, "0.00")}<div class=\"m\">{a.PriceDate:yyyy-MM-dd}</div></td><td>{N(a.Rsi14, "0.0")}</td><td>{N(a.Change5dPct)}</td><td>{E(a.Action)}<div class=\"m {a.Priority}\">{a.Priority}</div></td></tr>");
        }
        return sb.Append("</table>").ToString();
    }
}
