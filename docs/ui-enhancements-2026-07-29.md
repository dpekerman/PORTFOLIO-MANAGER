# UI Enhancement Report — 2026-07-29

## Summary

This report covers all changes made across Portfolio, Watchlist, EOD Signals, Transactions, and Allocation pages.

---

## PORTFOLIO PAGE

### 1. Remove ROLE and Min Age Filters from Stocks Grid

**Files changed:**

- `portfolio-page.component.html` — Removed the Role dropdown and Min Age input from the Stocks grid filter bar
- `portfolio-page.component.ts` — Removed `filterRole` and `filterMinAge` signals; updated `gridRows()`, `clearGridFilters()`, and `hasActiveFilters`

**To test:** Navigate to Portfolio → grid view. Confirm only Ticker/Company search, Sector, Industry, Momentum Shift, and Account filters are shown.

---

### 2. Add Stock Dialog — Role and Decision Source Fields

**Files changed:**

- `add-stock-dialog.component.html` — Added Role selector (Row 7, left column) and Decision Source selector (Row 7, right column)
- `add-stock-dialog.component.ts` — Added `holdingRole` and `decisionSource` form controls; injected `ConfigService` for decision source list; added roles array

**Backend changes:**

- `Models/Dtos.cs` — Added `HoldingRole?` parameter to `AddPortfolioItemRequest`
- `Services/PortfolioService.cs` — `AddAsync` now sets `HoldingRole` from request

**Frontend model:**

- `portfolio.models.ts` — Added `holdingRole?: string | null` to `AddPortfolioItemRequest`

**To test:** Click "Add Stock". Confirm Role dropdown (Core, Strategic, Strategic-Income, Swing, Speculative, Options) and Decision Source dropdown appear. Add a stock and verify role is saved in the grid.

---

### 3. Add Cash Dialog — Transaction Date Field

**Files changed:**

- `add-cash-dialog.component.html` — Added optional Transaction Date datepicker below Account Type
- `add-cash-dialog.component.ts` — Added `transactionDate` form control; imported `MatDatepickerModule` and `MatNativeDateModule`

**Backend changes:**

- `Models/CashItem.cs` — Added `public DateTime? TransactionDate { get; set; }`
- `Models/Dtos.cs` — Added `TransactionDate?` to `AddCashItemRequest`, `UpdateCashItemRequest`, `CashItemDto`
- `Services/CashService.cs` — `AddAsync` and `UpdateAsync` now use `TransactionDate`; `ToDto` returns it
- `Data/AppDbContext.cs` — Added EF config for `TransactionDate` on `CashItem` (removed duplicate CashItem config block)
- `Data/Migrations/20260729000001_AddCashTransactionDate.cs` — New migration adding `TransactionDate` column
- `Data/Migrations/AppDbContextModelSnapshot.cs` — Updated with new column

**SQL script:**

- `database/SCRIPTS/10_AddCashTransactionDate.sql` — Safe `ALTER TABLE` with existence check

**Frontend model:**

- `portfolio.models.ts` — Added `transactionDate?: string | null` to `CashItem` and `AddCashItemRequest`

**To test:** Click "Add Cash". Confirm Transaction Date datepicker appears (optional). Add a cash entry with a date and verify it's stored and displayed.

---

### 4. Fix Account Filter Total (STOCKS Grid)

**File changed:**

- `portfolio-page.component.ts` — `filteredTotalMktValue` now computes directly from `portfolio.summaries()` filtered by account, instead of from `gridRows()`. This fixes the issue where collapsed (aggregate) rows were excluded from the total.

**To test:**

1. In Portfolio → Stocks, filter by Account = Corp_TD
2. Note the "Corp_TD total:" displayed in the filter bar
3. Export to Excel and manually sum all Corp_TD values
4. Confirm the displayed total matches the Excel sum

---

## WATCHLIST PAGE

### 1. Split CHANGE into CHANGE$ and CHANGE%

**Files changed:**

- `watchlist-page.component.html` — Replaced single `change` column with two separate columns: `change` (CHANGE $) and `changePct` (CHANGE %)
- `grid-column.service.ts` — Updated watchlist registry to have `change` (label: "Change $") and new `changePct` (label: "Change %") columns

**To test:** Go to Watchlist → grid view. Confirm two separate columns: "CHANGE $" and "CHANGE %", each independently sortable.

---

### 2. Dynamic Trend Setup Filter

**File changed:**

- `watchlist-page.component.ts` — `trendSetupOptions` changed from hardcoded `const` array to a `computed()` signal that dynamically collects all distinct trend setup values from the current watchlist items

