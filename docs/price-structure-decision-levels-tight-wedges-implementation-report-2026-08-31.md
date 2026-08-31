# Price Structure Decision Levels and Tight Wedges Implementation Report

Date: 2026-08-31

## Summary

Implemented the shared Price Structure extension so the existing engine can surface both:

- a primary pattern, including structural wedges, tight swing wedges, and channels;
- a current key technical decision level, including support/resistance tests, breakouts, breakdowns, confluence zones, SMA levels, Fib levels, swing levels, and gap boundaries.

This work extends the existing shared Price Structure / TechnicalSnapshot path. It does not create a new scanner, does not create per-screen calculation logic, and does not change RSI, Momentum, MA, Channel, or Final Action calculations except to consume the shared Price Structure context.

## Completed TODO List

- [x] Added Upcoming Earnings dashboard expand/collapse behavior.
- [x] Persisted Upcoming Earnings collapse state with the existing dashboard localStorage service.
- [x] Extended backend `PriceStructureResult` with primary pattern fields.
- [x] Extended backend `PriceStructureResult` with key technical level fields.
- [x] Extended backend `PriceStructureResult` with breakout/breakdown trigger fields.
- [x] Mirrored the expanded `PriceStructureResult` in TypeScript.
- [x] Added multi-horizon wedge search inside the existing shared engine.
- [x] Preserved structural wedge rules for longer horizons.
- [x] Added tight swing wedge mode for 20D, 30D, and 40D horizons.
- [x] Required tighter contraction for tight wedges.
- [x] Kept tight wedge detection inside the existing Price Structure engine, not a separate scanner/service.
- [x] Added generic key-level candidate selection even when no wedge/channel exists.
- [x] Added candidate sources for wedge rails, swing highs/lows, Fib 38.2/50/61.8, SMA50, SMA200, and open gaps.
- [x] Added support/resistance role resolution based on current price location.
- [x] Used daily high for resistance tests.
- [x] Used daily low for support tests.
- [x] Used EOD close for breakout/breakdown confirmation.
- [x] Added failed breakout role-reversal state.
- [x] Added support reclaim role-reversal state.
- [x] Added confluence-zone detection for clustered important levels.
- [x] Added key-level quality and confluence source reporting.
- [x] Kept `TechnicalSnapshotService` as the shared backend source for MA/momentum/price-structure facts.
- [x] Populated shared `PriceStructureResult` on Market Leadership rows.
- [x] Populated shared `PriceStructureResult` on Portfolio summary DTOs.
- [x] Populated shared `PriceStructureResult` on Watchlist summary DTOs.
- [x] Populated shared `PriceStructureResult` on RSI scanner rows.
- [x] Added compact Price Structure display to Market Leadership tooltip.
- [x] Added compact Price Structure column to Portfolio stock grid.
- [x] Added compact Price Structure column to Watchlist grid.
- [x] Added compact Price Structure column to RSI Scanner grid.
- [x] Added Price Structure column registration to grid column preferences.
- [x] Added deterministic backend tests for key-level state behavior.
- [x] Added deterministic backend tests for confluence and tight wedge contraction.
- [x] Re-ran focused and full backend test suites.
- [x] Re-ran Angular development build.
- [x] Checked editor diagnostics.

## Backend Changes

### Shared Price Structure Result

`PriceStructureResult` now includes the old display fields plus the new shared contract:

- `PrimaryPatternType`
- `PrimaryPatternState`
- `PrimaryPatternQuality`
- `PrimaryPatternHorizon`
- `KeyLevelPrice`
- `KeyLevelType`
- `KeyLevelRole`
- `KeyLevelState`
- `KeyLevelDistancePercent`
- `KeyLevelDistanceAtr`
- `KeyLevelQuality`
- `KeyLevelSources`
- `KeyLevelConfluenceCount`
- `BreakoutTriggerPrice`
- `BreakdownTriggerPrice`
- `CalculatedAt`

`PriceStructureResult.None` now returns explicit `NONE` / neutral values for the expanded fields.

### Multi-Horizon Wedge Search

The existing `ChannelAnalysisService.AnalyzePriceStructure(...)` now evaluates multiple windows:

- Tight wedge windows: 20, 30, 40 trading days.
- Structural wedge windows: 60, 126, 250 trading days.

Structural wedge rules remain strict:

- same geometry as before;
- approximately 10 trading days same-side touch spacing;
- approximately 1.5 ATR move-away;
- contraction threshold remains at 30%;
- quality threshold remains at 70;
- breakout/breakdown thresholds remain 0.25 ATR.

Tight wedge mode uses shorter-horizon parameters:

- same wedge geometry;
- same-side touch spacing around 4 trading days;
- move-away around 1 ATR;
- minimum 2 independent touches per side;
- contraction threshold is 40%;
- quality threshold remains 70.

### Key Technical Level / Decision Zone

