namespace PortfolioManager.Api.Models;

/// <summary>
/// Persistent identity of one trade (open → additions → partial sales → full exit). A later re-open of the
/// same security is a NEW episode. Ownership mirrors the lots it groups (UserId may be null for legacy rows).
/// </summary>
public class TradeEpisode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public DateTime OpenDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    /// <summary>Open | Closed</summary>
    public string Status { get; set; } = EpisodeStatuses.Open;
    /// <summary>Assigned (created by a live transaction) | Backfilled (unambiguous history).</summary>
    public string Source { get; set; } = EpisodeSources.Assigned;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>The user's DESIRED risk configuration for an account holding. Never implies a broker order exists.</summary>
public class RiskPlan
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal? PlannedStopPrice { get; set; }
    public decimal? TargetPrice { get; set; }
    /// <summary>Fixed | Trailing (stored and displayed only; no trailing calculation is performed).</summary>
    public string StopType { get; set; } = StopTypes.Fixed;
    public string? StopRationale { get; set; }
    public string? TradeRationale { get; set; }
    public string? RiskNotes { get; set; }
    public string? Currency { get; set; }
    public Guid? TradeEpisodeId { get; set; }
    public DateTime? EpisodeOpenDate { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// What the USER reports about a real brokerage order. Never verified electronically, never deleted
/// (only status transitions). CoveredQuantity null = unknown (never 0, never "full").
/// </summary>
public class BrokerStopRecord
{
    public int Id { get; set; }
    /// <summary>Stable identity of the real broker order; two genuine identical orders get two identities.</summary>
    public Guid OrderIdentity { get; set; } = Guid.NewGuid();
    /// <summary>"{sheetId}:{sheetVersion}:{row}" for imported records; unique when present.</summary>
    public string? ImportKey { get; set; }
    public string? UserId { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string OrderType { get; set; } = BrokerOrderTypes.StopMarket;
    public decimal TriggerPrice { get; set; }
    public decimal? LimitPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string UserStatus { get; set; } = BrokerUserStatuses.UserConfirmedEstablished;
    /// <summary>Unverified | Verified — only an independent source may set Verified.</summary>
    public string BrokerVerificationStatus { get; set; } = BrokerVerification.Unverified;
    public decimal? CoveredQuantity { get; set; }
    public DateTime? PlacedDate { get; set; }
    public string? BrokerOrderRef { get; set; }
    public DateTime? LastUserVerifiedDate { get; set; }
    public string ConfirmationSource { get; set; } = ConfirmationSources.ManualEntry;
    public string? Notes { get; set; }
    public Guid? TradeEpisodeId { get; set; }
    /// <summary>Open date of the trade episode the order belongs to; an order never applies to a different episode.</summary>
    public DateTime? EpisodeOpenDate { get; set; }
    public int? ReplacedByRecordId { get; set; }
    /// <summary>Set when the trade closed in Portfolio Manager: the app cannot know the broker order was cancelled.</summary>
    public bool BrokerCancellationUnverified { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[]? RowVersion { get; set; }
}

/// <summary>Append-only history of risk-plan / broker-record changes.</summary>
public class RiskAuditEvent
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? TradeEpisodeId { get; set; }
    /// <summary>User | Import | System</summary>
    public string Source { get; set; } = RiskAuditSources.User;
    public string Symbol { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public int? RiskPlanId { get; set; }
    public int? BrokerStopRecordId { get; set; }
    public RiskPlan? RiskPlan { get; set; }
    public BrokerStopRecord? BrokerStopRecord { get; set; }
    public string EventType { get; set; } = string.Empty;
    /// <summary>JSON array of { field, old, new }.</summary>
    public string? ChangesJson { get; set; }
    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }
}

public static class EpisodeStatuses { public const string Open = "Open"; public const string Closed = "Closed"; }
public static class EpisodeSources { public const string Assigned = "Assigned"; public const string Backfilled = "Backfilled"; }
public static class StopTypes { public const string Fixed = "Fixed"; public const string Trailing = "Trailing"; }

public static class BrokerOrderTypes
{
    public const string StopMarket = "StopMarket";
    public const string StopLimit = "StopLimit";
    public const string Other = "Other";
    public static readonly string[] All = [StopMarket, StopLimit, Other];
}

public static class BrokerUserStatuses
{
    public const string UserConfirmedEstablished = "UserConfirmedEstablished";
    public const string UserReportedCancelled = "UserReportedCancelled";
    public const string UserReportedFilled = "UserReportedFilled";
    public const string Superseded = "Superseded";
    /// <summary>The trade closed in Portfolio Manager; the broker order's real state is unknown.</summary>
    public const string EpisodeClosed = "EpisodeClosed";
    public static readonly string[] Terminal = [UserReportedCancelled, UserReportedFilled, Superseded, EpisodeClosed];
    public static bool IsActive(string status) => !Terminal.Contains(status);
}

public static class BrokerVerification { public const string Unverified = "Unverified"; public const string Verified = "Verified"; }
public static class ConfirmationSources { public const string UserSuppliedStopSheet = "UserSuppliedStopSheet"; public const string ManualEntry = "ManualEntry"; }
public static class RiskAuditSources { public const string User = "User"; public const string Import = "Import"; public const string System = "System"; }

public static class CoverageStates
{
    public const string Unknown = "Unknown";
    public const string None = "None";
    public const string Partial = "Partial";
    public const string Full = "Full";
    public const string OverCovered = "OverCovered";
    /// <summary>Account holds non-Swing lots too, so the order cannot be allocated to the Swing exposure.</summary>
    public const string Ambiguous = "Ambiguous";
}

public static class RiskStatuses
{
    public const string StopBreached = "StopBreached";
    public const string ApproachingStop = "ApproachingStop";
    public const string MissingRiskPlan = "MissingRiskPlan";
    public const string NoBrokerProtection = "NoBrokerProtection";
    public const string PartialProtection = "PartialProtection";
    public const string UnknownCoverage = "UnknownCoverage";
    public const string AmbiguousCoverage = "AmbiguousCoverage";
    public const string OverCovered = "OverCovered";
    public const string StopMismatch = "StopMismatch";
    public const string StaleEvidence = "StaleEvidence";
}
