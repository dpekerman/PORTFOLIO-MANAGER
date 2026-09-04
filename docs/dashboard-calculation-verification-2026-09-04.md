# Dashboard Calculation Verification

**Date:** 2026-09-04  
**Branch:** `develop`  
**Scope:** Dashboard summary cards, EOD history, component changes, allocation, movers, RSI counters, earnings, and the related fixes.

## Executive conclusion

The reported period-value problem had a real code defect. Week and month baselines were previously allowed to fall back to an in-period snapshot when no snapshot existed before the period boundary. That can produce a misleading near-zero or otherwise incorrect value, especially around weekends and market holidays.

That fallback has been removed. The current code now uses only a snapshot strictly before the period start. If no such snapshot exists, the period change is `0` and the corresponding percentage is `0`; this is a data-availability state, not proof that the portfolio had no change.

The fix is implemented and the backend source has no C# compiler diagnostics when checked independently of the running executable. A full normal build was blocked because the currently running `PortfolioManager.Api.exe` locked the output file. Production data verification and deployment are still outstanding.

## Completed implementation

- [x] Week baseline fallback removed from `DashboardService`.
- [x] Month baseline fallback removed from `DashboardService`.
- [x] Component-change validation added with a `$0.01` tolerance.
- [x] `ILogger<DashboardService>` injected and mismatch warnings added.
- [x] Warnings added when manual positions fall back to stale cost basis.
- [x] Dashboard mover comments corrected from top/bottom 10 to top/bottom 50.
- [x] This report created.

## Not completed or not verifiable from this workspace

- [ ] Unit tests specifically covering week/month boundary and holiday cases. The existing test project has no focused `DashboardService` period-calculation tests.
- [ ] Live SQL verification of the account's `PortfolioValueHistories` rows. The local workspace does not provide a verified production database query result.
- [ ] Reconciliation against the stakeholder's displayed `$4,689` and `$104` values. This requires the exact snapshot rows, current holdings, cash, options, and timestamps used for that dashboard response.
- [ ] Production deployment and post-deployment monitoring. No deployment was performed.
- [ ] A user-facing `N/A` state for a missing baseline. Current API behavior returns numeric zero because the response model uses decimal fields.

## Exact current formulas

### Live portfolio total

The dashboard rebuild calculates:

```text
liveStocksValue  = sum(open portfolio positions)
liveCashValue    = sum(CashItems.Amount)
liveOptionsValue = sum(open OptionItems.MarketPrice * NumberOfContracts * 100)
liveTotal        = liveStocksValue + liveCashValue + liveOptionsValue
```

Closed portfolio items and closed option items are excluded.

For a non-manual stock position:

```text
positionValue = (quote.CurrentPrice, or AverageCostBasis if quote is missing) * Shares
```

For a manual position, the current code uses `ManualMarketValue` when present and otherwise uses `AverageCostBasis` as a fallback. The EOD/history paths log a warning when that fallback occurs. This fallback is potentially stale and must be reviewed in the data-entry workflow.

### Today

The service loads up to 365 history rows and orders them by `RecordedDate`.

- If the newest row is for the current ET date, the prior row is used as the baseline.
- Otherwise, the newest row is used as the baseline.
- If no baseline exists, today's change is zero.

```text
TodayChange = liveTotal - baseline.TotalValue
TodayChangePercent = round(TodayChange / baseline.TotalValue * 100, 2)
```

The percentage is `0` when the baseline is missing or not positive.

### Today component changes

When a baseline exists, each component is calculated independently:

```text
TodayStocksChange  = liveStocksValue  - baseline.StocksValue
TodayCashChange    = liveCashValue    - baseline.CashValue
TodayOptionsChange = liveOptionsValue - baseline.OptionsValue
```

The service now validates:

```text
TodayStocksChange + TodayCashChange + TodayOptionsChange
    ~= TodayChange
```

A warning is logged when the absolute difference exceeds `$0.01`. This detects inconsistent historical component values or a future change to the total formula; it does not alter the displayed values.

### This week

The week starts on Monday in Eastern Time. The baseline is the last history row where:

```text
RecordedDate < Monday-of-current-week
```

The calculation is:

```text
WeekChange = liveTotal - weekBaseline.TotalValue
WeekChangePercent = round(WeekChange / weekBaseline.TotalValue * 100, 2)
```

There is deliberately no fallback to a row on or after Monday. Therefore, if there is no earlier row, the API returns numeric zero for both fields. A market holiday on Monday is expected to use the prior trading day's close, provided that row exists.

### This month

The month baseline is the last history row where:

```text
RecordedDate < first-day-of-current-month
```

The calculation is:

```text
MonthChange = liveTotal - monthBaseline.TotalValue
MonthChangePercent = round(MonthChange / monthBaseline.TotalValue * 100, 2)
```

