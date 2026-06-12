using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController(IMarketDataProvider marketData, IPortfolioService portfolioService) : ControllerBase
{
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
            results.Add(new PortfolioSummaryDto(item, quote));
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
            results.Add(new PortfolioSummaryDto(item, syntheticQuote));
        }

        // Return in original sort order (by symbol)
        return Ok(results.OrderBy(r => r.Item.Symbol).ToList());
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
}
