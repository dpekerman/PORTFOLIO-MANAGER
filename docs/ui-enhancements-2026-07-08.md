# UI & Feature Enhancements — 2026-07-08

## Verification of Previous Session Items

### ✅ Watchlist Sticky Header (previously incomplete)

- **Root cause found**: `.watchlist-table` had `overflow: hidden` which breaks `position: sticky` on child elements.
- **Fix**: Removed `overflow: hidden` from `.watchlist-table` in `watchlist-page.component.scss`. The `matHeaderRowDef sticky: true` + `position: sticky; top: 0` on header cells now functions correctly within the `.grid-table-wrapper` scroll container.

### ✅ Transactions Sticky Header (previously incomplete)

- **Fix**: Added `overflow-y: auto; max-height: calc(100dvh - 340px)` to `.tx-table-wrapper` and added `.tx-grid-table th.mat-mdc-header-cell { position: sticky; top: 0; }` in `transactions-page.component.scss`. Both stock and option grids now have sticky headers.

---

## New Changes Implemented

### WATCHLIST

| Item               | Change                                                       |
| ------------------ | ------------------------------------------------------------ |
| Sticky grid header | Fixed by removing `overflow: hidden` from `.watchlist-table` |

### TRANSACTIONS

| Item                              | Change                                                                   |
| --------------------------------- | ------------------------------------------------------------------------ |
| Sticky headers (stocks + options) | Added `overflow-y: auto; max-height` + sticky CSS to `.tx-table-wrapper` |

### PORTFOLIO

#### 1. CSV Export — Show All Sub-Records for Multi-Account Tickers

**Before**: Exported one aggregated row per grouped ticker (e.g. KEY.TO with 2 accounts showed 1 row).  
**After**: Exports ALL individual rows. Aggregate header rows are skipped (`if (isAggRow) continue`), but all child rows are exported with their own `accountType`, `shares`, `avgCost`, etc.  
**Files changed**: `portfolio-page.component.ts` — `exportCsv()` method.

#### 2. Filtered Total MKT VALUE when Account Filter is Active

**New computed signal** `filteredTotalMktValue` — returns the sum of market values for all rows currently visible after the account filter is applied, or `null` when no account filter is set.  
**UI**: A highlighted badge appears in the filter bar (right side) when an account filter is active, showing `{Account} total: $XXX,XXX`.  
**Files changed**: `portfolio-page.component.ts`, `portfolio-page.component.html`, `portfolio-page.component.scss`.

#### 3. Add Cash Dialog — Account Type

**New field**: Account Type dropdown added to both **Add Cash** and **Edit Cash** dialogs, with the same account type list as "Add Stock" (`TFSA_L_RBC`, `Corp_TD`, etc.).  
**Backend**: `CashItem.AccountType` (nullable `nvarchar(30)`), `AddCashItemRequest` and `UpdateCashItemRequest` updated with `AccountType`. `CashService.AddAsync/UpdateAsync/ToDto` updated.  
**DB migration**: `20260708000002_AddDecisionSourceAndCashAccount` — adds `AccountType` to `CashItems`.  
**Files changed**: `CashItem.cs`, `Dtos.cs`, `CashService.cs`, `portfolio.models.ts`, `add-cash-dialog.*`, `edit-cash-dialog.*`.

#### 4. Decision Source Column (Stocks Grid + Options Grid)

New column for tracking why a position was entered or changed.  
**Available values**: `App Signal`, `Manual`, `Catalyst`, `Rebalance`, `Risk Control`, `Loss Harvest`.

**Backend**:

- `PortfolioItem.DecisionSource` (nullable `nvarchar(50)`) added
- `OptionItem.DecisionSource` (nullable `nvarchar(50)`) added
- `AddPortfolioItemRequest`, `UpdatePortfolioItemRequest`, `PortfolioItemDto` updated
- `AddOptionItemRequest`, `UpdateOptionItemRequest`, `OptionItemDto` updated
- `PortfolioService.AddAsync/UpdateAsync/ToDto` updated
- `OptionService.AddAsync/UpdateAsync/ToDto` updated
- DB migration: `20260708000002_AddDecisionSourceAndCashAccount`

**Frontend**:

