namespace PortfolioManager.Api.Models;

public class CashItem
{
    public int Id { get; set; }
    public string Description { get; set; } = "CASH";
    public decimal Amount { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Account type e.g. TFSA_L_RBC, Corp_TD</summary>
    public string? AccountType { get; set; }
}
