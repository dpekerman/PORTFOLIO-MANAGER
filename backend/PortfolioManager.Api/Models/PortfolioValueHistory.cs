namespace PortfolioManager.Api.Models;

/// <summary>What last wrote/recomputed a PortfolioValueHistory row. Serialized as a string via the
/// project's global JsonStringEnumConverter.</summary>
public enum PortfolioValueSource
{
    /// <summary>Initial once-daily 4:30 PM ET background timer insert.</summary>
    EodAuto,
    /// <summary>Admin-triggered POST /record-now (full Stocks+Options+Cash recompute).</summary>
    ManualRecordNow,
    /// <summary>Automatic 90s quiet-period reseal of today's row after a same-day mutation.</summary>
    SameDayReseal,
    /// <summary>Cash-only recalculation via RecalculateCashRangeAsync (backdated edits, admin Reconcile
    /// Today, admin Recalculate Cash From Date) — Stocks/Options are never touched by this path.</summary>
    CashRecalculation,
    /// <summary>Backfilled onto pre-existing rows by the migration that introduced this field — true
    /// origin unknown.</summary>
    Migration
}

/// <summary>End-of-day portfolio value snapshot persisted at 4:30 PM ET on trading days.</summary>
public class PortfolioValueHistory
{
    public int Id { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Trading date in ET, formatted as "YYYY-MM-DD".</summary>
    public string RecordedDate { get; set; } = "";

    public decimal TotalValue { get; set; }
    public decimal StocksValue { get; set; }
    public decimal CashValue { get; set; }
    public decimal OptionsValue { get; set; }

    /// <summary>What produced/most-recently-recomputed this row. Historical SnapshotStatus (Original/
    /// Resealed/CashRecalculated) is derived from this plus <see cref="LastRecalculatedAt"/> — never
    /// from live state.</summary>
    public PortfolioValueSource Source { get; set; } = PortfolioValueSource.EodAuto;

    /// <summary>Set only when this row is genuinely recomputed after its first insert (never set on the
    /// initial insert for a date). Null means "Original" — never recomputed since first recorded.</summary>
    public DateTime? LastRecalculatedAt { get; set; }
}

public record PortfolioValueHistoryDto(
    int Id,
    DateTime RecordedAt,
    string RecordedDate,
    decimal TotalValue,
    decimal StocksValue,
    decimal CashValue,
    decimal OptionsValue,
    PortfolioValueSource Source,
    DateTime? LastRecalculatedAt,
    /// <summary>Sum of CashItems on RecordedDate with IsExternalFlow==true. Never inferred from
    /// day-over-day CashValue deltas.</summary>
    decimal ExternalCashFlow,
    /// <summary>Derived from Source/LastRecalculatedAt/RecordedDate for every row except today's, where
    /// it may be overridden to "PendingReseal" based on live IMutationClock state.</summary>
    string SnapshotStatus,
    /// <summary>Simple arithmetic check: true if TotalValue != StocksValue+OptionsValue+CashValue.</summary>
    bool HasMismatch);

public record PortfolioBetaResult(
    decimal PortfolioBeta,
    decimal ExCashBeta,
    decimal CashPct,
    decimal ProxyPct,
    string Status,
    List<BetaContributor> TopContributors);

public record BetaContributor(
    string Symbol,
    decimal WeightPct,
    decimal Beta,
    bool IsProxy);

/// <summary>Request body for POST /api/portfoliobeta/calculate (with optional user-defined beta overrides).</summary>
public class PortfolioBetaRequest
{
    /// <summary>Optional symbol → beta overrides supplied by the user. Keys are case-insensitive symbols (e.g. "PYPL").</summary>
    public Dictionary<string, decimal>? BetaOverrides { get; set; }
}
