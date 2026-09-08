namespace PortfolioManager.Api.Services;

/// <summary>
/// Shared status vocabulary for AutomationRunLog fields. Plain string constants (matching this
/// codebase's convention for lifecycle/status fields, e.g. DailySignal.SignalState/ScanType) rather
/// than a C# enum. Distinguishes real failure from expected non-execution so a manual run fired
/// outside business-time windows never reports a false Failed.
/// </summary>
public static class AutomationStatuses
{
    // OverallStatus
    public const string Running = "Running";
    public const string Success = "Success";
    public const string PartialSuccess = "PartialSuccess";
    public const string Failed = "Failed";
    public const string SkippedNonTradingDay = "SkippedNonTradingDay";

    // Per-step statuses (RefreshStatus/RsiStatus/SnapshotStatus/ValueScreenerStatus)
    public const string Succeeded = "Succeeded";
    public const string NotEligible = "NotEligible";
    public const string NotScheduled = "NotScheduled";
    public const string NotObserved = "NotObserved";
}
