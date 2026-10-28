using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Risk;

public sealed record RiskPlanDto(
    int Id, string AccountType, string Symbol, decimal? PlannedStopPrice, decimal? TargetPrice, string StopType,
    string? StopRationale, string? TradeRationale, string? RiskNotes, string? Currency, DateTime? EpisodeOpenDate,
    bool BelongsToCurrentEpisode, DateTime UpdatedAtUtc, string RowVersion);

public sealed record BrokerStopDto(
    int Id, Guid OrderIdentity, string OrderType, decimal TriggerPrice, decimal? LimitPrice, string Currency,
    string UserStatus, string BrokerVerificationStatus, decimal? CoveredQuantity, DateTime? PlacedDate,
    string? BrokerOrderRef, DateTime? LastUserVerifiedDate, string ConfirmationSource, string? Notes,
    DateTime? EpisodeOpenDate, bool IsActive, bool BelongsToCurrentEpisode, bool BrokerCancellationUnverified,
    int? ReplacedByRecordId, DateTime UpdatedAtUtc, string RowVersion);

public sealed record RiskAuditEventDto(
    long Id, DateTime OccurredAtUtc, string? UserName, string Source, string EventType,
    IReadOnlyList<AuditChangeDto> Changes, string? Reason, int? BrokerStopRecordId);

public sealed record RiskPositionDto(
    string AccountType, string Symbol, bool HasOpenPosition, decimal TotalQuantity, decimal SwingQuantity, bool MixedRole,
    string EpisodeResolution, string? EpisodeNote, DateTime? EpisodeOpenDate, string Currency, string CurrencyBasis,
    RiskPlanDto? Plan, IReadOnlyList<BrokerStopDto> BrokerStops, IReadOnlyList<RiskAuditEventDto> History);

public sealed record SaveRiskPlanRequest(
    string AccountType, string Symbol, decimal? PlannedStopPrice, decimal? TargetPrice, string? StopType,
    string? StopRationale, string? TradeRationale, string? RiskNotes, string? Reason, string? RowVersion);

public sealed record SaveBrokerStopRequest(
    int? Id, string AccountType, string Symbol, string? OrderType, decimal? TriggerPrice, decimal? LimitPrice,
    decimal? CoveredQuantity, DateTime? PlacedDate, string? BrokerOrderRef, DateTime? LastUserVerifiedDate,
    string? Notes, string? Reason, bool AllowOverCoverage, string? RowVersion);

public sealed record BrokerStopTransitionRequest(string Status, int? ReplacedByRecordId, string? Reason, string? RowVersion);

public sealed record BackfillReportDto(
    bool Applied, int EpisodesPlanned, int LotsAssigned, int Unresolved,
    IReadOnlyList<BackfillEpisode> Episodes, IReadOnlyList<BackfillAssignment> Assignments, IReadOnlyList<BackfillUnresolved> UnresolvedItems);

public sealed record StopSheetRow(string Symbol, decimal TriggerPrice, string? OrderType, string? Currency, string? Account, int RowNumber);
public sealed record StopSheetRequest(string SheetId, string SheetVersion, IReadOnlyList<StopSheetRow> Rows, bool DryRun = true, bool AllowAdditionalOrders = false);

public sealed record StopSheetRowResult(
    int RowNumber, string Symbol, string Action, string? Account, decimal? SwingQuantity, decimal? TotalQuantity,
    string? Currency, string? CurrencyBasis, string? EpisodeResolution, DateTime? EpisodeOpenDate, decimal TriggerPrice,
    string? ExistingStop, string Detail);

public sealed record StopSheetResultDto(bool DryRun, bool Applied, string CorrelationId, int ToImport, int Skipped, int Blocked, IReadOnlyList<StopSheetRowResult> Rows);

public static class StopSheetActions
{
    public const string Import = "Import";
    public const string AlreadyImported = "AlreadyImported";
    public const string Imported = "Imported";
    public const string Unmatched = "Unmatched";
    public const string Ambiguous = "Ambiguous";
    public const string Conflict = "Conflict";
    public const string Invalid = "Invalid";
}