- `PortfolioItem.decisionSource?`, `OptionItem.decisionSource?` added to models
- `AddPortfolioItemRequest.decisionSource?`, `UpdatePortfolioItemRequest.decisionSource?` added
- `AddOptionItemRequest.decisionSource?`, `UpdateOptionItemRequest.decisionSource?` added
- Grid column `decisionSource` added to `portfolio-stocks` in `GridColumnService`
- Grid column `opt_decision_source` added to `portfolio-options` in `GridColumnService`
- Column def `matColumnDef="decisionSource"` added to portfolio HTML grid — inline `mat-select` for direct editing from the grid
- `updateDecisionSource()` method added to `PortfolioPageComponent`
- `decisionSources` constant added to `PortfolioPageComponent`
- Edit Position dialog: `decisionSource` added to form + result interface
- **Files changed**: `PortfolioItem.cs`, `OptionItem.cs`, `Dtos.cs`, `PortfolioService.cs`, `OptionService.cs`, `portfolio.models.ts`, `grid-column.service.ts`, `portfolio-page.component.ts/html`, `edit-position-dialog.component.ts/html`

### CONFIGURATION — Allocation & Risk Management

**New backend**:

- New models: `AllocationRiskTarget`, `AllocationSectorTarget`, `SinglePositionLimit` (`AllocationRiskModels.cs`)
- New DTOs: `AllocationRiskTargetDto`, `AllocationSectorTargetDto`, `SinglePositionLimitDto`, `AllocationRiskConfigDto`, and upsert request records
- `AppDbContext`: 3 new `DbSet<>` properties + `OnModelCreating` entity configs
- New service: `IAllocationRiskService` / `AllocationRiskService` — CRUD for all 3 tables
- New controller: `AllocationRiskController` (`/api/allocation-risk`) — GET all, POST/PUT/DELETE for each table
- Registered in `Program.cs`
- EF migration: `20260708000003_AddAllocationRiskTables` — creates tables and seeds defaults

**Default data seeded**:

| Allocation by Role | Target   |
| ------------------ | -------- |
| Core               | 40%      |
| Strategic          | 15%      |
| Strategic-Income   | 5%       |
| Swing              | 20%      |
| Speculative        | 10%      |
| Options            | 5%       |
| Cash               | 5%       |
| **TOTAL**          | **100%** |

| Allocation by Sector   | Target   |
| ---------------------- | -------- |
| Energy                 | 20%      |
| Industrials            | 20%      |
| Financial Services     | 15%      |
| Communication Services | 5%       |
| Utilities              | 10%      |
| Technology             | 10%      |
| Healthcare             | 5%       |
| Consumer Defensive     | 10%      |
| Materials              | 3%       |
| Cash                   | 2%       |
| **TOTAL**              | **100%** |

| Single Position Limits | Max |
| ---------------------- | --- |
| Core                   | 5%  |
| Strategic              | 5%  |
| Strategic-Income       | 5%  |
| Swing                  | 2%  |
| Speculative            | 2%  |
| Options                | 1%  |

**New frontend**:

- `AllocationRiskConfig`, `AllocationRiskTarget`, `AllocationSectorTarget`, `SinglePositionLimit` interfaces in `portfolio.models.ts`
- `PortfolioApiService`: `getAllocationRiskConfig()`, `upsertRiskTarget()`, `deleteRiskTarget()`, `upsertSectorTarget()`, `deleteSectorTarget()`, `upsertPositionLimit()`, `deletePositionLimit()`
- `ConfigPageComponent`: Allocation & Risk state signals (`riskTargets`, `sectorTargets`, `positionLimits`, inline edit signals), `loadAllocationRisk()`, CRUD methods for all 3 tables, loaded in `ngOnInit()`
- `MatSelectModule` added to config page imports
- New UI card in `config-page.component.html` with 3-column grid (Role Allocation / Sector Allocation / Position Limits), inline add/edit/delete rows, running totals
- New styles in `config-page.component.scss`

### Database Scripts Updated

