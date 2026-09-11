<#
.SYNOPSIS
    Scheduled Task action: ensures the Portfolio Manager backend is running, then triggers one
    EOD automation run. NOT elevated - runs as the logged-in user.

.DESCRIPTION
    1. GET /api/health
    2. If unreachable: checks whether a stale/hung process is already bound to port 5000 (e.g. a
       pre-sleep instance that survived a sleep/resume cycle in a broken, unresponsive state) and
       force-kills it, since the app's own single-instance Mutex would otherwise make a fresh
       Start-Process silently no-op against that zombie instead of actually recovering it.
    3. Starts the PUBLISHED Release build as a detached, hidden process (NOT `dotnet run`, not
       dependent on this script or a terminal staying open).
    4. Polls /api/health with bounded retries (2s interval, 60s cap). This readiness check is the
       SOLE authoritative success signal.
    5. Reads + DPAPI-unprotects the local automation secret and POSTs to /api/automation/trigger.

    On any failure exit, sends a best-effort alert email directly via SMTP (does not depend on the
    backend being reachable), deduped to at most one alert per Eastern-Time calendar day so a
    Task-Scheduler retry storm (see RestartCount in setup-eod-automation-task.ps1) doesn't spam
    the inbox. This is what makes a fully-missed day visible the SAME day instead of the next one.

.PARAMETER PublishDir
    Folder containing the published PortfolioManager.Api.dll (from `dotnet publish -c Release`).
    Defaults to backend/PortfolioManager.Api/publish relative to this script's repo.

.PARAMETER BaseUrl
    Base URL of the backend. Defaults to http://localhost:5000 (matches start-backend.bat).
#>
param(
    [string]$PublishDir = (Join-Path (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)) "backend\PortfolioManager.Api\publish"),
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$TestWake
)

$ErrorActionPreference = "Stop"
$logPrefix = "[EodAutomationTrigger]"
$repoDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$backendProjectDir = Join-Path $repoDir "backend\PortfolioManager.Api"
$correlationId = [Guid]::NewGuid().ToString("N")
$logDirectory = Join-Path $env:LOCALAPPDATA "PortfolioManager\logs"
$logFile = Join-Path $logDirectory "eod-automation-trigger-$(Get-Date -Format 'yyyy-MM-dd').log"

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

function Write-Log($message) {
    $entry = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff') $logPrefix [$correlationId] $message"
    Write-Host $entry
    Add-Content -LiteralPath $logFile -Value $entry -Encoding utf8
}

Write-Log "Trigger script started (test wake: $TestWake)."

function Test-Healthy {
    try {
        $resp = Invoke-WebRequest -Uri "$BaseUrl/api/health" -UseBasicParsing -TimeoutSec 5
        return $resp.StatusCode -eq 200
    } catch {
        return $false
    }
}

function Get-BackendSourceLastWriteTimeUtc {
    $files = @(
        Get-ChildItem -Path $backendProjectDir -Recurse -File -ErrorAction Stop |
            Where-Object {
                $_.FullName -notmatch "\\(bin|obj|publish)(\\|$)" -and
                ($_.Extension -eq ".cs" -or $_.Name -eq "PortfolioManager.Api.csproj" -or $_.Name -like "appsettings*.json")
            }
    )
    if ($files.Count -eq 0) { return [DateTime]::MinValue.ToUniversalTime() }
    return ($files | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
}

function Ensure-PublishedBuildCurrent {
    param([string]$DllPath)

    $sourceTime = Get-BackendSourceLastWriteTimeUtc
    $publishedTime = if (Test-Path -LiteralPath $DllPath) {
        (Get-Item -LiteralPath $DllPath).LastWriteTimeUtc
    } else {
        [DateTime]::MinValue.ToUniversalTime()
    }

    if ($publishedTime -ge $sourceTime) {
        Write-Log "Published backend is current ($publishedTime UTC)."
        return $false
    }

    Write-Log "Published backend is stale ($publishedTime UTC; source $sourceTime UTC). Publishing latest Release build."
    Push-Location $backendProjectDir
    try {
        & dotnet publish -c Release -o $PublishDir --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE."
        }
    } finally {
        Pop-Location
    }
    Write-Log "Latest backend published successfully."
    return $true
}

