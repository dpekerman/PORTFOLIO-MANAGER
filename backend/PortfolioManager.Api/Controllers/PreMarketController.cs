using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

public sealed record RunPreMarketRequest(
    DateOnly? AsOfDate, bool DryRun = true, bool UseMockNews = false, bool SendEmail = false, bool Force = true);

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/premarket")]
public sealed class PreMarketController(IPreMarketAgentService agent, AppDbContext db) : ControllerBase
{
    [HttpGet("config")]
    public async Task<PreMarketAgentConfig> GetConfig(CancellationToken ct) => await agent.GetConfigAsync(ct);

    [HttpPut("config")]
    public async Task<PreMarketAgentConfig> SaveConfig(PreMarketAgentConfig cfg, CancellationToken ct) =>
        await agent.SaveConfigAsync(cfg, ct);

    [HttpPost("run")]
    public async Task<ActionResult<object>> Run(RunPreMarketRequest r, CancellationToken ct)
    {
        var run = await agent.RunAsync(new PreMarketRunOptions(
            r.AsOfDate, r.DryRun, r.UseMockNews, r.SendEmail, r.Force, IsTest: true), ct);
        return Summary(run);
    }

    [HttpGet("runs")]
    public async Task<object> List(CancellationToken ct) =>
        (await db.PreMarketAgentRuns.AsNoTracking().OrderByDescending(r => r.StartedAtUtc).Take(30)
            .Select(r => new { r.Id, r.RunKey, r.AsOfDate, r.IsTestRun, r.Status, r.EmailStatus, r.DurationMs, r.StartedAtUtc })
            .ToListAsync(ct));

    [HttpGet("runs/{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var run = await db.PreMarketAgentRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null ? NotFound() : Ok(new { run.Id, run.RunKey, run.Status, run.EmailStatus, run.Error, report = run.ReportJson });
    }

    [HttpGet("runs/{id:int}/html")]
    public async Task<IActionResult> Html(int id, CancellationToken ct)
    {
        var html = await db.PreMarketAgentRuns.AsNoTracking().Where(r => r.Id == id).Select(r => r.ReportHtml).FirstOrDefaultAsync(ct);
        return html is null ? NotFound() : Content(html, "text/html");
    }

    private static object Summary(PreMarketAgentRun r) => new { r.Id, r.RunKey, r.Status, r.EmailStatus, r.EmailMessage, r.DurationMs, r.Error };
}
