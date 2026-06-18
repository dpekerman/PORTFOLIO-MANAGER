# Portfolio Manager — Cash & Options Implementation Report

**Date:** 2026-06-18  
**Status:** ✅ Complete — builds successfully

---

## Overview

This report documents the full implementation of **Cash** and **Options** portfolio tracking, added to the existing MY PORTFOLIO page. The portfolio summary bar now aggregates all three asset classes (Stocks, Cash, Options) and the page is restructured into three collapsible sections.

---

## 1. Backend Changes

### 1.1 New Database Models

#### `CashItem` (`Models/CashItem.cs`)

| Field         | Type          | Notes                |
| ------------- | ------------- | -------------------- |
| `Id`          | int (PK)      | Auto-increment       |
| `Description` | string (200)  | Default: "CASH"      |
| `Amount`      | decimal(18,4) | Cash balance         |
| `AddedAt`     | DateTime      | Auto-set on creation |

#### `OptionItem` (`Models/OptionItem.cs`)

| Field               | Type          | Notes                          |
| ------------------- | ------------- | ------------------------------ |
| `Id`                | int (PK)      | Auto-increment                 |
| `UnderlyingTicker`  | string (20)   | e.g. "AAPL"                    |
| `PositionType`      | string (10)   | "CALL" or "PUT"                |
| `ExpirationDate`    | DateTime      | Expiry date                    |
| `Strike`            | decimal(18,4) | Strike price                   |
| `Premium`           | decimal(18,4) | Premium paid per share         |
| `NumberOfContracts` | int           | Number of contracts            |
| `MarketPrice`       | decimal(18,4) | Current market price per share |
| `AddedAt`           | DateTime      | Auto-set on creation           |

### 1.2 DTOs (`Models/Dtos.cs`)

- `AddCashItemRequest` / `UpdateCashItemRequest` / `CashItemDto`
- `AddOptionItemRequest` / `UpdateOptionItemRequest` / `OptionItemDto`
- `OptionTechnicalDataDto` — includes Symbol, CurrentPrice, PreviousClose, YesterdayHigh, YesterdayLow, Rsi14, RsiSignal9, Sma20, Sma50, Ema21, Atr14, BollingerUpper, BollingerLower

### 1.3 Database Context (`Data/AppDbContext.cs`)

- Added `DbSet<CashItem> CashItems`
- Added `DbSet<OptionItem> OptionItems`
- Configured entity mappings in `OnModelCreating`

### 1.4 EF Core Migration

- `Data/Migrations/20260617130000_AddCashAndOptionTables.cs`
- Creates `CashItems` and `OptionItems` tables
- `AppDbContextModelSnapshot.cs` updated accordingly
- Migration runs automatically on startup (development environment)

### 1.5 Services

#### `CashService` (`Services/CashService.cs`)

Interface: `ICashService`  
Methods: `GetAllAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`

#### `OptionService` (`Services/OptionService.cs`)

Interface: `IOptionService`  
Methods: CRUD + `GetTechnicalDataAsync(symbol)`

Technical data computed from Yahoo Finance chart data (`query1.finance.yahoo.com`):

- **RSI-14** — 14-period Relative Strength Index
- **RSI Signal (9-EMA of RSI)** — signal line for RSI crossovers
- **SMA-20**, **SMA-50** — simple moving averages
- **EMA-21** — exponential moving average
- **ATR-14** — Average True Range (volatility)
- **Bollinger Bands** — 20-period, 2σ upper and lower bands
- **Yesterday High/Low**, **Previous Close**

### 1.6 Controllers

#### `CashController` (`Controllers/CashController.cs`)

Base route: `api/cash`

| Method | Endpoint        | Description         |
| ------ | --------------- | ------------------- |
| GET    | `api/cash`      | Get all cash items  |
| GET    | `api/cash/{id}` | Get cash item by ID |
| POST   | `api/cash`      | Add cash item       |
| PUT    | `api/cash/{id}` | Update cash item    |
| DELETE | `api/cash/{id}` | Delete cash item    |

#### `OptionsController` (`Controllers/OptionsController.cs`)

Base route: `api/options`

