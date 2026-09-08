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
