using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;

namespace PortfolioManager.Api.Services;

public interface ICashLedgerQueryService
{
    /// <summary>Ledger cash total as of a given ET calendar date: sum of all rows whose effective date
    /// (TransactionDate, falling back to AddedAt for any straggler row) is on or before <paramref name="date"/>.
    /// Future-dated rows are excluded. Optionally scoped to one AccountType.</summary>
    Task<decimal> GetTotalAsOfAsync(DateOnly date, string? accountType, CancellationToken ct = default);

    /// <summary>The accounting boundary: dates before this remain frozen/legacy; dates on/after it are
    /// authoritative from the cash ledger. Read from the persisted CashLedgerSettings singleton row —
    /// never inferred from MIN(TransactionDate)/app startup/DateTime.Today.</summary>
    Task<DateOnly> GetLedgerStartDateAsync(CancellationToken ct = default);
}

/// <summary>Kept separate from ICashService/IPortfolioValueHistoryService so neither of those needs to
/// depend on the other (CashService -> IPortfolioValueHistoryService -> this, with no cycle back).</summary>
public sealed class CashLedgerQueryService(AppDbContext db) : ICashLedgerQueryService
{
    // Instance-level (not static): the service is registered Scoped, so this cache naturally resets
    // per request/scope. A static cache would leak a stale value across different DbContexts/tests.
    private DateOnly? _cachedLedgerStartDate;

    public async Task<decimal> GetTotalAsOfAsync(DateOnly date, string? accountType, CancellationToken ct = default)
    {
        var cutoff = date.ToDateTime(TimeOnly.MinValue);
        var query = db.CashItems.AsNoTracking()
            .Where(c => (c.TransactionDate ?? c.AddedAt).Date <= cutoff);
        if (!string.IsNullOrWhiteSpace(accountType))
            query = query.Where(c => c.AccountType == accountType);
        // Sum client-side (not SumAsync) — decimal SUM doesn't translate on every relational provider
        // (e.g. SQLite, used by tests); row counts here are small so this is cheap on every provider.
        return (await query.Select(c => c.Amount).ToListAsync(ct)).Sum();
    }

    public async Task<DateOnly> GetLedgerStartDateAsync(CancellationToken ct = default)
    {
        if (_cachedLedgerStartDate is { } cached) return cached;

        var settings = await db.CashLedgerSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);
        if (settings is null)
            throw new InvalidOperationException(
                "CashLedgerSettings row (Id=1) is missing — the legacy-to-OpeningBalance migration must run before any ledger query.");

        var startDate = DateOnly.FromDateTime(settings.LedgerStartDate);
        _cachedLedgerStartDate = startDate;
        return startDate;
    }
}
