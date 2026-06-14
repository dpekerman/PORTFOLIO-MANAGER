using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IPortfolioService
{
    Task<IReadOnlyList<PortfolioItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<PortfolioItemDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PortfolioItemDto> AddAsync(AddPortfolioItemRequest request, CancellationToken ct = default);
    Task<PortfolioItemDto> AddManualAsync(AddManualPositionRequest request, CancellationToken ct = default);
    Task<PortfolioItemDto?> UpdateAsync(int id, UpdatePortfolioItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Fetches sector/industry from Yahoo Finance for every non-manual portfolio item that lacks one and persists the data.</summary>
    Task<int> RefreshSectorsAsync(CancellationToken ct = default);
}

public sealed class PortfolioService(AppDbContext db, IMarketDataProvider marketData) : IPortfolioService
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
        // Auto-fetch sector/industry from Yahoo Finance
        var (sector, industry) = await marketData.GetSectorAsync(request.Symbol, ct);

        var item = new PortfolioItem
        {
            Symbol           = request.Symbol.ToUpperInvariant(),
            CompanyName      = request.CompanyName,
            Shares           = request.Shares,
            AverageCostBasis = request.AverageCostBasis,
            Sector           = sector,
            Industry         = industry,
            AddedAt          = DateTime.UtcNow
        };

        db.PortfolioItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<PortfolioItemDto> AddManualAsync(AddManualPositionRequest request, CancellationToken ct = default)
    {
        // Generate a unique short symbol so the unique-index constraint is satisfied.
        // Manual positions are never looked up by symbol on Yahoo Finance.
        var sym = "M_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var item = new PortfolioItem
        {
            Symbol           = sym,
            CompanyName      = request.Name,
            Shares           = 1,
            AverageCostBasis = request.AverageCost,
            Sector           = request.Name,          // per product spec: Name → Sector
            Industry         = request.Description,   // Description → Industry
            IsManual         = true,
            ManualMarketValue = request.MarketValue,
            AddedAt          = DateTime.UtcNow
        };

        db.PortfolioItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<PortfolioItemDto?> UpdateAsync(int id, UpdatePortfolioItemRequest request, CancellationToken ct = default)
    {
        var item = await db.PortfolioItems.FindAsync([id], ct);
        if (item is null) return null;

        item.CompanyName      = request.CompanyName;
        item.Shares           = request.Shares;
        item.AverageCostBasis = request.AverageCostBasis;
        if (!string.IsNullOrWhiteSpace(request.Sector))   item.Sector   = request.Sector;
        if (!string.IsNullOrWhiteSpace(request.Industry)) item.Industry = request.Industry;

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
        new(item.Id, item.Symbol, item.CompanyName, item.Shares, item.AverageCostBasis,
            item.Sector, item.Industry, item.IsManual, item.ManualMarketValue, item.AddedAt);

    public async Task<int> RefreshSectorsAsync(CancellationToken ct = default)
    {
        var items = await db.PortfolioItems.ToListAsync(ct);
        // Process all items; throttle to 3 concurrent requests to avoid Yahoo rate limiting
        var semaphore = new SemaphoreSlim(3, 3);
        int updated  = 0;

        var tasks = items
            .Where(item => !item.IsManual)  // manual positions have no ticker — skip Yahoo call
            .Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var (sector, industry) = await marketData.GetSectorAsync(item.Symbol, ct);
                if (!string.IsNullOrWhiteSpace(sector))
                {
                    item.Sector   = sector;
                    item.Industry = industry;
                    Interlocked.Increment(ref updated);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        if (updated > 0)
            await db.SaveChangesAsync(ct);

        return updated;
    }
}
