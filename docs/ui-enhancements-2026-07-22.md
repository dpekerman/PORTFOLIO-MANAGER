# UI Enhancements — 2026-07-22

## Portfolio Page

### Day $ Header Sum — Bug Fix

**File:** `portfolio-page.component.ts`

- `totalDayGain` now excludes `CLOSE` transactions from the sum.  
  Previously, all portfolio rows were summed including closed positions; only open (`transactionType !== 'CLOSE'`) non-manual rows now contribute.

### Export CSV — Added Day $ Column

**File:** `portfolio-page.component.ts`

- Added **Day $** column to the exported CSV (positioned between Gain/Loss % and Daily Change).  
  Value = `shares × daily change per share` for each open stock position.

### AGE Column — Stocks & Options Grids

**Files:** `grid-column.service.ts`, `portfolio-page.component.ts`, `portfolio-page.component.html`, `portfolio-page.component.scss`

- Added **Age (days)** column (`age` for stocks, `opt_age` for options) to both grid registries.
- Column shows whole-number days since `openDate` (today − open date).
- Hidden by default via the column config dialog; user can pin it.
- Added **Min Age (days)** filter input to the stocks filter bar; filters positions older than the entered threshold.

---

## Transactions Page

### Sticky Columns

**File:** `transactions-page.component.html`, `transactions-page.component.scss`

- **Stocks grid:** `TICKER` (`tx_symbol`) and `COMPANY` (`tx_company`) columns are now sticky-left — they stay visible when scrolling right.
- **Options grid:** `UNDERLYING` (`otx_ticker`) column is now sticky-left.
- Added background colour rule so sticky cells render opaquely over scrolling content.

### Decision Source Filter + Column (Options)

**Files:** `grid-column.service.ts`, `transactions-page.component.ts`, `transactions-page.component.html`

- Added `otx_decision_source` (Decision Source) column to the transactions-options grid registry and HTML template.
- Added a **Decision Source** text filter in the filter bar (applies to both stocks and options).  
  Matches any substring of the decision source value, case-insensitive.

### AGE Column + Filter

**Files:** `grid-column.service.ts`, `transactions-page.component.ts`, `transactions-page.component.html`

- Added `tx_age` and `otx_age` (Age in days) columns to both grid registries and HTML templates.
- Calculation:
  - `TYPE = OPEN` → **today − open date**
  - `TYPE = CLOSE` → **close date − open date**
  - Value is always a whole number; shows `—` when open date is absent.
- Added **Min Age (days)** numeric filter in the filter bar (applies to both stocks and options).

### Filter Bar Improvements

**Files:** `transactions-page.component.html`, `transactions-page.component.scss`

- Added `MatFormFieldModule`, `MatInputModule`, `MatSelectModule`, `FormsModule` to the component imports.
- Filter bar now contains: **ALL / OPEN / CLOSED** toggle + **Decision Source** text field + **Min Age** number field + **Clear** button.

---

## RSI Scanner

### Morning Check — Popup Frequency Fix

**File:** `scanner-page.component.ts`

- **Problem:** The morning check panel was auto-opening on every page navigation / component re-mount.
- **Fix:** Added `MORNING_AUTO_OPENED_KEY` localStorage entry. The panel now auto-opens **at most once per calendar day**, regardless of how many times the user navigates to the scanner page.
- The `wasDismissedToday()` check still works as before — if the user closes the panel, it won't auto-open again that day.

### Morning Check — "Always Visible" Toggle Disabled by Default

**File:** `scanner-page.component.ts`

- `morningForced` signal now **always starts as `false`** on page load, regardless of any previous localStorage state.  
  Previously the forced-show state persisted across sessions.
- Users must explicitly click the toggle button each session to enable force-show mode.
- When force-show is enabled during a session it still opens the panel immediately; when disabled it closes it. No localStorage persistence across page loads.
