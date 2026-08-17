# UI & Feature Enhancements — 2026-07-07

## Summary

This document covers all changes implemented on 2026-07-07 across the Portfolio, Watchlist, EOD Signals, and Transactions sections.

---

## PORTFOLIO

### 1. Defect Fix — % TOTAL Calculation

**Problem:** The `% TOTAL` column was dividing position market value by the stocks-only total (`portfolio.totalValue()`), which excluded cash and options from the denominator.

**Root Cause:**  
`CPX.TO MKT VALUE = 37,676 / Stock Total ≠ 37,676 / 794,938 (full portfolio)`

**Fix:**

- Added `portfolioGrandTotal` computed signal in `portfolio-page.component.ts`:
  ```ts
  protected readonly portfolioGrandTotal = computed(() =>
    this.portfolio.totalValue() + this.cashState.totalCash() + this.optionState.totalMarketValue()
  );
  ```
- Updated both the aggregate row and individual row `% TOTAL` cells in the HTML template to use `portfolioGrandTotal()` as the denominator.
- Added `portfolioPct` case to `gridSortValue()` so sorting also uses the correct denominator.

---

### 2. Defect Fix — MKT VALUE Sorting for Multi-Account Tickers

**Problem:** Clicking "Sort by MKT VALUE highest first" did not correctly position tickers with multiple accounts (e.g. KEY.TO, RCI-B.TO, ENB.TO) because sorting was applied to individual rows _before_ grouping, so groups were ordered by their first position's MV rather than their aggregate MV.

**Fix:** Rewrote the `gridRows()` computed to:

1. Group rows by symbol first.
2. Sort within each group (individual row order).
3. Sort _groups_ by their aggregate sort value using the new `aggSortValue()` method.
4. Build the result in sorted group order.

New private method `aggSortValue(group, col)` computes the representative sort value for a multi-account group (e.g. total MV, total gain/loss, etc.).

---

### 3. Add Sorting on % TOTAL Column

**Change:** Added `portfolioPct` to the `GridSortCol` type and to the `gridSortValue()` / `aggSortValue()` switch statements. Added `mat-sort-header` directive to the `% TOTAL` column header in the template. The tooltip also updated to reflect "stocks + cash + options".

---

### 4. Defect Fix — Unable to Change ROLE on Positions with Multiple Transactions

**Problem:** Child rows (individual transactions within a grouped/multi-account position) showed a static `—` instead of a role selector, making it impossible to change their `holdingRole`.

**Fix:** Removed the `@else if (!groupedSymbols().has(...))` guard and replaced it with a simple `@else` that shows a role `mat-select` for ALL non-aggregate rows (both standalone and child rows), each bound to `updateHoldingRole()`.

---

### 5. Set Roles for Individual Transactions (Multiple Roles per Position)

**Change:** Each individual transaction row in a grouped position now displays its own `mat-select` for the holding role (Swing, Strategic, Strategic-Income, etc.), allowing different roles per transaction within the same ticker. The aggregate row's role select still updates all rows in the group at once.

---

### 6. New Role — "Strategic-Income" + Seed Specific Tickers

**Changes:**

- Added `'Strategic-Income'` to the `roles` constant in both the Portfolio and Watchlist components.
- Added `roleClass()` case: `'Strategic-Income'` → CSS class `role-strategic-income` (teal `#26a69a`).
- Added `.role-strategic-income` CSS in `portfolio-page.component.scss` and `watchlist-page.component.scss`.
- Created SQL seed script: `database/SCRIPTS/09_SetStrategicIncomeRole.sql`  
  Sets `HoldingRole = 'Strategic-Income'` for BANK.TO, SIXY.TO, T.TO, and HMAX.TO.  
  **Run this script manually against the database after deploying the backend.**

---

### 7. Add Filter on ACCOUNT

**Change:** Added a new `filterAccount` signal and `uniqueAccounts` computed in `portfolio-page.component.ts`.  
Added an "Account" dropdown filter to the grid filter bar in the HTML template.  
Updated `gridRows()`, `clearGridFilters()`, and `hasActiveFilters` to include the account filter.

---

## WATCHLIST

### 8. Favourite Flag — Tag & Filter

**Backend changes:**