The shared engine now selects a key decision level even when no pattern exists. Candidate sources include:

- wedge resistance/support rails;
- recent swing high;
- recent swing low;
- Fib 38.2;
- Fib 50;
- Fib 61.8;
- SMA50;
- SMA200;
- open-gap boundaries already available in the channel logic.

Each candidate calculates:

- distance percent;
- distance ATR;
- support/resistance/transition role;
- state;
- quality;
- breakout and breakdown trigger prices.

If multiple meaningful levels are within 0.5 ATR, the engine returns a `CONFLUENCE_ZONE` with merged sources and a confluence count.

### Key-Level States

Implemented shared state resolution for:

- `APPROACHING_RESISTANCE`
- `RESISTANCE_TEST`
- `BREAKOUT_CONFIRMED`
- `FAILED_BREAKOUT`
- `APPROACHING_SUPPORT`
- `SUPPORT_TEST`
- `BREAKDOWN_CONFIRMED`
- `SUPPORT_RECLAIM`
- `NONE`

Daily high/low are used for tests. EOD close is used for confirmation.

### Shared Screen Integration

The same `PriceStructureResult` is now available through:

- Market Leadership via `TechnicalSnapshotService`.
- Portfolio summaries via live quotes and data refresh snapshots.
- Watchlist summaries via live quotes and data refresh snapshots.
- RSI scanner rows via the same shared engine using the scanner's already-built OHLC candles.

## Frontend Changes

### Dashboard

Upcoming Earnings now behaves like the other dashboard panels:

- expand/collapse button in the header;
- `upcoming-earnings` section id;
- localStorage key: `dashboard_collapse_upcoming-earnings`;
- default expanded.

### Shared Models

Updated `portfolio.models.ts`:

- `PortfolioSummary.priceStructure?: PriceStructureResult | null`
- `WatchlistSummary.priceStructure?: PriceStructureResult | null`
- `RsiScanResult.priceStructure: PriceStructureResult`
- expanded `PriceStructureResult` fields matching the backend record.

### Grid Displays

Added compact `PRICE STRUCTURE` / `Price Structure` columns to:

- Portfolio stocks grid;
- Watchlist grid;
- RSI Scanner grid.

Market Leadership tooltip now shows both:

- Primary Pattern;
- Key Technical Level.

Portfolio and Watchlist prefer the DTO-level shared `priceStructure` and fall back to scanner-enriched rows if old snapshots do not yet include the new field.

## Important Non-Goals Preserved

- No new scanner was created.
- No ticker-specific behavior was hardcoded.
- No separate per-screen Price Structure logic was added.
- Structural wedge rules were not loosened globally.
- RSI calculations were not changed.
- Momentum calculations were not changed.
- MA calculations were not changed.
- Existing final-action hierarchy was not replaced.
- Key levels and tight wedges provide context; they do not independently create buy/sell recommendations.

## Automated Validation

### Focused Price Structure Tests

Command:

```powershell
cd d:\PORTFOLIO-MANAGER\backend
dotnet test PortfolioManager.Tests\PortfolioManager.Tests.csproj --filter ChannelWedgeTests
```

Result:

```text
Test summary: total: 31, failed: 0, succeeded: 31, skipped: 0
```

### Full Backend Test Suite

Command:

```powershell
cd d:\PORTFOLIO-MANAGER\backend
dotnet test PortfolioManager.Tests\PortfolioManager.Tests.csproj
```

Result:

```text
Test summary: total: 148, failed: 0, succeeded: 148, skipped: 0
```

### Frontend Build

Command:

```powershell
cd d:\PORTFOLIO-MANAGER\frontend\portfolio-manager-ui
npx ng build --configuration development
```

Result:

```text
Application bundle generation complete.
```

### Editor Diagnostics

Result:

```text
No errors found.
```

## Manual Test Steps

### 1. Start Application

```powershell
cd d:\PORTFOLIO-MANAGER
start-all.bat
```

Open:

```text
http://localhost:4200
```

### 2. Dashboard Upcoming Earnings Collapse

1. Open Dashboard.
2. Find `Upcoming earnings (next 7 days)`.
3. Click the expand/collapse icon.
4. Confirm the earnings list hides and a collapsed hint appears.
5. Refresh the page.
6. Confirm the collapsed/expanded state persists.
7. In browser DevTools, confirm localStorage contains `dashboard_collapse_upcoming-earnings`.

### 3. Market Leadership Price Structure

1. Open Dashboard -> Market Leadership.
2. Hover a Price Structure badge.
3. Confirm the tooltip includes:
   - Primary Pattern;
   - Pattern state;
   - Horizon;
   - Quality;
   - Independent upper/lower touches;
   - Key Technical Level;
   - Role;
   - State;
   - Distance ATR;
   - Breakout/Breakdown triggers;
   - Sources and confluence count.

### 4. Portfolio Price Structure Consistency

