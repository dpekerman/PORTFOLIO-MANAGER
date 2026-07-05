# Per-Grid Column Configuration — Redesign Report

**Date:** 2026-07-03  
**Branch:** develop  
**Build status:** ✅ Zero errors · Zero warnings

---

## What Changed and Why

The previous design placed a single global "Configure Columns" button in the app toolbar that opened a two-step dialog (Step 1: choose a grid; Step 2: manage its columns). Users had to remember which grid they wanted to configure and navigate a selection screen before seeing any columns.

**New design:** Every table in the application has its own `view_column` icon button placed directly in the table's filter bar or section header — exactly where the user is already looking at that table. Clicking it opens the column manager immediately for that specific grid, with no navigation step.

---

## Architecture

### Files Changed

#### Redesigned

| File                                                              | Change                                                                                         |
| ----------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `shared/column-config-dialog/column-config-dialog.component.ts`   | Receives `MAT_DIALOG_DATA: { gridId }` — opens directly to column management, no two-step flow |
| `shared/column-config-dialog/column-config-dialog.component.html` | Removed Step 1 (grid selection). Shows grid label, page badge, visible count in title          |
| `shared/column-config-dialog/column-config-dialog.component.scss` | Simplified: removed Step 1 card styles, added visible-count badge                              |
| `shared/layout/layout.component.ts`                               | Removed global toolbar button and `MatDialog` injection                                        |
| `shared/layout/layout.component.html`                             | Removed global `view_column` toolbar button                                                    |

#### New File

| File                                                       | Purpose                                                                               |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| `shared/column-config-dialog/grid-column-btn.component.ts` | Reusable `<app-grid-column-btn gridId="...">` component — the per-grid trigger button |

#### Updated (button added to each page)

| File                                                             | Button placement                                                     |
| ---------------------------------------------------------------- | -------------------------------------------------------------------- |
| `features/portfolio/portfolio-page.component.ts/.html`           | Stocks: end of filter bar · Cash: above table · Options: above table |
| `features/transactions/transactions-page.component.ts/.html`     | Stocks section header · Options section header                       |
| `features/scanner/rsi-scanner-table.component.ts/.html`          | Inside `scanner-table-toolbar` (alongside Export button)             |
| `features/eod-signals/eod-signals-page.component.ts/.html`       | End of filter bar                                                    |
| `features/value-screener/value-screener-page.component.ts/.html` | Above results table                                                  |
| `features/watchlist-page/watchlist-page.component.ts/.html`      | End of grid filter bar                                               |

---

## `GridColumnButtonComponent` — Key Behaviour

```html
<app-grid-column-btn gridId="portfolio-stocks" />
```

- Renders a `mat-icon-button` with the `view_column` icon
- **Tooltip:** `Configure Stocks columns (18 / 21 visible)` — shows live visible/total count
- **Highlighted in primary colour** when the grid has been customised vs defaults
- **Faded (65% opacity)** when at defaults, so it doesn't distract
- On click: opens `ColumnConfigDialogComponent` with `data: { gridId }` — no extra steps

---

## Dialog Design (After Redesign)

```
┌─────────────────────────────────────────────────────┐
│  ▦  Stocks             Portfolio  │  18 / 21 visible │
├─────────────────────────────────────────────────────┤
│  Use the toggles to show or hide columns…           │
│                                                     │
│  ▲▼  1  Ticker          ┤●├                         │
│  ▲▼  2  Company         ┤●├                         │
│  ▲▼  3  Account         ┤●├                         │
│   ─  4  Sector          ┤ ├  (hidden — faded)       │
│  ...                                                │
│  🔒  —  Actions         [Pinned]                    │
├─────────────────────────────────────────────────────┤
│  [↺ Reset to defaults]           [✓ Done]           │
└─────────────────────────────────────────────────────┘
```

- Title always shows grid label + page badge + live `X / Y visible` counter
- "Reset to defaults" is disabled when no customisation exists
- Changes are live (persist immediately to `localStorage` key `pm_grid_columns_v1`)

---

## Button Placement Map

| Page           | Grid        | Button Location                                            |
| -------------- | ----------- | ---------------------------------------------------------- |
| Portfolio      | Stocks      | Right end of the column filter bar (after count `18 / 24`) |
| Portfolio      | Cash        | Small action bar immediately above the cash table          |
| Portfolio      | Options     | Small action bar immediately above the options table       |
| Transactions   | Stocks      | Inside the "Stocks" collapsible section header             |
| Transactions   | Options     | Inside the "Options" collapsible section header            |
| Scanner        | RSI Scanner | Inside the table toolbar alongside the Excel export button |
| EOD Signals    | Signals     | Right end of the filter bar                                |
| Value Screener | Results     | Above the results table (right-aligned)                    |
| Watchlist      | Watchlist   | Right end of the grid filter bar (after count `12 / 45`)   |

