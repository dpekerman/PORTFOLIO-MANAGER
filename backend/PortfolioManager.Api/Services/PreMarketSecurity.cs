using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PortfolioManager.Api.Services;

/// <summary>SSRF protection for admin-configured news URLs.</summary>
public static class UrlGuard
{
    public static bool TryValidate(string? url, out Uri? uri, out string error)
    {
        uri = null; error = "";
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048) { error = "URL is empty or too long."; return false; }
        // {symbol} is a template placeholder, replaced before any request.
        var probe = url.Replace("{symbol}", "AAA", StringComparison.OrdinalIgnoreCase).Replace("{query}", "aaa", StringComparison.OrdinalIgnoreCase);
        if (!Uri.TryCreate(probe, UriKind.Absolute, out var u)) { error = "Not a valid absolute URL."; return false; }
        if (u.Scheme != Uri.UriSchemeHttps) { error = "Only https URLs are allowed."; return false; }
        if (!string.IsNullOrEmpty(u.UserInfo)) { error = "Credentials in URLs are not allowed."; return false; }
        if (u.Port != 443) { error = "Only the default https port is allowed."; return false; }
        var host = u.Host.ToLowerInvariant();
        if (host == "localhost" || host.EndsWith(".local") || host.EndsWith(".internal") || !host.Contains('.') && !IPAddress.TryParse(host, out _))
        { error = "Local/internal host names are not allowed."; return false; }
        if (IPAddress.TryParse(u.Host.Trim('[', ']'), out var ip) && IsPrivate(ip))
        { error = "Private or loopback addresses are not allowed."; return false; }
        uri = u;
        return true;
    }

    public static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
        var b = ip.GetAddressBytes();
        return b[0] == 10 || b[0] == 127 || b[0] == 0
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] is >= 64 and <= 127)
            || b[0] >= 224;
    }
}

public sealed record FetchResult(bool Ok, string? Body, int StatusCode, int DurationMs, string? Error);

/// <summary>
/// HTTP fetcher for news feeds: https only, DNS result validated at connect time (blocks DNS rebinding),
/// no proxy, redirects re-validated manually, size and time capped.
/// </summary>
public sealed class SafeHttpFetcher
{
    public const int MaxBytes = 1_000_000;
    private const int MaxRedirects = 3;
    private static readonly HttpClient Client = Create();

    private static HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(8),
            ConnectCallback = async (ctx, ct) =>
            {
                var addrs = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct);
                var safe = addrs.Where(a => !UrlGuard.IsPrivate(a)).ToArray();
                if (safe.Length == 0) throw new HttpRequestException("Blocked: host resolves to a private address.");
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(safe[0], ctx.DnsEndPoint.Port, ct);
                    return new NetworkStream(socket, true);
                }
                catch { socket.Dispose(); throw; }
            },
        };
        var c = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("PortfolioManager-NewsReader/1.0");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml;q=0.9");
        return c;
    }

    public async Task<FetchResult> FetchAsync(string url, int timeoutSeconds, CancellationToken ct = default, string? userAgent = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 2, 30)));
            var current = url;
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                if (!UrlGuard.TryValidate(current, out var uri, out var err))
                    return new(false, null, 0, (int)sw.ElapsedMilliseconds, err);
                using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                if (userAgent is not null) { req.Headers.UserAgent.Clear(); req.Headers.UserAgent.ParseAdd(userAgent); }
                using var resp = await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                var code = (int)resp.StatusCode;
                if (code is 301 or 302 or 303 or 307 or 308 && resp.Headers.Location is { } loc)
                {
                    current = new Uri(uri!, loc).ToString();
                    continue;
                }
                if (!resp.IsSuccessStatusCode)
                    return new(false, null, code, (int)sw.ElapsedMilliseconds, $"HTTP {code}");
                var body = await ReadCappedAsync(resp, cts.Token);
                return body is null
                    ? new(false, null, code, (int)sw.ElapsedMilliseconds, "Response exceeded the size limit.")
                    : new(true, body, code, (int)sw.ElapsedMilliseconds, null);
            }
            return new(false, null, 0, (int)sw.ElapsedMilliseconds, "Too many redirects.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return new(false, null, 0, (int)sw.ElapsedMilliseconds, ex is OperationCanceledException ? "Timed out." : ex.Message);
        }
    }

    private static async Task<string?> ReadCappedAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.Content.Headers.ContentLength > MaxBytes) return null;
        await using var s = await resp.Content.ReadAsStreamAsync(ct);
        var buf = new byte[16 * 1024];
        using var ms = new MemoryStream();
        int n;
        while ((n = await s.ReadAsync(buf, ct)) > 0)
        {
            ms.Write(buf, 0, n);
            if (ms.Length > MaxBytes) return null;
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
