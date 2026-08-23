namespace PortfolioManager.Api.Models;

/// <summary>Per-user snapshot of the latest portfolio quotes. UserId is the PK — one row per user.</summary>
public class PortfolioSnapshot
{
    public string UserId { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int ItemCount { get; set; }
}

/// <summary>Per-user snapshot of the latest watchlist quotes. UserId is the PK — one row per user.</summary>
public class WatchlistSnapshot
{
    public string UserId { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int ItemCount { get; set; }
}
