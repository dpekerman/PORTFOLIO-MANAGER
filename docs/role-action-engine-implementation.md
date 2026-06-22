# Role & Decision Engine Implementation

**Branch:** `develop`  
**Date:** 2026-06-22  
**Scope:** Full-stack — backend model/API + EF migration + Angular shared decision engine + all three pages

---

## Summary

Implemented a **Role field** for Watchlist and Portfolio items and a **shared Decision Engine** that classifies every scanned symbol into `TrendSetup`, `MomentumShift`, `BaseAction`, and `FinalAction`. The engine is role-aware: it adjusts the final portfolio action based on the position's investment role (Core / Strategic / Swing / Speculative / Options).

All three main pages (Watchlist, RSI Scanner, Portfolio) were updated to use the new engine. Old inline momentum methods (`momentumShift`, `momentumAction`, and related class/tooltip helpers) have been removed from every component.

---

## Backend Changes

### 1. `Models/WatchlistItem.cs`
- Added `public string Role { get; set; } = "Strategic";`

### 2. `Models/PortfolioItem.cs`
- Added `public string? HoldingRole { get; set; }` (nullable)

### 3. `Models/ScannerModels.cs` — `RsiScanResult`
- Added three new price properties with XML docs:
  - `public decimal Sma50Price { get; set; }`
  - `public decimal Ema10Price { get; set; }`
  - `public decimal Ema20Price { get; set; }`

### 4. `Models/Dtos.cs`
- `AddWatchlistItemRequest` — added `Role = "Strategic"` parameter
- `UpdateWatchlistRoleRequest(string Role)` — new record
- `WatchlistItemDto` — added `Role = "Strategic"` parameter (5th positional arg)
- `UpdatePortfolioItemRequest` — added `string? HoldingRole = null`
- `PortfolioItemDto` — added `string? HoldingRole = null`

### 5. `Data/AppDbContext.cs`
- `WatchlistItem` entity: `.Property(e => e.Role).HasMaxLength(20).HasDefaultValue("Strategic")`
- `PortfolioItem` entity: `.Property(e => e.HoldingRole).HasMaxLength(20)`

### 6. `Controllers/WatchlistController.cs`
- New endpoint: `[HttpPatch("{id:int}/role")]` → calls `watchlistService.UpdateRoleAsync(id, request.Role, ct)`

### 7. `Services/WatchlistService.cs`
- `IWatchlistService` interface extended with `Task<bool> UpdateRoleAsync(int id, string role, CancellationToken ct = default)`
- `AddAsync` sets `Role = request.Role ?? "Strategic"`
- `ToDto` passes `item.Role ?? "Strategic"` as the 5th arg
- New `UpdateRoleAsync` method: finds item by id, sets Role, saves

### 8. `Services/PortfolioService.cs`
- `UpdateAsync` sets `if (request.HoldingRole is not null) item.HoldingRole = request.HoldingRole`
- `ToDto` includes `item.HoldingRole` as the last parameter

### 9. `Services/RsiScannerService.cs`
- Computes `sma50Price = CalculateSma(closes, 50)`, `ema10Price = CalculateEma(closes, 10)`, `ema20Price = CalculateEma(closes, 20)` from the historical price data
- Assigns `Sma50Price`, `Ema10Price`, `Ema20Price` in the `return new RsiScanResult {}` block

### 10. `Data/Migrations/20260622183326_AddRoleAndHoldingRole.cs` *(created)*
- Manually authored migration (required because binary was locked at time of `ef migrations add`)
- Adds `Role nvarchar(20) NOT NULL DEFAULT 'Strategic'` to `WatchlistItems`
- Adds `HoldingRole nvarchar(20) NULL` to `PortfolioItems`
- **Applied to database** via `dotnet ef database update --configuration Release --no-build`

### 11. `Data/Migrations/AppDbContextModelSnapshot.cs`
- Added `Role` property to `WatchlistItem` entity block
- Added `HoldingRole` property to `PortfolioItem` entity block

---

## Frontend Changes

### 12. `core/models/portfolio.models.ts`
| Interface | Change |
|---|---|
| `WatchlistItem` | Added `role: string` |
| `PortfolioItem` | Added `holdingRole?: string \| null` |
| `UpdatePortfolioItemRequest` | Added `holdingRole?: string \| null` |
| `RsiScanResult` | Added `sma50Price: number`, `ema10Price: number`, `ema20Price: number` |

