# PM-029 Final Implementation and UAT Report

**Disposition:** Authorized PM-029 work and regression verification are complete for this handoff. PM-029 remains **OPEN** until manual UAT and explicit user approval. Approval-gated work has not been activated.

## Executive summary

- Implemented the authorized PM-029 scoring, detail-drawer, presentation, preference, and test-fixture work without changing the approved Swing-v2 scoring formula or FINAL ACTION rules.
- The Swing score is independent of trade setup availability. The saved CCA.TO example is scoreable while its protective stop and Market R:R remain unavailable.
- Retained the existing 39-column registry and filters; added separate Swing/Strategic view and column preference profiles. Sector Heat changes are presentation-only. Manual R is ephemeral and ticker-specific.
- The full Playwright suite completed with **1,033 passed, 179 skipped, 0 failed** across desktop Chrome, tablet, Samsung S21, and Samsung S23.
- Screenshots are **synthetic/API-mocked UAT captures**, not a live refresh or current account data.
- Portfolio Fit valuation/FX architecture and production structural-stop/target methodology remain blocked for separate approval. No migration, seed, live Opportunity refresh, commit, push, merge, or deployment was performed.

## 1. Baseline and preservation

| Item | Verified value |
|---|---|
| Branch | `feature/pm-029-final-review` |
| Starting and final HEAD | `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22` |
| Staged paths | 41 |
| Staged binary diff SHA-256, before and after | `e64aabfc810aba6158da2d0379ca4a3f587717ee1842ca02b1ea2deccf5149fd` |
| Index changes during this continuation | None |
| Whitespace checks | `git diff --check` and `git diff --cached --check` passed |

The 41 staged paths and their content were preserved exactly. New continuation work remains unstaged/untracked. The changed paths relate to PM-029 behavior, its tests/reports/screenshots, and shared UI controls needed for the PM-029 strategy profiles; no PM-035 implementation files were changed. The unrelated pre-existing API process was not stopped.

No branch switch, reset, clean, checkout, staging, commit, push, pull, merge, rebase, database migration/seed, deployment, broker action, or live/protected Opportunity refresh was performed.

## 2. Phase disposition

| Phase | Result | Boundary / remaining gate |
|---|---|---|
| 0C — read-only preview and FINAL ACTION parity | Complete in the preserved baseline | SQL Server-specific isolation/concurrency is still unverified; conditional Universe Value Screener persistence remains an existing separate write boundary. See [Phase 0C report](./pm029-phase-0c-readonly-preview-report.md). |
| 1A — Swing formula validation | Complete | The saved 224-row evidence supported the approved fixed S1–S5 denominator; details and formula comparison are in [Phase 1A report](./pm029-phase-1a-swing-score-validation.md). |
| 1B — Swing-v2 implementation | Complete | Uses approved fixed denominator and evidence gate; S6/setup is separate. No scoring-policy expansion. See [Phase 1B report](./pm029-phase-1b-implementation-report.md). |
| 2A — Strategic fundamentals investigation | Complete | Snapshot evidence and exact unavailability causes are documented in [Phase 2A report](./pm029-phase-2a-strategic-fundamentals-investigation.md). |
| 2B — fundamental evidence terminology | Limited correction complete | Corrected the F4 explanatory label to disclose proxy signals; no scoring, eligibility, provenance schema, or persisted field changes. New fundamental provenance remains a separate decision. |
| 3 — Sector / Theme Heat | Presentation correction complete | Direction-specific heat styling is distinct from score fills; relative-return calculations, mappings, and thresholds were not changed. Unmapped/unclassified evidence remains explicit. |
| 4 — CAD valuation and Portfolio Fit | **Blocked** | No shared accounting, FX, Allocation, or valuation contract was changed. Reuse/contract design requires a separate proposal and approval. |
| 5 — trade setup and Manual R | Manual R complete; production stop/target work blocked | Manual R is local-only, clears on ticker change, and does not influence score, Market R:R, saved plans, or FINAL ACTION. No structural stop, target, Target Quality, or high-R cap was activated. |
| 6 — strategy grid and identity | Complete within existing contracts | Independent Swing/Strategic filters, sort, and column preferences; single 39-column registry retained. |
| 7 — calendar/freshness/history | Demonstrated fixture defect corrected | Synthetic Opportunity market-session dates now roll back to weekdays. No snapshot schema/retention or production calendar policy change. |
| 8 — regression and UAT package | Complete for automated verification | Results and manual checklist are below. Manual UAT is still required. |

