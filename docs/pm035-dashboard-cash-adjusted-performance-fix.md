# PM-035 — Dashboard cash-adjusted performance fix

Date: 2026-11-16. Status: code complete, unit-tested; **backend restart required** before manual UAT (see §9).

## 1. Root cause
The upper "YTD performance" card was a second, older calculation path that ignored cash flows:

- `PerformanceSummaryService` returned `(current − start) / start`, and the widget (`performance-summary-widget.component.ts`) recomputed the same raw difference from the live total. A $40,000 of deposits therefore showed as profit.
- It fell back to the **earliest snapshot (2026-07-06)** and labelled it "Portfolio Return / Since inception", next to full-calendar-year benchmark returns (different period).
- Its backend "current value" excluded options, while the card overrode it with the dashboard total (which includes options).
- The snapshot at 2026-07-06 is also not comparable: total value drops **$804,141 → $776,716 on 2026-09-05** (cash-ledger start, no recorded flow), so any baseline before that date is unreliable.

The lower "Performance (cash in/out adjusted)" section (`PortfolioPerformanceService`) was verified correct and was kept.

## 2. Verified data (read-only SQL; nothing written)
- `PortfolioValueHistories`: 97 snapshots, 2026-07-06 → 2026-11-13. **No snapshot before Jan 1 2026 → true YTD is genuinely unavailable.**
- `CashLedgerSettings.LedgerStartDate` = 2026-09-05; opening balances (6 rows, $45,552) dated 2026-09-05.
- External flows (`CashFlowType` Deposit/Withdrawal): 2026-09-27 +100 / −100 (same account, nets to 0); **2026-11-11 +20,000 (TFSA_L_RBC); 2026-11-16 +20,000 (TFSA_D_TD)**.
- Not external (correctly excluded): TradeProceeds/TradePurchase, OpeningBalance, Adjustment±, Dividend/Interest/Fee/Tax.
- Flagged, not changed: AdjustmentIncrease +21,254 and AdjustmentDecrease −11,955 (Sep 25–27) are treated as investment result by the existing rules. Review if they were really external money.

## 3. Old vs corrected (live total $814,113)
| Period | Baseline | Value change | Net cash in/out | Investment gain/loss | Return % |
|---|---|---|---|---|---|
| Today / Week | 2026-11-13 $800,948 | +13,165 | +20,000 | **−6,835** | −0.85% (TWR) |
| Month | 2026-10-30 $779,411 | +34,702 | +40,000 | **−5,298** | −0.63% (TWR) |
| YTD | none before Jan 1 | — | — | **Unavailable** (no baseline) | — |
| Since cash-ledger start (new) | 2026-09-05 $776,716 | +37,397 | +40,000 | **−2,603** | −0.29% (TWR) |
| Old upper card ("since inception") | 2026-07-06 $805,804 | +8,310 | ignored | **+8,310 (wrong)** | +1.03% (wrong) |

Lower-section dollars are unchanged (they were already right); percentages were previously "N/A" whenever cash moved and are now time-weighted where snapshots allow. Figures above were recomputed independently from the DB. The upper card now shows the Since-ledger-start period (−$2,603), clearly labelled, and notes that YTD is unavailable. It does not show Since Inception, because pre-2026-09-05 flows are not recorded.

## 4. Return % methodology
- No external flow in period → simple return on the baseline (equal to time-weighted).
- External flow → **time-weighted return** over stored daily snapshots: each interval growth = (end value − flows in interval) / start value, flows assumed at end of day, intervals chained. Label is exposed as a tooltip (`returnMethod`).
- If a flow falls in an interval wider than 5 days (a missing snapshot) → **N/A**, never invented.
- Benchmarks (TSX Composite, S&P 500) start from the same baseline date as the portfolio period; if dates differ they are hidden, not rebased.

## 5. Files changed
Backend: `Services/PortfolioPerformanceService.cs` (flow list, TWR, `SinceStart` period, baseline value/method), `Services/PerformanceSummaryService.cs` (benchmarks only, same baseline), `Models/DashboardModels.cs` (`BaselineValue`, `ReturnMethod`).
Frontend: `performance-summary-widget.component.{ts,html}` (reads `summary.performance`; no own arithmetic), `shared/portfolio-performance/*` (hides `SinceStart` from the 4-period grid), `core/models/portfolio.models.ts`, `e2e/fixtures/dashboard.ts`.
Tests: `PortfolioPerformanceTests.cs`, `PerformanceSummaryServiceTests.cs`, `performance-summary-widget.component.spec.ts`, `portfolio-performance.component.spec.ts`.

Not touched: portfolio valuation, Portfolio Fit (PM-029), recommendations/FINAL ACTION, any financial record, migrations.

## 6. Tests
- Backend full suite: **1422 passed, 0 failed** (4 skipped, pre-existing).
- Frontend full unit suite: **717 passed, 0 failed**.
- Covered: no flows; +$20k deposit → $0 gain; −$20k withdrawal → $0 loss; $40k / +$34,702 → −$5,298; multiple deposits; deposit + withdrawal; cross-account transfer (Unavailable); trade/option/dividend/interest types excluded; USD flow (Unavailable); missing/stale baseline; ledger coverage; Since-start vs YTD boundaries; upper/lower consistency; TWR (incl. deposit-then-loss = −10%, not −20%); TWR gap → N/A. No Playwright run.

## 7. Source of external cash flows
`CashItems` rows with `CashFlowType` Deposit/Withdrawal, effective date `TransactionDate` (else `AddedAt`), window (baseline, today]. Ambiguity (USD marker, "transfer" wording, matching cross-account pair, unclassified type, future-dated row) → period shows Unavailable. CAD/USD conversion between accounts is not an external type and is excluded.

## 8. Missing baseline data
- No portfolio snapshot before 2026-01-01 → **YTD unavailable**. Not backfilled.
- No cash ledger before 2026-09-05 → Since-inception unavailable; Since-ledger-start used instead.

## 9. Manual UAT
1. Restart the backend (stop PID of `PortfolioManager.Api`, `dotnet run --launch-profile http`), then `ng serve`. Startup applies migrations; none are pending from this change.
2. Dashboard → expand **YTD performance**: expect "Return since Sep 5, 2026 ≈ −0.29%", gain ≈ −$2,603, net cash +$40,000, baseline $776,716, note "Year to date is unavailable…".
3. Expand **Performance (cash in/out adjusted)**: Today −$6,835, Month −$5,298, YTD Unavailable; percentages now populated.
4. Add a test deposit in a non-production account: total value and cash rise, gain/loss and Return % stay flat; remove/reverse it afterwards.
5. Confirm benchmark labels read "… since Sep 5, 2026" (or are hidden if history is unavailable).
