using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Manages the curated lists of sectors, industries, and decision sources,
/// persisted in the SectorIndustryConfigs table (single row, Id = 1) so they
/// are covered by the normal DB backup/migration process instead of a loose file.
/// </summary>
public class SectorIndustryService(AppDbContext db)
{
    private static readonly List<string> DefaultSectors =
    [
        "Communication Services", "Consumer Discretionary", "Consumer Staples", "Energy",
        "ETFs & Funds", "Financial Services", "Healthcare", "Industrials",
        "Information Technology", "Materials", "Real Estate", "Technology", "Utilities"
    ];

    private static readonly List<string> DefaultIndustries =
    [
        "Airlines", "Asset Management", "Banks – Diversified", "Banks – Regional", "Biotechnology",
        "Broadcasting", "Capital Markets", "Chemicals", "Communication Equipment",
        "Consumer Electronics", "Drug Manufacturers", "Electric Utilities", "Electronic Components",
        "ETFs & Funds", "Food Distribution", "Gold", "Healthcare Plans",
        "Independent Power Producers", "Information Technology Services", "Insurance – Diversified",
        "Integrated Freight & Logistics", "Internet Content & Information", "Medical Devices",
        "Medical Instruments & Supplies", "Oil & Gas Exploration & Production",
        "Oil & Gas Integrated", "Oil & Gas Midstream", "Oil & Gas Refining & Marketing",
        "Packaged Foods", "Pharmaceuticals", "REIT – Diversified", "REIT – Industrial",
        "REIT – Office", "REIT – Retail", "Semiconductors", "Software – Application",
        "Software – Infrastructure", "Specialty Chemicals", "Telecom Services",
        "Utilities – Regulated Electric", "Utilities – Regulated Gas"
    ];

    private static List<string> DefaultDecisionSources() =>
        ["App Signal", "App Signal - Add", "App Signal - RSI Oversold", "App Signal - Trim",
         "Bought Deal", "Catalyst", "Legacy", "Loss Harvest", "Manual", "Manual - Buy on pullback",
         "Rebalance", "Risk Control", "Risk Control - all out", "Risk Control - Trim"];

    /// <summary>Loads the single config row, seeding it with defaults on first run.</summary>
    private async Task<SectorIndustryConfig> LoadAsync(CancellationToken ct)
    {
        var row = await db.SectorIndustryConfigs.FirstOrDefaultAsync(r => r.Id == 1, ct);
        if (row is not null) return row;

        row = new SectorIndustryConfig
        {
            Id = 1,
            SectorsJson = JsonSerializer.Serialize(DefaultSectors),
            IndustriesJson = JsonSerializer.Serialize(DefaultIndustries),
            DecisionSourcesJson = JsonSerializer.Serialize(DefaultDecisionSources()),
        };
        db.SectorIndustryConfigs.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    private static List<string> ParseOrEmpty(string json) =>
        JsonSerializer.Deserialize<List<string>>(json) ?? [];

    public async Task<SectorIndustryListsDto> GetListsAsync(CancellationToken ct = default)
    {
        var row = await LoadAsync(ct);
        var sectors = ParseOrEmpty(row.SectorsJson);
        var industries = ParseOrEmpty(row.IndustriesJson);
        var decisionSources = ParseOrEmpty(row.DecisionSourcesJson);
        return new(
            sectors.Count > 0 ? sectors.OrderBy(s => s).ToList() : DefaultSectors,
            industries.Count > 0 ? industries.OrderBy(i => i).ToList() : DefaultIndustries,
            decisionSources.Count > 0 ? decisionSources : DefaultDecisionSources());
    }

    public async Task<DecisionSourcesDto> GetDecisionSourcesAsync(CancellationToken ct = default)
    {
        var row = await LoadAsync(ct);
        var items = ParseOrEmpty(row.DecisionSourcesJson);
        return new(items.Count > 0 ? items : DefaultDecisionSources());
    }

    /// <summary>Replaces the Decision Source list and persists to the DB.</summary>
    public async Task<DecisionSourcesDto> SaveDecisionSourcesAsync(
        UpdateDecisionSourcesRequest request, CancellationToken ct = default)
    {
        var items = request.Items
            .Select(d => d.Trim()).Where(d => d.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var row = await LoadAsync(ct);
        row.DecisionSourcesJson = JsonSerializer.Serialize(items);
        await db.SaveChangesAsync(ct);
        return new(items);
    }

    public async Task SaveListsAsync(UpdateSectorIndustryListsRequest request, CancellationToken ct = default)
    {
        var row = await LoadAsync(ct);

        row.SectorsJson = JsonSerializer.Serialize(
            request.Sectors.Select(s => s.Trim()).Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList());

        row.IndustriesJson = JsonSerializer.Serialize(
            request.Industries.Select(i => i.Trim()).Where(i => i.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(i => i).ToList());

        if (request.DecisionSources is not null)
        {
            row.DecisionSourcesJson = JsonSerializer.Serialize(
                request.DecisionSources.Select(d => d.Trim()).Where(d => d.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        await db.SaveChangesAsync(ct);
    }
}

