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
    /// <summary>Cache live scan results for 4 minutes to avoid hammering
    /// the Finnhub free tier (60 req/min). Demo responses are not cached.</summary>
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
}
