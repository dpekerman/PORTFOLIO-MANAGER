namespace PortfolioManager.Api.Models;

public class PortfolioItem
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal AverageCostBasis { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
