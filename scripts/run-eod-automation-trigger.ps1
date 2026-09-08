<#
.SYNOPSIS
    Scheduled Task action: ensures the Portfolio Manager backend is running, then triggers one
    EOD automation run. NOT elevated — runs as the logged-in user.

.DESCRIPTION
    1. GET /api/health
    2. If unreachable: starts the PUBLISHED Release build as a detached, hidden process (NOT
       `dotnet run`, not dependent on this script or a terminal staying open).
    3. Polls /api/health with bounded retries (2s interval, 60s cap). This readiness check is the
       SOLE authoritative success signal — regardless of whether this script's own Start-Process
       call ended up being the surviving instance or exited immediately because the backend's own
       single-instance Mutex/port-bind guard rejected a duplicate.
    4. Reads + DPAPI-unprotects the local automation secret and POSTs to /api/automation/trigger.

.PARAMETER PublishDir
    Folder containing the published PortfolioManager.Api.dll (from `dotnet publish -c Release`).
    Defaults to backend/PortfolioManager.Api/publish relative to this script's repo.

.PARAMETER BaseUrl
    Base URL of the backend. Defaults to http://localhost:5000 (matches start-backend.bat).
#>
param(
    [string]$PublishDir = (Join-Path (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)) "backend\PortfolioManager.Api\publish"),
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"
$logPrefix = "[EodAutomationTrigger]"

function Write-Log($message) {
    Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $logPrefix $message"
}

function Test-Healthy {
    try {
        $resp = Invoke-WebRequest -Uri "$BaseUrl/api/health" -UseBasicParsing -TimeoutSec 5
        return $resp.StatusCode -eq 200
    } catch {
        return $false
    }
}

# ── Step 1: health-check ──────────────────────────────────────────────────────
if (Test-Healthy) {
    Write-Log "Backend already healthy at $BaseUrl."
} else {
    Write-Log "Backend not reachable — attempting to start the published build."

    $dllPath = Join-Path $PublishDir "PortfolioManager.Api.dll"
    if (-not (Test-Path $dllPath)) {
        Write-Log "ERROR: Published build not found at $dllPath. Run 'dotnet publish -c Release -o `"$PublishDir`"' first. Aborting."
        exit 1
    }

    # Fire-and-forget, detached, hidden. Exit code is NOT inspected — a redundant duplicate
    # instance is expected to exit immediately via the backend's own single-instance Mutex/port
    # guard; the health-check polling below is the only signal that matters.
    Start-Process -FilePath "dotnet" -ArgumentList "`"$dllPath`"" -WorkingDirectory $PublishDir -WindowStyle Hidden

    # ── Step 2/3: bounded health-check retries (2s interval, 60s cap) ────────
    $deadline = (Get-Date).AddSeconds(60)
    $healthy = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        if (Test-Healthy) { $healthy = $true; break }
    }

    if (-not $healthy) {
        Write-Log "ERROR: Backend did not become healthy within 60s. Aborting trigger."
        exit 1
    }
    Write-Log "Backend became healthy."
}

# ── Step 4: read + DPAPI-unprotect the automation secret, POST the trigger ───
Add-Type -AssemblyName System.Security

$secretPath = Join-Path $env:LOCALAPPDATA "PortfolioManager\automation-secret.protected"
if (-not (Test-Path $secretPath)) {
    Write-Log "ERROR: No automation secret found at $secretPath. Run setup/rotate-secret from Configuration -> Automation first. Aborting."
    exit 1
}

$protectedBytes = [System.IO.File]::ReadAllBytes($secretPath)
$secretBytes = [System.Security.Cryptography.ProtectedData]::Unprotect(
    $protectedBytes, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
$secretBase64 = [Convert]::ToBase64String($secretBytes)

try {
    $resp = Invoke-WebRequest -Uri "$BaseUrl/api/automation/trigger" -Method Post `
        -Headers @{ "X-Automation-Key" = $secretBase64 } -UseBasicParsing -TimeoutSec 15
    Write-Log "Trigger POST returned $($resp.StatusCode): $($resp.Content)"
} catch {
    Write-Log "ERROR: Trigger POST failed: $($_.Exception.Message)"
    exit 1
}
