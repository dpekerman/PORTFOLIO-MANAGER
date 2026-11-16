# PM-029 Gate 2 — Final Portfolio Fit

**Status: implemented and tested; not yet visible in the running app.** The API must be restarted (and
the Trading Opportunities snapshot refreshed) to pick it up. Both are protected actions awaiting your approval. PM-029 stays OPEN until UAT passes.

## 1. Source files changed
- `Services/FinalAction/FinalActionService.cs` — one CAD basis for every allocation figure.
- `Services/FinalAction/PortfolioEvidenceGate.cs` — `ValuationCheck.CadConverted` status + `UsdCad` rate.
- `Services/Valuation/UsdCadRateSource.cs` (new) — one USD/CAD rate per evaluation, on top of the existing `BankOfCanadaFxProvider`.
- `Services/Valuation/BankOfCanadaPublicationCalendar.cs` (new) — production `IFxPublicationCalendar`.
- `Program.cs` — registers the provider, calendar and rate source.
- Tests: `Pm029Gate2FxTests.cs` (new), `FinalActionIntegrityTests.cs`.
No frontend change: grid and detail panel already render the Fit fields and the reason.

## 2. How the five items were resolved
1. **Connect valuation.** Fit already flows `OpportunityService → FinalActionService basis → ExposureFor → PortfolioFitCalculator`
   for both Swing and Strategic, so they share one basis and one rate. The mixed-currency block is replaced by CAD conversion
   (`MarketValue(s, valuation)`, `GrandTotalAsync`, `SectorExposure`, `ExposureFor`, `OwnershipOf`, `BuildContext`).
   I did **not** route through the shadow `SharedShadowValuationService`: it demands verified per-row currency, session and ledger evidence
   that this database cannot provide, so it would stay incomplete. Documented deviation; no second engine was built.
2. **Cash currency.** Approved convention applied in one place (`CashCurrency`): USD when account/description carries a USD marker, else CAD.
   Reproduced on real data: **65 CAD / 6 USD**. No cash rows were rewritten.
3. **Cash ownership.** The 5 "unowned" rows are the migrated opening balances (2026-09-05; Corp_TD, Margin_L_TD, TFSA_D_TD, TFSA_L_RBC, TFSA_L_TD),
   each with a valid `AccountType`; `UserId` is NULL (legacy, visible to the single user). The app's own scope (`UserId == user || UserId == null`)
   already includes them, so the same scope is reused: they count once, no record is changed. Nothing needs owner review.
   Cash is a single ledger (movements sum to the balance); no separate balance table exists, so nothing is double counted.
4. **Options.** Open positions = existing `TransactionType != CLOSE` rows (10); CLOSE rows excluded. Value = Market Price × contracts × 100,
   the formula the Options screen uses (`transactions-page.component.ts` "Current MKT Value"). Book value never substituted. There is no short
   direction in the data; no sign is applied.
   **Note:** you described Market Value as possibly the whole-position amount. The screen stores a per-share price (e.g. K.TO 3.75 vs premium 4.70,
   10 contracts), so multiplying is what the screen does; treating it as a total would understate options ~1,000×.
5. **FX calendar.** `BankOfCanadaPublicationCalendar` (weekdays minus federal holidays, 16:30 ET cutoff). `UsdCadRateSource` takes the latest
   observation already published at evaluation time (no look-ahead), tolerates one missed publication, else `FxStale`/`FxUnavailable`. Direction is CAD per USD.
   It is only called when the portfolio truly mixes CAD and USD; failure keeps Fit Unavailable with the reason; never 1:1.

## 3. FX verification
Bank of Canada FXUSDCAD 2026-11-13 = **1.4257** CAD per USD (live Valet, read-only).

## 4. CAD reconciliation (read-only local DB; aggregates only)
| Component | Native | CAD |
|---|---|---|
| Stocks/ETFs (55 open lots, current quotes, 0 cost fallbacks) | 749,373.67 CAD + 2,560.00 USD (CABA) | 753,023.46 |
| Cash (71 rows) | 25,547.61 CAD + 9,934.00 USD | 39,710.51 |
| Open options (10 rows) | 12,423.00 CAD + 1,110.00 USD (BIDU) | 14,005.53 |
| **Consolidated** | | **806,739.50** |

