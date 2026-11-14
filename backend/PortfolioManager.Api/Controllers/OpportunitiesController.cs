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
public sealed class OpportunitiesController(IOpportunityService service, AppDbContext db) : ControllerBase
{
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

    [HttpPost("refresh")]
    public async Task<ActionResult<OpportunitySnapshotDto>> Refresh(CancellationToken ct)
    {
        try
        {
            return Ok(await service.RefreshAsync(CurrentUserId(), ct));
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

public sealed record OpportunityReferenceDto(IReadOnlyList<IndexConstituent> Constituents, IReadOnlyList<SectorBenchmarkMapping> Benchmarks);

public sealed record IndexConstituentRequest(
    string Symbol, string? CompanyName, string? IndexCode, DateTime EffectiveFrom, string Source, DateTime SourceAsOfDate);

public sealed record SectorBenchmarkRequest(
    int? Id, string Sector, string Market, string BenchmarkSymbol, string? BenchmarkName, string Currency,
    IReadOnlyList<string>? ProviderSectorAliases, bool IsActive, string? Notes, string? Source, DateTime? SourceAsOfDate);
