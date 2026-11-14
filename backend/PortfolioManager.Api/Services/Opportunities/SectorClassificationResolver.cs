using System.Text.Json;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Opportunities;

public sealed record SectorClassification(string? Sector, string? Industry, string Source)
{
    public bool IsClassified => !string.IsNullOrWhiteSpace(Sector);
}

/// <summary>
/// Sector/industry precedence for an opportunity row: user-overridden PortfolioItem sector → PortfolioItem sector → market
/// provider (scanner/quote) sector → Unclassified. Records which source was used. "ETFs &amp; Funds" is not a sector.
/// Benchmark lookup maps the provider vocabulary (Yahoo / GICS) through each mapping's ProviderSectorAliasesJson.
/// </summary>
public static class SectorClassificationResolver
{
    public const string SourceOverride = "PortfolioOverride";
    public const string SourcePortfolio = "Portfolio";
    public const string SourceProvider = "MarketData";
    public const string SourceNone = "Unclassified";

    private static readonly HashSet<string> NotASector = new(StringComparer.OrdinalIgnoreCase) { "ETFs & Funds", "ETF", "Fund", "N/A", "Unknown" };

    public static SectorClassification Resolve(IEnumerable<PortfolioItem> lots, string? providerSector, string? providerIndustry) =>
        Resolve(lots.Select(l => (l.Sector, l.Industry, l.SectorIsOverridden)), providerSector, providerIndustry);

    public static SectorClassification Resolve(IEnumerable<PortfolioItemDto> lots, string? providerSector, string? providerIndustry) =>
        Resolve(lots.Select(l => (l.Sector, l.Industry, l.SectorIsOverridden)), providerSector, providerIndustry);

    private static SectorClassification Resolve(
        IEnumerable<(string Sector, string Industry, bool Overridden)> lots, string? providerSector, string? providerIndustry)
    {
        var list = lots.ToList();
        var overridden = list.FirstOrDefault(l => l.Overridden && Usable(l.Sector));
        if (overridden.Sector is not null) return new(overridden.Sector.Trim(), NullIfBlank(overridden.Industry), SourceOverride);
        var portfolio = list.FirstOrDefault(l => Usable(l.Sector));
        if (portfolio.Sector is not null) return new(portfolio.Sector.Trim(), NullIfBlank(portfolio.Industry), SourcePortfolio);
        if (Usable(providerSector)) return new(providerSector!.Trim(), NullIfBlank(providerIndustry), SourceProvider);
        return new(null, NullIfBlank(providerIndustry), SourceNone);
    }

    /// <summary>Active mapping for (sector, market) matching the canonical name or any provider alias; null ⇒ Unmapped.</summary>
    public static SectorBenchmarkMapping? FindBenchmark(IEnumerable<SectorBenchmarkMapping> mappings, string? sector, MarketExchange market)
    {
        if (!Usable(sector)) return null;
        var marketKey = market.ToString();
        var s = sector!.Trim();
        return mappings
            .Where(m => m.IsActive && string.Equals(m.Market, marketKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Id)
            .FirstOrDefault(m => string.Equals(m.Sector, s, StringComparison.OrdinalIgnoreCase)
                || Aliases(m).Contains(s, StringComparer.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> Aliases(SectorBenchmarkMapping m)
    {
        try { return JsonSerializer.Deserialize<List<string>>(m.ProviderSectorAliasesJson ?? "[]") ?? []; }
        catch (JsonException) { return []; }
    }

    private static bool Usable(string? sector) => !string.IsNullOrWhiteSpace(sector) && !NotASector.Contains(sector.Trim());

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