| File                  | Change                                                                                                                                                                                                                                                                             |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `02_CreateTables.sql` | Added `CashItems.AccountType`, `OptionItems.DecisionSource`, `PortfolioItems.Notes`, `PortfolioItems.DecisionSource`, `WatchlistItems.IsFavorite` (IF NOT EXISTS guards); added CREATE TABLE blocks for `AllocationRiskTargets`, `AllocationSectorTargets`, `SinglePositionLimits` |
| `03_SeedData.sql`     | Added default seed rows for all 3 allocation tables (runs only when tables are empty)                                                                                                                                                                                              |

---

## Files Changed Summary

| Layer              | File                                                           | Change                                                                                             |
| ------------------ | -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| Backend Model      | `Models/PortfolioItem.cs`                                      | Added `DecisionSource`                                                                             |
| Backend Model      | `Models/OptionItem.cs`                                         | Added `DecisionSource`                                                                             |
| Backend Model      | `Models/CashItem.cs`                                           | Added `AccountType`                                                                                |
| Backend Model      | `Models/AllocationRiskModels.cs`                               | **New** — 3 allocation/risk entity classes                                                         |
| Backend DTOs       | `Models/Dtos.cs`                                               | Updated PortfolioItem, OptionItem, Cash DTOs; added Allocation/Risk DTOs                           |
| Backend Service    | `Services/PortfolioService.cs`                                 | `DecisionSource` in Add/Update/ToDto                                                               |
| Backend Service    | `Services/OptionService.cs`                                    | `DecisionSource` in Add/Update/ToDto                                                               |
| Backend Service    | `Services/CashService.cs`                                      | `AccountType` in Add/Update/ToDto                                                                  |
| Backend Service    | `Services/AllocationRiskService.cs`                            | **New**                                                                                            |
| Backend Controller | `Controllers/AllocationRiskController.cs`                      | **New**                                                                                            |
| Backend Context    | `Data/AppDbContext.cs`                                         | 3 new DbSets + entity configs                                                                      |
| Backend Config     | `Program.cs`                                                   | Registered `AllocationRiskService`                                                                 |
| DB Migration       | `Migrations/20260708000002_AddDecisionSourceAndCashAccount.cs` | **New**                                                                                            |
| DB Migration       | `Migrations/20260708000003_AddAllocationRiskTables.cs`         | **New**                                                                                            |
| DB Scripts         | `database/SCRIPTS/02_CreateTables.sql`                         | New columns + new tables                                                                           |
| DB Scripts         | `database/SCRIPTS/03_SeedData.sql`                             | Default allocation data                                                                            |
| Frontend Model     | `core/models/portfolio.models.ts`                              | `decisionSource` on PortfolioItem/OptionItem; `accountType` on CashItem; AllocationRisk interfaces |
| Frontend API       | `core/services/portfolio-api.service.ts`                       | Allocation/Risk API methods                                                                        |
| Frontend Service   | `core/services/grid-column.service.ts`                         | `decisionSource` column in portfolio-stocks/options                                                |
| Frontend Component | `portfolio-page.component.ts`                                  | `decisionSources`, `updateDecisionSource`, `filteredTotalMktValue`, export fix                     |
| Frontend Component | `portfolio-page.component.html`                                | Decision Source column def, filtered total badge                                                   |
| Frontend Component | `portfolio-page.component.scss`                                | `.gf-filtered-total` style                                                                         |
| Frontend Dialog    | `edit-position-dialog.component.ts`                            | `decisionSource` in form + result                                                                  |
| Frontend Dialog    | `edit-position-dialog.component.html`                          | Decision Source select field                                                                       |
| Frontend Dialog    | `add-cash-dialog.component.ts/html`                            | Account Type field                                                                                 |
| Frontend Dialog    | `edit-cash-dialog.component.ts/html`                           | Account Type field                                                                                 |
| Frontend Component | `config-page.component.ts`                                     | Allocation & Risk state + CRUD methods                                                             |
| Frontend Component | `config-page.component.html`                                   | Allocation & Risk UI card                                                                          |
| Frontend Component | `config-page.component.scss`                                   | Allocation styles                                                                                  |
| Frontend SCSS      | `watchlist-page.component.scss`                                | Removed `overflow: hidden` from table (sticky header fix)                                          |
| Frontend SCSS      | `transactions-page.component.scss`                             | Added `overflow-y: auto + max-height + sticky` to table wrapper                                    |
