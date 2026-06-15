using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Abstraction for a market-data provider. The default implementation is Yahoo Finance.
/// A different provider (e.g. Alpha Vantage, Polygon.io) can be registered in Program.cs
/// by swapping the concrete class while keeping this interface.
/// </summary>
public interface IMarketDataProvider
{
    Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default);
    Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default);
    Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default);
    Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default);
    /// <summary>Fetch analyst consensus 1-year target prices via quoteSummary/financialData module.</summary>
    Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(IEnumerable<string> symbols, CancellationToken ct = default);
    /// <summary>
    /// Fetch deep fundamental snapshot for a single symbol via v10/finance/quoteSummary
    /// with modules: defaultKeyStatistics, financialData, cashflowStatementHistory,
    /// incomeStatementHistory, balanceSheetHistory, summaryDetail.
    /// Returns null if the symbol is not found or quoteSummary returns an error.
    /// </summary>
    Task<FundamentalsSnapshot?> GetFundamentalsAsync(string symbol, CancellationToken ct = default);
}

/// <summary>
/// Yahoo Finance implementation of <see cref="IMarketDataProvider"/>.
///
/// Endpoint strategy:
///   - Live quotes (batch):  query2 /v7/finance/quote + crumb  (returns price, change, sector, industry)
///   - Sector/industry:      query2 /v10/finance/quoteSummary?modules=assetProfile + crumb
///   - Fallback price only:  query1 /v8/finance/chart  (no crumb needed)
///   - Symbol search:        query1 /v1/finance/search  (no crumb needed)
/// </summary>
public sealed class YahooFinanceService : IMarketDataProvider
{
    private readonly HttpClient          _http;   // query1 â€” no crumb needed (v8 chart, search)
    private readonly YahooCrumbService   _crumb;
    private readonly ILogger<YahooFinanceService> _logger;

    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    // Maps Yahoo quoteType â†’ sector label when Yahoo doesn't return one
    private static readonly Dictionary<string, string> QuoteTypeSectorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ETF"]            = "ETFs & Funds",
        ["MUTUALFUND"]     = "ETFs & Funds",
        ["CURRENCY"]       = "Currencies & Forex",
        ["CRYPTOCURRENCY"] = "Crypto",
        ["INDEX"]          = "Indices",
        ["FUTURE"]         = "Futures & Commodities",
    };
    // Quote types excluded from symbol search results.
    // OPTION is intentionally kept so users can add options contracts to their watchlist.
    private static readonly HashSet<string> ExcludedSearchQuoteTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FUTURE", "CURRENCY", "CRYPTOCURRENCY"
    };
    public YahooFinanceService(HttpClient http, YahooCrumbService crumb, ILogger<YahooFinanceService> logger)
    {
        _http   = http;
        _crumb  = crumb;
        _logger = logger;
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Build a request to query2.finance.yahoo.com with crumb+cookie headers.</summary>
    private async Task<HttpRequestMessage> CrumbRequestAsync(string relativeUrl, CancellationToken ct)
    {
        var (crumb, cookieHeader) = await _crumb.GetAsync(ct);
        var separator = relativeUrl.Contains('?') ? "&" : "?";
        var url       = $"https://query2.finance.yahoo.com/{relativeUrl}{separator}crumb={Uri.EscapeDataString(crumb)}";
        var req       = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Cookie", cookieHeader);
        return req;
    }

    /// <summary>
    /// Sends a crumb-authenticated request to query2. If Yahoo returns 401 (crumb expired),
    /// invalidates the crumb, refreshes it, and retries once.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithCrumbAsync(string relativeUrl, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var req  = await CrumbRequestAsync(relativeUrl, ct);
            var resp = await _http.SendAsync(req, ct);

            if (resp.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                _logger.LogWarning("Yahoo Finance crumb expired â€” refreshing and retrying");
                _crumb.Invalidate();
                continue;
            }
            return resp;
        }
        throw new InvalidOperationException("Yahoo Finance crumb retry exhausted");
    }

    // â”€â”€ IMarketDataProvider â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        // Use v8/chart (no crumb needed) for single-symbol fallback
        try
        {
            var resp = await _http.GetAsync(
                $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=1d", ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json   = await resp.Content.ReadAsStringAsync(ct);
            var data   = JsonSerializer.Deserialize<YahooChartResponse>(json, _json);
            var result = data?.Chart?.Result?.FirstOrDefault();
            if (result?.Meta is null) return null;

            var m         = result.Meta;
            decimal price = m.RegularMarketPrice;
            decimal prev  = m.ChartPreviousClose != 0 ? m.ChartPreviousClose : m.RegularMarketPreviousClose;
            decimal change    = price - prev;
            decimal changePct = prev > 0 ? (change / prev) * 100m : 0m;

            return new StockQuote
            {
                Symbol        = symbol.ToUpperInvariant(),
                CompanyName   = m.LongName ?? m.ShortName ?? symbol,
                CurrentPrice  = price,
                Change        = Math.Round(change, 2),
                ChangePercent = Math.Round(changePct, 2),
                HighPrice     = m.RegularMarketDayHigh,
                LowPrice      = m.RegularMarketDayLow,
                OpenPrice     = m.RegularMarketOpen,
                PreviousClose = prev,
                Timestamp     = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yahoo Finance GetQuoteAsync failed for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<Dictionary<string, StockQuote>> GetBatchQuotesAsync(
        IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var result     = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);
        var symbolList = symbols.ToList();
        if (symbolList.Count == 0) return result;

        const int batchSize = 50;
        for (int i = 0; i < symbolList.Count; i += batchSize)
        {
            var batch  = symbolList.Skip(i).Take(batchSize).ToList();
            var joined = string.Join(",", batch.Select(Uri.EscapeDataString));
            try
            {
                var url  = $"v7/finance/quote?symbols={joined}" +
                           "&fields=regularMarketPrice,regularMarketChange,regularMarketChangePercent," +
                           "regularMarketDayHigh,regularMarketDayLow,regularMarketOpen," +
                           "regularMarketPreviousClose,regularMarketVolume," +
                           "longName,shortName,sector,industry,quoteType,marketState," +
                           "fiftyTwoWeekHigh,fiftyTwoWeekLow,targetMeanPrice,trailingPE,forwardPE,priceToBook,trailingAnnualDividendYield";

                var resp = await SendWithCrumbAsync(url, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Yahoo Finance v7 batch returned {Status} for batch {i}", resp.StatusCode, i);
                    continue;
                }

                var json = await resp.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<YahooBatchQuoteResponse>(json, _json);

                foreach (var q in data?.QuoteResponse?.Result ?? [])
                    result[q.Symbol] = MapQuoteItem(q);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yahoo Finance GetBatchQuotesAsync failed at batch {i}", i);
            }
        }

        // TSX fallback: symbols that returned no data are retried with ".TO"
        await FillMissingWithTsxFallbackAsync(symbolList, result, ct);

        return result;
    }

    public async Task<(string sector, string industry)> GetSectorAsync(string symbol, CancellationToken ct = default)
    {
        // Try both bare symbol and .TO variant via quoteSummary (most reliable source)
        var candidates = symbol.Contains('.') ? new[] { symbol } : new[] { symbol, symbol + ".TO" };

        foreach (var candidate in candidates)
        {
            try
            {
                var resp = await SendWithCrumbAsync(
                    $"v10/finance/quoteSummary/{Uri.EscapeDataString(candidate)}?modules=assetProfile", ct);

                if (!resp.IsSuccessStatusCode) continue;

                var json    = await resp.Content.ReadAsStringAsync(ct);
                var data    = JsonSerializer.Deserialize<YahooQuoteSummaryResponse>(json, _json);
                var profile = data?.QuoteSummary?.Result?.FirstOrDefault()?.AssetProfile;

                if (profile is not null && !string.IsNullOrWhiteSpace(profile.Sector))
                {
                    _logger.LogDebug("Sector for {Symbol} ({Candidate}): {Sector} / {Industry}",
                        symbol, candidate, profile.Sector, profile.Industry);
                    return (profile.Sector, profile.Industry ?? "");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yahoo Finance quoteSummary failed for {Candidate}", candidate);
            }
        }

        // Fallback: extract sector from the v7 batch result (includes quoteType-based labels for ETFs)
        try
        {
            var batch = await GetBatchQuotesAsync(new[] { symbol }, ct);
            if (batch.TryGetValue(symbol, out var q) && !string.IsNullOrWhiteSpace(q.Sector))
                return (q.Sector, q.Industry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yahoo Finance GetSectorAsync batch fallback failed for {Symbol}", symbol);
        }

        return ("", "");
    }

    public async Task<IReadOnlyList<SymbolSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default)
    {
        try
        {
            // Fetch more results than we need (20) so filtering still returns enough.
            // region=CA + lang=en-CA biases Yahoo towards TSX/TSX-V results first.
            var resp = await _http.GetAsync(
                $"https://query1.finance.yahoo.com/v1/finance/search" +
                $"?q={Uri.EscapeDataString(query)}&quotesCount=20&newsCount=0" +
                $"&listsCount=0&region=CA&lang=en-CA", ct);
            if (!resp.IsSuccessStatusCode) return [];

            var json = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<YahooSearchResponse>(json, _json);

            var results = (data?.Quotes ?? [])
                .Where(q => !string.IsNullOrWhiteSpace(q.Symbol))
                // Filter out futures, currencies — not tradeable in portfolio context
                .Where(q => string.IsNullOrWhiteSpace(q.QuoteType) || !ExcludedSearchQuoteTypes.Contains(q.QuoteType))
                .Select(q => new SymbolSearchResult
                {
                    Symbol        = q.Symbol,
                    Description   = q.Longname ?? q.Shortname ?? q.Symbol,
                    Type          = q.TypeDisp ?? (q.QuoteType ?? "Equity"),
                    DisplaySymbol = q.Symbol,
                    Exchange      = q.ExchDisp ?? ""
                })
                // Sort: Canadian equities first, then other equities, then options/warrants/rights last
                .OrderBy(r => IsOptionOrDerivative(r.Type) ? 2 : IsCanadianSymbol(r.Symbol) ? 0 : 1)
                .ThenBy(r => r.Symbol.Length)
                .Take(10)
                .ToList();

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yahoo Finance SearchSymbolAsync failed for query {Query}", query);
            return [];
        }
    }

    private static bool IsCanadianSymbol(string symbol) =>
        symbol.EndsWith(".TO", StringComparison.OrdinalIgnoreCase) ||
        symbol.EndsWith(".V", StringComparison.OrdinalIgnoreCase) ||
        symbol.EndsWith(".CN", StringComparison.OrdinalIgnoreCase) ||
        symbol.EndsWith(".NE", StringComparison.OrdinalIgnoreCase);

    private static bool IsOptionOrDerivative(string type) =>
        type.Equals("Option", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Warrant", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Rights", StringComparison.OrdinalIgnoreCase);

    // â”€â”€ Private helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private StockQuote MapQuoteItem(YahooQuoteItem q)
    {
        decimal price     = q.RegularMarketPrice != 0 ? q.RegularMarketPrice : q.RegularMarketPreviousClose;
        decimal prevClose = q.RegularMarketPreviousClose;
        decimal change    = q.RegularMarketChange != 0 ? q.RegularMarketChange : price - prevClose;
        decimal changePct = q.RegularMarketChangePercent != 0
            ? q.RegularMarketChangePercent
            : (prevClose > 0 ? change / prevClose * 100m : 0m);

        string sector = q.Sector ?? "";
        if (string.IsNullOrWhiteSpace(sector) && q.QuoteType is not null)
            QuoteTypeSectorMap.TryGetValue(q.QuoteType, out sector!);

        return new StockQuote
        {
            Symbol        = q.Symbol,
            CompanyName   = q.LongName ?? q.ShortName ?? q.Symbol,
            CurrentPrice  = price,
            Change        = Math.Round(change, 2),
            ChangePercent = Math.Round(changePct, 2),
            HighPrice     = q.RegularMarketDayHigh,
            LowPrice      = q.RegularMarketDayLow,
            OpenPrice     = q.RegularMarketOpen,
            PreviousClose = prevClose,
            Sector        = sector ?? "",
            Industry      = q.Industry ?? "",
            MarketState   = q.MarketState ?? "",
            Timestamp     = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Week52High    = q.FiftyTwoWeekHigh,
            Week52Low     = q.FiftyTwoWeekLow,
            TargetMeanPrice = q.TargetMeanPrice,
            TrailingPE    = q.TrailingPE,
            ForwardPE     = q.ForwardPE,
            PriceToBook   = q.PriceToBook,
            DividendYield = q.TrailingAnnualDividendYield,
            MarketCap     = q.MarketCap ?? 0L
        };
    }

    private async Task FillMissingWithTsxFallbackAsync(
        List<string> originalSymbols,
        Dictionary<string, StockQuote> result,
        CancellationToken ct)
    {
        var missing = originalSymbols
            .Where(s => !result.ContainsKey(s) && !s.Contains('.'))
            .ToList();
        if (missing.Count == 0) return;

        var toSymbols = missing.Select(s => s + ".TO").ToList();
        var toResult  = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);

        const int batchSize = 50;
        for (int i = 0; i < toSymbols.Count; i += batchSize)
        {
            var batch  = toSymbols.Skip(i).Take(batchSize).ToList();
            var joined = string.Join(",", batch.Select(Uri.EscapeDataString));
            try
            {
                var url  = $"v7/finance/quote?symbols={joined}" +
                           "&fields=regularMarketPrice,regularMarketChange,regularMarketChangePercent," +
                           "regularMarketDayHigh,regularMarketDayLow,regularMarketOpen," +
                           "regularMarketPreviousClose,regularMarketVolume," +
                           "longName,shortName,sector,industry,quoteType,marketState";
                var resp = await SendWithCrumbAsync(url, ct);
                if (!resp.IsSuccessStatusCode) continue;

                var json = await resp.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<YahooBatchQuoteResponse>(json, _json);
                foreach (var q in data?.QuoteResponse?.Result ?? [])
                    toResult[q.Symbol] = MapQuoteItem(q);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yahoo Finance TSX fallback failed at index {i}", i);
            }
        }

        foreach (var orig in missing)
        {
            if (toResult.TryGetValue(orig + ".TO", out var quote))
            {
                quote.Symbol = orig;
                result[orig] = quote;
            }
        }
    }

    /// <summary>
    /// Fetch analyst 1-year mean target prices via v10/finance/quoteSummary?modules=financialData.
    /// This is more reliable than the targetMeanPrice field in v7/finance/quote which is often 0
    /// for TSX-listed stocks because the field requires a paid Yahoo Finance data subscription.
    /// Returns a dictionary of symbol → targetMeanPrice (0 if unavailable).
    /// </summary>
    public async Task<Dictionary<string, decimal>> GetAnalystTargetsAsync(
        IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in symbols.Distinct())
        {
            // Try the exact symbol first; if it fails and symbol has no '.', also try with ".TO"
            var candidates = symbol.Contains('.')
                ? new[] { symbol }
                : new[] { symbol, symbol + ".TO" };

            decimal target = 0m;
            foreach (var candidate in candidates)
            {
                try
                {
                    var resp = await SendWithCrumbAsync(
                        $"v10/finance/quoteSummary/{Uri.EscapeDataString(candidate)}?modules=financialData", ct);

                    if (!resp.IsSuccessStatusCode) continue;

                    var json = await resp.Content.ReadAsStringAsync(ct);
                    // Parse the response — use a local record to avoid polluting the model namespace
                    using var doc  = System.Text.Json.JsonDocument.Parse(json);
                    var root       = doc.RootElement;
                    if (!root.TryGetProperty("quoteSummary", out var qs)) continue;
                    if (!qs.TryGetProperty("result", out var resArr) || resArr.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
                    if (resArr.GetArrayLength() == 0) continue;

                    var first = resArr[0];
                    if (!first.TryGetProperty("financialData", out var fd)) continue;
                    if (!fd.TryGetProperty("targetMeanPrice", out var tmp)) continue;
                    if (tmp.TryGetProperty("raw", out var rawProp))
                    {
                        target = rawProp.GetDecimal();
                        if (target > 0)
                        {
                            _logger.LogDebug("Analyst target for {Symbol} ({Candidate}): {Target}", symbol, candidate, target);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GetAnalystTargetsAsync failed for {Candidate}", candidate);
                }
                // Brief courtesy delay between individual symbol lookups
                await Task.Delay(300, ct);
            }
            result[symbol] = target;
        }
        return result;
    }

    /// <summary>
    /// Fetches a deep fundamental snapshot via v10/quoteSummary with 6 modules.
    /// Tries the exact symbol first; if that fails and there's no '.' suffix, retries with ".TO".
    /// </summary>
    public async Task<FundamentalsSnapshot?> GetFundamentalsAsync(
        string symbol, CancellationToken ct = default)
    {
        const string modules = "defaultKeyStatistics,financialData,cashflowStatementHistory," +
                               "incomeStatementHistory,balanceSheetHistory,summaryDetail,assetProfile";

        var candidates = symbol.Contains('.')
            ? new[] { symbol }
            : new[] { symbol, symbol + ".TO" };

        foreach (var candidate in candidates)
        {
            try
            {
                var resp = await SendWithCrumbAsync(
                    $"v10/finance/quoteSummary/{Uri.EscapeDataString(candidate)}?modules={Uri.EscapeDataString(modules)}", ct);
                if (!resp.IsSuccessStatusCode) continue;

                var json = await resp.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<YahooQuoteSummaryMultiResponse>(json, _json);
                var r    = data?.QuoteSummary?.Result?.FirstOrDefault();
                if (r is null) continue;

                return MapFundamentalsSnapshot(symbol, r);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetFundamentalsAsync failed for {Candidate}", candidate);
            }
            await Task.Delay(250, ct);
        }
        return null;
    }

    private static FundamentalsSnapshot MapFundamentalsSnapshot(string symbol, YahooQuoteSummaryModules r)
    {
        var ks  = r.DefaultKeyStatistics;
        var fd  = r.FinancialData;
        var sd  = r.SummaryDetail;
        var cf  = r.CashflowStatementHistory?.CashflowStatements?.FirstOrDefault();
        var inc = r.IncomeStatementHistory?.IncomeStatements?.FirstOrDefault();
        var bs  = r.BalanceSheetHistory?.BalanceSheetStatements?.FirstOrDefault();
        var ap  = r.CashflowStatementHistory; // will use assetProfile via YahooAssetProfileWrapper below

        // Helper
        static decimal Raw(YahooRawValue? v) => v?.Raw ?? 0m;
        static long RawL(YahooRawValue? v) => (long)(v?.Raw ?? 0m);

        // FCF: prefer financialData.freeCashflow (most reliable); fallback to OCF - |CapEx|
        long ocf  = RawL(cf?.OperatingCashFlow);
        long capex = Math.Abs(RawL(cf?.CapitalExpenditures));  // Yahoo reports CapEx as negative
        long fcf  = RawL(fd?.FreeCashflow);
        if (fcf == 0 && ocf != 0) fcf = ocf - capex;

        long marketCap = RawL(sd?.MarketCap);
        long ev        = RawL(ks?.EnterpriseValue);

        // Sector/Industry live in assetProfile which we parse from the raw JSON separately;
        // not mapped here since the model doesn't have a typed assetProfile on YahooQuoteSummaryModules.
        // Set it to empty string; caller can enrich via GetSectorAsync if needed.

        return new FundamentalsSnapshot
        {
            Symbol             = symbol,
            Sector             = "",
            Industry           = "",
            MarketCap          = marketCap,
            EnterpriseValue    = ev,
            Week52High         = Raw(sd?.FiftyTwoWeekHigh),
            Week52Low          = Raw(sd?.FiftyTwoWeekLow),
            TrailingPE         = Raw(sd?.TrailingPE != null ? sd.TrailingPE : ks?.TrailingPE),
            ForwardPE          = Raw(sd?.ForwardPE  != null ? sd.ForwardPE  : ks?.ForwardPE),
            PriceToBook        = Raw(sd?.PriceToBook != null ? sd.PriceToBook : ks?.PriceToBook),
            TrailingEps        = Raw(ks?.TrailingEps),
            ForwardEps         = Raw(ks?.ForwardEps),
            BookValuePerShare  = Raw(ks?.BookValue),
            ReturnOnAssets     = Raw(fd?.ReturnOnAssets != null ? fd.ReturnOnAssets : ks?.ReturnOnAssets),
            ReturnOnEquity     = Raw(fd?.ReturnOnEquity != null ? fd.ReturnOnEquity : ks?.ReturnOnEquity),
            DebtToEquity       = Raw(fd?.DebtToEquity),
            CurrentRatio       = Raw(fd?.CurrentRatio),
            ProfitMargins      = Raw(fd?.GrossProfits) > 0 && Raw(fd?.TotalRevenue) > 0
                                    ? Raw(fd.GrossProfits) / Raw(fd.TotalRevenue) : Raw(ks?.ProfitMargins),
            RevenueGrowth      = Raw(fd?.RevenueGrowth),
            DividendYield      = Raw(sd?.DividendYield) > 0 ? Raw(sd.DividendYield)
                                   : Raw(sd?.TrailingAnnualDividendYield),
            TrailingAnnualDividendYield = Raw(sd?.TrailingAnnualDividendYield),
            TargetMeanPrice    = Raw(fd?.TargetMeanPrice),
            AnalystCount       = (int)(fd?.NumberOfAnalystOpinions?.Raw ?? 0m),
            OperatingCashFlow  = ocf,
            CapitalExpenditures = capex,
            FreeCashFlow       = fcf,
            TotalRevenue       = RawL(inc?.TotalRevenue) > 0 ? RawL(inc.TotalRevenue) : RawL(fd?.TotalRevenue),
            NetIncome          = RawL(inc?.NetIncome),
            Ebit               = RawL(inc?.Ebit),
            TotalAssets        = RawL(bs?.TotalAssets),
            TotalLiabilities   = RawL(bs?.TotalLiabilities),
            StockholdersEquity = RawL(bs?.StockholdersEquity),
            LongTermDebt       = RawL(bs?.LongTermDebt),
            ShortTermDebt      = RawL(bs?.ShortTermDebt),
            TotalCurrentAssets = RawL(bs?.TotalCurrentAssets),
            TotalCurrentLiabilities = RawL(bs?.TotalCurrentLiabilities),
            Cash               = RawL(bs?.Cash),
            NetReceivables     = RawL(bs?.NetReceivables),
            DataAvailable      = true
        };
    }
}

