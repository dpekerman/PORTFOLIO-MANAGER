namespace PortfolioManager.Api.Models;

public class OptionItem
{
    public int Id { get; set; }
    public string UnderlyingTicker { get; set; } = string.Empty;
    /// <summary>CALL or PUT</summary>
    public string PositionType { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public decimal Strike { get; set; }
    public decimal Premium { get; set; }
    public int NumberOfContracts { get; set; }
    public decimal MarketPrice { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    // ── Transaction tracking fields ─────────────────────────────────────────
    /// <summary>OPEN or CLOSE</summary>
    public string? TransactionType { get; set; }
    /// <summary>Account type e.g. TFSA_L_RBC, Margin_D_TD, Corp_TD</summary>
    public string? AccountType { get; set; }
    public DateTime? OpenDate { get; set; }
    public DateTime? CloseDate { get; set; }
    public decimal? ClosingPrice { get; set; }
    /// <summary>Free-text notes stored per transaction record. Not shown in main grid.</summary>
    public string? Notes { get; set; }
}