| Method | Endpoint                         | Description                              |
| ------ | -------------------------------- | ---------------------------------------- |
| GET    | `api/options`                    | Get all option items                     |
| GET    | `api/options/{id}`               | Get option item by ID                    |
| POST   | `api/options`                    | Add option item                          |
| PUT    | `api/options/{id}`               | Update option item                       |
| DELETE | `api/options/{id}`               | Delete option item                       |
| GET    | `api/options/technical/{symbol}` | Get technical data for underlying ticker |

### 1.7 DI Registration (`Program.cs`)

```csharp
builder.Services.AddScoped<ICashService, CashService>();
builder.Services.AddHttpClient<IOptionService, OptionService>(client => {
    client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

---

## 2. Frontend Changes

### 2.1 Models (`core/models/portfolio.models.ts`)

New interfaces added:

- `CashItem`, `AddCashItemRequest`, `UpdateCashItemRequest`
- `OptionItem`, `AddOptionItemRequest`, `UpdateOptionItemRequest`
- `OptionTechnicalData`
- `OptionState` — union type: `'FREE_TRADE_MILESTONE' | 'CUT_LOSS' | 'ON_TARGET' | 'MOMENTUM_CONFIRM' | 'MONITOR' | 'EARLY_EXIT' | 'PROFIT_TARGET_MET' | 'UNKNOWN'`
- `OptionAnalysis` — computed analysis object: `{ item, technical, optionState, stateDescription, action, actionDescription, stockPrice, dte, cost, marketValue, gainLoss, gainLossPct }`

### 2.2 API Service (`core/services/portfolio-api.service.ts`)

9 new methods:

- `getCashItems()`, `addCashItem()`, `updateCashItem()`, `deleteCashItem()`
- `getOptionItems()`, `addOptionItem()`, `updateOptionItem()`, `deleteOptionItem()`
- `getOptionTechnicalData(symbol)`

### 2.3 State Services

#### `CashStateService` (`core/services/cash-state.service.ts`)

Singleton (`providedIn: 'root'`), signals-based.

- `items()` — signal: all cash items
- `loading()` — signal: loading state
- `totalCash()` — computed: sum of all amounts
- `refresh()`, `addItem()`, `updateItem()`, `deleteItem()`

#### `OptionStateService` (`core/services/option-state.service.ts`)

Singleton (`providedIn: 'root'`), signals-based.

- `items()` — signal: all option items
- `_technicalMap` — signal: map of symbol → technical data
- `analyses()` — computed: `OptionAnalysis[]` with state evaluation
- `totalCost()` — computed: sum of all position costs
- `totalMarketValue()` — computed: sum of all market values
- `refresh()`, `addItem()`, `updateItem()`, `deleteItem()`
- Full **Option State Rules Engine** (see Section 3)

### 2.4 Dialog Components

| Component                   | Purpose                                                                                               |
| --------------------------- | ----------------------------------------------------------------------------------------------------- |
| `AddCashDialogComponent`    | Form: Description + Amount                                                                            |
| `EditCashDialogComponent`   | Pre-populated edit form (receives `CashItem` via `MAT_DIALOG_DATA`)                                   |
| `AddOptionDialogComponent`  | Form: Ticker, Type (CALL/PUT), Expiry, Strike, Premium, Contracts, MarketPrice + live cost/MV preview |
| `EditOptionDialogComponent` | Pre-populated edit form (receives `OptionItem` via `MAT_DIALOG_DATA`)                                 |

### 2.5 Portfolio Summary Bar (`portfolio-summary-bar/`)

Updated to aggregate **all three asset classes**:

- **Total Value** = stocks market value + total cash + options market value
- **Total Cost** = stocks cost + cash amount + options cost
- **Total Gain/Loss** = total value − total cost
- **Return %** = gain/loss ÷ total cost
- **Positions** = stocks + cash items + option contracts (tooltip shows breakdown)

### 2.6 Portfolio Page (`portfolio-page.component`)

#### TypeScript changes:

- Injected `CashStateService` and `OptionStateService`
- Added signals: `stocksExpanded`, `cashExpanded`, `optionsExpanded`
- Added `optionDisplayedColumns` (15 columns)
- Added methods: `openAddCashDialog()`, `openEditCashDialog()`, `confirmDeleteCash()`, `openAddOptionDialog()`, `openEditOptionDialog()`, `confirmDeleteOption()`, `optionStateClass()`, `actionClass()`

#### HTML changes:

- Header count badge now shows stocks + cash + options count
- Three collapsible subsections (Stocks, Cash, Options)
- Each section has: clickable header with icon + count badge + total, chevron indicator, empty-state message, and both grid/card views
- Options table has **15 columns**: Underlying Ticker, Position Type, Expiry Date, Strike, Premium, Contracts, Stock Price, DTE (color-coded), Cost, Mkt Value, Gain/Loss, Gain %, Option State badge, Action badge, Edit/Delete

#### SCSS changes:

- `.action-btn-cash` (teal), `.action-btn-option` (purple)
- `.subsection`, `.subsection-header`, `.subsection-icon`, `.subsection-title-group`, `.subsection-total`, `.subsection-chevron`, `.subsection-empty`
- `.cash-card-grid`, `.cash-card`, `.cash-card-header`, `.cash-card-amount`, `.cash-card-date`, `.cash-card-actions`
- `.options-table-wrapper`, `.options-grid-table`
- `.opt-type-badge`, `.put-badge`, `.call-badge`, `.badge-put`, `.badge-call`
- `.dte-urgent` (red, DTE < 14), `.dte-warning` (yellow, DTE 14-43)
- `.opt-state-badge` with variants: `os-free-trade`, `os-cut`, `os-target`, `os-momentum`, `os-monitor`, `os-unknown`
- `.opt-action-badge` with variants: `oa-exit`, `oa-profit`, `oa-caution`, `oa-monitor`
- `.empty-actions`

---

## 3. Option State Rules Engine

The engine evaluates each option position using three DTE buckets and produces an `OptionState` and recommended `action`.

### Inputs Used

- `dte` — days to expiration
- `gainLossPct` — unrealized gain/loss percentage
- `technical.rsi14` — RSI(14) of the underlying stock
- `technical.currentPrice` vs `item.strike` — moneyness
- `item.positionType` — CALL or PUT

### FREE_TRADE_MILESTONE (checked first, all buckets)

If `gainLossPct >= 100%` → state = `FREE_TRADE_MILESTONE`, action = `TAKE_PROFIT`

---

### DTE < 14 (Short / Urgent)

| Condition                         | State       | Action          |
| --------------------------------- | ----------- | --------------- |
| Loss ≥ 50%                        | `CUT_LOSS`  | `EXIT_POSITION` |
| CALL: price > strike AND RSI > 65 | `ON_TARGET` | `TAKE_PROFIT`   |
| PUT: price < strike AND RSI < 35  | `ON_TARGET` | `TAKE_PROFIT`   |
| Default                           | `MONITOR`   | `HOLD_MONITOR`  |

---

### DTE 14–44 (Swing)

| Condition                   | State              | Action          |
| --------------------------- | ------------------ | --------------- |
| Loss ≥ 40%                  | `CUT_LOSS`         | `EXIT_POSITION` |
| CALL: RSI > 70 (overbought) | `MOMENTUM_CONFIRM` | `TAKE_PROFIT`   |
| PUT: RSI < 30 (oversold)    | `MOMENTUM_CONFIRM` | `TAKE_PROFIT`   |
| CALL: RSI 50-70             | `ON_TARGET`        | `HOLD_MONITOR`  |
| PUT: RSI 30-50              | `ON_TARGET`        | `HOLD_MONITOR`  |
| Default                     | `MONITOR`          | `HOLD_MONITOR`  |

---

### DTE ≥ 45 (Long / LEAPS)

| Condition      | State              | Action          |
| -------------- | ------------------ | --------------- |
| Loss ≥ 30%     | `CUT_LOSS`         | `EXIT_POSITION` |
| Gain ≥ 50%     | `EARLY_EXIT`       | `TAKE_PROFIT`   |
| CALL: RSI > 60 | `MOMENTUM_CONFIRM` | `HOLD_MONITOR`  |
| PUT: RSI < 40  | `MOMENTUM_CONFIRM` | `HOLD_MONITOR`  |
| Default        | `MONITOR`          | `HOLD_MONITOR`  |

---

### Action Display Mapping

| Action                          | CSS Class    | Color  |
| ------------------------------- | ------------ | ------ |
| `TAKE_PROFIT` / `EXIT_POSITION` | `oa-exit`    | Red    |
| `HOLD_PROFIT`                   | `oa-profit`  | Green  |
| `CAUTION` / `EARLY_EXIT`        | `oa-caution` | Yellow |
| `HOLD_MONITOR`                  | `oa-monitor` | Gray   |

---

## 4. Database Scripts

Updated `database/SCRIPTS/02_CreateTables.sql` with `IF NOT EXISTS` guards:

```sql
-- CashItems
IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'CashItems' AND type = 'U')
CREATE TABLE CashItems ( Id INT PRIMARY KEY IDENTITY, Description NVARCHAR(200) NOT NULL DEFAULT 'CASH', Amount DECIMAL(18,4) NOT NULL, AddedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE() );

