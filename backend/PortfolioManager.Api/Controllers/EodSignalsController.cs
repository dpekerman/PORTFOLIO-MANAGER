using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/eod-signals")]
public class EodSignalsController(
    AppDbContext db,
    IRsiScannerService scanner,
    EodSignalPersistenceService eodPersistence,
    ILogger<EodSignalsController> logger) : ControllerBase
{
    // -- Query ---------------------------------------------------------------

    [HttpGet]
    public async Task<ActionResult<DailySignalPagedResponse>> GetSignals(
        [FromQuery] string? ticker = null,
        [FromQuery] string? scanType = null,
        [FromQuery] string? signalType = null,
        [FromQuery] string? signalState = null,
        [FromQuery] string? ruleVersion = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;

        var query = db.DailySignals.AsQueryable();

        if (!string.IsNullOrWhiteSpace(ticker))
            query = query.Where(s => s.Symbol.ToUpper().Contains(ticker.ToUpper().Trim()));
        if (!string.IsNullOrWhiteSpace(scanType))
            query = query.Where(s => s.ScanType == scanType);
        if (!string.IsNullOrWhiteSpace(signalType))
            query = query.Where(s => s.SignalType == signalType);
        if (!string.IsNullOrWhiteSpace(signalState))
            query = query.Where(s => s.SignalState == signalState);
        if (!string.IsNullOrWhiteSpace(ruleVersion))
            query = query.Where(s => s.RuleVersion == ruleVersion);
        if (!string.IsNullOrWhiteSpace(dateFrom))
            query = query.Where(s => string.Compare(s.SignalDate, dateFrom, StringComparison.Ordinal) >= 0);
        if (!string.IsNullOrWhiteSpace(dateTo))
            query = query.Where(s => string.Compare(s.SignalDate, dateTo, StringComparison.Ordinal) <= 0);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.SignalDate)
            .ThenByDescending(s => s.RecordedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new DailySignalPagedResponse { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DailySignal>> GetSignal(int id, CancellationToken ct)
    {
        var signal = await db.DailySignals.FindAsync([id], ct);
        return signal is null ? NotFound() : Ok(signal);
    }

    // -- Lifecycle Updates ---------------------------------------------------

    private static readonly HashSet<string> ValidStates =
        new(["Active", "FollowThrough", "Invalidated", "Expired", "Reversed"], StringComparer.OrdinalIgnoreCase);

    [HttpPatch("{id:int}/state")]
    public async Task<IActionResult> UpdateState(int id, [FromBody] UpdateSignalStateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SignalState) || !ValidStates.Contains(request.SignalState))
            return BadRequest($"Invalid signal state. Must be one of: {string.Join(", ", ValidStates)}.");

        var signal = await db.DailySignals.FindAsync([id], ct);
        if (signal is null) return NotFound();
        signal.SignalState = request.SignalState;
        signal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("DailySignal {Id} ({Symbol}) state -> {State}", id, signal.Symbol, request.SignalState);
        return NoContent();
    }

    [HttpPatch("{id:int}/notes")]
    public async Task<IActionResult> UpdateNotes(int id, [FromBody] UpdateSignalNotesRequest request, CancellationToken ct)
    {
        var signal = await db.DailySignals.FindAsync([id], ct);
        if (signal is null) return NotFound();
        signal.Notes = request.Notes?.Trim();
        signal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // -- Meta ----------------------------------------------------------------

    [HttpGet("meta")]
    public async Task<IActionResult> GetMeta(CancellationToken ct)
    {
        var tickers      = await db.DailySignals.Select(s => s.Symbol).Distinct().OrderBy(s => s).ToListAsync(ct);
        var scanTypes    = await db.DailySignals.Select(s => s.ScanType).Distinct().OrderBy(s => s).ToListAsync(ct);
        var signalTypes  = await db.DailySignals.Select(s => s.SignalType).Distinct().OrderBy(s => s).ToListAsync(ct);
        var signalStates = await db.DailySignals.Select(s => s.SignalState).Distinct().OrderBy(s => s).ToListAsync(ct);
        var ruleVersions = await db.DailySignals.Select(s => s.RuleVersion).Distinct().OrderBy(s => s).ToListAsync(ct);
        var minDate      = await db.DailySignals.MinAsync(s => (string?)s.SignalDate, ct);
        var maxDate      = await db.DailySignals.MaxAsync(s => (string?)s.SignalDate, ct);
        var totalCount   = await db.DailySignals.CountAsync(ct);
        return Ok(new { tickers, scanTypes, signalTypes, signalStates, ruleVersions, minDate, maxDate, totalCount });
    }

    // -- Delete --------------------------------------------------------------

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteSignal(int id, CancellationToken ct)
    {
        var signal = await db.DailySignals.FindAsync([id], ct);
        if (signal is null) return NotFound();
        db.DailySignals.Remove(signal);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("DailySignal {Id} ({Symbol}) deleted.", id, signal.Symbol);
        return NoContent();
    }

    [HttpDelete]
    public async Task<ActionResult<object>> DeleteAll(
        [FromQuery] bool confirm = false,
        [FromQuery] string? ticker = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        CancellationToken ct = default)
    {
        if (!confirm) return BadRequest("Pass confirm=true to confirm bulk deletion.");

        var query = db.DailySignals.AsQueryable();
        if (!string.IsNullOrWhiteSpace(ticker))
            query = query.Where(s => s.Symbol == ticker.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(dateFrom))
            query = query.Where(s => string.Compare(s.SignalDate, dateFrom, StringComparison.Ordinal) >= 0);
        if (!string.IsNullOrWhiteSpace(dateTo))
            query = query.Where(s => string.Compare(s.SignalDate, dateTo, StringComparison.Ordinal) <= 0);

        var count = await query.CountAsync(ct);
        db.DailySignals.RemoveRange(query);
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Bulk deleted {Count} DailySignal record(s).", count);
        return Ok(new { deleted = count });
    }

    // -- Manual Persist-Now (for testing / ad-hoc persistence) ---------------
    // Runs a fresh scan and persists EodConfirm + Confirmed signals immediately,
    // regardless of whether the automatic EOD window is active.
    // Useful for verifying the persistence pipeline and inspecting real data.

    [HttpPost("persist-now")]
    public async Task<ActionResult<object>> PersistNow(CancellationToken ct)
    {
        // Mirror EXACTLY what ScannerController.GetRsiScan and RsiAlertBackgroundService do:
        // include ALL user-defined portfolio + watchlist symbols so non-TSX stocks are covered.
        var portfolioSymbols = await db.PortfolioItems
            .Where(p => !p.IsManual)
            .Select(p => p.Symbol)
            .ToListAsync(ct);
        var watchlistSymbols = await db.WatchlistItems
            .Select(w => w.Symbol)
            .ToListAsync(ct);
        var extraSymbols = portfolioSymbols
            .Concat(watchlistSymbols)
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        // Use Enhanced mode + default thresholds (matches background service + typical UI state)
        var result = await scanner.ScanAsync(extraSymbols, oversoldThreshold: 30m, overboughtThreshold: 75m, logicMode: "Enhanced", ct: ct);

        var allQualified = (result.OversoldChain ?? [])
            .Concat(result.OverboughtChain ?? [])
            .Where(r => r.Status == SignalStatus.EodConfirm || r.Status == SignalStatus.Confirmed)
            .ToList();

        if (allQualified.Count > 0)
            await eodPersistence.SaveAsync(allQualified, ct);

        var eodCount = allQualified.Count(r => r.Status == SignalStatus.EodConfirm);
        var confirmCount = allQualified.Count(r => r.Status == SignalStatus.Confirmed);

        logger.LogInformation("PersistNow: scanned {Total} symbols, saved {Saved} signals ({Eod} EodConfirm, {Confirm} Confirmed).",
            (result.OversoldChain?.Count ?? 0) + (result.OverboughtChain?.Count ?? 0),
            allQualified.Count, eodCount, confirmCount);

        return Ok(new
        {
            persisted    = allQualified.Count,
            eodConfirm   = eodCount,
            confirmed    = confirmCount,
            oversoldScanned  = result.OversoldChain?.Count  ?? 0,
            overboughtScanned = result.OverboughtChain?.Count ?? 0,
        });
    }

    // -- Test Data Seeding ---------------------------------------------------
    // Seeds 15 realistic test records covering both Oversold and Overbought chains,
    // all signal types (EodConfirm, Confirmed, EarlyWarning), and all lifecycle states.
    // No environment guard -- works in Development and can be removed for Production.

    [HttpPost("seed")]
    public async Task<ActionResult<object>> SeedTestData(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var seed = new[]
        {
            // OVERSOLD CHAIN
            new DailySignal { Symbol="TD.TO",  CompanyName="Toronto-Dominion Bank",        ScanType="Oversold",   SignalType="EodConfirm",   Rsi=22.4m,  Price=76.50m,  SignalDate="2026-06-20", RecordedAt=now.AddDays(-4),  RuleVersion="Enhanced", SignalState="FollowThrough", Sector="Financials",       ReversalProbability="High",   VolumeSignal="Validated",       TriggerDetails="RSI<25 · Price below 9-EMA · Volume 2.1x avg · ATR lower quartile" },
            new DailySignal { Symbol="RY.TO",  CompanyName="Royal Bank of Canada",         ScanType="Oversold",   SignalType="EodConfirm",   Rsi=27.1m,  Price=135.20m, SignalDate="2026-06-20", RecordedAt=now.AddDays(-4),  RuleVersion="Enhanced", SignalState="Active",       Sector="Financials",       ReversalProbability="Medium", VolumeSignal="Neutral",         TriggerDetails="RSI<30 · Price vs 9-EMA · MACD histogram rising (+0.009)" },
            new DailySignal { Symbol="ENB.TO", CompanyName="Enbridge Inc",                 ScanType="Oversold",   SignalType="EodConfirm",   Rsi=24.7m,  Price=58.40m,  SignalDate="2026-06-18", RecordedAt=now.AddDays(-6),  RuleVersion="Enhanced", SignalState="FollowThrough", Sector="Energy",           ReversalProbability="High",   VolumeSignal="Validated",       TriggerDetails="RSI<25 · Price below 9-EMA · Volume 1.9x avg · ATR lower quartile" },
            new DailySignal { Symbol="CM.TO",  CompanyName="CIBC",                         ScanType="Oversold",   SignalType="EodConfirm",   Rsi=21.9m,  Price=62.30m,  SignalDate="2026-06-17", RecordedAt=now.AddDays(-7),  RuleVersion="Enhanced", SignalState="FollowThrough", Sector="Financials",       ReversalProbability="High",   VolumeSignal="Validated",       TriggerDetails="RSI<22 · Price below 9-EMA · Volume 2.3x · Strong ATR signal" },
            new DailySignal { Symbol="TRP.TO", CompanyName="TC Energy Corp",               ScanType="Oversold",   SignalType="EodConfirm",   Rsi=19.8m,  Price=51.30m,  SignalDate="2026-06-12", RecordedAt=now.AddDays(-12), RuleVersion="Enhanced", SignalState="FollowThrough", Sector="Energy",           ReversalProbability="High",   VolumeSignal="Validated",       TriggerDetails="RSI<20 · Extreme oversold · Price vs 9-EMA confirmed · Volume surge" },
            new DailySignal { Symbol="BNS.TO", CompanyName="Bank of Nova Scotia",          ScanType="Oversold",   SignalType="EodConfirm",   Rsi=25.6m,  Price=68.90m,  SignalDate="2026-06-11", RecordedAt=now.AddDays(-13), RuleVersion="Enhanced", SignalState="Expired",      Sector="Financials",       ReversalProbability="Medium", VolumeSignal="Neutral",         TriggerDetails="RSI<26 · Price near 9-EMA · Volume 1.6x avg · ATR lower half" },
            new DailySignal { Symbol="MFC.TO", CompanyName="Manulife Financial",           ScanType="Oversold",   SignalType="Confirmed",    Rsi=28.5m,  Price=27.60m,  SignalDate="2026-06-17", RecordedAt=now.AddDays(-7),  RuleVersion="Legacy",   SignalState="Active",       Sector="Insurance",        ReversalProbability="Medium", VolumeSignal="Neutral",         TriggerDetails="RSI<30 · MACD bullish crossover · Candle closed above prior high" },
            new DailySignal { Symbol="WN.TO",  CompanyName="George Weston Limited",        ScanType="Oversold",   SignalType="Confirmed",    Rsi=30.0m,  Price=195.80m, SignalDate="2026-06-13", RecordedAt=now.AddDays(-11), RuleVersion="Legacy",   SignalState="Active",       Sector="Consumer Staples", ReversalProbability="Medium", VolumeSignal="Neutral",         TriggerDetails="RSI boundary touch · MACD histogram positive · Volume neutral" },
            new DailySignal { Symbol="ATD.TO", CompanyName="Alimentation Couche-Tard",     ScanType="Oversold",   SignalType="EodConfirm",   Rsi=23.2m,  Price=62.10m,  SignalDate="2026-06-13", RecordedAt=now.AddDays(-11), RuleVersion="Enhanced", SignalState="FollowThrough", Sector="Consumer Staples", ReversalProbability="High",   VolumeSignal="Validated",       TriggerDetails="RSI<24 · Price below 9-EMA · Volume 1.7x · ATR bottom half" },
            new DailySignal { Symbol="BCE.TO", CompanyName="BCE Inc",                      ScanType="Oversold",   SignalType="EarlyWarning", Rsi=29.8m,  Price=32.20m,  SignalDate="2026-06-19", RecordedAt=now.AddDays(-5),  RuleVersion="Legacy",   SignalState="Active",       Sector="Communication",    ReversalProbability="Medium", VolumeSignal="Low-Volume Trap", TriggerDetails="RSI near oversold threshold · No EOD confirmation yet · Low volume" },
            // OVERBOUGHT CHAIN
            new DailySignal { Symbol="CNQ.TO", CompanyName="Canadian Natural Resources",   ScanType="Overbought", SignalType="EodConfirm",   Rsi=78.3m,  Price=48.90m,  SignalDate="2026-06-20", RecordedAt=now.AddDays(-4),  RuleVersion="Enhanced", SignalState="Invalidated",  Sector="Energy",           ReversalProbability="High",   VolumeSignal="Validated",       TriggerDetails="RSI>75 · Price above 9-EMA · Volume 1.8x · ATR upper quartile" },
            new DailySignal { Symbol="SU.TO",  CompanyName="Suncor Energy Inc",            ScanType="Overbought", SignalType="Confirmed",    Rsi=81.5m,  Price=55.10m,  SignalDate="2026-06-19", RecordedAt=now.AddDays(-5),  RuleVersion="Legacy",   SignalState="Reversed",     Sector="Energy",           ReversalProbability="High",   VolumeSignal="Validated",       TriggerDetails="RSI>80 · MACD bearish crossover · Candle closed in lower half of range" },
            new DailySignal { Symbol="BAM.TO", CompanyName="Brookfield Asset Mgmt",        ScanType="Overbought", SignalType="Confirmed",    Rsi=76.2m,  Price=72.80m,  SignalDate="2026-06-18", RecordedAt=now.AddDays(-6),  RuleVersion="Enhanced", SignalState="Expired",      Sector="Financials",       ReversalProbability="Low",    VolumeSignal="Neutral",         TriggerDetails="RSI>75 · Stochastics overbought · Bollinger upper band · Low vol" },
            new DailySignal { Symbol="CNR.TO", CompanyName="Canadian National Railway",    ScanType="Overbought", SignalType="EodConfirm",   Rsi=79.1m,  Price=158.40m, SignalDate="2026-06-16", RecordedAt=now.AddDays(-8),  RuleVersion="Enhanced", SignalState="Reversed",     Sector="Industrials",      ReversalProbability="High",   VolumeSignal="Validated",       TriggerDetails="RSI>78 · Price above 9-EMA · Volume 2.0x avg · ATR upper quartile" },
            new DailySignal { Symbol="CP.TO",  CompanyName="Canadian Pacific Kansas City", ScanType="Overbought", SignalType="EarlyWarning", Rsi=73.5m,  Price=98.20m,  SignalDate="2026-06-16", RecordedAt=now.AddDays(-8),  RuleVersion="Legacy",   SignalState="Invalidated",  Sector="Industrials",      ReversalProbability="Low",    VolumeSignal="Low-Volume Trap", TriggerDetails="RSI approaching overbought · No full EOD confirmation · Volume weak" },
        };

        var existingKeys = await db.DailySignals
            .Select(s => s.Symbol + "|" + s.SignalDate)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);

        var toInsert = seed.Where(s => !existingSet.Contains($"{s.Symbol}|{s.SignalDate}")).ToList();

        if (toInsert.Count > 0)
        {
            db.DailySignals.AddRange(toInsert);
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Seeded {Count} test DailySignal record(s) ({Skipped} duplicates skipped).",
            toInsert.Count, seed.Length - toInsert.Count);
        return Ok(new { seeded = toInsert.Count, skipped = seed.Length - toInsert.Count });
    }
}
