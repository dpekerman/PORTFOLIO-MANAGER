---
applyTo: "**"
---

# Portfolio Manager

Angular 22 SPA + .NET 8 Web API + SQL Server. Yahoo Finance data (no API key).

## Agent Execution Rules

- Do not delegate implementation to subagents.
- Do not spawn parallel or background agents.
- Perform all work in the main Copilot agent session.
- Preserve project context across implementation steps.
- Review existing architecture before modifying code.
- Do not introduce architectural changes without justification.
- Run relevant tests after implementation.

## Run

```powershell
cd backend\PortfolioManager.Api ; dotnet run --launch-profile http   # port 5000
cd frontend\portfolio-manager-ui ; npx ng serve                      # port 4200
dotnet ef migrations add <Name> ; dotnet ef database update          # EF migrations
```

DB: run `database/SCRIPTS/01_CreateDatabase.sql` through `03_SeedData.sql` in order.

## State Pattern (MANDATORY for all features)

Two files per feature:
1. `core/services/*-api.service.ts` -- HttpClient wrapper, returns `Observable<T>`, zero state
2. `core/store/*.store.ts` -- NgRx `signalStore` (`@ngrx/signals`): `withState`, `withRequestStatus()` for loading/error, `rxMethod` + `tapResponse` for loads, `onceAuthenticated()` in `withHooks({ onInit })` for the first load. State is patched only inside the store; components read signals and call methods.

Pure-logic or per-instance services (`decision-engine`, `portfolio-rsi-analysis`, `trade-cash-link`, `session-timeout`, `ScreenRefreshService`) stay plain services. Template: `/new-angular-feature`; real example: `core/store/watchlist.store.ts`.

All HTTP via the API services, base `/api` proxied to `localhost:5000/api`.
Always use `DemoModeStore.maskValue()` / `maskPercent()` for monetary display -- never raw signal values.

## Mobile / Devices (MANDATORY for UI work)

Supported: Samsung S21 Ultra (384px), Samsung S23 (360px), Pixel Tablet, desktop. Design for 360px minimum; desktop must stay unchanged. Tables follow `/responsive-material-table` (cards on handsets). Verify with Playwright (`npx playwright test`, projects `desktop-chrome`, `tablet`, `mobile-s21`, `mobile-s23`; API is mocked, no backend needed). CI runs unit tests and e2e.

## Tooling

Angular CLI MCP (`search_documentation`, `get_best_practices`) and Context7 are configured in `.vscode/mcp.json`; use them to verify unfamiliar Angular/Material/NgRx APIs before using them. Official skills `angular-developer` and `angular-new-app` are vendored in `.github/skills/` (see `VENDORED.md`); this file wins on conflicts.

## Backend Conventions

- Enums as strings (`JsonStringEnumConverter` global). New C# enum = new TS string union in `portfolio.models.ts`.
- CORS: `"AngularDevPolicy"` allows `localhost:4200` only. Dev port change -> update `Program.cs`.
- Yahoo Finance: `YahooCrumbService` singleton caches crumb ~1hr. Scanner: 5 symbols/batch + 300ms delay -- never remove throttling.
- Background services (`RsiAlertBackgroundService`, `ValueScreenerSchedulerService`) run on timers -- careful.
- `notification-recipients.json` holds email targets (not DB).

## Angular Rules

- No `standalone: true` (default in v20+)
- `inject()` only -- no constructor injection
- `input()` / `output()` -- never `@Input()` / `@Output()`
- `@if` / `@for` / `@switch` -- never `*ngIf` / `*ngFor`
- `[class.x]` / `[style.x]` -- never `ngClass` / `ngStyle`
- `templateUrl` + `styleUrl` always (never inline)
- App is **zoneless** (Angular 21+ default, no zone.js). Never add `provideZoneChangeDetection`. Programmatic Reactive Forms `setValue`/`patchValue` do not trigger change detection; new forms use Signal Forms (`@angular/forms/signals`, stable in v22). Existing Reactive Forms are migrated separately.

## Features

`features/portfolio` | `features/transactions` | `features/scanner` | `features/allocation` | `features/watchlist-page` | `features/eod-signals` | `features/value-screener` | `features/config`

## Pitfalls

- EF: run `dotnet ef database update` after pull; never edit migration files
- `ValueScreenerSchedulerService` fires 5 PM ET weekdays; override `ScannerRuntimeConfig` for tests
- New feature? Use `/new-angular-feature`. New backend service? Use `/new-backend-service`.