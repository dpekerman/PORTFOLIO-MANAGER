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
}
