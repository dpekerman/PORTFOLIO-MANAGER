# UI Enhancements — 2026-07-02

## Summary of Changes

---

## 1. Backup / Restore — Watchlist, Allocation (Cash + Options), Portfolio

### Problem

Data was lost 3 times and had to be manually re-created. All manually-entered data (watchlist symbols, cash positions, options positions, portfolio holdings) needed a persistent backup/restore mechanism.

### Solution

Added **Backup** (download) and **Restore** (upload) buttons to:

- **Watchlist page** header actions
- **Allocation page** header actions (backs up both Cash and Options together)
- **Portfolio page** header actions

#### Backup Format

Each backup downloads as a `.json` file with the structure:

```json
{
  "exportedAt": "2026-07-02T...",
  "type": "watchlist|allocation|portfolio",
  "items": [...]
}
```

For allocation backups: `{ "exportedAt": "...", "type": "allocation", "cash": [...], "options": [...] }`

#### Behavior

- **Backup**: Calls backend endpoint → downloads JSON file to local machine
- **Restore**: User selects a `.json` file → shows confirmation dialog with item count and date → clears all current data → re-inserts from backup

#### Backend Endpoints Added

| Endpoint                      | Method | Description                                |
| ----------------------------- | ------ | ------------------------------------------ |
| `GET /api/watchlist/backup`   | GET    | Returns all watchlist items as JSON        |
| `POST /api/watchlist/restore` | POST   | Clears + restores watchlist from JSON body |
| `GET /api/cash/backup`        | GET    | Returns all cash items as JSON             |
| `POST /api/cash/restore`      | POST   | Clears + restores cash items               |
| `GET /api/options/backup`     | GET    | Returns all option items as JSON           |
| `POST /api/options/restore`   | POST   | Clears + restores option items             |
| `GET /api/portfolio/backup`   | GET    | Returns all portfolio items as JSON        |
| `POST /api/portfolio/restore` | POST   | Clears + restores portfolio items          |

#### Files Changed

- `backend/PortfolioManager.Api/Models/Dtos.cs` — Added backup/restore DTO records
- `backend/PortfolioManager.Api/Services/WatchlistService.cs` — Added `BackupAsync`, `RestoreAsync`
- `backend/PortfolioManager.Api/Services/CashService.cs` — Added `BackupAsync`, `RestoreAsync`
- `backend/PortfolioManager.Api/Services/OptionService.cs` — Added `BackupAsync`, `RestoreAsync`
- `backend/PortfolioManager.Api/Services/PortfolioService.cs` — Added `BackupAsync`, `RestoreAsync`
- `backend/PortfolioManager.Api/Controllers/WatchlistController.cs` — Backup/restore endpoints
- `backend/PortfolioManager.Api/Controllers/CashController.cs` — Backup/restore endpoints
- `backend/PortfolioManager.Api/Controllers/OptionsController.cs` — Backup/restore endpoints
- `backend/PortfolioManager.Api/Controllers/PortfolioController.cs` — Backup/restore endpoints
- `frontend/.../portfolio-api.service.ts` — Added `backupWatchlist`, `restoreWatchlist`, `backupCash`, `restoreCash`, `backupOptions`, `restoreOptions`, `backupPortfolio`, `restorePortfolio`
- `frontend/.../watchlist-page.component.ts` — Added `backupWatchlist()`, `onRestoreFileSelected()`
- `frontend/.../watchlist-page.component.html` — Backup/restore buttons in header
- `frontend/.../allocation-page.component.ts` — Added `backupAllocationData()`, `onRestoreFileSelected()`
- `frontend/.../allocation-page.component.html` — Backup/restore buttons in header
- `frontend/.../portfolio-page.component.ts` — Added `backupPortfolioData()`, `onPortfolioRestoreFileSelected()`
- `frontend/.../portfolio-page.component.html` — Backup/restore buttons in header

---

## 2. Hide Portfolio Value / Total Return / Positions from RSI Scanner Header

### Problem

The market header displayed Portfolio Value, Total Return, and Positions count on every screen, which was unnecessary and cluttered the header.

### Solution

Removed the entire `portfolio-kpis` section from the `MarketHeaderComponent`.

#### Files Changed

- `frontend/.../market-header.component.html` — Removed `<div class="portfolio-kpis">` block
- `frontend/.../market-header.component.ts` — Removed `PortfolioStateService` injection, `isPortfolioPositive` computed signal, `CurrencyPipe`, `DecimalPipe` imports

---

## 3. Fix Allocation Page — Total % for Cash and Options (Collapsed State)

### Problem

When Cash or Options sections were **collapsed**, the percentage shown in the header row was wrong:

- Cash showing `11.99%` when expanded → showed `0.1%` when collapsed
- Options showing `3.57%` when expanded → showed `0.0%` when collapsed

### Root Cause

The header row used `{{ cashPct() | number: '1.1-1' }}%` where `cashPct()` returns a decimal fraction (e.g., `0.1199`). The `number` pipe formats it as-is (`0.1`), while the inner table correctly used `| percent: '1.2-2'` which multiplies by 100.

### Fix

Changed the header row format to `{{ cashPct() * 100 | number: '1.1-1' }}%` and same for `optionsPct()`.

#### Files Changed

- `frontend/.../allocation-page.component.html` — Fixed `cashPct() * 100` and `optionsPct() * 100`

---

## 4. Watchlist — 52-Week High / Low Data

### Problem

