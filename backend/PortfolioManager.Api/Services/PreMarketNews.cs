using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface INewsProvider
{
    Task<NewsLookupResult> GetCompanyNewsAsync(string symbol, DateOnly asOf, CancellationToken ct = default);
    Task<NewsLookupResult> GetGlobalNewsAsync(DateOnly asOf, CancellationToken ct = default);
    /// <summary>Bounded search via an approved DISCOVERY source. Results are evidence for this run only; the publishers are not trusted permanently.</summary>
    Task<NewsLookupResult> DiscoverAsync(string query, DateOnly asOf, CancellationToken ct = default);
}

public sealed record ParsedArticle(string Headline, string Url, DateTime PublishedUtc, string Summary);

public static class FeedParser
{
    private static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex Space = new(@"\s+", RegexOptions.Compiled);

    public static string Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = Space.Replace(Tags.Replace(WebUtility.HtmlDecode(s), " "), " ").Trim();
        return t.Length <= max ? t : t[..max];
    }

    /// <summary>Parses RSS 2.0 or Atom. DTDs and external entities are disabled.</summary>
    public static List<ParsedArticle> Parse(string xml, int maxItems)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersFromEntities = 0 };
        using var reader = XmlReader.Create(new StringReader(xml.TrimStart('\uFEFF', ' ', '\r', '\n', '\t')), settings);
        var doc = XDocument.Load(reader);
        var result = new List<ParsedArticle>();
        foreach (var e in doc.Descendants().Where(x => x.Name.LocalName is "item" or "entry"))
        {
            string? V(string n) => e.Elements().FirstOrDefault(x => x.Name.LocalName == n)?.Value;
            var title = Clean(V("title"), 300);
            var link = V("link");
            if (string.IsNullOrWhiteSpace(link))
                link = e.Elements().FirstOrDefault(x => x.Name.LocalName == "link")?.Attribute("href")?.Value;
            link = link?.Trim();
            if (title.Length == 0 || string.IsNullOrEmpty(link) || !Uri.TryCreate(link, UriKind.Absolute, out var u)
                || u.Scheme is not ("http" or "https")) continue;
            var dateText = V("pubDate") ?? V("published") ?? V("updated") ?? V("date");
            var published = DateTime.TryParse(dateText, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var d)
                ? d : DateTime.MinValue;
            result.Add(new ParsedArticle(title, u.ToString(), published, Clean(V("description") ?? V("summary"), 400)));
            if (result.Count >= maxItems) break;
        }
        return result;
    }

    public static string Fingerprint(string url)
    {
        var u = Uri.TryCreate(url, UriKind.Absolute, out var p) ? p.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant() : url.ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(u)));
    }
}

public sealed class RssNewsProvider(AppDbContext db, SafeHttpFetcher fetcher, Microsoft.Extensions.Options.IOptions<EmailSettings> email, ILogger<RssNewsProvider> logger) : INewsProvider
{
    private static readonly SemaphoreSlim Throttle = new(1, 1);

    public static readonly (string Name, string Url, string Category, int Priority, string Markets, bool Approved)[] Defaults =
    [
        ("Yahoo Finance company headlines", "https://feeds.finance.yahoo.com/rss/2.0/headline?s={symbol}&region=US&lang=en-US", "COMPANY", 10, "TSX,US", true),
        ("SEC EDGAR 8-K filings (US)", "https://www.sec.gov/cgi-bin/browse-edgar?action=getcompany&CIK={symbol}&type=8-K&dateb=&owner=include&count=10&output=atom", "COMPANY", 8, "US", true),
        ("BBC News - Business", "https://feeds.bbci.co.uk/news/business/rss.xml", "GLOBAL", 20, "TSX,US", true),
        ("BBC News - World", "https://feeds.bbci.co.uk/news/world/rss.xml", "GLOBAL", 30, "TSX,US", true),
        ("Bank of Canada - press releases", "https://www.bankofcanada.ca/content_type/press-releases/feed/", "OFFICIAL", 5, "TSX,US", true),
        ("US Federal Reserve - press releases", "https://www.federalreserve.gov/feeds/press_all.xml", "OFFICIAL", 5, "TSX,US", true),
        // Free public RSS search. Stays pending/disabled until the user approves it; required for SMART/FULL discovery.
        ("Google News search (discovery)", "https://news.google.com/rss/search?q={query}&hl=en-CA&gl=CA&ceid=CA:en", "DISCOVERY", 90, "TSX,US", false),
    ];

    /// <summary>Adds any default source that is missing (by name); existing rows are never changed.</summary>
    public async Task<int> SeedDefaultsAsync(CancellationToken ct)
    {
        var have = (await db.NewsSources.Select(s => s.Name).ToListAsync(ct)).ToHashSet();
        foreach (var d in Defaults.Where(d => !have.Contains(d.Name)))
            db.NewsSources.Add(new NewsSourceConfiguration
            {
                Name = d.Name, Url = d.Url, Category = d.Category, Priority = d.Priority, MaxItems = 12, Markets = d.Markets,
                ApprovalStatus = d.Approved ? "APPROVED" : "PENDING", Enabled = d.Approved,
            });
        return await db.SaveChangesAsync(ct);
    }

