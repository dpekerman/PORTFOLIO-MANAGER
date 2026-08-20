# First Run — One-Time Setup

Do this once before running the migration script for the first time.

---

## Step 1 — Edit the Config File

Open `scripts\migration-config.ps1` in VS Code and replace `YOUR_PASSWORD_HERE`
with your actual Azure SQL password:

```powershell
$AzureSqlConnectionString = "Server=tcp:portfolio-sql-dpekerman.database.windows.net,1433;Initial Catalog=PortfolioManagerDb;Persist Security Info=False;User ID=portfolioadmin;Password=YOUR_ACTUAL_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

> This file is in `.gitignore` and will never be committed to Git.

---

## Step 2 — Wake Up the Azure SQL Database

The free serverless database pauses after 1 hour of inactivity. Wake it before migrating:

1. Go to [portal.azure.com](https://portal.azure.com)
2. Open `PortfolioManagerDb` → **Query editor (preview)**
3. Log in with `portfolioadmin` / your SQL password
4. Run: `SELECT 1`
5. Leave this tab open during the migration

---

## Step 3 — Run the Migration

Double-click `scripts\migrate.bat`

OR in a PowerShell terminal:

```powershell
cd D:\PORTFOLIO-MANAGER
.\scripts\migrate-full.ps1
```

The script runs 7 steps automatically and prints progress. Expected output:

```
STEP 1 - Connecting to local SQL Server...   Connected.
STEP 2 - Connecting to Azure SQL...          Connected.
STEP 3 - Fixing Azure SQL schema...          Schema is up to date.
STEP 4 - Deleting all existing Azure data... All tables cleared.
STEP 5 - Exporting data from local SQL...
  PortfolioItems : 97 rows
  WatchlistItems : 143 rows
  ...
  Total: 445 rows exported (633 KB)
STEP 6 - Importing to Azure SQL...           445 rows imported.
STEP 7 - Verifying row counts...
  PortfolioItems : 97 rows
  ...
Migration complete.
```

Total time: ~60-120 seconds.

---

## Step 4 — Verify in the App

1. Open [https://gray-smoke-012fa200f.7.azurestaticapps.net](https://gray-smoke-012fa200f.7.azurestaticapps.net)
2. Log in with your admin account
3. Check that Portfolio, Watchlist, and all other data appears correctly
