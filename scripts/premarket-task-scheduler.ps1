<#
 Registers (or removes) the Windows Task Scheduler entry for the AI pre-market report.
 The task wakes the laptop and runs a pre-published copy of the API in one-shot mode (no web server needed).
 It does NOT touch the EOD tasks. Times are LOCAL Windows time: this laptop is on Eastern Time, so 07:20 = 07:20 Toronto.
 The task starts early; the program then waits for the start time saved in Settings (default 07:30 Toronto).
 The program itself checks Settings (schedule enabled, first run date, weekend, TSX/US holidays) and is idempotent per day.

 Usage:   .\premarket-task-scheduler.ps1 -Action Install   (publishes the current code, then registers the task)
          .\premarket-task-scheduler.ps1 -Action Publish   (re-publish after code updates; keeps the task)
          .\premarket-task-scheduler.ps1 -Action Remove
          .\premarket-task-scheduler.ps1 -Action RunNow    (dry-run with mock news, no email)
          .\premarket-task-scheduler.ps1 -Action Status    (task state, next run, last result, log tail)
#>
param(
    [ValidateSet('Install', 'Publish', 'Remove', 'RunNow', 'Status')] [string]$Action = 'Install',
    [string]$At = '07:20',
    [string]$Environment = 'Development',   # Jwt/SMTP secrets live in user-secrets, which only load in Development
    [string]$ApiProject = (Join-Path $PSScriptRoot '..\backend\PortfolioManager.Api'),
    [string]$PublishDir = 'D:\AI-portfolio-manager\premarket-bin'
)

$taskName = 'PortfolioManager-PreMarketAI'
$project  = (Resolve-Path $ApiProject).Path
$log      = Join-Path $PublishDir 'premarket-task.log'

function Publish-App {
    New-Item -ItemType Directory -Force $PublishDir | Out-Null
    dotnet publish $project -c Release -o $PublishDir --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
    Write-Host "Published to $PublishDir"
}

switch ($Action) {
    { $_ -in 'Install', 'Publish' } {
        Publish-App
        if ($Action -eq 'Publish') { break }
        $cmd = "cd /d `"$PublishDir`" && set ASPNETCORE_ENVIRONMENT=$Environment&& dotnet PortfolioManager.Api.dll --premarket-run --send-email >> `"$log`" 2>&1"
        $act = New-ScheduledTaskAction -Execute 'cmd.exe' -Argument "/c $cmd"
        $trg = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday, Tuesday, Wednesday, Thursday, Friday -At $At
        $set = New-ScheduledTaskSettingsSet -WakeToRun -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
               -ExecutionTimeLimit (New-TimeSpan -Hours 2) -MultipleInstances IgnoreNew
        Register-ScheduledTask -TaskName $taskName -Action $act -Trigger $trg -Settings $set -Force | Out-Null
        Write-Host "Installed '$taskName' (weekdays $At, wake to run, catch-up if missed). Log: $log"
    }
    'Remove' {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
        Write-Host "Removed '$taskName'."
    }
    'RunNow' {
        $env:ASPNETCORE_ENVIRONMENT = $Environment
        Push-Location $PublishDir
        dotnet PortfolioManager.Api.dll --premarket-run --test --dry-run --mock-news
        Pop-Location
    }
    'Status' {
        $t = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        if (-not $t) { Write-Host 'Task is not installed.'; break }
        $i = Get-ScheduledTaskInfo -TaskName $taskName
        Write-Host "State: $($t.State)  Next run: $($i.NextRunTime)  Last run: $($i.LastRunTime)  Last result: $($i.LastTaskResult)"
        if (Test-Path $log) { Get-Content $log -Tail 5 }
    }
}
