# Data Migration — Automated Process Guide

This document explains the complete automated migration workflow and how to handle
schema drift between local SQL Server and Azure SQL.

---

## Why Schema Drift Happened

The original `AddCashOptionAndAdhocTables` EF migration had an empty `Up()` method,
so `CashItems`, `OptionItems`, and `AdhocAnalysisSessions` were created manually
with only base columns. Later EF migrations added the remaining columns, but a subset
did not apply cleanly, leaving Azure SQL missing columns like `IsFavorite`, `DecisionSource`,
`UserId`, etc.

---

## One-Time Fix (Run Now)

### Step 1 — Fix Azure SQL Schema

1. Open Azure portal → **PortfolioManagerDb** → **Query editor (preview)**
2. Log in with `portfolioadmin` / your SQL password
3. Open `scripts\fix-azure-schema.sql` in VS Code → copy all content → paste → **Run**
4. Confirm the output table shows all expected columns for `WatchlistItems`, `PortfolioItems`,
   `OptionItems`, and `CashItems`

### Step 2 — Re-run Data Import

```powershell
cd D:\PORTFOLIO-MANAGER

$azureConn = "Server=tcp:portfolio-sql-dpekerman.database.windows.net,1433;Initial Catalog=PortfolioManagerDb;User ID=portfolioadmin;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;"

.\scripts\migrate-local-to-azure.ps1 -ImportToAzure -CleanFirst `
  -AzureConnectionString $azureConn
```

The `-CleanFirst` flag deletes any partial data from previous failed attempts.

### Step 3 — Verify Row Counts

```powershell
$verifyConn = New-Object System.Data.SqlClient.SqlConnection($azureConn)
$verifyConn.Open()
$tables = @("AllocationRiskTargets","AllocationSectorTargets","SinglePositionLimits",
            "AdhocAnalysisSessions","PortfolioItems","WatchlistItems","CashItems",
            "OptionItems","DailySignals","StagedSignals","ValueScreenerScheduleConfigs",
            "ValueScreenerSnapshots","PortfolioValueHistories")
foreach ($t in $tables) {
    $cmd = $verifyConn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM [$t]"
    Write-Host ($t + ": " + $cmd.ExecuteScalar() + " rows")
}
$verifyConn.Close()
```

Compare the output against your local app row counts.

---

## Automated Full Migration (Single Command)

Once the schema is fixed, the complete migration is a single PowerShell command:

```powershell
cd D:\PORTFOLIO-MANAGER

$azureConn = "Server=tcp:portfolio-sql-dpekerman.database.windows.net,1433;Initial Catalog=PortfolioManagerDb;User ID=portfolioadmin;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;"

# 1. Export from local SQL
# 2. Import to Azure SQL (CleanFirst wipes Azure data before inserting)
.\scripts\migrate-local-to-azure.ps1 -ImportToAzure -CleanFirst `
  -AzureConnectionString $azureConn
```

Expected total time: 30-90 seconds depending on data size and DB wake-up time.

---

## Future Migrations (Ongoing)

After the one-time fix, **no manual data migration is needed for schema changes**.

When you add a new EF migration locally:

```powershell
# 1. Create the migration
cd D:\PORTFOLIO-MANAGER\backend\PortfolioManager.Api
dotnet ef migrations add MigrationName

# 2. Test locally
dotnet ef database update

# 3. Deploy to Azure — migrations run automatically on App Service startup
git add -A
git commit -m "feat: add MigrationName"
# Submit PR to develop, then develop -> main to trigger CD
```

The `MigrateAsync()` call in `Program.cs` applies pending migrations every time the
App Service restarts after a deployment.

---

## Re-migrating Data (When You Update Local Data)

If you continue adding portfolio data locally and want to sync it to Azure:

```powershell
# Always use -CleanFirst to avoid duplicate key errors
.\scripts\migrate-local-to-azure.ps1 -ImportToAzure -CleanFirst `
  -AzureConnectionString $azureConn
```

> **Important:** `-CleanFirst` deletes Azure data before inserting. Any changes made
> directly in the Azure app (e.g., new transactions entered by other users) will be
> overwritten. Use this only when local is the authoritative source.

---

## Keeping Local and Azure in Sync Long-Term

Once all users switch to the Azure app as the primary environment, **stop using
`-CleanFirst`**. From that point, the Azure database is the source of truth and
local SQL is only for development/testing.

| Phase            | Authoritative DB | Migration direction                 |
| ---------------- | ---------------- | ----------------------------------- |
| Now (transition) | Local SQL Server | Local → Azure with `-CleanFirst`    |
| After go-live    | Azure SQL        | Azure is primary; local is dev only |
| Future features  | Azure SQL        | EF migrations auto-apply on deploy  |

---

## Troubleshooting

### "Invalid column name" error

Run `scripts\fix-azure-schema.sql` in Azure portal Query Editor, then re-run the import.

### "Database is not currently available" error

The free serverless DB auto-paused. Open Azure portal → Query Editor → run any query
to wake it, then immediately retry the import.

### Import succeeds but row counts are wrong

The DB might have timed out mid-import. Re-run with `-CleanFirst` to start fresh.

### Connection refused / firewall error

Your home IP may have changed. Go to Azure portal → SQL Server →
Security → Networking → add your current IP → Save, then retry.
