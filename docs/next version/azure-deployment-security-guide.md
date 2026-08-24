# Azure Deployment & Security Guide — v2.0

**Portfolio Manager | Updated: 2026-08-21**
**Azure status: LIVE**

---

## Quick Reference — Live Azure Resources

| Resource       | Name / URL                                        |
| -------------- | ------------------------------------------------- |
| Resource Group | `rg-portfolio-manager` (Canada Central)           |
| App Service    | `portfolio-manager202608192326.azurewebsites.net` |
| Static Web App | `gray-smoke-012fa200f.7.azurestaticapps.net`      |
| SQL Server     | `portfolio-sql-dpekerman.database.windows.net`    |
| SQL Database   | `PortfolioManagerDb`                              |
| Monthly cost   | ~$18–22 CAD                                       |

---

## Part 1 — Security Fixes (Do Before Next Deploy)

### Fix 1 — Gmail App Password (CRITICAL)

`EmailNotification__Password` in App Service settings may be a regular Gmail password, not a 16-character App Password. If settings are ever exposed, your main Google account is at risk.

1. Go to [myaccount.google.com/apppasswords](https://myaccount.google.
com/apppasswords)
2. Enable 2-Step Verification if not already on
3. Create App Password → name it `Portfolio Manager Azure`
4. Copy the 16-character code (format: `xxxx xxxx xxxx xxxx`)
5. Azure Portal → App Service → **Settings → Environment variables**
6. Edit `EmailNotification__Password` → replace with 16-char app password → **Save → Apply**

---

### Fix 2 — Change SQL Server Password (CRITICAL)

Password `@Fang1970` appeared in VS Code terminal history during initial migration.

1. Azure Portal → SQL servers → `portfolio-sql-dpekerman`
2. Left menu → **Security → Reset password**
3. Set a new strong password (16+ chars: upper + lower + digit + symbol)
4. Azure Portal → App Service → **Settings → Environment variables**
5. Edit `ConnectionStrings__DefaultConnection` → replace `Password=@Fang1970;` with new password
6. **Save → Apply** → wait for App Service restart
7. Test: open `https://gray-smoke-012fa200f.7.azurestaticapps.net` → verify login works

---

### Fix 3 — Session Timeout (Recommended)

Default session timeout is 480 minutes (8h). Reduce for a financial app.

1. Open Portfolio Manager → **Configuration → Scanner tab**
2. Set **Session timeout** to `30` minutes → **Save Scanner Settings**

---

### Fix 4 — Azure Budget Alert (Recommended)

1. Azure Portal → search **Cost Management + Billing** → **Budgets** → **+ Add**
2. Name: `portfolio-monthly-cap`, Reset period: Monthly, Amount: `25` CAD
3. Alert condition: Actual cost > 90% → email `dima.pekerman@gmail.com`
4. **Create**

---

### Fix 5 — SQL Auto-Pause Delay (Recommended)

Free serverless SQL pauses after 1hr inactivity. EOD background service runs at 4:30 PM ET — extend delay to avoid interrupted window.

1. Azure Portal → SQL databases → `PortfolioManagerDb` → **Configure**
2. **Auto-pause delay** → change from `1 hour` to `2 hours` → **Apply**

---

### Fix 6 — Security Response Headers (Low Priority)

Add to `Program.cs` after `app.UseHttpsRedirection()`:

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});
```

---

### Fix 7 — GitHub Branch Protection (Low Priority)

1. GitHub → `dpekerman/PORTFOLIO-MANAGER` → **Settings → Branches**
2. **Add branch protection rule** → pattern: `main`
3. Enable: Require PR before merging, Require status checks to pass (`build`), Include administrators
4. **Create**

---

## Part 2 — Deploying New Features to Azure (develop → main → Azure)

### Step 1 — Pre-Deploy Checks on develop

```powershell
cd backend\PortfolioManager.Api
dotnet build                                         # 0 errors
dotnet test ..\PortfolioManager.Tests\               # all green

