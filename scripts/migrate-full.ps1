# Portfolio Manager - Full Migration Script
# Cleans Azure SQL, fixes schema, and imports all data from local SQL Server.
# Run via:  .\scripts\migrate-full.ps1
# Or just double-click:  scripts\migrate.bat

Set-StrictMode -Off
$ErrorActionPreference = "Stop"

# ---- Load config -----------------------------------------------------------
$configFile = Join-Path $PSScriptRoot "migration-config.ps1"
if (-not (Test-Path $configFile)) {
    Write-Error "Config file not found: $configFile`nEdit scripts\migration-config.ps1 with your Azure SQL connection string."
    exit 1
}
. $configFile

if ($AzureSqlConnectionString -like "*YOUR_PASSWORD_HERE*") {
    Write-Error "Please edit scripts\migration-config.ps1 and replace YOUR_PASSWORD_HERE with your actual SQL password."
    exit 1
}

# ---- Settings --------------------------------------------------------------
$LocalServer   = "localhost"
$LocalDatabase = "PortfolioManagerDb"
$outputFile    = Join-Path $PSScriptRoot ("migration-output-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + ".sql")

# Business tables - FK-safe insertion order (delete in reverse)
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
# STEP 3: Fix Azure SQL schema (add any missing columns - safe to re-run)
# ============================================================================
Write-Host "STEP 3 - Fixing Azure SQL schema (adding missing columns)..." -ForegroundColor Cyan

$schemaFixes = @(
    # WatchlistItems
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WatchlistItems') AND name='IsFavorite') ALTER TABLE [WatchlistItems] ADD [IsFavorite] BIT NOT NULL DEFAULT 0",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WatchlistItems') AND name='Role') ALTER TABLE [WatchlistItems] ADD [Role] NVARCHAR(20) NOT NULL DEFAULT N'Strategic'",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WatchlistItems') AND name='UserId') ALTER TABLE [WatchlistItems] ADD [UserId] NVARCHAR(450) NULL",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('WatchlistItems') AND name='Notes') ALTER TABLE [WatchlistItems] ADD [Notes] NVARCHAR(500) NOT NULL DEFAULT N''",
    # PortfolioItems
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PortfolioItems') AND name='DecisionSource') ALTER TABLE [PortfolioItems] ADD [DecisionSource] NVARCHAR(50) NULL",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PortfolioItems') AND name='DecisionSourceClosed') ALTER TABLE [PortfolioItems] ADD [DecisionSourceClosed] NVARCHAR(50) NULL",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PortfolioItems') AND name='HoldingRole') ALTER TABLE [PortfolioItems] ADD [HoldingRole] NVARCHAR(20) NULL",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PortfolioItems') AND name='UserId') ALTER TABLE [PortfolioItems] ADD [UserId] NVARCHAR(450) NULL",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PortfolioItems') AND name='Notes') ALTER TABLE [PortfolioItems] ADD [Notes] NVARCHAR(MAX) NULL",
    # OptionItems
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('OptionItems') AND name='DecisionSource') ALTER TABLE [OptionItems] ADD [DecisionSource] NVARCHAR(50) NULL",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('OptionItems') AND name='DecisionSourceClosed') ALTER TABLE [OptionItems] ADD [DecisionSourceClosed] NVARCHAR(50) NULL",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('OptionItems') AND name='Notes') ALTER TABLE [OptionItems] ADD [Notes] NVARCHAR(MAX) NULL",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('OptionItems') AND name='UserId') ALTER TABLE [OptionItems] ADD [UserId] NVARCHAR(450) NULL",
    # CashItems
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CashItems') AND name='AccountType') ALTER TABLE [CashItems] ADD [AccountType] NVARCHAR(30) NULL",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CashItems') AND name='TransactionDate') ALTER TABLE [CashItems] ADD [TransactionDate] DATETIME2 NULL",
    "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CashItems') AND name='UserId') ALTER TABLE [CashItems] ADD [UserId] NVARCHAR(450) NULL",
    # PortfolioValueHistories (create entire table if missing)
    "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='PortfolioValueHistories') CREATE TABLE [PortfolioValueHistories] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [RecordedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(), [RecordedDate] NVARCHAR(20) NOT NULL DEFAULT N'', [TotalValue] DECIMAL(18,4) NOT NULL DEFAULT 0, [StocksValue] DECIMAL(18,4) NOT NULL DEFAULT 0, [CashValue] DECIMAL(18,4) NOT NULL DEFAULT 0, [OptionsValue] DECIMAL(18,4) NOT NULL DEFAULT 0)",
    # AllocationRiskTargets
    "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='AllocationRiskTargets') CREATE TABLE [AllocationRiskTargets] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [Role] NVARCHAR(50) NOT NULL DEFAULT N'', [TargetPct] DECIMAL(18,4) NOT NULL DEFAULT 0, [DisplayOrder] INT NOT NULL DEFAULT 0)",
    # AllocationSectorTargets
    "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='AllocationSectorTargets') CREATE TABLE [AllocationSectorTargets] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [Sector] NVARCHAR(100) NOT NULL DEFAULT N'', [TargetPct] DECIMAL(18,4) NOT NULL DEFAULT 0, [DisplayOrder] INT NOT NULL DEFAULT 0)",
    # SinglePositionLimits
    "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='SinglePositionLimits') CREATE TABLE [SinglePositionLimits] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [Role] NVARCHAR(50) NOT NULL DEFAULT N'', [TargetPct] DECIMAL(18,4) NOT NULL DEFAULT 0, [DisplayOrder] INT NOT NULL DEFAULT 0)",
    # ValueScreenerSnapshots
    "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='ValueScreenerSnapshots') CREATE TABLE [ValueScreenerSnapshots] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [Origin] NVARCHAR(20) NOT NULL DEFAULT N'Portfolio', [RunAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(), [ResultsJson] NVARCHAR(MAX) NOT NULL DEFAULT N'[]')",
    # ValueScreenerScheduleConfigs
    "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='ValueScreenerScheduleConfigs') CREATE TABLE [ValueScreenerScheduleConfigs] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [ScheduledTimeEt] NVARCHAR(10) NOT NULL DEFAULT N'17:00', [Enabled] BIT NOT NULL DEFAULT 1, [LastPortfolioRunAt] DATETIME2 NULL, [LastWatchlistRunAt] DATETIME2 NULL)"
)

