# Grid Column Configuration — Implementation Report

**Date:** 2026-07-03  
**Branch:** develop  
**Status:** ✅ Production-ready — clean build, zero errors/warnings

---

## Overview

A unified, persistent grid-column configuration system has been implemented across all 9 grids in the application. Users can show/hide columns and reorder them via a two-step modal dialog. All preferences are stored in `localStorage` and restored on every page load.

---

## Architecture

### Design Decisions

| Decision           | Choice                                     | Reason                                                          |
| ------------------ | ------------------------------------------ | --------------------------------------------------------------- |
| Reorder UX         | ↑ / ↓ arrow buttons                        | User-tested better than drag-and-drop for dense lists           |
| Persistence        | `localStorage` (`pm_grid_columns_v1`)      | No server dependency; instant load; survives refreshes          |
| Reactivity         | Angular Signals (`computed`)               | Zero-overhead live updates; no `ChangeDetectorRef` calls needed |
| Config entry point | Global toolbar button (`view_column` icon) | Single consistent location; accessible from every page          |
| Change persistence | Immediate (no explicit Save)               | Simpler UX; Reset-to-defaults available as escape hatch         |
| Pinned columns     | Always last, cannot be hidden/moved        | `actions` / delete buttons must always be accessible            |

---

## New Files

### `core/services/grid-column.service.ts`

Singleton service (`providedIn: 'root'`) containing:

- **`GRID_REGISTRY`** — exported constant array of all 9 `GridDef` objects. Add/remove column definitions here as the data model evolves.
- **`GridColumnService.getColumnKeys(gridId)`** — returns a cached `computed<string[]>` signal. Components call this once at field-initializer time. The signal reactively updates when any preference changes.
- **`getEditablePrefs(gridId)`** — returns a plain `ColumnPreference[]` for dialog editing, merging stored prefs with defaults (forward-compat: newly-added default columns appear at the end).
- **`updatePrefs(gridId, prefs)`** — writes to the signal and immediately persists to localStorage.
- **`resetGrid(gridId)`** / **`resetAll()`** — deletes stored prefs (reverts to defaults).

**LocalStorage format** (`pm_grid_columns_v1`):

```json
{
  "portfolio-stocks": [
    { "key": "symbol", "visible": true },
    { "key": "marketValue", "visible": true },
    { "key": "rsi", "visible": false }
  ],
  "watchlist": [ ... ]
}
```

### `shared/column-config-dialog/column-config-dialog.component.ts/.html/.scss`

Material Dialog component with a two-step workflow:

**Step 1 — Grid selection**

- All grids listed, grouped by page with the page's icon
- Card-style buttons: grid name, column count, chevron arrow

**Step 2 — Column management**

- ↑ / ↓ arrow buttons to reorder (top item's Up is disabled; last item's Down is disabled)
- `mat-slide-toggle` to show/hide each column
- Hidden rows render at reduced opacity so position is clear
- Pinned columns displayed at the bottom as read-only informational rows
- **Reset this grid** button (disabled when no customisation is active)
- **Back** button returns to Step 1

**Footer actions** always include **Done** (closes dialog) and context-sensitive **Reset all grids**.

---

## Modified Files

### `shared/layout/layout.component.ts` / `.html`

- `MatDialog` injected; `openColumnConfig()` method added
- `view_column` icon button added to the global toolbar, immediately before the Refresh button
- Button label: "Configure table columns" (aria-label + tooltip)

### Grid Components Updated

All components now derive their column arrays from `GridColumnService.getColumnKeys()` — a signal. Template bindings updated from `columns` to `columns()` accordingly.

| Component                          | Grid IDs Wired                                            |
| ---------------------------------- | --------------------------------------------------------- |
| `portfolio-page.component.ts`      | `portfolio-stocks`, `portfolio-options`, `portfolio-cash` |
| `transactions-page.component.ts`   | `transactions-stocks`, `transactions-options`             |
| `rsi-scanner-table.component.ts`   | `scanner`                                                 |
| `eod-signals-page.component.ts`    | `eod-signals`                                             |
| `value-screener-page.component.ts` | `value-screener`                                          |
| `watchlist-page.component.ts`      | `watchlist`                                               |

---

## Grid Registry — Complete Column Inventory

