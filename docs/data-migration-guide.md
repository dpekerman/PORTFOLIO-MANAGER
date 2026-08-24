# Data Migration: Local SQL Server → Azure SQL

**Purpose:** Export all business data from your local `PortfolioManagerDb` and import it into Azure SQL.  
**What is migrated:** All 13 business tables (portfolio, watchlist, signals, etc.)  
**What is NOT migrated:** ASP.NET Identity tables (users/roles) — you create fresh accounts on Azure via `/setup`

---

## Prerequisites

- Local SQL Server running with `PortfolioManagerDb`
- Azure SQL firewall allows your local IP (already done in Step 4 of the setup checklist)
- Your Azure SQL connection string (from Notepad — `SQL_CONNECTION_STRING`)

---

## Step 1 — Generate the Migration Script (automated)

Open a PowerShell terminal in VS Code and run:

```powershell
cd D:\PORTFOLIO-MANAGER
.\scripts\migrate-local-to-azure.ps1
```

This script:

1. Connects to your local SQL Server (trusted connection, no password needed)
2. Reads every row from all 13 business tables
3. Generates a `.sql` file with `INSERT` statements in the correct order
4. Saves it as `scripts\migration-output-YYYYMMDD-HHmmss.sql`

**Expected output:**

```
Connecting to local SQL Server...
  PortfolioItems: 47 rows exported
  WatchlistItems: 23 rows exported
  CashItems: 8 rows exported
  ...
Migration script saved: scripts\migration-output-20260820-143022.sql (128 KB)
Review the file, then run Step 2 to import to Azure SQL.
```

---

## Step 2 — Review the Generated Script (manual)

Open `scripts\migration-output-*.sql` in VS Code and quickly verify:

- Row counts look correct (match what you see in your local app)
- No obviously wrong data at the top of each table section
- File size is reasonable (not 0 bytes)

---

## Step 3 — Wake Up the Azure SQL Database (manual)

Before importing, wake the database to prevent auto-pause timeout:

1. Go to Azure portal → `PortfolioManagerDb` → **Query editor (preview)**
2. Log in with `portfolioadmin` / your SQL password
3. Run: `SELECT COUNT(*) FROM [PortfolioItems]`
4. Leave this tab open — it keeps the DB awake

---

## Step 4 — Import to Azure SQL (automated)

In PowerShell:

```powershell
cd D:\PORTFOLIO-MANAGER

# Set your Azure SQL connection string (paste from Notepad)
$azureConn = "Server=tcp:portfolio-sql-dpekerman.database.windows.net,1433;Initial Catalog=PortfolioManagerDb;User ID=portfolioadmin;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;"

# Run the import
.\scripts\migrate-local-to-azure.ps1 -ImportToAzure -AzureConnectionString $azureConn
```

OR — import the generated SQL file directly via Azure portal Query Editor:

1. Open the generated `.sql` file in Notepad
2. Copy all content
3. Paste into Azure portal → `PortfolioManagerDb` → **Query editor**
4. Click **Run**

---

## Step 5 — Verify the Migration (manual)

Run these checks in the Azure portal Query Editor:

```sql
-- Verify row counts match local
SELECT 'PortfolioItems' AS TableName, COUNT(*) AS Rows FROM [PortfolioItems]
UNION ALL SELECT 'WatchlistItems', COUNT(*) FROM [WatchlistItems]
UNION ALL SELECT 'CashItems', COUNT(*) FROM [CashItems]
UNION ALL SELECT 'OptionItems', COUNT(*) FROM [OptionItems]
UNION ALL SELECT 'DailySignals', COUNT(*) FROM [DailySignals]
UNION ALL SELECT 'StagedSignals', COUNT(*) FROM [StagedSignals]
UNION ALL SELECT 'AllocationRiskTargets', COUNT(*) FROM [AllocationRiskTargets]
UNION ALL SELECT 'AllocationSectorTargets', COUNT(*) FROM [AllocationSectorTargets]
UNION ALL SELECT 'SinglePositionLimits', COUNT(*) FROM [SinglePositionLimits]
UNION ALL SELECT 'ValueScreenerSnapshots', COUNT(*) FROM [ValueScreenerSnapshots]
UNION ALL SELECT 'ValueScreenerScheduleConfigs', COUNT(*) FROM [ValueScreenerScheduleConfigs]
UNION ALL SELECT 'PortfolioValueHistories', COUNT(*) FROM [PortfolioValueHistories]
UNION ALL SELECT 'AdhocAnalysisSessions', COUNT(*) FROM [AdhocAnalysisSessions]
ORDER BY TableName;
```

Compare the row counts against your local SQL to confirm everything migrated correctly.

---

## Re-running the Migration (if needed)

If you need to re-run (e.g., you added data locally after the first migration):

```powershell
.\scripts\migrate-local-to-azure.ps1 -CleanFirst -AzureConnectionString $azureConn
```

The `-CleanFirst` flag truncates Azure tables before inserting (safe since you have no Azure-only data yet).

---

## Tables NOT Migrated (and why)

| Table              | Reason                                                       |
| ------------------ | ------------------------------------------------------------ |
| `AspNetUsers`      | Create fresh admin via `/setup` — don't port local passwords |
| `AspNetRoles`      | Auto-seeded by the app on startup                            |
| `AspNetUserRoles`  | No users to assign yet                                       |
| `AspNetUserClaims` | N/A                                                          |
| `AspNetUserLogins` | N/A                                                          |
| `AspNetUserTokens` | N/A                                                          |
| `RefreshTokens`    | Session data — invalid after migration                       |
