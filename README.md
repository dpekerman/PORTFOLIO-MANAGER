# Portfolio Manager

A full-stack, executive-grade stock/options portfolio tracker, RSI momentum scanner, and EOD automation platform — built with **Angular 22**, **.NET 8**, and **SQL Server**, using [Yahoo Finance](https://finance.yahoo.com) as the market data provider.

> **No API key required.** All live data is sourced from Yahoo Finance — just run the backend and start scanning.

Repo: [github.com/dpekerman/PORTFOLIO-MANAGER](https://github.com/dpekerman/PORTFOLIO-MANAGER)

---

## Features

- **Account Types Configuration** - shared stock/option/cash choices, managed by Admins in Configuration -> Account Types. Add, rename, and delete save individually; used types cannot be deleted.

- **Portfolio & Transactions** — CRUD for stock/option positions, full transaction history with context capture (splits, dividends, cash flows)
- **Cash Ledger** — running cash balance, manual adjustments, auto-linking of trades to cash movements, ledger start date tracking
- **Options Tracking** — separate options book (calls/puts, premiums) rolled into total portfolio value
- **RSI Momentum Scanner** — TSX/US watchlist scan with RSI(14), Stochastics, MACD crossover, Bollinger Bands, OBV volume, 50/200 DMA deviation, and an aggregate Low/Medium/High reversal-probability rating
- **EOD Signals & Staged Signals** — end-of-day CONFIRMED vs. EARLY WARNING signal classification, persisted daily, with Fibonacci levels and technical channel analysis
- **EOD Automation Pipeline** — scheduled orchestrator that runs the full close-of-market cycle (scan → persist → snapshot → notify), with missed-run watchdog, recovery, and diagnostics endpoints
- **Value Screener** — scheduled fundamental/technical screening job (weekdays 5 PM ET) with persisted results and manual refresh
- **Market Leadership Tracker** — sector/industry relative-strength leadership calculations
- **Allocation & Risk** — sector/position risk targets, allocation-vs-target breakdown
- **Portfolio Value History** — daily EOD snapshots (stocks/cash/options split) with backfill & reconciliation endpoints
- **Dashboard** — portfolio summary, EOD summary, portfolio actions/action-center, performance summary, decision-performance analytics
- **Watchlist** — snapshotting, earnings-date tracking, per-symbol notes
- **Authentication & Roles** — ASP.NET Identity + JWT, first-run admin setup, role-gated endpoints
- **Notifications** — email alerts (MailKit/Gmail SMTP) for signals and automation run results; recipients managed via `notification-recipients.json`
- **Automated Backups** — scheduled database backup background service
- **Demo Mode** — global value-masking (`demoMode.maskValue()` / `maskPercent()`) for screenshots/demos without exposing real balances
- **Dark Bloomberg-style UI** — Angular Material 22, zoneless, signals-based state, OnPush, fully responsive (card layout ≤768px)

---

## Managing account types

Open **Configuration -> Account Types** to add a new choice (1-120 characters).
All existing stock, option, cash, and Link Cash selectors use this shared database list.
Only Admins can change it; authenticated users can view it.

Renaming updates matching records across **all users**, including closed transactions,
cash entries, and stored portfolio snapshots, in one transaction. It does not change
amounts or dates. An account with any remaining records cannot be deleted, even if
its cash balance is zero. Save All and Reset All do not modify account types.

Backend startup applies the generated `AddAccountTypes` migration and imports existing
legacy account names. Whitespace/case collisions are reported instead of merging accounts.
For manual SQL deployment on an existing schema, run
[`23_AddAccountTypes.sql`](database/SQL/23_AddAccountTypes.sql) and then
[`24_WidenAccountTypeNames.sql`](database/SQL/24_WidenAccountTypeNames.sql) (names up to
120 characters) before restarting the API.
The master deployment script includes the equivalent upgrade.
The design-time factory allows EF scaffolding without starting the API. It uses the
same configuration as the API; review the intended database or supply an explicit
`--connection` before using `dotnet ef database update`.

Old backups may contain names removed or renamed since export. Restore rejects missing
types before replacing records; ask an Admin to add the names or map the backup to
current choices first. Allocation restore commits cash and options together, so an
invalid account type cannot leave a partially replaced allocation.

Focused SQL Server tests use separate, uniquely named LocalDB databases and clean
up only those databases. Set `PM_ACCOUNT_TYPES_SQL_TESTS=1` to enable them.

## Architecture

```
PORTFOLIO-MANAGER/
├── backend/
│   ├── PortfolioManager.Api/         # .NET 8 Web API (port 5000)
│   │   ├── Controllers/              # Portfolio, Transactions, Cash, Options, Scanner,
│   │   │                             #   EodSignals, ValueScreener, AllocationRisk, Watchlist,
│   │   │                             #   Dashboard, Analytics, Automation, Auth, Users, ...
│   │   ├── Data/                     # EF Core 8 DbContext + SQL Server migrations
│   │   ├── Models/                   # entities, DTOs, scanner/technical models
│   │   └── Services/                 # ~55 services: Yahoo Finance client, RSI scanner,
│   │                                 #   EOD automation orchestrator, value screener,
│   │                                 #   market leadership, notifications, backups, auth
│   └── PortfolioManager.Tests/       # xUnit test project
├── frontend/
│   └── portfolio-manager-ui/         # Angular 22 SPA (port 4200)
│       └── src/app/
│           ├── core/                 # models, api/state service pairs, demo mode, guards
│           └── features/             # portfolio, transactions, scanner, allocation,
│                                     #   watchlist-page, eod-signals, value-screener,
│                                     #   portfolio-value-history, dashboard, auth, config
├── database/
│   ├── SCRIPTS/                      # numbered deployment/migration scripts (00 → 18+)
│   └── SQL/                          # supporting SQL assets
├── scripts/                          # Azure migration, backup/restore, EOD automation task setup
└── .github/workflows/                # ci.yml (build/lint/test/audit), cd.yml (Azure deploy)
```

---

## Quick Start (local)

### Prerequisites

| Tool       | Version            |
| ---------- | ------------------ |
| .NET SDK   | 8.0+               |
| Node.js    | 22.x LTS           |
| SQL Server | 2019+ (Express OK) |

### 1 — Database setup

Run the scripts in `database/SCRIPTS/` **in order** against your SQL Server instance (or use `00_MASTER_DeployProduction.sql` for a one-shot production deploy):

```sql
01_CreateDatabase.sql
02_CreateTables.sql
03_SeedData.sql                 -- optional: demo positions
04_SeedNotificationRecipients.sql
11_AddIdentityAndAuth.sql        -- required for login
...                              -- remaining numbered migrations, in order
```

### 2 — Backend

```powershell
cd backend\PortfolioManager.Api
dotnet run --launch-profile http
# API available at http://localhost:5000/swagger
```

### 3 — Frontend

```powershell
cd frontend\portfolio-manager-ui
npm install
npx ng serve
# UI available at http://localhost:4200
```

### One-click launch

```cmd
start-all.bat     # kills existing processes on 5000/4200, starts both in separate windows
```

Other root-level helper scripts: `start-backend.bat`, `start-frontend.bat`, `add-firewall-rules.bat` (LAN/mobile access), `show-costs.bat` (Azure monthly cost report), `migrate-to-azure.bat` (cloud migration).

---

## API Endpoints (selected)

| Area            | Path                                                                               | Description                               |
| --------------- | ---------------------------------------------------------------------------------- | ----------------------------------------- |
| Portfolio       | `/api/portfolio`                                                                   | CRUD portfolio positions                  |
| Cash            | `/api/cash`, `/api/cash/ledger-start-date`                                         | Cash ledger entries & balance adjustments |
| Options         | `/api/options`                                                                     | CRUD options positions                    |
| Stocks          | `/api/stocks/quotes`, `/api/stocks/quote/{symbol}`                                 | Live Yahoo Finance quotes                 |
| Scanner         | `/api/scanner/rsi`, `/api/scanner/rsi/snapshot`, `/api/scanner/market-indices`     | RSI scan, cached snapshot, market indices |
| EOD Signals     | `/api/eod-signals`, `/api/eod-signals/meta`                                        | Daily confirmed/early-warning signals     |
| Value Screener  | `/api/valuescreener/analyze`, `/api/valuescreener/latest`                          | Fundamental screener results              |
| Allocation Risk | `/api/allocation-risk`                                                             | Sector/position risk targets              |
| Watchlist       | `/api/watchlist`, `/api/watchlist/snapshot`                                        | Watchlist CRUD + snapshotting             |
| Portfolio Value | `/api/portfoliovaluehistory/latest`, `/range`, `/record-now`                       | Daily EOD value history                   |
| Dashboard       | `/api/dashboard`, `/api/dashboard/eod-summary`, `/api/dashboard/market-leadership` | Aggregated dashboard data                 |
| Analytics       | `/api/analytics/decision-performance`                                              | Trade decision performance analytics      |
| Automation      | `/api/automation/trigger`, `/status/{runId}`, `/history`                           | EOD automation pipeline control           |
| Notifications   | `/api/notification/recipients`, `/status`                                          | Email notification config                 |
| Auth            | `/api/auth/setup`, `/api/auth/login`                                               | First-run admin setup + JWT login         |
| Users           | `/api/users`, `/api/users/preferences`                                             | User management & preferences             |

Full request/response contracts are available via Swagger at `http://localhost:5000/swagger` when running the backend in Development.

---

## Background Services

Several `IHostedService` workers run continuously alongside the API:

- `PortfolioValueEodBackgroundService` — records the daily EOD portfolio value snapshot
- `RsiAlertBackgroundService` — scans for RSI signal changes and triggers notifications
- `ValueScreenerSchedulerService` — runs the value screener weekdays at 5 PM ET
- `AutomationMissedRunWatchdogService` — detects and recovers missed EOD automation runs
- `DatabaseBackupBackgroundService` — scheduled database backups

---

## Environment Configuration

**Never commit connection strings or secrets.**

| Secret                   | Where to set                                                                             |
| ------------------------ | ---------------------------------------------------------------------------------------- |
| SQL connection           | `appsettings.json` locally; GitHub Secret `SQL_CONNECTION_STRING` in CI                  |
| JWT signing key          | `.NET User Secrets` locally; App Service settings in Azure                               |
| Gmail App Password       | GitHub Secret `GMAIL_APP_PASSWORD` (CI notify) / `EMAIL_APP_PASSWORD` (CD → App Service) |
| Notification recipients  | `notification-recipients.json` (not stored in DB)                                        |
| Azure deploy credentials | GitHub Secrets `AZURE_WEBAPP_PUBLISH_PROFILE`, `AZURE_STATIC_WEB_APPS_API_TOKEN`         |

---

## GitHub Branch Strategy

| Branch      | Purpose                                     |
| ----------- | ------------------------------------------- |
| `main`      | Production-ready, protected. PR required.   |
| `develop`   | Integration branch for features.            |
| `feature/*` | Short-lived feature branches off `develop`. |

---

## CI/CD

- **`ci.yml`** — runs on every push/PR to `main`/`develop`: .NET build + xUnit tests, Angular production build + lint, npm audit (high+), NuGet vulnerability scan, artifact upload (`api-drop`, `angular-drop`), success email notification.
- **`cd.yml`** — runs on push to `main` (or manual dispatch): deploys the API to Azure App Service and the Angular SPA to Azure Static Web Apps. Gracefully skips if Azure secrets aren't configured yet — see `docs/cloud-migration-guide.md`.

---

## Security Notes

- ASP.NET Identity + JWT bearer auth on protected endpoints; first-run `/api/auth/setup` provisions the initial admin
- CORS locked to `localhost:4200` (`AngularDevPolicy`) in development
- Rate limiting enabled on the API (`AddRateLimiter`)
- EF Core parameterized queries prevent SQL injection
- Secrets (SQL, JWT, SMTP) never committed — use `.NET User Secrets` locally, GitHub Secrets / Azure App Settings in CI/CD
- No sensitive data stored in browser localStorage; demo mode masks monetary values for screenshots

---

## Further Reading

See `docs/` for detailed implementation reports and guides, including EOD automation pipeline, market leadership tracker, cash ledger, auth & roles, Azure setup/cost checklist, and cloud migration.
