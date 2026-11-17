using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Api.Controllers;

/// <summary>
/// PM-029 Trading Opportunities (read-only decision support). GET endpoints read stored snapshots only and never call market data;
/// POST refresh builds a new snapshot (single-flight per user → 409 while one is running).
/// </summary>
[Authorize(Roles = "Admin,Trader")]
[ApiController]
[Route("api/opportunities")]
public sealed class OpportunitiesController(
    IOpportunityService service, AppDbContext db, IHostApplicationLifetime lifetime, OpportunityScheduleConfig schedule) : ControllerBase
{
    private OpportunityScheduleDto ScheduleDto() => new(
        schedule.Enabled, schedule.TimeEt, schedule.Days, schedule.LastRunDateEt, schedule.LastRunAtUtc, schedule.LastRunStatus, schedule.LastRunMessage);

    [Authorize(Roles = "Admin")]
    [HttpGet("schedule")]
    public ActionResult<OpportunityScheduleDto> GetSchedule() => Ok(ScheduleDto());

    /// <summary>Saves the automatic refresh schedule. The scheduler picks it up on its next one-minute tick.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("schedule")]
    public ActionResult<OpportunityScheduleDto> PutSchedule(OpportunityScheduleRequest req)
    {
        if (!OpportunityScheduleConfig.IsValidTime(req.TimeEt)) return ValidationProblem("Time must be HH:mm (Eastern).");
        if (req.Days is null || req.Days.Count == 0) return ValidationProblem("Select at least one day.");
        if (req.Days.Any(d => d is DayOfWeek.Saturday or DayOfWeek.Sunday)) return ValidationProblem("Only market days (Mon-Fri) can be scheduled.");
        schedule.Update(req.Enabled, req.TimeEt, req.Days);
        return Ok(ScheduleDto());
    }

    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    public async Task<ActionResult<OpportunitySnapshotDto>> Get(CancellationToken ct) =>
        Ok(await service.GetLatestAsync(CurrentUserId(), ct));

    /// <summary>Drawer detail from the same snapshot as the grid row. 410 when that snapshot was replaced/expired.</summary>
    [HttpGet("{symbol}")]
    public async Task<ActionResult<OpportunityDetailDto>> GetDetail(string symbol, [FromQuery] int? snapshotId, CancellationToken ct)
    {
        var detail = await service.GetDetailAsync(CurrentUserId(), symbol, snapshotId, ct);
        return detail is null
            ? Problem(statusCode: StatusCodes.Status410Gone, title: "Snapshot unavailable",
                detail: "This opportunity snapshot is no longer available. Reload the latest snapshot.")
            : Ok(detail);
    }

    /// <summary>
    /// A refresh scans the whole universe and takes minutes, so a closed tab, reload or proxy timeout must not cancel it half-way;
    /// only application shutdown does. The single-flight gate still rejects a second concurrent refresh.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<OpportunitySnapshotDto>> Refresh()
    {
        try
        {
            return Ok(await service.RefreshAsync(CurrentUserId(), lifetime.ApplicationStopping));
        }
        catch (OpportunityRefreshInProgressException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Refresh in progress", detail: ex.Message);
        }
    }

    [HttpGet("config")]
    public ActionResult<OpportunityConfigDto> Config() => Ok(OpportunityConfigDto.Current);

    // ── Reference data (TSX 60 constituents, sector benchmarks) — read for all, edit Admin only, audited by the interceptor ──

    [HttpGet("reference")]
    public async Task<ActionResult<OpportunityReferenceDto>> Reference(CancellationToken ct)
    {
        var constituents = await db.IndexConstituents.AsNoTracking()
            .OrderBy(c => c.IndexCode).ThenBy(c => c.EffectiveTo != null).ThenBy(c => c.Symbol).ToListAsync(ct);
        var mappings = await db.SectorBenchmarkMappings.AsNoTracking().OrderBy(m => m.Market).ThenBy(m => m.Sector).ToListAsync(ct);
        return Ok(new OpportunityReferenceDto(constituents, mappings));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("reference/constituents")]
    public async Task<ActionResult<IndexConstituent>> AddConstituent(IndexConstituentRequest req, CancellationToken ct)
    {
        var symbol = req.Symbol.Trim().ToUpperInvariant();
        var index = string.IsNullOrWhiteSpace(req.IndexCode) ? OpportunityService.IndexCode : req.IndexCode.Trim().ToUpperInvariant();
        if (symbol.Length == 0 || string.IsNullOrWhiteSpace(req.Source))
            return ValidationProblem("Symbol and Source are required.");
        if (await db.IndexConstituents.AnyAsync(c => c.IndexCode == index && c.Symbol == symbol && c.EffectiveTo == null, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Already a current constituent");
        var row = new IndexConstituent
        {
            IndexCode = index, Symbol = symbol, CompanyName = req.CompanyName?.Trim() ?? "",
            EffectiveFrom = req.EffectiveFrom.Date, Source = req.Source.Trim(), SourceAsOfDate = req.SourceAsOfDate.Date,
            VerifiedAtUtc = DateTime.UtcNow, VerifiedBy = User.FindFirstValue(ClaimTypes.Name),
        };
        db.IndexConstituents.Add(row);
        await db.SaveChangesAsync(ct);
        return Ok(row);
    }

    /// <summary>Membership history is kept: removing a constituent sets <c>EffectiveTo</c>, it never deletes the row.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("reference/constituents/{id:int}/retire")]
    public async Task<ActionResult<IndexConstituent>> RetireConstituent(int id, [FromQuery] DateTime? effectiveTo, CancellationToken ct)
    {
        var row = await db.IndexConstituents.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (row is null) return NotFound();
        if (row.EffectiveTo is not null) return Ok(row);
        row.EffectiveTo = (effectiveTo ?? DateTime.UtcNow).Date;
        await db.SaveChangesAsync(ct);
        return Ok(row);
    }

    /// <summary>Undo of a retire (e.g. a mis-click): clears <c>EffectiveTo</c>. Refused if the symbol is already a current constituent.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("reference/constituents/{id:int}/restore")]
    public async Task<ActionResult<IndexConstituent>> RestoreConstituent(int id, CancellationToken ct)
    {
        var row = await db.IndexConstituents.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (row is null) return NotFound();
        if (row.EffectiveTo is null) return Ok(row);
        if (await db.IndexConstituents.AnyAsync(c => c.IndexCode == row.IndexCode && c.Symbol == row.Symbol && c.EffectiveTo == null, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Already a current constituent");
        row.EffectiveTo = null;
        await db.SaveChangesAsync(ct);
        return Ok(row);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("reference/benchmarks")]
    public async Task<ActionResult<SectorBenchmarkMapping>> UpsertBenchmark(SectorBenchmarkRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Sector) || string.IsNullOrWhiteSpace(req.BenchmarkSymbol) || string.IsNullOrWhiteSpace(req.Currency))
            return ValidationProblem("Sector, BenchmarkSymbol and Currency are required.");
        if (!Enum.TryParse<MarketExchange>(req.Market, ignoreCase: true, out var market))
            return ValidationProblem($"Market must be one of: {string.Join(", ", Enum.GetNames<MarketExchange>())}.");
        var row = req.Id is { } id
            ? await db.SectorBenchmarkMappings.SingleOrDefaultAsync(m => m.Id == id, ct)
            : await db.SectorBenchmarkMappings.SingleOrDefaultAsync(m => m.Sector == req.Sector.Trim() && m.Market == market.ToString(), ct);
        if (row is null)
        {
            if (req.Id is not null) return NotFound();
            row = new SectorBenchmarkMapping();
            db.SectorBenchmarkMappings.Add(row);
        }
        row.Sector = req.Sector.Trim();
        row.Market = market.ToString();
        row.BenchmarkSymbol = req.BenchmarkSymbol.Trim().ToUpperInvariant();
        row.BenchmarkName = req.BenchmarkName?.Trim() ?? "";
        row.Currency = req.Currency.Trim().ToUpperInvariant();
        row.ProviderSectorAliasesJson = System.Text.Json.JsonSerializer.Serialize(
            (req.ProviderSectorAliases ?? []).Select(a => a.Trim()).Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        row.IsActive = req.IsActive;
        row.Notes = req.Notes?.Trim();
        row.Source = req.Source?.Trim();
        row.SourceAsOfDate = req.SourceAsOfDate?.Date;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedBy = User.FindFirstValue(ClaimTypes.Name);
        await db.SaveChangesAsync(ct);
        return Ok(row);
    }
}

public sealed record OpportunityScheduleDto(
    bool Enabled, string TimeEt, IReadOnlyList<DayOfWeek> Days, string? LastRunDateEt, DateTime? LastRunAtUtc, string? LastRunStatus, string? LastRunMessage);

public sealed record OpportunityScheduleRequest(bool Enabled, string TimeEt, IReadOnlyList<DayOfWeek> Days);

public sealed record OpportunityReferenceDto(IReadOnlyList<IndexConstituent> Constituents, IReadOnlyList<SectorBenchmarkMapping> Benchmarks);

public sealed record IndexConstituentRequest(
    string Symbol, string? CompanyName, string? IndexCode, DateTime EffectiveFrom, string Source, DateTime SourceAsOfDate);

public sealed record SectorBenchmarkRequest(
    int? Id, string Sector, string Market, string BenchmarkSymbol, string? BenchmarkName, string Currency,
    IReadOnlyList<string>? ProviderSectorAliases, bool IsActive, string? Notes, string? Source, DateTime? SourceAsOfDate);