    private string? UserAgentFor(string url) =>
        url.Contains("sec.gov", StringComparison.OrdinalIgnoreCase)
            ? $"PortfolioManager/1.0 (personal research; {(string.IsNullOrWhiteSpace(email.Value.Username) ? "contact unavailable" : email.Value.Username)})"
            : null;

    public async Task<NewsLookupResult> DiscoverAsync(string query, DateOnly asOf, CancellationToken ct = default)
    {
        await SeedDefaultsAsync(ct);
        var s = (await Active("DISCOVERY", ct)).FirstOrDefault();
        if (s is null) return new("COVERAGE_INCOMPLETE", [], "No approved discovery source is enabled.");
        var (from, to) = Window(asOf);
        var url = s.Url.Replace("{query}", Uri.EscapeDataString(query), StringComparison.OrdinalIgnoreCase);
        var (ok, list, err) = await PullAsync(s, url, ct);
        if (!ok) return new("SOURCE_UNAVAILABLE", [], err);
        var items = new List<NewsItemDto>();
        foreach (var a in list.Where(a => a.PublishedUtc >= from && a.PublishedUtc <= to).Take(5))
            items.Add(await SaveAsync(a with { Headline = "[discovered] " + a.Headline }, s, null, "DISCOVERY", ct));
        return items.Count > 0 ? new("NEWS_FOUND", Dedupe(items)) : new("VERIFIED_NO_MATERIAL_NEWS", []);
    }

    private static (DateTime From, DateTime To) Window(DateOnly asOf)
    {
        var to = asOf.ToDateTime(new TimeOnly(13, 0), DateTimeKind.Utc);
        return (to.AddHours(-72), to);
    }

    public async Task<NewsLookupResult> GetCompanyNewsAsync(string symbol, DateOnly asOf, CancellationToken ct = default)
    {
        await SeedDefaultsAsync(ct);
        var sources = (await Active("COMPANY", ct)).Where(s => s.Markets.Contains(TradingCalendar.ExchangeOf(symbol) == MarketExchange.Tsx ? "TSX" : "US", StringComparison.OrdinalIgnoreCase)).ToList();
        if (sources.Count == 0) return new("COVERAGE_INCOMPLETE", [], "No approved company news source is enabled for this market.");
        var (from, to) = Window(asOf);
        var items = new List<NewsItemDto>();
        var okSources = 0;
        string? lastError = null;
        foreach (var s in sources)
        {
            var isSec = s.Url.Contains("sec.gov", StringComparison.OrdinalIgnoreCase);
            var url = s.Url.Replace("{symbol}", Uri.EscapeDataString(isSec ? symbol.Replace('.', '-') : symbol), StringComparison.OrdinalIgnoreCase);
            var (ok, list, err) = await PullAsync(s, url, ct);
            if (!ok) { lastError = err; continue; }
            okSources++;
            foreach (var a in list.Where(a => a.PublishedUtc >= from && a.PublishedUtc <= to))
                items.Add(await SaveAsync(isSec ? a with { Headline = "[SEC] " + a.Headline } : a, s, symbol, "COMPANY", ct));
        }
        if (items.Count > 0) return new("NEWS_FOUND", Dedupe(items));
        return okSources > 0
            ? new("VERIFIED_NO_MATERIAL_NEWS", [], "Checked approved sources only; free coverage (especially TSX/TSXV) may be incomplete.")
            : new("SOURCE_UNAVAILABLE", [], lastError);
    }

    public async Task<NewsLookupResult> GetGlobalNewsAsync(DateOnly asOf, CancellationToken ct = default)
    {
        await SeedDefaultsAsync(ct);
        var sources = (await Active("GLOBAL", ct)).Concat(await Active("OFFICIAL", ct)).ToList();
        if (sources.Count == 0) return new("COVERAGE_INCOMPLETE", [], "No approved global/official source is enabled.");
        var (from, to) = Window(asOf);
        var items = new List<NewsItemDto>();
        var okSources = 0; var failed = new List<string>();
        foreach (var s in sources)
        {
            var (ok, list, err) = await PullAsync(s, s.Url, ct);
            if (!ok) { failed.Add($"{s.Name}: {err}"); continue; }
            okSources++;
            foreach (var a in list.Where(a => a.PublishedUtc >= from && a.PublishedUtc <= to))
                items.Add(await SaveAsync(a, s, null, s.Category, ct));
        }
        var note = failed.Count > 0 ? "Unavailable: " + string.Join("; ", failed) : null;
        if (items.Count > 0) return new("NEWS_FOUND", Dedupe(items), note);
        return okSources > 0 ? new("VERIFIED_NO_MATERIAL_NEWS", [], note) : new("SOURCE_UNAVAILABLE", [], note);
    }

    private static List<NewsItemDto> Dedupe(List<NewsItemDto> items) =>
        items.GroupBy(i => i.EvidenceId).Select(g => g.First()).OrderByDescending(i => i.PublishedUtc).ToList();