# Detects a process already bound to the backend's port that is NOT answering /api/health (e.g. a
# pre-sleep instance left in a broken state by a sleep/resume cycle) and force-kills it. Without
# this, Start-Process below would spawn a redundant duplicate that immediately exits via the app's
# own single-instance Mutex, leaving the original unresponsive process still holding the port -
# exactly what happened on 2026-09-08 (health-check never passed within the 60s window).
function Clear-HungPortOwner {
    param([int]$Port)
    try {
        $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
        foreach ($conn in $conns) {
            $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
            if ($proc -and $proc.ProcessName -like "PortfolioManager.Api*") {
                Write-Log "Found unresponsive '$($proc.ProcessName)' (PID $($proc.Id)) holding port $Port but failing /api/health - force-killing it."
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 2
            }
        }
    } catch {
        Write-Log "WARNING: Clear-HungPortOwner check failed (non-fatal): $($_.Exception.Message)"
    }
}

# Best-effort SMTP alert, independent of backend health (uses .NET's built-in SmtpClient - no
# MailKit dependency needed here). Reads the same EmailNotification/notification-recipients config
# the backend itself uses. Never throws - a failure to send the alert must not mask the original
# automation failure with a script crash.
function Send-FailureAlert {
    param([string]$Reason)

    try {
        $alertMarker = Join-Path $env:LOCALAPPDATA "PortfolioManager\last-trigger-failure-alert.txt"
        $etZone = [System.TimeZoneInfo]::FindSystemTimeZoneById("Eastern Standard Time")
        $todayEt = [System.TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow, $etZone).ToString("yyyy-MM-dd")

        if ((Test-Path $alertMarker) -and (Get-Content $alertMarker -Raw -ErrorAction SilentlyContinue).Trim() -eq $todayEt) {
            Write-Log "Failure alert already sent today ($todayEt) - skipping duplicate email."
            return
        }

        $cfgPath = Join-Path $PublishDir "appsettings.Development.json"
        if (-not (Test-Path $cfgPath)) { $cfgPath = Join-Path $PublishDir "appsettings.json" }
        $recipientsPath = Join-Path $PublishDir "notification-recipients.json"

        if (-not (Test-Path $cfgPath) -or -not (Test-Path $recipientsPath)) {
            Write-Log "WARNING: Cannot send failure alert - config not found ($cfgPath or $recipientsPath)."
            return
        }

        $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
        $email = $cfg.EmailNotification
        $recipients = (Get-Content $recipientsPath -Raw | ConvertFrom-Json).Emails

        if (-not $email -or -not $email.Enabled -or [string]::IsNullOrWhiteSpace($email.Username) `
                -or [string]::IsNullOrWhiteSpace($email.Password) -or -not $recipients -or $recipients.Count -eq 0) {
            Write-Log "WARNING: Email not configured/enabled - skipping failure alert."
            return
        }

        $mail = New-Object System.Net.Mail.MailMessage
        $mail.From = New-Object System.Net.Mail.MailAddress($email.FromAddress, $email.FromName)
        foreach ($to in $recipients) { $mail.To.Add($to) }
        $mail.Subject = "Portfolio Manager - EOD Automation FAILED ($todayEt)"
        $mail.IsBodyHtml = $false
        $mail.Body = @"
The scheduled EOD automation trigger failed on $todayEt and could not reach the backend.

Reason: $Reason

Today's data may be missing. Open Configuration -> Automation and click "Fix Missing Data"
to persist EOD Signals, Portfolio Snapshot and Value Screener for today in one click.

(This is an automated message from run-eod-automation-trigger.ps1. At most one alert is sent
per calendar day even if the Scheduled Task retries.)
"@

        $smtp = New-Object System.Net.Mail.SmtpClient($email.SmtpHost, [int]$email.SmtpPort)
        $smtp.EnableSsl = [bool]$email.UseStartTls
        $smtp.Credentials = New-Object System.Net.NetworkCredential($email.Username, $email.Password)
        $smtp.Send($mail)

        New-Item -ItemType Directory -Path (Split-Path $alertMarker) -Force -ErrorAction SilentlyContinue | Out-Null
        Set-Content -Path $alertMarker -Value $todayEt -NoNewline
        Write-Log "Failure alert email sent to $($recipients -join ', ')."
    } catch {
        Write-Log "WARNING: Failed to send failure alert email (non-fatal): $($_.Exception.Message)"
    }
}

# ── Step 1: refresh published build, then health-check ─────────────────────────
$dllPath = Join-Path $PublishDir "PortfolioManager.Api.dll"
$publishedWasRefreshed = $false
try {
    $publishedWasRefreshed = Ensure-PublishedBuildCurrent -DllPath $dllPath
} catch {
    $reason = "Latest backend publish failed: $($_.Exception.Message)"
    Write-Log "ERROR: $reason Aborting trigger."
    Send-FailureAlert -Reason $reason
    exit 1
}

if (Test-Healthy -and -not $publishedWasRefreshed) {
    Write-Log "Backend already healthy at $BaseUrl."
} else {
    if ($publishedWasRefreshed -and (Test-Healthy)) {
        $uri = [Uri]$BaseUrl
        $healthyOwner = Get-NetTCPConnection -LocalPort $uri.Port -State Listen -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($healthyOwner) {
            $healthyProcess = Get-Process -Id $healthyOwner.OwningProcess -ErrorAction SilentlyContinue
            if ($healthyProcess -and $healthyProcess.ProcessName -like "PortfolioManager.Api*") {
                Write-Log "Published build changed; restarting the healthy backend so the scheduled run uses the latest code."
                Stop-Process -Id $healthyProcess.Id -Force -ErrorAction Stop
                Start-Sleep -Seconds 2
            }
        }
    }

    Write-Log "Backend not reachable or was refreshed - checking for a hung process before starting the published build."

    $uri = [Uri]$BaseUrl
    Clear-HungPortOwner -Port $uri.Port

    if (-not (Test-Path $dllPath)) {
        $reason = "Published build not found at $dllPath. Run 'dotnet publish -c Release -o `"$PublishDir`"' first."
        Write-Log "ERROR: $reason Aborting."
        Send-FailureAlert -Reason $reason
        exit 1
    }

    # Fire-and-forget, detached, hidden. Exit code is NOT inspected - a redundant duplicate
    # instance is expected to exit immediately via the backend's own single-instance Mutex/port
    # guard; the health-check polling below is the only signal that matters.
    #
    # ASPNETCORE_ENVIRONMENT=Development is REQUIRED here: without it .NET defaults to
    # "Production", which (a) never loads dotnet user-secrets (Jwt:Secret lives ONLY there - see
    # Program.cs's explicit throw "Jwt:Secret is not configured") so the published build crashed
    # instantly on startup, and (b) layers in appsettings.Production.json's blank
    # ConnectionStrings:DefaultConnection over the working one in the base appsettings.json. This
    # was the REAL reason the 2026-09-08 recovery attempt's health-check never passed within 60s -
    # confirmed by reproducing it directly: `dotnet PortfolioManager.Api.dll` crashed immediately
    # with "Jwt:Secret is not configured" the moment ASPNETCORE_ENVIRONMENT was unset.
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    Start-Process -FilePath "dotnet" -ArgumentList "`"$dllPath`"" -WorkingDirectory $PublishDir -WindowStyle Hidden

    # ── Step 2/3: bounded health-check retries (2s interval, 60s cap) ────────
    $deadline = (Get-Date).AddSeconds(60)
    $healthy = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        if (Test-Healthy) { $healthy = $true; break }
    }

    if (-not $healthy) {
        $reason = "Backend did not become healthy within 60s after Start-Process."
        Write-Log "ERROR: $reason Aborting trigger."
        Send-FailureAlert -Reason $reason
        exit 1
    }
    Write-Log "Backend became healthy."
}

# ── Step 4: read + DPAPI-unprotect the automation secret, POST the trigger ───
Add-Type -AssemblyName System.Security

$secretPath = Join-Path $env:LOCALAPPDATA "PortfolioManager\automation-secret.protected"
if (-not (Test-Path $secretPath)) {
    $reason = "No automation secret found at $secretPath. Run setup/rotate-secret from Configuration -> Automation first."
    Write-Log "ERROR: $reason Aborting."
    Send-FailureAlert -Reason $reason
    exit 1
}

$protectedBytes = [System.IO.File]::ReadAllBytes($secretPath)
$secretBytes = [System.Security.Cryptography.ProtectedData]::Unprotect(
    $protectedBytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
$secretBase64 = [Convert]::ToBase64String($secretBytes)

try {
    $triggerEndpoint = if ($TestWake) { "trigger-test" } else { "trigger" }
    $resp = Invoke-WebRequest -Uri "$BaseUrl/api/automation/$triggerEndpoint" -Method Post `
        -Headers @{ "X-Automation-Key" = $secretBase64; "X-Automation-Correlation-Id" = $correlationId } `
        -UseBasicParsing -TimeoutSec 15
    Write-Log "Trigger POST returned $($resp.StatusCode): $($resp.Content)"
} catch {
    $reason = "Trigger POST failed: $($_.Exception.Message)"
    Write-Log "ERROR: $reason"
    Send-FailureAlert -Reason $reason
    exit 1
}
