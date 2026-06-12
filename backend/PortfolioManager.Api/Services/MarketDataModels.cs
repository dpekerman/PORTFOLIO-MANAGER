using System.Text.Json.Serialization;

namespace PortfolioManager.Api.Services;

// ── Yahoo Finance /v8/finance/chart response ────────────────────────────────
public sealed class YahooChartResponse
{
    public YahooChart? Chart { get; set; }
}

public sealed class YahooChart
{
    public List<YahooChartResult>? Result { get; set; }
}

public sealed class YahooChartResult
{
    public YahooMeta? Meta { get; set; }
    public List<long>? Timestamp { get; set; }
    public YahooIndicators? Indicators { get; set; }
}

public sealed class YahooMeta
{
    [JsonPropertyName("regularMarketPrice")]         public decimal RegularMarketPrice { get; set; }
    [JsonPropertyName("regularMarketDayHigh")]       public decimal RegularMarketDayHigh { get; set; }
    [JsonPropertyName("regularMarketDayLow")]        public decimal RegularMarketDayLow { get; set; }
    [JsonPropertyName("regularMarketOpen")]          public decimal RegularMarketOpen { get; set; }
    [JsonPropertyName("regularMarketVolume")]        public long    RegularMarketVolume { get; set; }
    [JsonPropertyName("chartPreviousClose")]         public decimal ChartPreviousClose { get; set; }
    [JsonPropertyName("regularMarketPreviousClose")] public decimal RegularMarketPreviousClose { get; set; }
    [JsonPropertyName("regularMarketChange")]        public decimal RegularMarketChange { get; set; }
    [JsonPropertyName("regularMarketChangePercent")] public decimal RegularMarketChangePercent { get; set; }
    [JsonPropertyName("longName")]                   public string? LongName { get; set; }
    [JsonPropertyName("shortName")]                  public string? ShortName { get; set; }
}

public sealed class YahooIndicators
{
    [JsonPropertyName("quote")]
    public List<YahooQuoteData>? Quote { get; set; }
}

public sealed class YahooQuoteData
{
    public List<decimal?> Open   { get; set; } = [];
    public List<decimal?> High   { get; set; } = [];
    public List<decimal?> Low    { get; set; } = [];
    public List<decimal?> Close  { get; set; } = [];
    public List<long?>    Volume { get; set; } = [];
}

// ── Yahoo Finance /v1/finance/search response ───────────────────────────────
public sealed class YahooSearchResponse
{
    public List<YahooSearchQuote>? Quotes { get; set; }
}

public sealed class YahooSearchQuote
{
    public string  Symbol   { get; set; } = "";
    public string? Shortname { get; set; }
    public string? Longname  { get; set; }

    [JsonPropertyName("exchDisp")]
    public string? ExchDisp { get; set; }

    [JsonPropertyName("typeDisp")]
    public string? TypeDisp { get; set; }
}

// ── Yahoo Finance /v7/finance/quote batch response ──────────────────────────
public sealed class YahooBatchQuoteResponse
{
    [JsonPropertyName("quoteResponse")]
    public YahooQuoteResponse? QuoteResponse { get; set; }
}

public sealed class YahooQuoteResponse
{
    public List<YahooQuoteItem>? Result { get; set; }
}

public sealed class YahooQuoteItem
{
    [JsonPropertyName("symbol")]                        public string  Symbol                      { get; set; } = "";
    [JsonPropertyName("regularMarketPrice")]            public decimal RegularMarketPrice           { get; set; }
    [JsonPropertyName("regularMarketChange")]           public decimal RegularMarketChange          { get; set; }
    [JsonPropertyName("regularMarketChangePercent")]    public decimal RegularMarketChangePercent   { get; set; }
    [JsonPropertyName("regularMarketDayHigh")]          public decimal RegularMarketDayHigh         { get; set; }
    [JsonPropertyName("regularMarketDayLow")]           public decimal RegularMarketDayLow          { get; set; }
    [JsonPropertyName("regularMarketOpen")]             public decimal RegularMarketOpen            { get; set; }
    [JsonPropertyName("regularMarketPreviousClose")]    public decimal RegularMarketPreviousClose   { get; set; }
    [JsonPropertyName("regularMarketVolume")]           public long    RegularMarketVolume          { get; set; }
    [JsonPropertyName("longName")]                      public string? LongName                     { get; set; }
    [JsonPropertyName("shortName")]                     public string? ShortName                    { get; set; }
    [JsonPropertyName("sector")]                        public string? Sector                       { get; set; }
    [JsonPropertyName("industry")]                      public string? Industry                     { get; set; }
    [JsonPropertyName("quoteType")]                     public string? QuoteType                    { get; set; }
    [JsonPropertyName("marketState")]                   public string? MarketState                  { get; set; }
    [JsonPropertyName("marketCap")]                     public long?   MarketCap                    { get; set; }
    [JsonPropertyName("fiftyTwoWeekHigh")]              public decimal FiftyTwoWeekHigh             { get; set; }
    [JsonPropertyName("fiftyTwoWeekLow")]               public decimal FiftyTwoWeekLow              { get; set; }
}

/// <summary>Result shape returned by GET /api/stocks/search.</summary>
public sealed class SymbolSearchResult
{
    public string Description   { get; set; } = string.Empty;
    public string DisplaySymbol { get; set; } = string.Empty;
    public string Symbol        { get; set; } = string.Empty;
    public string Type          { get; set; } = string.Empty;
}

// ── Yahoo Finance /v11/finance/quoteSummary?modules=assetProfile ──────────────
public sealed class YahooQuoteSummaryResponse
{
    [JsonPropertyName("quoteSummary")]
    public YahooQuoteSummaryResult? QuoteSummary { get; set; }
}

public sealed class YahooQuoteSummaryResult
{
    public List<YahooAssetProfileWrapper>? Result { get; set; }
}

public sealed class YahooAssetProfileWrapper
{
    [JsonPropertyName("assetProfile")]
    public YahooAssetProfile? AssetProfile { get; set; }
}

public sealed class YahooAssetProfile
{
    [JsonPropertyName("sector")]
    public string? Sector   { get; set; }
    [JsonPropertyName("industry")]
    public string? Industry { get; set; }
}
