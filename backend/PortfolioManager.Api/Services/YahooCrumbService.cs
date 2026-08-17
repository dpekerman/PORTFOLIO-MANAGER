using System.Net;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Fetches and caches the Yahoo Finance session crumb + cookies required by
/// the v7 (batch quote) and v10 (quoteSummary) endpoints.
///
/// Yahoo Finance added a crumb requirement. Without it the API returns 401 Unauthorized.
/// The crumb is a short string obtained after visiting finance.yahoo.com and exchanging
/// the session cookies for a crumb via /v1/test/getcrumb.
/// </summary>
public sealed class YahooCrumbService
{
    private readonly ILogger<YahooCrumbService> _logger;
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private string? _crumb;
    private string? _cookieHeader;
    private DateTime _expiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public YahooCrumbService(ILogger<YahooCrumbService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns a valid (crumb, cookieHeader) pair, refreshing if expired or missing.
    /// </summary>
    public async Task<(string crumb, string cookieHeader)> GetAsync(CancellationToken ct = default)
    {
        if (DateTime.UtcNow < _expiresAt && _crumb is not null)
            return (_crumb, _cookieHeader!);

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (DateTime.UtcNow < _expiresAt && _crumb is not null)
                return (_crumb, _cookieHeader!);

            await RefreshAsync(ct);
            return (_crumb!, _cookieHeader!);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Call this when a 401/Unauthorized is received so the crumb is re-fetched on next use.
    /// </summary>
    public void Invalidate() => _expiresAt = DateTime.MinValue;

    private async Task RefreshAsync(CancellationToken ct)
    {
        _logger.LogInformation("Refreshing Yahoo Finance crumb...");

        var jar     = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer  = jar,
            UseCookies       = true,
            AllowAutoRedirect = true,
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");

        // Step 1: visit a Yahoo Finance page so Yahoo sets the A1/A3 session cookies
        try
        {
            await client.GetAsync("https://finance.yahoo.com/quote/SPY/", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reach finance.yahoo.com for cookie seeding");
        }

        // Step 2: exchange cookies for a crumb
        var crumb = await client.GetStringAsync(
            "https://query2.finance.yahoo.com/v1/test/getcrumb", ct);

        if (string.IsNullOrWhiteSpace(crumb) || crumb.Contains("Unauthorized"))
            throw new InvalidOperationException($"Yahoo Finance crumb fetch failed: {crumb}");

        // Step 3: extract cookie header to attach to subsequent requests
        var cookies = jar.GetCookieHeader(new Uri("https://query2.finance.yahoo.com/"));
        if (string.IsNullOrWhiteSpace(cookies))
            cookies = jar.GetCookieHeader(new Uri("https://finance.yahoo.com/"));

        _crumb       = crumb.Trim();
        _cookieHeader = cookies;
        _expiresAt   = DateTime.UtcNow.AddMinutes(55); // Yahoo crumbs last ~1 hour
        _logger.LogInformation("Yahoo Finance crumb acquired (expires {At})", _expiresAt);
    }
}
