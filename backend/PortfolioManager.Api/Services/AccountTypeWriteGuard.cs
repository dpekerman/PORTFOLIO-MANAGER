using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

internal static class AccountTypeWriteGuard
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    internal static async Task<IDbContextTransaction?> BeginAsync(AppDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;
        var owned = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlRawAsync("""
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource = 'PortfolioManager.AccountTypes',
                        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                    IF @result < 0 THROW 51000, 'Account types are busy. Retry the operation.', 1;
                    """, ct);
            return owned;
        }
        catch
        {
            if (owned is not null) await owned.DisposeAsync();
            throw;
        }
    }

    internal static bool NeedsValidation(AppDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        return db.ChangeTracker.Entries().Any(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && e.Entity is PortfolioItem or OptionItem or CashItem or AccountType or PortfolioSnapshot);
    }

    internal static async Task ValidateAsync(AppDbContext db, CancellationToken ct)
    {
        var catalog = await db.AccountTypes.AsNoTracking().ToListAsync(ct);
        foreach (var entry in db.ChangeTracker.Entries<AccountType>())
        {
            if (entry.State == EntityState.Unchanged) continue;
            catalog.RemoveAll(x => x.Id == entry.Entity.Id && entry.Entity.Id != 0);
            if (entry.State != EntityState.Deleted) catalog.Add(entry.Entity);
        }
        var choices = catalog.ToDictionary(x => x.NormalizedName, x => x.Name, StringComparer.Ordinal);
        var missing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in db.ChangeTracker.Entries().Where(e =>
            e.State is EntityState.Added or EntityState.Modified
            && e.Entity is PortfolioItem or OptionItem or CashItem))
        {
            var account = entry.Property("AccountType");
            if (entry.State != EntityState.Added && !account.IsModified) continue;
            var value = account.CurrentValue as string;
            if (string.IsNullOrWhiteSpace(value)) { account.CurrentValue = null; continue; }
            var name = AccountTypeService.ValidateName(value);
            if (!choices.TryGetValue(AccountTypeService.Normalize(name), out var canonical))
                missing.Add(name);
            else account.CurrentValue = canonical;
        }
        if (missing.Count > 0)
            throw new AccountTypeException(409, $"Unknown account types: {string.Join(", ", missing.Order())}. Ask an Admin to add/map them in Configuration before saving or restoring. Existing data has not been replaced.");

        var snapshots = db.ChangeTracker.Entries<PortfolioSnapshot>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified).ToList();
        if (snapshots.Count == 0) return;
        var accounts = await db.PortfolioItems.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.AccountType, ct);
        foreach (var entry in db.ChangeTracker.Entries<PortfolioItem>())
        {
            if (entry.State == EntityState.Deleted) accounts.Remove(entry.Entity.Id);
            else if (entry.State == EntityState.Added || entry.Property(x => x.AccountType).IsModified)
                accounts[entry.Entity.Id] = entry.Entity.AccountType;
        }
        foreach (var entry in snapshots)
        {
            List<PortfolioSummaryDto> rows;
            try
            {
                rows = JsonSerializer.Deserialize<List<PortfolioSummaryDto>>(entry.Entity.SnapshotJson, Json)
                    ?? throw new JsonException("Expected an array.");
            }
            catch (JsonException)
            {
                throw new AccountTypeException(409, "A stored portfolio snapshot is invalid. Refresh/rebuild it before renaming accounts.");
            }
            rows = rows.Select(x => accounts.TryGetValue(x.Item.Id, out var account)
                ? x with { Item = x.Item with { AccountType = account } } : x).ToList();
            entry.Entity.SnapshotJson = JsonSerializer.Serialize(rows, Json);
            entry.Entity.ItemCount = rows.Count;
        }
    }
}
