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
    /// <summary>Provider time of the regular-session price; null for legacy/fallback quotes without provenance.</summary>
    public long? SourcePriceTimestamp { get; set; }
    /// <summary>Trading currency reported by the market-data provider (ISO code); null when the provider did not supply it.</summary>
    public string? Currency { get; set; }
    /// <summary>52-week high price.</summary>
    public decimal Week52High { get; set; }
    /// <summary>52-week low price.</summary>
    public decimal Week52Low { get; set; }
    /// <summary>Analyst consensus 1-year target price (mean). 0 when not available.</summary>
    public decimal TargetMeanPrice { get; set; }
    // ── Fundamental data (from Yahoo Finance v7 quote) ─────────────────────────
    /// <summary>Trailing P/E ratio. 0 when not available.</summary>
    public decimal TrailingPE { get; set; }
    /// <summary>Forward P/E ratio. 0 when not available.</summary>
    public decimal ForwardPE { get; set; }
    /// <summary>Price-to-Book ratio. 0 when not available.</summary>
    public decimal PriceToBook { get; set; }
    /// <summary>Trailing annual dividend yield (0.03 = 3%). 0 when not paying.</summary>
    public decimal DividendYield { get; set; }
    /// <summary>Market capitalisation in the quote currency. 0 when not available.</summary>
    public long MarketCap { get; set; }
    // ── Forward dividend (estimate, not a guaranteed payment) ─────────────────
    /// <summary>Annualised forward dividend per share in the trading currency. Null when unknown or unreliable; 0 only when the provider reports a zero rate.</summary>
    public decimal? ForwardDividendPerShare { get; set; }
    /// <summary>Forward annual dividend ÷ current price × 100 (a percentage, 3.5 = 3.5%). Null when unknown or unreliable.</summary>
    public decimal? ForwardYieldPct { get; set; }
    /// <summary>Positive, Zero, Missing, Stale or Inconsistent. Missing means unknown and is never a zero dividend.</summary>
    public string? DividendStatus { get; set; }
    /// <summary>Provider's latest/next dividend payment date (UTC). It dates the dividend; Yahoo supplies no observation time for it.</summary>
    public DateTime? DividendDateUtc { get; set; }
    public string? DividendSource { get; set; }
}
