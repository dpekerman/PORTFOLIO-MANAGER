using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController(IFinnhubService finnhubService, IPortfolioService portfolioService) : ControllerBase
{
    /// <summary>Gets live quotes for all portfolio items.</summary>
    [HttpGet("quotes")]
    public async Task<ActionResult<IReadOnlyList<PortfolioSummaryDto>>> GetAllQuotes(CancellationToken ct)
    {
        var items = await portfolioService.GetAllAsync(ct);

        var tasks = items.Select(async item =>
        {
            var quote = await finnhubService.GetQuoteAsync(item.Symbol, ct);
            if (quote is not null) quote.CompanyName = item.CompanyName;
            return new PortfolioSummaryDto(item, quote);
        });

        var results = await Task.WhenAll(tasks);
        return Ok(results);
    }

    /// <summary>Gets a live quote for a single symbol.</summary>
    [HttpGet("quote/{symbol}")]
    public async Task<ActionResult<StockQuote>> GetQuote(string symbol, CancellationToken ct)
    {
        var quote = await finnhubService.GetQuoteAsync(symbol, ct);
        return quote is null ? NotFound() : Ok(quote);
    }

    /// <summary>Searches for stock symbols via Finnhub.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<FinnhubSearchResult>>> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Query parameter 'q' is required.");

        var results = await finnhubService.SearchSymbolAsync(q, ct);
        return Ok(results);
    }
}