The watchlist had no visibility into 52-week price range, making it hard to assess if a stock is near its high or low.

### Solution

Added a new **"52W RANGE"** column to the watchlist grid view and a 52W strip to the card view.

#### Grid View — New `52W RANGE` Column

For each symbol, displays:

- **H** `$52.10` `-12.3%` ← current price is 12.3% below 52W High
- **L** `$34.50` `+25.1%` ← current price is 25.1% above 52W Low

The % figures are color-coded:

- Distance from High → red (you are X% below the 52W peak)
- Distance from Low → green (you are X% above the 52W floor)

#### Card View — New 52W Strip

Shows a compact 2-line strip at the bottom of each card:

- `52W H $52.10 -12.3%`
- `52W L $34.50 +25.1%`

The `week52High` and `week52Low` fields were already present in `StockQuote` model — only UI display was added.

#### Files Changed

- `frontend/.../watchlist-page.component.html` — Added `week52` column definition
- `frontend/.../watchlist-page.component.ts` — Added `'week52'` to `displayedColumns`
- `frontend/.../watchlist-page.component.scss` — Added `.week52-cell`, `.week52-row`, `.week52-label`, `.week52-price`, `.week52-pct` styles
- `frontend/.../watchlist-card.component.html` — Added `.week52-strip` section
- `frontend/.../watchlist-card.component.scss` — Added `.week52-strip`, `.week52-item`, `.week52-tag`, `.week52-val`, `.week52-delta` styles

---

## 5. Angular @defer — Deferred Rendering for Heavy Components

Introduced Angular 17+ `@defer` blocks to improve initial render performance by deferring non-critical heavy content.

### Applied Locations

#### Allocation Page — Sector Exposition

```html
@defer (on viewport) {
<app-sector-exposition />
} @placeholder {
<div class="sector-placeholder">...</div>
}
```

The sector chart renders only when it enters the viewport, reducing initial page load cost.

#### Watchlist Page — Grid Table

```html
@defer (on idle) {
<div class="grid-table-wrapper">
  <table mat-table ...>
    ...
  </table>
</div>
} @placeholder {
<mat-progress-bar mode="indeterminate" />
}
```

The heavy `mat-table` with all columns defers to browser idle time, letting the filter bar and page header render first.

#### Files Changed

- `frontend/.../allocation-page.component.html` — `@defer (on viewport)` wrapping sector exposition
- `frontend/.../allocation-page.component.scss` — Added `.sector-placeholder` styles
- `frontend/.../watchlist-page.component.html` — `@defer (on idle)` wrapping grid table
- `frontend/.../watchlist-page.component.scss` — Added `.wl-table-placeholder` style

---

## Build Status

| Target                                         | Result                                               |
| ---------------------------------------------- | ---------------------------------------------------- |
| Backend `.NET 8`                               | ✅ Compiles (server running, file-lock warning only) |
| Frontend `ng build --configuration production` | ✅ Success (CSS budget warnings are pre-existing)    |
| TypeScript diagnostics                         | ✅ No errors                                         |

---

## Files Modified Summary

### Backend

| File                                 | Change                                          |
| ------------------------------------ | ----------------------------------------------- |
| `Models/Dtos.cs`                     | + Backup/restore DTO records for all 4 entities |
| `Services/WatchlistService.cs`       | + `BackupAsync`, `RestoreAsync`                 |
| `Services/CashService.cs`            | + `BackupAsync`, `RestoreAsync`                 |
| `Services/OptionService.cs`          | + `BackupAsync`, `RestoreAsync`                 |
| `Services/PortfolioService.cs`       | + `BackupAsync`, `RestoreAsync`                 |
| `Controllers/WatchlistController.cs` | + `GET /backup`, `POST /restore`                |
| `Controllers/CashController.cs`      | + `GET /backup`, `POST /restore`                |
| `Controllers/OptionsController.cs`   | + `GET /backup`, `POST /restore`                |
| `Controllers/PortfolioController.cs` | + `GET /backup`, `POST /restore`                |

### Frontend

| File                                                    | Change                                                       |
| ------------------------------------------------------- | ------------------------------------------------------------ |
| `core/services/portfolio-api.service.ts`                | + 8 backup/restore methods                                   |
| `shared/market-header/market-header.component.html`     | - Removed portfolio KPIs section                             |
| `shared/market-header/market-header.component.ts`       | - Removed PortfolioStateService, unused pipes                |
| `features/allocation/allocation-page.component.html`    | Fix % formula; + backup/restore buttons; + @defer            |
| `features/allocation/allocation-page.component.ts`      | + MatSnackBar, PortfolioApiService; + backup/restore methods |
| `features/allocation/allocation-page.component.scss`    | + sector-placeholder styles                                  |
| `features/watchlist-page/watchlist-page.component.html` | + 52W column; + backup/restore buttons; + @defer             |
| `features/watchlist-page/watchlist-page.component.ts`   | + 52W to displayedColumns; + backup/restore methods          |
| `features/watchlist-page/watchlist-page.component.scss` | + 52W column styles; + placeholder styles                    |
| `features/watchlist-page/watchlist-card.component.html` | + 52W strip section                                          |
| `features/watchlist-page/watchlist-card.component.scss` | + 52W strip styles                                           |
| `features/portfolio/portfolio-page.component.html`      | + backup/restore buttons                                     |
| `features/portfolio/portfolio-page.component.ts`        | + backup/restore methods                                     |
