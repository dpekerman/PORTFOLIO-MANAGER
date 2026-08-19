# Portfolio History Backfill — Implementation Report

**Date:** 2026-08-19  
**Branch:** develop  
**Scope:** Root cause investigation of incorrect 1-Day Change + full backfill system

---

## 1. Root Cause: Why 1-Day Change Was Wrong

The EOD background service (`PortfolioValueEodBackgroundService`) saves a portfolio value snapshot every weekday at **4:30 PM ET**. It only fires if the backend is running at that time.

The database showed the last snapshot was **2026-08-14 (Friday)**. Snapshots for **Mon Aug 17** and **Tue Aug 18** were missing because the backend was not running at 4:30 PM ET on those days.

The summary bar component logic:

```
previousDayValue = most recent history record that is NOT today
1-Day Change     = currentLiveValue − previousDayValue
```

With Aug 14 as the most recent record, the "1-Day Change" was actually computing a **5-calendar-day change**, not a 1-day change.

---

## 2. Files Changed

### Backend

| File                                             | Change                                                                                                          |
| ------------------------------------------------ | --------------------------------------------------------------------------------------------------------------- |
| `Services/YahooFinanceService.cs`                | Added `GetHistoricalClosingPricesAsync` to interface + implementation                                           |
| `Services/PortfolioValueHistoryService.cs`       | Added `BackfillMissingAsync`, `GetMissingDatesAsync`, `BackfillDateAsync`, `TryGetEasternTz` + logger injection |
| `Controllers/PortfolioValueHistoryController.cs` | Added `GET /missing-days` and `POST /backfill` endpoints                                                        |

### Frontend

| File                                                                          | Change                                                                                                                    |
| ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `core/services/portfolio-api.service.ts`                                      | Added `backfillMissingHistory()` and `getMissingHistoryDays()`                                                            |
| `features/portfolio/portfolio-summary-bar/portfolio-summary-bar.component.ts` | Auto-trigger silent backfill on load when gap detected; refactored history load into `loadHistory()` / `setPreviousDay()` |
| `features/portfolio/portfolio-summary-bar/portfolio-summary-bar.component.ts` | Added `PortfolioValueHistoryDto` import                                                                                   |
| `features/config/config-page.component.ts`                                    | Added Portfolio History section: signals, `scanMissingDays()`, `reconstructMissingDays()`, `DecimalPipe` import           |
| `features/config/config-page.component.html`                                  | Added "Portfolio History" tab button + full section panel (scan → preview → reconstruct → results)                        |
| `features/config/config-page.component.scss`                                  | Added styles: `.history-result-card`, `.history-date-chip`, `.history-filled-row`, `.history-value`, `.history-note`      |

---

## 3. New API Endpoints

All endpoints require `[Authorize(Roles = "Admin")]`.

### `GET /api/portfoliovaluehistory/missing-days?lookbackDays=30`

Returns a list of weekday dates (past N days) with no snapshot. **Read-only — no DB writes.**

```json
["2026-08-18", "2026-08-15"]
```

### `POST /api/portfoliovaluehistory/backfill?lookbackDays=14`

For each missing weekday date:

1. Identifies positions open on that date (current OPEN positions where `OpenDate <= targetDate`, plus CLOSE positions where `CloseDate > targetDate`)
2. Fetches historical closing prices from Yahoo Finance `v8/finance/chart` (150 ms throttle per symbol, TSX `.TO` fallback)
3. Computes: `stocks (historical prices × shares) + cash (current) + options (current MarketPrice)`
4. Saves record with `RecordedAt = targetDate 20:30 UTC` (≈ 4:30 PM EDT)
5. Skips date if Yahoo returns no data (market was closed / holiday)

Returns an array of `PortfolioValueHistoryDto` for every newly created record.

> **Accuracy note:** Cash balances and option market prices use current values as an approximation since point-in-time snapshots do not exist. Stock prices are accurate historical closes from Yahoo Finance.

---

## 4. Auto-Heal on Portfolio Page Load

The portfolio summary bar now self-heals silently:

```
1. Load last 2 history records
2. If most recent record < last trading day:
     → Call POST /backfill (silent, no spinner)
     → On success: reload history and update previousDayValue
     → On error (403, network): fall back to existing record
3. Set 1-Day Change = currentValue − previousDayValue
```

This means after a backend outage, the first page load after recovery will automatically backfill and display a correct 1-Day Change with no manual action required, provided the user has the Admin role.