cd ..\..\frontend\portfolio-manager-ui
npx ng build --configuration production             # must succeed
```

---

### Step 2 — Apply EF Migrations Against Azure SQL (when new migrations added)

Run from local machine with Azure connection string. Never run schema changes from CI.

```powershell
cd backend\PortfolioManager.Api

$azureConn = "Server=tcp:portfolio-sql-dpekerman.database.windows.net,1433;" +
             "Initial Catalog=PortfolioManagerDb;" +
             "User ID=portfolioadmin;Password=YOUR_NEW_SQL_PASSWORD;" +
             "Encrypt=True;Connection Timeout=30;"

dotnet ef database update --connection $azureConn

# Verify all applied
dotnet ef migrations list --connection $azureConn
```

**Current new migrations to apply for this release:**

- `20260821160920_AddRsiSnapshotAndUserPreferences` — creates `RsiScanSnapshots` and `UserPreferences` tables

---

### Step 3 — Merge develop → main

1. Push final commits to `develop`
2. GitHub → **Pull requests → New pull request** → Base: `main` ← Compare: `develop`
3. Title: `feat: RSI snapshot persistence + user preferences DB`
4. Verify CI `build` passes → **Approve → Merge**

---

### Step 4 — GitHub Actions Auto-Deploy

After merge to `main`, `.github/workflows/cd.yml` automatically:

- Builds .NET 8 API → deploys to App Service `portfolio-manager202608192326`
- Builds Angular → deploys to Static Web App

Monitor: GitHub → **Actions** tab → watch latest `cd.yml` run (~4–6 minutes)

---

### Step 5 — Post-Deploy Verification

Open `https://gray-smoke-012fa200f.7.azurestaticapps.net` in incognito.

| Check           | Expected                                   |
| --------------- | ------------------------------------------ |
| App loads       | Angular SPA renders                        |
| Login           | Redirects to dashboard                     |
| RSI Scanner     | Shows data immediately (cached snapshot)   |
| Manual Refresh  | Live scan fires, "cached" badge disappears |
| Interval = 0:00 | No auto-refresh fires                      |
| Column settings | Persist after logout/login across browsers |
| App config      | RSI thresholds persist after logout/login  |
| Log stream      | No Exception entries in first 60s          |

**App Service log stream:**
Azure Portal → `portfolio-manager202608192326` → **Log stream**

---

### Rollback

Azure Portal → App Service → **Deployment Center → Deployment logs** → find previous → **Redeploy**

```powershell
# DB rollback (if needed)
dotnet ef database update PreviousMigrationName --connection $azureConn
```

---

## Part 3 — Environment Variables Reference

| #   | Variable                               | Value                                                | Secret  |
| --- | -------------------------------------- | ---------------------------------------------------- | ------- |
| 1   | `ASPNETCORE_ENVIRONMENT`               | `Production`                                         | No      |
| 2   | `ConnectionStrings__DefaultConnection` | Azure SQL ADO.NET conn string                        | **Yes** |
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

## Quick Security Checklist

- [ ] Generate Gmail App Password → update `EmailNotification__Password`
- [ ] Change SQL password → update `ConnectionStrings__DefaultConnection`
- [ ] Set budget alert at $25 CAD
- [ ] Set SQL auto-pause to 2 hours
- [ ] Set session timeout to 30 min in Config page
- [ ] Add security response headers to `Program.cs`
- [ ] Set up GitHub branch protection on `main`

---

## Future Security Improvements

| Improvement                     | Benefit                                          | Effort |
| ------------------------------- | ------------------------------------------------ | ------ |
| Azure Key Vault                 | Remove secrets from App Service entirely         | Medium |
| Health check `/health` endpoint | Azure auto-restarts unhealthy instances          | Low    |
| IP allowlist                    | Restrict API to known IPs                        | Low    |
| Password strength audit         | Ensure all 3 user accounts have strong passwords | Low    |
