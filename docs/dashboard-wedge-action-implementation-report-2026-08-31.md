# Dashboard, Wedge Quality, and Ownership-Aware Signals Implementation Report

Date: 2026-08-31

## Summary

This implementation completed three coordinated changes:

1. Dashboard sections are now collapsible/expandable with persisted local state.
2. Wedge quality scoring now measures structural credibility instead of rewarding raw pivot density.
3. Dashboard Market Signals now derive action labels from ownership context and expose ownership badges after the ticker.

A shared `TechnicalSnapshotService` was added as the backend owner for MA structure, momentum, and price structure facts. Market Leadership now consumes this service directly, so the technical fact calculation has a single reusable source instead of being embedded inside the Market Leadership screen service.

## Completed TODO List

- [x] Add dashboard section collapse state service.
- [x] Persist dashboard collapse state to `localStorage`.
- [x] Default all dashboard sections to expanded except Market Leadership.
- [x] Add expand/collapse controls to Portfolio Value History.
- [x] Add expand/collapse controls to Top Movers.
- [x] Add expand/collapse controls to Market Indices.
- [x] Add expand/collapse controls to Market Leadership.
- [x] Add expand/collapse controls to Market Signals.
- [x] Add expand/collapse controls to Allocation vs Targets.
- [x] Add expand/collapse controls to Action Center.
- [x] Add expand/collapse controls to Signal Changes Today.
- [x] Add expand/collapse controls to Priority Candidates.
- [x] Add expand/collapse controls to YTD Performance.
- [x] Fix default-collapsed section toggle behavior.
- [x] Add dashboard header-right styling for mixed controls and toggle buttons.
- [x] Replace wedge quality raw pivot-count formula.
- [x] Add independent upper/lower touch counts.
- [x] Add raw pivot high/low diagnostic counts.
- [x] Add upper/lower trendline fit quality diagnostics.
- [x] Add 100-point wedge quality component scoring.
- [x] Preserve existing breakout/breakdown thresholds.
- [x] Add focused wedge quality tests.
- [x] Add ownership-aware Dashboard Market Signals action interpreter.
- [x] Add P/W ownership flags to dashboard RSI signal DTO.
- [x] Show P/W badges after ticker symbols in Dashboard Market Signals.
- [x] Update action severity mapping for new vocabulary.
- [x] Add ownership-aware action regression tests.
- [x] Add shared `TechnicalSnapshotService` for MA structure, momentum, and price structure facts.
- [x] Register `TechnicalSnapshotService` in DI.
- [x] Switch Market Leadership to consume `TechnicalSnapshotService`.
- [x] Update frontend `PriceStructureResult` and `DashboardRsiSignal` models.
- [x] Validate backend tests.
- [x] Validate frontend build.

## Changed Files

### Frontend

- `frontend/portfolio-manager-ui/src/app/core/services/dashboard-collapse-state.service.ts`
  - New signal-based service for dashboard collapse state.
  - Persists per-section state under `dashboard_collapse_${sectionId}`.
  - Handles default state and reset behavior.

- `frontend/portfolio-manager-ui/src/app/features/dashboard/dashboard-page.component.ts`
  - Injects `DashboardCollapseStateService`.
  - Replaces individual section signals with `isExpanded(sectionId)` and `toggleExpanded(sectionId)` helpers.

- `frontend/portfolio-manager-ui/src/app/features/dashboard/dashboard-page.component.html`
  - Adds collapse/expand controls and conditional rendering for all dashboard sections.
  - Adds P/W ownership badges after Market Signals ticker symbols.

- `frontend/portfolio-manager-ui/src/app/features/dashboard/dashboard-page.component.scss`
  - Adds `.db-panel-hd-right` layout.
  - Adds `.db-owner-badge` styling for portfolio/watchlist ownership indicators.

- `frontend/portfolio-manager-ui/src/app/core/models/portfolio.models.ts`
  - Adds new wedge diagnostic fields to `PriceStructureResult`.
  - Adds `isInPortfolio` and `isInWatchlist` to `DashboardRsiSignal`.

