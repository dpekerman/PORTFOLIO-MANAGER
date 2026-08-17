# EOD Signals — Implementation & How It Works

**Date:** 2026-06-24  
**Feature:** Automatic persistence of EOD CONFIRM and CONFIRMED signals to a queryable SQL table during the configured end-of-day window.

---

## How the Feature Works — End-to-End

### 1. Background Scanner Loop

`RsiAlertBackgroundService` runs every N seconds (configurable, default 60s) as a .NET `BackgroundService`. On each tick it calls `IRsiScannerService.ScanAsync()`, which scans all TSX + portfolio/watchlist symbols via Yahoo Finance and assigns each result a `SignalStatus`:

| Status | Meaning |
|--------|---------|
| `EodConfirm` | All 4 EOD rules met (RSI < 25 / > 75, Price vs 9-EMA, Volume ≥ 1.5× avg, ATR position) |
| `Confirmed` | Price-action trigger met on candle close (MACD crossover / Bollinger breakout / candle pattern) |
| `EarlyWarning` | RSI threshold crossed, no full confirmation |
| `Neutral` | No signal |

### 2. EOD Window Check

After each scan the background service checks `ScannerRuntimeConfig.IsEodWindowActive()`:

```
Current ET time ≥ EodWindowStart  AND  Current ET time ≤ EodWindowEnd
AND EodWindowEnabled = true
```

Default window: **3:30 PM – 4:00 PM Eastern Time** (configurable on the Configuration screen).

### 3. What Gets Persisted

When the EOD window is active, the service collects signals from **both** chains:

```
OversoldChain  (ScanType = "Oversold")  → signals where Status = EodConfirm OR Confirmed
OverboughtChain(ScanType = "Overbought")→ signals where Status = EodConfirm OR Confirmed
```

**EarlyWarning signals are NOT persisted** — they have not yet met the confirmation threshold.

### 4. Database Write (`EodSignalPersistenceService.SaveAsync`)

For each qualifying signal, a `DailySignal` row is inserted into the `DailySignals` SQL Server table with the following fields mapped from `RsiScanResult`:

| DailySignal Column | Source |
|--------------------|--------|
| Symbol | `r.Symbol` |
| CompanyName | `r.CompanyName` |
| ScanType | `r.ScanType.ToString()` → `"Oversold"` / `"Overbought"` |
| SignalType | `r.Status.ToString()` → `"EodConfirm"` / `"Confirmed"` |
| Rsi | `r.Rsi` (rounded to 2dp) |
| Price | `r.CurrentPrice` |
| TriggerDetails | `r.TriggerDetails` (algorithm narrative) |
| SignalDate | Today's ET date (`yyyy-MM-dd`) |
| RecordedAt | `r.ScannedAt` (UTC timestamp) |
| RuleVersion | `r.LogicMode` → `"Legacy"` / `"Enhanced"` |
| SignalState | `"Active"` (default; trader updates manually) |
| Sector | `r.Sector` |
| ReversalProbability | `r.ReversalProbability` → `"High"` / `"Medium"` / `"Low"` |
| VolumeSignal | `r.VolumeSignal` → `"Validated"` / `"Neutral"` / `"Low-Volume Trap"` |

**Duplicate guard:** Before inserting, existing `Symbol + SignalDate` pairs for today are queried. Only new combinations are inserted — the same signal is never double-counted if the background loop fires multiple times during the window.

### 5. JSON Dual-Write (Morning Panel)

`SaveAsync` also writes a JSON file (`eod-signal-history.json`) to disk for the legacy "Yesterday's EOD" morning panel. The file holds the latest day's signals only and is overwritten on each save.

### 6. Email Notification

EOD CONFIRM signals (not Confirmed) trigger a separate high-priority email via `EmailNotificationService.NotifyNewEodConfirmedSignalsAsync`. Recipients are configured on the Configuration screen.

---

## Database Schema

**Table:** `DailySignals` (created by migration `20260624000000_AddDailySignals`)

