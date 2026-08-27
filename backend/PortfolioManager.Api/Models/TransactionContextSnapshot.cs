namespace PortfolioManager.Api.Models;

/// <summary>Market context captured automatically when a transaction is opened.</summary>
public class TransactionContextSnapshot
{
    public int Id { get; set; }
    /// <summary>FK to PortfolioItems.Id (the opening transaction record).</summary>
    public int TransactionId { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    // ── Technical context at entry ────────────────────────────────────────────
    public decimal? RsiAtEntry { get; set; }
    public string? TrendShiftAtEntry { get; set; }
    public string? FibZoneAtEntry { get; set; }
    public string? VolumeSignalAtEntry { get; set; }
    public string? TurnStrengthAtEntry { get; set; }

    // ── Fundamental context at entry ─────────────────────────────────────────
    public decimal? ValueScoreAtEntry { get; set; }
    public string? ValueTierAtEntry { get; set; }

    // ── Portfolio context at entry ────────────────────────────────────────────
    public string? HoldingRoleAtEntry { get; set; }
    /// <summary>"over" | "under" | "on-target" — sector deviation vs allocation target.</summary>
    public string? SectorAllocationStatusAtEntry { get; set; }
}
