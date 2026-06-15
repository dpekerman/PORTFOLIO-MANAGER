using System.Text.Json;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Singleton that manages the curated lists of sectors and industries,
/// persisting them to a JSON file so they survive application restarts.
/// </summary>
public class SectorIndustryService
{
    private readonly string _filePath;
    private readonly ILogger<SectorIndustryService> _logger;
    private readonly object _lock = new();
    private SectorIndustryListsDto _lists;

    private static readonly SectorIndustryListsDto _defaults = new(
        Sectors:
        [
            "Communication Services",
            "Consumer Discretionary",
            "Consumer Staples",
            "Energy",
            "ETFs & Funds",
            "Financial Services",
            "Healthcare",
            "Industrials",
            "Information Technology",
            "Materials",
            "Real Estate",
            "Technology",
            "Utilities"
        ],
        Industries:
        [
            "Airlines",
            "Asset Management",
            "Banks – Diversified",
            "Banks – Regional",
            "Biotechnology",
            "Broadcasting",
            "Capital Markets",
            "Chemicals",
            "Communication Equipment",
            "Consumer Electronics",
            "Drug Manufacturers",
            "Electric Utilities",
            "Electronic Components",
            "ETFs & Funds",
            "Food Distribution",
            "Gold",
            "Healthcare Plans",
            "Independent Power Producers",
            "Information Technology Services",
            "Insurance – Diversified",
            "Integrated Freight & Logistics",
            "Internet Content & Information",
            "Medical Devices",
            "Medical Instruments & Supplies",
            "Oil & Gas Exploration & Production",
            "Oil & Gas Integrated",
            "Oil & Gas Midstream",
            "Oil & Gas Refining & Marketing",
            "Packaged Foods",
            "Pharmaceuticals",
            "REIT – Diversified",
            "REIT – Industrial",
            "REIT – Office",
            "REIT – Retail",
            "Semiconductors",
            "Software – Application",
            "Software – Infrastructure",
            "Specialty Chemicals",
            "Telecom Services",
            "Utilities – Regulated Electric",
            "Utilities – Regulated Gas"
        ]
    );

    public SectorIndustryService(ILogger<SectorIndustryService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _filePath = Path.Combine(env.ContentRootPath, "sector-industry-lists.json");
        _lists = Load();
    }

    public SectorIndustryListsDto GetLists()
    {
        lock (_lock) return new(_lists.Sectors.OrderBy(s => s).ToList(), _lists.Industries.OrderBy(i => i).ToList());
    }

    public void SaveLists(UpdateSectorIndustryListsRequest request)
    {
        var updated = new SectorIndustryListsDto(
            Sectors: request.Sectors.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList(),
            Industries: request.Industries.Select(i => i.Trim()).Where(i => i.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(i => i).ToList()
        );

        lock (_lock)
        {
            _lists = updated;
            Persist();
        }
    }

    private SectorIndustryListsDto Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var dto = JsonSerializer.Deserialize<SectorIndustryListsDto>(json);
                if (dto is not null)
                {
                    _logger.LogInformation("Loaded {S} sectors and {I} industries from file.", dto.Sectors.Count, dto.Industries.Count);
                    return dto;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load sector-industry-lists.json — using defaults.");
        }

        // Persist defaults on first run
        _lists = _defaults;
        Persist();
        return _defaults;
    }

    private void Persist()
    {
        try
        {
            var json = JsonSerializer.Serialize(_lists, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save sector-industry-lists.json.");
        }
    }
}
