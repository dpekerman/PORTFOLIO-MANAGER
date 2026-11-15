# PM-029 Phase 1B — Swing-v2 Implementation Report

**Disposition:** Phase 1B implementation, read-only replay, and verification are complete. PM-029 remains **OPEN** pending manual UAT and explicit approval. No Phase 2 work was started.

## 1. Baseline and preservation

| Item | Baseline / verified state |
|---|---|
| Branch | `feature/pm-029-final-review` |
| Starting `HEAD` | `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22` |
| Original staged PM-029 paths | 18 |
| Original staged binary-diff hash | `708a965eab9e5167192fd554e21c6a4398e0ab86` |
| Commit, push, branch switch, migration, seed, deployment | None |

The original staged content remains unchanged. Phase 1B and the previously approved Phase 0C work are unstaged overlays in the existing worktree. No unrelated staged changes were made.

## 2. Implemented behavior

- Swing now uses **pm029-v2**: `round-half-away-from-zero(100 × awarded S1–S5 / 80)`. S1 and S3 remain essential; the existing 70% technical completeness gate and score bands remain unchanged.
- S6, protective stops, targets, and Market R:R no longer gate Swing technical scoring. Missing setup evidence remains explicitly unavailable; no entry, stop, target, or Reward/Risk is fabricated.
- Strategic remains **pm029-v1** with its original scoring behavior. Existing snapshots remain immutable and version-specific disclaimers are preserved.
- `ScoreDenominatorMax` is additive API metadata. `AvailableMax` still reports observed evidence; legacy payloads without the denominator retain their prior presentation.
- Swing scoring rejects nonpositive analysis prices, malformed indicator domains, and scans outside the completed exchange session. Freshness uses the existing exchange calendar and actual regular/early close; no arbitrary age cutoff or score cap was introduced.
- The details panel distinguishes fixed technical points from observed evidence and separately presents setup readiness. Reason codes for invalid/missing technical inputs have readable labels.
- Existing grid columns, filters, colors, heat formatting, responsive behavior, and FINAL ACTION presentation were not redesigned.

### Main files changed for Phase 1B

- Backend: `OpportunityScoringEngine.cs`, `OpportunityScoringConfig.cs`, `OpportunityModels.cs`, `OpportunityDtos.cs`, `OpportunityService.cs`, `TradingCalendar.cs`.
- Backend tests: `Pm029CalculatorTests.cs`, `Pm029SwingFormulaValidationTests.cs`, `Pm029OpportunityServiceTests.cs`, `TradingCalendarTests.cs`.
- Frontend: `opportunities.models.ts`, `opportunity-details-panel.component.html`, `opportunity-details-panel.component.spec.ts`, `opportunities-logic.ts`, `opportunities-logic.spec.ts`.
- E2E: `e2e/fixtures/opportunities.ts`, `e2e/trading-opportunities.spec.ts`.
- Specification: `pm-029-trading-opportunities.md`.

The shared scanner, staged-signal, identity-resolution, and FINAL ACTION refactor files in the worktree are the previously approved Phase 0C implementation; Phase 1B did not change their business rules.

## 3. Saved-snapshot replay

Snapshot 6 was read through the existing local application connection using read-only application intent and SELECT-only queries. No Opportunity refresh was initiated. The saved snapshot remains `pm029-v1`, `Succeeded`, with 224 rows; all rows had `RowStatus=Ok`, a positive analysis price, and the same expected/data session (`2026-11-13`). The saved JSON SHA-256 captured before implementation was:

`EAB3E5CC54EB2F16398D15E491AFFBDC3361B348D8A76E073E33612B4C913BB3`

The replay reuses stored S1–S5 component evidence; it is not a fresh market scan. It does not update the stored snapshot or invoke production caches.

| Component | Observed / Partial / Missing | Awarded / available max across 224 |
|---|---:|---:|
| S1 Trend & Structure | 220 / 4 / 0 | 2,565 / 5,576 |
| S2 Relative Strength | 190 / 0 / 34 | 1,686 / 3,800 |
| S3 RSI/MACD Momentum | 224 / 0 / 0 | 1,867 / 3,360 |
| S4 Volume Confirmation | 224 / 0 / 0 | 850 / 2,240 |
| S5 Entry Quality | 160 / 64 / 0 | 1,406 / 2,112 |
| S6 Risk/Reward (excluded from Swing-v2) | 5 / 0 / 219 | 100 / 100 |

Before the change, **3 rows were Verified, 2 Provisional, and 219 Insufficient** because S6 was missing; all 219 carried `MissingEssential:S6`. Under the approved Option B replay, all 224 pass the technical evidence gate: **130 Verified and 94 Provisional**. This changes score availability, not trade readiness.

### Candidate formula comparison