### Backend

- `backend/PortfolioManager.Api/Services/ChannelAnalysisService.cs`
  - Replaces old quality formula:
    - Previous formula heavily rewarded raw pivot count.
    - New formula uses structural inputs with capped scores.
  - Adds fields in `PriceStructureResult`:
    - `RawPivotHighCount`
    - `RawPivotLowCount`
    - `IndependentUpperTouchCount`
    - `IndependentLowerTouchCount`
    - `UpperFitQuality`
    - `LowerFitQuality`
  - Adds public scoring helpers for regression tests.

- `backend/PortfolioManager.Api/Services/DashboardSignalActionInterpreter.cs`
  - New ownership-aware interpreter for Dashboard Market Signals.
  - Portfolio-owned symbols use portfolio vocabulary.
  - Watchlist-only symbols use watchlist vocabulary.
  - Scanner-only symbols use scanner/caution vocabulary.

- `backend/PortfolioManager.Api/Services/TechnicalSnapshotService.cs`
  - New shared technical fact service.
  - Produces one reusable snapshot containing MA/momentum facts and price structure.

- `backend/PortfolioManager.Api/Services/MarketLeadershipService.cs`
  - Uses `ITechnicalSnapshotService` instead of calculating price structure locally.

- `backend/PortfolioManager.Api/Services/DashboardService.cs`
  - Derives active portfolio/watchlist ownership sets.
  - Uses `DashboardSignalActionInterpreter.Resolve(...)` for Market Signals actions.
  - Includes ownership flags in dashboard RSI signal rows.

- `backend/PortfolioManager.Api/Models/DashboardModels.cs`
  - Adds ownership flags to `DashboardRsiSignal`.

- `backend/PortfolioManager.Api/Services/ActionSeverityMapper.cs`
  - Supports `STOP/EXIT` and `TECHNICAL CAUTION` vocabulary.

- `backend/PortfolioManager.Api/Program.cs`
  - Registers `ITechnicalSnapshotService`.

- `backend/PortfolioManager.Tests/ChannelWedgeTests.cs`
  - Adds tests for capped independent touch scoring, contraction bands, apex bands, and raw pivot density not inflating quality.

- `backend/PortfolioManager.Tests/ChannelAndSeverityTests.cs`
  - Adds tests proving Dashboard Market Signals actions depend on ownership.

## Wedge Quality Scoring

The new quality score is a 100-point structural score:

| Component           | Max | Behavior                                                                     |
| ------------------- | --: | ---------------------------------------------------------------------------- |
| Trendline fit       |  30 | Average R-squared fit of upper/lower rails                                   |
| Independent touches |  20 | Balanced independent touches only: 2+2 = 8, 3+3 = 15, 4+4 = 20               |
| Contraction         |  20 | 30-40% = 8, 40-50% = 12, 50-65% = 16, 65%+ = 20                              |
| Geometry            |  10 | Rewards correct converging wedge slope structure                             |
| Rail violations     |  10 | Penalizes material closes outside rails without changing breakout thresholds |
| Apex proximity      |  10 | 0-15 days = 10, 16-60 = 8, 61-120 = 4, 121-180 = 2, >180 = 0                 |

The minimum quality threshold remains 70.

Existing breakout/breakdown trigger thresholds are unchanged:

- Falling wedge breakout: close > upper trendline + 0.25 ATR.
- Rising wedge breakdown: close < lower trendline - 0.25 ATR.

## Ownership-Aware Market Signals

Dashboard Market Signals now treat the same technical facts differently depending on ownership:

| Ownership    | Example Actions                                                                                             |
| ------------ | ----------------------------------------------------------------------------------------------------------- |
| Portfolio    | `ADD CANDIDATE`, `ADD WATCH`, `HOLD`, `HOLD/EXTENDED`, `TRIM WATCH`, `REVIEW`, `EXIT REVIEW`                |
| Watchlist    | `ENTRY CANDIDATE`, `BUY WATCH`, `REVERSAL WATCH`, `WAIT`, `WAIT FOR REVERSAL`, `WAIT FOR PULLBACK`, `AVOID` |
| Scanner-only | `BUY WATCH`, `REVERSAL WATCH`, `WAIT FOR REVERSAL`, `TECHNICAL CAUTION`, `AVOID`                            |

