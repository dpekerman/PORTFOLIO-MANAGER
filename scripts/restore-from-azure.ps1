# Portfolio Manager - Restore Script (Azure SQL -> Local SQL Server)
# Use this after time away (e.g. vacation) to pull the latest Azure data back
# down to your local dev database. Single command: syncs schema, backs up
# local DB first, wipes local business tables, imports exact data from Azure,
# verifies counts.
# Run via:  .\scripts\restore-from-azure.ps1
# Or just double-click:  scripts\restore-from-azure.bat
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
$LocalDatabase = "PortfolioManagerDb"
$outputFile    = Join-Path $PSScriptRoot ("restore-output-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + ".sql")
$backupDir     = Join-Path $PSScriptRoot "local-backups"
$backupFile    = Join-Path $backupDir ("PortfolioManagerDb_PreRestore_" + (Get-Date -Format 'yyyyMMdd-HHmmss') + ".bak")

# Same exact table list as migrate-full.ps1 - keep these two files in sync.
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

function Run-SQL($conn, $sql) {
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
# STEP 2: Back up local database before making any changes (safety net)
# ============================================================================
Write-Host "STEP 2 - Backing up local database before restore..." -ForegroundColor Cyan
if (-not (Test-Path $backupDir)) { New-Item -ItemType Directory -Path $backupDir | Out-Null }
Run-SQL $localConn ("BACKUP DATABASE [$LocalDatabase] TO DISK = N'" + $backupFile.Replace("'", "''") + "' WITH FORMAT, STATS = 10;")
Write-Host ("  Backup saved: " + $backupFile) -ForegroundColor Green

# ============================================================================
# STEP 3: Connect to Azure SQL
# ============================================================================
Write-Host "STEP 3 - Connecting to Azure SQL..." -ForegroundColor Cyan
$azureConn = New-Object System.Data.SqlClient.SqlConnection($AzureSqlConnectionString)
$azureConn.Open()
Write-Host "  Connected." -ForegroundColor Green

# ============================================================================
# STEP 4: Sync schema (auto-diff Azure -> local, create/alter as needed)
# ============================================================================
Write-Host "STEP 4 - Syncing schema (tables/columns) from Azure to local..." -ForegroundColor Cyan
Sync-AllSchemas -sourceConn $azureConn -targetConn $localConn -tables $tables
Write-Host "  Schema is up to date." -ForegroundColor Green

# ============================================================================
# STEP 5: Delete all existing local data (reverse insertion order)
# ============================================================================
Write-Host "STEP 5 - Deleting all existing local SQL data..." -ForegroundColor Cyan
$reversedTables = $tables[($tables.Length - 1)..0]
foreach ($table in $reversedTables) {
    Run-SQL $localConn "DELETE FROM [$table]"
    Write-Host ("  Cleared: " + $table) -ForegroundColor DarkGray
}
Write-Host "  All tables cleared." -ForegroundColor Green

# ============================================================================
# STEP 6: Export from Azure SQL and build INSERT script
# ============================================================================
Write-Host "STEP 6 - Exporting data from Azure SQL..." -ForegroundColor Cyan

$writer = [System.IO.StreamWriter]::new($outputFile, $false, [System.Text.Encoding]::UTF8)
$writer.WriteLine("-- Portfolio Manager Data Restore (Azure -> Local)")
$writer.WriteLine("-- Generated : " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$writer.WriteLine("SET NOCOUNT ON;")
$writer.WriteLine("")

$totalRows = 0

foreach ($table in $tables) {
    $cmd = $azureConn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM [$table]"
    $count = [int]$cmd.ExecuteScalar()

    if ($count -eq 0) {
        Write-Host ("  " + $table + " : 0 rows - skipped") -ForegroundColor DarkGray
        continue
    }

    $metaCmd = $azureConn.CreateCommand()
    $metaCmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, COLUMNPROPERTY(OBJECT_ID('$table'), COLUMN_NAME, 'IsIdentity') AS IsIdentity FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$table' ORDER BY ORDINAL_POSITION"
    $metaReader = $metaCmd.ExecuteReader()
    $cols = @()
    while ($metaReader.Read()) {
        $cols += [pscustomobject]@{ Name = $metaReader[0]; Type = $metaReader[1]; IsIdentity = [bool]($metaReader[2]) }
    }
    $metaReader.Close()

    # Skip identity columns — let local SQL auto-generate new IDs (no cross-table FK references)
    $dataCols = $cols | Where-Object { -not $_.IsIdentity }
    $colList  = ($dataCols | ForEach-Object { "[$($_.Name)]" }) -join ", "

    $writer.WriteLine("-- [$table] : $count rows")

    $dataCmd = $azureConn.CreateCommand()
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
$azureConn.Close()

$fileSize = [Math]::Round((Get-Item $outputFile).Length / 1KB, 1)
Write-Host ("  Total: " + $totalRows + " rows exported (" + $fileSize + " KB)") -ForegroundColor Yellow

# ============================================================================
# STEP 7: Import to local SQL Server
# ============================================================================
Write-Host "STEP 7 - Importing to local SQL Server..." -ForegroundColor Cyan

$sql = [System.IO.File]::ReadAllText($outputFile)
if ($sql.Trim().Length -gt 0) {
    $importCmd = $localConn.CreateCommand()
    $importCmd.CommandTimeout = 600
    $importCmd.CommandText = $sql
    $importCmd.ExecuteNonQuery() | Out-Null
}

Write-Host ("  " + $totalRows + " rows imported.") -ForegroundColor Green

# ============================================================================
# STEP 7b: Reassign UserId to the local admin account
# (Azure and local user GUIDs differ - migrated rows carry the AZURE user's
# UserId, which matches no AspNetUsers row locally and would be invisible)
# ============================================================================
Write-Host "STEP 7b - Reassigning migrated UserId values to the local admin..." -ForegroundColor Cyan
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
UPDATE UserPreferences SET UserId = @AdminId;
'@
Run-SQL $localConn $reassignSql
Write-Host "  UserId reassigned." -ForegroundColor Green

# ============================================================================
# STEP 8: Verify row counts match exactly between Azure and local
# ============================================================================
Write-Host "STEP 8 - Verifying row counts (Azure vs local)..." -ForegroundColor Cyan
$azureVerifyConn = New-Object System.Data.SqlClient.SqlConnection($AzureSqlConnectionString)
$azureVerifyConn.Open()

$allMatch = $true
foreach ($table in $tables) {
    $ac = $azureVerifyConn.CreateCommand(); $ac.CommandText = "SELECT COUNT(*) FROM [$table]"
    $azureCount = [int]$ac.ExecuteScalar()

    $lc = $localConn.CreateCommand(); $lc.CommandText = "SELECT COUNT(*) FROM [$table]"
    $localCount = [int]$lc.ExecuteScalar()

    if ($localCount -eq $azureCount) {
        Write-Host ("  " + $table + " : " + $localCount + " rows (match)") -ForegroundColor Green
    } else {
        Write-Host ("  " + $table + " : azure=" + $azureCount + " local=" + $localCount + " MISMATCH") -ForegroundColor Red
        $allMatch = $false
    }
}
$azureVerifyConn.Close()
$localConn.Close()

Write-Host ""
if ($allMatch) {
    Write-Host "Restore complete. Local SQL row counts match Azure exactly." -ForegroundColor Green
} else {
    Write-Host "Restore finished with MISMATCHES - review output above." -ForegroundColor Red
    exit 1
}
Write-Host ("SQL file saved at: " + $outputFile) -ForegroundColor DarkGray
Write-Host ("Pre-restore backup: " + $backupFile) -ForegroundColor DarkGray
