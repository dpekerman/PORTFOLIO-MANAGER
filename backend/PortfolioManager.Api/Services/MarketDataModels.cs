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

    [JsonPropertyName("quoteType")]
    public string? QuoteType { get; set; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }
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
    [JsonPropertyName("targetMeanPrice")]               public decimal TargetMeanPrice              { get; set; }
    [JsonPropertyName("trailingPE")]                    public decimal TrailingPE                   { get; set; }
    [JsonPropertyName("forwardPE")]                     public decimal ForwardPE                    { get; set; }
    [JsonPropertyName("priceToBook")]                   public decimal PriceToBook                  { get; set; }
    [JsonPropertyName("trailingAnnualDividendYield")]   public decimal TrailingAnnualDividendYield   { get; set; }
}

/// <summary>Result shape returned by GET /api/stocks/search.</summary>
public sealed class SymbolSearchResult
{
    public string Description   { get; set; } = string.Empty;
    public string DisplaySymbol { get; set; } = string.Empty;
    public string Symbol        { get; set; } = string.Empty;
    public string Type          { get; set; } = string.Empty;
    public string Exchange      { get; set; } = string.Empty;
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

// ── Yahoo Finance /v10/finance/quoteSummary?modules=financialData ─────────────
public sealed class YahooFinancialDataWrapper
{
    [JsonPropertyName("financialData")]
    public YahooFinancialData? FinancialData { get; set; }
}

public sealed class YahooFinancialData
{
    [JsonPropertyName("targetMeanPrice")]
    public YahooRawValue? TargetMeanPrice { get; set; }
    [JsonPropertyName("targetHighPrice")]
    public YahooRawValue? TargetHighPrice { get; set; }
    [JsonPropertyName("targetLowPrice")]
    public YahooRawValue? TargetLowPrice  { get; set; }
    [JsonPropertyName("numberOfAnalystOpinions")]
    public YahooRawValue? NumberOfAnalystOpinions { get; set; }
}

public sealed class YahooRawValue
{
    [JsonPropertyName("raw")]  public decimal Raw  { get; set; }
    [JsonPropertyName("fmt")]  public string?  Fmt  { get; set; }
}

// ── Yahoo Finance v10/finance/quoteSummary (multi-module) ─────────────────────
// Used by ValueScreenerService for deep fundamental data.
// Modules: defaultKeyStatistics, financialData, cashflowStatementHistory, incomeStatementHistory

public sealed class YahooQuoteSummaryMultiResponse
{
    [JsonPropertyName("quoteSummary")]
    public YahooQuoteSummaryMultiResult? QuoteSummary { get; set; }
}

public sealed class YahooQuoteSummaryMultiResult
{
    [JsonPropertyName("result")]
    public List<YahooQuoteSummaryModules>? Result { get; set; }
    [JsonPropertyName("error")]
    public object? Error { get; set; }
}

public sealed class YahooQuoteSummaryModules
{
    [JsonPropertyName("defaultKeyStatistics")]
    public YahooDefaultKeyStatistics? DefaultKeyStatistics { get; set; }

    [JsonPropertyName("financialData")]
    public YahooFinancialDataFull? FinancialData { get; set; }

    [JsonPropertyName("cashflowStatementHistory")]
    public YahooCashflowStatementHistory? CashflowStatementHistory { get; set; }

    [JsonPropertyName("incomeStatementHistory")]
    public YahooIncomeStatementHistory? IncomeStatementHistory { get; set; }

    [JsonPropertyName("balanceSheetHistory")]
    public YahooBalanceSheetHistory? BalanceSheetHistory { get; set; }

