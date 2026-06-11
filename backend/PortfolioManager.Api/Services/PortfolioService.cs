using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IPortfolioService
{
    Task<IReadOnlyList<PortfolioItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<PortfolioItemDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PortfolioItemDto> AddAsync(AddPortfolioItemRequest request, CancellationToken ct = default);
    Task<PortfolioItemDto?> UpdateAsync(int id, UpdatePortfolioItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class PortfolioService(AppDbContext db) : IPortfolioService
{
    public async Task<IReadOnlyList<PortfolioItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await db.PortfolioItems
            .AsNoTracking()
            .OrderBy(x => x.Symbol)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    public async Task<PortfolioItemDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await db.PortfolioItems.FindAsync([id], ct);
        return item is null ? null : ToDto(item);
    }

    public async Task<PortfolioItemDto> AddAsync(AddPortfolioItemRequest request, CancellationToken ct = default)
    {
        var item = new PortfolioItem
        {
            Symbol = request.Symbol.ToUpperInvariant(),
            CompanyName = request.CompanyName,
            Shares = request.Shares,
            AverageCostBasis = request.AverageCostBasis,
            AddedAt = DateTime.UtcNow
        };

        db.PortfolioItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<PortfolioItemDto?> UpdateAsync(int id, UpdatePortfolioItemRequest request, CancellationToken ct = default)
    {
        var item = await db.PortfolioItems.FindAsync([id], ct);
        if (item is null) return null;

        item.CompanyName = request.CompanyName;
        item.Shares = request.Shares;
        item.AverageCostBasis = request.AverageCostBasis;

        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var item = await db.PortfolioItems.FindAsync([id], ct);
        if (item is null) return false;

        db.PortfolioItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static PortfolioItemDto ToDto(PortfolioItem item) =>
        new(item.Id, item.Symbol, item.CompanyName, item.Shares, item.AverageCostBasis, item.AddedAt);
}