**To test:**

1. Go to Watchlist → filter bar → "Trend Setup" dropdown
2. Confirm options reflect values actually appearing in the grid (not a fixed list)
3. Try filtering for "Reclaim" — should appear if any item has that trend setup value

---

## EOD SIGNALS PAGE

### 1. New Columns: Last Price, Price Diff, Diff %, Days Passed

**Files changed:**

- `eod-signals-page.component.html` — Added `daysPassed` column (right of DATE), `lastPrice` column (right of PRICE), `priceDiff` column, and `diffPct` column
- `eod-signals-page.component.ts`:
  - Updated `SortCol` type to include new columns
  - Added `currentPriceMap` signal
  - Added `fetchCurrentPrices()` — batch-fetches live prices via RSI scanner after signals load
  - Added `daysPassed()`, `lastPrice()`, `priceDiff()`, `diffPct()` helper methods
  - Updated `sortedSignals()` computed to handle all new sort columns
  - `loadSignals()` now calls `fetchCurrentPrices()` on success
- `grid-column.service.ts` — Added `daysPassed`, `lastPrice`, `priceDiff`, `diffPct` to eod-signals registry
- `eod-signals-page.component.scss` — Added `.eod-pos` / `.eod-neg` colour classes

**Calculations:**

- `DAYS PASSED` = Today − Signal Date (whole days)
- `LAST PRICE` = Current market price (fetched live via scanner API)
- `PRICE DIFF` = LAST PRICE − PRICE (signal price)
- `DIFF %` = PRICE DIFF / PRICE × 100

**To test:**

1. Go to EOD Signals page
2. Confirm "DAYS PASSED" appears after DATE column (e.g. a signal from 5 days ago shows "5")
3. Confirm "LAST PRICE", "PRICE DIFF", "DIFF %" appear after PRICE column (data loads after a few seconds from live API)
4. Sort by each new column to verify

---

## TRANSACTIONS PAGE

### 1. Add DIFF$ Column

**Files changed:**

- `transactions-page.component.html` — Added `tx_diff_dollar` column: PRICE DIFF × SHARES
- `transactions-page.component.ts` — Added `stockDiffDollar()` helper; added `tx_diff_dollar` and `tx_diff_dollar` to `StockTxCol` type and `stockSortValue()`
- `grid-column.service.ts` — Added `tx_diff_dollar` (label: "Diff $") to transactions-stocks registry

**To test:** Transactions → ALL tab → confirm "DIFF $" column shows PRICE DIFF × SHARES.

---

### 2. Remove Min Age Filter

**Files changed:**

- `transactions-page.component.html` — Removed Min Age input field; simplified clear filter button condition
- `transactions-page.component.ts` — Removed `filterMinAge` signal; removed min-age logic from `sortedStockTransactions()` and `sortedOptionTransactions()`

**To test:** Transactions page — confirm Min Age field is gone from the filter bar.

---

### 3. Trans Date Column (ALL Tab)

**Files changed:**

- `transactions-page.component.html` — Added `tx_trans_date` column
- `transactions-page.component.ts`:
  - Added `tx_trans_date` to `StockTxCol` type
  - Added `stockTransDate()`: returns `closeDate` if TYPE=CLOSE, `openDate` if TYPE=OPEN
  - Added sort handling for `tx_trans_date`
- `grid-column.service.ts` — Added `tx_trans_date` (label: "Trans Date") to transactions-stocks registry

**To test:** Transactions → ALL tab → confirm "TRANS DATE" column shows close date for CLOSE records and open date for OPEN records. Click column header to sort.

---

### 4. Export to CSV (ALL Tab)

**Files changed:**

- `transactions-page.component.html` — Added "Export CSV" button in filter bar, visible only when ALL tab is selected
- `transactions-page.component.ts` — Added `exportStocksCsv()` method that generates a CSV file with all current stock transaction columns

**To test:** Go to Transactions → ALL tab. Click "Export CSV". Verify a `.csv` file downloads with all stock columns including Diff $, Trans Date, etc.

---

## ALLOCATION PAGE

### 1. Beta by Positions — Sortable Grid

**Files changed:**

- `allocation-page.component.html` — Replaced plain HTML `<table>` with `mat-table` featuring `matSort`. Default sort: Ticker (A→Z). Columns: SYMBOL, WEIGHT %, BETA (editable input), ACTIONS (reset override button)
- `allocation-page.component.ts`:
  - Added `MatSortModule`, `MatTableModule` imports
  - Added `betaSortCol`, `betaSortDir` signals
  - Added `betaColumns` array
  - Added `sortedBetaContributors()` computed signal for sorted data
  - Added `onBetaSortChange()` handler
