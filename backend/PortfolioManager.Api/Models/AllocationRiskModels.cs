namespace PortfolioManager.Api.Models;

/// <summary>Target allocation percentage for each holding role (Risk Management).</summary>
public class AllocationRiskTarget
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    /// <summary>Target percentage, e.g. 40 means 40%.</summary>
    public decimal TargetPct { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Target allocation percentage for each market sector.</summary>
public class AllocationSectorTarget
{
    public int Id { get; set; }
    public string Sector { get; set; } = string.Empty;
    public decimal TargetPct { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Maximum single-position size for each holding role.</summary>
public class SinglePositionLimit
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public decimal TargetPct { get; set; }
    public int DisplayOrder { get; set; }
}
