# Portfolio Manager — Dashboard Implementation Report

Generated: 2026-08-25 · Branch: develop

---

## Previous request (Aug 25 — first implementation pass)

### ✅ Dashboard as landing page

- `app.routes.ts`: default redirect changed from `/portfolio` to `/dashboard`.
- `login.component.ts` and `setup.component.ts`: explicit `router.navigate` updated to `/dashboard`.
- Layout navigation: Dashboard entry added first in `navLinks`.

### ✅ Dashboard landing page — core panels

- Persisted `DashboardSnapshots` table (per-user, `UserId` PK, JSON payload).
- `GET /api/dashboard` returns latest snapshot; 204 when none exists.
- `POST /api/dashboard/refresh` rebuilds from live data.
- Snapshot-first load: navigating to Dashboard never calls Yahoo Finance.

### ✅ Portfolio value chart

- Initial bar chart replaced by smooth SVG area/line chart.
- Time range selectors: 1M · 3M · 6M · YTD · 1Y · ALL using `MatButtonToggle`.
- Y-axis normalised to data range so daily variation is visible.

### ✅ Today / This week / This month cards

- Today: `latest − previousClose`.
- This month: `latest − last EOD record before the 1st` (prior-month close).

### ✅ Top movers (3 gainers + 3 losers)

- Source: merged portfolio + watchlist snapshots.
- `isPortfolio` / `isWatchlist` membership flags.

### ✅ Market indices (Dow, Nasdaq, S&P 500)

- Fetched live during explicit rebuild; cached in snapshot JSON.

### ✅ Active RSI signals

- Count sourced from persisted RSI scan snapshot.

### ✅ Allocation by sector

- Actual % calculated from portfolio snapshot (marketValue / totalValue).

### ✅ Upcoming earnings (7-day window)

- Yahoo `calendarEvents` provider with manual Watchlist override.
- Tier 1: manual date (source = "Manual"). Tier 2: Yahoo (source = "Yahoo").

### ✅ Watchlist earnings column

- Replaced manual `<input type="date">` with read-only display.
- Added "Refresh Earnings" calendar icon button → `POST /api/watchlist/refresh-earnings`.
- `EarningsDate` persisted to `WatchlistItems` and included in JSON backup/restore.

### ✅ Stand By/No Add yellow styling

- `finalActionClass()` in `DecisionEngineService` now returns `ma-standby-no-add` (yellow) for the exact phrase "Stand By/No Add".

### ✅ Scanner calculations

- `StochasticD` (3-period SMA of %K).
- RSI bullish/bearish divergence detection (20–60 bar window).
- `BollingerPctB` and `BollingerBandwidth`.
- TSX 13-bucket intraday volume projection.
- EOD position sizing (1% account risk budget, 10% max position).

### ✅ EOD position sizing persistence

- `PositionSizingShares`, `PositionSizingRiskAmount`, `PositionSizingPositionValue`, `PositionSizingLimitingReason` added to `DailySignals`.
- Displayed in EOD Signals table with tooltip showing limiting reason.

### ✅ Azure deployment readiness

- EF migrations: `AddDashboardSnapshot`, `AddWatchlistEarningsDate`, `AddEodPositionSizing`.
- Numbered SQL scripts: `16_`, `17_`, `18_`.
- Idempotent combined script: `scripts/portfolio-manager-deploy.sql`.
- All four data transfer scripts updated to include `DashboardSnapshots`.

---

## This request (Aug 25 — second implementation pass)

### ✅ 1. This-week calculation fix

**Problem:** On Monday the week-start baseline was the same as today, giving zero.
**Fix:** `DashboardService.cs` — week baseline now uses `LastOrDefault(date < weekStart)` to take Friday's closing value, giving the correct Mon–Fri change.

### ✅ 2. Y-axis labels on chart

- SVG chart expanded to `960 × 210` with `padL = 66` for label space.
- 5 evenly-spaced Y-axis labels with horizontal grid lines.
- Labels formatted as `$XXXk` or `$X.XXM`.

### ✅ 3. Market indices: price column + VIX, DXY, GOLD, TSX, OIL

Added to `IndexSymbols`:
| Symbol | Label |
|---|---|
| `^GSPTSE` | TSX Composite |
| `^VIX` | VIX |
| `DX-Y.NYB` | DXY (USD) |
| `GC=F` | Gold |
| `CL=F` | Oil (WTI) |

Market indices panel now shows: Name · Price · Change $ · Change %.
Price formatted as integer (>500) or 2-decimal (<500).

### ✅ 4. Top movers — company name + configurable count

