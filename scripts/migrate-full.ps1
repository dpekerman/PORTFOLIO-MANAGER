# Portfolio Manager - Full Migration Script (Local SQL Server -> Azure SQL)
# Single-command deploy: syncs schema (auto-detected column/table diff),
# wipes Azure business tables, imports exact data from local, verifies counts.
# Run via:  .\scripts\migrate-full.ps1
# Or just double-click:  scripts\migrate.bat
#
# Auth/Identity tables (AspNetUsers, AspNetRoles, RefreshTokens, etc.) are
# intentionally excluded - each environment keeps its own login accounts.

Set-StrictMode -Off
$ErrorActionPreference = "Stop"

# ---- Load config -----------------------------------------------------------
$configFile = Join-Path $PSScriptRoot "migration-config.ps1"
if (-not (Test-Path $configFile)) {
    Write-Error "Config file not found: $configFile`nEdit scripts\migration-config.ps1 with your Azure SQL connection string."
    exit 1
}
. $configFile
. (Join-Path $PSScriptRoot "lib\SchemaSync.ps1")

if ($AzureSqlConnectionString -like "*YOUR_PASSWORD_HERE*") {
    Write-Error "Please edit scripts\migration-config.ps1 and replace YOUR_PASSWORD_HERE with your actual SQL password."
    exit 1
}

# ---- Settings --------------------------------------------------------------
$LocalServer   = "localhost"
$LocalDatabase = "PortfolioManagerLocal"
$outputFile    = Join-Path $PSScriptRoot ("migration-output-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + ".sql")

# Business + settings tables - exact list synced between environments.
# (Order is cosmetic only - no DB-level FK constraints exist between these tables.)
# RSI/quote snapshots are ephemeral caches. DashboardSnapshots is persistent user data
# and must be included in Azure/local data migration.
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
    "UserPreferences",
    "SectorIndustryConfigs",
    "DashboardSnapshots"
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

function Run-AzureSQL($conn, $sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 300
    $cmd.CommandText = $sql
    $cmd.ExecuteNonQuery() | Out-Null
}

# ============================================================================
# STEP 1: Connect to local SQL
# ============================================================================
Write-Host ""
Write-Host "STEP 1 - Connecting to local SQL Server ($LocalServer/$LocalDatabase)..." -ForegroundColor Cyan
$localConn = New-Object System.Data.SqlClient.SqlConnection(
    "Server=$LocalServer;Database=$LocalDatabase;Trusted_Connection=True;TrustServerCertificate=True;")
$localConn.Open()
Write-Host "  Connected." -ForegroundColor Green

# ============================================================================
# STEP 2: Connect to Azure SQL
# ============================================================================
Write-Host "STEP 2 - Connecting to Azure SQL..." -ForegroundColor Cyan
$azureConn = New-Object System.Data.SqlClient.SqlConnection($AzureSqlConnectionString)
$azureConn.Open()
Write-Host "  Connected." -ForegroundColor Green

# ============================================================================
# STEP 3: Sync schema (auto-diff local -> Azure, create/alter as needed)
# ============================================================================
Write-Host "STEP 3 - Syncing schema (tables/columns) from local to Azure..." -ForegroundColor Cyan
Sync-AllSchemas -sourceConn $localConn -targetConn $azureConn -tables $tables
Write-Host "  Schema is up to date." -ForegroundColor Green

# ============================================================================
# STEP 4: Delete all existing Azure data (reverse insertion order)
# ============================================================================
Write-Host "STEP 4 - Deleting all existing Azure SQL data..." -ForegroundColor Cyan
$reversedTables = $tables[($tables.Length - 1)..0]
foreach ($table in $reversedTables) {
    Run-AzureSQL $azureConn "DELETE FROM [$table]"
    Write-Host ("  Cleared: " + $table) -ForegroundColor DarkGray
}
Write-Host "  All tables cleared." -ForegroundColor Green

# ============================================================================
# STEP 5: Export from local SQL and build INSERT script
# ============================================================================
Write-Host "STEP 5 - Exporting data from local SQL Server..." -ForegroundColor Cyan

$writer = [System.IO.StreamWriter]::new($outputFile, $false, [System.Text.Encoding]::UTF8)
$writer.WriteLine("-- Portfolio Manager Data Migration")
$writer.WriteLine("-- Generated : " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$writer.WriteLine("SET NOCOUNT ON;")
$writer.WriteLine("")

$totalRows = 0

foreach ($table in $tables) {
    $cmd = $localConn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM [$table]"
    $count = [int]$cmd.ExecuteScalar()

    if ($count -eq 0) {
        Write-Host ("  " + $table + " : 0 rows - skipped") -ForegroundColor DarkGray
        continue
    }

    $metaCmd = $localConn.CreateCommand()
    $metaCmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, COLUMNPROPERTY(OBJECT_ID('$table'), COLUMN_NAME, 'IsIdentity') AS IsIdentity FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$table' ORDER BY ORDINAL_POSITION"
    $metaReader = $metaCmd.ExecuteReader()
    $cols = @()
    while ($metaReader.Read()) {
        $cols += [pscustomobject]@{ Name = $metaReader[0]; Type = $metaReader[1]; IsIdentity = [bool]($metaReader[2]) }
    }
    $metaReader.Close()

    # Skip identity columns — let Azure auto-generate new IDs (no cross-table FK references)
    $dataCols = $cols | Where-Object { -not $_.IsIdentity }
    $colList  = ($dataCols | ForEach-Object { "[$($_.Name)]" }) -join ", "

    $writer.WriteLine("-- [$table] : $count rows")

    $dataCmd = $localConn.CreateCommand()
    $dataCmd.CommandText = "SELECT " + (($dataCols | ForEach-Object { "[$($_.Name)]" }) -join ", ") + " FROM [$table] ORDER BY (SELECT NULL)"
    $dataReader = $dataCmd.ExecuteReader()
    while ($dataReader.Read()) {
        $vals = @()
        for ($i = 0; $i -lt $dataReader.FieldCount; $i++) {
            $val = if ($dataReader.IsDBNull($i)) { $null } else { $dataReader.GetValue($i) }
            $vals += Format-SqlValue $val $dataCols[$i].Type
        }
        $writer.WriteLine("INSERT INTO [$table] ($colList) VALUES (" + ($vals -join ", ") + ");")
    }
    $dataReader.Close()

    $writer.WriteLine("")
    $totalRows += $count
    Write-Host ("  " + $table + " : " + $count + " rows") -ForegroundColor Green
}

$writer.Flush()
$writer.Close()
$localConn.Close()

$fileSize = [Math]::Round((Get-Item $outputFile).Length / 1KB, 1)
Write-Host ("  Total: " + $totalRows + " rows exported (" + $fileSize + " KB)") -ForegroundColor Yellow

# ============================================================================
# STEP 6: Import to Azure SQL
# ============================================================================
Write-Host "STEP 6 - Importing to Azure SQL..." -ForegroundColor Cyan

$sql = [System.IO.File]::ReadAllText($outputFile)
if ($sql.Trim().Length -gt 0) {
    $importCmd = $azureConn.CreateCommand()
    $importCmd.CommandTimeout = 600
    $importCmd.CommandText = $sql
    $importCmd.ExecuteNonQuery() | Out-Null
}

Write-Host ("  " + $totalRows + " rows imported.") -ForegroundColor Green

# ============================================================================
# STEP 6b: Reassign UserId to the Azure admin account
# (local and Azure user GUIDs differ - migrated rows carry the LOCAL user's
# UserId, which matches no AspNetUsers row on Azure and would be invisible)
# ============================================================================
Write-Host "STEP 6b - Reassigning migrated UserId values to the Azure admin..." -ForegroundColor Cyan
$reassignSql = @'
SET QUOTED_IDENTIFIER ON;
DECLARE @AdminId NVARCHAR(450);
SELECT TOP 1 @AdminId = u.Id FROM AspNetUsers u
  JOIN AspNetUserRoles ur ON u.Id = ur.UserId
  JOIN AspNetRoles r ON ur.RoleId = r.Id
  WHERE r.Name = 'Admin'
  ORDER BY CASE WHEN u.Email = 'dima.pekerman@gmail.com' THEN 0 ELSE 1 END, u.CreatedAt;
IF @AdminId IS NULL
    SELECT TOP 1 @AdminId = Id FROM AspNetUsers ORDER BY CreatedAt;
UPDATE PortfolioItems  SET UserId = @AdminId WHERE UserId IS NOT NULL;
UPDATE WatchlistItems  SET UserId = @AdminId WHERE UserId IS NOT NULL;
UPDATE CashItems       SET UserId = @AdminId WHERE UserId IS NOT NULL;
UPDATE OptionItems     SET UserId = @AdminId WHERE UserId IS NOT NULL;
-- Dedupe first: reassigning all rows to one admin would otherwise violate the
-- unique (UserId, PreferenceKey) index when multiple distinct users had saved prefs.
;WITH Ranked AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY PreferenceKey ORDER BY UpdatedAt DESC) AS rn
    FROM UserPreferences
)
DELETE FROM UserPreferences WHERE Id IN (SELECT Id FROM Ranked WHERE rn > 1);
UPDATE UserPreferences SET UserId = @AdminId;
'@
Run-AzureSQL $azureConn $reassignSql
Write-Host "  UserId reassigned." -ForegroundColor Green

