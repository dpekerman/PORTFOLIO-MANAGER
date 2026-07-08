using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IAllocationRiskService
{
    Task<AllocationRiskConfigDto> GetAllAsync(CancellationToken ct = default);

    // Risk targets (by Role)
    Task<AllocationRiskTargetDto> UpsertRiskTargetAsync(int? id, UpsertAllocationRiskTargetRequest request, CancellationToken ct = default);
    Task<bool> DeleteRiskTargetAsync(int id, CancellationToken ct = default);

    // Sector targets
    Task<AllocationSectorTargetDto> UpsertSectorTargetAsync(int? id, UpsertAllocationSectorTargetRequest request, CancellationToken ct = default);
    Task<bool> DeleteSectorTargetAsync(int id, CancellationToken ct = default);

    // Single position limits (by Role)
    Task<SinglePositionLimitDto> UpsertPositionLimitAsync(int? id, UpsertSinglePositionLimitRequest request, CancellationToken ct = default);
    Task<bool> DeletePositionLimitAsync(int id, CancellationToken ct = default);
}

public sealed class AllocationRiskService(AppDbContext db) : IAllocationRiskService
{
    public async Task<AllocationRiskConfigDto> GetAllAsync(CancellationToken ct = default)
    {
        var riskTargets = await db.AllocationRiskTargets.AsNoTracking().OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var sectorTargets = await db.AllocationSectorTargets.AsNoTracking().OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var positionLimits = await db.SinglePositionLimits.AsNoTracking().OrderBy(x => x.DisplayOrder).ToListAsync(ct);

        return new AllocationRiskConfigDto(
            riskTargets.Select(x => new AllocationRiskTargetDto(x.Id, x.Role, x.TargetPct, x.DisplayOrder)).ToList(),
            sectorTargets.Select(x => new AllocationSectorTargetDto(x.Id, x.Sector, x.TargetPct, x.DisplayOrder)).ToList(),
            positionLimits.Select(x => new SinglePositionLimitDto(x.Id, x.Role, x.TargetPct, x.DisplayOrder)).ToList()
        );
    }

    public async Task<AllocationRiskTargetDto> UpsertRiskTargetAsync(int? id, UpsertAllocationRiskTargetRequest request, CancellationToken ct = default)
    {
        AllocationRiskTarget item;
        if (id.HasValue)
        {
            item = await db.AllocationRiskTargets.FindAsync([id.Value], ct) ?? new AllocationRiskTarget();
            item.Role = request.Role;
            item.TargetPct = request.TargetPct;
        }
        else
        {
            var maxOrder = await db.AllocationRiskTargets.MaxAsync(x => (int?)x.DisplayOrder, ct) ?? 0;
            item = new AllocationRiskTarget { Role = request.Role, TargetPct = request.TargetPct, DisplayOrder = maxOrder + 1 };
            db.AllocationRiskTargets.Add(item);
        }
        await db.SaveChangesAsync(ct);
        return new AllocationRiskTargetDto(item.Id, item.Role, item.TargetPct, item.DisplayOrder);
    }

    public async Task<bool> DeleteRiskTargetAsync(int id, CancellationToken ct = default)
    {
        var item = await db.AllocationRiskTargets.FindAsync([id], ct);
        if (item is null) return false;
        db.AllocationRiskTargets.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AllocationSectorTargetDto> UpsertSectorTargetAsync(int? id, UpsertAllocationSectorTargetRequest request, CancellationToken ct = default)
    {
        AllocationSectorTarget item;
        if (id.HasValue)
        {
            item = await db.AllocationSectorTargets.FindAsync([id.Value], ct) ?? new AllocationSectorTarget();
            item.Sector = request.Sector;
            item.TargetPct = request.TargetPct;
        }
        else
        {
            var maxOrder = await db.AllocationSectorTargets.MaxAsync(x => (int?)x.DisplayOrder, ct) ?? 0;
            item = new AllocationSectorTarget { Sector = request.Sector, TargetPct = request.TargetPct, DisplayOrder = maxOrder + 1 };
            db.AllocationSectorTargets.Add(item);
        }
        await db.SaveChangesAsync(ct);
        return new AllocationSectorTargetDto(item.Id, item.Sector, item.TargetPct, item.DisplayOrder);
    }

    public async Task<bool> DeleteSectorTargetAsync(int id, CancellationToken ct = default)
    {
        var item = await db.AllocationSectorTargets.FindAsync([id], ct);
        if (item is null) return false;
        db.AllocationSectorTargets.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SinglePositionLimitDto> UpsertPositionLimitAsync(int? id, UpsertSinglePositionLimitRequest request, CancellationToken ct = default)
    {
        SinglePositionLimit item;
        if (id.HasValue)
        {
            item = await db.SinglePositionLimits.FindAsync([id.Value], ct) ?? new SinglePositionLimit();
            item.Role = request.Role;
            item.TargetPct = request.TargetPct;
        }
        else
        {
            var maxOrder = await db.SinglePositionLimits.MaxAsync(x => (int?)x.DisplayOrder, ct) ?? 0;
            item = new SinglePositionLimit { Role = request.Role, TargetPct = request.TargetPct, DisplayOrder = maxOrder + 1 };
            db.SinglePositionLimits.Add(item);
        }
        await db.SaveChangesAsync(ct);
        return new SinglePositionLimitDto(item.Id, item.Role, item.TargetPct, item.DisplayOrder);
    }

    public async Task<bool> DeletePositionLimitAsync(int id, CancellationToken ct = default)
    {
        var item = await db.SinglePositionLimits.FindAsync([id], ct);
        if (item is null) return false;
        db.SinglePositionLimits.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