1. Open Portfolio.
2. Use the column configuration button if `PRICE STRUCTURE` is hidden.
3. Show the `PRICE STRUCTURE` column.
4. Confirm each active stock can show the same compact Price Structure result as Market Leadership for the same ticker after refresh.
5. Hover the badge and compare the key fields with Market Leadership:
   - `PrimaryPatternType`
   - `PrimaryPatternState`
   - `PrimaryPatternQuality`
   - `KeyLevelPrice`
   - `KeyLevelType`
   - `KeyLevelRole`
   - `KeyLevelState`

### 5. Watchlist Price Structure Consistency

1. Open Watchlist.
2. Show the `PRICE STRUCTURE` column if hidden.
3. Confirm watchlist rows display compact shared Price Structure context.
4. Compare the same ticker against Portfolio, Market Leadership, or RSI Scanner after refresh.

### 6. RSI Scanner Price Structure Consistency

1. Open RSI Scanner.
2. Show the `Price Structure` column if hidden.
3. Run or refresh scanner data.
4. Confirm scanner rows display the shared Price Structure result.
5. Confirm Price Structure is contextual only and does not replace the existing RSI/Final Action logic.

### 7. TSLA-Like Key-Level Validation

Use TSLA only as a validation case, not as hardcoded behavior.

1. Ensure TSLA is available in a refreshed scanner/watchlist/portfolio path.
2. Inspect the `priceStructure` result in browser Network response or tooltip.
3. If price is near Fib 50 / swing high / SMA confluence, expect:
   - `KeyLevelType = CONFLUENCE_ZONE` when levels cluster within 0.5 ATR;
   - `KeyLevelRole = RESISTANCE` when price approaches from below;
   - `KeyLevelState = RESISTANCE_TEST` when daily high tests the level but close does not confirm breakout;
   - `KeyLevelState = BREAKOUT_CONFIRMED` only when close exceeds `KeyLevelPrice + 0.25 ATR`.

### 8. TTD-Like Tight Wedge Validation

Use TTD only as a validation case, not as hardcoded behavior.

1. Ensure TTD has refreshed daily OHLC history.
2. Inspect 20D, 30D, and 40D behavior via the returned Price Structure tooltip/API response.
3. If valid short compression exists, expect:
   - `PrimaryPatternType = TIGHT_FALLING_WEDGE` or `TIGHT_RISING_WEDGE` depending on actual geometry;
   - minimum 2 independent touches per side;
   - contraction at or above 40%;
   - quality at or above 70.
4. Confirm MA structure remains visible independently, for example `200 > 50 > P`.
5. Confirm a tight wedge alone does not create an automatic bullish trade action.

## Files Changed

### Backend

- `backend/PortfolioManager.Api/Services/ChannelAnalysisService.cs`
- `backend/PortfolioManager.Api/Services/TechnicalSnapshotService.cs`
- `backend/PortfolioManager.Api/Services/MarketLeadershipService.cs`
- `backend/PortfolioManager.Api/Services/RsiScannerService.cs`
- `backend/PortfolioManager.Api/Services/DataRefreshService.cs`
- `backend/PortfolioManager.Api/Controllers/StocksController.cs`
- `backend/PortfolioManager.Api/Controllers/WatchlistController.cs`
- `backend/PortfolioManager.Api/Models/Dtos.cs`
- `backend/PortfolioManager.Api/Models/ScannerModels.cs`
- `backend/PortfolioManager.Tests/ChannelWedgeTests.cs`

### Frontend

- `frontend/portfolio-manager-ui/src/app/core/models/portfolio.models.ts`
- `frontend/portfolio-manager-ui/src/app/core/services/dashboard-collapse-state.service.ts`
- `frontend/portfolio-manager-ui/src/app/core/services/grid-column.service.ts`
- `frontend/portfolio-manager-ui/src/app/features/dashboard/dashboard-page.component.html`
- `frontend/portfolio-manager-ui/src/app/features/dashboard/market-leadership-widget/market-leadership-widget.component.ts`
- `frontend/portfolio-manager-ui/src/app/features/portfolio/portfolio-page.component.ts`
- `frontend/portfolio-manager-ui/src/app/features/portfolio/portfolio-page.component.html`
- `frontend/portfolio-manager-ui/src/app/features/watchlist-page/watchlist-page.component.ts`
- `frontend/portfolio-manager-ui/src/app/features/watchlist-page/watchlist-page.component.html`
- `frontend/portfolio-manager-ui/src/app/features/scanner/rsi-scanner-table.component.ts`
- `frontend/portfolio-manager-ui/src/app/features/scanner/rsi-scanner-table.component.html`
- `frontend/portfolio-manager-ui/src/app/features/scanner/rsi-scanner-table.component.scss`

## Notes

Old persisted portfolio/watchlist snapshots may not contain `priceStructure` until the next refresh. The frontend handles this by treating the field as optional and falling back to scanner-enriched data where possible.

No EF migration is required.
