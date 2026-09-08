# EOD Automation Pipeline — Implementation Report

**Date:** 2026-09-07
**Branch:** `develop`
**Status:** Backend + Frontend implemented, built, and tested. Windows-side activation (publish, Scheduled Task registration, secret generation) is a manual follow-up — see "Final Step" below.

---

## 1. Summary

Implemented the EOD Automation Pipeline exactly per the finalized, multi-round-reviewed plan: a thin orchestrator that wakes/keeps the machine awake, calls the **existing, unmodified** `DataRefreshService.RefreshAllAsync()`, and observes (without duplicating) the three pre-existing background services (RSI/EOD Signals, Portfolio Snapshot, Value Screener) to produce an honest, auditable per-day run log.

No new EOD/RSI/snapshot engine was built. No existing background service, business-time config, or financial calculation logic was rewritten. Exactly two small, explicitly pre-approved edits were made to existing files (`Program.cs`, `PortfolioValueEodBackgroundService.cs`); everything else is net-new, additive code.

---

## 2. What was built, by phase

### Step 0 — Safety checkpoint (completed first, before any code)
- Confirmed no frontend/backend processes were running.
- Git tag `pre-automation-pipeline-2026-09-07` (rollback point for the whole feature).
- Verified/created `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\before-WEEKUP-CHANGES\`, explicitly granted `NT SERVICE\MSSQLSERVER` Modify permission (in addition to inherited `Authenticated Users` Modify).
- Full backup of `PortfolioManagerLocal` (`WITH INIT` — `COMPRESSION` dropped, not supported on SQL Server **Express** edition).
- **Verified** via `RESTORE VERIFYONLY` — "The backup set on file 1 is valid." File confirmed on disk (15,065,088 bytes).

### Phase B — Database (isolated commit, tag `phase-b-automation-run-log-2026-09-07`)
- New `AutomationRunLog` entity + EF migration `20260907234053_AddAutomationRunLog` (one new table, additive only).
- Uses this codebase's existing convention for lifecycle fields: plain `string` status columns (matching `DailySignal.SignalState`/`ScanType`), not C# enums.
- Build: clean. Tests: 225/225 passed.

### Phase C — Backend orchestration (tag `phase-cde-automation-backend-2026-09-07`)
New files only, except one small guard added to one existing file:
- `SystemAwakeService.cs` — handle-based Windows power request (`PowerCreateRequest`/`PowerSetRequest`/`PowerClearRequest` via P/Invoke with a `SafeHandle`), **not** `SetThreadExecutionState` (which is thread-affine and unsafe across `async`/`await` continuations). Requests `SystemRequired` only — never forces the display to stay on. Falls back to a no-op lease on non-Windows.
- `TradingSessionGuard.cs` — shared trading-day check (latest daily bar for `^GSPC`, already used elsewhere in this codebase for market indices, compared to today's ET date) — closes the confirmed pre-existing holiday gap.
- `AutomationSecretStore.cs` — DPAPI (`CurrentUser` scope) generate/validate for the local-only trigger secret. Stored only in `%LOCALAPPDATA%\PortfolioManager\automation-secret.protected` — never in source control, `appsettings.json`, or logs.
- `AutomationRuntimeConfig.cs` — machine wake/keep-awake settings, persisted to `automation-config.json` (same pattern as the existing `ScannerRuntimeConfig`) — deliberately **separate** from the EOD Window / Value Screener business-time config.
- `AutomationStatuses.cs` — shared status vocabulary (`Succeeded`/`Failed`/`NotEligible`/`NotScheduled`/`NotObserved`) so expected non-execution is never reported as a false failure.
- `AutomationRunCoordinator.cs` — singleton, single-flight execution; a second trigger while a run is in flight returns the **same** `runId` rather than starting a duplicate; executes the orchestrator inside its own `CreateAsyncScope()` with a token linked to `IHostApplicationLifetime.ApplicationStopping` — never the caller's `RequestAborted` token.
- `EodAutomationOrchestratorService.cs` — the orchestrator itself: acquires the power-request lease → resolves the single Admin-role owner → trading-day guard → calls `IDataRefreshService.RefreshAllAsync()` (**unchanged, reused**) → bounded verification poll (eligibility-aware RSI/Snapshot/Value-Screener status) → finalizes `OverallStatus`. Also implements the separate, non-destructive `RunTestWakeAsync` path.
- **One small guard added to `PortfolioValueEodBackgroundService.cs`** (the single pre-approved exception): a `TradingSessionGuard` check before writing a snapshot, fixing the confirmed gap where a weekday market holiday could record a snapshot mislabeled with that date using stale last-traded quotes. `DeriveSnapshotStatus`/`LastRecalculatedAt`/Original-Resealed derivation logic in `PortfolioValueHistoryService` is untouched.
- **One small addition to `Program.cs`**: a named-`Mutex` single-instance guard at the very top (before `WebApplication.CreateBuilder`) — a second backend instance exits immediately rather than running side-by-side with the first. Plus the new DI registrations.
- A real DI-container bug was caught and fixed during this phase: `ITradingSessionGuard` (Scoped) was initially injected directly into the Singleton `PortfolioValueEodBackgroundService` — ASP.NET Core's built-in validator correctly rejected this at startup; fixed by resolving it from the per-call `IServiceScopeFactory` scope instead (same pattern already used for the service's other scoped dependencies).

### Phase D — API (same tag as Phase C)
New `AutomationController`:
- `POST /api/automation/trigger` — **not** JWT-protected (the Scheduled Task has no login session); guarded by loopback-only (`IPAddress.IsLoopback`) + DPAPI-validated `X-Automation-Key` header (`CryptographicOperations.FixedTimeEquals`). Returns `202 Accepted { runId }` immediately.
- `POST /api/automation/run-now` (Admin JWT) — "Run Automation Now"; identical orchestrator path as Scheduled, never bypasses business-time gates.
- `POST /api/automation/test-wake` (Admin JWT) — non-destructive infra check only.
- `GET /api/automation/status/{runId}`, `GET /api/automation/last-run`, `GET /api/automation/history` (Admin JWT).
- `POST /api/automation/rotate-secret` (Admin JWT) — regenerates the DPAPI secret; never echoes the plaintext value.
- `GET/PUT /api/automation/settings` (Admin JWT) — editable wake/keep-awake fields + read-only echo of the existing EOD Window / Value Screener config (never duplicated in storage).
- `POST /api/automation/setup`, `GET /api/automation/task-status` (Admin JWT) — Scheduled Task registration (one-time UAC elevation via `Process.Start(Verb="runas")`) and read-only status query.
- `GET /api/health` (unauthenticated, trivial) — used only by the local trigger script's readiness check.

### Phase E — Windows integration scripts
- `scripts/setup-eod-automation-task.ps1` — registers/updates the Wake-enabled Scheduled Task (`-WakeToRun -StartWhenAvailable`, "Run only when user is logged on"). Requires Administrator (one-time, or via "Enable/Repair Automation" in the UI).
- `scripts/run-eod-automation-trigger.ps1` — the Task's actual daily action: health-check → start the **published** build detached/hidden if not already running (bounded 60s health-check retries; readiness is the sole authoritative signal, exit codes not inspected) → DPAPI-decrypt the local secret → `POST /api/automation/trigger`.

### Phase F — Frontend (tag `phase-f-automation-frontend-2026-09-07`)
- New `AutomationApiService` (HTTP only) + `AutomationStateService` (signals, polling `/status/{runId}` while a run is in flight) — two-service pattern.
- New DTOs in `portfolio.models.ts`.
- New **Automation** tab in the existing Configuration page (Admin-only, same pattern as Portfolio History/Users): Enable/Repair Automation, editable wake/keep-awake/grace fields, read-only EOD Window/Value Screener echo, Windows Task status, last-run step summary (using the honest status vocabulary, not a binary ✓/✗), Rotate Secret, Test Wake, Run Automation Now, run history.
- `npx ng build` — succeeds. Only pre-existing SCSS budget warnings on files never touched by this change (confirmed by diff scope).

---

## 3. Testing performed (every phase)

| Phase | Build | Backend tests | Runtime smoke test |
|---|---|---|---|
| B | Clean | 225/225 passed | — |
| C | Clean (after fixing 1 DI lifetime bug) | 225/225 passed | Backend started cleanly; all 3 pre-existing background services started; existing endpoints (`/api/auth/setup-required` → 200, `/api/scanner/eod-settings` → 401 as before) unchanged |
| D | Clean | 225/225 passed | `/api/health` → 200; `/api/automation/trigger` (no secret) → 403; `/api/automation/run-now` (no JWT) → 401; pre-existing endpoint re-confirmed unchanged |
| F | Clean (`ng build`) | n/a (frontend) | — |
| Final | — | 225/225 passed (re-run) | Ports 5000/4200 confirmed free after each smoke test |

**Full diff since the pre-implementation checkpoint:** 23 files changed, 3,853 insertions, **0 deletions** outside the two pre-approved small edits. No existing test was modified. No existing service's public behavior changed except the one documented holiday guard.

---

## 4. Database impact — confirmed

```sql
SELECT COUNT(*) FROM AutomationRunLogs;   -- 0 rows
```

**Zero `AutomationRunLog` rows exist.** The orchestrator has never actually executed during this entire implementation session — every smoke test only verified that unauthenticated/unauthorized calls are correctly rejected (403/401), which happens before the orchestrator is ever invoked.

One incidental, **expected** side effect: during runtime smoke tests, the real (unmodified) backend was briefly started to validate the DI container and routing. Because this happened to be within `PortfolioValueEodBackgroundService`'s own pre-existing 4:30 PM–midnight ET window, that **pre-existing, unmodified** service wrote its normal once-daily snapshot for 2026-09-07 (`Source=EodAuto`, `LastRecalculatedAt=NULL` → "Original") — exactly the behavior it has always had, and something that would have happened regardless of this change the next time the app was run today. This is not something the new Automation feature caused or altered.

No row was ever written to `DailySignals`, `CashItems`, `PortfolioItems`, `OptionItems`, or `Transactions` by any new code.

---

## 5. Rollback points

| Tag | What it captures |
|---|---|
| `pre-automation-pipeline-2026-09-07` | Clean state before any code change (matches the `.bak` file) |
| `phase-b-automation-run-log-2026-09-07` | + `AutomationRunLog` table only |
| `phase-cde-automation-backend-2026-09-07` | + full backend orchestration |
| `phase-f-automation-frontend-2026-09-07` | + frontend Automation tab (current `HEAD`) |

To roll back the code only: `git reset --hard <tag>`.
To roll back the database too: restore `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\before-WEEKUP-CHANGES\PortfolioManagerLocal_pre-automation.bak` (or simply run `dotnet ef database update` down to the previous migration, since the only schema change is the additive `AutomationRunLogs` table).

**Nothing has been pushed to the remote** (`origin/develop`) — all commits and tags are local only, pending your review.

---

## 6. Final step — what's left before this actually runs unattended

The code is complete and tested, but **automation will not fire on its own yet**. Three manual, one-time actions remain, all safe and reversible:

1. **Publish a stable build** (the trigger script deliberately does not use `dotnet run`):
   ```powershell
   cd backend\PortfolioManager.Api
   dotnet publish -c Release -o publish
   ```
2. **Log in as Admin** in the Angular app → **Configuration → Automation**.
3. Click **Enable Automation** — this generates the DPAPI secret (if not already present) and registers the Windows Scheduled Task (one UAC prompt). Verify the tab shows "Task installed" with a sensible next-run time.
4. Verify Windows **Wake Timers** are allowed (Power Options → plan settings → Advanced → Sleep → *Allow wake timers: Enable*), and that the machine is plugged in / its lid setting won't block a wake-from-sleep at the scheduled time.

After that, the Scheduled Task fires on its own daily at the configured `WakeTimeEt` (default 15:15 ET) — no further action needed. Watch the **Automation** tab's "Last run" / history after the first real trading day to confirm end-to-end behavior.

---

## 7. How to test safely, without touching the current database snapshot

**Safest — zero financial-table writes at all:**
Click **Test Wake** in the Automation tab (or `POST /api/automation/test-wake` as Admin). This only acquires the power-request lease, checks `db.Database.CanConnectAsync()`, and makes one read-only quote call — it writes **exactly one** `AutomationRunLog` row and nothing else. Confirm with:
```sql
SELECT * FROM AutomationRunLogs WHERE TriggerType = 'TestWake' ORDER BY CreatedAtUtc DESC;
```

**Next level — exercises the real refresh, still safe:**
Click **Run Automation Now** *outside* the business windows (i.e., not between 15:30–17:15 ET). This will:
- Call `RefreshAllAsync()` for real — which refreshes the `PortfolioSnapshots`/`WatchlistSnapshots`/`DashboardSnapshots` **cache** tables (the same thing that happens every time you open the Portfolio/Watchlist/Dashboard pages — always-overwritten latest-only tables, not historical).
- Report `RsiStatus=NotEligible`, `SnapshotStatus=NotEligible`, `ValueScreenerStatus=NotScheduled`/`NotObserved` — because the real business windows haven't opened, **no** `DailySignals` or `PortfolioValueHistory` row will be created.
- `OverallStatus` should come back `Success`.

This is the recommended way to rehearse the full trigger → coordinator → orchestrator → status-polling flow without any risk to historical data.

**Full end-to-end rehearsal (including a real snapshot write), with zero risk to production data:**
If you want to see a *complete* run — including `PortfolioValueEodBackgroundService` actually writing a snapshot — do it against a throwaway copy of the database rather than `PortfolioManagerLocal`:
```sql
RESTORE DATABASE [PortfolioManagerLocalTest]
FROM DISK = 'D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\before-WEEKUP-CHANGES\PortfolioManagerLocal_pre-automation.bak'
WITH MOVE 'PortfolioManagerDb' TO 'D:\...\PortfolioManagerLocalTest.mdf',
     MOVE 'PortfolioManagerDb_log' TO 'D:\...\PortfolioManagerLocalTest.ldf';
```
Then point a *separate* run of the published backend at that database via `ConnectionStrings:DefaultConnection` (env var or a copy of `appsettings.json`), on a different port, and click Run Automation Now / wait for a real trading window there. Your real `PortfolioManagerLocal` is never touched by this rehearsal.

---

## 8. Known limitations / follow-ups (carried over from the approved plan, not new)

- "Run only when user is logged on" means automation will not fire if the machine is fully logged out (not just asleep) — accepted tradeoff from the original design review.
- DPAPI `CurrentUser` scope ties the secret to one specific Windows user/machine — re-run "Rotate Secret" if that ever changes.
- The `/setup` endpoint's UAC elevation only works because the API runs as a normal desktop process (not a Windows Service) — noted as a constraint if that ever changes.
- `dotnet publish` output location (`backend/PortfolioManager.Api/publish`) is currently a convention, not enforced by config; override via the `-PublishDir` parameter on `run-eod-automation-trigger.ps1` if you publish elsewhere.