# ============================================================================
# STEP 7: Verify row counts match exactly between local and Azure
# ============================================================================
Write-Host "STEP 7 - Verifying row counts (local vs Azure)..." -ForegroundColor Cyan
$localVerifyConn = New-Object System.Data.SqlClient.SqlConnection(
    "Server=$LocalServer;Database=$LocalDatabase;Trusted_Connection=True;TrustServerCertificate=True;")
$localVerifyConn.Open()

$allMatch = $true
foreach ($table in $tables) {
    $lc = $localVerifyConn.CreateCommand(); $lc.CommandText = "SELECT COUNT(*) FROM [$table]"
    $localCount = [int]$lc.ExecuteScalar()

    $ac = $azureConn.CreateCommand(); $ac.CommandText = "SELECT COUNT(*) FROM [$table]"
    $azureCount = [int]$ac.ExecuteScalar()

    if ($localCount -eq $azureCount) {
        Write-Host ("  " + $table + " : " + $azureCount + " rows (match)") -ForegroundColor Green
    } elseif ($table -eq "UserPreferences") {
        # Expected: multiple distinct local users' rows are deduped down to one per
        # PreferenceKey when reassigned to the single Azure admin (Step 6b) - not a bug.
        Write-Host ("  " + $table + " : local=" + $localCount + " azure=" + $azureCount + " (deduped to one admin - expected)") -ForegroundColor DarkGray
    } else {
        Write-Host ("  " + $table + " : local=" + $localCount + " azure=" + $azureCount + " MISMATCH") -ForegroundColor Red
        $allMatch = $false
    }
}
$localVerifyConn.Close()
$azureConn.Close()

Write-Host ""
if ($allMatch) {
    Write-Host "Migration complete. Azure SQL row counts match local exactly." -ForegroundColor Green
} else {
    Write-Host "Migration finished with MISMATCHES - review output above." -ForegroundColor Red
    exit 1
}
Write-Host ("SQL file saved at: " + $outputFile) -ForegroundColor DarkGray
