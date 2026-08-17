# UI Enhancements – 2026-06-26

## Summary

Six feature improvements applied across the **Allocation** and **Portfolio** pages.

---

## Allocation Page

### 1. Header KPI Cards – Grand Total (Stocks + Cash + Options)

**Before:** Portfolio Value, Total Cost Basis, and Total Gain/Loss in the header showed stocks-only figures.

**After:** All three KPI cards now reflect the full portfolio including Stocks, Cash, and Options.

| KPI               | Calculation                                            |
| ----------------- | ------------------------------------------------------ |
| Portfolio Value   | `stocks MV + cash total + options MV`                  |
| Total Cost Basis  | `stocks cost + options cost + cash (deployed capital)` |
| Total Gain / Loss | `Portfolio Value − Total Cost Basis`                   |

Sub-label updated to "Stocks · Cash · Options" to make the scope clear.

**New computed signals added** (`allocation-page.component.ts`):

- `grandTotalCost()` – combined cost basis
- `grandTotalGainLoss()` – combined unrealised gain/loss
- `grandTotalGainLossIsPositive()` – colour toggle helper
- `grandTotalGainLossPct()` – combined return %
- `optionsGainLoss()`, `optionsGainLossIsPositive()`, `optionsGainLossPct()` – options P&L
- `stocksPct()` – stocks share of grand total

---

### 2. Cash & Options Rows Align with Sector Rows

**Before:** Cash and Options were styled as separate accordion widgets with their own layout that didn't match the sector columns.

**After:** Both Cash and Options section headers use the same column layout as sector rows:

```
[▶] [icon] NAME  [count]  | ▓▓▓░░ bar | % TOTAL | MKT VALUE | GAIN/LOSS | G/L %
```

- **Bar colour**: teal for Cash (`#4db6ac`), purple for Options (`#ab47bc`)
- **Cash**: Gain/Loss and G/L% show `—` (cash has no unrealised P&L)
- **Options**: Gain/Loss and G/L% show live options P&L (options MV − options cost)
- Collapsed headers line up visually with the sector rows produced by `app-sector-exposition`, enabling side-by-side comparison at a glance

**New CSS classes** (`allocation-page.component.scss`):
`alloc-sector-row`, `alloc-sector-header`, `alloc-sector-left/right`, `alloc-bar-track/fill`, `alloc-col-pct-val`, `alloc-col-mv`, `alloc-col-gl`, `alloc-col-glpct`, `alloc-gl-pos/neg`

---

### 3. Net Portfolio Totals Row at the Bottom

**Before:** No aggregate summary row after all sections.

**After:** A "PORTFOLIO NET TOTAL" row appears below all sectors, cash, and options, showing:

| Column      | Value                     |
| ----------- | ------------------------- |
| % Total     | 100.0%                    |
| Total MV    | `grandTotal()`            |
| Gain / Loss | `grandTotalGainLoss()`    |
| G/L %       | `grandTotalGainLossPct()` |

---

## Portfolio Page

### 4. Stocks Summary – Day $ Total Added

**Before:** The Stocks section header showed Market Value, Total Cost, and Gain/Loss.

**After:** A fourth stat "**Day $**" is shown, displaying the aggregate intraday dollar gain/loss across all non-manual stock positions (reuses the existing `totalDayGain()` computed signal).

---

### 5. Grid View – Multi-Transaction Groups Start Collapsed

**Before:** When landing on the Portfolio page, all ticker groups with multiple transactions (e.g. same ticker held in multiple accounts) were expanded by default.

**After:** On initial page load, any ticker that has more than one open transaction is **automatically collapsed**. The user clicks the symbol row to expand and see individual lots.

**Implementation**: A one-time `effect()` in the constructor runs when portfolio data first loads, builds the set of symbols with `count > 1`, and sets `collapsedSymbols` accordingly. The flag `initialCollapseApplied` prevents re-collapsing on subsequent refreshes.

---

### 6. Options Grid – Current Market Price (CMP) Editable Column

**Before:** The option contract's current market price could only be updated via the full edit dialog.

**After:** A new **CMP** column appears in the options grid. The cell contains an inline number input pre-populated with `item.marketPrice`. Editing the value and pressing Tab/Enter triggers `updateOptionMarketPrice()` which immediately persists the new price via the existing `optionState.updateItem()` API call. A snackbar confirms success or failure.

**Column placement**: Between CONTRACTS and STOCK PRICE.

**Sorting**: The CMP column participates in `matSort` (sorts by `item.marketPrice`).

**Styling**: A new `.inline-price-input` class in `portfolio-page.component.scss` provides a minimal input field that blends into the table cell, with a focus ring on edit.

---

## Files Changed

| File                                                 | Change                                                                                                                 |
| ---------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `features/allocation/allocation-page.component.ts`   | New computed signals for grand totals, options P&L, stocks pct                                                         |
| `features/allocation/allocation-page.component.html` | Updated KPI cards; new sector-aligned Cash/Options headers; net totals row                                             |
| `features/allocation/allocation-page.component.scss` | New CSS for sector-aligned rows and net totals                                                                         |
| `features/portfolio/portfolio-page.component.ts`     | `opt_cmp` sort col type; `optionDisplayedColumns` updated; initial collapse effect; `updateOptionMarketPrice()` method |
| `features/portfolio/portfolio-page.component.html`   | Day $ stat in Stocks header; `opt_cmp` column with inline input                                                        |
| `features/portfolio/portfolio-page.component.scss`   | `.inline-price-input` style                                                                                            |

---

## Build Status

✅ `ng build --configuration development` — **no errors**  
✅ `tsc --noEmit` — **no type errors**
