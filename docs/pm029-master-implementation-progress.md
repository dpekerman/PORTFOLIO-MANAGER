# PM-029 Master Implementation Progress

**Program status:** IN PROGRESS — PM-029 remains OPEN.  
**Mode:** Controlled autopilot under the approved master prompt.  
**Current branch policy:** Final functional work uses a new branch from `develop`, as explicitly requested. Earlier phase baselines below are historical.

## Final functional work and scoped backlog

Branch: `feature/pm-029-final-functional-implementation`, based on `develop` at `ce6f990`.
The final review is recorded in [pm029-final-functional-implementation-review.md](pm029-final-functional-implementation-review.md).

The base repository was checked with `git grep -n -E 'PM-036|PM-037' HEAD --`; neither ID existed.
These entries do not overwrite another feature:

| ID | State | Priority | Scope |
|---|---|---|---|
| PM-029 | OPEN | Existing | Individual-stock scoring; baseline advisory Setup Quality and structural stop/target validation; safe shared valuation preparation; company names; independent sorting. Closure requires actual-data readiness and manual UAT. |
| PM-036 | Deferred | P2 | ETF/fund Swing scoring, ETF-specific Strategic methodology, authoritative classification, benchmarks, and Canadian/USD ETF validation. ETF holdings remain in portfolio valuation and FINAL ACTION. |
| PM-037 | Deferred | P2 | Advanced structural stop optimization, historical target-quality calibration, additional setup methodologies, extended backtesting. Does not duplicate PM-029's baseline column or validation. |

## Baseline

**Phase 10 re-entry note:** The live checkout was on the same branch at HEAD `5a5e1ea14709ce426c9992adfc848316b096dc2e`, with no staged diff. This differs from the baseline below and from other retained handoff records (41, 55, and 59 staged-path counts were recorded). The discrepancy is preserved without attributing authorship or reconstructing an earlier index; Phase 10 leaves the current index untouched.

- Branch: `feature/pm-029-final-review`
- HEAD: `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22`
- Existing staged paths: 41; raw `git diff --cached --binary` SHA-256: `e64aabfc810aba6158da2d0379ca4a3f587717ee1842ca02b1ea2deccf5149fd`
- Unstaged tracked paths: 0.
- Existing untracked deliverable: `docs/pm029-phase-2a-strategic-fundamentals-investigation.md`, SHA-256 `060d8cbdbc694a0ff14c446f295d0b5ba2efe672f2783a9dfa7aa744392f8d80`
- Baseline discrepancies are preserved, not repaired: earlier notes described 18 staged files and another staged-diff hash; the actual current index has 41 staged paths.
- An API process is already running and holds the normal backend build outputs. Do not stop it; use isolated build output directories.

## Completed prior phases

| Phase | State | Confirmed result |
|---|---|---|
| 0A–0C | COMPLETED | Shared scanner Preview/Persist/ReadOnly and FINAL ACTION parity implementation is in the accepted baseline; SQL Server concurrency remains unverified. |
| 1A–1B | COMPLETED | Swing-v2 uses the approved fixed S1–S5 denominator; Strategic-v1 remains unchanged. Historical snapshot and tests are documented. |
| 2A | COMPLETED | Read-only snapshot 6 analysis: 204/224 Strategic scores; CCA.TO passes T7. Full report is in `pm029-phase-2a-strategic-fundamentals-investigation.md`. |

## Remaining phases

### Phase 2B — Strategic fundamental evidence integrity

**State:** COMPLETED  
**Approval:** Non-policy, backward-compatible corrections are authorized. Persisted provenance/schema, scoring eligibility, F2 fallback, T8 signal definitions, and freshness changes are not authorized.