- Snapshot stores **top 10 / bottom 10** movers.
- Company name (`companyName`) shown in a secondary smaller column.
- `mat-select` dropdown in the panel header lets the user choose 3, 5, 7, or 10.
- `moversCount` signal controls `visibleMovers` / `visibleLosers` computed slices.

### ✅ 5. Allocation vs sector targets

**New `DashboardAllocation` fields:** `targetPercent`, `delta`, `status`.

Status logic (tolerance ±2% = green, ±2–5% = yellow, >5% = red):

| Status        | Meaning                     |
| ------------- | --------------------------- |
| `good`        | Within ±2% of target        |
| `watch-over`  | 2–5% above target           |
| `watch-under` | 2–5% below target           |
| `over`        | >5% above target            |
| `under`       | >5% below target            |
| `no-target`   | No sector target configured |

Source: `AllocationSectorTargets` DB table (configured in `/config`).
Displayed as a table: Sector · Actual · Target · Δ · Status badge.

### ✅ 6. Removed 2 bottom quick-actions

Only "Scan now" remains in the nav strip. "Analyze watchlist" and "Add transaction" removed from the dashboard.

### ✅ 7. RSI signals — expanded panel

**Summary row** (always visible): Oversold · Overbought · New Today · Action Required.

**"New Today"** = `DailySignals` rows where `SignalDate = today` (ET).
**"Action Required"** = Confirmed + EodConfirm signals across both chains.

**Expanded detail tables** (toggle via `expand_more` button):

- Oversold Opportunities: Ticker, RSI, Momentum Shift, Volume, Chg%, Action.
- Overbought/Trim Watch: same columns.
- "View all" link when >8 signals.

**Action labels:**
| Condition | Label |
|---|---|
| Confirmed/EodConfirm + Oversold | `BUY WATCH` |
| EarlyWarning + Bull Turn | `WATCH` |
| EarlyWarning + Still Falling | `WAIT` |
| Confirmed/EodConfirm + Overbought | `TRIM WATCH` |
| EarlyWarning + Bear Turn | `REVIEW` |

### ✅ 8. Navigatable panel icons

Every panel's leading icon is now a `mat-icon-button` with `routerLink`:

| Panel                 | Navigates to   |
| --------------------- | -------------- |
| Portfolio value chart | `/portfolio`   |
| Top movers            | `/portfolio`   |
| Market indices        | `/scanner`     |
| RSI signals           | `/eod-signals` |
| Allocation vs targets | `/allocation`  |
| Upcoming earnings     | `/watchlist`   |

---

## Database / migration status

| Migration                  | Description                             | SQL Script                        |
| -------------------------- | --------------------------------------- | --------------------------------- |
| `AddDashboardSnapshot`     | Per-user Dashboard snapshot table       | `16_CreateDashboardSnapshots.sql` |
| `AddWatchlistEarningsDate` | Earnings date column on WatchlistItems  | `17_AddWatchlistEarningsDate.sql` |
| `AddEodPositionSizing`     | Position sizing columns on DailySignals | `18_AddEodPositionSizing.sql`     |

**No new migrations** were added in this request — all changes serialise inside the existing `DashboardSnapshot.SnapshotJson` blob.

**Azure deployment steps:**

1. Apply EF migrations: `dotnet ef database update` against Azure SQL, or run `scripts/portfolio-manager-deploy.sql` (idempotent, covers all migrations).
2. Run `scripts/migrate-full.ps1` to transfer data (includes `DashboardSnapshots`).
3. On first login after deploy, click **Refresh** to rebuild the snapshot with live market indices.

---

## Files changed (this session)

**Backend:**

- `Models/DashboardModels.cs` — Extended `DashboardAllocation`, new `DashboardRsiSignal`, `DashboardRsiSection`, updated `DashboardResponse`.
- `Services/DashboardService.cs` — Week fix, 8 index symbols, top-10 movers, sector-target comparison, RSI detail section.

**Frontend:**

- `core/models/portfolio.models.ts` — Extended `DashboardAllocation`, new `DashboardRsiSignal`, `DashboardRsiSection`, `rsiSection?` on `DashboardResponse`.
- `features/dashboard/dashboard-page.component.ts` — `moversCount` signal, `rsiExpanded` signal, `fmtIdx()`, `allocStatusClass()`, Y-axis labels in `svgChart`, `MatSelectModule`.
- `features/dashboard/dashboard-page.component.html` — Full redesign with all 7 items.
- `features/dashboard/dashboard-page.component.scss` — RSI table, allocation table, navigatable icon buttons, Y-axis, index grid.
