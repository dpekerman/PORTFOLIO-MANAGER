# Price Structure Human-Friendly Display - Phase 2 Report

Date: 2026-09-01

## Phase 1 Checkpoint

Phase 1 was already committed at the clean repository `HEAD` before Phase 2 began:

```text
Commit: 15fb193d1b4f7c70a09d584f0ef60618350e14f2
Subject: improve price structure
Branch: develop
```

Pre-Phase-2 validation:

- Backend: 164/164 tests passed in Release.
- Focused Price Structure frontend tests: 4/4 passed.
- Angular development build passed.
- VS Code diagnostics were clean.
- No EF migration or model-snapshot changes were present.

## Summary

Phase 2 changes only Price Structure presentation and existing exports. The backend engine, TechnicalSnapshot architecture, internal technical values, ATR thresholds, candidate ranking, channel/wedge detection, hard-negative logic, persistence, and Final Action rules were not changed.

One shared frontend formatter continues to serve:

- Portfolio
- Watchlist
- RSI Scanner
- Market Leadership
- Action Center

This guarantees the same internal Price Structure result has the same user-facing label and explanation everywhere.

## Friendly Display Architecture

`price-structure-display.ts` now derives a friendly label from existing combinations of:

- `PrimaryPatternType` and `PrimaryPatternState`
- `KeyLevelType`, `KeyLevelRole`, and `KeyLevelState`

Internal state strings are never mutated. They remain available for calculations, persistence, sorting, tests, and the tooltip's technical details.

Phase 1 event priority remains intact:

1. Hard structural negative
2. Confirmed constructive event
3. Active level interaction
4. Approaching/developing event
5. No meaningful event

Pattern event priority and current-level proximity are still separate. For example, a tight rising wedge breakdown remains the compact display event while a nearby channel support test remains visible in details.

## Friendly Label Mapping

Representative mappings include:

| Internal fact                 | Display label               |
| ----------------------------- | --------------------------- |
| Confluence zone + support     | STRONG SUPPORT ZONE         |
| Confluence zone + resistance  | STRONG RESISTANCE ZONE      |
| SUPPORT_TEST                  | TESTING SUPPORT             |
| RESISTANCE_TEST               | TESTING RESISTANCE          |
| APPROACHING_SUPPORT           | NEAR SUPPORT                |
| APPROACHING_RESISTANCE        | NEAR RESISTANCE             |
| SUPPORT_RECLAIM               | SUPPORT RECOVERED           |
| BREAKOUT_WATCH                | BREAKOUT WATCH              |
| BREAKOUT_CONFIRMED            | BREAKOUT CONFIRMED          |
| BREAKDOWN_WATCH               | SUPPORT AT RISK             |
| BREAKDOWN_CONFIRMED           | SUPPORT BROKEN              |
| FAILED_BREAKOUT               | BREAKOUT FAILED             |
| Swing high test               | TESTING RECENT HIGH         |
| Swing low test                | TESTING RECENT LOW          |
| Fib 38.2/50/61.8 test         | TESTING FIB 38.2/50/61.8    |
| Channel third-touch approach  | NEAR CHANNEL SUPPORT        |
| Channel third-touch test      | TESTING CHANNEL SUPPORT     |
| Channel lower-rail retest     | RETESTING CHANNEL SUPPORT   |
| CHANNEL_BROKEN                | CHANNEL SUPPORT BROKEN      |
| Tight falling wedge near apex | TIGHT WEDGE - NEAR BREAKOUT |
| Tight falling wedge breakout  | TIGHT WEDGE BREAKOUT        |
| Rising wedge breakdown        | WEDGE BREAKDOWN             |
| Tight rising wedge breakdown  | TIGHT WEDGE BREAKDOWN       |

The UI uses a typographic dash in labels such as `TIGHT WEDGE — NEAR BREAKOUT`; the table above uses ASCII punctuation for document portability.

Compatibility mappings cover both current split-model combinations and older composite aliases such as `CONFLUENCE_SUPPORT`, `THIRD_RAIL_TEST`, and `FIB_50_TEST`.

