namespace PortfolioManager.Api.Models;

/// <summary>
/// PM-029: verified, date-stamped index membership (e.g. TSX 60). Current members have <see cref="EffectiveTo"/> = null.
/// Seeded only from reviewed scripts / Config UI — never hard-coded in services.
/// </summary>
public class IndexConstituent
{
    public int Id { get; set; }
    public string IndexCode { get; set; } = "TSX60";
    public string Symbol { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Source { get; set; } = "";
    public DateTime SourceAsOfDate { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? VerifiedBy { get; set; }
}

/// <summary>PM-029: sector → benchmark (sector ETF) per exchange calendar, used by Sector Heat.</summary>
public class SectorBenchmarkMapping
{
    public int Id { get; set; }
    public string Sector { get; set; } = "";
    /// <summary><see cref="Services.MarketExchange"/> name ("Tsx" / "Nyse").</summary>
    public string Market { get; set; } = "Tsx";
    public string BenchmarkSymbol { get; set; } = "";
    public string BenchmarkName { get; set; } = "";
    public string Currency { get; set; } = "CAD";
    /// <summary>JSON array of provider sector names that map to <see cref="Sector"/>.</summary>
    public string ProviderSectorAliasesJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public string? Source { get; set; }
    public DateTime? SourceAsOfDate { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}

/// <summary>PM-029: immutable opportunity snapshot (grid, drawer and export all read the same row).</summary>
public class OpportunitySnapshot
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string ScoringVersion { get; set; } = "";
    public string? EvaluationId { get; set; }
    public DateTime ComputedAtUtc { get; set; }
    public DateTime? FinalActionComputedAtUtc { get; set; }
    public DateTime DataAsOfSession { get; set; }
    /// <summary>Succeeded | Partial</summary>
    public string Status { get; set; } = "Succeeded";
    public string? ErrorSummary { get; set; }
    public string SnapshotJson { get; set; } = "{}";
}

/// <summary>PM-029: last refresh attempt per user, kept apart so a failure never relabels the last success.</summary>
public class OpportunityRefreshState
{
    public string UserId { get; set; } = "";
    public DateTime? LastAttemptAtUtc { get; set; }
    /// <summary>Running | Succeeded | Partial | Failed</summary>
    public string? LastAttemptStatus { get; set; }
    public string? ErrorSummary { get; set; }
    public int? LastSuccessfulSnapshotId { get; set; }
}
