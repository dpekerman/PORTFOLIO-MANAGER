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
    private const string CacheKey = "rsi_scan";
    /// <summary>Cache live scan results for 4 minutes to avoid hammering Yahoo Finance.</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(4);

    [HttpGet("rsi")]
    public async Task<ActionResult<ScannerResponse>> GetRsiScan(
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        if (!force && cache.TryGetValue(CacheKey, out ScannerResponse? cached) && cached is not null)
        {
            logger.LogDebug("Returning cached RSI scan (scanned at {Time})", cached.ScannedAt);
            return Ok(cached);
        }

        var result = await scanner.ScanAsync(ct);

        // Only cache live results — demo data has no TTL value
        if (!result.IsDemo)
            cache.Set(CacheKey, result, CacheTtl);

        return Ok(result);
    }

    /// <summary>Force-invalidate the cache (e.g. after market open).</summary>
    [HttpDelete("rsi/cache")]
    public IActionResult ClearCache()
    {
        cache.Remove(CacheKey);
        return NoContent();
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