## Tooltip Design

Every shared Price Structure tooltip now begins with:

1. `WHAT IS HAPPENING?`
2. `WHY DOES IT MATTER?`
3. `WHAT TO WATCH NEXT?`

Plain-language narratives cover:

- support and resistance tests;
- strong support/resistance zones;
- support recovery;
- confirmed and failed breakouts;
- broken support/channel/wedge structures;
- channel interactions;
- falling, rising, and tight wedges.

The tooltip then shows `TECHNICAL DETAILS`, retaining:

- exact internal pattern and level states;
- pattern horizon, quality, lookback, rails, contraction, and projected apex;
- channel touch history or wedge independent touches as appropriate;
- current/original level role and level type;
- level/zone, distance percentage, and distance ATR;
- Daily High, Daily Low, EOD Close, and ATR;
- role-aware confirmation/failure trigger labels;
- sources and confluence count.

Monetary values continue to pass through the caller's demo-mode masking function.

## Export Changes

The friendly `Price Structure` label was added to every existing relevant export:

### Portfolio

- Existing format: CSV
- Added `Price Structure` between Momentum Shift and Action.
- Stock rows use the same shared label shown on screen.
- Cash and Option rows receive a blank Price Structure cell.
- All header and data rows contain 21 columns.

### Watchlist

- Existing format: XLSX
- Added `Price Structure` using the shared label for each filtered/sorted row.

### RSI Scanner

- Existing format: XLSX
- Added `Price Structure` using each scanner row's shared result.

Action Center and Market Leadership do not currently have export workflows, so Phase 2 does not invent new export buttons. Their on-screen labels and tooltips automatically receive the friendly language through the shared helper.

## Files Changed

- `frontend/portfolio-manager-ui/src/app/core/price-structure-display.ts`
- `frontend/portfolio-manager-ui/src/app/core/price-structure-display.spec.ts`
- `frontend/portfolio-manager-ui/src/app/features/portfolio/portfolio-page.component.ts`
- `frontend/portfolio-manager-ui/src/app/features/watchlist-page/watchlist-page.component.ts`
- `frontend/portfolio-manager-ui/src/app/features/scanner/rsi-scanner-table.component.ts`
- `docs/price-structure-display-implementation-phase2-2026-09-01.md`

## Database, API, and Engine Impact

- Database migration: none
- Schema change: none
- Backend DTO change: none
- Internal enum/state change: none
- Price Structure calculation change: none
- ATR threshold change: none
- Candidate-level ranking change: none
- Wedge/channel algorithm change: none
- TechnicalSnapshot architecture change: none
- Final Action change: none

## Automated Validation

### Phase 1 Backend Non-Regression

```powershell
dotnet test backend\PortfolioManager.Tests\PortfolioManager.Tests.csproj -c Release --nologo
```

Result:

```text
Total: 164
Passed: 164
Failed: 0
Skipped: 0
```

### Focused Phase 2 Frontend Tests

```powershell
cd frontend\portfolio-manager-ui
npm run test -- --watch=false --include src/app/core/price-structure-display.spec.ts
```

Result:

```text
Test files: 1 passed
Tests: 45 passed
```

Coverage includes all acceptance mappings, internal-state immutability, hard-event priority, channel/wedge diagnostics, role-aware triggers, and the three-question tooltip structure.

### Angular Build

```powershell
cd frontend\portfolio-manager-ui
npx ng build --configuration development
```

Result: passed.

### Full Frontend Suite

```powershell
cd frontend\portfolio-manager-ui
npm run test -- --watch=false
```

Result:

```text
Tests: 45 passed, 1 failed
```

The only failure is the pre-existing scaffold assertion in `src/app/app.spec.ts` that expects an `h1` containing `Hello, portfolio-manager-ui`. The routed application does not render that scaffold heading. All 44 Phase 2 tests pass.

### Static Validation

- VS Code diagnostics: no errors in touched files.
- `git diff --check`: passed.
- Phase 2 diff contains no backend, migration, or EF model-snapshot files.
- No owned-source TODO/FIXME markers were found in the affected frontend source.