    private Task<List<NewsSourceConfiguration>> Active(string category, CancellationToken ct) =>
        db.NewsSources.Where(s => s.Enabled && !s.IsArchived && s.ApprovalStatus == "APPROVED" && s.Category == category)
            .OrderBy(s => s.Priority).ToListAsync(ct);

    private async Task<(bool Ok, List<ParsedArticle> Items, string? Error)> PullAsync(NewsSourceConfiguration s, string url, CancellationToken ct)
    {
        await Throttle.WaitAsync(ct);
        try
        {
            var r = await fetcher.FetchAsync(url, s.TimeoutSeconds, ct, UserAgentFor(url));
            await Task.Delay(300, ct); // keep the polite pacing used by the scanner
            s.AvgDurationMs = s.AvgDurationMs == 0 ? r.DurationMs : (s.AvgDurationMs + r.DurationMs) / 2;
            if (!r.Ok)
            {
                s.LastFailureUtc = DateTime.UtcNow; s.LastError = r.Error;
                await db.SaveChangesAsync(ct);
                return (false, [], r.Error);
            }
            try
            {
                var list = FeedParser.Parse(r.Body!, s.MaxItems);
                s.LastSuccessUtc = DateTime.UtcNow; s.LastError = null;
                await db.SaveChangesAsync(ct);
                return (true, list, null);
            }
            catch (Exception ex)
            {
                s.LastFailureUtc = DateTime.UtcNow; s.LastError = "Feed could not be parsed.";
                logger.LogWarning(ex, "Feed parse failed for {Source}", s.Name);
                await db.SaveChangesAsync(ct);
                return (false, [], s.LastError);
            }
        }
        finally { Throttle.Release(); }
    }

    private async Task<NewsItemDto> SaveAsync(ParsedArticle a, NewsSourceConfiguration s, string? symbol, string category, CancellationToken ct)
    {
        var fp = FeedParser.Fingerprint(a.Url);
        var row = await db.NewsArticles.FirstOrDefaultAsync(x => x.Fingerprint == fp, ct);
        if (row is null)
        {
            row = new NewsArticleReference
            {
                Fingerprint = fp, Url = a.Url, Headline = a.Headline, Publisher = s.Name, PublishedUtc = a.PublishedUtc,
                Symbol = symbol, Category = category, Summary = a.Summary, SourceId = s.Id,
            };
            db.NewsArticles.Add(row);
            s.UsefulArticleCount++;
            await db.SaveChangesAsync(ct);
        }
        return new NewsItemDto($"A{row.Id}", row.Headline, row.Publisher, row.Url, row.PublishedUtc, row.RetrievedUtc, row.Summary, false);
    }
}

/// <summary>Deterministic, clearly labelled MOCK news used only for testing the pipeline.</summary>
public sealed class MockNewsProvider : INewsProvider
{
    public Task<NewsLookupResult> GetCompanyNewsAsync(string symbol, DateOnly asOf, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Only ~1 in 6 symbols gets an "earnings" headline so the demo report is realistic rather than flagging everything.
        var hash = symbol.Aggregate(0, (h, c) => h * 31 + c) & 0x7fffffff;
        if (hash % 6 != 0) return Task.FromResult(new NewsLookupResult("VERIFIED_NO_MATERIAL_NEWS", [], "MOCK: no news generated."));
        var item = new NewsItemDto($"MOCK-{symbol}", $"[MOCK] {symbol} reports quarterly earnings, raises guidance", "Mock Publisher",
            "https://example.com/mock/" + Uri.EscapeDataString(symbol), asOf.ToDateTime(new TimeOnly(6, 0)), now,
            "Sample item to exercise the report pipeline. Not real news.", true);
        return Task.FromResult(new NewsLookupResult("NEWS_FOUND", [item]));
    }

    public Task<NewsLookupResult> DiscoverAsync(string query, DateOnly asOf, CancellationToken ct = default)
    {
        var id = "MOCK-D-" + Math.Abs(query.GetHashCode() % 10000);
        return Task.FromResult(new NewsLookupResult("NEWS_FOUND", [new NewsItemDto(id, $"[MOCK][discovered] {query}", "Mock discovery", "https://example.com/mock/" + id,
            asOf.ToDateTime(new TimeOnly(5, 0)), DateTime.UtcNow, "Sample discovered item. Not real news.", true)]));
    }

    public Task<NewsLookupResult> GetGlobalNewsAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        NewsItemDto N(string id, string h) => new(id, h, "Mock Publisher", "https://example.com/mock/" + id,
            asOf.ToDateTime(new TimeOnly(5, 30)), now, "Sample global item. Not real news.", true);
        return Task.FromResult(new NewsLookupResult("NEWS_FOUND", [
            N("MOCK-G1", "[MOCK] Crude oil jumps as OPEC signals supply cuts amid Middle East conflict"),
            N("MOCK-G2", "[MOCK] Bank of Canada holds interest rate; inflation data due Friday"),
        ]));
    }
}