    [JsonPropertyName("summaryDetail")]
    public YahooSummaryDetail? SummaryDetail { get; set; }
}

// -- defaultKeyStatistics module -----------------------------------------------
public sealed class YahooDefaultKeyStatistics
{
    [JsonPropertyName("enterpriseValue")]       public YahooRawValue? EnterpriseValue       { get; set; }
    [JsonPropertyName("forwardPE")]             public YahooRawValue? ForwardPE              { get; set; }
    [JsonPropertyName("trailingEps")]           public YahooRawValue? TrailingEps            { get; set; }
    [JsonPropertyName("forwardEps")]            public YahooRawValue? ForwardEps             { get; set; }
    [JsonPropertyName("bookValue")]             public YahooRawValue? BookValue              { get; set; }
    [JsonPropertyName("priceToBook")]           public YahooRawValue? PriceToBook            { get; set; }
    [JsonPropertyName("returnOnEquity")]        public YahooRawValue? ReturnOnEquity         { get; set; }
    [JsonPropertyName("returnOnAssets")]        public YahooRawValue? ReturnOnAssets         { get; set; }
    [JsonPropertyName("profitMargins")]         public YahooRawValue? ProfitMargins          { get; set; }
    [JsonPropertyName("sharesOutstanding")]     public YahooRawValue? SharesOutstanding      { get; set; }
    [JsonPropertyName("floatShares")]           public YahooRawValue? FloatShares            { get; set; }
    [JsonPropertyName("heldPercentInstitutions")] public YahooRawValue? HeldPercentInstitutions { get; set; }
    [JsonPropertyName("trailingPE")]            public YahooRawValue? TrailingPE             { get; set; }
}

// -- financialData module -------------------------------------------------------
public sealed class YahooFinancialDataFull
{
    [JsonPropertyName("targetMeanPrice")]             public YahooRawValue? TargetMeanPrice           { get; set; }
    [JsonPropertyName("targetHighPrice")]             public YahooRawValue? TargetHighPrice           { get; set; }
    [JsonPropertyName("targetLowPrice")]              public YahooRawValue? TargetLowPrice            { get; set; }
    [JsonPropertyName("numberOfAnalystOpinions")]     public YahooRawValue? NumberOfAnalystOpinions   { get; set; }
    [JsonPropertyName("totalCash")]                   public YahooRawValue? TotalCash                 { get; set; }
    [JsonPropertyName("totalDebt")]                   public YahooRawValue? TotalDebt                 { get; set; }
    [JsonPropertyName("totalRevenue")]                public YahooRawValue? TotalRevenue              { get; set; }
    [JsonPropertyName("ebitda")]                      public YahooRawValue? Ebitda                    { get; set; }
    [JsonPropertyName("operatingCashflow")]           public YahooRawValue? OperatingCashflow         { get; set; }
    [JsonPropertyName("freeCashflow")]                public YahooRawValue? FreeCashflow              { get; set; }
    [JsonPropertyName("returnOnAssets")]              public YahooRawValue? ReturnOnAssets            { get; set; }
    [JsonPropertyName("returnOnEquity")]              public YahooRawValue? ReturnOnEquity            { get; set; }
    [JsonPropertyName("revenueGrowth")]               public YahooRawValue? RevenueGrowth             { get; set; }
    [JsonPropertyName("grossProfits")]                public YahooRawValue? GrossProfits              { get; set; }
    [JsonPropertyName("currentRatio")]                public YahooRawValue? CurrentRatio              { get; set; }
    [JsonPropertyName("debtToEquity")]                public YahooRawValue? DebtToEquity              { get; set; }
    [JsonPropertyName("quickRatio")]                  public YahooRawValue? QuickRatio                { get; set; }
}

// -- cashflowStatementHistory module -------------------------------------------
public sealed class YahooCashflowStatementHistory
{
    [JsonPropertyName("cashflowStatements")]
    public List<YahooCashflowStatement>? CashflowStatements { get; set; }
}

public sealed class YahooCashflowStatement
{
    [JsonPropertyName("endDate")]                      public YahooRawValue? EndDate                    { get; set; }
    [JsonPropertyName("totalCashFromOperatingActivities")] public YahooRawValue? OperatingCashFlow      { get; set; }
    [JsonPropertyName("capitalExpenditures")]          public YahooRawValue? CapitalExpenditures        { get; set; }
    [JsonPropertyName("totalCashFromInvestingActivities")] public YahooRawValue? InvestingCashFlow      { get; set; }
    [JsonPropertyName("totalCashFromFinancingActivities")] public YahooRawValue? FinancingCashFlow      { get; set; }
}

// -- incomeStatementHistory module ---------------------------------------------
public sealed class YahooIncomeStatementHistory
{
    [JsonPropertyName("incomeStatementHistory")]
    public List<YahooIncomeStatement>? IncomeStatements { get; set; }
}

public sealed class YahooIncomeStatement
{
    [JsonPropertyName("endDate")]           public YahooRawValue? EndDate          { get; set; }
    [JsonPropertyName("totalRevenue")]      public YahooRawValue? TotalRevenue     { get; set; }
    [JsonPropertyName("grossProfit")]       public YahooRawValue? GrossProfit      { get; set; }
    [JsonPropertyName("ebit")]              public YahooRawValue? Ebit             { get; set; }
    [JsonPropertyName("netIncome")]         public YahooRawValue? NetIncome        { get; set; }
    [JsonPropertyName("totalOperatingExpenses")] public YahooRawValue? TotalOperatingExpenses { get; set; }
}

// -- balanceSheetHistory module ------------------------------------------------
public sealed class YahooBalanceSheetHistory
{
    [JsonPropertyName("balanceSheetStatements")]
    public List<YahooBalanceSheetStatement>? BalanceSheetStatements { get; set; }
}

public sealed class YahooBalanceSheetStatement
{
    [JsonPropertyName("endDate")]                 public YahooRawValue? EndDate               { get; set; }
    [JsonPropertyName("totalAssets")]             public YahooRawValue? TotalAssets           { get; set; }
    [JsonPropertyName("totalLiab")]               public YahooRawValue? TotalLiabilities      { get; set; }
    [JsonPropertyName("totalStockholderEquity")]  public YahooRawValue? StockholdersEquity    { get; set; }
    [JsonPropertyName("longTermDebt")]            public YahooRawValue? LongTermDebt          { get; set; }
    [JsonPropertyName("shortLongTermDebt")]       public YahooRawValue? ShortTermDebt         { get; set; }
    [JsonPropertyName("totalCurrentAssets")]      public YahooRawValue? TotalCurrentAssets    { get; set; }
    [JsonPropertyName("totalCurrentLiabilities")] public YahooRawValue? TotalCurrentLiabilities { get; set; }
    [JsonPropertyName("cash")]                    public YahooRawValue? Cash                  { get; set; }
    [JsonPropertyName("netReceivables")]          public YahooRawValue? NetReceivables        { get; set; }
}

// -- summaryDetail module ------------------------------------------------------
public sealed class YahooSummaryDetail
{
    [JsonPropertyName("marketCap")]           public YahooRawValue? MarketCap         { get; set; }
    [JsonPropertyName("trailingPE")]          public YahooRawValue? TrailingPE        { get; set; }
    [JsonPropertyName("forwardPE")]           public YahooRawValue? ForwardPE         { get; set; }
    [JsonPropertyName("dividendYield")]       public YahooRawValue? DividendYield     { get; set; }
    [JsonPropertyName("trailingAnnualDividendYield")] public YahooRawValue? TrailingAnnualDividendYield { get; set; }
    [JsonPropertyName("fiftyTwoWeekHigh")]    public YahooRawValue? FiftyTwoWeekHigh  { get; set; }
    [JsonPropertyName("fiftyTwoWeekLow")]     public YahooRawValue? FiftyTwoWeekLow   { get; set; }
    [JsonPropertyName("priceToBook")]         public YahooRawValue? PriceToBook       { get; set; }
    [JsonPropertyName("beta")]                public YahooRawValue? Beta              { get; set; }
}

// -- Consolidated fundamental snapshot (output of GetFundamentalsAsync) --------
public sealed class FundamentalsSnapshot
{
    public string Symbol             { get; set; } = "";
    public string Sector             { get; set; } = "";
    public string Industry           { get; set; } = "";

