using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using System.Security.Claims;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class StocksController(
    IMarketDataProvider marketData,
    ITechnicalSnapshotService technicalSnapshots,
    IPortfolioService portfolioService,
    IPortfolioSnapshotService portfolioSnapshot,
    IDashboardService dashboard) : ControllerBase
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Returns the latest persisted portfolio snapshot from DB — no Yahoo Finance call. 204 when no snapshot exists yet.</summary>
    [HttpGet("quotes/snapshot")]
    public async Task<ActionResult<IReadOnlyList<PortfolioSummaryDto>>> GetPortfolioSnapshot(CancellationToken ct)
    {
        var uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid)) return Unauthorized();
        var snapshot = await portfolioSnapshot.GetLatestAsync(uid, ct);
        if (snapshot is null) return NoContent();
        return Ok(snapshot);
    }

    /// <summary>Gets live quotes for all portfolio items (uses Yahoo batch endpoint to avoid rate limits).</summary>
    [HttpGet("quotes")]
    public async Task<ActionResult<IReadOnlyList<PortfolioSummaryDto>>> GetAllQuotes(CancellationToken ct)
    {
        var items = await portfolioService.GetAllAsync(ct);
        if (items.Count == 0) return Ok(Array.Empty<PortfolioSummaryDto>());

        // Separate manual positions from real tickers
        var manualItems = items.Where(i => i.IsManual).ToList();
        var tickerItems = items.Where(i => !i.IsManual).ToList();

        // Single batch call for real tickers
        var quotes = tickerItems.Count > 0
            ? await marketData.GetBatchQuotesAsync(tickerItems.Select(i => i.Symbol), ct)
            : new Dictionary<string, StockQuote>();

        var results = new List<PortfolioSummaryDto>();

        foreach (var item in tickerItems)
        {
            quotes.TryGetValue(item.Symbol, out var quote);
            if (quote is not null) quote.CompanyName = item.CompanyName;
            var technical = await technicalSnapshots.GetSnapshotAsync(item.Symbol, ct);
            results.Add(new PortfolioSummaryDto(item, quote, technical.PriceStructure));
        }

        // For manual positions, synthesize a StockQuote from stored values (no Yahoo call)
        foreach (var item in manualItems)
        {
            var mv = item.ManualMarketValue ?? item.AverageCostBasis;
            var syntheticQuote = new StockQuote
            {
                Symbol        = item.Symbol,
                CompanyName   = item.CompanyName,
                CurrentPrice  = mv,          // shares = 1 so price == total value
                Change        = 0m,
                ChangePercent = 0m,
                Sector        = item.Sector,
                Industry      = item.Industry,
                MarketState   = "MANUAL",
                Timestamp     = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            results.Add(new PortfolioSummaryDto(item, syntheticQuote, PriceStructureResult.None));
        }

        // Return in original sort order (by symbol)
        var sorted = results.OrderBy(r => r.Item.Symbol).ToList();

        // Persist snapshot so the frontend loads instantly on next page open
        var uid = CurrentUserId();
        if (!string.IsNullOrEmpty(uid))
        {
            await portfolioSnapshot.SaveAsync(uid, sorted.AsReadOnly(), ct);
            await dashboard.RebuildAsync(uid, ct);
        }

        return Ok(sorted);
    }

    /// <summary>Gets a live quote for a single symbol.</summary>
    [HttpGet("quote/{symbol}")]
    public async Task<ActionResult<StockQuote>> GetQuote(string symbol, CancellationToken ct)
    {
        var quote = await marketData.GetQuoteAsync(symbol, ct);
        return quote is null ? NotFound() : Ok(quote);
    }

    /// <summary>Searches for stock symbols.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<SymbolSearchResult>>> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Query parameter 'q' is required.");

        var results = await marketData.SearchSymbolAsync(q, ct);
        return Ok(results);
    }

    /// <summary>Lightweight batch price lookup for arbitrary symbols. Max 50 symbols per call.
    /// Used by EOD Signals page to refresh last-price column without running a full RSI scan.</summary>
    [HttpPost("batch-prices")]
    public async Task<IActionResult> GetBatchPrices(
        [FromBody] IReadOnlyList<string> symbols, CancellationToken ct)
    {
        if (symbols is null || symbols.Count == 0) return Ok(Array.Empty<object>());
        var distinct = symbols.Take(50).Select(s => s.Trim().ToUpperInvariant()).Distinct().ToList();
        var quotes = await marketData.GetBatchQuotesAsync(distinct, ct);
        var result = quotes.Select(kv => new { symbol = kv.Key, price = kv.Value.CurrentPrice }).ToList();
        return Ok(result);
    }
}