There is deliberately no fallback to a row within the current month.

## EOD snapshot creation

`PortfolioValueEodBackgroundService` attempts to write one row at 4:30 PM ET on weekdays in production. It skips a date if `ExistsForDateAsync` finds any history row for that date.

The persisted row contains:

```text
StocksValue  + CashValue + OptionsValue = TotalValue
```

The development environment bypasses the production time window, so local development can create a snapshot at any time. `PortfolioValueHistoryService.RecordCurrentValueAsync` also overwrites all existing rows for the current ET date before inserting a new one.

Backfill skips weekends and missing market data. It uses historical closing prices for non-manual positions, current cash, and current option market prices. This means a backfilled historical total can contain present-day cash/options values rather than historically reconstructed cash/options values; that is a data-quality limitation to address separately.

## Other dashboard numbers

### Movers

Portfolio and watchlist snapshot rows are merged and grouped by symbol. Portfolio membership is retained when a symbol appears in both collections. Results are sorted by quote day-change percentage. The response contains the top 50 and bottom 50; the frontend may display fewer.

```text
QuoteChangePercent = (CurrentPrice - PreviousClose) / PreviousClose * 100
```

### Sector allocation

Active quoted positions are grouped by sector; blank sectors become `Unclassified`.

```text
SectorValue = sum(CurrentPrice * Shares)
SectorPercent = SectorValue / StocksValue * 100
```

Cash is added separately with:

```text
CashPercent = CashValue / (StocksValue + CashValue) * 100
```

Options are not included in the sector denominator.

### Role allocation

Active quoted stock positions are grouped by holding role and use the live total as denominator:

```text
RolePercent = RoleStockValue / liveTotal * 100
```

Open option value is merged into the `Options` role. The live total includes stocks, cash, and options.

### RSI counters

The summary counters count distinct symbols in each scanner chain whose status is not `Neutral`. The persisted dashboard response is normalized again on read to de-duplicate signal rows and recalculate those counts.

### Earnings

Manual watchlist earnings dates override Yahoo dates for that symbol. Remaining dates come from Yahoo. Only portfolio/watchlist symbols with dates from today through seven ET calendar days ahead are returned.

## Why `$104` could appear

The former baseline logic effectively allowed this shape:

```text
baseline = last row before period start
        ?? first row on/after period start
```

When the first branch was absent, the second branch could select an in-period or current row. Subtracting that row from the live value can make the period card look artificially small or otherwise unrelated to the prior close. This is especially easy to trigger when the period begins on a weekend or market holiday and the history table has gaps.

The current code removes the second branch. To determine whether the stakeholder's `$4,689` is correct, compare the dashboard response with these exact records:

```sql
SELECT RecordedDate, RecordedAt, TotalValue, StocksValue, CashValue, OptionsValue
FROM PortfolioValueHistories
WHERE RecordedDate <= '2026-09-04'
ORDER BY RecordedDate DESC;
```

The query must be run against the same account/database used by the dashboard. The relevant week baseline for 2026-09-04 is the last row before Monday 2026-08-31; the month baseline is the last row before 2026-09-01.

## Recommended next changes

1. Add focused service tests for: valid prior Friday baseline, Monday holiday with prior Friday baseline, missing baseline, month boundary, and current-day EOD row handling.
2. Change the API contract to represent missing period baselines explicitly (`null` plus a baseline-available flag, or a documented `N/A` status) instead of conflating missing data with a genuine zero change.
3. Reconcile manual-position semantics. Confirm whether `ManualMarketValue` is a total position value or a per-share value, then apply that meaning consistently in dashboard, EOD, backfill, and allocation calculations.
4. Reconstruct historical cash and options values when backfilling, or label backfilled rows as estimates so they are not treated as exact closes.
5. Scope history reads and existence checks by account if the database model supports multiple users. The current dashboard history query reads the available history rows without a user predicate, so account isolation must be verified against the schema and deployment data.
6. After deploying, monitor the new component-mismatch and manual-fallback warnings and compare one dashboard response with the SQL baseline rows.

## Verification record

- Source review completed: `DashboardService`, `PortfolioValueEodBackgroundService`, `PortfolioValueHistoryService`, and `DashboardModels`.
- Normal `dotnet build` attempted but output copying was blocked by a running `PortfolioManager.Api.exe` process.
- `dotnet build --no-restore -p:UseAppHost=false` succeeded; the only warning was the locked executable cleanup.
- The existing test project passed: **176 tests passed, 0 failed, 0 skipped**. It does not yet contain focused `DashboardService` period-boundary tests.
- A language-service check produced no errors in the edited C# files.
- No production database query or deployment was performed in this session.
