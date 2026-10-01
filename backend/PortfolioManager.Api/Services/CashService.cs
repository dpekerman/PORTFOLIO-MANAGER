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
    /// <summary>"Adjust Balance": user types the desired new total; backend computes the delta vs the
    /// current ledger total and inserts one new row. Rejects a Type/delta direction mismatch (400).</summary>
    Task<CashItemDto> AdjustBalanceAsync(AdjustCashBalanceRequest request, CancellationToken ct = default);
    /// <summary>Creates the TradePurchase/TradeProceeds row for one trade leg, tied to it via SourceType/SourceItemId.
    /// Throws ArgumentException/InvalidOperationException for invalid input, a missing/manual source, or an already-linked leg.</summary>
    Task<CashItemDto> AddLinkedAsync(AddLinkedCashRequest request, CancellationToken ct = default);
    /// <summary>The cash row linked to a trade leg, or null when the leg has no linked cash row.</summary>
    Task<CashItemDto?> GetLinkedAsync(string sourceType, int sourceItemId, CancellationToken ct = default);
    /// <summary>After a partial close: moves the share of the purchase cash row that belongs to the remaining shares onto a new row
    /// linked to the remainder position. Net cash and every historical balance are unchanged (both rows share one date).</summary>
    Task SplitPurchaseLinkAsync(int closedItemId, int remainderItemId, decimal remainingShares, decimal totalShares, CancellationToken ct = default);
    Task<IReadOnlyList<CashBackupItem>> BackupAsync(CancellationToken ct = default);
    Task<int> RestoreAsync(IReadOnlyList<CashBackupItem> items, CancellationToken ct = default);
}