- Added `IsFavorite bool` property to `WatchlistItem` model.
- Created EF Core migration: `20260707000001_AddWatchlistFavorite.cs`.
- Updated `AppDbContextModelSnapshot.cs` to include `IsFavorite`.
- Updated `AppDbContext.cs` entity config to configure the column (`bit`, default `false`).
- Updated `WatchlistItemDto` to include `IsFavorite`.
- Added `UpdateWatchlistFavoriteRequest` DTO.
- Added `UpdateFavoriteAsync()` to `IWatchlistService` and `WatchlistService`.
- Added `PATCH /api/watchlist/{id}/favorite` endpoint to `WatchlistController`.

**Frontend changes:**

- Added `isFavorite: boolean` to `WatchlistItem` interface in `portfolio.models.ts`.
- Added `updateWatchlistFavorite(id, isFavorite)` to `PortfolioApiService`.
- Added `updateFavorite(id, isFavorite)` to `WatchlistStateService`.
- Added `filterFavorites = signal(false)` in `WatchlistPageComponent`.
- Added a star icon toggle button in the filter bar to show only favourites.
- Updated `filteredSorted()` to apply the favourites filter.
- Added `toggleFavorite(w)` method.
- Added a ⭐ star icon button to each row in the grid (in the actions column) to toggle the favourite state.
- Updated grid clear-filters button to also reset `filterFavorites`.

**To apply the DB migration:** Run `dotnet ef database update` from the backend directory.

---

### 9. BUY SCORE Column Sorting

**Change:** Added `'buyScore'` to the `SortColumn` type union. Added a `case 'buyScore'` in the `filteredSorted()` sort switch that uses `buyScoreForSymbol().score`. Added `mat-sort-header` directive to the BUY SCORE column header.

---

### 10. Defect Fix — "Avoid new buy/Review" Shows Green Instead of Yellow

**Root Cause:** The `finalActionClass()` method in `decision-engine.service.ts` checked for `a.includes('buy')` _before_ checking `a.includes('avoid')`. The action string "Avoid New Buy / Review" matched the `buy` branch first and was assigned the green `ma-confirmed-buy` class.

**Fix:** Moved the `avoid/caution/review/wait` check to come _before_ the `buy/accumulate/add` check in `finalActionClass()`. The action now correctly maps to `ma-avoid`.

Additionally, updated the `.ma-avoid` CSS in `watchlist-page.component.scss` from grey to **yellow** (`rgba(255, 235, 59, 0.82)`) as requested.

---

### 11. Notes for Watchlist Items

**Backend changes:**

- Added `UpdateWatchlistNotesRequest(string Notes)` DTO.
- Added `UpdateNotesAsync(id, notes)` to `IWatchlistService` and `WatchlistService`.
- Added `PATCH /api/watchlist/{id}/notes` endpoint to `WatchlistController`.

**Frontend changes:**

- Added `updateWatchlistNotes(id, notes)` to `PortfolioApiService`.
- Added `updateNotes(id, notes)` to `WatchlistStateService`.
- Added a `notes` pinned column to the `watchlist` grid in `GridColumnService`.
- Added a `<ng-container matColumnDef="notes">` column definition in the HTML template.
- Added `openNotes(w)` method in `WatchlistPageComponent` that opens `TransactionNotesDialogComponent` (the same dialog used on the Transactions page).
- The notes icon in the actions column shows `sticky_note_2` (filled) when notes exist, and `note_add` when empty. Tooltip shows note text preview.

---

## EOD SIGNALS

### 12. Defect Fix — Snack-bar Appears on Every Filter Change

**Problem:** The `pollForUpdates()` method ran every 30 seconds and compared `m.totalCount` (unfiltered total from the meta API) against `this.totalCount()` (the _filtered_ count from the current page response). When a filter was active (e.g. filtering by ticker), the filtered count would be much lower than the unfiltered total, causing the snack-bar to fire on every poll.

**Fix:** Added a `lastKnownMetaCount = signal<number | null>(null)` private signal that tracks the last _unfiltered_ total seen during background polling.

- Initialized from the startup meta call in `ngOnInit`.
- Updated from every `refreshMeta()` call.
- `pollForUpdates()` now compares `m.totalCount` against `lastKnownMetaCount()` (not the filtered display count). The snack-bar only fires when the unfiltered total genuinely increases.

---

## TRANSACTIONS

### 13. Add "Last Price" Column

**Change:** Added `'tx_last_price'` to the `StockTxCol` type, added a sort value case (`s.quote?.currentPrice ?? 0`), added `stockLastPrice()` helper method, added a new `<ng-container matColumnDef="tx_last_price">` column in the HTML template, and replaced `tx_mkt_value` with `tx_last_price` in `GridColumnService`'s `transactions-stocks` column registry.

