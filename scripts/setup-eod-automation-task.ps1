#Requires -RunAsAdministrator
<#
.SYNOPSIS
    One-time (or repair) setup: registers a Wake-enabled Windows Scheduled Task that fires the
    EOD Automation trigger script daily at the configured wake time.

.DESCRIPTION
    This script ONLY registers/updates the Scheduled Task itself - it does NOT run automation.
    The Task's action is scripts/run-eod-automation-trigger.ps1, which health-checks the backend,
    starts it if needed, and POSTs to /api/automation/trigger.

    Also creates the "PortfolioManagerApi" Windows Event Log source (if missing), piggybacking on
    this script's existing elevation - no separate UAC prompt. Once created, the backend can write
    diagnostic logs to Event Viewer (Windows Logs -> Application, Source = PortfolioManagerApi) even
    when it's the hidden/detached process the trigger script starts.

    Must be run elevated (Administrator) - required to set "Wake the computer to run this task".
    Can be invoked directly, or via the Configuration -> Automation -> "Enable Automation" /
    "Repair Automation" buttons (which shell out to this script with a UAC prompt).

.PARAMETER WakeTimeEt
    HH:mm in Eastern Time - when the task fires. Defaults to 15:15 (matches AutomationRuntimeConfig's
    default WakeTimeEt). Pass the current value from GET /api/automation/settings if it has been
    changed from the default.

.PARAMETER SafetyNetTimeEt
    HH:mm in Eastern Time for a second daily trigger - defaults to 16:45 (after both the RSI EOD
    window and Snapshot window have opened, matching EodAutomationOrchestratorService's own 16:45
    fallback). Added 2026-09-16 after the primary WakeTimeEt trigger silently failed to fire on
    2026-09-15 with zero Task Scheduler trace. A second independent trigger re-POSTs to the same
    /api/automation/trigger endpoint; every underlying write it observes/persists is idempotent, so
    this is safe even on a day the primary trigger already succeeded.

.PARAMETER TaskName
    Scheduled Task name. Must match what GET /api/automation/task-status queries.
#>
param(
    [string]$WakeTimeEt = "15:15",
    [string]$SafetyNetTimeEt = "16:45",
    [string]$TaskName = "PortfolioManagerEodAutomation"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$triggerScript = Join-Path $scriptDir "run-eod-automation-trigger.ps1"

if (-not (Test-Path $triggerScript)) {
    Write-Error "Trigger script not found: $triggerScript"
    exit 1
}

# Create the Event Log source once, while we already have elevation - lets the backend write
# diagnostic logs visible in Event Viewer even as a hidden/detached process (see Program.cs).
if (-not [System.Diagnostics.EventLog]::SourceExists("PortfolioManagerApi")) {
    Write-Host "Creating Windows Event Log source 'PortfolioManagerApi'."
    [System.Diagnostics.EventLog]::CreateEventSource("PortfolioManagerApi", "Application")
} else {
    Write-Host "Event Log source 'PortfolioManagerApi' already exists."
}

# Wake Timers must be allowed on the active power plan, or Task Scheduler's -WakeToRun silently
# does nothing (the machine simply never wakes). This is NOT on by default on most Windows
# installs - enable it now for both AC and battery, on the currently active plan, while we
# already have elevation. Non-fatal if it fails (e.g. setting hidden on this SKU).
try {
    & powercfg /setacvalueindex SCHEME_CURRENT SUB_SLEEP RTCWAKE 1
    & powercfg /setdcvalueindex SCHEME_CURRENT SUB_SLEEP RTCWAKE 1
    & powercfg /setactive SCHEME_CURRENT
    Write-Host "Wake Timers enabled on the active power plan (AC and battery)."
} catch {
    Write-Warning "Could not set 'Allow wake timers' automatically: $_. Verify manually via powercfg /q SCHEME_CURRENT SUB_SLEEP RTCWAKE."
}

# Parse "HH:mm" as Eastern Time and anchor the trigger's StartBoundary to an explicit UTC offset -
# this is the PowerShell equivalent of Task Scheduler's "Synchronize across time zones" checkbox.
# WITHOUT this, New-ScheduledTaskTrigger's default StartBoundary has NO offset ("floating" local
# time), meaning the daily trigger silently follows whatever Windows local timezone the machine is
# LATER switched to (e.g. while traveling) rather than staying anchored to Eastern Time. Verify this
# actually worked via GET /api/automation/timezone-diagnostics (Automation tab) - it independently
# recomputes the expected Eastern Time next-run and compares it to the Task Scheduler's real one.
#
# KNOWN LIMITATION: the UTC offset baked in here is fixed at registration time (EDT -04:00 or EST
# -05:00, whichever applies right now) - Windows does not dynamically re-derive DST for a named zone
# on a synchronized trigger. If a US Eastern DST transition occurs while this task stays registered
# unmodified, the trigger will drift by exactly 1 hour until "Repair Automation" is re-run. Re-run it
# once shortly after each DST change (~2nd Sunday of March, 1st Sunday of November) as routine hygiene.
try { $easternTz = [System.TimeZoneInfo]::FindSystemTimeZoneById("Eastern Standard Time") }
catch { $easternTz = [System.TimeZoneInfo]::FindSystemTimeZoneById("America/New_York") }

function New-AnchoredDailyTrigger {
    param([string]$TimeEt)
    $timeOfDay = [DateTime]::ParseExact($TimeEt, "HH:mm", $null).TimeOfDay
    $nowEt = [System.TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow, $easternTz)
    $todayEt = $nowEt.Date.Add($timeOfDay)
    $offset = $easternTz.GetUtcOffset($todayEt)
    $offsetStr = "{0}{1:hh\:mm}" -f $(if ($offset -ge [TimeSpan]::Zero) { "+" } else { "-" }), $offset.Duration()
    $boundary = "{0:yyyy-MM-ddTHH:mm:ss}{1}" -f $todayEt, $offsetStr
    Write-Host "Anchoring trigger to Eastern Time: $boundary (synchronized across time zones)."
    $t = New-ScheduledTaskTrigger -Daily -At $todayEt
    $t.StartBoundary = $boundary
    return $t
}

$action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$triggerScript`""

# Two independent daily triggers - WakeTimeEt (primary) and SafetyNetTimeEt (backup). Added after
# the primary trigger silently failed to fire on 2026-09-15 with no Task Scheduler diagnostic trace
# at all. The safety-net run is harmless on a day the primary already succeeded: RefreshAllAsync is
# just an extra quote refresh, and RSI/Snapshot/ValueScreener are only ever observed here, not
# written - their actual persistence is upsert-by-day regardless of how many times it's checked.
$trigger = @(
    (New-AnchoredDailyTrigger -TimeEt $WakeTimeEt),
    (New-AnchoredDailyTrigger -TimeEt $SafetyNetTimeEt)
)

# WakeToRun: allows Task Scheduler to wake a sleeping machine to run this task.
# StartWhenAvailable: if the machine was off/asleep past the trigger time, run as soon as possible.
# RestartCount/RestartInterval: if the trigger script exits non-zero (e.g. backend didn't become
# healthy in time right after a sleep/wake cycle), Task Scheduler retries up to 3 more times, 5
# minutes apart, before giving up for the day — this is what actually failed on 2026-09-08 (a
# single failed health-check silently ended the whole day's automation with no retry).
$settings = New-ScheduledTaskSettingsSet -WakeToRun -StartWhenAvailable `
    -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
    -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 5)