| Gate | Formula | Scorable | Unscorable | Verified | Provisional | Strong | Attractive | Moderate | Caution | Weak |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 70% | A — available-evidence normalized | 224 | 0 | 130 | 94 | 11 | 57 | 33 | 13 | 110 |
| 70% | B — fixed 80-point pool | 224 | 0 | 130 | 94 | 6 | 53 | 34 | 16 | 115 |
| 80% | A | 190 | 34 | 130 | 60 | 9 | 50 | 29 | 12 | 90 |
| 80% | B | 190 | 34 | 130 | 60 | 6 | 52 | 30 | 12 | 90 |
| 90% | A | 190 | 34 | 130 | 60 | 9 | 50 | 29 | 12 | 90 |
| 90% | B | 190 | 34 | 130 | 60 | 6 | 52 | 30 | 12 | 90 |

At 70%, A and B have identical scores for 131 rows; mean absolute difference is 2.22 points, maximum 25, Spearman correlation 0.9712, and top-20 overlap 14/20. The fixed denominator avoids inflating rows that lack S2 sector-relative evidence: `NOVO.TO` is 95 under A versus 71 under B at 75% completeness; `CCL-B.TO` is 91 versus 66 at 72.5%. At 80% and 90%, top-20 membership is identical.

**Option B top 20 at the approved 70% gate** (ties ordered by symbol):

| Rank | Symbol | Score / band | Completeness |
|---:|---|---|---:|
| 1 | PPL.TO | 89 / Strong | 97.5% |
| 2 | AAPL | 86 / Strong | 97.5% |
| 3 | ARE.TO | 86 / Strong | 100% |
| 4 | MTL.TO | 86 / Strong | 100% |
| 5 | TOU.TO | 85 / Strong | 100% |
| 6 | TXN | 85 / Strong | 100% |
| 7 | ATRL.TO | 84 / Attractive | 97.5% |
| 8 | KR | 84 / Attractive | 97.5% |
| 9 | STN.TO | 84 / Attractive | 97.5% |
| 10 | TOI.V | 83 / Attractive | 100% |
| 11 | TSLA | 83 / Attractive | 100% |
| 12 | EIF.TO | 81 / Attractive | 100% |
| 13 | LMT | 81 / Attractive | 100% |
| 14 | TRP.TO | 81 / Attractive | 97.5% |
| 15 | BAM.TO | 80 / Attractive | 97.5% |
| 16 | FNV.TO | 80 / Attractive | 100% |
| 17 | VNP.TO | 80 / Attractive | 100% |
| 18 | IBM | 79 / Attractive | 97.5% |
| 19 | KXS.TO | 79 / Attractive | 97.5% |
| 20 | LLY | 79 / Attractive | 97.5% |

The complete Option A and B comparison and its ranking analysis are also recorded in [the Phase 1A validation report](./pm029-phase-1a-swing-score-validation.md).

### CCA.TO no-setup result

Snapshot component evidence is S1 **0/25 observed**, S2 **missing (0/20 available)** due to unmapped sector-relative data, S3 **8/15**, S4 **4/10**, and S5 **10/10**. Awarded S1–S5 totals 22; available evidence is 60/80 (75%). The fixed-pool result is `round(22/80 × 100) = 28`, **Provisional**. The saved setup has no valid stop (`NoValidStop`, `DynamicStopMissing`), so Market R:R remains unavailable. No stop or target is inferred from the Swing score.

### High-R setup audit

The saved setup values were checked against the formula `(target - entry) / (entry - stop)`. All three securities were TSX CAD listings whose row price, entry, setup currency, and analysis price basis agreed. Their stop equaled the scanner `DynamicStopLoss`, and each stop distance was inside the existing 0.5–4 ATR gate.

| Symbol | Entry / stop | Target (source) | ATR / stop distance | Stored R / recomputed R | Risk / reward |
|---|---|---|---|---|---|
| LAC.TO | CAD 2.12 / 1.8133 | CAD 9.47 (52-week high) | 0.1978 / 1.551 ATR | 23.965 / 23.965 | 14.47% / 346.70% |
| ETHH-B.TO | CAD 9.26 / 8.5098 | CAD 16.03 (52-week high) | 0.3935 / 1.906 ATR | 9.024 / 9.024 | 8.10% / 73.11% |
| BTCC.TO | CAD 11.95 / 11.2176 | CAD 17.57 (52-week high) | 0.3949 / 1.855 ATR | 7.673 / 7.673 | 6.13% / 47.03% |

The ratios are arithmetically consistent with stored inputs. In each case the target source is the 52-week high, which is substantially above entry for LAC.TO in particular; this makes the ratio sensitive to a distant target and does **not** establish near-term execution quality. No arbitrary cap, target replacement, or score change was made. Target-quality methodology remains an approval question if a future phase proposes changing the existing setup contract.

## 4. Protected behavior and write boundary

