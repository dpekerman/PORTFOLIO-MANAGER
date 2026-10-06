using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public sealed class AccountTypeService(AppDbContext db) : IAccountTypeService
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var tx = await AccountTypeWriteGuard.BeginAsync(db, ct);
        var names = await db.PortfolioItems.Select(x => x.AccountType)
            .Concat(db.OptionItems.Select(x => x.AccountType))
            .Concat(db.CashItems.Select(x => x.AccountType)).ToListAsync(ct);
        var catalog = await db.AccountTypes.ToListAsync(ct);
        foreach (var raw in names.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            var name = ValidateName(raw!);
            if (name != raw)
                throw new AccountTypeException(409, $"Legacy account '{raw}' contains surrounding whitespace. Correct it before managing account types.");
            var normalized = Normalize(name);
            var existing = catalog.Find(x => x.NormalizedName == normalized);
            if (existing is not null)
            {
                if (existing.Name != name)
                    throw new AccountTypeException(409, $"Legacy account '{name}' conflicts with '{existing.Name}'. Resolve the names without merging financial accounts.");
                continue;
            }
            var item = new AccountType { Name = name, NormalizedName = normalized };
            catalog.Add(item);
            db.AccountTypes.Add(item);
        }
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AccountTypeResponse>> GetAllAsync(CancellationToken ct)
    {
        var catalog = await db.AccountTypes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var stocks = await db.PortfolioItems.GroupBy(x => x.AccountType).Select(g => new { Name = g.Key, Count = g.Count() }).ToListAsync(ct);
        var options = await db.OptionItems.GroupBy(x => x.AccountType).Select(g => new { Name = g.Key, Count = g.Count() }).ToListAsync(ct);
        var cash = await db.CashItems.GroupBy(x => x.AccountType).Select(g => new { Name = g.Key, Count = g.Count() }).ToListAsync(ct);
        return catalog.Select(x => new AccountTypeResponse(x.Id, x.Name, x.Version,
            stocks.Where(g => g.Name == x.Name).Sum(g => g.Count),
            options.Where(g => g.Name == x.Name).Sum(g => g.Count),
            cash.Where(g => g.Name == x.Name).Sum(g => g.Count))).ToList();
    }

    public async Task<AccountTypeResponse> AddAsync(string name, CancellationToken ct)
    {
        name = ValidateName(name);
        await using var tx = await AccountTypeWriteGuard.BeginAsync(db, ct);
        await CheckUniqueAsync(name, null, ct);
        var item = new AccountType { Name = name, NormalizedName = Normalize(name) };
        db.AccountTypes.Add(item);
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new(item.Id, item.Name, item.Version, 0, 0, 0);
    }

    public async Task<AccountTypeRenameResponse> RenameAsync(int id, UpdateAccountTypeRequest request, CancellationToken ct)
    {
        var name = ValidateName(request.Name);
        await using var tx = await AccountTypeWriteGuard.BeginAsync(db, ct);
        var item = await FindAsync(id, request.Version, ct);
        await CheckUniqueAsync(name, id, ct);
        var oldName = item.Name;
        var stocks = await db.PortfolioItems.Where(x => x.AccountType == oldName).ToListAsync(ct);
        var options = await db.OptionItems.Where(x => x.AccountType == oldName).ToListAsync(ct);
        var cash = await db.CashItems.Where(x => x.AccountType == oldName).ToListAsync(ct);
        foreach (var stock in stocks) stock.AccountType = name;
        foreach (var option in options) option.AccountType = name;
        foreach (var entry in cash) entry.AccountType = name;
        item.Name = name;
        item.NormalizedName = Normalize(name);
        item.Version = Guid.NewGuid();
        // Snapshot validation patches authoritative account fields before the transaction commits.
        await db.PortfolioSnapshots.LoadAsync(ct);
        foreach (var snapshot in db.PortfolioSnapshots.Local)
            db.Entry(snapshot).Property(x => x.SnapshotJson).IsModified = true;
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new(oldName, new(item.Id, name, item.Version, stocks.Count, options.Count, cash.Count));
    }

    public async Task DeleteAsync(int id, Guid version, CancellationToken ct)
    {
        await using var tx = await AccountTypeWriteGuard.BeginAsync(db, ct);
        var item = await FindAsync(id, version, ct);
        if (await db.PortfolioItems.AnyAsync(x => x.AccountType == item.Name, ct)
            || await db.OptionItems.AnyAsync(x => x.AccountType == item.Name, ct)
            || await db.CashItems.AnyAsync(x => x.AccountType == item.Name, ct))
            throw new AccountTypeException(409, "This account type is in use, including closed transactions or cash entries, and cannot be deleted.");
        db.AccountTypes.Remove(item);
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }

    private async Task<AccountType> FindAsync(int id, Guid version, CancellationToken ct)
    {
        var item = await db.AccountTypes.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AccountTypeException(404, "Account type not found.");
        if (item.Version != version)
            throw new AccountTypeException(409, "Account types have changed. Refresh the list before trying again.");
        return item;
    }

    private async Task CheckUniqueAsync(string name, int? exceptId, CancellationToken ct)
    {
        var normalized = Normalize(name);
        if (await db.AccountTypes.AnyAsync(x => x.NormalizedName == normalized && x.Id != exceptId, ct))
            throw new AccountTypeException(409, "An account type with that name already exists.");
    }

    internal static string ValidateName(string? value)
    {
        var name = value?.Trim() ?? "";
        if (name.Length is < 1 or > 120)
            throw new AccountTypeException(400, "Account type names must contain 1 to 120 characters.");
        return name;
    }

    internal static string Normalize(string name) => name.ToUpperInvariant();
}