# "Run whether user is logged on or not" via S4U (Service-for-User) - no stored password required
# and no interactive desktop session needed, unlike the earlier "Interactive" logon type. This
# fixes the failure mode where the daily run is silently missed whenever the machine is off or
# nobody is logged in at the wake time (e.g. while traveling) - S4U still works as long as the
# machine is powered on. Trade-off: no access to network resources that need the user's own
# credentials - not a problem here since the task only calls the backend over localhost.
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType S4U -RunLevel Limited

$task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal $principal

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Write-Host "Task '$TaskName' already exists - updating in place."
    Set-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal | Out-Null
} else {
    Write-Host "Registering new task '$TaskName' at $WakeTimeEt ET and $SafetyNetTimeEt ET daily."
    Register-ScheduledTask -TaskName $TaskName -InputObject $task | Out-Null
}

Write-Host "Done. Verify with: Get-ScheduledTask -TaskName '$TaskName' | Get-ScheduledTaskInfo"
Write-Host "Verify Eastern Time anchoring is correct via the Automation tab's Timezone Diagnostics panel"
Write-Host "(GET /api/automation/timezone-diagnostics) - it flags drift if the trigger and Eastern Time"
Write-Host "wake config ever disagree, which a display-only fix could never catch."
Write-Host "View automation logs in Event Viewer: Windows Logs -> Application, Source = PortfolioManagerApi"
Write-Host ""
Write-Host "Prerequisites to verify manually on this machine:"
Write-Host "  - This machine's supported sleep state: run 'powercfg /a' - if it lists only"
Write-Host "    'Standby (S0 Low Power Idle)' (Modern Standby), wake behavior differs from classic S3;"
Write-Host "    Modern Standby keeps networking alive, which actually helps this scenario."
Write-Host "  - Laptop should stay PLUGGED IN during the automation window - Modern Standby draws"
Write-Host "    continuous (small) power, unlike S3's near-zero draw, and this machine has no"
Write-Host "    Hibernate fallback configured for a critically low battery."
Write-Host "  - 'Run only when user is logged on' means automation will NOT fire if you are fully"
Write-Host "    logged out (not just asleep) - do not sign out, only let it sleep."