### 14. Remove "Current MKT Value" Column

**Change:** Removed `'tx_mkt_value'` from the `StockTxCol` type, removed the `tx_mkt_value` sort case, removed the HTML column definition, and replaced the column entry in `GridColumnService` with `tx_last_price`.

---

## Database Migration Steps

After deploying the backend, run the following in order:

1. **Apply EF Core migration:**

   ```bash
   cd backend/PortfolioManager.Api
   dotnet ef database update
   ```

   This adds the `IsFavorite` column to `WatchlistItems`.

2. **Set Strategic-Income role for specified tickers:**
   Run the SQL script against the local database:
   ```
   database/SCRIPTS/09_SetStrategicIncomeRole.sql
   ```

---

## Files Changed

| File                                                             | Changes                                                                                                                                                                                                                       |
| ---------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `backend/Models/WatchlistItem.cs`                                | Added `IsFavorite` property                                                                                                                                                                                                   |
| `backend/Models/Dtos.cs`                                         | Updated `WatchlistItemDto`; added `UpdateWatchlistFavoriteRequest`, `UpdateWatchlistNotesRequest`                                                                                                                             |
| `backend/Services/WatchlistService.cs`                           | Added `UpdateFavoriteAsync`, `UpdateNotesAsync`                                                                                                                                                                               |
| `backend/Controllers/WatchlistController.cs`                     | Added `PATCH favorite` and `PATCH notes` endpoints                                                                                                                                                                            |
| `backend/Data/AppDbContext.cs`                                   | Added `IsFavorite` entity config                                                                                                                                                                                              |
| `backend/Data/Migrations/20260707000001_AddWatchlistFavorite.cs` | New migration                                                                                                                                                                                                                 |
| `backend/Data/Migrations/AppDbContextModelSnapshot.cs`           | Updated WatchlistItem snapshot                                                                                                                                                                                                |
| `database/SCRIPTS/09_SetStrategicIncomeRole.sql`                 | New seed script                                                                                                                                                                                                               |
| `core/models/portfolio.models.ts`                                | Added `isFavorite` to `WatchlistItem`                                                                                                                                                                                         |
| `core/services/portfolio-api.service.ts`                         | Added `updateWatchlistFavorite`, `updateWatchlistNotes`                                                                                                                                                                       |
| `core/services/watchlist-state.service.ts`                       | Added `updateFavorite`, `updateNotes`                                                                                                                                                                                         |
| `core/services/decision-engine.service.ts`                       | Fixed `finalActionClass` ordering (avoid-before-buy)                                                                                                                                                                          |
| `core/services/grid-column.service.ts`                           | Added `tx_last_price`; removed `tx_mkt_value`; added `notes` to watchlist                                                                                                                                                     |
| `features/portfolio/portfolio-page.component.ts`                 | Added `portfolioGrandTotal`, `uniqueAccounts`, `filterAccount`, `aggSortValue`; added `portfolioPct` sort; added `Strategic-Income` role; fixed `gridRows()` grouping/sort; updated `clearGridFilters` and `hasActiveFilters` |
| `features/portfolio/portfolio-page.component.html`               | Fixed `% TOTAL` formula; added `mat-sort-header` to `% TOTAL`; fixed child-row role select; added Account filter                                                                                                              |
| `features/portfolio/portfolio-page.component.scss`               | Added `.role-strategic-income` CSS class                                                                                                                                                                                      |
| `features/watchlist-page/watchlist-page.component.ts`            | Added `buyScore` sort; `filterFavorites`; `Strategic-Income` role; `toggleFavorite`; `openNotes`; updated `roleClass`                                                                                                         |
| `features/watchlist-page/watchlist-page.component.html`          | Added favourites star filter; `mat-sort-header` on BUY SCORE; notes column; star/notes in actions                                                                                                                             |
| `features/watchlist-page/watchlist-page.component.scss`          | Fixed `ma-avoid` to yellow; added `.role-strategic-income`                                                                                                                                                                    |
| `features/eod-signals/eod-signals-page.component.ts`             | Added `lastKnownMetaCount`; fixed `pollForUpdates` and `refreshMeta`                                                                                                                                                          |
| `features/transactions/transactions-page.component.ts`           | Added `tx_last_price` type/sort; removed `tx_mkt_value`; added `stockLastPrice()`                                                                                                                                             |
| `features/transactions/transactions-page.component.html`         | Replaced `tx_mkt_value` column with `tx_last_price`                                                                                                                                                                           |
