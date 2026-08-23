using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IWatchlistSnapshotService
{
    Task SaveAsync(string userId, IReadOnlyList<WatchlistSummaryDto> data, CancellationToken ct = default);
    Task<IReadOnlyList<WatchlistSummaryDto>?> GetLatestAsync(string userId, CancellationToken ct = default);
}

public class WatchlistSnapshotService(AppDbContext db, ILogger<WatchlistSnapshotService> logger) : IWatchlistSnapshotService
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task SaveAsync(string userId, IReadOnlyList<WatchlistSummaryDto> data, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(data, _json);
        var existing = await db.WatchlistSnapshots.FindAsync([userId], ct);
        if (existing is null)
        {
            db.WatchlistSnapshots.Add(new WatchlistSnapshot
            {
                UserId = userId,
                SnapshotJson = json,
                UpdatedAt = DateTime.UtcNow,
                ItemCount = data.Count,
            });
        }
        else
        {
            existing.SnapshotJson = json;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.ItemCount = data.Count;
        }
        await db.SaveChangesAsync(ct);
        logger.LogDebug("[WatchlistSnapshot] Saved {Count} items.", data.Count);
    }

    public async Task<IReadOnlyList<WatchlistSummaryDto>?> GetLatestAsync(string userId, CancellationToken ct = default)
    {
        var row = await db.WatchlistSnapshots.FindAsync([userId], ct);
        if (row is null) return null;
        try
        {
            return JsonSerializer.Deserialize<List<WatchlistSummaryDto>>(row.SnapshotJson, _json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[WatchlistSnapshot] Failed to deserialize snapshot.");
            return null;
        }
    }
}