> **Note:** The scanner button appears on each of the three scanner table instances (Oversold, Overbought, Neutral) and inside the Ad-Hoc Analyzer, since they all share the same `scanner` grid ID and same column config.

---

## How to Test

### Setup

```
npm start  (in frontend/portfolio-manager-ui)
```

Backend optional — works with demo data.

---

### Test 1 — Button is contextual

1. Navigate to **Portfolio** (grid view)
2. Find the Stocks filter bar (Ticker / Sector / Industry dropdowns)
3. ✅ A faint `view_column` icon appears at the right end of the filter bar
4. Navigate to **Watchlist** (grid view)
5. ✅ Same icon appears at the right end of the Watchlist filter bar
6. Navigate to **Transactions**
7. ✅ Icon appears in both "Stocks" and "Options" section headers

---

### Test 2 — Dialog opens for the correct grid

1. On the **Watchlist** grid filter bar, click the `view_column` button
2. ✅ Dialog opens immediately showing "Watchlist" as the title with "Watchlist" page badge
3. ✅ Columns shown: Ticker, Description, Role, Last Price, Change, ... (14 rows + 1 pinned)
4. No grid-selection step — you go straight to the column list

---

### Test 3 — Hide a column

1. Open Watchlist column config
2. Toggle **"Buy Score"** to off
3. ✅ Row immediately fades to 40% opacity
4. Close dialog with **Done**
5. ✅ "Buy Score" column is no longer visible in the Watchlist table
6. ✅ Button is now highlighted in primary colour (customised indicator)

---

### Test 4 — Reorder columns

1. Open Portfolio → Stocks column config
2. Click ↑ on **"Sector"** twice to move it up
3. ✅ Sector appears higher in the numbered list
4. Close dialog
5. ✅ Sector column now appears earlier in the Portfolio grid

---

### Test 5 — Visible count badge updates live

1. Open Watchlist column config
2. Hide 3 columns via toggles
3. ✅ Counter in dialog title changes from e.g. `13 / 13 visible` to `10 / 13 visible` instantly

---

### Test 6 — Persistence across reload

1. Hide 2 columns in the Portfolio Stocks grid
2. Refresh browser (F5)
3. ✅ Columns remain hidden after reload
4. Open DevTools → Application → Local Storage → `pm_grid_columns_v1`
5. ✅ JSON shows the saved preferences

---

### Test 7 — Reset to defaults

1. Open column config for any customised grid
2. Click **"Reset to defaults"**
3. ✅ All columns reappear in original order, toggles all ON
4. ✅ "Reset to defaults" button becomes disabled again
5. ✅ Table behind the dialog is updated immediately

---

### Test 8 — Scanner (shared component)

1. Navigate to the RSI Scanner page
2. ✅ `view_column` button appears in the **Oversold**, **Overbought**, and **Neutral** table toolbars (alongside the Excel export button)
3. Click any of them — they all open the **"RSI Scanner"** config
4. Hide **"Tracking"** column
5. ✅ Tracking column disappears from ALL three scanner tables at once (shared config)

---

### Test 9 — Ad-Hoc Analyzer (scanner sub-component)

1. On the Scanner page, open the Ad-Hoc Analyzer
2. ✅ The `view_column` button appears in its table toolbar too
3. Click it — ✅ Opens the same "RSI Scanner" config
4. Even if `signalHistory` is visible in config, it is hidden in Ad-Hoc Analyzer (controlled by `showHistory=false` input)

---

### Test 10 — No global toolbar button

1. Look at the top toolbar
2. ✅ The `view_column` icon is **not** in the global toolbar — only the per-grid buttons exist

---

## LocalStorage Format (unchanged)

Key: `pm_grid_columns_v1`

```json
{
  "portfolio-stocks": [
    { "key": "symbol", "visible": true },
    { "key": "company", "visible": true },
    { "key": "sector", "visible": false },
    ...
  ],
  "watchlist": [...]
}
```

Grids not yet customised do not appear in localStorage (defaults are implied).

---

## Forward Compatibility

To **add a new column** to any grid in future:

1. Add `<ng-container matColumnDef="newCol">` to the component HTML
2. Add `{ key: 'newCol', label: 'New Column' }` to the appropriate `GridDef` in `grid-column.service.ts`
3. Done — new column appears at the end of existing users' column lists automatically

To **add a new grid** (new page with a new table):

1. Register it in `GRID_REGISTRY` inside `grid-column.service.ts`
2. Wire `displayedColumns = inject(GridColumnService).getColumnKeys('my-grid-id')` in the component
3. Update the HTML row defs to use `displayedColumns()`
4. Add `<app-grid-column-btn gridId="my-grid-id" />` to the filter bar / toolbar
