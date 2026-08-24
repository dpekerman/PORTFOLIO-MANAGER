# Portfolio Manager - Full Local Database Backup
# Backs up the entire local PortfolioManagerDb (all tables) to a .bak file
# under D:\PORTFOLIO-MANAGER-SQL-BACKUP, plus a human-readable data export
# (.sql) of the business/settings tables for quick inspection or restore.
# Run via:  .\scripts\backup-local-db.ps1

Set-StrictMode -Off
$ErrorActionPreference = "Stop"

$LocalServer   = "localhost"
$LocalDatabase = "PortfolioManagerLocal"
$BackupRoot    = "D:\PORTFOLIO-MANAGER-SQL-BACKUP"
$stamp         = Get-Date -Format 'yyyyMMdd-HHmmss'
$bakFile       = Join-Path $BackupRoot ($LocalDatabase + "_" + $stamp + ".bak")
$sqlFile       = Join-Path $BackupRoot ($LocalDatabase + "_DataExport_" + $stamp + ".sql")

# All business + settings tables (same list used by the migration scripts).
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
    "SectorIndustryConfigs"
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

if (-not (Test-Path $BackupRoot)) { New-Item -ItemType Directory -Path $BackupRoot | Out-Null }

Write-Host "Connecting to local SQL Server ($LocalServer/$LocalDatabase)..." -ForegroundColor Cyan
$conn = New-Object System.Data.SqlClient.SqlConnection(
    "Server=$LocalServer;Database=$LocalDatabase;Trusted_Connection=True;TrustServerCertificate=True;")
$conn.Open()
Write-Host "  Connected." -ForegroundColor Green

# ---- Full native backup (.bak) - every table, every schema object ----------
Write-Host "Creating full database backup (.bak)..." -ForegroundColor Cyan
$bakCmd = $conn.CreateCommand()
$bakCmd.CommandTimeout = 600
$bakCmd.CommandText = "BACKUP DATABASE [$LocalDatabase] TO DISK = N'" + $bakFile.Replace("'", "''") + "' WITH FORMAT, STATS = 10;"
$bakCmd.ExecuteNonQuery() | Out-Null
$bakSize = [Math]::Round((Get-Item $bakFile).Length / 1MB, 2)
Write-Host ("  Saved: " + $bakFile + " (" + $bakSize + " MB)") -ForegroundColor Green

# ---- Human-readable data export (.sql) of business/settings tables --------
Write-Host "Exporting business/settings table data (.sql)..." -ForegroundColor Cyan
$writer = [System.IO.StreamWriter]::new($sqlFile, $false, [System.Text.Encoding]::UTF8)
$writer.WriteLine("-- Portfolio Manager Local Database - Full Data Export")
$writer.WriteLine("-- Generated : " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$writer.WriteLine("SET NOCOUNT ON;")
$writer.WriteLine("")

$totalRows = 0
foreach ($table in $tables) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM [$table]"
    $count = [int]$cmd.ExecuteScalar()
    if ($count -eq 0) {
        Write-Host ("  " + $table + " : 0 rows - skipped") -ForegroundColor DarkGray
        continue
    }

    $metaCmd = $conn.CreateCommand()
    $metaCmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$table' ORDER BY ORDINAL_POSITION"
    $metaReader = $metaCmd.ExecuteReader()
    $cols = @()
    while ($metaReader.Read()) { $cols += [pscustomobject]@{ Name = $metaReader[0]; Type = $metaReader[1] } }
    $metaReader.Close()
    $colList = ($cols | ForEach-Object { "[$($_.Name)]" }) -join ", "

    $writer.WriteLine("-- [$table] : $count rows")
    $dataCmd = $conn.CreateCommand()
    $dataCmd.CommandText = "SELECT $colList FROM [$table] ORDER BY (SELECT NULL)"
    $dataReader = $dataCmd.ExecuteReader()
    while ($dataReader.Read()) {
        $vals = @()
        for ($i = 0; $i -lt $dataReader.FieldCount; $i++) {
            $val = if ($dataReader.IsDBNull($i)) { $null } else { $dataReader.GetValue($i) }
            $vals += Format-SqlValue $val $cols[$i].Type
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
$conn.Close()

$sqlSize = [Math]::Round((Get-Item $sqlFile).Length / 1KB, 1)
Write-Host ("  Saved: " + $sqlFile + " (" + $sqlSize + " KB, " + $totalRows + " rows)") -ForegroundColor Green

Write-Host ""
Write-Host "Backup complete." -ForegroundColor Green
Write-Host ("  Full .bak : " + $bakFile) -ForegroundColor DarkGray
Write-Host ("  Data .sql : " + $sqlFile) -ForegroundColor DarkGray
