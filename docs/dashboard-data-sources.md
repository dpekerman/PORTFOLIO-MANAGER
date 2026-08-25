# Dashboard Data Sources Reference

Generated: 2026-08-25 · Version: develop

---

## Overview

The Dashboard is a **persisted snapshot** updated on demand.
A `DashboardSnapshot` row is written to the database for each authenticated user
whenever a live data source is refreshed (portfolio quotes, watchlist quotes, RSI scan)
or when the user clicks **Refresh** on the Dashboard page.
All on-screen values come from the stored snapshot — navigating to the Dashboard
**never** triggers a Yahoo Finance call.

---

## 1. Portfolio Value

**Card: "Portfolio value"**

| Field        | Formula                                                                          | Source                          |
| ------------ | -------------------------------------------------------------------------------- | ------------------------------- |
| `totalValue` | Sum of all open-position market values in the latest `PortfolioValueHistory` row | `PortfolioValueHistories` table |
| `updatedAt`  | UTC timestamp of when the snapshot was rebuilt                                   | `DashboardSnapshots.UpdatedAt`  |

`PortfolioValueHistories` is populated by the EOD background service
(`PortfolioValueEodBackgroundService`) every trading day at 4:30 PM ET, and also
by the manual `POST /api/portfoliovaluehistory/record-now` endpoint.

---

## 2. Today / This Week / This Month Change

**Cards: "Today", "This week", "This month"**

All three cards show `change ($)` and `change (%)` relative to a baseline row
from `PortfolioValueHistories` (ordered ascending by `RecordedDate`).

| Card       | Baseline row                                              | Formula                             |
| ---------- | --------------------------------------------------------- | ----------------------------------- |
| Today      | Second-to-last row in the table                           | `latestValue − prev1Value`          |
| This week  | Last row recorded **before** Monday 00:00 ET              | `latestValue − mondayCloseValue`    |
| This month | Last row recorded **before** the 1st of the current month | `latestValue − prevMonthCloseValue` |

If there is no prior-month record the next available row is used as fallback.

**Why the baseline is "before" the period start, not "on" it:**
EOD records represent end-of-day closing values.
The week/month P&L is therefore:
`today's close  −  last close before the period began`.

**Percentage formula:**
`(change / baselineValue) × 100`, rounded to 2 decimal places.
Returns `0` when the baseline value is zero or missing.

---

## 3. Portfolio Value History Chart

**Panel: "Portfolio value history"**

Raw data: all rows from `PortfolioValueHistories`, ordered ascending by `RecordedDate`,
limited to the last 365 rows per rebuild.

The frontend filters this list by the selected time range (`1M`, `3M`, `6M`, `YTD`, `1Y`, `ALL`).

**Y-axis normalization:**
The SVG chart uses `(value − min) / (max − min)` within the visible window, with
10 % vertical padding so the line never touches the edge. This is why the chart shows
meaningful variation even when daily moves are small relative to total portfolio size.

**Chart type:** Smooth cubic-bezier area chart; the dashed reference line marks
the **first value** in the selected window (start-of-period baseline).

---

## 4. Top Movers / Bottom Movers

**Panel: "Top movers"**

Source: the persisted `PortfolioSnapshot` and `WatchlistSnapshot` for the current user.
Each snapshot contains the last live quote returned by `GET /api/stocks/quotes` or
`GET /api/watchlist` respectively.

**Selection logic:**

1. Merge portfolio items (with live quotes) and watchlist items (with live quotes).
2. De-duplicate by symbol — portfolio membership takes precedence.
3. Sort descending by `quote.changePercent`.
4. **Top 3** = highest positive movers.
5. **Bottom 3** = lowest (most negative) movers.

`changePercent` is Yahoo Finance day-over-day percentage: `(close − previousClose) / previousClose × 100`.

---

## 5. Market Indices

**Panel: "Market indices"**

Source: live Yahoo Finance quote call during **explicit Dashboard rebuild only**
(not on navigation).

| Symbol  | Label      |
| ------- | ---------- |
| `^DJI`  | Dow Jones  |
| `^NDX`  | Nasdaq 100 |
| `^GSPC` | S&P 500    |

Fields displayed: `price`, `change`, `changePercent` (day-over-day, same session).
Indices are refreshed by `IMarketDataProvider.GetBatchQuotesAsync()` and stored
inside the `DashboardSnapshot.SnapshotJson` blob.

