using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IPortfolioSnapshotService
{
    Task SaveAsync(string userId, IReadOnlyList<PortfolioSummaryDto> data, CancellationToken ct = default);
    Task<IReadOnlyList<PortfolioSummaryDto>?> GetLatestAsync(string userId, CancellationToken ct = default);
    Task PatchHoldingRoleAsync(string userId, int itemId, string holdingRole, CancellationToken ct = default);
}

public class PortfolioSnapshotService(AppDbContext db, ILogger<PortfolioSnapshotService> logger) : IPortfolioSnapshotService
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task SaveAsync(string userId, IReadOnlyList<PortfolioSummaryDto> data, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(data, _json);
        var existing = await db.PortfolioSnapshots.FindAsync([userId], ct);
        if (existing is null)
        {
            db.PortfolioSnapshots.Add(new PortfolioSnapshot
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
        logger.LogDebug("[PortfolioSnapshot] Saved {Count} items.", data.Count);
    }

    public async Task<IReadOnlyList<PortfolioSummaryDto>?> GetLatestAsync(string userId, CancellationToken ct = default)
    {
        var row = await db.PortfolioSnapshots.FindAsync([userId], ct);
        if (row is null) return null;
        try
        {
            return JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(row.SnapshotJson, _json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PortfolioSnapshot] Failed to deserialize snapshot.");
            return null;
        }
    }

    // Patches a single item's HoldingRole in the snapshot without a full Yahoo Finance fetch.
    public async Task PatchHoldingRoleAsync(string userId, int itemId, string holdingRole, CancellationToken ct = default)
    {
        var row = await db.PortfolioSnapshots.FindAsync([userId], ct);
        if (row is null) return;
        try
        {
            var items = JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(row.SnapshotJson, _json);
            if (items is null) return;
            var idx = items.FindIndex(s => s.Item.Id == itemId);
            if (idx < 0) return;
            items[idx] = new PortfolioSummaryDto(items[idx].Item with { HoldingRole = holdingRole }, items[idx].Quote, items[idx].PriceStructure);
            row.SnapshotJson = JsonSerializer.Serialize(items, _json);
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PortfolioSnapshot] Failed to patch HoldingRole for item {Id}.", itemId);
        }
    }
}
