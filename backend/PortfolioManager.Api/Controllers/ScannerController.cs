using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using System.Text.Json;
using System.Security.Claims;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ScannerController(
    IRsiScannerService scanner,
    IMemoryCache cache,
    AppDbContext db,
    ScannerRuntimeConfig runtimeConfig,
    EodSignalPersistenceService eodPersistence,
    IRsiSnapshotService snapshotService,
    IDashboardService dashboard,
    ILogger<ScannerController> logger) : ControllerBase
{
    private const string CacheKeyPrefix = "rsi_scan";

    /// <summary>Returns the latest persisted RSI scan snapshot from the database without hitting Yahoo Finance. 204 when no snapshot exists yet.</summary>
    [HttpGet("rsi/snapshot")]
    public async Task<ActionResult<ScannerResponse>> GetRsiSnapshot(CancellationToken ct)
    {
        var snapshot = await snapshotService.GetLatestAsync(ct);
        if (snapshot is null) return NoContent();
        return Ok(snapshot);
    }

    [HttpGet("rsi")]
    public async Task<ActionResult<ScannerResponse>> GetRsiScan(
        [FromQuery] bool force = false,
        [FromQuery] decimal oversold = 30m,
        [FromQuery] decimal overbought = 75m,
        [FromQuery] string logicMode = "Legacy",
        CancellationToken ct = default)
    {
        // Pull all user-defined symbols so the scan covers the full portfolio + watchlist.
        // Exclude closed positions so the Tracking badge shows only active holdings.
        var portfolioSymbols = await db.PortfolioItems
            .Where(p => !p.IsManual && p.TransactionType != "CLOSE")
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

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await scanner.ScanAsync(extraSymbols, oversold, overbought, logicMode, userId, ct);

        // Persist snapshot so the frontend loads instantly without hitting Yahoo Finance ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â demo data has no TTL value
        if (!result.IsDemo)
        {
            await snapshotService.SaveAsync(result, ct);
            if (!string.IsNullOrEmpty(userId)) await dashboard.RebuildAsync(userId, ct);
        }

        return Ok(result);
    }

    /// <summary>Clears in-memory market-indices cache so next request fetches fresh data.</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("rsi/cache")]
    public IActionResult ClearCache()
    {
        cache.Remove(IndicesCacheKey);
        logger.LogInformation("Market indices cache cleared.");
        return NoContent();
    }

    private const string IndicesCacheKey = "market_indices";
    private static readonly TimeSpan IndicesCacheTtl = TimeSpan.FromMinutes(5);

    private static readonly (string symbol, string name)[] IndexSymbols =
    [
        ("^DJI",  "Dow Jones"),
        ("^NDX",  "Nasdaq 100"),
        ("^GSPC", "S&P 500"),
    ];

    /// <summary>Returns real-time prices for Dow Jones, Nasdaq 100 and S&amp;P 500.</summary>
    [HttpGet("market-indices")]
    public async Task<ActionResult<MarketIndicesResponse>> GetMarketIndices(
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        if (!force && cache.TryGetValue(IndicesCacheKey, out MarketIndicesResponse? cached) && cached is not null)
            return Ok(cached);

        var marketData = HttpContext.RequestServices.GetRequiredService<IMarketDataProvider>();
        var symbols = IndexSymbols.Select(x => x.symbol).ToList();
        var quotes = await marketData.GetBatchQuotesAsync(symbols, ct);

        var indices = IndexSymbols
            .Select(idx =>
            {
                quotes.TryGetValue(idx.symbol, out var q);
                return new MarketIndexDto(
                    idx.symbol,
                    idx.name,
                    q?.CurrentPrice ?? 0,
                    q?.Change ?? 0,
                    q?.ChangePercent ?? 0);
            })
            .ToList();

        var response = new MarketIndicesResponse(indices, DateTime.UtcNow);
        cache.Set(IndicesCacheKey, response, IndicesCacheTtl);
        return Ok(response);
    }

    /// <summary>
    /// Ad-hoc analysis: accepts up to 20 user-supplied symbols and returns RSI scan results for each.
    /// Not cached ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â always fetches live data.
    /// </summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpPost("analyze")]
    public async Task<ActionResult<List<RsiScanResult>>> AnalyzeSymbols(
        [FromBody] AnalyzeRequest request,
        CancellationToken ct)
    {
        if (request.Symbols is null || request.Symbols.Count == 0)
            return BadRequest("Provide at least one symbol.");

        if (request.Symbols.Count > 100)
            return BadRequest("Maximum 100 symbols per request.");

        logger.LogInformation("Ad-hoc analysis requested for {Count} symbols. Oversold<{OS} Overbought>{OB} Mode={Mode}",
            request.Symbols.Count, request.OversoldThreshold, request.OverboughtThreshold, request.LogicMode);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var results = await scanner.AnalyzeSymbolsAsync(
            request.Symbols,
            request.OversoldThreshold,
            request.OverboughtThreshold,
            request.LogicMode,
            userId,
            ct);
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

    // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ Ad-hoc Session Persistence ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Save the current ad-hoc analysis session (symbols + results) to the database.</summary>
    [Authorize(Roles = "Admin")]
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
                logger.LogWarning(ex, "Failed to deserialise adhoc session results ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Å“ returning symbols only.");
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

    // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ EOD Window Settings ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬

    /// <summary>Returns the current EOD confirmation window settings.</summary>
    [HttpGet("eod-settings")]
    public IActionResult GetEodSettings()
    {
        return Ok(new EodWindowSettingsDto
        {
            EodWindowStart            = runtimeConfig.EodWindowStart,
            EodWindowEnd              = runtimeConfig.EodWindowEnd,
            EodWindowEnabled          = runtimeConfig.EodWindowEnabled,
            EodOversoldRsiThreshold   = runtimeConfig.EodOversoldRsiThreshold,
            EodOverboughtRsiThreshold = runtimeConfig.EodOverboughtRsiThreshold,
        });
    }

    /// <summary>
    /// Updates the EOD confirmation window at runtime.
    /// Changes take effect immediately for the background service (no restart required).
    /// Settings are persisted to disk so they survive server restarts.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("eod-settings")]
    public IActionResult UpdateEodSettings([FromBody] EodWindowSettingsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EodWindowStart) || string.IsNullOrWhiteSpace(dto.EodWindowEnd))
            return BadRequest("EodWindowStart and EodWindowEnd are required (format: HH:mm).");

        if (!TimeSpan.TryParse(dto.EodWindowStart, out _) || !TimeSpan.TryParse(dto.EodWindowEnd, out _))
            return BadRequest("Invalid time format. Use HH:mm (e.g. '15:30', '16:30').");

        if (dto.EodOversoldRsiThreshold is <= 0 or > 49)
            return BadRequest("EodOversoldRsiThreshold must be between 1 and 49.");
        if (dto.EodOverboughtRsiThreshold is < 51 or > 99)
            return BadRequest("EodOverboughtRsiThreshold must be between 51 and 99.");

        runtimeConfig.EodWindowStart            = dto.EodWindowStart;
        runtimeConfig.EodWindowEnd              = dto.EodWindowEnd;
        runtimeConfig.EodWindowEnabled          = dto.EodWindowEnabled;
        runtimeConfig.EodOversoldRsiThreshold   = dto.EodOversoldRsiThreshold;
        runtimeConfig.EodOverboughtRsiThreshold = dto.EodOverboughtRsiThreshold;

        // Persist to disk so settings survive a server restart
        runtimeConfig.SaveToFile();

        logger.LogInformation(
            "EOD window updated: {Start}\u2013{End} ET, Enabled={Enabled}, OS<{OS} OB>{OB}",
            dto.EodWindowStart, dto.EodWindowEnd, dto.EodWindowEnabled,
            dto.EodOversoldRsiThreshold, dto.EodOverboughtRsiThreshold);

        return Ok(new EodWindowSettingsDto
        {
            EodWindowStart            = runtimeConfig.EodWindowStart,
            EodWindowEnd              = runtimeConfig.EodWindowEnd,
            EodWindowEnabled          = runtimeConfig.EodWindowEnabled,
            EodOversoldRsiThreshold   = runtimeConfig.EodOversoldRsiThreshold,
            EodOverboughtRsiThreshold = runtimeConfig.EodOverboughtRsiThreshold,
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
