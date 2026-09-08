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
        const string query = "Get-ScheduledTask -TaskName 'PortfolioManagerEodAutomation' -ErrorAction SilentlyContinue | " +
            "ForEach-Object { $info = $_ | Get-ScheduledTaskInfo; [PSCustomObject]@{ " +
            "Exists = $true; State = $_.State.ToString(); NextRunTime = $info.NextRunTime; " +
            "LastRunTime = $info.LastRunTime; LastTaskResult = $info.LastTaskResult } } | ConvertTo-Json -Compress";

        var (exitCode, stdout, _) = await RunPowerShellAsync(query, ct);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return Ok(new AutomationTaskStatusDto(false, null, null, null, null));

        try
        {
            var parsed = JsonSerializer.Deserialize<AutomationTaskStatusDto>(stdout,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Ok(parsed ?? new AutomationTaskStatusDto(false, null, null, null, null));
        }
        catch
        {
            return Ok(new AutomationTaskStatusDto(false, null, null, null, null));
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
