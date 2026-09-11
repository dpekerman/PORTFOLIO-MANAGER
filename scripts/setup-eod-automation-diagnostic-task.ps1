#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Registers a one-time, non-destructive Modern Standby diagnostic task for Portfolio Manager.

.DESCRIPTION
    The temporary task runs the production trigger script with -TestWake. That path starts or
    health-checks the published backend, validates SQL connectivity, reads one ^GSPC quote, and
    writes one AutomationRunLogs audit row with durable correlation diagnostics. It does not run
    RefreshAllAsync, recover data, or write portfolio, signal, or screener records.

    This script neither changes nor deletes PortfolioManagerEodAutomation. Remove the temporary
    task afterward with: Unregister-ScheduledTask -TaskName 'PortfolioManagerEodAutomationDiagnostic' -Confirm:$false

.PARAMETER TestTimeEt
    Eastern/Toronto wall-clock time today in HH:mm. Must be at least five minutes ahead.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^([01]\d|2[0-3]):[0-5]\d$')]
    [string]$TestTimeEt
)

$ErrorActionPreference = "Stop"
$taskName = "PortfolioManagerEodAutomationDiagnostic"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$triggerScript = Join-Path $scriptDir "run-eod-automation-trigger.ps1"

if (-not (Test-Path -LiteralPath $triggerScript)) {
    throw "Trigger script not found: $triggerScript"
}

try { $easternTz = [TimeZoneInfo]::FindSystemTimeZoneById("Eastern Standard Time") }
catch { $easternTz = [TimeZoneInfo]::FindSystemTimeZoneById("America/New_York") }

$timeOfDay = [DateTime]::ParseExact($TestTimeEt, "HH:mm", $null).TimeOfDay
$nowEt = [TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow, $easternTz)
$scheduledEt = $nowEt.Date.Add($timeOfDay)
if ($scheduledEt -le $nowEt.AddMinutes(5)) {
    throw "TestTimeEt must be at least five minutes in the future in Toronto time. Current Toronto time: $($nowEt.ToString('HH:mm'))."
}

$offset = $easternTz.GetUtcOffset($scheduledEt)
$offsetText = "{0}{1:hh\:mm}" -f $(if ($offset -ge [TimeSpan]::Zero) { "+" } else { "-" }), $offset.Duration()
$startBoundary = "{0:yyyy-MM-ddTHH:mm:ss}{1}" -f $scheduledEt, $offsetText

$action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$triggerScript`" -TestWake"
$trigger = New-ScheduledTaskTrigger -Once -At $scheduledEt
$trigger.StartBoundary = $startBoundary
$settings = New-ScheduledTaskSettingsSet -WakeToRun -StartWhenAvailable -DontStopOnIdleEnd `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 10)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited
$task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal $principal

if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Set-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal | Out-Null
} else {
    Register-ScheduledTask -TaskName $taskName -InputObject $task | Out-Null
}

Write-Host "Registered $taskName for $($scheduledEt.ToString('yyyy-MM-dd h:mm tt')) Toronto time."
Write-Host "Production task PortfolioManagerEodAutomation was not changed."
Write-Host "Verify: Get-ScheduledTask -TaskName '$taskName' | Get-ScheduledTaskInfo"