foreach ($fix in $schemaFixes) {
    Run-AzureSQL $azureConn $fix
}
Write-Host "  Schema is up to date." -ForegroundColor Green

# ============================================================================
# STEP 4: Delete all existing Azure data (reverse insertion order)
# ============================================================================
Write-Host "STEP 4 - Deleting all existing Azure SQL data..." -ForegroundColor Cyan
$reversedTables = [array]::Reverse($tables.Clone()); $reversedTables = $tables[($tables.Length-1)..0]
foreach ($table in $reversedTables) {
    # Skip tables that don't exist yet in Azure (will be created in step 6)
    $checkCmd = $azureConn.CreateCommand()
    $checkCmd.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name='$table'"
    $exists = [int]$checkCmd.ExecuteScalar()
    if ($exists -eq 0) {
        Write-Host ("  Skipped (table not yet created): " + $table) -ForegroundColor DarkGray
        continue
    }
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
$importCmd = $azureConn.CreateCommand()
$importCmd.CommandTimeout = 600
$importCmd.CommandText = $sql
$importCmd.ExecuteNonQuery() | Out-Null
$azureConn.Close()

Write-Host ("  " + $totalRows + " rows imported.") -ForegroundColor Green

# ============================================================================
# STEP 7: Verify
# ============================================================================
Write-Host "STEP 7 - Verifying row counts in Azure SQL..." -ForegroundColor Cyan
$verifyConn = New-Object System.Data.SqlClient.SqlConnection($AzureSqlConnectionString)
$verifyConn.Open()
$allOk = $true
foreach ($table in $tables) {
    $cmd = $verifyConn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM [$table]"
    $azureCount = [int]$cmd.ExecuteScalar()
    Write-Host ("  " + $table + " : " + $azureCount + " rows") -ForegroundColor Green
}
$verifyConn.Close()

Write-Host ""
Write-Host "Migration complete." -ForegroundColor Green
Write-Host ("SQL file saved at: " + $outputFile) -ForegroundColor DarkGray