-- OptionItems
IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'OptionItems' AND type = 'U')
CREATE TABLE OptionItems ( Id INT PRIMARY KEY IDENTITY, UnderlyingTicker NVARCHAR(20) NOT NULL, PositionType NVARCHAR(10) NOT NULL, ExpirationDate DATETIME2 NOT NULL, Strike DECIMAL(18,4) NOT NULL, Premium DECIMAL(18,4) NOT NULL, NumberOfContracts INT NOT NULL DEFAULT 1, MarketPrice DECIMAL(18,4) NOT NULL DEFAULT 0, AddedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE() );
```

---

## 5. Testing Guide

### Backend

1. Start backend: `start-backend.bat`
2. Test cash API: `GET /api/cash`, `POST /api/cash` with `{ "amount": 5000, "description": "CASH" }`
3. Test options API: `GET /api/options`, `POST /api/options` with full option object
4. Test technical data: `GET /api/options/technical/AAPL`

### Frontend

1. Start frontend: `start-frontend.bat`
2. Navigate to MY PORTFOLIO
3. Verify summary bar shows combined totals across all 3 asset types
4. Click "+ Add Cash" → enter amount → verify it appears in Cash section
5. Click "+ Add Option" → fill form → verify it appears in Options section with state badge
6. Toggle between Card and Grid views
7. Collapse/expand each section using the chevron
8. Edit and delete items in each section
9. Verify DTE coloring (red < 14, yellow 14-43)
10. Verify Option State badges and Action badges reflect the rules engine

---

## 6. File Inventory

### Backend (new/modified)

| File                                                       | Status      |
| ---------------------------------------------------------- | ----------- |
| `Models/CashItem.cs`                                       | ✅ Created  |
| `Models/OptionItem.cs`                                     | ✅ Created  |
| `Models/Dtos.cs`                                           | ✅ Modified |
| `Data/AppDbContext.cs`                                     | ✅ Modified |
| `Data/Migrations/20260617130000_AddCashAndOptionTables.cs` | ✅ Created  |
| `Data/Migrations/AppDbContextModelSnapshot.cs`             | ✅ Modified |
| `Services/CashService.cs`                                  | ✅ Created  |
| `Services/OptionService.cs`                                | ✅ Created  |
| `Controllers/CashController.cs`                            | ✅ Created  |
| `Controllers/OptionsController.cs`                         | ✅ Created  |
| `Program.cs`                                               | ✅ Modified |
| `database/SCRIPTS/02_CreateTables.sql`                     | ✅ Modified |

### Frontend (new/modified)

| File                                                         | Status       |
| ------------------------------------------------------------ | ------------ |
| `core/models/portfolio.models.ts`                            | ✅ Modified  |
| `core/services/portfolio-api.service.ts`                     | ✅ Modified  |
| `core/services/cash-state.service.ts`                        | ✅ Created   |
| `core/services/option-state.service.ts`                      | ✅ Created   |
| `portfolio/add-cash-dialog/` (3 files)                       | ✅ Created   |
| `portfolio/edit-cash-dialog/` (3 files)                      | ✅ Created   |
| `portfolio/add-option-dialog/` (3 files)                     | ✅ Created   |
| `portfolio/edit-option-dialog/` (3 files)                    | ✅ Created   |
| `portfolio-summary-bar/portfolio-summary-bar.component.ts`   | ✅ Modified  |
| `portfolio-summary-bar/portfolio-summary-bar.component.html` | ✅ Modified  |
| `portfolio/portfolio-page.component.ts`                      | ✅ Modified  |
| `portfolio/portfolio-page.component.html`                    | ✅ Rewritten |
| `portfolio/portfolio-page.component.scss`                    | ✅ Modified  |
