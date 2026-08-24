param(
    [int]$Year = (Get-Date).Year,
    [int]$Month = (Get-Date).Month,
    [string]$GitHubOwner = "dpekerman",
    [string]$GitHubToken = $env:GITHUB_TOKEN,
    [decimal]$CopilotMonthlyUsd = 10
)

$ErrorActionPreference = "Stop"

function Write-Section($title) {
    Write-Host ""
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

function Write-Money($label, $amount, $currency = "USD") {
    if ($null -eq $amount) {
        Write-Host ("{0,-34} unavailable" -f $label) -ForegroundColor DarkGray
    } else {
        Write-Host ("{0,-34} {1} {2:N2}" -f $label, $currency, [decimal]$amount)
    }
}

function ConvertFrom-SecureToken($token) {
    if ($token -is [System.Security.SecureString]) {
        $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($token)
        try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    }
    return [string]$token
}

$from = Get-Date -Year $Year -Month $Month -Day 1 -Hour 0 -Minute 0 -Second 0
$to = $from.AddMonths(1)

Write-Host "Portfolio Manager monthly cost summary"
Write-Host ("Period: {0:yyyy-MM-dd} to {1:yyyy-MM-dd}" -f $from, $to.AddDays(-1))

# Azure actual cost and budgets
Write-Section "Azure"
try {
    Import-Module Az.Accounts -ErrorAction Stop
    $context = Get-AzContext
    if (-not $context) {
        Write-Host "Not signed in to Azure. Run: Connect-AzAccount" -ForegroundColor Yellow
    } else {
        $subscriptionId = $context.Subscription.Id
        $token = ConvertFrom-SecureToken (Get-AzAccessToken -ResourceUrl "https://management.azure.com").Token
        $headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

        $body = @{
            type = "ActualCost"
            timeframe = "Custom"
            timePeriod = @{
                from = $from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
                to = $to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            }
            dataset = @{
                granularity = "None"
                aggregation = @{
                    totalCost = @{ name = "PreTaxCost"; function = "Sum" }
                }
            }
        } | ConvertTo-Json -Depth 10

        $uri = "https://management.azure.com/subscriptions/$subscriptionId/providers/Microsoft.CostManagement/query?api-version=2023-03-01"
        $cost = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $body
        $row = $cost.properties.rows | Select-Object -First 1
        $azureAmount = if ($row) { $row[0] } else { 0 }
        $azureCurrency = if ($row -and $row.Count -gt 1) { $row[1] } else { "USD" }
        Write-Money "Actual cost this month" $azureAmount $azureCurrency

        try {
            $budgetsUri = "https://management.azure.com/subscriptions/$subscriptionId/providers/Microsoft.Consumption/budgets?api-version=2023-05-01"
            $budgets = Invoke-RestMethod -Method Get -Uri $budgetsUri -Headers $headers
            if ($budgets.value.Count -eq 0) {
                Write-Host "Budgets                            none configured" -ForegroundColor Yellow
            } else {
                foreach ($budget in $budgets.value) {
                    $amount = $budget.properties.amount
                    $grain = $budget.properties.timeGrain
                    Write-Money "Budget: $($budget.name) ($grain)" $amount $azureCurrency
                }
            }
        } catch {
            Write-Host "Budgets                            unavailable: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "Azure cost unavailable: $($_.Exception.Message)" -ForegroundColor Yellow
}

# GitHub billing usage
Write-Section "GitHub"
if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
    Write-Host "GitHub billing unavailable: set GITHUB_TOKEN with billing access." -ForegroundColor Yellow
    Write-Host "Example: `$env:GITHUB_TOKEN='ghp_...' ; .\scripts\show-monthly-costs.ps1"
} else {
    $ghHeaders = @{
        Authorization = "Bearer $GitHubToken"
        Accept = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
        "User-Agent" = "portfolio-manager-cost-script"
    }

    foreach ($endpoint in @("actions", "packages", "shared-storage")) {
        try {
            $url = "https://api.github.com/users/$GitHubOwner/settings/billing/$endpoint"
            $result = Invoke-RestMethod -Method Get -Uri $url -Headers $ghHeaders
            Write-Host ""
            Write-Host "[$endpoint]"
            $result.PSObject.Properties | ForEach-Object {
                Write-Host ("  {0,-34} {1}" -f $_.Name, $_.Value)
            }
        } catch {
            Write-Host "[$endpoint] unavailable: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

# Copilot subscription estimate
Write-Section "Copilot"
Write-Money "Estimated Copilot subscription" $CopilotMonthlyUsd "USD"
Write-Host "Note: GitHub does not expose personal Copilot subscription charges through the public user billing API."
Write-Host "Set -CopilotMonthlyUsd 0, 10, or 39 depending on your plan."

Write-Section "Summary"
Write-Host "Azure actual cost is pulled live from Azure Cost Management."
Write-Host "GitHub usage requires GITHUB_TOKEN. Copilot is an estimate parameter."
