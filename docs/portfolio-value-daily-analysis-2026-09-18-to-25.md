# Portfolio Value Day-by-Day Analysis: Sept 18–25, 2026

Data source: `PortfolioValueHistories` table, `PortfolioManagerLocal` database (queried directly via SQL Server, read-only — no data modified).

## Raw Data

| Date | Day | Total Value | Stocks | Cash | Options | Source | Recorded At (UTC) |
|---|---|---:|---:|---:|---:|---|---|
| 2026-09-18 | Friday | $738,694.21 | $667,533.21 | $45,561.00 | $25,600.00 | CashRecalculation | 2026-09-20 05:39:53 |
| 2026-09-19 | Saturday | — no row (market closed) | | | | | |
| 2026-09-20 | Sunday | — no row (market closed) | | | | | |
| 2026-09-21 | Monday | $764,277.27 | $665,870.27 | $72,807.00 | $25,600.00 | CashRecalculation | 2026-09-21 17:05:10 |
| 2026-09-22 | Tuesday | $767,403.35 | $668,996.35 | $72,807.00 | $25,600.00 | CashRecalculation | 2026-09-22 22:40:07 |
| 2026-09-23 | Wednesday | $762,544.05 | $664,137.05 | $72,807.00 | $25,600.00 | CashRecalculation | 2026-09-23 20:30:06 |
| 2026-09-24 | Thursday | $759,513.36 | $661,106.36 | $72,807.00 | $25,600.00 | CashRecalculation | 2026-09-24 20:30:01 |
| 2026-09-25 | Friday | $766,289.36 | $680,524.36 | $51,505.00 | $34,260.00 | CashRecalculation | 2026-09-25 20:30:24 |

## Day-over-Day Change (vs. prior trading day)

| From → To | Total $ Δ | Total % Δ | Stocks $ Δ | Cash $ Δ | Options $ Δ |
|---|---:|---:|---:|---:|---:|
| Fri 09-18 → Mon 09-21 | +$25,583.07 | +3.46% | −$1,662.93 | +$27,246.00 | $0.00 |
| Mon 09-21 → Tue 09-22 | +$3,126.08 | +0.41% | +$3,126.08 | $0.00 | $0.00 |
| Tue 09-22 → Wed 09-23 | −$4,859.30 | −0.63% | −$4,859.30 | $0.00 | $0.00 |
| Wed 09-23 → Thu 09-24 | −$3,030.69 | −0.40% | −$3,030.69 | $0.00 | $0.00 |
| Thu 09-24 → Fri 09-25 | +$6,776.00 | +0.89% | +$19,418.00 | −$21,302.00 | +$8,660.00 |
| **Net (09-18 → 09-25)** | **+$27,595.16** | **+3.74%** | **+$12,991.16** | **+$5,944.00** | **+$8,660.00** |

## Observations

1. **Net gain over the week**: Portfolio total value rose **+$27,595.16 (+3.74%)** from Sept 18 to Sept 25.
2. **Sept 18 → 21 jump (+3.46%)** is driven almost entirely by a **+$27,246 cash inflow**, not market movement — stocks value actually *decreased* slightly (−$1,662.93) over that gap. Worth confirming whether this is a deposit, dividend, or a settled trade.
3. **Sept 21–24 movement is pure stock market fluctuation** — cash and options were flat ($72,807.00 and $25,600.00 unchanged) each day, so the daily total swings (+0.41%, −0.63%, −0.40%) came entirely from the equity/ETF holdings.
4. **Sept 25 shows a portfolio rebalancing event**: cash dropped by **$21,302** while options rose by **$8,660** and stocks jumped **+$19,418** — consistent with cash being deployed into new stock/option positions (money moved out of cash into stocks/options; totals to $6,776 net since these don't perfectly offset, implying market gains layered on top of the reallocation).
5. **Data-quality flag**: every row in this range has `Source = CashRecalculation`, not `EodAuto` (the normal 4:30 PM ET automatic snapshot). This means these rows were last overwritten by a cash-recalculation process (e.g., backdated edits or admin "Recalculate Cash From Date"), not the original EOD capture — the true origin of the stock/options figures may be from an earlier `EodAuto` write that got resealed. Recommend cross-checking against `AutomationRunLogs` if the provenance of these values needs to be verified for reporting purposes.
6. **No weekend rows** (Sept 19–20) — expected, since `PortfolioValueEodBackgroundService` only records on trading days.

## Query Used (read-only, no data modified)

```sql
SELECT RecordedDate, RecordedAt, TotalValue, StocksValue, CashValue, OptionsValue, Source
FROM PortfolioValueHistories
WHERE RecordedDate BETWEEN '2026-09-18' AND '2026-09-25'
ORDER BY RecordedDate, RecordedAt;
```
