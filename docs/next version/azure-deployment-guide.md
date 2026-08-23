# Azure Deployment Guide — Portfolio Manager

**Date:** 2026-08-22 | **Branch:** develop → main | **Azure status:** LIVE

---

## Live Azure Resources

| Resource       | Name                                              |
| -------------- | ------------------------------------------------- |
| Resource Group | `rg-portfolio-manager` (Canada Central)           |
| App Service    | `portfolio-manager202608192326.azurewebsites.net` |
| Static Web App | `gray-smoke-012fa200f.7.azurestaticapps.net`      |
| SQL Server     | `portfolio-sql-dpekerman.database.windows.net`    |
| SQL Database   | `PortfolioManagerDb`                              |

---

## Part 1 — Pre-Deploy Security Fixes (Do FIRST)

### Fix 1: Gmail App Password

`EmailNotification__Password` in App Service must be a 16-character Gmail App Password, not your regular password.

1. Go to [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords)
2. Create App Password → name it "Portfolio Manager Azure" → copy the 16-char code
3. Azure Portal → App Service `portfolio-manager202608192326` → **Settings → Environment variables**
4. Edit `EmailNotification__Password` → replace with 16-char app password → **Save → Apply**

### Fix 2: Change SQL Server Password

Password `@Fang1970` was exposed in terminal history.

1. Azure Portal → SQL servers → `portfolio-sql-dpekerman` → **Security → Reset password**
2. Set new strong password (16+ chars)
3. Azure Portal → App Service → **Environment variables**
4. Edit `ConnectionStrings__DefaultConnection` → update `Password=...` → **Save → Apply**

---

## Part 2 — Apply New EF Migrations to Azure SQL

Run once from your local machine whenever new EF migrations are added.

```powershell
cd D:\PORTFOLIO-MANAGER\backend\PortfolioManager.Api

$azureConn = "Server=tcp:portfolio-sql-dpekerman.database.windows.net,1433;" +
             "Initial Catalog=PortfolioManagerDb;User ID=portfolioadmin;" +
             "Password=YOUR_NEW_SQL_PASSWORD;Encrypt=True;Connection Timeout=30;"

# Apply all pending migrations
dotnet ef database update --connection $azureConn

# Verify all are applied
dotnet ef migrations list --connection $azureConn
```

**Current migrations to apply (new since last deploy):**

- `20260821160920_AddRsiSnapshotAndUserPreferences` — RsiScanSnapshots + UserPreferences tables
- `20260822152539_AddPortfolioAndWatchlistSnapshots` — PortfolioSnapshots + WatchlistSnapshots tables

All migrations use `IF NOT EXISTS` guards — safe to run on both fresh and existing databases.

**Alternative: Run SQL script manually via Azure Portal**

1. Azure Portal → SQL Database `PortfolioManagerDb` → **Query editor**
2. Paste and run: `database/SCRIPTS/14_CreateSnapshotTables.sql`

---

## Part 3 — Migrate Local Data to Azure

This exports all business data from local SQL and imports to Azure. Snapshot tables are excluded (they regenerate automatically on first page load after deploy).

```powershell
cd D:\PORTFOLIO-MANAGER\scripts

# Set your Azure connection string
$azureConn = "Server=tcp:portfolio-sql-dpekerman.database.windows.net,1433;..."

# Step 1: Generate SQL file only (review before importing)
.\migrate-local-to-azure.ps1

# Step 2: Import to Azure (clean existing Azure data first)
.\migrate-local-to-azure.ps1 -ImportToAzure -CleanFirst -AzureConnectionString $azureConn
```

**Tables included in migration:**

- AllocationRiskTargets, AllocationSectorTargets, SinglePositionLimits
- PortfolioItems, WatchlistItems, CashItems, OptionItems
- DailySignals, StagedSignals
- ValueScreenerScheduleConfigs, ValueScreenerSnapshots
- PortfolioValueHistories
- **UserPreferences** ← new: user column settings and app config

**Tables excluded (regenerate automatically):**

- RsiScanSnapshots, PortfolioSnapshots, WatchlistSnapshots ← ephemeral caches

**Identity tables excluded (created via first-run setup):**

- AspNetUsers, AspNetRoles, AspNetUserRoles, RefreshTokens

---

## Part 4 — Deploy Code to Azure

### Step 1: Local Verification

```powershell
# Backend
cd D:\PORTFOLIO-MANAGER\backend\PortfolioManager.Api
dotnet build                                    # 0 errors
dotnet test ..\PortfolioManager.Tests\          # 86/86 pass

# Frontend
cd D:\PORTFOLIO-MANAGER\frontend\portfolio-manager-ui
npx ng build --configuration production         # succeeds
```

