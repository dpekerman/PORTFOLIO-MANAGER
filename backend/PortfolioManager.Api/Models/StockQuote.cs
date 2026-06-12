namespace PortfolioManager.Api.Models;

/// <summary>
/// Live quote returned from Yahoo Finance, enriched with portfolio data.
/// </summary>
public class StockQuote
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal PreviousClose { get; set; }
    public string Sector { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    /// <summary>Yahoo Finance market state: REGULAR, PRE, POST, CLOSED, PREPRE, POSTPOST.</summary>
    public string MarketState { get; set; } = string.Empty;
    public long Timestamp { get; set; }
}
