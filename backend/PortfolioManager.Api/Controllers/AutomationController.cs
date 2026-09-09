using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/automation")]
public class AutomationController(
    AppDbContext db,
    IAutomationRunCoordinator coordinator,
    IAutomationSecretStore secretStore,
    AutomationRuntimeConfig automationConfig,
    ScannerRuntimeConfig scannerConfig,
    ValueScreenerPersistenceService valueScreenerPersistence,
    IMissedDataRecoveryService missedDataRecovery,
    IDatabaseBackupService databaseBackup,
    IConfiguration configuration,
    ILogger<AutomationController> logger) : ControllerBase
{
    private static readonly string DefaultScriptsDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts"));

    private string ScriptsDirectory =>
        configuration["Automation:ScriptsDirectory"] is { Length: > 0 } configured ? configured : DefaultScriptsDir;

    // ── Scheduled-Task-facing trigger — NOT a JWT endpoint ───────────────────
    // Deliberately NOT [Authorize]: the Windows Scheduled Task has no user login session. Guarded
    // instead by loopback-only + a DPAPI-protected local secret (see AutomationSecretStore).
    [AllowAnonymous]
    [HttpPost("trigger")]
    public async Task<ActionResult<AutomationTriggerResponseDto>> Trigger(CancellationToken ct)
    {
        if (!IsLoopbackRequest())
        {
            logger.LogWarning("[AutomationController] /trigger rejected: non-loopback caller.");
            return Forbid();
        }

        var header = Request.Headers["X-Automation-Key"].FirstOrDefault();
        if (!await secretStore.ValidateAsync(header, ct))
        {
            logger.LogWarning("[AutomationController] /trigger rejected: invalid or missing secret.");
            return Forbid();
        }

        if (!automationConfig.Enabled)
        {
            // "Automation enabled" gates only the unattended Scheduled-Task path. Manual admin
            // actions (Run Automation Now / Test Wake) below are unaffected — an admin explicitly
            // clicking those buttons should always work regardless of this toggle.
            logger.LogInformation("[AutomationController] /trigger ignored: automation is disabled in settings.");
            return Ok(new { started = false, reason = "disabled" });
        }

        var runId = coordinator.StartRun("Scheduled");
        return Accepted(new AutomationTriggerResponseDto(runId));
    }

    // ── UI-facing (Admin only) ────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpPost("run-now")]
    public ActionResult<AutomationTriggerResponseDto> RunNow()
    {
        // "Run Automation Now" — identical orchestrator path as the Scheduled trigger. Never
        // bypasses RSI EOD Window / Snapshot eligibility / Value Screener schedule; it evaluates
        // and reports real eligibility exactly like a scheduled run would at this moment.
        var runId = coordinator.StartRun("ManualRunNow");
        return Accepted(new AutomationTriggerResponseDto(runId));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("test-wake")]
    public ActionResult<AutomationTriggerResponseDto> TestWake()
    {
        // Non-destructive: keep-awake + DB connectivity + one cheap read-only quote call only.
        // Never calls RefreshAllAsync, never touches RSI/signals/snapshot/cash/transactions.
        var runId = coordinator.StartTestWake();
        return Accepted(new AutomationTriggerResponseDto(runId));
    }

    // "Fix Missing Data" — one-click recovery for a day the scheduled automation failed to run.
    // Runs synchronously (not via the run-coordinator/AutomationRunLog poll pattern above): each
    // step is already a fast, existing, individually-idempotent write (see
    // MissedDataRecoveryService), so a single request/response round-trip is sufficient.
    [Authorize(Roles = "Admin")]
    [HttpPost("recover-missed-data")]
    public async Task<ActionResult<MissedDataRecoveryResult>> RecoverMissedData(CancellationToken ct)
    {
        var result = await missedDataRecovery.RecoverTodayAsync(ct);
        return result.Status == "AlreadyRunning" ? Conflict(result) : Ok(result);
    }

    // On-demand "Backup Now" — unlike the scheduled background timer, this always produces a new
    // backup: if today's default file already exists, a new timestamped file is added alongside it
    // instead of being skipped, so every manual click is preserved.
    [Authorize(Roles = "Admin")]
    [HttpPost("backup-now")]
    public async Task<ActionResult<DatabaseBackupResult>> BackupNow(CancellationToken ct)
    {
        var result = await databaseBackup.RunManualBackupAsync(ct);
        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("cancel")]
    public ActionResult Cancel()
    {
        var cancelled = coordinator.CancelCurrent();
        return Ok(new { cancelled });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("status/{runId:guid}")]
    public async Task<ActionResult<AutomationRunLogDto>> GetStatus(Guid runId, CancellationToken ct)
    {
        var log = await db.AutomationRunLogs.AsNoTracking().FirstOrDefaultAsync(r => r.RunId == runId, ct);
        return log is null ? NotFound() : Ok(ToDto(log));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("last-run")]
    public async Task<ActionResult<AutomationRunLogDto>> GetLastRun(CancellationToken ct)
    {
        var log = await db.AutomationRunLogs.AsNoTracking()
            .OrderByDescending(r => r.ActualStartUtc)
            .FirstOrDefaultAsync(ct);
        return log is null ? NotFound() : Ok(ToDto(log));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<AutomationRunLogDto>>> GetHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var logs = await db.AutomationRunLogs.AsNoTracking()
            .OrderByDescending(r => r.ActualStartUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(logs.Select(ToDto).ToList());
    }

    // Clears only the AutomationRunLogs audit table (this feature's own run history) — never
    // touches PortfolioValueHistories, DailySignals, cash/transactions, or any other financial data.
    [Authorize(Roles = "Admin")]
    [HttpDelete("history")]
    public async Task<ActionResult> ClearHistory(CancellationToken ct)
    {
        if (coordinator.IsRunning)
            return Conflict(new { message = "Cannot clear history while a run is in progress." });

        var count = await db.AutomationRunLogs.ExecuteDeleteAsync(ct);
        logger.LogInformation("[AutomationController] Cleared {Count} AutomationRunLog row(s).", count);
        return Ok(new { cleared = true, count });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("rotate-secret")]
    public async Task<ActionResult> RotateSecret(CancellationToken ct)
    {
        await secretStore.GenerateAndStoreAsync(ct);
        // Never echo the plaintext value — the trigger script reads the protected file directly.
        return Ok(new { rotated = true });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("settings")]
    public ActionResult<AutomationSettingsDto> GetSettings()
    {
        return Ok(BuildSettingsDto());
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("settings")]
    public ActionResult<AutomationSettingsDto> UpdateSettings([FromBody] UpdateAutomationSettingsRequest request)
    {
        automationConfig.Enabled = request.Enabled;
        if (!string.IsNullOrWhiteSpace(request.WakeTimeEt)) automationConfig.WakeTimeEt = request.WakeTimeEt;
        automationConfig.KeepAwakeUntilEtOverride = string.IsNullOrWhiteSpace(request.KeepAwakeUntilEtOverride)
            ? null : request.KeepAwakeUntilEtOverride;
        if (request.CompletionGraceMinutes > 0) automationConfig.CompletionGraceMinutes = request.CompletionGraceMinutes;
        if (request.MaxPollMinutes > 0) automationConfig.MaxPollMinutes = request.MaxPollMinutes;
        automationConfig.SaveToFile();

        return Ok(BuildSettingsDto());
    }

    private AutomationSettingsDto BuildSettingsDto()
    {
        var vsCfg = valueScreenerPersistence.GetOrCreateScheduleConfigAsync().GetAwaiter().GetResult();
        return new AutomationSettingsDto(
            automationConfig.Enabled,
            automationConfig.WakeTimeEt,
            automationConfig.KeepAwakeUntilEtOverride,
            automationConfig.ComputeKeepAwakeUntilEt(scannerConfig.EodWindowEnd, vsCfg.ScheduledTimeEt),
            automationConfig.CompletionGraceMinutes,
            automationConfig.MaxPollMinutes,
            scannerConfig.EodWindowStart,
            scannerConfig.EodWindowEnd,
            scannerConfig.EodWindowEnabled,
            vsCfg.ScheduledTimeEt,
            vsCfg.Enabled,
            secretStore.Exists());
    }

    // ── Windows Scheduled Task management ─────────────────────────────────────
    [Authorize(Roles = "Admin")]
    [HttpGet("task-status")]
    public async Task<ActionResult<AutomationTaskStatusDto>> GetTaskStatus(CancellationToken ct)
    {
        return Ok(await QueryTaskStatusAsync(ct));
    }

    /// <summary>Diagnostic/display-only: compares the fixed business timezone (Eastern) against
    /// whatever the Windows local timezone currently is, and verifies the Scheduled Task's actual
    /// next-run instant (not just its displayed local time) still matches what Eastern Time wake
    /// config expects. Never changes scheduling logic or business config.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("timezone-diagnostics")]
    public async Task<ActionResult<AutomationTimezoneDiagnosticsDto>> GetTimezoneDiagnostics(CancellationToken ct)
    {
        var easternTz = MarketHoursGate.GetEasternTimeZone();
        var localTz = TimeZoneInfo.Local;

        if (easternTz is null)
        {
            return Ok(new AutomationTimezoneDiagnosticsDto(
                "Eastern Time (unavailable on this machine)", localTz.DisplayName,
                automationConfig.WakeTimeEt, "unknown", false, null, null, "unknown", null, null,
                "Eastern Time zone data is not available on this machine — cannot verify."));
        }

        var wakeTimeOfDay = TimeSpan.TryParse(automationConfig.WakeTimeEt, out var t) ? t : new TimeSpan(15, 15, 0);
        var nowUtc = DateTime.UtcNow;
        var nowEt = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, easternTz);

        // Next occurrence of the configured wake time, in Eastern local time, converted to UTC.
        var todayWakeEt = nowEt.Date.Add(wakeTimeOfDay);
        var nextWakeEt = todayWakeEt > nowEt ? todayWakeEt : todayWakeEt.AddDays(1);
        var expectedNextRunUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextWakeEt, DateTimeKind.Unspecified), easternTz);
        var expectedNextRunLocal = TimeZoneInfo.ConvertTimeFromUtc(expectedNextRunUtc, localTz);

        var wakeTimeTodayEt = nowEt.Date.Add(wakeTimeOfDay);
        var wakeTimeTodayUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(wakeTimeTodayEt, DateTimeKind.Unspecified), easternTz);
        var wakeTimeTodayLocal = TimeZoneInfo.ConvertTimeFromUtc(wakeTimeTodayUtc, localTz);

        var task = await QueryTaskStatusAsync(ct);

        string? taskNextRunEtDisplay = null;
        string? taskNextRunLocalDisplay = null;
        bool? matches = null;
        double? driftMinutes = null;
        string? warning = null;

        if (task.Exists && task.NextRunTime.HasValue)
        {
            // NextRunTime came back as an ISO-8601 string with the machine's CURRENT local UTC
            // offset already baked in (see QueryTaskStatusAsync) — DateTime.Kind may be Unspecified
            // after JSON round-trip, so re-anchor it as Local before converting, matching how it was
            // produced (Get-ScheduledTaskInfo returns Kind=Local DateTimes).
            var taskNextRunLocal = DateTime.SpecifyKind(task.NextRunTime.Value, DateTimeKind.Local);
            var taskNextRunUtc = taskNextRunLocal.ToUniversalTime();
            var taskNextRunEt = TimeZoneInfo.ConvertTimeFromUtc(taskNextRunUtc, easternTz);

            taskNextRunEtDisplay = $"{taskNextRunEt:MMM d, h:mm tt} ET";
            taskNextRunLocalDisplay = $"{taskNextRunLocal:MMM d, h:mm tt} computer local";

            driftMinutes = Math.Abs((taskNextRunUtc - expectedNextRunUtc).TotalMinutes);
            matches = driftMinutes <= 2;
            if (matches == false)
            {
                warning = $"The Scheduled Task's actual next-run instant is off by ~{driftMinutes:F0} " +
                    "minute(s) from what the Eastern Time wake config expects. This means the underlying " +
                    "Task Scheduler trigger itself has drifted from Eastern Time (not just a display issue) " +
                    "— re-run Repair Automation to re-anchor it.";
            }
        }
        else if (task.Exists)
        {
            warning = "Task exists but Task Scheduler did not report a next-run time — try Repair Automation.";
        }

        return Ok(new AutomationTimezoneDiagnosticsDto(
            "Eastern Time (America/Toronto / America/New_York)",
            $"{localTz.DisplayName} ({localTz.Id})",
            $"{wakeTimeTodayEt:h:mm tt} ET",
            $"{wakeTimeTodayLocal:h:mm tt} computer local",
            task.Exists,
            taskNextRunEtDisplay,
            taskNextRunLocalDisplay,
            $"{nextWakeEt:MMM d, h:mm tt} ET",
            matches,
            driftMinutes,
            warning));
    }

    private async Task<AutomationTaskStatusDto> QueryTaskStatusAsync(CancellationToken ct)
    {
        // PowerShell's ConvertTo-Json serializes [DateTime] as legacy "/Date(ticks)/" — System.Text.Json
        // can't parse that into DateTime?, so dates must be formatted as ISO-8601 strings here first.
        const string query = "Get-ScheduledTask -TaskName 'PortfolioManagerEodAutomation' -ErrorAction SilentlyContinue | " +
            "ForEach-Object { $info = $_ | Get-ScheduledTaskInfo; " +
            "$next = if ($info.NextRunTime) { $info.NextRunTime.ToString('o') } else { $null }; " +
            "$last = if ($info.LastRunTime) { $info.LastRunTime.ToString('o') } else { $null }; " +
            "[PSCustomObject]@{ Exists = $true; State = $_.State.ToString(); NextRunTime = $next; " +
            "LastRunTime = $last; LastTaskResult = $info.LastTaskResult } } | ConvertTo-Json -Compress";

        var (exitCode, stdout, _) = await RunPowerShellAsync(query, ct);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return new AutomationTaskStatusDto(false, null, null, null, null);

        try
        {
            var parsed = JsonSerializer.Deserialize<AutomationTaskStatusDto>(stdout,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return parsed ?? new AutomationTaskStatusDto(false, null, null, null, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AutomationController] Failed to parse task-status PowerShell output: {Stdout}", stdout);
            return new AutomationTaskStatusDto(false, null, null, null, null);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("setup")]
    public async Task<ActionResult> Setup(CancellationToken ct)
    {
        if (!secretStore.Exists())
            await secretStore.GenerateAndStoreAsync(ct);

        var scriptPath = Path.Combine(ScriptsDirectory, "setup-eod-automation-task.ps1");
        if (!System.IO.File.Exists(scriptPath))
            return Problem($"Setup script not found at {scriptPath}.", statusCode: StatusCodes.Status500InternalServerError);

        try
        {
            // One-time elevation: this spawns a NEW elevated child process just for Scheduled Task
            // registration (works because this API runs as a normal desktop process, not a Windows
            // Service) — the running API itself never needs elevated privileges.
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -WakeTimeEt \"{automationConfig.WakeTimeEt}\"",
                UseShellExecute = true,
                Verb = "runas",
            };
            using var process = Process.Start(psi);
            return Ok(new { started = true, elevated = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AutomationController] Failed to launch elevated setup script.");
            return Problem("Failed to launch the elevated setup script (UAC may have been declined).",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunPowerShellAsync(
        string command, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start powershell.exe");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private bool IsLoopbackRequest()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null) return false;
        return IPAddress.IsLoopback(remoteIp) || remoteIp.Equals(IPAddress.IPv6Loopback);
    }

    private static AutomationRunLogDto ToDto(AutomationRunLog r) => new(
        r.RunId, r.TradingDate, r.TriggerType, r.ScheduledStartUtc, r.ActualStartUtc, r.CompletedAtUtc,
        r.OverallStatus, r.RefreshStatus, r.RefreshStartedAtUtc, r.RefreshCompletedAtUtc,
        r.PortfolioSymbolCount, r.WatchlistSymbolCount, r.RsiStatus, r.RsiCompletedAtUtc,
        r.EodSignalsPersistedCount, r.SnapshotStatus, r.SnapshotCompletedAtUtc, r.SnapshotSource,
        r.ValueScreenerStatus, r.ValueScreenerLastRunAtUtc, r.PowerRequestAcquiredAtUtc,
        r.PowerRequestReleasedAtUtc, r.ErrorStep, r.ErrorMessage, r.MachineName);
}
