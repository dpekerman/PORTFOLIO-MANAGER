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
    "PortfolioValueHistories",
    "UserPreferences"
    # Snapshot tables (RsiScanSnapshots, PortfolioSnapshots, WatchlistSnapshots) are intentionally
    # excluded - they are ephemeral caches that regenerate automatically on first page load after deploy.
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

    # Exclude identity columns - let Azure SQL auto-generate IDs (no IDENTITY_INSERT needed)
    $insertCols = $cols | Where-Object { -not $_.IsIdentity }
    $colList = ($insertCols | ForEach-Object { "[$($_.Name)]" }) -join ", "

    $writer.WriteLine("-- [$table] : $count rows")
    if ($CleanFirst) {
        $writer.WriteLine("DELETE FROM [$table];")
    }

    $cmd.CommandText = "SELECT * FROM [$table] ORDER BY (SELECT NULL)"
    $dataReader = $cmd.ExecuteReader()
    while ($dataReader.Read()) {
        $vals = @()
        for ($i = 0; $i -lt $dataReader.FieldCount; $i++) {
            if ($cols[$i].IsIdentity) { continue }  # skip identity - Azure generates new IDs
            $val = if ($dataReader.IsDBNull($i)) { $null } else { $dataReader.GetValue($i) }
            $vals += Format-SqlValue $val $cols[$i].Type
        }
        $writer.WriteLine("INSERT INTO [$table] ($colList) VALUES (" + ($vals -join ", ") + ");")
    }
    $dataReader.Close()

    $writer.WriteLine("")
    $totalRows += $count
}

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

# Discover which tables actually exist on Azure (EF migrations may not have run yet)
$existsCmd = $azureConn.CreateCommand()
$existsCmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'"
$existsReader = $existsCmd.ExecuteReader()
$azureTables = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
while ($existsReader.Read()) { $azureTables.Add($existsReader[0]) | Out-Null }
$existsReader.Close()

Write-Host ("Importing " + $totalRows + " rows to Azure SQL...") -ForegroundColor Cyan

# Read generated SQL, split by table block, execute each block separately
$sqlContent = [System.IO.File]::ReadAllText($outputFile)

$azureCmd = $azureConn.CreateCommand()
$azureCmd.CommandTimeout = 300

# Execute each table's SQL block (DELETE + INSERTs) as one batch
$blocks = [System.Text.RegularExpressions.Regex]::Split($sqlContent, "(?m)^-- \[")
$imported = 0; $skipped = 0

$azureCmd.CommandText = "BEGIN TRANSACTION;"; $azureCmd.ExecuteNonQuery() | Out-Null

foreach ($block in $blocks) {
    $b = $block.Trim()
    if ([string]::IsNullOrWhiteSpace($b)) { continue }
    # Reconstruct table name from first line: "TableName] : N rows"
    $tableMatch = [System.Text.RegularExpressions.Regex]::Match($b, '^(\w+)\]')
    if (-not $tableMatch.Success) { continue }
    $tableName = $tableMatch.Groups[1].Value
    if (-not $azureTables.Contains($tableName)) {
        Write-Host ("  SKIPPED [$tableName] - table not found on Azure (run EF migrations first)") -ForegroundColor Yellow
        $skipped++
        continue
    }
    $rowCount = ([System.Text.RegularExpressions.Regex]::Matches($b, 'INSERT INTO')).Count
    try {
        # Re-add the comment prefix that was stripped by the split
        $azureCmd.CommandText = "-- [$b"
        $azureCmd.ExecuteNonQuery() | Out-Null
        $imported += $rowCount
        Write-Host ("  [" + $tableName + "] : " + $rowCount + " rows") -ForegroundColor Green
    } catch {
        Write-Error ("Import failed on [$tableName]: $_")
        $azureCmd.CommandText = "ROLLBACK TRANSACTION;"; $azureCmd.ExecuteNonQuery() | Out-Null
        $azureConn.Close()
        Write-Host "Tip: open $outputFile in Azure portal Query Editor and run it manually." -ForegroundColor Yellow
        exit 1
    }
}

$azureCmd.CommandText = "COMMIT TRANSACTION;"; $azureCmd.ExecuteNonQuery() | Out-Null

# Reassign all migrated UserIds to the Azure admin user
# (local and Azure user GUIDs differ because they were created independently)
Write-Host "Reassigning UserIds to Azure admin..." -ForegroundColor Cyan
$fixSql = @'
DECLARE @AdminId NVARCHAR(450);
SELECT TOP 1 @AdminId = u.Id FROM AspNetUsers u
  JOIN AspNetUserRoles ur ON u.Id = ur.UserId
  JOIN AspNetRoles r ON ur.RoleId = r.Id WHERE r.Name = 'Admin';
IF @AdminId IS NULL SELECT TOP 1 @AdminId = Id FROM AspNetUsers ORDER BY CreatedAt;
UPDATE PortfolioItems SET UserId = @AdminId;
UPDATE WatchlistItems SET UserId = @AdminId;
UPDATE CashItems      SET UserId = @AdminId;
UPDATE OptionItems    SET UserId = @AdminId;
'@
$fixCmd = $azureConn.CreateCommand(); $fixCmd.CommandText = $fixSql; $fixCmd.CommandTimeout = 60
$fixCmd.ExecuteNonQuery() | Out-Null

$azureConn.Close()
if ($skipped -gt 0) {
    Write-Host ("Import complete. " + $imported + " rows written. " + $skipped + " table(s) skipped (missing on Azure - re-run after deploying code).") -ForegroundColor Yellow
} else {
    Write-Host ("Import complete. " + $imported + " rows written to Azure SQL.") -ForegroundColor Green
}
