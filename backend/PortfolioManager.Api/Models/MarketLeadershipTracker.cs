namespace PortfolioManager.Api.Models;

public enum MarketLeadershipTrackerType
{
    ETF,
    Theme,
    Future,
    Commodity,
    SectorProxy,
    Other,
}

public sealed class MarketLeadershipTracker
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public MarketLeadershipTrackerType TrackerType { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}