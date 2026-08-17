# UI & CI Enhancements — August 14, 2026

---

## 1. CI Pipeline Fix — `.github/workflows/ci.yml`

**Problem:** GitHub Actions job `.NET 8 API` failed at "Run unit tests" with:

> `MSBUILD : error MSB1003: Specify a project or solution file.`

**Root cause:** The step ran `dotnet test --no-build` from `backend/` which has no `.sln` file. The test project was also never built (only `PortfolioManager.Api` was built in the prior step).

**Fix:** Changed working directory to `backend/PortfolioManager.Tests` and removed `--no-build`; packages were already restored by the API step so `--no-restore` is still safe to use but omitted to keep the command simple. Now runs: `dotnet test PortfolioManager.Tests/PortfolioManager.Tests.csproj --configuration Release --verbosity normal`.

---

## 2. Portfolio — Add Stock Dialog: Hide Close Fields for OPEN Transactions

**Files:** `add-stock-dialog.component.html`

Close Date and Closing Price fields are now conditionally rendered:

```html
@if (form.controls.transactionType.value === 'CLOSE') {
<!-- Close Date | Closing Price -->
}
```

- When Transaction Type = **OPEN** (default): Close Date and Closing Price fields are hidden from DOM
- When Transaction Type = **CLOSE**: both fields appear in the correct grid positions

---

## 3. Options — Decision Source — Closed Field

### Backend (`Dtos.cs`, `OptionItem.cs`, `OptionService.cs`)

- Added `DecisionSourceClosed` property to `OptionItem` C# model (the DB column already existed from migration `AddDecisionSourceClosed`)
- Added `string? DecisionSourceClosed = null` to `UpdateOptionItemRequest` and `OptionItemDto` records
- Updated `OptionService.UpdateAsync()` to map `request.DecisionSourceClosed → item.DecisionSourceClosed`
- Updated `OptionService.ToDto()` to include `DecisionSourceClosed` in the projected DTO

### Frontend (`portfolio.models.ts`, `edit-option-dialog.component.ts/.html`)

- Added `decisionSourceClosed?: string | null` to `OptionItem` and `UpdateOptionItemRequest` interfaces
- Added `decisionSourceClosed` form control to the edit dialog reactive form
- New field is submitted via `updateItem()` call
- In the template, the field is conditionally shown:

```html
@if (form.controls.transactionType.value === 'CLOSE') {
<mat-form-field>
  <mat-label>Decision Source — Closed</mat-label>
  <mat-select formControlName="decisionSourceClosed">...</mat-select>
</mat-form-field>
}
```

---

## 4. Transactions Page — Decision Source — Closed Column (Options Grid)

**Files:** `grid-column.service.ts`, `transactions-page.component.html`

Added `otx_decision_source_closed` column to the `transactions-options` grid registry (after `otx_decision_source`).

Added `<ng-container matColumnDef="otx_decision_source_closed">` to the transactions page HTML, rendering `a.item.decisionSourceClosed`. Column is user-configurable (toggleable via the grid column config button).

---

## 5. RSI Scanner — Sortable Columns + Default Trend Shift Grouping

**Files:** `rsi-scanner-table.component.ts`, `rsi-scanner-table.component.html`

- Added `MatSortModule` to imports
- Added `sortCol` / `sortDir` signals (default: `momentumShift` / `asc`)
- Added `trendShiftPriority()` helper with grouping order:
  1. Bull Turn / Bear Turn (priority 0)
  2. Stabilizing (priority 1)
  3. Still Falling / Still Rising (priority 2)
  4. Waiting / empty (priority 3)
- Added `sortedResults` computed signal that applies the active sort before rendering
- Added `onSortChange(sort: Sort)` handler
- Table element now uses `matSort` + `matSortActive="momentumShift"` + `matSortDirection="asc"` + `(matSortChange)`
- Added `mat-sort-header` to: TICKER, RSI (14), RSI Δ1D, Price, Reversal P., Trend Shift

---

## 6. EOD Signals Page — Excel Export

**Files:** `eod-signals-page.component.ts`, `eod-signals-page.component.html`

- Added `import * as XLSX from 'xlsx'` (library was already installed)
- Added `exportToExcel()` method that exports `sortedSignals()` with columns:
  Date, Ticker, Scan Type, Signal Type, Trend Shift, Turn Strength, RSI (14), RSI Δ1D, Entry Price, Stop Loss, Risk / Share, Risk %, SMA 200, Signal Price, Volume, Reversal P., Mode, State, Days Passed
- Output filename: `eod-signals-{YYYY-MM-DD}.xlsx`
- Added download icon button to the page toolbar (disabled when no signals loaded)