| Column | Type | Notes |
|--------|------|-------|
| Id | int PK (identity) | Auto-increment |
| Symbol | nvarchar(20) | Ticker e.g. "TD.TO" |
| CompanyName | nvarchar(200) | |
| ScanType | nvarchar(20) | `Oversold` or `Overbought` |
| SignalType | nvarchar(30) | `EodConfirm`, `Confirmed`, or `EarlyWarning` |
| Rsi | decimal(7,4) | RSI(14) at time of signal |
| Price | decimal(18,4) | Last price at time of signal |
| TriggerDetails | nvarchar(1000) | Narrative explaining why signal fired |
| SignalDate | nvarchar(10) | ET date `yyyy-MM-dd` — indexed |
| RecordedAt | datetime2 | UTC scan timestamp |
| RuleVersion | nvarchar(20) | `Legacy` or `Enhanced` |
| SignalState | nvarchar(30) | Default `Active`; trader-editable |
| Sector | nvarchar(100) | From sector classification |
| ReversalProbability | nvarchar(20) | `High` / `Medium` / `Low` |
| VolumeSignal | nvarchar(30) | `Validated` / `Neutral` / `Low-Volume Trap` |
| Notes | nvarchar(max) | Nullable; trader-editable free text |
| UpdatedAt | datetime2 | Nullable; set on PATCH operations |

**Indexes:**
- `IX_DailySignals_Symbol`
- `IX_DailySignals_SignalDate`
- `IX_DailySignals_Symbol_SignalDate` (composite, for the most common filter combination)

**Migration applied:** `db.Database.MigrateAsync()` runs on **every startup** (not just Development), so the table is created automatically the first time the backend starts after the migration was added.

---

## Signal Lifecycle States

Traders update `SignalState` manually via the dropdown in the EOD Signals grid:

| State | Meaning |
|-------|---------|
| `Active` | Signal recorded; outcome not yet known |
| `FollowThrough` | Price moved in the expected direction after the signal |
| `Invalidated` | Thesis invalidated — reversal broke a key structural level |
| `Expired` | Signal date passed without follow-through |
| `Reversed` | Price moved in the opposite direction |

---

## API Endpoints

| Method | URL | Description |
|--------|-----|-------------|
| GET | `/api/eod-signals` | Paginated + filtered list |
| GET | `/api/eod-signals/{id}` | Single record |
| GET | `/api/eod-signals/meta` | Filter dropdown options + date range + count |
| PATCH | `/api/eod-signals/{id}/state` | Update `SignalState` |
| PATCH | `/api/eod-signals/{id}/notes` | Update `Notes` |
| DELETE | `/api/eod-signals/{id}` | Delete single record |
| DELETE | `/api/eod-signals?confirm=true` | Bulk delete (with optional ticker/date filters) |
| POST | `/api/eod-signals/seed` | Insert 15 test records (duplicate-safe) |

**Query params for GET /api/eod-signals:** `ticker`, `scanType`, `signalType`, `signalState`, `ruleVersion`, `dateFrom` (yyyy-MM-dd), `dateTo` (yyyy-MM-dd), `page` (1-based), `pageSize` (max 200)

---

## Frontend — EOD Signals Page (`/eod-signals`)

### Filters
- **Ticker** — text input, debounced 400 ms, auto-populated from RSI Scanner "View History" links
- **Scan Type** — Oversold / Overbought
- **Signal Type** — EodConfirm / Confirmed / EarlyWarning
- **Signal State** — Active / FollowThrough / Invalidated / Expired / Reversed
- **Rule Version** — Legacy / Enhanced
- **Date From / Date To** — Material Datepicker, sends `yyyy-MM-dd` to API

### Table Columns (client-side sortable on current page)
`Date · Ticker · Scan · Signal · RSI · Price · Reversal · Volume · Mode · State · Actions`

### Per-Row Actions
- **Info tooltip** — shows `TriggerDetails` (the algorithm narrative)
- **Open in Scanner** — navigates to `/scanner` for live view of the ticker
- **Delete** — confirms via dialog, removes from DB

### Header Actions
- **Refresh** — re-fetches current filters
- **Seed (beaker icon)** — calls `POST /api/eod-signals/seed`, inserts 15 test records
- **Delete All** — bulk-deletes all visible records (with count + confirmation dialog)

### Inline State Edit
The `SignalState` column renders as a `mat-select` dropdown. Changing the value calls `PATCH /api/eod-signals/{id}/state` immediately.

---

## Configuration Screen Integration

