using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

public sealed record RunPreMarketRequest(
    DateOnly? AsOfDate, bool DryRun = true, bool UseMockNews = false, bool SendEmail = false);

public sealed record NewsSourceRequest(
    string Name, string Url, string Category, int Priority, bool Enabled, string? Markets,
    int RefreshMinutes, int TimeoutSeconds, int MaxItems);

public sealed record FeedbackRequest(string Rating, string? Symbol, string? Comment);

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/premarket")]
public sealed class PreMarketController(
    IPreMarketAgentService agent, AppDbContext db, IAiProvider ai, SafeHttpFetcher fetcher,
    RssNewsProvider rss, EmailNotificationService email) : ControllerBase
{
    private static readonly string[] Categories = ["COMPANY", "GLOBAL", "OFFICIAL", "DISCOVERY"];
    private static readonly string[] Ratings = ["REVIEWED", "USEFUL", "NOT_USEFUL", "ACTION_TAKEN", "NO_ACTION_TAKEN"];

    // ── Config / status ─────────────────────────────────────────────────────
    [HttpGet("config")]
    public async Task<PreMarketAgentConfig> GetConfig(CancellationToken ct) => await agent.GetConfigAsync(ct);

    [HttpPut("config")]
    public async Task<PreMarketAgentConfig> SaveConfig(PreMarketAgentConfig cfg, CancellationToken ct) =>
        await agent.SaveConfigAsync(cfg, ct);

    [HttpGet("status")]
    public async Task<object> Status(CancellationToken ct)
    {
        var cfg = await agent.GetConfigAsync(ct);
        var (ok, msg) = await ai.HealthAsync(ct);
        var today = PreMarketAgentService.TorontoToday();
        DateTime? next = null;
        if (cfg.ScheduleEnabled)
        {
            var d = today;
            if (cfg.FirstRunDate is { } f && d < f) d = f;
            var t = PreMarketAgentService.TorontoToUtc(d, cfg.StartTimeLocal);
            if (t < DateTime.UtcNow) { d = d.AddDays(1); t = PreMarketAgentService.TorontoToUtc(d, cfg.StartTimeLocal); }
            while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) { d = d.AddDays(1); t = PreMarketAgentService.TorontoToUtc(d, cfg.StartTimeLocal); }
            next = t;
        }
        return new { aiHealthy = ok, aiMessage = msg, aiModel = ai.ModelName, nextRunUtc = next, torontoToday = today };
    }

    // ── Runs ────────────────────────────────────────────────────────────────
    [HttpPost("run")]
    public async Task<object> Run(RunPreMarketRequest r, CancellationToken ct)
    {
        var run = await agent.RunAsync(new PreMarketRunOptions(r.AsOfDate, r.DryRun, r.UseMockNews, r.SendEmail, Force: true, IsTest: true), ct);
        return Summary(run);
    }

    [HttpGet("runs")]
    public async Task<object> List(CancellationToken ct) =>
        await db.PreMarketAgentRuns.AsNoTracking().OrderByDescending(r => r.StartedAtUtc).Take(60)
            .Select(r => new
            {
                r.Id, r.RunKey, r.AsOfDate, r.IsTestRun, r.Status, r.EmailStatus, r.DurationMs, r.StartedAtUtc,
                r.AiCalls, r.AiModel, r.ArticlesRetrieved,
            }).ToListAsync(ct);

    [HttpGet("runs/latest")]
    public async Task<IActionResult> Latest(CancellationToken ct)
    {
        var run = await db.PreMarketAgentRuns.AsNoTracking().Where(r => r.ReportJson != null)
            .OrderByDescending(r => r.StartedAtUtc).FirstOrDefaultAsync(ct);
        return run is null ? NoContent() : Ok(Detail(run));
    }

    [HttpGet("runs/{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var run = await db.PreMarketAgentRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null ? NotFound() : Ok(Detail(run));
    }

    [HttpGet("runs/{id:int}/evaluation")]
    public async Task<IActionResult> Evaluation(int id, [FromServices] PreMarketEvaluationService eval, CancellationToken ct)
    {
        var r = await eval.EvaluateAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpGet("runs/{id:int}/html")]
    public async Task<IActionResult> Html(int id, CancellationToken ct)
    {
        var html = await db.PreMarketAgentRuns.AsNoTracking().Where(r => r.Id == id).Select(r => r.ReportHtml).FirstOrDefaultAsync(ct);
        return html is null ? NotFound() : Content(html, "text/html");
    }

    /// <summary>Manual resend of a saved report. SMTP acceptance is reported, not delivery.</summary>
    [HttpPost("runs/{id:int}/resend")]
    public async Task<IActionResult> Resend(int id, CancellationToken ct)
    {
        var run = await db.PreMarketAgentRuns.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run?.ReportHtml is null) return NotFound();
        var subject = $"Portfolio Manager | Pre-Market Investment Audit | {run.AsOfDate:yyyy-MM-dd} | {(run.Status == "PARTIAL" ? "Partial" : "Complete")}{(run.IsTestRun ? " | TEST" : "")} | Resend";
        var report = System.Text.Json.JsonSerializer.Deserialize<PreMarketReportDto>(run.ReportJson!,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var (ok, msg, _) = report is null
            ? await email.SendAutomationSummaryAsync(subject, run.ReportHtml, ct)
            : await email.SendHtmlWithTextAsync(subject, run.ReportHtml, PreMarketReportRenderer.RenderText(report), ct);
        run.EmailStatus = ok ? "SMTP_ACCEPTED" : "FAILED"; run.EmailMessage = msg; run.EmailSubmittedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { ok, message = msg, emailStatus = run.EmailStatus });
    }

    [HttpPost("test-email")]
    public async Task<object> TestEmail()
    {
        var (ok, msg, count) = await email.SendAutomationTestEmailAsync();
        return new { ok, message = msg, recipients = count };
    }

    // ── News sources ────────────────────────────────────────────────────────
    [HttpGet("sources")]
    public async Task<object> Sources(CancellationToken ct)
    {
        await rss.SeedDefaultsAsync(ct);
        return await db.NewsSources.AsNoTracking().Where(s => !s.IsArchived).OrderBy(s => s.Category).ThenBy(s => s.Priority).ToListAsync(ct);
    }

    [HttpPost("sources")]
    public async Task<IActionResult> AddSource(NewsSourceRequest r, CancellationToken ct)
    {
        var s = new NewsSourceConfiguration { ApprovalStatus = "PENDING", Enabled = false };
        return await Save(s, r, isNew: true, ct);
    }

    [HttpPut("sources/{id:int}")]
    public async Task<IActionResult> UpdateSource(int id, NewsSourceRequest r, CancellationToken ct)
    {
        var s = await db.NewsSources.FindAsync([id], ct);
        return s is null ? NotFound() : await Save(s, r, isNew: false, ct);
    }

    [HttpDelete("sources/{id:int}")]
    public async Task<IActionResult> ArchiveSource(int id, CancellationToken ct)
    {
        var s = await db.NewsSources.FindAsync([id], ct);
        if (s is null) return NotFound();
        s.IsArchived = true; s.Enabled = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Newly added sources stay PENDING (and unused) until a user explicitly approves them.</summary>
    [HttpPost("sources/{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken ct)
    {
        var s = await db.NewsSources.FindAsync([id], ct);
        if (s is null) return NotFound();
        if (!UrlGuard.TryValidate(s.Url, out _, out var err)) return BadRequest(new { error = err });
        s.ApprovalStatus = "APPROVED"; s.Enabled = true;
        await db.SaveChangesAsync(ct);
        return Ok(s);
    }

    [HttpPost("sources/{id:int}/test")]
    public async Task<IActionResult> TestSource(int id, CancellationToken ct)
    {
        var s = await db.NewsSources.FindAsync([id], ct);
        if (s is null) return NotFound();
        var url = s.Url.Replace("{symbol}", "AAPL", StringComparison.OrdinalIgnoreCase).Replace("{query}", "stock", StringComparison.OrdinalIgnoreCase);
        var r = await fetcher.FetchAsync(url, s.TimeoutSeconds, ct, url.Contains("sec.gov", StringComparison.OrdinalIgnoreCase) ? "PortfolioManager/1.0 (personal research)" : null);
        var count = 0;
        if (r.Ok) { try { count = FeedParser.Parse(r.Body!, s.MaxItems).Count; } catch { r = r with { Ok = false, Error = "Feed could not be parsed." }; } }
        if (r.Ok) { s.LastSuccessUtc = DateTime.UtcNow; s.LastError = null; } else { s.LastFailureUtc = DateTime.UtcNow; s.LastError = r.Error; }
        s.AvgDurationMs = s.AvgDurationMs == 0 ? r.DurationMs : (s.AvgDurationMs + r.DurationMs) / 2;
        await db.SaveChangesAsync(ct);
        return Ok(new { ok = r.Ok, error = r.Error, articles = count, durationMs = r.DurationMs });
    }

    private async Task<IActionResult> Save(NewsSourceConfiguration s, NewsSourceRequest r, bool isNew, CancellationToken ct)
    {
        if (!Categories.Contains(r.Category)) return BadRequest(new { error = "Unknown category." });
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 120) return BadRequest(new { error = "Name is required (max 120)." });
        if (!UrlGuard.TryValidate(r.Url, out _, out var err)) return BadRequest(new { error = err });
        var urlChanged = !isNew && !string.Equals(s.Url, r.Url, StringComparison.Ordinal);
        s.Name = r.Name.Trim(); s.Url = r.Url.Trim(); s.Category = r.Category;
        s.Priority = Math.Clamp(r.Priority, 1, 100);
        s.Markets = string.IsNullOrWhiteSpace(r.Markets) ? "TSX,US" : r.Markets.Trim();
        s.RefreshMinutes = Math.Clamp(r.RefreshMinutes, 5, 1440);
        s.TimeoutSeconds = Math.Clamp(r.TimeoutSeconds, 2, 30);
        s.MaxItems = Math.Clamp(r.MaxItems, 1, 50);
        if (urlChanged) { s.ApprovalStatus = "PENDING"; s.Enabled = false; } // a changed URL must be re-approved
        else if (s.ApprovalStatus == "APPROVED") s.Enabled = r.Enabled;
        if (isNew) db.NewsSources.Add(s);
        await db.SaveChangesAsync(ct);
        return Ok(s);
    }

    // ── Feedback ────────────────────────────────────────────────────────────
    [HttpPost("runs/{id:int}/feedback")]
    public async Task<IActionResult> AddFeedback(int id, FeedbackRequest r, CancellationToken ct)
    {
        if (!Ratings.Contains(r.Rating)) return BadRequest(new { error = "Unknown rating." });
        if (!await db.PreMarketAgentRuns.AnyAsync(x => x.Id == id, ct)) return NotFound();
        db.PreMarketFeedbacks.Add(new PreMarketFeedback
        {
            RunId = id, Rating = r.Rating, Symbol = r.Symbol?.Trim().ToUpperInvariant(),
            Comment = r.Comment is { Length: > 500 } ? r.Comment[..500] : r.Comment,
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("runs/{id:int}/feedback")]
    public async Task<object> GetFeedback(int id, CancellationToken ct) =>
        await db.PreMarketFeedbacks.AsNoTracking().Where(f => f.RunId == id).OrderByDescending(f => f.CreatedUtc).ToListAsync(ct);

    private static object Summary(PreMarketAgentRun r) => new { r.Id, r.RunKey, r.Status, r.EmailStatus, r.EmailMessage, r.DurationMs, r.Error };
    private static object Detail(PreMarketAgentRun r) => new
    {
        r.Id, r.RunKey, r.AsOfDate, r.IsTestRun, r.Status, r.EmailStatus, r.EmailMessage, r.Error, r.DurationMs, r.StartedAtUtc,
        r.AiModel, r.AiCalls, r.AiDurationMs, r.NewsDurationMs, r.ArticlesRetrieved,
        report = r.ReportJson is null ? null : (object)System.Text.Json.JsonDocument.Parse(r.ReportJson).RootElement,
    };
}
