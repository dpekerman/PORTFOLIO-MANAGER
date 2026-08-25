using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using System.Security.Claims;

namespace PortfolioManager.Api.Services;

public interface IWatchlistService
{
    Task<IReadOnlyList<WatchlistItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<WatchlistItemDto> AddAsync(AddWatchlistItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> UpdateRoleAsync(int id, string role, CancellationToken ct = default);
    Task<bool> UpdateFavoriteAsync(int id, bool isFavorite, CancellationToken ct = default);
    Task<bool> UpdateNotesAsync(int id, string notes, CancellationToken ct = default);
    Task<bool> UpdateEarningsDateAsync(int id, DateTime? earningsDate, CancellationToken ct = default);
    Task<IReadOnlyList<WatchlistBackupItem>> BackupAsync(CancellationToken ct = default);
    Task<int> RestoreAsync(IReadOnlyList<WatchlistBackupItem> items, CancellationToken ct = default);
}

public sealed class WatchlistService(AppDbContext db, IHttpContextAccessor httpCtx) : IWatchlistService
{
    private string? CurrentUserId() => httpCtx.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool IsAdmin() => httpCtx.HttpContext?.User.IsInRole("Admin") ?? false;

    private IQueryable<WatchlistItem> OwnedItems()
    {
        var q = db.WatchlistItems.AsQueryable();
        if (IsAdmin()) return q;
        var uid = CurrentUserId();
        return q.Where(x => x.UserId == uid || x.UserId == null);
    }

    public async Task<IReadOnlyList<WatchlistItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await OwnedItems()
            .AsNoTracking()
            .OrderBy(x => x.Symbol)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<WatchlistItemDto> AddAsync(AddWatchlistItemRequest request, CancellationToken ct = default)
    {
        var item = new WatchlistItem
        {
            UserId  = CurrentUserId(),
            Symbol  = request.Symbol.ToUpperInvariant(),
            Notes   = request.Notes ?? "",
            Role    = request.Role ?? "Strategic",
            AddedAt = DateTime.UtcNow
        };
        db.WatchlistItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        db.WatchlistItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateRoleAsync(int id, string role, CancellationToken ct = default)
    {
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        item.Role = role;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateFavoriteAsync(int id, bool isFavorite, CancellationToken ct = default)
    {
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        item.IsFavorite = isFavorite;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateNotesAsync(int id, string notes, CancellationToken ct = default)
    {
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        item.Notes = notes ?? "";
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateEarningsDateAsync(int id, DateTime? earningsDate, CancellationToken ct = default)
    {
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        item.EarningsDate = earningsDate?.Date;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static WatchlistItemDto ToDto(WatchlistItem item) =>
        new(item.Id, item.Symbol, item.Notes, item.AddedAt, item.Role ?? "Strategic", item.IsFavorite, item.EarningsDate);

    public async Task<IReadOnlyList<WatchlistBackupItem>> BackupAsync(CancellationToken ct = default)
    {
        var items = await OwnedItems().AsNoTracking().OrderBy(x => x.Symbol).ToListAsync(ct);
        return items.Select(x => new WatchlistBackupItem(x.Symbol, x.Notes, x.Role ?? "Strategic", x.AddedAt, x.EarningsDate)).ToList();
    }

    public async Task<int> RestoreAsync(IReadOnlyList<WatchlistBackupItem> items, CancellationToken ct = default)
    {
        var uid = CurrentUserId();
        var existing = await OwnedItems().ToListAsync(ct);
        db.WatchlistItems.RemoveRange(existing);

        var newItems = items.Select(i => new WatchlistItem
        {
            UserId  = uid,
            Symbol  = i.Symbol.ToUpperInvariant(),
            Notes   = i.Notes ?? "",
            Role    = i.Role ?? "Strategic",
            AddedAt = i.AddedAt,
            EarningsDate = i.EarningsDate
        }).ToList();

        db.WatchlistItems.AddRange(newItems);
        await db.SaveChangesAsync(ct);
        return newItems.Count;
    }
}