- `allocation-page.component.scss` — Added `.beta-contrib-table-wrapper`, `.beta-mat-table`, `.beta-symbol`, `.num-right`, `.beta-contrib-count` styles

**To test:**

1. Go to Allocation page
2. Click the Portfolio Beta card to expand contributors
3. Confirm grid appears with sortable header columns (Symbol, Weight %, Beta)
4. Default sort is Ticker A→Z
5. Click column headers to resort; confirm beta override inputs still work

---

## DB / MIGRATIONS

### SQL Script

- `database/SCRIPTS/10_AddCashTransactionDate.sql` — Run this to add `TransactionDate DATETIME2 NULL` to `CashItems` table (idempotent with existence check)

### EF Migration

- `Data/Migrations/20260729000001_AddCashTransactionDate.cs` — New migration for the above column
- Run: `dotnet ef database update` from `backend/PortfolioManager.Api`

---

## How to Deploy

### Backend

1. Run the SQL script: `database/SCRIPTS/10_AddCashTransactionDate.sql`
2. OR run `dotnet ef database update` from the backend project
3. Restart the backend: `cd backend/PortfolioManager.Api && dotnet run --launch-profile http`

### Frontend

```
cd frontend/portfolio-manager-ui
npx ng serve
```

---

## Files Changed Summary

| File                                                               | Change                                                                |
| ------------------------------------------------------------------ | --------------------------------------------------------------------- |
| `backend/Models/CashItem.cs`                                       | Added `TransactionDate`                                               |
| `backend/Models/Dtos.cs`                                           | Added `TransactionDate` to cash DTOs, `HoldingRole` to portfolio add  |
| `backend/Services/CashService.cs`                                  | Use `TransactionDate` in CRUD                                         |
| `backend/Services/PortfolioService.cs`                             | Set `HoldingRole` on add                                              |
| `backend/Data/AppDbContext.cs`                                     | EF config for `TransactionDate`; removed duplicate CashItem block     |
| `backend/Data/Migrations/20260729000001_AddCashTransactionDate.cs` | New migration                                                         |
| `backend/Data/Migrations/AppDbContextModelSnapshot.cs`             | Updated snapshot                                                      |
| `database/SCRIPTS/10_AddCashTransactionDate.sql`                   | SQL migration script                                                  |
| `frontend/.../portfolio.models.ts`                                 | Added `holdingRole` and `transactionDate` fields                      |
| `frontend/.../portfolio-page.component.ts`                         | Removed Role/MinAge filters; fixed `filteredTotalMktValue`            |
| `frontend/.../portfolio-page.component.html`                       | Removed Role/MinAge filter UI                                         |
| `frontend/.../add-stock-dialog.component.ts`                       | Added Role & Decision Source form controls                            |
| `frontend/.../add-stock-dialog.component.html`                     | Added Role & Decision Source fields                                   |
| `frontend/.../add-cash-dialog.component.ts`                        | Added Transaction Date datepicker                                     |
| `frontend/.../add-cash-dialog.component.html`                      | Added Transaction Date field                                          |
| `frontend/.../watchlist-page.component.ts`                         | Dynamic `trendSetupOptions`                                           |
| `frontend/.../watchlist-page.component.html`                       | Split CHANGE into CHANGE$ and CHANGE%                                 |
| `frontend/.../grid-column.service.ts`                              | Added watchlist `changePct`, EOD new columns, transaction new columns |
| `frontend/.../eod-signals-page.component.ts`                       | New columns, price fetch, helper methods                              |
| `frontend/.../eod-signals-page.component.html`                     | Added Days Passed, Last Price, Price Diff, Diff% columns              |
| `frontend/.../eod-signals-page.component.scss`                     | Added `.eod-pos`/`.eod-neg` colours                                   |
| `frontend/.../transactions-page.component.ts`                      | Removed MinAge; added Diff$, Trans Date, export CSV                   |
| `frontend/.../transactions-page.component.html`                    | Removed MinAge filter; added Diff$, Trans Date; export button         |
| `frontend/.../allocation-page.component.ts`                        | MatSort/MatTable; sortable beta grid                                  |
| `frontend/.../allocation-page.component.html`                      | mat-table for beta contributors                                       |
| `frontend/.../allocation-page.component.scss`                      | New beta grid styles                                                  |
