---
applyTo: "**"
---

# Portfolio Manager

Angular 22 SPA + .NET 8 Web API + SQL Server. Yahoo Finance data (no API key).

## Run

```powershell
cd backend\PortfolioManager.Api ; dotnet run --launch-profile http   # port 5000
cd frontend\portfolio-manager-ui ; npx ng serve                      # port 4200
dotnet ef migrations add <Name> ; dotnet ef database update          # EF migrations
```

DB: run `database/SCRIPTS/01_CreateDatabase.sql` through `03_SeedData.sql` in order.

## State Pattern (MANDATORY for all features)

Two services per feature in `core/services/`:
1. `*-api.service.ts` -- HttpClient wrapper, returns `Observable<T>`, zero state
2. `*-state.service.ts` -- `signal()`/`computed()` state, calls API service, `.asReadonly()` exposed

All HTTP via `PortfolioApiService`, base `/api` proxied to `localhost:5000/api`.
Always use `demoMode.maskValue()` / `maskPercent()` for monetary display -- never raw signal values.

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

## Features

`features/portfolio` | `features/transactions` | `features/scanner` | `features/allocation` | `features/watchlist-page` | `features/eod-signals` | `features/value-screener` | `features/config`

## Pitfalls

- EF: run `dotnet ef database update` after pull; never edit migration files
- `ValueScreenerSchedulerService` fires 5 PM ET weekdays; override `ScannerRuntimeConfig` for tests
- New feature? Use `/new-angular-feature`. New backend service? Use `/new-backend-service`.