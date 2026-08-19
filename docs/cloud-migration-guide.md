# Portfolio Manager — Azure Cloud Deployment Guide

**Status:** Code preparation complete — awaiting Azure resource creation  
**Target environment:** Azure (Canada Central)  
**Estimated monthly cost:** $18–25 CAD  
**Date prepared:** 2026-08-19

---

## Architecture

```
Internet
    │
    ▼
Azure Static Web Apps (FREE)
    Angular 22 SPA
    HTTPS + SPA routing via staticwebapp.config.json
    │
    │  Angular calls https://portfolio-api.azurewebsites.net/api/*
    ▼
Azure App Service B1 Linux  (~$18 CAD/month, always-on)
    ASP.NET Core .NET 8
    JWT Authentication (already built)
    Rate limiting: 200 req/min per IP
    3 background services — RSI (60s), EOD 4:30 PM ET, Screener 5 PM ET
    EF migrations run automatically on startup
    │
    ▼
Azure SQL Database — Free Serverless
    100,000 vCore-seconds/month + 32 GB storage
    $0/month within free limits
    EF migrations create all tables (Identity + business)
```

**Why App Service B1 over Container Apps:**  
Your 3 background services (`RsiAlertBackgroundService`, `PortfolioValueEodBackgroundService`,
`ValueScreenerSchedulerService`) run continuously. Container Apps scale-to-zero would kill them.
App Service B1 Linux is always-on, simpler to deploy (no Docker required), and the same cost.

**Why Canada Central:**  
Canada East does not support Azure SQL Database Free Serverless or Static Web Apps.
Canada Central (Toronto) has full service availability and keeps data in Canada.

---

## What Was Changed ✅ DONE

| File                                                | Change                                                                                                         |
| --------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| `Services/EodSignalPersistenceService.cs`           | Removed JSON file writes/reads; now queries `DailySignals` DB table — no `eod-signal-history.json` file needed |
| `Program.cs`                                        | Added rate limiting (200 req/min per IP); CORS now also reads `CorsOrigin` config key for production origin    |
| `appsettings.Production.json`                       | New file — production logging levels; secrets come from App Service env vars                                   |
| `src/environments/environment.ts`                   | New — dev uses empty `apiBaseUrl` (proxy handles it)                                                           |
| `src/environments/environment.prod.ts`              | New — production `apiBaseUrl` points to App Service                                                            |
| `src/app/core/interceptors/base-url.interceptor.ts` | New — prepends `apiBaseUrl` to all `/api` calls in production                                                  |
| `src/app/app.config.ts`                             | Added `baseUrlInterceptor` before `authInterceptor`                                                            |
| `angular.json`                                      | Added `fileReplacements` for production build (swaps environment file)                                         |
| `staticwebapp.config.json`                          | New — SPA fallback routing + security headers                                                                  |
| `.github/workflows/cd.yml`                          | New — CD workflow that deploys on every push to `main`                                                         |

---

## Step Legend

- 🤖 **Automated** — GitHub Actions or code handles this automatically
- 👤 **Manual** — You must do this in Azure portal or your terminal
- ✏️ **Edit** — You must edit a file with your specific values

---

## Phase 0 — Git Setup ✅ DONE

- ✅ `develop` merged into `main` and pushed
- ✅ `feature/cloud-migration` branch created, all code implemented, merged into `main` + `develop`, deleted
- ✅ `docs/development-workflow.md` created — see [Development Workflow](development-workflow.md)

---

## Phase 1 — Update `environment.prod.ts` with Your App Service Hostname ✏️ Manual ← START HERE

> **Do this AFTER you create the App Service in Phase 2 (Step 5).**

Once you know your App Service hostname, edit this file:

**File:** `frontend/portfolio-manager-ui/src/environments/environment.prod.ts`

Replace `portfolio-api.azurewebsites.net` with your actual App Service hostname:

```typescript
export const environment = {
  production: true,
  apiBaseUrl: "https://YOUR-APP-NAME.azurewebsites.net", // ← change this
};
```

---

## Phase 2 — Create Azure Resources 👤 Manual (~45 minutes)

### Step 1 — Sign in to Azure portal