### 13. `core/services/portfolio-api.service.ts`
- `addWatchlistItem(symbol, notes = '', role = 'Strategic')` — passes `{symbol, notes, role}` to API
- New `updateWatchlistRole(id: number, role: string): Observable<void>` — calls `PATCH /api/watchlist/{id}/role`

### 14. `core/services/watchlist-state.service.ts`
- `addItem(symbol, role = 'Strategic')` — passes role to API call
- New `updateRole(id: number, role: string)` — calls API and updates the items signal in-place

### 15. `core/services/decision-engine.service.ts` *(new file)*

The core of this feature. A `providedIn: 'root'` service with the following exported types and methods:

**Types:**
- `TrendSetup` — union of 9 states: `'Waterfall'|'Reversal'|'Extended'|'Quality Trend'|'Constructive'|'Early Reversal'|'Cooling'|'Caution'|'Neutral'`
- `MomentumShift` — union of momentum states
- `BaseAction` / `FinalAction` — action strings
- `InvestmentRole` — `'Core'|'Strategic'|'Swing'|'Speculative'|'Options'`
- `DecisionResult` — full calculation output (trendSetup, momentumShift, baseAction, finalAction)
- `PageDecision` — display object with text + CSS classes + hover descriptions

**Calculation logic (`calculateDecision(r, role)`):**
- `trendSetup` — classifies using `ema10Price > ema20Price` for direction, `close vs sma50Price`, RSI, MACD histogram delta
- `momentumShift` — uses `rsiSignal` crossover direction, volume ratio, MACD delta
- `baseAction` — maps trendSetup + momentumShift to a neutral action
- `finalAction` — applies role-specific adjustment table

**Role adjustment tables:**
| Role | Bullish Action | Bearish Action |
|---|---|---|
| Core | Hold / Buy Dip | Hold / Trim |
| Strategic | Add / Buy | Reduce |
| Swing | Buy Signal | Sell Signal |
| Speculative | High-Risk Buy | Exit |
| Options | Buy Calls | Buy Puts |

**Page translators:**
- `translateForRsiScanner(r)` — no role, uses `baseAction` as `finalAction`
- `translateForWatchlist(r, role)` — role-adjusted `finalAction`
- `translateForPortfolio(r, role, isOwned)` — ownership-aware; isOwned=true adjusts hold/trim language

---

### 16. `features/watchlist-page/add-watchlist-dialog.component.ts`
- Added `MatSelectModule` import
- Form now has `role: ['Strategic', Validators.required]`
- `roles = ['Core', 'Strategic', 'Swing', 'Speculative', 'Options']`
- Template has `<mat-select formControlName="role">` with options
- `confirm()` returns `AddWatchlistDialogResult { symbol, role }`
- Exported `AddWatchlistDialogResult` interface

### 17. `features/watchlist-page/watchlist-page.component.ts`
- Added `DecisionEngineService` injection
- `SortColumn` type extended with `'role'|'trendSetup'|'finalAction'`, removed `'momentumAction'`
- `roles = ['Core','Strategic','Swing','Speculative','Options']`
- `decisionForSymbol(symbol, role)` method using `engine.translateForWatchlist()`
- `displayedColumns` updated: `['symbol','company','role','price','changePct','sector','rsi','trendSetup','momentumShift','finalAction','actions']`
- `updateRole(w, role)` method
- `roleClass(role)` CSS helper
- **Removed:** `momentumShift`, `momentumAction`, `momentumShiftClass`, `momentumShiftTooltip`, `momentumActionClass`, `momentumActionTooltip`

### 18. `features/watchlist-page/watchlist-page.component.html`
- Grid columns updated: `role` (inline `mat-select` with `(selectionChange)="updateRole(w, $event.value)"`), `trendSetup` badge, `momentumShift` badge, `finalAction` badge
- All badges use `decisionForSymbol(w.item.symbol, w.item.role)`
- Old `change$` and `momentumAction` columns removed

### 19. `features/watchlist-page/watchlist-page.component.scss`
- Added `.wl-ts-badge` base style and 9 trend setup CSS classes:
  - `ts-waterfall`, `ts-reversal`, `ts-extended`, `ts-quality`, `ts-constructive`, `ts-early-reversal`, `ts-cooling`, `ts-caution`, `ts-neutral`