| Grid ID                | Page           | Configurable Cols                                                                                                                                                                                                     | Pinned Cols |
| ---------------------- | -------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------- |
| `portfolio-stocks`     | Portfolio      | symbol, company, accountType, sector, industry, shares, avgCost, price, analystTarget, changePct, dayGain, marketValue, portfolioPct, gainLoss, gainLossPct, rsi, holdingRole, trendSetup, momentumShift, finalAction | actions     |
| `portfolio-options`    | Portfolio      | opt_ticker, opt_type, opt_expiry, opt_strike, opt_premium, opt_contracts, opt_cmp, opt_stockPrice, opt_dte, opt_cost, opt_mv, opt_gl, opt_glp, opt_state, opt_action                                                  | opt_actions |
| `portfolio-cash`       | Portfolio      | description, amount, addedAt                                                                                                                                                                                          | cashActions |
| `transactions-stocks`  | Transactions   | tx_type, tx_account, tx_symbol, tx_company, tx_shares, tx_avg_cost, tx_open_date, tx_close_date, tx_closing_price, tx_gain_loss, tx_gain_pct, tx_mkt_value                                                            | tx_actions  |
| `transactions-options` | Transactions   | otx_type, otx_account, otx_ticker, otx_position, otx_expiry, otx_strike, otx_premium, otx_contracts, otx_open_date, otx_close_date, otx_closing_price, otx_gain_loss, otx_gain_pct, otx_mkt_value                     | otx_actions |
| `scanner`              | Scanner        | tracking, symbol, rsi, rsiSignal, price, change, analystUpside, probability, trendSetup, momentumShift, baseAction, status, trigger, signalHistory                                                                    | _(none)_    |
| `eod-signals`          | EOD Signals    | signalDate, symbol, scanType, signalType, rsi, price, reversalProbability, volumeSignal, ruleVersion, signalState                                                                                                     | actions     |
| `value-screener`       | Value Screener | ticker, description, technicalState, score, actionTrigger                                                                                                                                                             | _(none)_    |
| `watchlist`            | Watchlist      | symbol, company, role, price, change, analystTarget, week52, sector, rsi, trendSetup, momentumShift, buyScore, finalAction                                                                                            | actions     |

> **Note on Scanner:** The `signalHistory` column is only rendered when `showHistory=true` (default in Scanner page; disabled in Ad-Hoc Analyzer). Even if visible in preferences, the component filters it out when `showHistory=false`.

---

## Forward Compatibility

**Adding a new column to any grid** in future:

1. Add the column's `<ng-container matColumnDef="...">` block to the component HTML.
2. Add a `ColumnDef` entry to the appropriate `GridDef` in `GRID_REGISTRY` (in `grid-column.service.ts`).
3. No changes to localStorage format are needed — `getEditablePrefs()` automatically appends any new default columns that aren't in a user's saved preferences.

**Removing a column:**

1. Remove the `matColumnDef` from the HTML.
2. Remove the `ColumnDef` from `GRID_REGISTRY`.
3. Stale entries in localStorage are silently ignored by the service's merge logic.

---

## How to Test

### Prerequisites

- Frontend running: `npm start` in `frontend/portfolio-manager-ui`
- Backend running (optional — works with demo data)

### Test 1 — Open the Dialog

1. Open any page (Portfolio, Watchlist, etc.)
2. Click the **`view_column`** icon in the top toolbar (left of the Refresh button)
3. ✅ Dialog opens showing all pages and their grids grouped by page icon

### Test 2 — Grid Selection

1. In the dialog, click **"Stocks"** under Portfolio
2. ✅ Dialog shows the 20 configurable columns with slide toggles and ↑/↓ buttons
3. ✅ "Actions" pinned column shown at the bottom with lock icon

### Test 3 — Hide a Column

1. Open the dialog → Portfolio → Stocks
2. Toggle **"RSI (14)"** to off
3. Close the dialog
4. Navigate to Portfolio (grid view)
5. ✅ The RSI column is no longer visible in the table

### Test 4 — Reorder Columns

1. Open the dialog → Portfolio → Stocks
2. Click ↑ on "Sector" twice to move it up
3. Close the dialog
4. ✅ Sector column now appears earlier in the table

### Test 5 — Persistence

1. Make some column changes
2. Refresh the browser (F5)
3. ✅ Changes are still applied — columns match last configuration

### Test 6 — Reset to Defaults

1. In the dialog with a customised grid selected
2. Click **"Reset this grid"**
3. ✅ All columns restored to defaults; toggle is enabled only when customisation exists

### Test 7 — Reset All Grids

1. Customise several grids
2. Open dialog, click **"Reset all grids"** button
3. ✅ All grids revert to factory defaults; `pm_grid_columns_v1` key removed from localStorage

### Test 8 — Scanner Ad-Hoc Analyzer

1. Go to Scanner page → click Ad-Hoc Analyzer
2. Hide the `signalHistory` column via dialog
3. Open scanner results
4. ✅ Even with `signalHistory` visible in prefs, it is absent in Ad-Hoc (showHistory=false)

### Test 9 — Transactions Footer Row

1. Configure and hide some transaction columns
2. Go to Transactions page and enable "Show Totals Row"
3. ✅ Footer row spans only the visible columns correctly

### Test 10 — LocalStorage Inspection

1. Open DevTools → Application → Local Storage
2. Check key `pm_grid_columns_v1`
3. ✅ JSON object with grid IDs as keys and `[{ key, visible }]` arrays as values

---

## Known Constraints

- The **Cash** grid (4 columns) supports configuration but is not particularly useful to customise.
- The `signalHistory` column in the Scanner config is informational — its actual visibility in the Ad-Hoc Analyzer is always controlled by the `showHistory` input regardless of user preference.
- localStorage is per-browser-profile. Preferences do not sync across devices.
