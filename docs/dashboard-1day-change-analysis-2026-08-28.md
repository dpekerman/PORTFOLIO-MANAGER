# Dashboard – 1 Day Change: Calculation Analysis & Bug Report

**Date:** 2026-08-28

---

## How 1 Day Change Is Calculated

### Data Source: `PortfolioValueHistories` table

Each trading day, the EOD background service (`PortfolioValueEodBackgroundService`) fires at **4:30 PM ET** and records:

| Field          | Source                                                                    |
| -------------- | ------------------------------------------------------------------------- |
| `StocksValue`  | Live Yahoo Finance prices × shares for every open position                |
| `CashValue`    | `SUM(CashItems.Amount)` at time of snapshot                               |
| `OptionsValue` | `SUM(OptionItems.MarketPrice × NumberOfContracts × 100)` for open options |
| `TotalValue`   | `StocksValue + CashValue + OptionsValue`                                  |
| `RecordedDate` | Eastern Time date string (`yyyy-MM-dd`)                                   |

### Dashboard Rebuild Logic (`DashboardService.RebuildAsync`)

When the user clicks **Refresh**, `DataRefreshService` fetches live Yahoo quotes, persists a fresh `PortfolioSnapshot`, and then the dashboard is rebuilt:

```
liveStocksValue  = Σ (Quote.CurrentPrice × Shares) for all open non-manual positions
                 + ManualMarketValue for manual positions  (from fresh portfolio snapshot)
liveCashValue    = SUM(CashItems.Amount)   [direct DB read, always live]
liveOptionsValue = SUM(MarketPrice × Contracts × 100) for open options [direct DB read]
liveTotal        = liveStocksValue + liveCashValue + liveOptionsValue
```

**Selecting the baseline ("yesterday"):**

```
history = PortfolioValueHistories ORDER BY RecordedDate ASC (last 365 entries)

IF history[-1].RecordedDate == today (ET)
    yesterdayEntry = history[-2]   ← today's EOD already recorded; use the entry before it
ELSE
    yesterdayEntry = history[-1]   ← most recent close (could be Friday on a Monday)
```

**Change computation:**

```
todayChange        = liveTotal         − yesterdayEntry.TotalValue
todayStocksChange  = liveStocksValue   − yesterdayEntry.StocksValue
todayCashChange    = liveCashValue     − yesterdayEntry.CashValue
todayOptionsChange = liveOptionsValue  − yesterdayEntry.OptionsValue

todayChange ≡ todayStocksChange + todayCashChange + todayOptionsChange  (always true)
```

This means: **if Cash and Options have not changed since yesterday's close, `todayChange` will equal `todayStocksChange` exactly.**

---

## Bugs Found & Fixed

### Bug 1 — `RecordCurrentValueAsync` used UTC date (not ET)

**File:** `Services/PortfolioValueHistoryService.cs`

```csharp
// BEFORE (bug): UTC date
var recordedDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

// AFTER (fix): ET date — consistent with EOD service and dashboard logic
var tz = TryGetEasternTz();
var recordedDate = (tz is not null
    ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz)
    : DateTime.UtcNow).ToString("yyyy-MM-dd");
```

**Impact:** The `/api/portfoliovaluehistory/record-now` (Admin-only) endpoint could store a record with tomorrow's UTC date when called between 8 PM–midnight ET. This caused `hasTodayEntry` in the dashboard to be false, making `yesterdayEntry` point at the (incorrectly-dated) today record, and `todayChange` would be near-zero.

The EOD background service was already correct (it has always used ET date).

---

### Bug 2 — No per-component breakdown exposed

**Files:** `Models/DashboardModels.cs`, `Services/DashboardService.cs`, `portfolio.models.ts`, `dashboard-page.component.html/scss`

`DashboardSummary` previously only exposed `TodayChange` and `TodayChangePercent`. There was no way to tell whether the change came from Stocks, Cash, or Options movement.

**Fix:** Added three new fields to `DashboardSummary`:

```csharp
// Backend record (DashboardModels.cs)
public sealed record DashboardSummary(
    decimal TotalValue,
    decimal TodayChange,
    decimal TodayChangePercent,
    decimal TodayStocksChange,   // ← NEW
    decimal TodayCashChange,     // ← NEW
    decimal TodayOptionsChange,  // ← NEW
    ...
);
```

