namespace PortfolioManager.Api.Models;

/// <summary>Single-row table holding the curated Sector/Industry/Decision-Source picklists (JSON-encoded).</summary>
public class SectorIndustryConfig
{
    public int Id { get; set; }
    public string SectorsJson { get; set; } = "[]";
    public string IndustriesJson { get; set; } = "[]";
    public string DecisionSourcesJson { get; set; } = "[]";
}