---

## 5. Config Page — Portfolio History Tab

Location: **Settings → Portfolio History** (Admin-only tab)

### Flow

```
[Scan for Missing Days]
        ↓
  List of missing dates shown as red chips
        ↓
[Reconstruct N Day(s)]
        ↓
  Yahoo Finance API called per symbol per missing day
        ↓
  Results shown as green chips with portfolio total value
  Snackbar: "N day(s) reconstructed successfully"
```

---

## 6. How to Test

### Prerequisites

- Logged in as an **Admin** user
- Backend running on `localhost:5000`
- Frontend running on `localhost:4200`

---

### Test 1 — Verify 1-Day Change is now correct

1. Open `http://localhost:4200` and navigate to the **Portfolio** page
2. The summary bar at the top shows **1-Day Change**
3. Check the DB to confirm today's previous-day snapshot exists:
   ```sql
   SELECT TOP 5 RecordedDate, TotalValue, RecordedAt
   FROM PortfolioValueHistories
   ORDER BY RecordedAt DESC
   ```
4. The change should now reflect `currentValue - yesterdayEOD`, not a stale value from days ago

---

### Test 2 — Scan for Missing Days (Config Page)

1. Navigate to **Settings** → click the **Portfolio History** tab
2. Click **"Scan for Missing Days"**
3. Expected results:
   - If history is complete: green banner "No missing snapshots — history is complete"
   - If gaps exist: red chips showing each missing date (e.g. `2026-08-18`, `2026-08-17`)
4. Confirm no DB changes occurred (scan is read-only):
   ```sql
   SELECT COUNT(*) FROM PortfolioValueHistories  -- count should be unchanged
   ```

---

### Test 3 — Reconstruct Missing Days

1. After a scan that shows missing dates, click **"Reconstruct N Day(s)"**
2. A spinner appears while Yahoo Finance historical prices are fetched
3. On completion:
   - Green chips appear for each reconstructed date with the computed portfolio total
   - Snackbar confirms: `"N day(s) reconstructed successfully"`
   - The missing days chips (red) disappear
4. Verify in DB:
   ```sql
   SELECT RecordedDate, TotalValue, StocksValue, CashValue, OptionsValue, RecordedAt
   FROM PortfolioValueHistories
   ORDER BY RecordedAt DESC
   ```
   New rows should appear for the previously missing dates
5. Reload the Portfolio page — the 1-Day Change should now be accurate

---

### Test 4 — Market Holiday / No Data

1. If a "missing day" is a public holiday (e.g. Labour Day), Yahoo Finance returns no candles
2. The backfill service detects `prices.Count == 0` and skips that date
3. The date will remain in "missing days" after backfill, but no broken record is created
4. Verify: the reconstructed results panel shows fewer rows than missing days

---

### Test 5 — Auto-Heal on Page Load

1. Manually delete the most recent 2 history records to simulate an outage:
   ```sql
   DELETE TOP(2) FROM PortfolioValueHistories
   -- Note the RecordedDate values first so you can verify they come back
   ```
2. Reload the Portfolio page
3. Watch the backend log for:
   ```
   [PortfolioValueHistory] Backfilling 2026-08-18
   [PortfolioValueHistory] Backfilling 2026-08-19
   ```
4. The 1-Day Change card shows a loading spinner, then updates automatically
5. Confirm records are restored in DB

---

### Test 6 — Non-Admin User (403 Graceful Fallback)

1. Log in as a non-Admin user (Trader or Viewer role)
2. Navigate to Portfolio page — the auto-backfill call will receive a 403
3. Expected: no crash, 1-Day Change still displays using the most recent available snapshot
4. The **Portfolio History** tab should not be visible in Settings

---

## 7. Known Limitations

| Limitation                      | Detail                                                                                                                         |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Cash is approximate             | Current cash balance used for historical snapshots (no audit trail)                                                            |
| Options are approximate         | Current `MarketPrice` used; actual option prices on past dates are unavailable                                                 |
| Positions opened/closed mid-gap | If a position was opened after a gap date, it is correctly excluded. Positions closed after a gap date are correctly included. |
| Rate limiting                   | 150 ms delay per symbol. A portfolio with 30+ symbols will take ~5–10 s per missing day                                        |
| Lookback window                 | Default 30 days. Increase `lookbackDays` query param up to 90 if needed                                                        |
