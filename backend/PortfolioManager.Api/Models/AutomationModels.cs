namespace PortfolioManager.Api.Models;

public record AutomationTriggerResponseDto(Guid RunId);

/// <summary>Editable Automation (machine wake/keep-awake) fields plus a read-only echo of the
/// existing EOD Window / Value Screener business-time config for display — never duplicated in
/// storage, always sourced live from ScannerRuntimeConfig / ValueScreenerScheduleConfig.</summary>
public record AutomationSettingsDto(
    bool Enabled,
    string WakeTimeEt,
    string? KeepAwakeUntilEtOverride,
    string ComputedKeepAwakeUntilEt,
    int CompletionGraceMinutes,
    int MaxPollMinutes,
    string EodWindowStartEt,
    string EodWindowEndEt,
    bool EodWindowEnabled,
    string ValueScreenerScheduledTimeEt,
    bool ValueScreenerEnabled,
    bool SecretConfigured);

public record UpdateAutomationSettingsRequest(
    bool Enabled,
    string WakeTimeEt,
    string? KeepAwakeUntilEtOverride,
    int CompletionGraceMinutes,
    int MaxPollMinutes);

public record AutomationTaskStatusDto(
    bool Exists,
    string? State,
    DateTime? NextRunTime,
    DateTime? LastRunTime,
    int? LastTaskResult);

public record AutomationTriggerDiagnosticDto(
    string[] Entries,
    AutomationRunLogDto? MatchingRun);

/// <summary>Diagnostic/display-only comparison of the business timezone (Eastern, fixed) against the
/// Windows local timezone the machine currently happens to be set to — plus a live check of whether
/// the Scheduled Task's actual next-run instant still matches what Eastern Time business config
/// expects. Never affects scheduling logic; a mismatch means the underlying Task Scheduler trigger
/// itself is timezone-drifted, not just a display artifact.</summary>
public record AutomationTimezoneDiagnosticsDto(
    string BusinessTimeZoneLabel,
    string WindowsLocalTimeZoneLabel,
    string WakeTimeEtDisplay,
    string WakeTimeLocalDisplay,
    bool TaskExists,
    string? TaskNextRunEtDisplay,
    string? TaskNextRunLocalDisplay,
    string ExpectedNextRunEtDisplay,
    bool? TaskMatchesExpected,
    double? DriftMinutes,
    string? Warning);
