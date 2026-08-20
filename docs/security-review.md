# Security Review — Portfolio Manager Azure Deployment

**Date:** 2026-08-20  
**Scope:** Azure-hosted deployment with personal financial data  
**Users:** Maximum 3 (Admin, Trader, Viewer)

---

## Current Security Status

### ✅ Already Secured

| Control                | Implementation                                                    | Status |
| ---------------------- | ----------------------------------------------------------------- | ------ |
| Authentication         | ASP.NET Core Identity + JWT Bearer + HttpOnly refresh cookie      | ✅     |
| Authorization          | `[Authorize]` on all controllers except `/api/auth/*`             | ✅     |
| Token expiry           | Access token: 15 min, Refresh token: 7 days                       | ✅     |
| Refresh token storage  | SHA-256 hashed + revocation tracking in DB                        | ✅     |
| No public registration | `/api/auth/setup` is first-run only (guarded by user count check) | ✅     |
| HTTPS only             | Enforced by App Service + SWA                                     | ✅     |
| CORS                   | Restricted to `gray-smoke-012fa200f.7.azurestaticapps.net` only   | ✅     |
| Rate limiting          | 200 requests/minute per IP (built-in .NET middleware)             | ✅     |
| Swagger                | Disabled in production (`IsDevelopment()` gate)                   | ✅     |
| Password policy        | 8+ chars, upper + lower + digit required                          | ✅     |
| Role-based access      | Admin / Trader / Viewer roles enforced                            | ✅     |
| SQL injection          | EF Core parameterized queries throughout                          | ✅     |
| App Service settings   | Encrypted at rest by Azure                                        | ✅     |
| Azure SQL firewall     | Only Azure services + your IP allowed                             | ✅     |

---

## ⚠️ Recommendations (Action Required)

### 1. Email Password — Fix Now

The `EmailNotification__Password` value in App Service settings (`@Dima1970`) does not look like a Gmail App Password (should be 16 characters, format `xxxx xxxx xxxx xxxx`). Using your regular Gmail password here is a **security risk** — if the App Service settings were ever exposed, your main Google account password would be compromised.

**Fix:**

1. Go to [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords)
2. Create an App Password named "Portfolio Manager Azure"
3. Copy the 16-character code (e.g., `abcd efgh ijkl mnop`)
4. Update `EmailNotification__Password` in App Service → Environment variables → Save

---

### 2. Database Password in Connection String — Low Priority

The SQL password `@Fang1970` appeared in VS Code terminal history during the migration process. It is also stored in the App Service `ConnectionStrings__DefaultConnection` setting (encrypted at rest).

**Recommended:** Change the SQL password now that it has been exposed in terminal output.

1. Azure portal → SQL Server `portfolio-sql-dpekerman` → **Security → Reset password**
2. Set a new strong password (16+ chars)
3. Update `ConnectionStrings__DefaultConnection` in App Service → Environment variables
4. Update your Notepad record

---

### 3. Session Timeout — Review Setting

The Angular app has a `SessionTimeoutService`. Verify the timeout is configured appropriately for a financial application.

Recommended: 15–30 minutes of inactivity for a portfolio manager app.

---

### 4. Budget Alert — Set If Not Done

Unexpected Azure charges can occur if resources are misconfigured. Set a monthly budget alert:

1. Azure portal → **Cost Management + Billing** → **Budgets** → **+ Add**
2. Amount: `$25 CAD`
3. Alert at 90% → your email
4. Click **Create**

---

### 5. Azure SQL Auto-Pause Behavior

The free serverless SQL database auto-pauses after 1 hour of inactivity. When the API starts after a pause, `MigrateAsync()` may take 10–30 seconds while the DB wakes up. This is normal but users will experience a slow first load.

**Optional fix** — set minimum auto-pause delay to avoid interrupting EOD background services:

- Azure portal → SQL Database → **Configure** → **Auto-pause delay** → set to `60 minutes` (default) or `120 minutes`

---

### 6. Application Insights — Already Enabled

Azure auto-added Application Insights when the App Service was created. Your app logs are already being collected. To view them:

- Azure portal → `portfolio-manager202608192326` → **Logs** tab
- Or: App Service → **Log stream** (real-time)

---

## Future Security Improvements (Phase 2)

These are not required now but worth considering as the app grows:

| Improvement                     | Benefit                                                    | Effort |
| ------------------------------- | ---------------------------------------------------------- | ------ |
| Azure Key Vault                 | Removes secrets from App Service settings entirely         | Medium |
| Health Check endpoint           | Azure monitors and restarts unhealthy instances            | Low    |
| IP allowlist                    | Restrict API to known IPs only (if all users on fixed IPs) | Low    |
| Content Security Policy headers | Prevent XSS via Angular response headers                   | Low    |
| Password strength audit         | Enforce complexity for the 3 created accounts              | Low    |

---

## Quick Security Checklist (Do These Today)

- [ ] Fix `EmailNotification__Password` → get a real Gmail App Password
- [ ] Change SQL password (was exposed in terminal history)
- [ ] Set Azure budget alert at $25 CAD
- [ ] Confirm all 3 user accounts have strong passwords
- [ ] Set up GitHub branch protection rules (see development-workflow.md)