### Step 2: Merge develop → main

1. Push final commits to `develop`
2. GitHub → `dpekerman/PORTFOLIO-MANAGER` → **Pull requests → New pull request**
3. Base: `main` ← Compare: `develop`
4. Add title: e.g., `feat: RSI/Portfolio/Watchlist snapshots + user preferences DB`
5. Verify CI `build` check passes → **Approve → Merge**

### Step 3: GitHub Actions Auto-Deploy

After merge to `main`, `.github/workflows/cd.yml` automatically:

- Builds .NET 8 API → deploys to App Service
- Builds Angular (`ng build --configuration production`) → deploys to Static Web App
- **App Service runs `MigrateAsync()` on startup** → applies all pending EF migrations

Monitor: GitHub → **Actions** tab → watch latest `cd.yml` run (~5 min)

### Step 4: Post-Deploy Verification

Open `https://gray-smoke-012fa200f.7.azurestaticapps.net` in an incognito window.

| Check                   | Expected                                            |
| ----------------------- | --------------------------------------------------- |
| App loads               | Angular SPA renders, no blank screen                |
| Login                   | Redirects to dashboard                              |
| Portfolio page          | Shows data instantly (snapshot loads from DB)       |
| Watchlist page          | Shows data instantly (snapshot)                     |
| RSI Scanner page        | Shows data or "Retry" immediately                   |
| Configuration → Scanner | Three refresh dropdowns (RSI, Portfolio, Watchlist) |
| Save Settings           | No 400 errors in DevTools Network panel             |
| Column changes          | Persist after logout/login across browsers          |
| App Service logs        | No SQL or EF exceptions in first 60s                |

**App Service log stream:**
Azure Portal → `portfolio-manager202608192326` → **Log stream**

---

## Part 5 — Azure Environment Variables (Full Reference)

| #   | Variable                               | Value                                                | Secret  |
| --- | -------------------------------------- | ---------------------------------------------------- | ------- |
| 1   | `ASPNETCORE_ENVIRONMENT`               | `Production`                                         | No      |
| 2   | `ConnectionStrings__DefaultConnection` | Azure SQL ADO.NET connection string                  | **Yes** |
| 3   | `Jwt__Secret`                          | 64-char base64 string                                | **Yes** |
| 4   | `Jwt__Issuer`                          | `PortfolioManager`                                   | No      |
| 5   | `Jwt__Audience`                        | `PortfolioManagerClient`                             | No      |
| 6   | `CorsOrigin`                           | `https://gray-smoke-012fa200f.7.azurestaticapps.net` | No      |
| 7   | `EmailNotification__Enabled`           | `true`                                               | No      |
| 8   | `EmailNotification__SmtpHost`          | `smtp.gmail.com`                                     | No      |
| 9   | `EmailNotification__SmtpPort`          | `587`                                                | No      |
| 10  | `EmailNotification__UseStartTls`       | `true`                                               | No      |
| 11  | `EmailNotification__Username`          | `dima.pekerman@gmail.com`                            | No      |
| 12  | `EmailNotification__Password`          | Gmail App Password (16 chars)                        | **Yes** |
| 13  | `EmailNotification__FromAddress`       | `dima.pekerman@gmail.com`                            | No      |

**GitHub Secrets (for CD workflow):**

| Secret                            | Purpose                           |
| --------------------------------- | --------------------------------- |
| `AZURE_WEBAPP_PUBLISH_PROFILE`    | Deploys .NET API to App Service   |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Deploys Angular to Static Web App |

---

## Part 6 — Rollback

**Code rollback:**
Azure Portal → App Service → **Deployment Center → Deployment logs** → find previous → **Redeploy**

**Database rollback (if a migration caused issues):**

```powershell
cd D:\PORTFOLIO-MANAGER\backend\PortfolioManager.Api
dotnet ef database update PreviousMigrationName --connection $azureConn
```

---

## Part 7 — Recommended Security Improvements (Backlog)

| Improvement                                       | Effort | Priority    |
| ------------------------------------------------- | ------ | ----------- |
| Azure Key Vault (move secrets out of App Service) | Medium | Post-launch |
| Health check endpoint `/health`                   | Low    | Post-launch |
| GitHub branch protection on `main`                | Low    | This week   |
| Azure budget alert at $25 CAD                     | Low    | This week   |
| SQL auto-pause delay: change 1hr → 2hr            | Low    | This week   |
| Content Security Policy headers in Program.cs     | Low    | Next sprint |