## 3. Scoring and saved-snapshot evidence

The historical replay used the saved 224-security Opportunity snapshot documented in the Phase 1B and Phase 2A reports. It replays stored component evidence; it is **not** a fresh market scan and does not claim that provider data or indicators were refreshed.

- Swing-v2 uses the approved fixed 80-point technical pool: `round-half-away-from-zero(100 × awarded S1–S5 / 80)`.
- S1 and S3 remain essential, and the existing 70% evidence-completeness gate and score bands remain.
- In the saved replay, the former S6 dependency made 219 rows unscorable. Under the approved formula, 224 were technically scorable: **130 Verified and 94 Provisional**.
- Missing technical inputs remain explicitly missing; they are not treated as observed weakness. The fixed denominator avoids the score inflation found in available-evidence normalization when S2 or other nonessential evidence is unavailable.
- CCA.TO's stored S1–S5 evidence gives a Swing score of **28, Provisional**. Its setup has no valid stop, so Market R:R remains unavailable; no entry, stop, target, or reward is fabricated.
- Phase 2A found **204/224 Strategic scores**. CCA.TO passes the existing T7 gate; the saved evidence does not justify relaxing the T7 factor minimum or claiming score recovery through a policy change.
- The saved high-R values for LAC.TO, ETHH-B.TO, and BTCC.TO were arithmetically consistent with their stored entry/stop/target data and existing ATR gate. The target inputs were 52-week highs, making the ratios sensitive to distant targets. No arbitrary cap, target replacement, or scoring manipulation was made.
- No production scoring caches or Opportunity snapshots were populated with experimental scores.

## 4. Implemented UI and workflow changes

- Added independent saved Swing/Strategic grid-column and view preference profiles while retaining one 39-column selector/registry. Existing single-profile preferences migrate compatibly into both profiles; reset is profile-local.
- Kept score filters, sort behavior, exports, score coloring, Sector Heat, and responsive presentation tied to the active perspective.
- Completed the details drawer's Overview, Technicals, Scores, and Evidence presentation with readable component evidence and numeric formatting.
- Added a clearly separate ephemeral Manual R calculator with validation, risk/reward percentages, and a visual. It clears when the ticker changes and is not persisted.
- Prevented duplicate company subtitles when the company name equals the ticker.
- Corrected synthetic E2E session dates that fell on weekends.
- Preserved the PM-029 refresh safety boundary and last-successful-snapshot behavior on failed refreshes.

No shared FINAL ACTION decision logic, scanner lifecycle, saved risk plan, broker stop, portfolio/accounting record, security mapping, or signal lifecycle policy was intentionally changed in this continuation.

## 5. Portfolio Fit, stops, and remaining approval gates

### Portfolio Fit

Portfolio Fit remains blocked pending approval of a common CAD-equivalent valuation and FX source/account-scope contract. No warning was suppressed, no unverified FX conversion was substituted, and no shared accounting/FX/Allocation service was changed. The existing proposal is in the session artifact `pm029-common-currency-valuation-proposal.md`; it is not authorization to implement the shared contract.

### Protective stops and Target Quality

The scanner's existing `DynamicStopLoss` remains the current setup evidence. A new-entry protective-stop methodology was not activated. No stop, target, or Reward value is inferred when required inputs are absent. Any changed stop methodology, production setup contract, or Target Quality policy requires a separate explicit decision.

### Phase 0C write boundary and protected state

Phase 0C's shared calculation path retains separate Preview/Persist/ReadOnly modes and routes PM-029 through the authoritative FINAL ACTION engine in preview mode. Its isolated SQLite/InMemory checks cover tracked-entity and write-boundary behavior; they do not prove SQL Server provider-specific isolation or cross-process concurrency.

The existing conditional Universe Value Screener snapshot write remains reachable from the normal Opportunity refresh path and was not silently removed. No live refresh was run here. Final UI/E2E runs used mocked APIs, and backend tests used isolated fixtures; no protected live financial, signal, mapping, FINAL ACTION, or broker-risk records were intentionally written.