```typescript
// Frontend interface (portfolio.models.ts)
export interface DashboardSummary {
  todayStocksChange: number;   // ← NEW
  todayCashChange: number;     // ← NEW
  todayOptionsChange: number;  // ← NEW
  ...
}
```

The "Today" card in the dashboard metric strip now shows the breakdown:

```
Today
+$3,200  (1.2%)
  Stocks  +$3,200
  Cash    $0        ← hidden when zero
  Options −$150     ← hidden when zero
```

Cash and Options rows are hidden when they are $0 so the card stays compact on unchanged days.

---

## Known Limitation: Backfilled Records Use Current Cash/Options

When the **Backfill** endpoint fills missing historical dates, it fetches historical stock prices from Yahoo but reads **today's** `CashItems` and `OptionItems` values (because we don't snapshot those).

This means backfilled records show:

- `StocksValue` — accurate for that historical date
- `CashValue` / `OptionsValue` — today's values, NOT the actual historical values

**Effect on 1 Day Change:** Only affects the 1 Day Change if the _baseline record_ (`yesterdayEntry`) was backfilled AND cash/options were different on that date versus today. In practice this is rare (cash/options change infrequently) and the Stocks component of the change will always be accurate.

**Effect on Week/Month Change:** Same limitation — if the week or month baseline was backfilled, the Cash/Options delta will be slightly off. The Stocks component is always correct.

This is a structural limitation (no cash/options history table) and does not require an immediate fix.

---

## Summary of All Dashboard Calculations

### Hero Section

| Value            | Formula                                              |
| ---------------- | ---------------------------------------------------- |
| Portfolio Value  | `liveStocksValue + liveCashValue + liveOptionsValue` |
| 1 Day Change ($) | `liveTotal − yesterdayEntry.TotalValue`              |
| 1 Day Change (%) | `todayChange / yesterdayEntry.TotalValue × 100`      |

### Metric Strip

| Value           | Formula                                          |
| --------------- | ------------------------------------------------ |
| Today — Stocks  | `liveStocksValue − yesterdayEntry.StocksValue`   |
| Today — Cash    | `liveCashValue − yesterdayEntry.CashValue`       |
| Today — Options | `liveOptionsValue − yesterdayEntry.OptionsValue` |
| This week ($)   | `liveTotal − weekBase.TotalValue`                |
| This week (%)   | `weekChange / weekBase.TotalValue × 100`         |
| This month ($)  | `liveTotal − monthBase.TotalValue`               |
| This month (%)  | `monthChange / monthBase.TotalValue × 100`       |

**weekBase** = last history entry strictly before the Monday of the current week.  
**monthBase** = last history entry strictly before the 1st of the current month (= prior month's last close).

### Sector Allocation Table

- Denominator = `liveStocksValue` only (no Cash, no Options-role items)
- Cash row added separately using `stocksValue + liveCashValue` as its denominator

### Role Allocation Table

- Denominator = `liveTotal` (Stocks + Cash + Options)
- Options market value merged with any stocks classified under the "Options" holding role

### Top/Bottom Movers

- Sourced from `Quote.ChangePercent` in the portfolio & watchlist snapshots
- Closed positions (`TransactionType = "CLOSE"`) are excluded

### Chart (Value History)

- Points come from `PortfolioValueHistories.TotalValue` ordered by `RecordedDate`
- Range filter (1M / 3M / 6M / YTD / 1Y / ALL) applied in the frontend

---

## Files Changed

| File                                                          | Change                                                                                          |
| ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `backend/…/Models/DashboardModels.cs`                         | Added `TodayStocksChange`, `TodayCashChange`, `TodayOptionsChange` to `DashboardSummary` record |
| `backend/…/Services/DashboardService.cs`                      | Computes three breakdown values; passes them to `DashboardSummary` constructor                  |
| `backend/…/Services/PortfolioValueHistoryService.cs`          | **Bug fix**: `RecordCurrentValueAsync` now uses ET date instead of UTC                          |
| `frontend/…/core/models/portfolio.models.ts`                  | Added three fields to `DashboardSummary` interface                                              |
| `frontend/…/features/dashboard/dashboard-page.component.html` | "Today" card extracted from the `@for` loop; breakdown rows added                               |
| `frontend/…/features/dashboard/dashboard-page.component.scss` | Added `.db-metric-breakdown` and `.db-metric-breakdown-row` styles                              |
