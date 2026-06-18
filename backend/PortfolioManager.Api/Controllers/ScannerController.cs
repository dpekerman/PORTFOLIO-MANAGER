using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using System.Text.Json;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScannerController(
    IRsiScannerService scanner,
    IMemoryCache cache,
    AppDbContext db,
    ScannerRuntimeConfig runtimeConfig,
    EodSignalPersistenceService eodPersistence,
    ILogger<ScannerController> logger) : ControllerBase
{
    private const string CacheKeyPrefix = "rsi_scan";
    /// <summary>Cache live scan results for 4 minutes to avoid hammering Yahoo Finance.</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(4);

    [HttpGet("rsi")]
    public async Task<ActionResult<ScannerResponse>> GetRsiScan(
        [FromQuery] bool force = false,
        [FromQuery] decimal oversold = 30m,
        [FromQuery] decimal overbought = 75m,
        [FromQuery] string logicMode = "Legacy",
        CancellationToken ct = default)
    {
        // Cache key includes thresholds + logicMode so switching modes forces a fresh scan
        var cacheKey = $"{CacheKeyPrefix}_{oversold}_{overbought}_{logicMode}";
        if (!force && cache.TryGetValue(cacheKey, out ScannerResponse? cached) && cached is not null)
        {
            logger.LogDebug("Returning cached RSI scan (scanned at {Time})", cached.ScannedAt);
            return Ok(cached);
        }

        // Pull all user-defined symbols so the scan covers the full portfolio + watchlist.
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

        var result = await scanner.ScanAsync(extraSymbols, oversold, overbought, logicMode, ct);

        // Only cache live results — demo data has no TTL value
        if (!result.IsDemo)
            cache.Set(cacheKey, result, CacheTtl);

        return Ok(result);
    }

    /// <summary>Force-invalidate all RSI scan cache entries (e.g. after config save).</summary>
    [HttpDelete("rsi/cache")]
    public IActionResult ClearCache()
    {
        // IMemoryCache does not expose enumerate; use a compact token pattern instead.
        // We store a "version" key that is appended to the cache key so all old keys become stale.
        // For simplicity, force=true on the next request is the primary mechanism.
        // Here we also remove the most common key patterns used by the UI.
        foreach (var mode in new[] { "Legacy", "Enhanced" })
        foreach (var os in new[] { 25m, 30m, 35m })
        foreach (var ob in new[] { 70m, 75m, 80m })
            cache.Remove($"{CacheKeyPrefix}_{os}_{ob}_{mode}");

        logger.LogInformation("RSI scan cache cleared (all common key patterns).");
        return NoContent();
    }

    /// <summary>
    /// Ad-hoc analysis: accepts up to 20 user-supplied symbols and returns RSI scan results for each.
    /// Not cached — always fetches live data.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<List<RsiScanResult>>> AnalyzeSymbols(
        [FromBody] AnalyzeRequest request,
        CancellationToken ct)
    {
        if (request.Symbols is null || request.Symbols.Count == 0)
            return BadRequest("Provide at least one symbol.");

        if (request.Symbols.Count > 50)
            return BadRequest("Maximum 50 symbols per request.");

        logger.LogInformation("Ad-hoc analysis requested for {Count} symbols. Oversold<{OS} Overbought>{OB} Mode={Mode}",
            request.Symbols.Count, request.OversoldThreshold, request.OverboughtThreshold, request.LogicMode);
        var results = await scanner.AnalyzeSymbolsAsync(request.Symbols, request.OversoldThreshold, request.OverboughtThreshold, request.LogicMode, ct);
        return Ok(results);
    }

    /// <summary>
    /// Diagnostic: tests connectivity to Yahoo Finance.
    /// Returns 200 with status info. Safe to call from Swagger.
    /// </summary>
    [HttpGet("test")]
    public async Task<IActionResult> TestApiKey(
        CancellationToken ct)
    {
        using var client = new System.Net.Http.HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.Timeout = TimeSpan.FromSeconds(10);
        try
        {
            var resp = await client.GetAsync(
                "https://query1.finance.yahoo.com/v8/finance/chart/RY.TO?interval=1d&range=1d", ct);
            return Ok(new
            {
                status     = resp.IsSuccessStatusCode ? "ok" : "error",
                httpStatus = (int)resp.StatusCode,
                provider   = "Yahoo Finance",
                message    = resp.IsSuccessStatusCode
                    ? "Yahoo Finance responded 200. TSX data is available."
                    : $"Yahoo Finance returned {(int)resp.StatusCode}. Check network connectivity."
            });
        }
        catch (Exception ex)
        {
            return Ok(new { status = "exception", provider = "Yahoo Finance", message = ex.Message });
        }
    }

    // ── Ad-hoc Session Persistence ────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Save the current ad-hoc analysis session (symbols + results) to the database.</summary>
    [HttpPost("adhoc-session")]
    public async Task<IActionResult> SaveAdhocSession(
        [FromBody] SaveAdhocSessionRequest request,
        CancellationToken ct)
    {
        const string key = "default";

        var symbolsJson  = JsonSerializer.Serialize(request.Symbols, JsonOpts);
        var resultsJson  = request.Results is null ? null
                         : JsonSerializer.Serialize(request.Results, JsonOpts);

        var existing = await db.AdhocAnalysisSessions
            .FirstOrDefaultAsync(s => s.SessionKey == key, ct);

        if (existing is null)
        {
            db.AdhocAnalysisSessions.Add(new AdhocAnalysisSession
            {
                SessionKey           = key,
                Symbols              = symbolsJson,
                ResultsJson          = resultsJson,
                OversoldThreshold    = request.OversoldThreshold,
                OverboughtThreshold  = request.OverboughtThreshold,
                LogicMode            = request.LogicMode,
                CreatedAt            = DateTime.UtcNow,
                UpdatedAt            = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Symbols             = symbolsJson;
            existing.ResultsJson         = resultsJson;
            existing.OversoldThreshold   = request.OversoldThreshold;
            existing.OverboughtThreshold = request.OverboughtThreshold;
            existing.LogicMode           = request.LogicMode;
            existing.UpdatedAt           = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Load the most-recent ad-hoc analysis session from the database.</summary>
    [HttpGet("adhoc-session")]
    public async Task<ActionResult<LoadAdhocSessionResponse>> LoadAdhocSession(CancellationToken ct)
    {
        const string key = "default";

        var session = await db.AdhocAnalysisSessions
            .Where(s => s.SessionKey == key)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (session is null)
            return Ok(new LoadAdhocSessionResponse());

        var symbols = JsonSerializer.Deserialize<List<string>>(session.Symbols, JsonOpts) ?? [];
        List<RsiScanResult>? results = null;
        if (session.ResultsJson is not null)
        {
            try { results = JsonSerializer.Deserialize<List<RsiScanResult>>(session.ResultsJson, JsonOpts); }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialise adhoc session results – returning symbols only.");
            }
        }

        return Ok(new LoadAdhocSessionResponse
        {
            Symbols              = symbols,
            Results              = results,
            OversoldThreshold    = session.OversoldThreshold,
            OverboughtThreshold  = session.OverboughtThreshold,
            LogicMode            = session.LogicMode,
            UpdatedAt            = session.UpdatedAt,
        });
    }

    // ── EOD Window Settings ───────────────────────────────────────────────────

    /// <summary>Returns the current EOD confirmation window settings.</summary>
    [HttpGet("eod-settings")]
    public IActionResult GetEodSettings()
    {
        return Ok(new EodWindowSettingsDto
        {
            EodWindowStart   = runtimeConfig.EodWindowStart,
            EodWindowEnd     = runtimeConfig.EodWindowEnd,
            EodWindowEnabled = runtimeConfig.EodWindowEnabled,
        });
    }

    /// <summary>
    /// Updates the EOD confirmation window at runtime.
    /// Changes take effect immediately for the background service (no restart required).
    /// </summary>
    [HttpPut("eod-settings")]
    public IActionResult UpdateEodSettings([FromBody] EodWindowSettingsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EodWindowStart) || string.IsNullOrWhiteSpace(dto.EodWindowEnd))
            return BadRequest("EodWindowStart and EodWindowEnd are required (format: HH:mm).");

        if (!TimeSpan.TryParse(dto.EodWindowStart, out _) || !TimeSpan.TryParse(dto.EodWindowEnd, out _))
            return BadRequest("Invalid time format. Use HH:mm (e.g. '15:30', '16:00').");

        runtimeConfig.EodWindowStart   = dto.EodWindowStart;
        runtimeConfig.EodWindowEnd     = dto.EodWindowEnd;
        runtimeConfig.EodWindowEnabled = dto.EodWindowEnabled;

        logger.LogInformation(
            "EOD window updated: {Start}–{End} ET, Enabled={Enabled}",
            dto.EodWindowStart, dto.EodWindowEnd, dto.EodWindowEnabled);

        return Ok(new EodWindowSettingsDto
        {
            EodWindowStart   = runtimeConfig.EodWindowStart,
            EodWindowEnd     = runtimeConfig.EodWindowEnd,
            EodWindowEnabled = runtimeConfig.EodWindowEnabled,
        });
    }

    /// <summary>Returns whether the EOD window is currently active (for UI indicator).</summary>
    [HttpGet("eod-window-active")]
    public IActionResult GetEodWindowStatus()
    {
        return Ok(new
        {
            isActive         = runtimeConfig.IsEodWindowActive(),
            eodWindowStart   = runtimeConfig.EodWindowStart,
            eodWindowEnd     = runtimeConfig.EodWindowEnd,
            eodWindowEnabled = runtimeConfig.EodWindowEnabled,
            serverTimeUtc    = DateTime.UtcNow.ToString("HH:mm:ss"),
        });
    }

    /// <summary>
    /// Returns the EOD CONFIRM signals that were recorded during the most recent EOD window.
    /// The <c>isMorningWindow</c> flag indicates whether the server time is currently before noon ET.
    /// The frontend uses this to show a "Morning Check" panel during the next trading morning.
    /// </summary>
    [HttpGet("yesterday-eod")]
    public async Task<IActionResult> GetYesterdayEodSignals(CancellationToken ct)
    {
        var response = await eodPersistence.GetYesterdayEodAsync(ct);
        return Ok(response);
    }
}

public sealed record AnalyzeRequest(
    List<string> Symbols,
    decimal OversoldThreshold = 30m,
    decimal OverboughtThreshold = 75m,
    string LogicMode = "Legacy");
