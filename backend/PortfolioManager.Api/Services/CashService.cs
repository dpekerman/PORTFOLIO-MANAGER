using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using System.Security.Claims;

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

public sealed class CashService(AppDbContext db, IHttpContextAccessor httpCtx) : ICashService
{
    private string? CurrentUserId() => httpCtx.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool IsAdmin() => httpCtx.HttpContext?.User.IsInRole("Admin") ?? false;

    private IQueryable<CashItem> OwnedItems()
    {
        var q = db.CashItems.AsQueryable();
        if (IsAdmin()) return q;
        var uid = CurrentUserId();
        return q.Where(x => x.UserId == uid || x.UserId == null);
    }

    public async Task<IReadOnlyList<CashItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await OwnedItems()
            .AsNoTracking()
            .OrderBy(x => x.AddedAt)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<CashItemDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : ToDto(item);
    }

    public async Task<CashItemDto> AddAsync(AddCashItemRequest request, CancellationToken ct = default)
    {
        var item = new CashItem
        {
            UserId          = CurrentUserId(),
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
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
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
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        db.CashItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static CashItemDto ToDto(CashItem item) =>
        new(item.Id, item.Description, item.Amount, item.AddedAt, item.AccountType, item.TransactionDate);

    public async Task<IReadOnlyList<CashBackupItem>> BackupAsync(CancellationToken ct = default)
    {
        var items = await OwnedItems().AsNoTracking().OrderBy(x => x.AddedAt).ToListAsync(ct);
        return items.Select(x => new CashBackupItem(x.Description, x.Amount, x.AddedAt)).ToList();
    }

    public async Task<int> RestoreAsync(IReadOnlyList<CashBackupItem> items, CancellationToken ct = default)
    {
        var uid = CurrentUserId();
        var existing = await OwnedItems().ToListAsync(ct);
        db.CashItems.RemoveRange(existing);

        var newItems = items.Select(i => new CashItem
        {
            UserId      = uid,
            Description = string.IsNullOrWhiteSpace(i.Description) ? "CASH" : i.Description,
            Amount      = i.Amount,
            AddedAt     = i.AddedAt
        }).ToList();

        db.CashItems.AddRange(newItems);
        await db.SaveChangesAsync(ct);
        return newItems.Count;
    }
}