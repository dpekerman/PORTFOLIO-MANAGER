# Portfolio Manager - Data Migration Script
# Exports all business data from local SQL Server and optionally imports to Azure SQL
#
# Usage:
#   Generate SQL file only:
#     .\migrate-local-to-azure.ps1
#
#   Generate and import to Azure:
#     .\migrate-local-to-azure.ps1 -ImportToAzure -AzureConnectionString "Server=tcp:..."
#
#   Wipe Azure tables first, then import (for re-runs):
#     .\migrate-local-to-azure.ps1 -ImportToAzure -CleanFirst -AzureConnectionString "Server=tcp:..."

param(
    [string]$LocalServer            = "localhost",
    [string]$LocalDatabase          = "PortfolioManagerDb",
    [switch]$ImportToAzure,
    [switch]$CleanFirst,
    [string]$AzureConnectionString  = ""
)

$outputFile = Join-Path $PSScriptRoot ("migration-output-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + ".sql")

# Business tables only - in FK-safe insertion order.
# Identity/auth tables (AspNetUsers, AspNetRoles, etc.) are excluded intentionally.
$tables = @(
    "AllocationRiskTargets",
    "AllocationSectorTargets",
    "SinglePositionLimits",
    "AdhocAnalysisSessions",
    "PortfolioItems",
    "WatchlistItems",
    "CashItems",
    "OptionItems",
    "DailySignals",
    "StagedSignals",
    "ValueScreenerScheduleConfigs",
    "ValueScreenerSnapshots",
    "PortfolioValueHistories"
)

function Format-SqlValue($value, $typeName) {
    if ($null -eq $value -or $value -is [DBNull]) { return "NULL" }
    switch -Wildcard ($typeName) {
        "int*"          { return $value.ToString() }
        "bigint"        { return $value.ToString() }
        "smallint"      { return $value.ToString() }
        "tinyint"       { return $value.ToString() }
        "bit"           { if ($value) { return "1" } else { return "0" } }
        "decimal"       { return $value.ToString() }
        "numeric"       { return $value.ToString() }
        "float"         { return $value.ToString() }
        "real"          { return $value.ToString() }
        "money"         { return $value.ToString() }
        "date"          { return "'" + $value.ToString("yyyy-MM-dd") + "'" }
        "time"          { return "'" + $value.ToString("HH:mm:ss.fffffff") + "'" }
        "datetime*"     { return "'" + $value.ToString("yyyy-MM-dd HH:mm:ss.fffffff") + "'" }
        "smalldatetime" { return "'" + $value.ToString("yyyy-MM-dd HH:mm:ss") + "'" }
        default         { return "N'" + $value.ToString().Replace("'", "''") + "'" }
    }
}

# ---- Connect to local SQL --------------------------------------------------
Write-Host "Connecting to local SQL Server ($LocalServer/$LocalDatabase)..." -ForegroundColor Cyan
$localConn = New-Object System.Data.SqlClient.SqlConnection(
    "Server=$LocalServer;Database=$LocalDatabase;Trusted_Connection=True;TrustServerCertificate=True;")
try {
    $localConn.Open()
} catch {
    Write-Error "Cannot connect to local SQL Server: $_"
    exit 1
}

# ---- Generate SQL script ---------------------------------------------------
$writer = [System.IO.StreamWriter]::new($outputFile, $false, [System.Text.Encoding]::UTF8)
$writer.WriteLine("-- Portfolio Manager Data Migration")
$writer.WriteLine("-- Generated : " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$writer.WriteLine("-- Source    : $LocalServer / $LocalDatabase")
$writer.WriteLine("-- Tables    : " + $tables.Count + " business tables (no identity/auth data)")
$writer.WriteLine("")
$writer.WriteLine("SET NOCOUNT ON;")
$writer.WriteLine("SET XACT_ABORT ON;")
$writer.WriteLine("BEGIN TRANSACTION;")
$writer.WriteLine("")

$totalRows = 0

foreach ($table in $tables) {
    $cmd = $localConn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM [$table]"
    $count = [int]$cmd.ExecuteScalar()

    if ($count -eq 0) {
        Write-Host ("  " + $table + " : 0 rows - skipped") -ForegroundColor DarkGray
        $writer.WriteLine("-- [$table] : 0 rows - skipped")
        $writer.WriteLine("")
        continue
    }

    Write-Host ("  " + $table + " : " + $count + " rows") -ForegroundColor Green

    # Get column metadata
    $metaQuery = "SELECT COLUMN_NAME, DATA_TYPE, COLUMNPROPERTY(OBJECT_ID('$table'), COLUMN_NAME, 'IsIdentity') AS IsIdentity FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$table' ORDER BY ORDINAL_POSITION"
    $cmd.CommandText = $metaQuery
    $metaReader = $cmd.ExecuteReader()
    $cols = @()
    while ($metaReader.Read()) {
        $cols += [pscustomobject]@{
            Name       = $metaReader[0]
            Type       = $metaReader[1]
            IsIdentity = [bool]($metaReader[2])
        }
    }
    $metaReader.Close()

    $hasIdentity = ($cols | Where-Object { $_.IsIdentity }).Count -gt 0
    $colList = ($cols | ForEach-Object { "[$($_.Name)]" }) -join ", "

    $writer.WriteLine("-- [$table] : $count rows")
    if ($CleanFirst) {
        $writer.WriteLine("DELETE FROM [$table];")
    }
    if ($hasIdentity) { $writer.WriteLine("SET IDENTITY_INSERT [$table] ON;") }

    $cmd.CommandText = "SELECT * FROM [$table] ORDER BY (SELECT NULL)"
    $dataReader = $cmd.ExecuteReader()
    while ($dataReader.Read()) {
        $vals = @()
        for ($i = 0; $i -lt $dataReader.FieldCount; $i++) {
            $val = if ($dataReader.IsDBNull($i)) { $null } else { $dataReader.GetValue($i) }
            $vals += Format-SqlValue $val $cols[$i].Type
        }
        $writer.WriteLine("INSERT INTO [$table] ($colList) VALUES (" + ($vals -join ", ") + ");")
    }
    $dataReader.Close()

    if ($hasIdentity) { $writer.WriteLine("SET IDENTITY_INSERT [$table] OFF;") }
    $writer.WriteLine("")
    $totalRows += $count
}

$writer.WriteLine("COMMIT TRANSACTION;")
$writer.WriteLine("-- Migration complete: $totalRows total rows")
$writer.Flush()
$writer.Close()
$localConn.Close()

$fileSize = [Math]::Round((Get-Item $outputFile).Length / 1KB, 1)
Write-Host ""
Write-Host ("Migration script saved: " + $outputFile + " (" + $fileSize + " KB, " + $totalRows + " rows)") -ForegroundColor Yellow
Write-Host ""

# ---- Optional: import to Azure ---------------------------------------------
if (-not $ImportToAzure) {
    Write-Host "Review the file, then re-run with -ImportToAzure -AzureConnectionString '...' to import." -ForegroundColor Cyan
    exit 0
}

if ([string]::IsNullOrWhiteSpace($AzureConnectionString)) {
    Write-Error "-AzureConnectionString is required when using -ImportToAzure"
    exit 1
}

Write-Host "Connecting to Azure SQL..." -ForegroundColor Cyan
$azureConn = New-Object System.Data.SqlClient.SqlConnection($AzureConnectionString)
try {
    $azureConn.Open()
} catch {
    Write-Error "Cannot connect to Azure SQL: $_"
    exit 1
}

Write-Host ("Importing " + $totalRows + " rows to Azure SQL...") -ForegroundColor Cyan
$sql = [System.IO.File]::ReadAllText($outputFile)

$azureCmd = $azureConn.CreateCommand()
$azureCmd.CommandTimeout = 300
$azureCmd.CommandText = $sql
try {
    $azureCmd.ExecuteNonQuery() | Out-Null
    Write-Host ("Import complete. " + $totalRows + " rows written to Azure SQL.") -ForegroundColor Green
} catch {
    Write-Error ("Import failed: " + $_)
    Write-Host "Tip: open the generated .sql file in Azure portal Query Editor and run it there instead." -ForegroundColor Yellow
}
$azureConn.Close()