The **EOD Confirmation Window** card on `/config` allows changing:
- Window start time (ET, 30-minute steps, default 3:30 PM)
- Window end time (ET, 30-minute steps, default 4:00 PM / 5:00 PM shown = extended)
- Enable/disable toggle

Changes take effect **immediately** (the singleton `ScannerRuntimeConfig` is updated via `PUT /api/scanner/eod-settings`) — no backend restart required.

**Note:** The manual "Persist to EOD Dashboard" button has been removed. Persistence is fully automatic during the EOD window.

---

## Testing EOD Signals

### Step 1 — Restart backend to apply migration
```
start-all.bat
```
The DailySignals table is created on first startup via `db.Database.MigrateAsync()`.

### Step 2 — Seed test records
Navigate to `/eod-signals` and click the **beaker (science) icon** in the page header.  
Or call directly: `POST http://localhost:5000/api/eod-signals/seed`  
Returns: `{ "seeded": 15, "skipped": 0 }` on first call.

### Step 3 — Test filters and sort
- Filter by Ticker → type "TD"
- Filter by Scan Type → "Oversold"
- Filter by Signal State → "Active"
- Click column headers to sort (toggles asc/desc)
- Open datepicker for date range

### Step 4 — Test state changes
Click the State dropdown in any row → select "FollowThrough" → observe table refresh.

### Step 5 — Test delete
- Click the delete (trash) icon on any row → confirm dialog → row removed
- Click "Delete All" in header → confirm → all visible records deleted

### Step 6 — Test real automatic persistence
Set the EOD window start/end time on `/config` to a window that includes the current time (e.g. start = "now" − 5 min, end = "now" + 10 min), then wait for the background scanner to fire. If any RSI scan produces `EodConfirm` or `Confirmed` signals, they will appear in the EOD Signals page automatically.

### Step 7 — View history from RSI Scanner
Click the **timeline icon** next to any signal row in the RSI Scanner → navigates to `/eod-signals?ticker={symbol}` with the ticker pre-filled.

---

## Files Changed in This Session

### Backend
| File | Change |
|------|--------|
| `Data/Migrations/20260624000000_AddDailySignals.cs` | NEW migration (Up/Down) |
| `Data/Migrations/20260624000000_AddDailySignals.Designer.cs` | NEW Designer file — **required for EF Core migration discovery** |
| `Data/Migrations/AppDbContextModelSnapshot.cs` | Updated with DailySignal entity |
| `Data/AppDbContext.cs` | Added `DailySignals` DbSet + entity configuration |
| `Models/ScannerModels.cs` | Added `DailySignal`, `EodSignalQueryParams`, `UpdateSignalStateRequest`, `UpdateSignalNotesRequest`, `DailySignalPagedResponse` |
| `Services/EodSignalPersistenceService.cs` | Added DB dual-write; maps ALL RsiScanResult fields |
| `Services/RsiAlertBackgroundService.cs` | Now persists BOTH `EodConfirm` AND `Confirmed` signals during EOD window (from both chains) |
| `Controllers/EodSignalsController.cs` | Full CRUD + meta + seed; seed has no environment guard |
| `Program.cs` | `MigrateAsync()` now runs on ALL environments (not just Development) |

### Frontend
| File | Change |
|------|--------|
| `core/models/portfolio.models.ts` | Added `DailySignal`, `DailySignalPagedResponse`, `EodSignalsMeta`, `EodSignalFilters`, `SignalState` |
| `core/services/portfolio-api.service.ts` | Added EOD signal API methods; removed `persistEodSignalsNow` |
| `app/app.routes.ts` | Lazy route `/eod-signals` |
| `shared/layout/layout.component.ts` | Nav link added |
| `features/eod-signals/eod-signals-page.component.ts` | Full implementation with signals, sort, delete, seed, datepicker |
| `features/eod-signals/eod-signals-page.component.html` | Full-width responsive template |
| `features/eod-signals/eod-signals-page.component.scss` | Scanner-style full-width styles |
| `features/eod-signals/eod-signals.routes.ts` | Lazy route definition |
| `features/scanner/rsi-scanner-table.component.*` | "View Signal History" button per row |
| `features/config/config-page.component.ts` | Removed `persistingNow` / `persistNow` |
| `features/config/config-page.component.html` | Removed "Persist to EOD Dashboard" button |
