namespace PortfolioManager.Api.Models;

public class CashItem
{
    public int Id { get; set; }
    public string Description { get; set; } = "CASH";
    public decimal Amount { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