public sealed class CashService(
    AppDbContext db,
    IHttpContextAccessor httpCtx,
    ICashLedgerQueryService cashLedger,
    IPortfolioValueHistoryService historyService,
    IMutationClock mutationClock) : ICashService
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

    private static TimeZoneInfo? TryGetEasternTz()
    {
        foreach (var id in new[] { "Eastern Standard Time", "America/New_York" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* ignored */ }
        }
        return null;
    }

    private static DateOnly TodayEt()
    {
        var tz = TryGetEasternTz();
        var now = tz is not null ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz) : DateTime.UtcNow;
        return DateOnly.FromDateTime(now);
    }

    /// <summary>Persists pending changes and reconciles PortfolioValueHistories for the given effective
    /// date: future dates need no recompute; today marks the debounced same-day reseal; anything earlier
    /// runs a synchronous, transactional cash-range recalculation (never touches Stocks/OptionsValue).</summary>
    private async Task ReconcileAfterMutationAsync(DateOnly effectiveDate, CancellationToken ct)
    {
        var todayEt = TodayEt();
        if (effectiveDate > todayEt)
        {
            await db.SaveChangesAsync(ct);
            return;
        }
        if (effectiveDate == todayEt)
        {
            await db.SaveChangesAsync(ct);
            mutationClock.Touch();
            mutationClock.MarkTodayDirty();
            return;
        }

        if (!db.Database.IsRelational())
        {
            // Non-relational test providers (e.g. EF Core InMemory) don't support transactions —
            // still perform both writes so service-level logic can be verified without atomicity.
            await db.SaveChangesAsync(ct);
            await historyService.RecalculateCashRangeAsync(effectiveDate, ct);
            mutationClock.Touch();
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.SaveChangesAsync(ct);
            await historyService.RecalculateCashRangeAsync(effectiveDate, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
        mutationClock.Touch();
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
        if (!CashFlowTypeRules.IsKnownType(request.CashFlowType))
            throw new ArgumentException("CashFlowType is required and must be one of the known ledger types.", nameof(request));
        if (string.Equals(request.CashFlowType, CashFlowTypeRules.OpeningBalance, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OpeningBalance is migration/admin-only and cannot be added directly.", nameof(request));

        var signedAmount = CashFlowTypeRules.DeriveSignedAmount(request.CashFlowType, request.Amount);
        var effectiveDate = request.TransactionDate.HasValue
            ? DateOnly.FromDateTime(request.TransactionDate.Value)
            : TodayEt();

        var item = new CashItem
        {
            UserId          = CurrentUserId(),
            Description     = string.IsNullOrWhiteSpace(request.Description) ? "CASH" : request.Description,
            Amount          = signedAmount,
            AccountType     = request.AccountType,
            TransactionDate = effectiveDate.ToDateTime(TimeOnly.MinValue),
            CashFlowType    = request.CashFlowType,
            AddedAt         = DateTime.UtcNow
        };
        db.CashItems.Add(item);
        await ReconcileAfterMutationAsync(effectiveDate, ct);
        return ToDto(item);
    }

    public async Task<CashItemDto> AdjustBalanceAsync(AdjustCashBalanceRequest request, CancellationToken ct = default)
    {
        if (!CashFlowTypeRules.IsKnownType(request.CashFlowType))
            throw new ArgumentException("CashFlowType is required and must be one of the known ledger types.", nameof(request));
        if (string.Equals(request.CashFlowType, CashFlowTypeRules.OpeningBalance, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OpeningBalance is migration/admin-only and cannot be adjusted directly.", nameof(request));

        var effectiveDate = request.TransactionDate.HasValue
            ? DateOnly.FromDateTime(request.TransactionDate.Value)
            : TodayEt();
        var currentTotal = await cashLedger.GetTotalAsOfAsync(TodayEt(), request.AccountType, ct);
        var delta = request.DesiredNewTotal - currentTotal;

        if (delta == 0m)
            throw new InvalidOperationException("Desired total equals the current total for this account — no change to record.");

        var allowedSign = CashFlowTypeRules.AllowedSign(request.CashFlowType);
        if (allowedSign is null || Math.Sign(delta) != allowedSign)
            throw new InvalidOperationException(
                $"CashFlowType '{request.CashFlowType}' direction does not match the computed change of {delta:C}. " +
                "Pick a Type consistent with whether the balance is increasing or decreasing.");

        var item = new CashItem
        {
            UserId          = CurrentUserId(),
            Description     = $"Balance adjustment ({request.CashFlowType})",
            Amount          = delta,
            AccountType     = request.AccountType,
            TransactionDate = effectiveDate.ToDateTime(TimeOnly.MinValue),
            CashFlowType    = request.CashFlowType,
            AddedAt         = DateTime.UtcNow
        };
        db.CashItems.Add(item);
        await ReconcileAfterMutationAsync(effectiveDate, ct);
        return ToDto(item);
    }

    public async Task<CashItemDto> AddLinkedAsync(AddLinkedCashRequest request, CancellationToken ct = default)
    {
        if (!TradeLinkSourceTypes.IsKnown(request.SourceType))
            throw new ArgumentException("SourceType must be one of PortfolioOpen, PortfolioClose, OptionOpen, OptionClose.", nameof(request));
        if (request.Amount <= 0m)
            throw new ArgumentException("Amount must be a positive magnitude.", nameof(request));

        await EnsureSourceItemUsableAsync(request.SourceType, request.SourceItemId, ct);

        if (await db.CashItems.AnyAsync(c => c.SourceType == request.SourceType && c.SourceItemId == request.SourceItemId, ct))
            throw new InvalidOperationException("This trade already has a linked cash entry.");

        var cashFlowType = TradeLinkSourceTypes.CashFlowTypeFor(request.SourceType);
        var effectiveDate = request.TransactionDate.HasValue
            ? DateOnly.FromDateTime(request.TransactionDate.Value)
            : TodayEt();

        var item = new CashItem
        {
            UserId          = CurrentUserId(),
            Description     = string.IsNullOrWhiteSpace(request.Description) ? "CASH" : request.Description.Trim(),
            Amount          = CashFlowTypeRules.DeriveSignedAmount(cashFlowType, request.Amount),
            AccountType     = request.AccountType,
            TransactionDate = effectiveDate.ToDateTime(TimeOnly.MinValue),
            CashFlowType    = cashFlowType,
            AddedAt         = DateTime.UtcNow,
            SourceType      = request.SourceType,
            SourceItemId    = request.SourceItemId
        };
        db.CashItems.Add(item);
        await ReconcileAfterMutationAsync(effectiveDate, ct);
        return ToDto(item);
    }

    public async Task<CashItemDto?> GetLinkedAsync(string sourceType, int sourceItemId, CancellationToken ct = default)
    {
        if (!TradeLinkSourceTypes.IsKnown(sourceType)) return null;
        var item = await OwnedItems().AsNoTracking()
            .FirstOrDefaultAsync(c => c.SourceType == sourceType && c.SourceItemId == sourceItemId, ct);
        return item is null ? null : ToDto(item);
    }

    public async Task SplitPurchaseLinkAsync(int closedItemId, int remainderItemId, decimal remainingShares, decimal totalShares, CancellationToken ct = default)
    {
        if (totalShares <= 0m || remainingShares <= 0m || remainingShares >= totalShares) return;

        var purchase = await OwnedItems().FirstOrDefaultAsync(
            c => c.SourceType == TradeLinkSourceTypes.PortfolioOpen && c.SourceItemId == closedItemId, ct);
        if (purchase is null) return;
        if (await db.CashItems.AnyAsync(
                c => c.SourceType == TradeLinkSourceTypes.PortfolioOpen && c.SourceItemId == remainderItemId, ct)) return;

        var remainderAmount = Math.Round(purchase.Amount * remainingShares / totalShares, 2, MidpointRounding.AwayFromZero);
        if (remainderAmount == 0m) return;

        purchase.Amount -= remainderAmount;
        purchase.ModifiedAt = DateTime.UtcNow;
        db.CashItems.Add(new CashItem
        {
            UserId          = purchase.UserId,
            Description     = $"{purchase.Description} (remaining shares)",
            Amount          = remainderAmount,
            AccountType     = purchase.AccountType,
            TransactionDate = purchase.TransactionDate,
            CashFlowType    = purchase.CashFlowType,
            AddedAt         = DateTime.UtcNow,
            SourceType      = TradeLinkSourceTypes.PortfolioOpen,
            SourceItemId    = remainderItemId
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The source trade must exist, be visible to the caller, and not be a manual (non-ticker) position.</summary>
    private async Task EnsureSourceItemUsableAsync(string sourceType, int sourceItemId, CancellationToken ct)
    {
        var uid = CurrentUserId();
        var admin = IsAdmin();
        if (TradeLinkSourceTypes.IsPortfolio(sourceType))
        {
            var position = await db.PortfolioItems.AsNoTracking()
                .Where(x => x.Id == sourceItemId && (admin || x.UserId == uid || x.UserId == null))
                .Select(x => new { x.IsManual })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("The trade to link no longer exists.");
            if (position.IsManual)
                throw new InvalidOperationException("Manual positions have no trade price and cannot be linked to cash.");
            return;
        }

        var exists = await db.OptionItems.AsNoTracking()
            .AnyAsync(x => x.Id == sourceItemId && (admin || x.UserId == uid || x.UserId == null), ct);
        if (!exists) throw new InvalidOperationException("The trade to link no longer exists.");
    }

    public async Task<CashItemDto?> UpdateAsync(int id, UpdateCashItemRequest request, CancellationToken ct = default)
    {
        if (!CashFlowTypeRules.IsKnownType(request.CashFlowType))
            throw new ArgumentException("CashFlowType is required and must be one of the known ledger types.", nameof(request));

        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;

        var wasOpeningBalance = string.Equals(item.CashFlowType, CashFlowTypeRules.OpeningBalance, StringComparison.OrdinalIgnoreCase);
        var staysOpeningBalance = string.Equals(request.CashFlowType, CashFlowTypeRules.OpeningBalance, StringComparison.OrdinalIgnoreCase);
        if (wasOpeningBalance != staysOpeningBalance)
            throw new InvalidOperationException("CashFlowType cannot be changed to/from OpeningBalance through this endpoint — it is migration/admin-only.");

        var newAmount = staysOpeningBalance
            ? Math.Abs(request.Amount)
            : CashFlowTypeRules.DeriveSignedAmount(request.CashFlowType, request.Amount);

        var oldDate = item.TransactionDate.HasValue ? DateOnly.FromDateTime(item.TransactionDate.Value) : TodayEt();
        var newDate = request.TransactionDate.HasValue ? DateOnly.FromDateTime(request.TransactionDate.Value) : oldDate;
        var amountChanged = item.Amount != newAmount;
        var dateChanged = oldDate != newDate;

        item.Description     = string.IsNullOrWhiteSpace(request.Description) ? "CASH" : request.Description;
        item.Amount          = newAmount;
        item.AccountType     = request.AccountType;
        item.TransactionDate = newDate.ToDateTime(TimeOnly.MinValue);
        item.CashFlowType    = request.CashFlowType;
        item.ModifiedAt      = DateTime.UtcNow;

        if (!amountChanged && !dateChanged)
        {
            // Only classification/description/account changed. No CashValue recompute needed — but this
            // IS a real historical classification change and must still be persisted.
            await db.SaveChangesAsync(ct);
            return ToDto(item);
        }

        var todayEt = TodayEt();
        if (oldDate > todayEt && newDate > todayEt)
        {
            // Both old and new dates are strictly future — nothing currently depends on either yet.
            await db.SaveChangesAsync(ct);
            return ToDto(item);
        }

        var earliest = oldDate < newDate ? oldDate : newDate;
        await ReconcileAfterMutationAsync(earliest, ct);
        return ToDto(item);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        if (string.Equals(item.CashFlowType, CashFlowTypeRules.OpeningBalance, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("OpeningBalance entries cannot be deleted — they are the ledger's foundation for this account.");

        var effectiveDate = item.TransactionDate.HasValue ? DateOnly.FromDateTime(item.TransactionDate.Value) : TodayEt();
        db.CashItems.Remove(item);
        await ReconcileAfterMutationAsync(effectiveDate, ct);
        return true;
    }

    private static CashItemDto ToDto(CashItem item) =>
        new(item.Id, item.Description, item.Amount, item.AddedAt, item.AccountType, item.TransactionDate,
            item.CashFlowType, CashFlowTypeRules.IsExternalFlow(item.CashFlowType), item.ModifiedAt,
            item.SourceType, item.SourceItemId);

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