- Requirements: explain factor availability accurately; correct misleading Piotroski terminology; add only verified, non-persistent evidence; preserve the 204/224 snapshot results and FINAL ACTION.
- Files examined: PM-029 specification, Phase 1B and 2A reports, scoring/value-screener code, Angular and .NET project instructions.
- Files modified: `backend/PortfolioManager.Api/Services/Opportunities/OpportunityScoringEngine.cs`, `backend/PortfolioManager.Api/Services/ValueScreenerService.cs`, `backend/PortfolioManager.Api/Models/ValueScreenerModels.cs`, `backend/PortfolioManager.Tests/Pm029CalculatorTests.cs`, and `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunities-logic.ts`.
- Implemented: clarified that F4's stored indicators are proxy-based and not a Piotroski F-score; corrected only explanatory names/comments and the UI reason label. Legacy JSON field/reason identifiers and all calculation/eligibility logic remain unchanged.
- Confirmed findings: no source-value or alternate-origin repair can recover a T7 failure from snapshot 6 without new data or policy changes. Statement dates and authoritative instrument type are not persisted.
- Tests: `Pm029CalculatorTests` passed (72/72); targeted Angular opportunity logic tests passed (15/15). The new backend assertion confirms the fixture's Strategic score remains 84 while the F4 label discloses proxies.
- Remaining: persistent statement/provider provenance and any scoring eligibility/policy change still require approval.
- Blocked decisions: persistent statement/provider provenance, instrument classification contract, and any factor/scoring rule changes.
- Exact next task: complete the Phase 3 source/mapping audit and make only a presentation correction supported by existing signed relative-return evidence.

### Phase 3 — Sector / Theme Heat

**State:** COMPLETE — presentation-only improvement  
**Approval:** Reuse existing Dashboard calculation and already-verified mappings only. New benchmark or taxonomy policy requires approval.

- Requirements: independent context-momentum colour and signed relative return; completed-session and currency alignment; mandatory listed securities; accessible responsive details.
- Findings: Phase 2A identified 21 unmapped and 13 unclassified 60D rows; CCA.TO has unmapped 60D evidence.
- Files examined: `MarketLeadershipCalculator`, `MarketLeadershipService`, `SectorRelativeCalculator`, `SectorClassificationResolver`, Sector Heat badge/template/styles, Phase 2A snapshot report, and the PM-029 implementation spec.
- Findings: Opportunity Sector Heat already calculates signed 20D/60D relative returns through `SectorRelativeCalculator`; current verified mapping gaps (21 unmapped and 13 unclassified 60D rows; CCA.TO unmapped) cannot be resolved safely by guessing. Dashboard leadership is a different calculation, not a sector taxonomy source.
- Implemented direction-specific Sector Heat styling distinct from score fills. Calculations, existing mappings, and thresholds were not changed.
- Responsive screenshot coverage includes heatmap views on desktop, tablet, Samsung S21, and Samsung S23.
- Blocked decisions: any unverified benchmark mapping, new taxonomy source, or changed Dashboard threshold.
- Remaining: verified benchmark/taxonomy gaps stay explicit; new mapping or threshold policy requires approval.

### Phase 4 — CAD valuation and Portfolio Fit

**State:** BLOCKED — APPROVAL for shared financial implementation  
**Approval:** Architecture, isolated prototype, and verification design only.

- Requirements: common CAD-equivalent valuation authority, FX provenance, account/role-aware Portfolio Fit semantics, shared-consumer reconciliation.
- Files modified/tests: none.
- Blocked decisions: shared accounting/FX/Allocation/EOD/history contracts, DTO/schema changes, production consumer integration.
- Exact next task: produce the common-currency architecture and component-level reconciliation proposal without modifying shared production services.

### Phase 5 — Setups, Target Quality, Manual R

**State:** Manual R COMPLETE; structural stop/target activation BLOCKED.

- 5B requirements: shadow/replay proposed structural protection without production activation or use of saved plans/orders/cost basis.
- 5C requirements: display-only target-quality calibration, including LAC.TO, ETHH-B.TO, and BTCC.TO; no cap or target changes.
- 5D requirements: ticker-specific ephemeral manual scenario; no persistence or FINAL ACTION/scoring influence.
- Implemented an ephemeral, ticker-specific Manual R calculator with validation, percentages, accessible risk/reward visual, and ticker-change reset. It does not persist or feed score, Market R:R, saved plans, or FINAL ACTION.
- Captured Manual R and unavailable-setup views across the four supported viewports.
- Blocked decisions: production structural-stop resolver and Target Quality activation.
- No stop methodology, Target Quality policy, or high-R cap was activated. These remain separate approval gates.

### Phase 6 — Strategy grid and identity

**State:** COMPLETE within existing grid and preference contracts.

