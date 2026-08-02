using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface ICashService
{
    Task<IReadOnlyList<CashItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<CashItemDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CashItemDto> AddAsync(AddCashItemRequest request, CancellationToken ct = default);
    Task<CashItemDto?> UpdateAsync(int id, UpdateCashItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CashBackupItem>> BackupAsync(CancellationToken ct = default);
    Task<int> RestoreAsync(IReadOnlyList<CashBackupItem> items, CancellationToken ct = default);
}

public sealed class CashService(AppDbContext db) : ICashService
{
    public async Task<IReadOnlyList<CashItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await db.CashItems
            .AsNoTracking()
            .OrderBy(x => x.AddedAt)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<CashItemDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await db.CashItems.FindAsync([id], ct);
        return item is null ? null : ToDto(item);
    }

    public async Task<CashItemDto> AddAsync(AddCashItemRequest request, CancellationToken ct = default)
    {
        var item = new CashItem
        {
            Description     = string.IsNullOrWhiteSpace(request.Description) ? "CASH" : request.Description,
            Amount          = request.Amount,
            AccountType     = request.AccountType,
            TransactionDate = request.TransactionDate,
            AddedAt         = DateTime.UtcNow
        };
        db.CashItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<CashItemDto?> UpdateAsync(int id, UpdateCashItemRequest request, CancellationToken ct = default)
    {
        var item = await db.CashItems.FindAsync([id], ct);
        if (item is null) return null;
        item.Description     = string.IsNullOrWhiteSpace(request.Description) ? "CASH" : request.Description;
        item.Amount          = request.Amount;
        item.AccountType     = request.AccountType;
        item.TransactionDate = request.TransactionDate;
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var item = await db.CashItems.FindAsync([id], ct);
        if (item is null) return false;
        db.CashItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static CashItemDto ToDto(CashItem item) =>
        new(item.Id, item.Description, item.Amount, item.AddedAt, item.AccountType, item.TransactionDate);

    public async Task<IReadOnlyList<CashBackupItem>> BackupAsync(CancellationToken ct = default)
    {
        var items = await db.CashItems.AsNoTracking().OrderBy(x => x.AddedAt).ToListAsync(ct);
        return items.Select(x => new CashBackupItem(x.Description, x.Amount, x.AddedAt)).ToList();
    }

    public async Task<int> RestoreAsync(IReadOnlyList<CashBackupItem> items, CancellationToken ct = default)
    {
        var existing = await db.CashItems.ToListAsync(ct);
        db.CashItems.RemoveRange(existing);

        var newItems = items.Select(i => new CashItem
        {
            Description = string.IsNullOrWhiteSpace(i.Description) ? "CASH" : i.Description,
            Amount      = i.Amount,
            AddedAt     = i.AddedAt
        }).ToList();

        db.CashItems.AddRange(newItems);
        await db.SaveChangesAsync(ct);
        return newItems.Count;
    }
}