Go to [https://portal.azure.com](https://portal.azure.com) and sign in with your Microsoft account.

If you don't have an Azure subscription:

1. Go to [https://azure.microsoft.com/en-ca/pricing/purchase-options/azure-account](https://azure.microsoft.com/en-ca/pricing/purchase-options/azure-account)
2. Click **Start free** or **Pay as you go**
3. You need a credit card for verification, but the SQL database will be $0

---

### Step 2 — Create Resource Group

1. In the portal search bar, type **Resource groups** → click it
2. Click **+ Create**
3. Fill in:
   - **Subscription:** your subscription
   - **Resource group name:** `rg-portfolio-manager`
   - **Region:** `Canada Central`
4. Click **Review + create** → **Create**

---

### Step 3 — Create Azure SQL Database (Free Serverless)

1. Go to [https://aka.ms/azuresqlhub](https://aka.ms/azuresqlhub)
2. In the **Create a database** panel, click **Start free**
3. You should see a **"Free offer applied!"** banner at the top
4. Fill in:
   - **Subscription:** your subscription
   - **Resource group:** `rg-portfolio-manager`
   - **Database name:** `PortfolioManagerDb`
   - **Server:** click **Create new**
     - **Server name:** `portfolio-sql-[yourname]` (must be globally unique, e.g. `portfolio-sql-dpekerman`)
     - **Location:** `Canada Central`
     - **Authentication:** SQL Authentication
     - **Admin login:** `portfolioadmin`
     - **Password:** create a strong password and save it (you'll need it later)
   - Click **OK**
5. **Compute + storage:** should already show "Free offer applied" — do not change
6. **Behavior when free limit reached:** select **Auto-pause the database until next month**
   - This means if you exceed 100K vCore-seconds, it pauses until the 1st of next month
   - Your data is safe — the database is not deleted
7. Click **Review + create** → **Create** → wait ~2 minutes
8. Once created, go to the database → **Settings → Connection strings**
9. Copy the **ADO.NET** connection string — it looks like:
   ```
   Server=tcp:portfolio-sql-[yourname].database.windows.net,1433;Initial Catalog=PortfolioManagerDb;Persist Security Info=False;User ID=portfolioadmin;Password={your_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
   ```
10. Replace `{your_password}` with your actual password → save this full string

**Allow Azure services to connect:**

1. Go to the SQL Server (not the database) → **Security → Networking**
2. Under **Firewall rules**, toggle **Allow Azure services and resources to access this server** → ON
3. Also click **+ Add your client IPv4 address** (adds your current IP for running migrations locally)
4. Click **Save**

---

### Step 4 — Create App Service Plan

1. Search for **App Service plans** → click **+ Create**
2. Fill in:
   - **Resource group:** `rg-portfolio-manager`
   - **Name:** `portfolio-asp`
   - **Operating System:** Linux
   - **Region:** `Canada Central`
   - **Pricing plan:** `B1` (Basic, 1 core, 1.75 GB RAM)
3. Click **Review + create** → **Create**

---

### Step 5 — Create Web App (App Service)

1. Search for **App Services** → click **+ Create** → **Web App**
2. Fill in:
   - **Resource group:** `rg-portfolio-manager`
   - **Name:** `portfolio-api` (or any unique name — this becomes `portfolio-api.azurewebsites.net`)
   - **Publish:** Code
   - **Runtime stack:** `.NET 8 (LTS)`
   - **Operating System:** Linux
   - **Region:** `Canada Central`
   - **Linux Plan:** select `portfolio-asp (B1)` (created in Step 4)
3. Click **Review + create** → **Create**
4. ✏️ **Note down your App Service name** — you need it to update `environment.prod.ts` in Phase 1

---

### Step 6 — Create Static Web App

1. Search for **Static Web Apps** → click **+ Create**
2. Fill in:
   - **Resource group:** `rg-portfolio-manager`
   - **Name:** `portfolio-ui`
   - **Plan type:** Free
   - **Region:** `East US 2` (Static Web Apps free tier is deployed from this region globally)
   - **Deployment details → Source:** GitHub
   - Click **Sign in with GitHub** → authorize
   - **Organization:** `dpekerman`
   - **Repository:** `PORTFOLIO-MANAGER`
   - **Branch:** `main`
   - **Build presets:** Angular
   - **App location:** `frontend/portfolio-manager-ui`
   - **Output location:** `dist/portfolio-manager-ui/browser`
3. Click **Review + create** → **Create**
4. Once created, go to the Static Web App → **Settings → Overview**
5. Copy the **URL** (e.g. `https://purple-wave-123abc.azurestaticapps.net`) — you need it for App Service CORS config

> **Note:** Creating the Static Web App automatically adds a GitHub Actions workflow file to your repo.
> Delete or ignore this auto-generated file — the `cd.yml` already handles deployment correctly.

---

## Phase 3 — Configure App Service Settings 👤 Manual (~10 minutes)

Go to your App Service (`portfolio-api`) → **Settings → Configuration → Application settings**

Click **+ New application setting** for each entry below:

| Name                                   | Value                                             |
| -------------------------------------- | ------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`               | `Production`                                      |
| `ConnectionStrings__DefaultConnection` | your Azure SQL connection string from Step 3      |
| `Jwt__Secret`                          | generate a 64-character random string (see below) |
| `Jwt__Issuer`                          | `PortfolioManager`                                |
| `Jwt__Audience`                        | `PortfolioManagerClient`                          |
| `CorsOrigin`                           | `https://your-static-web-app.azurestaticapps.net` |
| `EmailNotification__Enabled`           | `true`                                            |
| `EmailNotification__SmtpHost`          | `smtp.gmail.com`                                  |
| `EmailNotification__SmtpPort`          | `587`                                             |
| `EmailNotification__UseStartTls`       | `true`                                            |
| `EmailNotification__Username`          | `dima.pekerman@gmail.com`                         |
| `EmailNotification__Password`          | your Gmail app password                           |
| `EmailNotification__FromAddress`       | `dima.pekerman@gmail.com`                         |

Click **Save** after adding all settings.

**Generate JWT secret (run in PowerShell):**

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { [byte](Get-Random -Max 256) }))
```

Copy the output — that is your `Jwt__Secret`. It must be at least 32 characters; 48 bytes encoded = 64 chars.

---

## Phase 4 — GitHub Actions Secrets 👤 Manual (~5 minutes)

Go to your GitHub repository → **Settings → Secrets and variables → Actions → New repository secret**

### Secret 1: AZURE_WEBAPP_PUBLISH_PROFILE

1. Go to your App Service in Azure portal
2. Click **Get publish profile** (top of the Overview page)
3. This downloads an XML file
4. Open the file in a text editor, copy ALL the contents
5. In GitHub Secrets, create:
   - **Name:** `AZURE_WEBAPP_PUBLISH_PROFILE`
   - **Value:** paste the full XML content

### Secret 2: AZURE_STATIC_WEB_APPS_API_TOKEN

1. Go to your Static Web App in Azure portal
2. Click **Manage deployment token** (in the Overview page)
3. Copy the token
4. In GitHub Secrets, create:
   - **Name:** `AZURE_STATIC_WEB_APPS_API_TOKEN`
   - **Value:** paste the token

---

## Phase 5 — Database Setup 👤 Manual (run once from your local machine)

EF migrations handle all table creation — you do NOT use the raw SQL scripts in `database/SCRIPTS/` for Azure. Those were for the original local setup only.

### Run EF migrations against Azure SQL

Open a PowerShell terminal in `backend/PortfolioManager.Api/`:

```powershell
cd D:\PORTFOLIO-MANAGER\backend\PortfolioManager.Api

dotnet ef database update `
  --connection "Server=tcp:portfolio-sql-[yourname].database.windows.net,1433;Initial Catalog=PortfolioManagerDb;User ID=portfolioadmin;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;"
```

This applies all 20+ EF migrations including:

- ASP.NET Identity tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `RefreshTokens`, etc.)
- All business tables (`PortfolioItems`, `WatchlistItems`, `DailySignals`, `StagedSignals`, `ValueScreenerSnapshots`, etc.)
- All indexes and constraints

Expected output: `Applying migration '...'` for each migration → `Done.`

> **Note:** The App Service also runs `MigrateAsync()` on every startup, so future migrations
> (after new deployments) apply automatically without you doing anything.

---

## Phase 6 — Update Environment File and Deploy ✏️ + 🤖

### 6a — Update the production API URL ✏️ Manual

Edit `frontend/portfolio-manager-ui/src/environments/environment.prod.ts`:

```typescript
export const environment = {
  production: true,
  apiBaseUrl: "https://portfolio-api.azurewebsites.net", // ← use your actual App Service name
};
```

### 6b — Commit and deploy to Azure 🤖 Automated

```powershell
cd D:\PORTFOLIO-MANAGER
git checkout develop
git add -A
git commit -m "feat: set production API URL"
git push origin develop
git checkout main
git merge develop --no-edit
git push origin main
```

**GitHub Actions CD workflow triggers automatically on push to `main`:**

1. Job 1: Builds .NET 8 API → deploys to App Service via Zip Deploy
2. Job 2: Builds Angular 22 → deploys to Static Web Apps

Both jobs run in parallel. Total deployment time: ~3–5 minutes.

---

## Phase 7 — First-Run Setup 👤 Manual (done once)

1. Navigate to your Static Web App URL (e.g. `https://purple-wave-123abc.azurestaticapps.net`)
2. You will be redirected to `/setup` — this is the first-run admin creation page
3. Fill in your admin credentials:
   - **Display Name:** Dmitry
   - **Email:** dima.pekerman@gmail.com
   - **Password:** choose a strong password (8+ chars, upper + lower + digit)
4. Click **Setup** — you are now logged in as Admin

**Create additional users** (Admin UI → Config/Users page):

1. Go to Settings → Users (or `/config` page)
2. Click **Add User** for each additional user:
   - User 2: assign role **Trader** or **Viewer**
   - User 3: assign role **Trader** or **Viewer**

---

## Phase 8 — Verification Checklist 👤 Manual

Run these checks after deployment:

| Check                     | How                                                         | Expected                   |
| ------------------------- | ----------------------------------------------------------- | -------------------------- |
| App loads                 | Open Static Web App URL                                     | Redirects to `/login`      |
| Auth enforced             | GET `https://portfolio-api.azurewebsites.net/api/portfolio` | Returns `401 Unauthorized` |
| Swagger disabled          | GET `https://portfolio-api.azurewebsites.net/swagger`       | Returns `404`              |
| Login works               | Use login page with admin credentials                       | Dashboard loads            |
| Background services alive | App Service → **Log stream**                                | RSI scan logs every 60s    |
| CORS correct              | Browser DevTools → check `/api` call origin headers         | No CORS errors             |
| Rate limiting             | Not normally tested; limit is 200 req/min per IP            | —                          |

---

## Monthly Monitoring 👤 Manual (recommended)

### Set a billing alert (do this once)

1. Azure portal → **Cost Management + Billing** → **Budgets**
2. Click **+ Add**
3. **Amount:** 25 (CAD)
4. **Alert condition:** 90% of budget
5. **Alert recipients:** your email
6. Click **Create**

### Monitor SQL Database free limits

1. Go to your SQL Database → **Overview**
2. Check **Free monthly vCore amount** — if it's getting low, your background services are consuming more compute than expected
3. Normal usage for 3 users + Yahoo Finance polling: well within 100K vCore-seconds/month

---

## Troubleshooting

### App Service won't start

1. Go to App Service → **Diagnose and solve problems**
2. Check **Application Logs** → look for startup errors
3. Common cause: missing `Jwt__Secret` or wrong connection string format

### 401 errors on all API calls after login

- Check `CorsOrigin` setting matches the exact Static Web App URL (no trailing slash)
- Check `Jwt__Issuer` and `Jwt__Audience` match values in `appsettings.json`

### Angular 404 on page refresh

- Verify `staticwebapp.config.json` is deployed (it should be in the build output)
- The `/*` → `/index.html` route handles all Angular deep links

### Email notifications not sending

- Verify `EmailNotification__Password` is the Gmail App Password (16-char, no spaces)
  — get it at Google Account → Security → 2-Step Verification → App passwords
- Check App Service log stream for `[EmailNotification]` entries

### SQL Database paused (auto-pause after free limit)

- This is expected if you exceeded 100K vCore-seconds
- The database resumes automatically on the 1st of next month
- App Service will fail to connect until then — plan your usage accordingly
- To prevent: go to SQL Database → **Overview → Behavior when free limit reached** → change to **Continue using database for additional charges** (standard rates apply)

---

## Cost Summary

| Service               | Tier                           | CAD/month             |
| --------------------- | ------------------------------ | --------------------- |
| Azure Static Web Apps | Free                           | $0                    |
| Azure App Service     | B1 Linux, Canada Central       | ~$18                  |
| Azure SQL Database    | Free Serverless (100K vCore-s) | $0                    |
| Outbound bandwidth    | ~1–3 GB/month (3 users)        | $0                    |
| Application Insights  | Free 5 GB ingestion            | $0                    |
| **Total**             |                                | **~$18–22 CAD/month** |

Set a budget alert at **$25 CAD** for safety margin.

---

## After Deployment — Future Changes

All future changes follow this workflow:

```
git checkout develop
# make changes
git push origin develop
# create PR → develop → main
# merge PR → CD workflow deploys automatically
```

EF database migrations in future features apply automatically on App Service startup
(via `MigrateAsync()` in `Program.cs`).

---

## SQL Scripts Reference (local development only)

The scripts in `database/SCRIPTS/` are for setting up a new local SQL Server instance.
**Do NOT run them against Azure SQL.** EF migrations are the authoritative schema source for Azure.

| Script                              | Purpose                                               |
| ----------------------------------- | ----------------------------------------------------- |
| `01_CreateDatabase.sql`             | Creates local DB                                      |
| `02_CreateTables.sql`               | Initial table set (pre-EF)                            |
| `03_SeedData.sql`                   | Optional seed data                                    |
| `14_AddFibonacciToDailySignals.sql` | Fibonacci columns (applied via EF migration on Azure) |
