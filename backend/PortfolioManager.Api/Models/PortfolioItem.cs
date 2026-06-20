namespace PortfolioManager.Api.Models;

public class PortfolioItem
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal AverageCostBasis { get; set; }
    public string Sector { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    /// <summary>True when sector/industry were manually set by the user. RefreshSectors skips these items.</summary>
    public bool SectorIsOverridden { get; set; } = false;
    public bool IsManual { get; set; } = false;
    public decimal? ManualMarketValue { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    // ── Transaction tracking fields ─────────────────────────────────────────
    /// <summary>OPEN or CLOSE</summary>
    public string? TransactionType { get; set; }
    /// <summary>Account type e.g. TFSA_L_RBC, Margin_D_TD, Corp_TD</summary>
    public string? AccountType { get; set; }
    public DateTime? OpenDate { get; set; }
    public DateTime? CloseDate { get; set; }
    public decimal? ClosingPrice { get; set; }
}