- Added `.role-select` and 5 role color classes: `role-core`, `role-strategic`, `role-swing`, `role-speculative`, `role-options`

### 20. `features/scanner/rsi-scanner-table.component.ts`
- Added `DecisionEngineService` injection
- `displayedColumns` updated: `['trendSetup','momentumShift','baseAction']`, removed `'momentumAction'`
- Added `decision(row: RsiScanResult): PageDecision` method
- **Removed:** all old inline momentum methods (6 methods)

### 21. `features/scanner/rsi-scanner-table.component.html`
- Old `momentumShift` and `momentumAction` column definitions replaced with:
  - `trendSetup` column — shows `dec.trendSetup` with `dec.trendSetupClass`
  - `momentumShift` column — shows `dec.momentumShift` with `dec.momentumShiftClass`
  - `baseAction` column — shows `dec.baseAction` / `dec.finalAction` with `dec.finalActionClass` and `dec.hoverDescription` tooltip
- Uses `@let dec = decision(row)` Angular 17+ template variable syntax

### 22. `features/portfolio/portfolio-page.component.ts`
- Added `DecisionEngineService` injection (`protected readonly engine`)
- `GridSortCol` type updated: `'trendSetup'|'momentumShift'|'finalAction'` replaces `'momentumShift'|'momentumAction'`
- `gridDisplayedColumns` updated: `'trendSetup'`, `'momentumShift'`, `'finalAction'` replace `'momentumShift'`, `'momentumAction'`
- Added `decisionForPortfolio(symbol, holdingRole)` method using `engine.translateForPortfolio(r, holdingRole, true)`
- `gridSortValue()` updated for new columns
- CSV export updated to use `decisionForPortfolio()` instead of old methods
- **Removed:** `momentumShift`, `momentumAction`, `momentumShiftClass`, `momentumShiftTooltip`, `momentumActionClass`, `momentumActionTooltip`

### 23. `features/portfolio/portfolio-page.component.html`
- Old `momentumShift` and `momentumAction` `ng-container` column definitions replaced with:
  - `trendSetup` column — uses `decisionForPortfolio(tsSym, tsRole)` with `.trendSetupClass` and `.trendSetupReason`
  - `momentumShift` column — uses `.momentumShiftClass` and `.momentumShiftReason`
  - `finalAction` column — uses `.finalActionClass` and `.hoverDescription`
- Aggregate rows (`isAggRow`) pass `null` for role (no holdingRole on grouped rows)

---

## Database Schema Changes

```sql
-- WatchlistItems table
ALTER TABLE WatchlistItems ADD Role NVARCHAR(20) NOT NULL DEFAULT 'Strategic';

-- PortfolioItems table
ALTER TABLE PortfolioItems ADD HoldingRole NVARCHAR(20) NULL;
```

All existing watchlist rows default to `Role = 'Strategic'`.

---

## Decision Engine Logic Reference

### TrendSetup Classification
| State | Conditions |
|---|---|
| Waterfall | EMA10 < EMA20 AND RSI < 35 AND MACD delta < 0 |
| Reversal | EMA10 < EMA20 AND RSI < 35 AND MACD delta ≥ 0 |
| Extended | EMA10 > EMA20 AND RSI > 65 AND price > SMA50 |
| Quality Trend | EMA10 > EMA20 AND RSI 50–65 AND price > SMA50 |
| Constructive | EMA10 > EMA20 AND RSI 45–65 |
| Early Reversal | EMA10 < EMA20 AND RSI 35–50 AND MACD delta ≥ 0 |
| Cooling | EMA10 > EMA20 AND RSI > 60 AND MACD delta < 0 |
| Caution | EMA10 < EMA20 AND RSI < 50 |
| Neutral | Default fallback |

### MomentumShift Uses
- `rsiSignal` crossover direction (existing signal from backend)
- Volume ratio (> 1.5 = high volume confirmation)
- `macdHistDelta` (positive = improving, negative = weakening)

---

## Build Status

| Project | Status |
|---|---|
| Backend `.NET 8` | ✅ Build succeeded — 0 errors, 3 pre-existing warnings |
| Frontend `Angular 21` | ✅ Build succeeded — 0 errors, 3 pre-existing budget warnings |
| EF Migration | ✅ Applied to database |
