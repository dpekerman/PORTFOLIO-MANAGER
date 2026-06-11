using System.Text.Json;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IFinnhubService
{
    Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default);
    Task<IReadOnlyList<FinnhubSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default);
}

public sealed class FinnhubService : IFinnhubService
{
    private readonly HttpClient _http;
    private readonly ILogger<FinnhubService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FinnhubService(HttpClient http, ILogger<FinnhubService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<StockQuote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"quote?symbol={Uri.EscapeDataString(symbol)}", ct);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Finnhub rate limit hit for quote {Symbol}. Backing off 2s.", symbol);
                await Task.Delay(2000, ct);
                response = await _http.GetAsync($"quote?symbol={Uri.EscapeDataString(symbol)}", ct);
            }

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<FinnhubQuoteResponse>(json, _jsonOptions);

            if (data is null || data.T == 0)
                return null;

            return new StockQuote
            {
                Symbol = symbol.ToUpperInvariant(),
                CurrentPrice = data.C,
                Change = data.D,
                ChangePercent = data.Dp,
                HighPrice = data.H,
                LowPrice = data.L,
                OpenPrice = data.O,
                PreviousClose = data.Pc,
                Timestamp = data.T
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch quote for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IReadOnlyList<FinnhubSearchResult>> SearchSymbolAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"search?q={Uri.EscapeDataString(query)}", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<FinnhubSearchResponse>(json, _jsonOptions);
            return data?.Result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search symbols for query {Query}", query);
            return [];
        }
    }
}
