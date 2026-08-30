# Unified Refresh Progress System — Implementation Report

**Date:** 2026-08-29
**Status:** ✅ Complete (Phases 1–5)

## Summary

Replaced the spinning toolbar refresh icon with a unified, modeless progress popup
that tracks a 5-step refresh pipeline, disables the refresh button while in-flight,
detects offline/server errors, and auto-retries transient failures.

## Goals Delivered

1. Refresh button disabled while a refresh is in-flight (prevents concurrent refreshes).
2. HTTP errors during refresh are detected, classified, and surfaced with retry options.
3. Spinning toolbar icon replaced by a dedicated popup with per-step progress.
4. Per-screen mini-popup infrastructure for independent screen refreshes.
5. Offline detection with auto-retry for network/timeout errors, manual retry for server errors.
6. EOD Signals polling integrated as a real step in the unified refresh (not a stub).

## Backend Changes

| File                                                          | Change                                                                                                                      |
| ------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `backend/PortfolioManager.Api/Models/DataRefreshResultDto.cs` | Added `PortfolioSymbols` / `WatchlistSymbols` (`IReadOnlyList<string>`) so the frontend can display ticker counts per step. |
| `backend/PortfolioManager.Api/Services/DataRefreshService.cs` | Extracts sorted portfolio/watchlist symbol lists before returning the DTO.                                                  |

CancellationToken propagation was already correct — no controller changes needed.

## Frontend Changes

### Services

| File                                         | Change                                                                                                                                                                                                                                                                                                                                                                                                 |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `core/services/app-refresh.service.ts`       | Rewritten. 5-step pipeline (`fetch-portfolio`, `fetch-watchlist`, `dashboard`, `actions`, `eod-signals`), ticker-count progress, `RefreshReason` (`auto-timer`/`user-click`/`feature-refresh`), offline detection (`isOffline`, `offlineReason`: `network`/`server`/`timeout`), auto-retry with 5s countdown (up to 3 attempts, network/timeout only), `navigator.onLine` listener, `cancelRefresh()`. |
| `core/services/eod-signals-state.service.ts` | **New.** Polls `getEodSignals()` every 5 min and `getEodWindowStatus()` every 30s (paused when tab hidden). Exposes `signals`, `meta`, `loading`, `error`, `eodWindowActive`, `lastPollAt`, `newSignalCount`. Called from `AppRefreshService` as the real implementation of the "eod-signals" step.                                                                                                    |
| `core/services/screen-refresh.service.ts`    | **New.** Plain class (not `@Injectable`) meant to be instantiated per screen/component. `AbortController`-based cancellation, `startRefresh()/updateProgress()/setCurrentItem()/completeRefresh()/errorRefresh()/cancel()/getAbortSignal()`.                                                                                                                                                           |
| `core/services/portfolio-state.service.ts`   | Injects `AppRefreshService` (wiring point for future per-screen progress reporting). No behavior change to existing `refresh()`/`setFromRefresh()`.                                                                                                                                                                                                                                                    |
| `core/services/watchlist-state.service.ts`   | Same as above.                                                                                                                                                                                                                                                                                                                                                                                         |
| `core/services/scanner-state.service.ts`     | Same as above.                                                                                                                                                                                                                                                                                                                                                                                         |
| `core/models/portfolio.models.ts`            | `DataRefreshResultDto` interface updated with `portfolioSymbols` / `watchlistSymbols` to match backend DTO.                                                                                                                                                                                                                                                                                            |

### UI Components

| File                                                                    | Change                                                                                                                                                                                                       |
| ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `shared/app-refresh-progress/app-refresh-progress.component.ts`         | Added `getOfflineTitle()`, `getOfflineMessage()`, `showConnectionRestoredSnackbar()` (via `MatSnackBar`), `cancel()`, `retry()`, `dismiss()`.                                                                |
| `shared/app-refresh-progress/app-refresh-progress.component.html`       | Renders all 5 steps with ticker counts (`X/Y`) and current symbol (`→ AAPL`); offline section (icon, title, message, retry countdown); error section (retry/dismiss); success state ("Refreshed X min ago"). |
| `shared/app-refresh-progress/app-refresh-progress.component.scss`       | Styles for offline/error sections, step counts, current-item highlight, retry countdown.                                                                                                                     |
| `shared/screen-refresh-progress/screen-refresh-progress.component.ts`   | **New.** Mini-popup wrapper around a `ScreenRefreshService` instance (`@Input() refreshService`).                                                                                                            |
| `shared/screen-refresh-progress/screen-refresh-progress.component.html` | **New.** Spinner + screen name + progress + current symbol + cancel button.                                                                                                                                  |
| `shared/screen-refresh-progress/screen-refresh-progress.component.scss` | **New.** Fixed top-right position, z-index 1050, slide-in animation.                                                                                                                                         |
| `shared/layout/layout.component.ts`                                     | `refreshAll()` now calls `appRefresh.refreshAll('user-click')`. Added `isRefreshDisabled = computed(() => appRefresh.isRefreshing())`.                                                                       |
| `shared/layout/layout.component.html`                                   | Refresh button bound to `[disabled]="isRefreshDisabled()"`; removed the spinning icon class binding.                                                                                                         |

## Key Design Decisions

- **Hybrid progress model:** logical step groups (Portfolio/Watchlist/Dashboard/Actions/EOD) each with dynamic ticker counts, rather than one flat progress bar.
- **Error classification drives retry policy:** network (status 0) and timeout errors auto-retry (5s delay, max 3 attempts); server errors (5xx) require manual "Retry Now".
- **`ScreenRefreshService` is not a singleton** — it's instantiated per screen/component so multiple screens can refresh independently without shared state collisions.
- **Button disable is sufficient debouncing** — no separate debounce timer needed since `refreshAll()` early-returns while `isRefreshing()` is true.

## Verification

- No TypeScript compilation errors in any modified/created frontend file.
- Pre-existing backend nullable-reference warnings in `YahooFinanceService.cs` and tsconfig `rootDir` warnings are unrelated to this work and were not introduced by it.

## Deferred / Optional (Not Implemented)

These were scoped out as low-priority/nice-to-have and can be picked up later:

- Wrap portfolio/watchlist snapshot saves in a DB transaction with rollback on cancellation (`DataRefreshService`).
- Wire `ScreenRefreshService` + `ScreenRefreshProgressComponent` into the actual portfolio/watchlist/scanner page components for live per-screen mini-popups (infrastructure exists, not yet consumed by any page).
- Offline indicator badge on the toolbar.
- Automated unit/integration/E2E test coverage for the new retry/offline flows.

## Suggested Manual Test Plan

1. Click refresh → verify 5-step popup appears, ticker counts increment, popup shows "Refreshed X min ago" then auto-hides.
2. Click refresh, then Cancel mid-flight → verify HTTP aborts and popup closes immediately.
3. DevTools → Network → Offline → click refresh → verify "No internet connection" + retry countdown, then go back online and confirm auto-retry succeeds + "Connection restored" snackbar.
4. Stop the backend → click refresh → verify "Server unavailable" with manual "Retry Now" only (no auto-retry).
5. Rapid-click the refresh button → verify only one refresh runs (button disabled after first click).
