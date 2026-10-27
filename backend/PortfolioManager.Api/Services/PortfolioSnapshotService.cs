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
        var existing = await db.PortfolioSnapshots.FindAsync([userId], ct);
        data = CarryForwardFinalActions(existing?.SnapshotJson, data);
        var json = JsonSerializer.Serialize(data, _json);
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

    /// <summary>
    /// A market-data refresh rebuilds every row from fresh quotes, which carry no Final Action. The last published action, its
    /// evaluation time and session are persisted history: keep them for rows that have none, so a refresh (even a failed or
    /// partial one) can never erase the last valid action or make it look newly evaluated.
    /// </summary>
    internal static IReadOnlyList<PortfolioSummaryDto> CarryForwardFinalActions(string? previousJson, IReadOnlyList<PortfolioSummaryDto> incoming)
    {
        if (string.IsNullOrWhiteSpace(previousJson)) return incoming;
        List<PortfolioSummaryDto>? previous;
        try { previous = JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(previousJson, _json); }
        catch (JsonException) { return incoming; }
        if (previous is null || previous.Count == 0) return incoming;

        var byId = previous.Where(p => !string.IsNullOrEmpty(p.Item.FinalAction)).GroupBy(p => p.Item.Id).ToDictionary(g => g.Key, g => g.First().Item);
        if (byId.Count == 0) return incoming;
        return incoming.Select(s =>
            string.IsNullOrEmpty(s.Item.FinalAction) && byId.TryGetValue(s.Item.Id, out var old)
                ? s with
                {
                    Item = s.Item with
                    {
                        FinalAction = old.FinalAction,
                        FinalActionSeverity = old.FinalActionSeverity,
                        FinalActionPriority = old.FinalActionPriority,
                        FinalActionUpdatedAt = old.FinalActionUpdatedAt,
                        FinalActionSession = old.FinalActionSession,
                    },
                }
                : s).ToList();
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
                        FinalActionSession = old.FinalActionSession,
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
                // The stamp is the symbol's own evaluation time, never the write time, and it never moves backwards,
                // so republishing a cached evaluation cannot make an older action look newly evaluated.
                var evaluatedAt = sync.EvaluatedAtUtc ?? now;
                if (list[i].Item.FinalActionUpdatedAt is { } existing && existing > evaluatedAt) continue;
                list[i] = list[i] with
                {
                    Item = list[i].Item with
                    {
                        FinalAction = sync.FinalAction,
                        FinalActionSeverity = sync.Severity,
                        FinalActionPriority = sync.Priority,
                        FinalActionUpdatedAt = evaluatedAt,
                        FinalActionSession = sync.Session ?? list[i].Item.FinalActionSession,
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
