# PM-029 Phase 10 — Portfolio Fit and Manual R Integrity Report

**Disposition: PM-029 remains OPEN.** A narrow Portfolio Fit evidence-status correction and regression work are complete. Production-universe validation and owner manual UAT remain pending.

**Branch:** `feature/pm-029-final-review`

**Phase 10 starting HEAD observed:** `5a5e1ea14709ce426c9992adfc848316b096dc2e`

**Evidence type:** Automated tests and synthetic/API-mocked UI evidence; not production data.

## Executive summary

The audit found a narrow Portfolio Fit status gap: if the security's sector was known and had a configured allocation target, but sector exposure itself was unavailable, the calculator could leave Fit at `Eligible` without explaining the missing evidence. The calculator now returns `Review` with `SectorExposureUnavailable`. It does not estimate exposure or calculate headroom from missing data.

Manual R remains a local, temporary scenario separate from Market R and the approved scoring/decision pipeline. Added tests cover invalid prices and verify that changing Manual R leaves the score evidence and available portfolio/market decision evidence unchanged.

No scoring formula, shared FX/accounting or valuation service, FINAL ACTION behavior, stop/target methodology, database schema, database contents, or production data was changed. The automated results do **not** close the real-universe or manual-UAT gates.

## Findings and disposition

| Area | Finding | Phase 10 action / remaining limit |
|---|---|---|
| Portfolio Fit | A known sector with a target but unavailable sector exposure had no explicit review reason and could remain `Eligible`. | **Corrected:** classify as `Review`, add `SectorExposureUnavailable`, preserve null sector weight/headroom. Covered by a focused backend test and UI reason-label test. |
| Manual R validation | Incomplete, zero, negative, or non-finite prices must not produce a ratio. | Added focused Angular cases for each field and invalidity class. Manual R remains session-only and isolated by symbol and perspective. |
| Manual R non-interference | Manual R must not overwrite the calculated Market R or affect scores/Portfolio Fit/Sector Heat/FINAL ACTION. | Added four-viewport Playwright regression comparing score blocks and visible grid evidence before/after. The test compares Market R's detail evidence at every viewport and checks its grid column where rendered; handset card mode intentionally omits the non-primary Market R column. |
| Manual R currency labels | The three inputs share the row's single currency label. | Verified the synthetic QRK scenario labels all fields CAD. `ManualRValues` does not model distinct currencies per field; mapped CDR/listing-versus-analysis currency remains an evidence question, not a basis for a guessed conversion. |
| Swing / Strategic completeness | Swing availability is governed by identity, scan/evaluation validity, freshness, analysis price, and scoring evidence/completeness—not by setup or Manual R. | Existing independence tests remain in place; no score eligibility or scoring policy was changed. Synthetic fixtures do not establish completeness across the production universe. |
| Sector Heat | Opportunity Sector Heat is signed stock-minus-benchmark return over aligned sessions. Dashboard Market Leadership classifies an individual tracker using its own absolute price/momentum/trend evidence. | No mapping, threshold, or semantic changes made. Synthetic mapping fixtures are not production mapping validation. |
| FX / valuation | Mixed-currency allocation is not authoritatively converted to a common currency; currency classification does not establish quote freshness or quote availability. Stock value may use average-cost fallback. | **Approval/data gate remains open.** No FX conversion, quote-quality rule, or financial valuation change was introduced. |
| Production coverage | Earlier review referenced a 224-security scan, but Phase 10 had no authorized live SQL Server or current sanitized universe export to validate. | **Blocked.** Five synthetic fixture rows cannot prove mapping, fundamentals, FX allocation, Swing/Strategic completeness, or Fit coverage across the live universe. |

## Changes made