- Requirements: independent Swing/Strategic defaults, column/order/sort/filter preferences, descriptions, responsive fixes, N/A-last sorting, exports.
- Added independent Swing/Strategic column and view preference profiles while preserving the single 39-column registry and legacy preference migration.
- Added profile isolation, migration, reset, export, and sort coverage.
- Blocked decisions: none for existing supported preferences; any new persisted backend/API identity contract requires approval.
- Existing columns, filters, score colors, exports, and responsive patterns were preserved; no backend identity contract was added.

### Phase 7 — Calendar, freshness, history

**State:** COMPLETE for the demonstrated synthetic fixture defect; other policy/schema work remains out of scope.

- Requirements: exchange-specific completed sessions and holidays, synthetic Saturday fixture correction, immutable snapshot/version semantics, failure preservation.
- Synthetic Opportunity fixture sessions now roll back to a weekday. Added regression coverage; no snapshot schema, retention, or production calendar policy was changed.
- Blocked decisions: snapshot retention/schema changes.
- Failed refresh preservation remains covered by the existing isolated Opportunity tests.

### Phase 8 — Regression and final UAT package

**State:** AUTOMATED VERIFICATION COMPLETE; manual UAT PENDING.

- Full backend Release: 1,258 passed, 0 failed, 4 SQL Server-dependent skipped.
- Angular: 699 passed across 77 files; production build passed with existing SCSS budget warnings.
- Full Playwright: 1,033 passed, 179 skipped, 0 failed across desktop Chrome, tablet, mobile S21, and mobile S23.
- Targeted PM-029 E2E: 20 passed; separate opt-in screenshot capture: 4/4 viewports passed.
- Screenshots: 42 synthetic/API-mocked images in `docs/pm029-final-screenshots/`.
- Final report and UAT checklist: `docs/pm029-final-implementation-and-uat-report.md`.
- Manual UAT, shared CAD/FX Portfolio Fit, production stop/target methodology, and SQL Server-specific concurrency remain unresolved/approval-gated.

### Phase 10 — Portfolio Fit and Manual R integrity

**State:** SAFE CORRECTION AND AUTOMATED VERIFICATION COMPLETE; LIVE-UNIVERSE VALIDATION AND USER UAT BLOCKED/PENDING. PM-029 remains OPEN.

- Corrected one evidence-status gap: when the sector is known and has a configured target but sector exposure is unavailable, Portfolio Fit now returns `Review` with `SectorExposureUnavailable`; it does not invent a sector weight or headroom. Unknown-sector, no-target, allocation-unverifiable, and limit semantics remain unchanged.
- Added focused .NET, Angular, and browser regressions for the Fit reason, invalid Manual R values, currency labels, and Manual R non-interference with Swing/Strategic score evidence, Market R, Fit, Sector Heat, and FINAL ACTION.
- Focused .NET Release filter: 196 passed. Opportunity-logic Angular tests: 21 passed. Angular production build passed with existing SCSS budget warnings. PM-029 Playwright: 56 passed across desktop Chrome, tablet, Samsung S21 (384 px), and Samsung S23 (360 px).
- Captured and verified 70 watermarked synthetic/API-mocked screenshots in `docs/screenshots/pm029-phase-10-integrity-uat/`; archive: `docs/screenshots/pm029-phase-10-integrity-uat.zip`.
- No scoring formula, shared FX/accounting/valuation service, FINAL ACTION, stop/target method, schema, database, or production data was changed.
- Full-universe mapping/score/Fit coverage remains unverified: the test fixtures are synthetic and the existing Phase 9 discovery did not find an available SQL Server or current sanitized universe export.
- Mixed-currency Portfolio Fit remains unavailable/unverifiable until an authoritative common-currency policy is approved. Quote freshness and mapped-Canadian-security Manual R currency questions also remain open; no policy was inferred.
- User manual UAT and real-universe validation remain required before PM-029 can close. Full evidence and follow-up gates: `docs/pm029-phase-10-integrity-and-uat-report.md`.

## Program-wide invariants

- Preserve the staged baseline; do not stage files.
- Do not change branches, commit, push, pull, merge, rebase, create PRs, deploy, migrate, or seed.
- No production scoring-policy, FINAL ACTION, stop/target, shared CAD/FX, or protected financial-state change without approval.
- Manual R stays ephemeral and separate from Market R and saved plans.
- Do not modify PM-035.
- Distinguish real snapshot evidence from synthetic fixtures and screenshots.
- PM-029 remains OPEN until explicit user acceptance.
