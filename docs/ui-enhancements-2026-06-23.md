# UI Enhancement Release Notes

## Session: 2026-06-23

Build status: ✅ Production build clean — 0 TypeScript errors, 0 template errors (3 pre-existing CSS budget warnings only)

---

## 1. Portfolio Grid — Role Picklist on Grouped / Aggregate Rows

**Problem:** When MDA.TO (or any symbol with multiple account entries) was collapsed into a grouped total row, the Role column showed "—" with no way to change the role.

**Change:** The aggregate row now shows a full `mat-select` Role picker.

- Selecting a role on the aggregate header applies it to **all child positions** sharing that symbol
- The aggregate row reads the role from the first position in the group (so it shows the existing value on load)
- Individual child rows (when the group is expanded) continue to update only their own role as before
- Grouped child rows (those that belong to a visible aggregate) continue to show "—" (since the aggregate header controls them)

**Files changed:**

- `portfolio-page.component.ts` — added `holdingRole: string | null` to `AggregatePortfolioRow` interface; populated it in `gridRows()` computed; added `updateAggHoldingRole(agg, role)` method that iterates all summaries matching `agg.symbol` and calls `updateHoldingRole` on each
- `portfolio-page.component.html` — replaced the old `@if (!isAggRow)` / "—" fallback with a three-branch structure: aggregate → aggregate picker; individual (not in a group) → individual picker; child of group → "—"

---

## 2. Portfolio Grid — Analyst Target Column

**Change:** New **ANALYST TARGET** column added to the right of AVG COST in the portfolio grid.

- Displays the mean analyst 12-month price target (from Yahoo Finance via the RSI scan result)
- Shows the upside/downside % relative to the current price in a smaller coloured line below the price
  - Green = positive upside
  - Red = negative (price already above analyst target)
- Shows "—" when no analyst data is available (manual positions, positions without scan data)
- Works for both aggregate rows and individual rows (both show the same symbol-level analyst target)

**Files changed:**

- `portfolio-page.component.ts` — added `'analystTarget'` to `gridDisplayedColumns` (after `'holdingRole'`); added `analystForSymbol(symbol)` helper that reads from `rsiMap()`
- `portfolio-page.component.html` — added `<ng-container matColumnDef="analystTarget">` with price + coloured upside display
- `portfolio-page.component.scss` — added `.analyst-target-cell`, `.analyst-price`, `.analyst-upside`, `.at-positive`, `.at-negative` styles

---

## 3. Portfolio Grid — Filter Bar: RSI Min/Max Replaced with Role and Momentum Shift

**Problem:** The RSI Min / RSI Max text inputs were rarely used for filtering and took up space in the filter bar.

**Change:** Replaced both RSI inputs with two dropdown selects:

| Old                    | New                                                                  |
| ---------------------- | -------------------------------------------------------------------- |
| RSI min (number input) | **Role** picklist (Core / Strategic / Swing / Speculative / Options) |
| RSI max (number input) | **Momentum Shift** picklist (all 10 signal values)                   |

- Both new filters use Angular `signal()` state: `filterRole` and `filterMomentumShift`
- `gridRows()` computed updated to apply role comparison (`s.item.holdingRole ?? 'Strategic'`) and momentum shift comparison (via `decisionForPortfolio()`)
- `clearGridFilters()` and `hasActiveFilters` getter updated accordingly
- The "Clear" button appears whenever either (or any other) filter is active

**Files changed:**

- `portfolio-page.component.ts` — removed `filterRsiMin`, `filterRsiMax` signals; added `filterRole`, `filterMomentumShift`; added `momentumShiftOptions` constant array; updated `gridRows()`, `clearGridFilters()`, `hasActiveFilters`
- `portfolio-page.component.html` — replaced RSI `<mat-form-field>` inputs with Role and Momentum Shift `<mat-select>` fields

---

## 4. Watchlist Grid — RSI (14) Column Centered

