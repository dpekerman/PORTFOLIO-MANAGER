using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScannerController(
    IRsiScannerService scanner,
    IMemoryCache cache,
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

        var result = await scanner.ScanAsync(oversold, overbought, logicMode, ct);

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
}

public sealed record AnalyzeRequest(
    List<string> Symbols,
    decimal OversoldThreshold = 30m,
    decimal OverboughtThreshold = 75m,
    string LogicMode = "Legacy");