Old unconverted sum was 800,948.28; the 5,791.22 difference is exactly the USD→CAD conversion (no adjustment added).
Stock/ETF figures use the Portfolio snapshot the app itself serves; I did not run the app UI to compare screens (not authorized); please compare in UAT.

## 5. Allocation results (same data)
Top weights of the CAD total: NVDA.TO 4.48%, ENB.TO 4.36%, BRK.TO 3.71%, MDA.TO 3.63%, FFH.TO 3.43% (limits: Core/Strategic 5, Swing 3, Speculative 2).
Sector shares of the stock-only denominator: Financial Services 17.7%, Energy 17.2%, Industrials 13.6%, Communication Services 13.0%, Utilities 9.0%,
Technology 9.0%, Consumer Defensive 7.3%, Consumer Cyclical 4.5%, Materials 4.3%, ETFs & Funds 2.5%, Healthcare 1.9%.
Headroom/status come from the unchanged `PortfolioFitCalculator` (2 pp sector tolerance preserved).

## 6. FINAL ACTION
Gate 1 rule untouched: ADD is non-actionable whenever valuation is unreliable or the denominator is missing/non-positive. It now lifts only when the CAD
conversion is verified, and all other gates still apply.

## 7. Tests
Backend full suite (isolated copy; running API locks its build output): **1410 passed, 0 failed, 4 skipped.** New cases: CAD/USD stock, CAD/USD cash,
long options CAD+USD, closed option excluded, one denominator for both perspectives, mixed without rate stays Unavailable, CAD-only never calls FX, FX
publication/no look-ahead/stale/missing/provider failure, holiday calendar. Angular: no changes, not re-run.
The 20 earlier cases (position/sector limits, unknown sector, gate) remain green.

## 8. Remaining blockers / protected actions
1. **Restart the API** so DI picks up the new services (startup runs background jobs; confirm acceptable).
2. **Run an Opportunities refresh** — writes `OpportunitySnapshots` (prunes beyond 5) and `OpportunityRefreshState`; without it the stored snapshot still says Unavailable.
3. No migration, backfill or financial-record change is required.
Rollback: revert the branch; the stored snapshot can be superseded by the previous one (5 are retained).

## 9. Live result (snapshot 17, after restart + owner refresh)
Fit: Eligible 167, NoAdd 39, Review 18, Unavailable 0 (was 224 Unavailable). Reasons: SectorOverTarget 39, SectorNoTarget 23,
SectorUnknown 12, PositionNearLimit 5, RolePolicyAmbiguous 3. A first refresh showed 178 SectorUnknown because Fit received only the scanner
sector; Fit now uses the row's resolved sector (`OpportunityService`), which cut it to 12. Weights match the reconciliation
(NVDA.TO 4.48%, ENB.TO 4.36%, BRK.TO 3.71%). Swing and Strategic read the same per-row Fit.

## 10. Reconciliation against the app's own totals (read-only, Dashboard history 2026-11-13)
| Component | Dashboard/Portfolio (stored) | My raw sum | Match |
|---|---|---|---|
| Stocks/ETFs | 751,933.67 | 749,373.67 + 2,560.00 | exact |
| Cash | 35,481.61 | 25,547.61 + 9,934.00 | exact |
| Open options | 13,533.00 | 12,423.00 + 1,110.00 | exact |
| Total | 800,948.28 | 800,948.28 | exact |

The only difference to the Fit denominator (806,739.50 CAD) is the USD→CAD conversion of 5,791.22 at 1.4257. Dashboard/Portfolio add USD
amounts as if they were CAD (existing behavior, not changed here; changing it needs your approval).

## 11. Manual UAT
After restart + refresh, open Trading Opportunities (Strategic, then Swing): Fit no longer Unavailable; weight, limit, headroom, sector weight/target/headroom shown and
identical in both perspectives and the detail panel; compare totals with Dashboard/Portfolio (expect ≈ 806.7k CAD ±quote moves); an open option's Market Value
moves the total, a CLOSED one does not; a position at/over its limit shows NoAdd and FINAL ACTION ADD stays blocked; the detail Evidence warning names the exact reason if FX is missing.