**Problem:** The RSI value in the watchlist was right-aligned (matching other numeric columns) but visually reads better centered since it's a compact badge value.

**Change:** The RSI column header and cell now use `text-align: center` instead of `text-align: right`.

**Files changed:**

- `watchlist-page.component.html` — changed class on RSI `<th>` from `wl-num-header` to `wl-rsi-header`; changed class on `<td>` from `wl-num` to `wl-rsi-cell`
- `watchlist-page.component.scss` — added `.wl-rsi-header { text-align: center !important; }` and `.wl-rsi-cell { text-align: center !important; font-family: Roboto Mono; font-size: 0.78rem; }`

---

## 5. Watchlist Grid — Filter Bar: Trend Setup and Final Action Filters Added

**Change:** Two new dropdown filters added to the watchlist grid filter bar.

- **Trend Setup** dropdown — all 9 TrendSetup values from the decision engine as a static list
- **Final Action** dropdown — dynamically computed from the current watchlist items' decisions (only shows actions that are actually represented in the watchlist)
- Existing text search filter remains unchanged
- New "Clear all filters" button (filter_list_off icon) appears when any filter (text, trend setup, or final action) is active
- The count label `N of M` continues to reflect filtered count

**Files changed:**

- `watchlist-page.component.ts` — added `filterTrendSetup` and `filterFinalAction` signals; updated `filteredSorted()` computed to apply them; added `trendSetupOptions` static array; added `finalActionOptions` computed (dynamic list from live decisions)
- `watchlist-page.component.html` — added two `<mat-form-field mat-select>` dropdowns to `grid-filter-bar`; added conditional clear-all button
- `watchlist-page.component.scss` — added `.grid-filter-select { min-width: 180px; max-width: 260px; }` for the narrower select fields

---

## 6. RSI Scanner — Technical Signals Column Hidden

**Problem:** The "Technical Signals" column showed 5 indicator chips (Stoch, MACD, Bollinger, Volume, DMA). The column is wide and most of the same data is available in the Trend Setup / Momentum Shift columns produced by the decision engine.

**Change:** Removed `'indicators'` from `displayedColumns`.

The ng-container definition remains in the HTML (it is not deleted) — the column can be re-enabled in one line if needed.

**Files changed:**

- `rsi-scanner-table.component.ts` — removed `'indicators'` from `displayedColumns` array

---

## 7. RSI Scanner — Probability of Reversal Column Restored

**Problem:** The Reversal Probability column (`'probability'`) had been removed from the displayed columns in a previous session.

**Change:** Added `'probability'` back to `displayedColumns`, replacing the position previously held by `'indicators'`.

The column shows the aggregate `reversalProbability` value (High / Medium / Low) computed by `CalculateReversalProbabilityEnhanced()` or `CalculateReversalProbability()` in the backend, coloured by the `probClass()` helper.

**Files changed:**

- `rsi-scanner-table.component.ts` — added `'probability'` to `displayedColumns` in place of `'indicators'`

---

## Summary Table

| #   | Feature                                | Component            | Type        |
| --- | -------------------------------------- | -------------------- | ----------- |
| 1   | Role picklist on grouped rows          | Portfolio grid       | Enhancement |
| 2   | Analyst Target column                  | Portfolio grid       | New column  |
| 3   | Role + Momentum Shift filters          | Portfolio filter bar | Replace     |
| 4   | RSI centered in cell                   | Watchlist grid       | Style       |
| 5   | Trend Setup + Final Action filters     | Watchlist filter bar | New feature |
| 6   | Hide Technical Signals column          | RSI Scanner          | Remove      |
| 7   | Restore Probability of Reversal column | RSI Scanner          | Restore     |

## Build Verification

```
npx ng build --configuration production
✅ Application bundle generation complete.
⚠️  3 pre-existing CSS budget warnings (portfolio, value-screener, scanner SCSS files)
0 TypeScript errors
0 Template errors
```