Dashboard ticker display now shows ownership immediately after the ticker:

- `P` = active portfolio holding.
- `W` = watchlist-only symbol.
- No badge = scanner-only symbol.

Portfolio ownership wins when a symbol appears in both portfolio and watchlist.

## How To Test

### Automated Validation

From the repository root:

```powershell
cd backend
dotnet test PortfolioManager.Tests\PortfolioManager.Tests.csproj
```

Expected result from this implementation:

```text
Test summary: total: 138, failed: 0, succeeded: 138, skipped: 0
```

From the frontend project:

```powershell
cd frontend\portfolio-manager-ui
npx ng build --configuration development
```

Expected result:

```text
Application bundle generation complete.
```

### Manual Dashboard Collapse Test

1. Start the app:

```powershell
cd d:\PORTFOLIO-MANAGER
start-all.bat
```

2. Open the frontend at `http://localhost:4200`.
3. Navigate to Dashboard.
4. Confirm these initial states:
   - Portfolio Value History is expanded.
   - Top Movers is expanded.
   - Market Indices is expanded.
   - Market Leadership is collapsed.
   - Market Signals is expanded when data is available.
   - Allocation vs Targets is expanded.
   - Action Center is expanded.
   - Signal Changes Today is expanded.
   - Priority Candidates is expanded.
   - YTD Performance is expanded.
5. Collapse and expand several sections.
6. Refresh the browser.
7. Confirm the same sections stay collapsed/expanded.
8. In browser DevTools, verify keys like `dashboard_collapse_top-movers` and `dashboard_collapse_market-leadership` exist in localStorage.

### Manual Ownership-Aware Signal Test

1. Ensure at least one active portfolio symbol and at least one watchlist-only symbol are included in the latest RSI scanner snapshot.
2. Rebuild/refresh dashboard data.
3. Open Dashboard -> Market Signals.
4. Confirm portfolio-owned rows show `P` after the ticker.
5. Confirm watchlist-only rows show `W` after the ticker.
6. Confirm portfolio-owned rows do not show watchlist-only actions such as `ENTRY CANDIDATE` or `BUY WATCH`.
7. Confirm watchlist-only rows do not show portfolio-only actions such as `HOLD`, `TRIM WATCH`, or `EXIT REVIEW` unless the symbol is actually portfolio-owned.

### Manual Wedge Quality Test

1. Run a scanner or Market Leadership refresh that includes a symbol with a known wedge candidate, such as `GDX`.
2. Inspect the returned `priceStructure` object in the API response or browser Network tab.
3. Confirm the response includes:
   - `rawPivotHighCount`
   - `rawPivotLowCount`
   - `independentUpperTouchCount`
   - `independentLowerTouchCount`
   - `upperFitQuality`
   - `lowerFitQuality`
4. Confirm quality no longer climbs only because many raw pivots were detected.
5. Confirm far-away apexes receive lower apex contribution.

## Validation Completed

- Backend focused tests:

```powershell
dotnet test PortfolioManager.Tests\PortfolioManager.Tests.csproj --filter "ChannelWedgeTests|ChannelAndSeverityTests"
```

Result: 41 passed, 0 failed.

- Backend full tests:

```powershell
dotnet test PortfolioManager.Tests\PortfolioManager.Tests.csproj
```

Result: 138 passed, 0 failed.

- Frontend build:

```powershell
npx ng build --configuration development
```

Result: successful development build.

- Editor diagnostics:

Result: no errors in touched backend or frontend files.

## Notes

- This implementation preserves existing scanner throttling and Yahoo Finance behavior.
- No EF migration is required.
- No database schema changes were made.
- The `TechnicalSnapshotService` is now the reusable backend source for technical facts. Market Leadership is wired through it in this change; future screen-level displays can consume the same service where they need identical MA structure, momentum, and price structure facts.