## 6. Automated verification

| Verification | Result |
|---|---|
| Full backend Release suite | **1,258 passed, 0 failed, 4 skipped** |
| Angular unit suite | **699 passed across 77 test files** |
| Angular production build | Passed; existing SCSS budget warnings only |
| Full Playwright suite | **1,033 passed, 179 skipped, 0 failed** (1,212 total; 12.8 minutes) |
| Playwright projects | `desktop-chrome`, `tablet`, `mobile-s21`, `mobile-s23` |
| Targeted PM-029 Trading Opportunities E2E | **20 passed** |
| Opt-in four-viewport screenshot flow | **4/4 passed** |
| `git diff --check` / `git diff --cached --check` | Passed |

The four backend skips are SQL Server-dependent tests: three `AccountTypeSqlServerTests` and one `Pm033IsolatedSqlTests`. They require an isolated SQL Server test environment. SQLite and EF InMemory do not replace SQL Server concurrency/write-isolation verification. The full Playwright run intentionally skipped opt-in screenshot cases; screenshot capture was run separately on all four viewports. No test failures were reported in the full Playwright run.

## 7. Screenshots

All 42 images in [`pm029-final-screenshots/`](./pm029-final-screenshots/) are **synthetic/API-mocked**. They demonstrate the UI and responsive behavior; they are not evidence of live market data or actual portfolio values.

### Desktop Chrome

![Desktop Sector Heat](./pm029-final-screenshots/desktop-chrome-01-heatmap.png)

![Desktop Manual R](./pm029-final-screenshots/desktop-chrome-02b-manual-r.png)

### Tablet

![Tablet CCA.TO score without a setup](./pm029-final-screenshots/tablet-04-swing-no-setup.png)

### Samsung S21 (384 px)

![Samsung S21 details drawer](./pm029-final-screenshots/mobile-s21-02c-technicals.png)

### Samsung S23 (360 px)

![Samsung S23 score without a setup](./pm029-final-screenshots/mobile-s23-04-swing-no-setup.png)

The directory also contains Overview, Technicals, Scores, Evidence, multiple FINAL ACTION decisions, columns, heat, and Manual R captures for the applicable viewport.

## 8. Manual UAT checklist

Perform UAT in an approved non-production/test environment. Do not use this report as authorization for a live refresh or protected-record write.

- [ ] Confirm CCA.TO can show its independent technical Swing score while setup and Market R:R remain unavailable.
- [ ] Review all four drawer tabs; verify component points, denominator, completeness, missing reasons, and displayed precision.
- [ ] Verify Weak/Provisional score presentation, filters, sorting, exports, and active Swing/Strategic perspective.
- [ ] Confirm the 39-column selector and existing filters are preserved; changing a perspective restores its own columns, filters, and sort.
- [ ] Review Sector Heat colors without interpreting unmapped evidence as a verified benchmark.
- [ ] Check 360 px and 384 px layouts, tablet, and desktop for readable cards/drawer controls and no unintended overflow.
- [ ] Verify Manual R validation and ticker reset; confirm it does not change Market R:R, scores, FINAL ACTION, or saved plans.
- [ ] Compare FINAL ACTION labels, rationale, actionability, and role/account context with the authoritative engine.
- [ ] Confirm missing setup values remain unavailable and review LAC.TO, ETHH-B.TO, and BTCC.TO without assuming distant 52-week highs are executable targets.
- [ ] Confirm a failed Opportunity refresh retains the last successful snapshot in the approved test environment.
- [ ] Verify signal, staged-signal history, security mappings, holdings, transactions, cash/options, saved plans, and broker-risk records remain unchanged in the approved isolated environment.

## 9. Recommendation and next decisions

**Recommendation:** The authorized Swing score/UI corrections and automated regressions are ready for manual review. Do **not** close PM-029. Before further implementation, obtain separate approval for:

1. Any changed protective-stop or target methodology and its replay evidence.
2. The shared CAD valuation/FX/account-scope contract needed for reliable Portfolio Fit.
3. Persistent fundamental/instrument provenance or any Strategic eligibility/scoring change.
4. SQL Server relational integration and concurrency verification if an isolated SQL Server test environment is made available.

PM-035 remains separate. No GitHub operation, commit, deployment, live refresh, migration, or seed was performed.
