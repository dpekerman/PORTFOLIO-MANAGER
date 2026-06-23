using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IWatchlistService
{
    Task<IReadOnlyList<WatchlistItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<WatchlistItemDto> AddAsync(AddWatchlistItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> UpdateRoleAsync(int id, string role, CancellationToken ct = default);
}

public sealed class WatchlistService(AppDbContext db) : IWatchlistService
{
    public async Task<IReadOnlyList<WatchlistItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await db.WatchlistItems
            .AsNoTracking()
            .OrderBy(x => x.Symbol)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    public async Task<WatchlistItemDto> AddAsync(AddWatchlistItemRequest request, CancellationToken ct = default)
    {
        var item = new WatchlistItem
        {
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
        var item = await db.WatchlistItems.FindAsync([id], ct);
        if (item is null) return false;

        db.WatchlistItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateRoleAsync(int id, string role, CancellationToken ct = default)
    {
        var item = await db.WatchlistItems.FindAsync([id], ct);
        if (item is null) return false;

        item.Role = role;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static WatchlistItemDto ToDto(WatchlistItem item) =>
        new(item.Id, item.Symbol, item.Notes, item.AddedAt, item.Role ?? "Strategic");
}
