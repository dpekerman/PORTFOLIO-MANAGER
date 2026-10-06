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
    Task PatchFinalActionsAsync(string userId, IReadOnlyList<FinalActionSyncItem> items, CancellationToken ct = default);
    Task UpsertItemsAsync(string userId, IReadOnlyList<PortfolioItemDto> items, CancellationToken ct = default);
    Task RemoveItemAsync(string userId, int itemId, CancellationToken ct = default);
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

    // Keeps the stored snapshot in step with add/edit/delete so a reload never shows pre-edit rows.
    // Existing rows keep their quote, price structure and Final Action; new rows get no quote until the next refresh.
    public async Task UpsertItemsAsync(string userId, IReadOnlyList<PortfolioItemDto> items, CancellationToken ct = default)
    {
        var row = await db.PortfolioSnapshots.FindAsync([userId], ct);
        if (row is null || items.Count == 0) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(row.SnapshotJson, _json);
            if (list is null) return;
            foreach (var dto in items)
            {
                var idx = list.FindIndex(s => s.Item.Id == dto.Id);
                if (idx < 0)
                {
                    list.Add(new PortfolioSummaryDto(dto, null));
                    continue;
                }
                var old = list[idx].Item;
                list[idx] = list[idx] with
                {
                    Item = dto with
                    {
                        FinalAction = old.FinalAction,
                        FinalActionSeverity = old.FinalActionSeverity,
                        FinalActionPriority = old.FinalActionPriority,
                        FinalActionUpdatedAt = old.FinalActionUpdatedAt,
                    },
                };
            }
            row.SnapshotJson = JsonSerializer.Serialize(list, _json);
            row.UpdatedAt = DateTime.UtcNow;
            row.ItemCount = list.Count;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PortfolioSnapshot] Failed to upsert items.");
        }
    }

    public async Task RemoveItemAsync(string userId, int itemId, CancellationToken ct = default)
    {
        var row = await db.PortfolioSnapshots.FindAsync([userId], ct);
        if (row is null) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(row.SnapshotJson, _json);
            if (list is null || list.RemoveAll(s => s.Item.Id == itemId) == 0) return;
            row.SnapshotJson = JsonSerializer.Serialize(list, _json);
            row.UpdatedAt = DateTime.UtcNow;
            row.ItemCount = list.Count;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PortfolioSnapshot] Failed to remove item {Id}.", itemId);
        }
    }

    // Patches each holding's Final Action (computed client-side) so Dashboard Action Center can
    // reuse it instead of re-deriving one from scanner/EOD facts alone.
    public async Task PatchFinalActionsAsync(string userId, IReadOnlyList<FinalActionSyncItem> items, CancellationToken ct = default)
    {
        var row = await db.PortfolioSnapshots.FindAsync([userId], ct);
        if (row is null) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(row.SnapshotJson, _json);
            if (list is null) return;
            var byId = items.ToDictionary(i => i.ItemId);
            var now = DateTime.UtcNow;
            var changed = false;
            for (var i = 0; i < list.Count; i++)
            {
                if (!byId.TryGetValue(list[i].Item.Id, out var sync)) continue;
                list[i] = list[i] with
                {
                    Item = list[i].Item with
                    {
                        FinalAction = sync.FinalAction,
                        FinalActionSeverity = sync.Severity,
                        FinalActionPriority = sync.Priority,
                        FinalActionUpdatedAt = now,
                    },
                };
                changed = true;
            }
            if (!changed) return;
            row.SnapshotJson = JsonSerializer.Serialize(list, _json);
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PortfolioSnapshot] Failed to patch final actions.");
        }
    }
}