    // Price / market data
    public decimal Price             { get; set; }
    public long    MarketCap         { get; set; }
    public long    EnterpriseValue   { get; set; }
    public decimal Week52High        { get; set; }
    public decimal Week52Low         { get; set; }

    // Valuation multiples
    public decimal TrailingPE        { get; set; }
    public decimal ForwardPE         { get; set; }
    public decimal PriceToBook       { get; set; }
    public decimal TrailingEps       { get; set; }
    public decimal ForwardEps        { get; set; }
    public decimal BookValuePerShare { get; set; }

    // Cash flow
    public long OperatingCashFlow    { get; set; }  // most recent annual, absolute value
    public long CapitalExpenditures  { get; set; }  // most recent annual, absolute value (negative in Yahoo → stored as positive)
    public long FreeCashFlow         { get; set; }  // OCF − |CapEx|; also can come from financialData.freeCashflow

    // Income statement
    public long TotalRevenue         { get; set; }
    public long NetIncome            { get; set; }
    public long Ebit                 { get; set; }

    // Balance sheet
    public long TotalAssets          { get; set; }
    public long TotalLiabilities     { get; set; }
    public long StockholdersEquity   { get; set; }
    public long LongTermDebt         { get; set; }
    public long ShortTermDebt        { get; set; }
    public long TotalCurrentAssets   { get; set; }
    public long TotalCurrentLiabilities { get; set; }
    public long Cash                 { get; set; }
    public long NetReceivables       { get; set; }

    // Profitability / quality
    public decimal ReturnOnAssets    { get; set; }  // ratio e.g. 0.08 = 8%
    public decimal ReturnOnEquity    { get; set; }
    public decimal DebtToEquity      { get; set; }
    public decimal CurrentRatio      { get; set; }
    public decimal ProfitMargins     { get; set; }
    public decimal RevenueGrowth     { get; set; }

    // Dividend
    public decimal DividendYield     { get; set; }  // ratio e.g. 0.035 = 3.5%
    public decimal TrailingAnnualDividendYield { get; set; }

    // Analyst
    public decimal TargetMeanPrice   { get; set; }
    public int     AnalystCount      { get; set; }

    // Flags
    public bool DataAvailable        { get; set; }  // false if quoteSummary returned nothing
}
