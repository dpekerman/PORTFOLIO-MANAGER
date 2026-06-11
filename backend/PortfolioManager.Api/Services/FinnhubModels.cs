namespace PortfolioManager.Api.Services;

/// <summary>
/// Response shape from Finnhub /quote endpoint.
/// </summary>
public sealed class FinnhubQuoteResponse
{
    public decimal C { get; set; }  // Current price
    public decimal D { get; set; }  // Change
    public decimal Dp { get; set; } // Percent change
    public decimal H { get; set; }  // High
    public decimal L { get; set; }  // Low
    public decimal O { get; set; }  // Open
    public decimal Pc { get; set; } // Previous close
    public long T { get; set; }     // Timestamp
}

/// <summary>
/// Response shape from Finnhub /search endpoint.
/// </summary>
public sealed class FinnhubSearchResponse
{
    public int Count { get; set; }
    public List<FinnhubSearchResult> Result { get; set; } = [];
}

public sealed class FinnhubSearchResult
{
    public string Description { get; set; } = string.Empty;
    public string DisplaySymbol { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