## Manual Testing Steps

### Prerequisites

1. Start the backend using `start-backend.bat` or the normal backend command.
2. Start the frontend using `start-frontend.bat` or `npx ng serve`.
3. Open `http://localhost:4200`.
4. Run a full data refresh so current Price Structure snapshots are loaded.

### Cross-Screen Label Consistency

1. Choose a ticker visible on more than one of Portfolio, Watchlist, RSI Scanner, Market Leadership, and Action Center.
2. Record its internal `primaryPatternType`, `primaryPatternState`, `keyLevelType`, `keyLevelRole`, and `keyLevelState` from the network response.
3. Compare the Price Structure label on every screen where it appears.
4. Confirm the same internal combination displays the same friendly label everywhere.
5. Confirm no primary grid label requires understanding `confluence`, `rail`, `reclaim`, or `apex`.

### Tooltip Structure

1. Hover a Price Structure value.
2. Confirm the friendly label appears first.
3. Confirm these sections appear in order:
   - WHAT IS HAPPENING?
   - WHY DOES IT MATTER?
   - WHAT TO WATCH NEXT?
   - TECHNICAL DETAILS
4. Confirm technical details still contain the exact internal state and type.
5. Confirm prices are masked when demo masking is enabled.

### Support and Resistance

1. Find a `SUPPORT_TEST` ticker and confirm the grid says `TESTING SUPPORT` or a more specific friendly label such as `TESTING FIB 50`.
2. Confirm its tooltip explains support in plain language and includes the breakdown trigger.
3. Find a `RESISTANCE_TEST` ticker and confirm the grid says `TESTING RESISTANCE` or the corresponding recent-high/Fib label.
4. Confirm its tooltip includes breakout and rejection guidance.

### Confluence

1. Find a `CONFLUENCE_ZONE` with role `SUPPORT`.
2. Confirm every screen displays `STRONG SUPPORT ZONE`.
3. Confirm the tooltip lists source levels and confluence count in Technical Details.
4. Repeat with a resistance confluence and expect `STRONG RESISTANCE ZONE`.

### Wedges and Hard Events

1. Find a tight falling wedge near apex and confirm `TIGHT WEDGE — NEAR BREAKOUT`.
2. Confirm the tooltip explains compression before showing wedge geometry.
3. For an MRVL-style tight rising wedge breakdown plus nearby support test, confirm the compact label remains `TIGHT WEDGE BREAKDOWN`.
4. Confirm the same tooltip still contains the current support decision level.

### Channel

1. Find a rising channel in third-touch approach/test state.
2. Confirm the display says `NEAR CHANNEL SUPPORT` or `TESTING CHANNEL SUPPORT`.
3. Confirm the tooltip shows channel touch history.
4. Confirm it does not show wedge independent-touch counters.

### Portfolio CSV Export

1. Open Portfolio and export CSV.
2. Open `portfolio.csv`.
3. Confirm a `Price Structure` column appears between `Momentum Shift` and `Action`.
4. Confirm stock rows contain the same friendly labels as the grid.
5. Confirm Cash and Option rows have a blank Price Structure value.
6. Confirm all rows remain aligned under 21 headers.

### Watchlist XLSX Export

1. Open Watchlist and apply any desired filters.
2. Export to Excel.
3. Open the generated workbook.
4. Confirm `Price Structure` exists.
5. Confirm exported rows match the filtered/sorted set and use the same friendly labels as the grid.

### RSI Scanner XLSX Export

1. Open RSI Scanner and select a scan section.
2. Export to Excel.
3. Open the generated workbook.
4. Confirm `Price Structure` exists.
5. Confirm labels match those displayed in the scanner table.

### Internal-State Preservation

1. Inspect a Price Structure API response in browser developer tools.
2. Confirm internal values remain technical strings such as `SUPPORT_RECLAIM`, `CONFLUENCE_ZONE`, `THIRD_TOUCH_APPROACHING`, or `NEAR_APEX`.
3. Confirm only UI/export wording changed.
