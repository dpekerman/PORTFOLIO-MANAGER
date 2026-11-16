# PM-029 Gate 2 — Portfolio Fit delivery

**Status: BLOCKED at the owner-approval boundary.** The safe part is implemented and tested.
Portfolio Fit still shows *Unavailable* on real data, correctly, because the CAD denominator
cannot be verified from the current database. PM-029 stays OPEN.

Branch: `feature/pm029-gate2-portfolio-fit` (from synced `develop` @ a615199, Gate 1 / PR #73 present).
Nothing staged, committed, pushed, migrated, seeded, refreshed or restarted.

## 1. Files changed
- `backend/PortfolioManager.Api/Services/FinalAction/FinalActionService.cs` — `ClassifyValuation` now states
  the per-component currency split and the exact missing evidence (was a generic "mixes CAD and USD").
- `backend/PortfolioManager.Tests/FinalActionIntegrityTests.cs` — one new test for that explanation.

## 2. Existing services reused (none replaced)
`FinalActionService.ExposureFor` / `ClassifyValuation`, `PortfolioFitCalculator`, `PortfolioEvidenceGate`,
`Pm029CadValuationCalculator`, `SharedShadowValuationService`, `ValuationConsumerAdapter`,
`VerifiedFinancialEvidenceAdapter`, `BankOfCanadaFxProvider`. Angular grid/detail panel already render weight,
limit, headroom, sector weight/target/headroom, status and reasons — no frontend change was needed.

## 3. Integration finding (why Fit is Unavailable)
Live path: `OpportunityService → FinalActionService.EvaluatePreviewAsync → basis.Valuation → ExposureFor →
PortfolioFitCalculator`. `ValuationCheck.Reliable` is false whenever stocks/cash/options span CAD and USD, so
Fit = `Unavailable (AllocationUnverifiable)`.
The PM-029 CAD valuation stack (Phases 12–15) is **not registered in DI and not called by any controller or
service** — it is shadow-only (`ProductionActivationAllowed = false`). Wiring it today would still yield an
incomplete snapshot (section 4), so it was deliberately not activated.

The legacy `GrandTotalAsync` also treats every open option as a positive long asset
(`MarketPrice × contracts × 100`) and sums mixed currencies raw. It is not used while the check is unreliable.

## 4. Real financial component availability (read-only local DB, aggregates only)
| Component | Evidence | Class |
|---|---|---|
| Stocks (55 open lots, all with sector) | Quote + provider currency; 54 CAD (.TO/.V), 1 USD | 1 Available (needs live quotes; not re-queried) |
| ETFs | Included in the same lots; denominator only | 1 Available |
| Cash (71 rows, 7 accounts) | No schema currency; 65 rows CAD by convention, 6 USD-marked | **3 Owner confirmation** |
| Cash ownership | 5 rows with `UserId = NULL`, summing to **45,280** (larger than the whole net cash of 35,481) | **3 Owner confirmation — material** |
| Open options (10 OPEN rows, 5 unowned) | Manual mark × contracts × 100 = 13,533 native; no long/short side, contract id, multiplier source, or mark timestamp | **4 Schema/data correction** (never assume direction) |
| USD/CAD | Bank of Canada 2026-11-13 = 1.4257 (CAD per USD), provider exists | 2 Available, not integrated |
| FX publication calendar | Calculator needs an explicit calendar; none is registered for production | **3 Owner/approval** |
| Position limits / sector targets | 6 role limits (Core 5, Strategic 5, Strategic-Income 5, Swing 3, Speculative 2, Options 1); 11 sector targets | 1 Available |

## 5. CAD reconciliation
**Not possible to close.** Consolidated CAD total = Unavailable. Cash USD (9,934 USD-marked rows) and
option currency cannot be converted without confirmed currency/ownership; no adjustment was forced.
Indicative only (not a reconciliation): USD-marked cash × 1.4257 ≈ 14,163 CAD.

## 6. Allocation examples
None from real data (denominator unverified). Synthetic fixtures are labelled SYNTHETIC in the images folder.

## 7. FINAL ACTION compatibility
Gate 1 rule preserved: ADD stays non-actionable when valuation is unreliable or the denominator is missing/non-positive;
risk-reducing and informational actions unaffected. No FINAL ACTION code changed.

## 8. Tests
- Backend focused (FinalAction, Pm029, Opportunity, Valuation, Fit, Cad, Shadow; isolated copy because the running
  API locks its output): **426 passed, 0 failed, 0 skipped.** Existing suites already cover the 20 required cases
  (CAD/USD stocks, cash, long/short options, closed excluded, missing currency/FX, non-positive denominator,
  hard limit, sector tolerance, unknown sector, FINAL ACTION gate, cross-consumer consistency).
- Angular: not re-run (no frontend change).
- Mocked Playwright screenshot capture (alt port 4310, API mocked, no live app): `captures UAT screenshots` **4/4 passed**.
  The pre-existing `final functional changes preserve inactive quality…` test **fails 4/4** at
  `setup-quality toHaveCount(0)` (e2e line 561); it predates this change (Setup Quality was approved 2026-11-15 and the
  assertion looks stale) — unrelated, not touched.

## 9. Remaining blockers / required owner decisions
1. Confirm cash currency per account (or approve the CAD-default + `USD` marker convention as authoritative).
2. Assign owners to the 5 unowned cash rows (45,280) and 5 unowned open option rows.
3. Provide option position evidence: side (long/short), contract id, multiplier, mark source/time (needs schema fields).
4. Approve a production Bank of Canada publication calendar (or an explicit staleness policy).
5. Approve registering the shared CAD valuation as the Fit/FINAL ACTION authority (replaces legacy `GrandTotalAsync`).

## 10. Minimal activation and rollback (not executed)
- Reuse: `SharedShadowValuationService` + `ValuationConsumerAdapter`; replace `CheckValuationAsync`/`GrandTotalAsync` inputs
  with the shared snapshot behind a config flag (default off).
- Writes: owner fixes to `CashItems` (UserId, currency) and `OptionItems` (side, contract, multiplier) — needs a migration.
  Snapshots are re-written only by an approved Opportunities refresh.
- Rollback: turn the flag off (legacy path returns); restore data from a pre-change backup / reverse migration.

## 11. Manual UAT (after approval)
Strategic then Swing: compare Portfolio Fit status, weight, limit, headroom, sector weight/target/headroom and reasons;
confirm both use one denominator; compare Portfolio/Cash/Options/CAD total with Dashboard; confirm ADD remains non-actionable
while evidence is missing and the reason now names the exact missing items (visible in Evidence warnings).

## 12. Images for review
`D:\PORTFOLIO-MANAGER-GATE2-IMAGES` — 94 SYNTHETIC mocked-API screenshots (desktop, tablet, S21, S23).
