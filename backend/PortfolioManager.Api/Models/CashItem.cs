namespace PortfolioManager.Api.Models;

public class CashItem
{
    public int Id { get; set; }
    /// <summary>Owning user — null means legacy/unowned data visible only to Admins.</summary>
    public string? UserId { get; set; }
    public string Description { get; set; } = "CASH";
    public decimal Amount { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Account type e.g. TFSA_L_RBC, Corp_TD</summary>
    public string? AccountType { get; set; }
    /// <summary>Optional user-entered transaction date (separate from system AddedAt).</summary>
    public DateTime? TransactionDate { get; set; }
}
