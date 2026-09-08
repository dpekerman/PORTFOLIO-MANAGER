#Requires -RunAsAdministrator
<#
.SYNOPSIS
    One-time (or repair) setup: registers a Wake-enabled Windows Scheduled Task that fires the
    EOD Automation trigger script daily at the configured wake time.

.DESCRIPTION
    This script ONLY registers/updates the Scheduled Task itself — it does NOT run automation.
    The Task's action is scripts/run-eod-automation-trigger.ps1, which health-checks the backend,
    starts it if needed, and POSTs to /api/automation/trigger.

    Must be run elevated (Administrator) — required to set "Wake the computer to run this task".
    Can be invoked directly, or via the Configuration -> Automation -> "Enable Automation" /
    "Repair Automation" buttons (which shell out to this script with a UAC prompt).

.PARAMETER WakeTimeEt
    HH:mm in Eastern Time — when the task fires. Defaults to 15:15 (matches AutomationRuntimeConfig's
    default WakeTimeEt). Pass the current value from GET /api/automation/settings if it has been
    changed from the default.

.PARAMETER TaskName
    Scheduled Task name. Must match what GET /api/automation/task-status queries.
#>
param(
    [string]$WakeTimeEt = "15:15",
    [string]$TaskName = "PortfolioManagerEodAutomation"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$triggerScript = Join-Path $scriptDir "run-eod-automation-trigger.ps1"

if (-not (Test-Path $triggerScript)) {
    Write-Error "Trigger script not found: $triggerScript"
    exit 1
}

# Parse "HH:mm" as a local wall-clock time for Register-ScheduledTaskTrigger. The Scheduled Task
# system uses the machine's local time zone; this repo assumes the machine is set to Eastern Time
# (documented prerequisite) since business-time config (EOD Window, Value Screener) is Eastern-only.
$parsed = [DateTime]::ParseExact($WakeTimeEt, "HH:mm", $null)

$action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$triggerScript`""

$trigger = New-ScheduledTaskTrigger -Daily -At $parsed

# WakeToRun: allows Task Scheduler to wake a sleeping machine to run this task.
# StartWhenAvailable: if the machine was off/asleep past the trigger time, run as soon as possible.
$settings = New-ScheduledTaskSettingsSet -WakeToRun -StartWhenAvailable `
    -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Hours 2)

# "Run only when user is logged on" (round-4 decision — simplest, no stored credentials; accepted
# tradeoff that automation won't fire if fully logged out). Uses the currently logged-in user.
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

$task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal $principal

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Write-Host "Task '$TaskName' already exists — updating in place."
    Set-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal | Out-Null
} else {
    Write-Host "Registering new task '$TaskName' at $WakeTimeEt ET daily."
    Register-ScheduledTask -TaskName $TaskName -InputObject $task | Out-Null
}

Write-Host "Done. Verify with: Get-ScheduledTask -TaskName '$TaskName' | Get-ScheduledTaskInfo"
Write-Host ""
Write-Host "Prerequisites to verify manually on this machine:"
Write-Host "  - Windows 'Wake Timers' allowed (Power Options -> plan settings -> Advanced -> Sleep -> Allow wake timers: Enable)"
Write-Host "  - Laptop is plugged in / lid setting won't prevent wake-from-sleep at the scheduled time"
Write-Host "  - 'Run only when user is logged on' means automation will NOT fire if you are fully logged out (not just asleep)"
