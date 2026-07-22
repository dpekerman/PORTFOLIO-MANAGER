namespace PortfolioManager.Api.Models;

public class WatchlistItem
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Investment role: Core | Strategic | Swing | Speculative. Default: Strategic.</summary>
    public string Role { get; set; } = "Strategic";
    /// <summary>Whether this symbol is marked as a favourite for quick filtering.</summary>
    public bool IsFavorite { get; set; } = false;
}
