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
                           "fiftyTwoWeekHigh,fiftyTwoWeekLow";

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
            var resp = await _http.GetAsync(
                $"https://query1.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(query)}&quotesCount=15&newsCount=0", ct);
            if (!resp.IsSuccessStatusCode) return [];

            var json = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<YahooSearchResponse>(json, _json);

            return data?.Quotes?
                .Where(q => !string.IsNullOrWhiteSpace(q.Symbol))
                .Select(q => new SymbolSearchResult
                {
                    Symbol        = q.Symbol,
                    Description   = q.Longname ?? q.Shortname ?? q.Symbol,
                    Type          = q.TypeDisp ?? "Equity",
                    DisplaySymbol = q.Symbol
                })
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yahoo Finance SearchSymbolAsync failed for query {Query}", query);
            return [];
        }
    }

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
            Timestamp     = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
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
}

