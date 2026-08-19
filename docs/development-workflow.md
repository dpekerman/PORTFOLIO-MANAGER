# Development Workflow — Portfolio Manager

Two environments run from **one codebase**. Local dev and Azure production never break each other
because the environment switch is entirely build-time.

---

## How the Two Environments Coexist

| What | Local (`develop` branch) | Azure (`main` branch) |
|---|---|---|
| Angular API calls | `/api/*` → proxy → `localhost:5000` | `/api/*` → `https://portfolio-api.azurewebsites.net/api/*` via `baseUrlInterceptor` |
| Environment file | `environment.ts` (`apiBaseUrl: ''`) | `environment.prod.ts` (`apiBaseUrl: 'https://...'`) |
| Backend config | `appsettings.Development.json` | `appsettings.Production.json` + App Service env vars |
| Database | Local SQL Server | Azure SQL Database (Free Serverless) |
| Deployment | `npx ng serve` + `dotnet run` | GitHub Actions CD (auto on push to `main`) |

The `baseUrlInterceptor` is a **no-op** when `apiBaseUrl` is empty — local dev is completely unaffected.

---

## Branch Strategy

```
main      ──────────────────────────────────────────────►  Azure production
            ▲                     ▲
            │  release PR         │  hotfix PR (rare)
            │                     │
develop   ──┼─────────────────────┼──────────────────────►  local development
            │
            └─► feature/*  ───────►  develop (via PR)
```

| Branch | Rule |
|---|---|
| `main` | Azure production. Never commit directly. Only merge PRs from `develop`. |
| `develop` | Default branch for all development. Push daily work here. |
| `feature/*` | Optional. Branch from `develop` for larger features; PR back to `develop`. |

---

## Daily Development (local)

```powershell
# Start both servers (local)
D:\PORTFOLIO-MANAGER\start-all.bat

# Work on develop
git checkout develop
# ... edit files ...
git add -A
git commit -m "feat|fix|chore: description"
git push origin develop
```

No deployment happens. CI runs (build + test) on push to `develop`.

---

## Feature Work (optional, for larger changes)

```powershell
git checkout develop
git pull origin develop
git checkout -b feature/my-feature

# ... make changes, commit often ...
git push origin feature/my-feature
```

Then open a Pull Request on GitHub: `feature/my-feature` → `develop`.
After review and CI passes, merge and delete the feature branch.

---

## Deploy to Azure

When `develop` is stable and you want to release to Azure:

```powershell
git checkout main
git pull origin main
git merge develop --no-edit
git push origin main
```

GitHub Actions CD workflow (`.github/workflows/cd.yml`) triggers automatically:
- Builds and deploys .NET 8 API → Azure App Service
- Builds Angular 22 → Azure Static Web Apps

Total deployment time: ~3–5 minutes.
Watch progress at: `https://github.com/dpekerman/PORTFOLIO-MANAGER/actions`

---

## Adding a New EF Migration

Always run migrations from `develop` against your local DB first:

```powershell
cd D:\PORTFOLIO-MANAGER\backend\PortfolioManager.Api

# Create migration
dotnet ef migrations add MigrationName

# Apply locally
dotnet ef database update
```

On the next deploy to Azure, `MigrateAsync()` in `Program.cs` applies the migration
automatically on App Service startup. **No manual step required.**

---

## Updating the Production API URL

If you rename the Azure App Service, update this one file on `develop`:

**`frontend/portfolio-manager-ui/src/environments/environment.prod.ts`**

```typescript
export const environment = {
  production: true,
  apiBaseUrl: 'https://YOUR-NEW-APP-NAME.azurewebsites.net',
};
```

Commit to `develop`, then merge to `main` to deploy.

---

## Secrets and Configuration

| Secret | Where to set it | How it reaches the app |
|---|---|---|
| SQL connection string | Azure App Service → Configuration | `ConnectionStrings__DefaultConnection` env var |
| JWT secret | Azure App Service → Configuration | `Jwt__Secret` env var |
| Gmail app password | Azure App Service → Configuration | `EmailNotification__Password` env var |
| CORS origin | Azure App Service → Configuration | `CorsOrigin` env var |
| Static Web Apps token | GitHub repo → Secrets → `AZURE_STATIC_WEB_APPS_API_TOKEN` | GitHub Actions |
| App Service publish profile | GitHub repo → Secrets → `AZURE_WEBAPP_PUBLISH_PROFILE` | GitHub Actions |

**Never commit secrets to any branch.** All secrets live in Azure portal or GitHub Secrets.

---

## After a Merge to Main — What to Expect

1. GitHub Actions starts within ~30 seconds
2. `deploy-backend` and `deploy-frontend` run in parallel
3. App Service restarts with new code; EF migrations run on startup
4. Angular static files replace the previous version in Static Web Apps
5. CI email notification sent on success (if configured)

If a deployment fails, the previous version keeps running in Azure — there is no downtime
window where the app is completely unavailable.

---

## Quick Reference

| Task | Command |
|---|---|
| Start local dev | `start-all.bat` |
| Run backend tests | `cd backend ; dotnet test PortfolioManager.Tests/` |
| Angular prod build (test) | `cd frontend/portfolio-manager-ui ; npx ng build --configuration production` |
| Deploy to Azure | `git checkout main ; git merge develop --no-edit ; git push origin main` |
| Add EF migration | `dotnet ef migrations add Name` (in `backend/PortfolioManager.Api`) |
| View CD run | `https://github.com/dpekerman/PORTFOLIO-MANAGER/actions` |