- `backend/PortfolioManager.Api/Services/Opportunities/OpportunityModels.cs` — added the additive Fit reason code `SectorExposureUnavailable`.
- `backend/PortfolioManager.Api/Services/Opportunities/PortfolioFitCalculator.cs` — fail to `Review` when a known, targeted sector has no available exposure; no value is inferred.
- `backend/PortfolioManager.Tests/Pm029CalculatorTests.cs` — added the missing-exposure Fit status/reason assertion.
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunities-logic.ts` — added the user-facing label for the Fit reason.
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunities-logic.spec.ts` — added the Fit reason label and invalid Manual R boundary cases.
- `frontend/portfolio-manager-ui/e2e/trading-opportunities.spec.ts` — added cross-viewport Manual R non-interference assertions and an invalid Manual R screenshot state.
- `docs/pm-029-trading-opportunities.md` — documented that unavailable sector exposure with a configured target requires review.
- `docs/pm029-master-implementation-progress.md` — recorded Phase 10 status and the live-checkout baseline discrepancy.

No new policy or financial calculation was added. The Fit correction only changes how missing approved evidence is classified.

## Verification

| Check | Result |
|---|---|
| Focused .NET Release filter (PM-029 calculators and Final Action) | **196 passed, 0 failed, 0 skipped** |
| Angular opportunity-logic unit tests | **21 passed** |
| Angular production build | **Passed.** Existing component SCSS budget warnings remain in dashboard, scanner, watchlist, portfolio, signal-tracker, and settings styles. |
| PM-029 Playwright matrix | **56 passed, 0 failed** across desktop Chrome, tablet, mobile S21 (384 px), and mobile S23 (360 px). |
| Synthetic screenshot capture | **70 PNGs**, all filename-prefixed `SYNTHETIC-` and visibly watermarked `SYNTHETIC | NOT PRODUCTION`; archive contains 70 entries. |
| `git diff --check` | Passed for tracked changes. |
| Database / production-universe access | Not performed; unavailable/unauthorized current data source and no approved sanitized export. |

Playwright APIs are mocked by the existing test harness. These tests demonstrate deterministic fixture behavior and responsive UI behavior only.

## Screenshot package

- [All 70 screenshots (ZIP)](screenshots/pm029-phase-10-integrity-uat.zip)
- [Screenshot directory](screenshots/pm029-phase-10-integrity-uat/)

The ZIP is 16.7 MB (SHA-256: `DC2BD3C2641F42CDF259FA9BFBC2179C5F0D8C4BD40129FD8E92C4E5B42284CA`).
The package contains desktop/tablet captures of the heatmap, overview, valid and invalid Manual R, technicals, scores, evidence, multi-action details, columns, Strategic defaults/grid, and the no-setup CCA case. Samsung S21 and S23 also include the overview-levels capture. Each viewport has its own files; screenshots are synthetic and must not be interpreted as current portfolio or market data.

## Remaining acceptance gates

| Gate | Status | Needed before closure |
|---|---|---|
| Current full-universe mapping, score, fundamentals, FX allocation, and Fit coverage | **BLOCKED** | Authorized production access or an approved, current, sanitized export with universe denominator and evidence provenance. |
| Mixed-currency Portfolio Fit | **APPROVAL REQUIRED** | Owner-approved common-currency valuation authority, FX source/timestamp/fallback rules, and consumer reconciliation before any implementation. |
| Quote freshness / missing-quote semantics | **OPEN** | Production examples and an approved rule distinguishing stale/missing quotes from cost-basis fallback. |
| CDR Manual R currency semantics | **OPEN** | Authoritative listing/analysis currency contract before allowing or labeling field-level multi-currency inputs. |
| User manual UAT | **PENDING** | Owner reviews the report and screenshot package and records explicit acceptance. |

## Git and safety notes

The active branch remained `feature/pm-029-final-review`; no branch change, commit, push, staging, database operation, or production-data read was performed. At Phase 10 re-entry, the live checkout reported HEAD `5a5e1ea14709ce426c9992adfc848316b096dc2e` and an empty staged diff. This differs from retained reports/checkpoints that record HEAD `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22` and varying staged counts (41, 55, and 59). The earlier staged index was not reconstructed, and no authorship or disposition is inferred.

**Recommendation:** Treat the safe evidence-status correction as complete, but keep PM-029 open. Do not start a broad implementation cycle or change FX/scoring policy until the live-universe evidence and owner decisions above are available.