---

## 6. Active RSI Signals

**Panel: "Active RSI signals"**

Source: `RsiScanSnapshot` table (single global row, Id = 1).
Populated by `ScannerController.GetRsiScan()` after every live scan.

| Counter           | Definition                                                      |
| ----------------- | --------------------------------------------------------------- |
| `oversoldCount`   | Number of results in `OversoldChain` where `Status ≠ Neutral`   |
| `overboughtCount` | Number of results in `OverboughtChain` where `Status ≠ Neutral` |

This includes `Confirmed`, `EodConfirm`, and `EarlyWarning` statuses.
Neutral-only signals (RSI recovered, still tracked) are **excluded**.

---

## 7. Allocation by Sector

**Panel: "Allocation by sector"**

Source: `PortfolioSnapshot` for the current user (same snapshot used by Movers).

**Computation:**

```
sectorValue  = sum(quote.currentPrice × item.shares)  for each position in a sector
allocationPct = sectorValue / totalPortfolioValue × 100
```

- Manual positions use `manualMarketValue` instead of live price.
- Positions with no sector assigned are grouped under `"Unclassified"`.
- Sorted descending by value; displayed with a proportional horizontal bar.

---

## 8. Upcoming Earnings (next 7 days)

**Panel: "Upcoming earnings"**

Source: two-tier with override semantics:

| Priority    | Source                                                           | When used                                                     |
| ----------- | ---------------------------------------------------------------- | ------------------------------------------------------------- |
| 1 (highest) | `WatchlistItem.EarningsDate` (manually set, `Source = "Manual"`) | When the user has explicitly set a date for a symbol          |
| 2           | Yahoo Finance `calendarEvents.earnings.earningsDate`             | Fetched during Dashboard rebuild; shown as `Source = "Yahoo"` |

Only symbols from the **portfolio** and **watchlist** are queried.
Results are filtered to: `today ≤ earningsDate ≤ today + 7 days` (ET date).

To bulk-refresh earnings dates from Yahoo Finance without rebuilding the whole Dashboard,
use the **calendar button** in the Watchlist toolbar
(`POST /api/watchlist/refresh-earnings`).

---

## Refresh Triggers

| Event                                      | What rebuilds                                                   |
| ------------------------------------------ | --------------------------------------------------------------- |
| User clicks **Refresh** on Dashboard       | Full Dashboard rebuild (market indices + earnings fetched live) |
| `GET /api/stocks/quotes` succeeds          | Dashboard rebuilt for the requesting user                       |
| `GET /api/watchlist` succeeds              | Dashboard rebuilt for the requesting user                       |
| `GET /api/scanner/rsi` succeeds (non-demo) | Dashboard rebuilt for the requesting user                       |
| `PATCH /api/watchlist/{id}/earnings-date`  | Dashboard rebuilt for the requesting user                       |
| `POST /api/watchlist/refresh-earnings`     | Dashboard rebuilt for the requesting user                       |

The Dashboard is **never** rebuilt on navigation — loading the page always reads
the existing snapshot row without any Yahoo Finance call.

---

## Database Tables Used

| Table                     | Role                                                    |
| ------------------------- | ------------------------------------------------------- |
| `DashboardSnapshots`      | Persisted JSON snapshot per user (`UserId` PK)          |
| `PortfolioValueHistories` | EOD total values used for chart and period calculations |
| `PortfolioSnapshots`      | Last portfolio quotes — used for movers and allocation  |
| `WatchlistSnapshots`      | Last watchlist quotes — used for movers                 |
| `RsiScanSnapshots`        | Last RSI scan result — used for signal counts           |
| `WatchlistItems`          | `EarningsDate` column — manual earnings override        |

---

## Azure Deployment Checklist

1. Run `dotnet ef database update` against Azure SQL to apply migrations:
   - `AddDashboardSnapshot`
   - `AddWatchlistEarningsDate`
   - `AddEodPositionSizing`
2. Or execute the numbered scripts in order: `16_`, `17_`, `18_`.
3. Run `scripts/migrate-full.ps1` to copy data (includes `DashboardSnapshots`).
4. Idempotent EF script: `scripts/portfolio-manager-deploy.sql`.
5. First Dashboard load after deploy will show the migrated snapshot;
   click **Refresh** for fresh market indices and earnings.