- No live Opportunity refresh, production scoring write, migration, seed, or database update was run.
- No FINAL ACTION recommendation rules, scanner lifecycle, staged-signal state, mapping, saved plan, broker stop, holding, transaction, cash, option, or account record was intentionally changed.
- Phase 0C Preview/Persist/ReadOnly separation and FINAL ACTION parity safeguards remain in place; backend regression tests cover the existing shared behavior.
- SQL Server snapshot access was read-only. The snapshot hash will be checked again after the full regression run and recorded here.
- The Phase 0C Universe Value Screener persistence boundary and SQL Server concurrency limitation are unchanged; no refresh was run to exercise that boundary.

The post-test read-only check returned the same snapshot hash and metadata: snapshot 6 is still `pm029-v1` / `Succeeded`, the last attempt remains `Succeeded`, and the last successful snapshot remains 6. The SHA-256 is unchanged from the baseline above. Playwright used mocked APIs; the .NET tests did not refresh the local Opportunity snapshot.

## 5. Screenshots

These are API-mocked, synthetic UAT screenshots, not screenshots of a live refresh or an account's current data. The CCA view demonstrates an independent Swing score beside an unavailable setup.

![Desktop heatmap](./pm029-phase-1b-screenshots/desktop-chrome-01-heatmap.png)

![Desktop CCA.TO technical score without setup](./pm029-phase-1b-screenshots/desktop-chrome-04-swing-no-setup.png)

![Samsung S23 CCA.TO technical score without setup](./pm029-phase-1b-screenshots/mobile-s23-04-swing-no-setup.png)

All viewport screenshots are in [`pm029-phase-1b-screenshots/`](./pm029-phase-1b-screenshots/): desktop Chrome, tablet, Samsung S21 (384 px), and Samsung S23 (360 px), including heatmap, Overview, Technicals, Scores, Evidence, multiple FINAL ACTION decisions, columns, and the CCA no-setup case.

## 6. Validation

| Suite | Result |
|---|---|
| Full backend Release suite | 1,257 passed, 0 failed, 4 skipped (three `AccountTypeSqlServerTests`, one `Pm033IsolatedSqlTests`; isolated SQL Server test setup unavailable) |
| Angular unit tests | 691 passed across 77 test files |
| Angular production build | Passed; existing SCSS budget warnings only |
| Targeted UAT screenshot flow | 4/4 passed on desktop Chrome, tablet, mobile S21, and mobile S23 |
| Full Playwright regression (1,204 tests, four projects) | 1,024 passed, 179 skipped, 1 unrelated failure; isolated retry of that failure passed |
| PM-029 Trading Opportunities E2E after final fixture/label changes | 32 passed, 4 opt-in screenshot cases skipped |
| Opt-in screenshot capture after final changes | 4/4 passed on desktop Chrome, tablet, mobile S21, and mobile S23 |
| `git diff --check` and staged diff check | Passed |

The initial full Playwright attempt had four screenshot failures because the shared Columns dialog does not close on Escape, plus one transient mobile risk-dialog timeout. The test now closes Columns using its accessible Close button, and the opt-in screenshot test passed on all four viewports. In the final full-suite run, the sole failure was an unrelated mobile-S21 Signal Tracker test that timed out while clicking the Status filter; its isolated rerun passed in 2.6 seconds. No Signal Tracker changes were made. The final PM-029 E2E and screenshot runs passed after the fixture and reason-label refinements.

## 7. Manual UAT checklist

Use an approved non-production/test environment and do not initiate a live Opportunity refresh for this review.

- [ ] Confirm Swing-v2 can display a technical score for CCA.TO while its setup and Market R:R remain unavailable.
- [ ] Review Overview, Technicals, Scores, and Evidence; confirm the 80-point denominator, evidence completeness, component points, missing S2 reason, and numeric precision.
- [ ] Verify Weak/Provisional colors and markers, score filters, sorting, and the existing full column selector.
- [ ] Confirm the 39-column selector and existing filters, Sector Heat formatting, and desktop/tablet layout remain intact.
- [ ] Verify 360 px and 384 px layouts have no horizontal overflow and that card/tab interactions remain usable.
- [ ] Compare FINAL ACTION labels, reasons, actionability, and account/role-specific decisions with the authoritative source.
- [ ] Confirm a missing stop does not fabricate Entry/Stop/Target/Reward, and inspect the three high-R cases without assuming a distant 52-week high is an executable target.
- [ ] Confirm the saved snapshot and protected financial/scanner records were not modified by the test run.

## 8. Remaining limitations and approval gates

- No new live market scan was run; the 224-row replay is reproducible from persisted S1–S5 evidence but does not refresh indicators.
- The four SQL Server-dependent backend tests were skipped. No isolated relational SQL Server concurrency/write-isolation suite was available for Phase 1B.
- The saved snapshot remains `pm029-v1`; new `pm029-v2` results have not been persisted because live refresh was not authorized.
- Manual UAT and explicit user approval remain required. Do not proceed to Phase 2, change setup/target methodology, or close PM-029 without that approval.
