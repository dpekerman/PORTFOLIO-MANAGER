# PM-029 Phase 9A — Post-Implementation Integrity Audit

**Audit date:** 2026-11-15
**Disposition:** **BLOCKED FOR SIGN-OFF — confirmed test-evidence and requirements gaps remain.**
**Scope:** Read-only source/report inspection, isolated tests, and synthetic/API-mocked screenshots. PM-029 remains **OPEN**.

## Executive decision

This audit did **not** establish a production scoring-calculation defect. The backend uses one `SectorRelativeResult` for Swing S2, Strategic T2, Sector Heat, and the matching technical indicator fields. However, the current synthetic UI fixture assigns scores and indicator values independently of that evidence, so screenshots cannot be trusted as scoring-parity proof. The default grid also does not meet the Phase 9A requirement for distinct Swing and Strategic defaults.

Other material acceptance gaps are not safe to resolve by inference: the requested “sector momentum strength” display conflicts with the approved relative-return Sector Heat definition; no common-currency valuation proposal or structural-stop shadow/replay evidence was found; and the requested per-security instrument/source/mapping audit cannot be revalidated without a current approved sanitized snapshot export. Do not change production scoring, financial logic, mappings, database schema, or database contents on the basis of this report.

**Smallest next phase:** authorize a bounded correction package for (1) synthetic fixture/evidence consistency and (2) perspective-specific default columns using the exact approved column lists; separately decide Sector Heat semantics and provide the missing valuation/stop proposals and sanitized evidence. Keep production scoring and database writes out of that correction package unless explicitly authorized.

## 1. Authorization and preservation

- No application source, tests, scoring rules, financial logic, migration, seed, or PM-035 file was changed.
- No database was connected to or modified. No Opportunity refresh, Value Screener run, live Yahoo/provider request, or protected portfolio/account data access occurred.
- No branch switch, stage/unstage, reset, clean, checkout, commit, push, pull, merge, or deployment occurred.
- The only deliverables from this audit are this report and 38 synthetic screenshot PNGs in [`pm029-final-screenshots/`](./pm029-final-screenshots/).

| Preservation check | Audit-start value |
|---|---|
| Branch | `feature/pm-029-final-review` |
| HEAD | `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22` |
| Staged paths | 55 |
| Unstaged tracked paths | 0 |
| Raw SHA-256 of `git diff --cached --binary` | `876892977f1b7a2e50c55d9af1a221f63cfee94995728a1679a41f558db05f7f` |
| Audit-start untracked paths | 38 screenshot PNGs |

**Prior-report discrepancy:** the PM-029 master and final UAT reports state 41 staged paths and hash `e64aabfc810aba6158da2d0379ca4a3f587717ee1842ca02b1ea2deccf5149fd`. The index observed at this audit’s start has 55 paths and a different raw-byte hash. This discrepancy predates this audit; its cause was not inferred, and the index was not changed. The post-audit check is recorded in §11.

## 2. Evidence labels and historical-record limits

- **Current source:** inspected locally; no runtime refresh or database read.
- **Historical snapshot evidence:** claims copied from prior Phase 1A/1B/2A reports. They are archival evidence, not independently revalidated in Phase 9A.
- **Synthetic evidence:** screenshots and E2E data use mocked APIs and invented fixture companies/values. They do not represent current securities, portfolio balances, or live market data.
- **Unverified:** not established by current source or a safe, current row-level record. Unknown instrument type, mapping, or benchmark is not filled in by ticker-name inference.

Earlier reports describe different access states: Phase 0C said snapshot 5 was inaccessible; later Phase 1A/1B/2A reports document read-only analysis of snapshot 6. This audit did not connect to either database snapshot. Treat the later snapshot findings as the historical record only; do not interpret this audit as a current refresh or a renewed snapshot verification.

## 3. Confirmed findings

### 3.1 Synthetic score/evidence inconsistency — confirmed fixture defect, not a production-score finding

The backend’s S2 and T2 calculations consume the same completed-session `D20`/`D60` SectorRelativeResult that populates the row’s Sector Heat and `Rs20Pp`/`Rs60Pp` indicators ([OpportunityScoringEngine.cs](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityScoringEngine.cs:147), [OpportunityService.cs](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityService.cs:268), [OpportunityService.cs](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityService.cs:337)). The approved scoring contract gives Swing S2 14/20D points at +5 pp, 11 at +2 pp, 8 at +0.5 pp, 5 in the neutral band, 2 at −2 pp, and 0 below; 60D contributes up to 6. Strategic T2 uses the same 60D difference ([PM-029 scoring contract](./pm-029-trading-opportunities.md#31-swing-v2-technical-opportunity-80--essential-s1-s3)).

The Playwright fixture does not preserve that invariant:

- `heat(3.6, 'Positive', 2)` generates 20D +3.6 pp and 60D +5.76 pp, which imply **11 + 6 = 17/20** for S2, not 20/20 ([opportunities.ts](../frontend/portfolio-manager-ui/e2e/fixtures/opportunities.ts:185), [OpportunityScoringEngine.cs](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityScoringEngine.cs:147)).
- The same QRK fixture assigns an 88 Swing score and 100% completeness, while `scoreResult()` distributes that headline across component capacities independently of the indicator/heat fields. Its resulting S2 evidence is therefore not calculated from the displayed returns ([opportunities.ts](../frontend/portfolio-manager-ui/e2e/fixtures/opportunities.ts:218), [opportunities.ts](../frontend/portfolio-manager-ui/e2e/fixtures/opportunities.ts:352)).
- Fixture indicators set `rs20Pp` and `rs60Pp` to null, despite the row being shown as Matched with relative returns ([opportunities.ts](../frontend/portfolio-manager-ui/e2e/fixtures/opportunities.ts:155)).
- Synthetic CCA.TO is assigned a Matched +0.3 pp heat versus XMA.TO while its Swing score says S2 is missing because the heat is Unmapped. Those states cannot both describe the same evidence ([opportunities.ts](../frontend/portfolio-manager-ui/e2e/fixtures/opportunities.ts:254)).

These are confirmed **mock/test provenance defects**. They invalidate score-parity conclusions from those synthetic screenshots but do **not** prove that production scoring is wrong. The screenshots are useful for layout and interaction review only.

### 3.2 Perspective defaults — confirmed requirements gap

The registry currently defaults both perspectives to the same nine columns: Symbol, Swing Score, Strategic Score, Reward : Risk, Risk, Portfolio Fit, Final Action, Sector Heat, and Sector. All remaining registered columns, including the underlying 20D/60D heat fields and setup levels, are hidden by default ([grid-registry.ts](../frontend/portfolio-manager-ui/src/app/core/store/grid-registry.ts:478)). The unit test explicitly locks this common nine-column list ([grid-column.store.spec.ts](../frontend/portfolio-manager-ui/src/app/core/store/grid-column.store.spec.ts:150)).

Phase 9A requires different defaults for Swing and Strategic. That is not satisfied by the current clean-profile defaults. Do not guess the replacement lists from the current registry or alter saved user preferences; use the exact approved lists from the Phase 9A requirements in a separately authorized change.

### 3.3 Sector Heat does not measure absolute sector momentum

The implemented measure is stock price return minus benchmark price return over completed 20/60-session windows. Badge direction and intensity are classified from that **relative difference**, not from the sector/theme’s own absolute momentum ([SectorRelativeCalculator.cs](../backend/PortfolioManager.Api/Services/Opportunities/SectorRelativeCalculator.cs:12), [opportunities-logic.ts](../frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunities-logic.ts:316)). The approved PM-029 contract also describes relative alpha and signed 20-session heat ([PM-029 §4](./pm-029-trading-opportunities.md#4-sector-heat-stock-vs-sector-benchmark)).

The Phase 9A request’s “background = sector momentum strength” wording therefore conflicts with the existing contract. A relative outperformance badge cannot safely be relabeled as absolute sector momentum. Product/methodology approval is required to choose between retaining relative heat, adding a separate absolute-momentum measure, or another explicitly defined presentation. No recoloring or mapping change is authorized by this audit.

### 3.4 UI observations from the 38 current screenshots

All observations below are synthetic and visual, not evidence of live data:

- **Phone details tabs:** at 360/384 px, the four tab labels do not all fit in the initial viewport; Material’s tab-scroll arrows are visible. This is a discoverability issue, not proof that the hidden tabs are unreachable.
- **Column dialog:** the title is visibly ellipsized on phone-sized screenshots; the dialog and controls still open.
- **Tablet grid:** the container is `overflow: auto`, and the screenshot’s initial horizontal viewport does not show all registered/default columns. The captured image does not prove that the off-screen columns are inaccessible; it does show no strong visual cue that the grid can be horizontally scrolled.
- **Manual R:** the entry/stop/target scenario is clearly presented as temporary and separate from calculated Market R:R. The captured phone fields remain usable; no calculation error was observed in the mocked interaction.
- The screenshot flow does not capture the Strategic grid perspective, so the image set does not visually compare its clean-profile defaults with Swing.
- These are low-priority presentation/usability issues compared with the score-fixture and requirements gaps. No Playwright accessibility audit or assistive-technology test was run.

### 3.5 Screenshot-count discrepancy

The earlier final UAT report claims **42 images**. The current screenshot test writes nine desktop/tablet images and ten phone images per project, which totals **38** across the four configured projects ([trading-opportunities.spec.ts](../frontend/portfolio-manager-ui/e2e/trading-opportunities.spec.ts:218)). The current output directory contains exactly 38 PNGs. No four additional images were fabricated to match the older claim.

## 4. Cross-layer behavior and boundaries

| Area | Verified implementation | Audit conclusion |
|---|---|---|
| Swing/Strategic relative strength | S2 reads `D20` and `D60`; T2 reads `D60`; row heat and indicator fields are built from those same results. | Production data path is internally shared. Synthetic fixture scores/indicators break the expected consistency, as detailed in §3.1. |
| Sector Heat | Split-adjusted close return differences; completed-session alignment, matching currency, coverage checks, and explicit unavailable statuses. | Relative performance, not absolute sector momentum. Existing mapping status/data were not queried. |
| Market R:R | Calculated setup uses completed scanner data, scanner `DynamicStopLoss`, an eligible upper rail/open gap/52-week high target, ATR/risk validation, and the existing 1.5 R:R actionability threshold ([TradeSetupResolver.cs](../backend/PortfolioManager.Api/Services/Opportunities/TradeSetupResolver.cs:12)). | No structural stop or target-quality replacement is present in this path. The grid’s “Reward : Risk” is the calculated setup R:R, not Manual R. |
| Manual R | Local signal/form state; positive prices and `stop < entry < target`; resets on ticker change; does not affect Market R:R, scores, Fit, or Final Action ([opportunity-details-panel.component.ts](../frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunity-details-panel.component.ts:117)). | Correctly isolated in the current implementation; it is not a registered grid column and is not persistent. |
| FINAL ACTION | The opportunity service copies authoritative evaluation decisions; multiple differing decisions are presented neutrally. Accounts are shown at row level; individual FinalAction DTOs do not carry account identity (per the approved contract). | No recommendation is inferred from score/colour. Do not expand the decision/account contract without approval. |
| Portfolio Fit / valuation | Existing Final Action code sums stocks, cash and options for its denominator and detects mixed currency; it has no verified FX conversion and marks mixed-currency allocation unreliable ([FinalActionService.cs](../backend/PortfolioManager.Api/Services/FinalAction/FinalActionService.cs:855), [FinalActionService.cs](../backend/PortfolioManager.Api/Services/FinalAction/FinalActionService.cs:891)). | No shared CAD valuation proposal was found. Raw mixed-currency totals must not be presented as a verified CAD value; the existing fail-closed reliability gate is not a substitute for an approved FX/accounting contract. |
| Freshness/calendar | Per-row expected sessions use analysis-market metadata/suffix and a rule-based TSX or NYSE calendar ([TradingCalendar.cs](../backend/PortfolioManager.Api/Services/TradingCalendar.cs:3), [OpportunityService.cs](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityService.cs:247)). | Exchange enum has only TSX and NYSE; TSXV and other Canadian suffixes collapse to TSX, and US markets collapse to NYSE. Snapshot-level `DataAsOfSession` and stale comparison use TSX, while rows have their own expected sessions. Validate this against the exact per-exchange freshness requirement before changing it. |
| Fundamental freshness/provenance | Current Strategic age gate is based on `AnalyzedAt`; saved output does not preserve statement period-end or all raw factor inputs (documented in [Phase 2A](./pm029-phase-2a-strategic-fundamentals-investigation.md#4-fundamental-data-findings)). | A recent analysis timestamp alone does not establish recent underlying statements. Do not relax factor gates from the archived evidence. |

## 5. Requested-security evidence matrix

No current database or provider data was read in this audit. “Historical evidence” below refers only to the named prior reports; it does not establish the current instrument type, classification, source, benchmark mapping, or data availability. In particular, do not infer that a ticker is an ETF, wrapper/CDR, common stock, or a particular theme solely from its symbol.

| Security | Historical row-level evidence available in repository | Instrument type / current sector or theme / source / current benchmark | Safe proposed mapping now? | Approval if mapping is later proposed |
|---|---|---|---|---|
| `CCA.TO` | Snapshot 6 report: Swing 28 Provisional; S2/T2 relative evidence unmapped; Strategic 54 Provisional; T7 Partial 25/25. No current re-read ([Phase 1B](./pm029-phase-1b-implementation-report.md#cca-to-no-setup-result), [Phase 2A](./pm029-phase-2a-strategic-fundamentals-investigation.md#5-cca-to--correction-to-the-reported-t7-failure)). | Not currently verified. Prior saved row says no underlying mapping; it does not establish a current classification or benchmark. | No. | Yes—taxonomy, source provenance, market, instrument eligibility, and benchmark methodology. |
| `NKE.TO` | No specific saved-row evidence found in the reviewed reports. | Unverified. | No. | Yes. |
| `NOVO.TO` | Phase 1A/1B archived comparison identifies missing S2 sector-relative evidence; not a current mapping check. | Unverified. | No. | Yes. |
| `QSR.TO` | Phase 1A archived comparison identifies missing S2 sector-relative evidence; not a current mapping check. | Unverified. | No. | Yes. |
| `RCI-B.TO` | Phase 1A archived report identifies missing S2 alongside strong S3; exact current heat status is not revalidated. | Unverified. | No. | Yes. |
| `T.TO` | No specific saved-row evidence found in the reviewed reports. | Unverified. | No. | Yes. |
| `WELL.TO` | No specific saved-row evidence found in the reviewed reports. | Unverified. | No. | Yes. |
| `QQQ` | No specific saved-row evidence found in the reviewed reports. | Unverified. | No. | Yes. |
| `SPY` | No specific saved-row evidence found in the reviewed reports. | Unverified. | No. | Yes. |
| `SMH` | Phase 2A archived snapshot: one of 17 T7 failures due to fewer than two eligible valuation factors. This says nothing conclusive about its sector benchmark. | Unverified. | No. | Yes. |
| `SLV` | Phase 2A archived snapshot: one of 17 T7 failures due to fewer than two eligible valuation factors. | Unverified. | No. | Yes. |
| `TAN` | Phase 2A archived snapshot: one of 17 T7 failures due to fewer than two eligible valuation factors. | Unverified. | No. | Yes. |
| `URA` | Phase 2A archived snapshot: one of 17 T7 failures due to fewer than two eligible valuation factors. | Unverified. | No. | Yes. |
| `WEAT` | Phase 2A archived snapshot: one of 17 T7 failures due to fewer than two eligible valuation factors. | Unverified. | No. | Yes. |
| `XCLN.TO` | Phase 2A archived snapshot: one of 17 T7 failures due to fewer than two eligible valuation factors. | Unverified. | No. | Yes. |
| `XNDU.TO` | Phase 2A archived snapshot: one of 17 T7 failures due to fewer than two eligible valuation factors. | Unverified. | No. | Yes. |
| `TDOC.TO` | Phase 2A archived snapshot: one of 17 T7 failures due to fewer than two eligible valuation factors. | Unverified. | No. | Yes. |

**Mapping decision:** do not activate or seed any new mapping. The archived reports identify broad gaps (21 unmapped and 13 unclassified 60D rows) and specifically document CCA.TO as unmapped, but they do not supply current, per-security instrument/source provenance for this requested list. A sanitized export must contain only approved non-sensitive row evidence (symbol, analysis identity/type, sector/theme and source, current mapping/benchmark, relevant data status and timestamps); omit account identifiers, quantities, costs, balances, and credentials.

## 6. Requirements-gap matrix

| Requirement area | Status | Evidence / gap |
|---|---|---|
| Technical scoring independent of trade setup | **Implemented; historical evidence only** | Current scoring path is separate from `TradeSetupResolver`. Archived CCA.TO report supports the intended example, but this audit did not read the snapshot. |
| Cross-layer score/heat/indicator evidence | **Confirmed synthetic-fixture defect** | QRK and CCA fixture contradictions in §3.1; production defect not established. |
| Separate Swing and Strategic clean-profile columns | **Not met** | Both currently resolve to the same nine defaults; existing unit test enshrines them. |
| Sector/theme momentum strength presentation | **Requirements conflict** | Existing badge is stock-vs-benchmark relative return. “Momentum strength” is not a field in the current heat DTO. |
| Manual R isolation | **Implemented** | Temporary local scenario, ticker reset, no effect on authoritative values; no grid column. |
| Shared CAD valuation / Portfolio Fit | **Blocked / proposal absent** | Existing mixed-currency check fails closed; no approved FX source, CAD conversion or shared-service contract was found. |
| Structural stop / Target Quality / high-R calibration | **Not evidenced; not activated** | Current setup still uses scanner `DynamicStopLoss` and existing target candidates. No approved structural-stop formula or 224-security shadow/replay report was found. |
| Refresh write boundary | **Partially verified historically; SQL Server gap** | PM-029 refresh is expected to persist Opportunity snapshot/refresh state and may persist Universe Value Screener results. Prior SQLite/InMemory checks do not prove SQL Server isolation or cross-process behavior. |
| Mobile/tablet usability | **Partial** | Current captures show clipped tab labels/title and initially off-screen grid columns; no confirmed blocked control. |
| Per-exchange freshness | **Partial / needs policy verification** | Row expected sessions are exchange-aware within two calendar enums; snapshot-level date is TSX-based; statement-period provenance is absent. |
| 179 Playwright skips | **Unverified by suite-level breakdown** | The historical final report states 179 skipped, but its stored result does not provide a usable exact per-suite list. This audit did not rerun the full suite or invent counts. |
| Screenshot inventory | **Corrected** | Current run has 38, not the 42 claimed in the prior report. |
| SQL Server persistence/concurrency | **Not verified** | No SQL Server test environment was used. The four historical backend skips are three `AccountTypeSqlServerTests` and one `Pm033IsolatedSqlTests`; they are not a PM-029 SQL Server concurrency suite. |

## 7. Valuation and structural-stop approval gates

### Common-currency valuation

The Phase 4 portion of the prior final report is explicitly **Blocked**. No separate shared CAD valuation proposal was found. Before any implementation, the owner must approve:

1. authoritative FX source, timestamp/as-of rules, conversion direction, and stale/missing-rate behavior;
2. explicit currency and provenance for holdings, cash, options, and any shared/global rows;
3. user/account scope and treatment of shared `UserId == null` cash/options;
4. consumers that must agree (Final Action, Portfolio, Dashboard, Allocation, and PM-029 Fit);
5. whether and how historical snapshots are recomputed, with PM-035 external cash-flow invariants preserved;
6. an isolated SQL Server test plan and rollback/replay plan.

Do not treat the current raw multi-currency sum as CAD or use a new FX assumption to make Fit available.

### Structural stop and target quality

The current setup consumes the scanner’s `DynamicStopLoss`; it does not calculate the proposed structural stop. The reviewed repository reports contain no shadow result for the 224-security snapshot and no reproducible evidence for the proposed 0.25-ATR buffer or 3.0-ATR threshold. Before production activation, approve the stop/target formulas, source fields, event/corporate-action handling, rejection reasons, no-lookahead replay method, comparison metrics, and exact rollback boundary. A separately authorized shadow/replay-only phase is the safe next step; do not alter the current production resolver based on unverified examples.

## 8. Test and screenshot evidence

| Audit run | Result | Scope |
|---|---|---|
| Backend PM-029 filtered tests | **109 passed, 0 failed, 0 skipped** | `dotnet test backend\PortfolioManager.Tests\PortfolioManager.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Pm029"` |
| PM-029 Playwright functional spec | **40 passed, 4 skipped** | 44 project/test instances over desktop Chrome, tablet, mobile S21 and mobile S23; screenshot-gated cases were skipped because the capture environment variable was unset. Mocked APIs; no backend/database. |
| Separate PM-029 screenshot capture | **4/4 project runs passed; 38 PNGs generated** | Desktop/tablet: 9 each; mobile S21/S23: 10 each. API mocked and values synthetic. |
| Full Playwright regression | **Not rerun** | The earlier report’s 1,033 passed / 179 skipped is historical. Exact breakdown of the 179 skips is not available from the saved result reviewed here. |
| Angular unit suite/build | **Not rerun** | No Angular source or test was changed in this audit. |
| SQL Server integration | **Not run** | No SQL Server connection or writes were authorized. |

Known source-level skip patterns include device-conditional responsive tests, the opt-in PM-029 screenshot test, and the permanently skipped `signal effectiveness` suite. Static inspection does not establish the historical 179 skips’ exact per-suite counts.

### All current screenshots

All links below point to the 38 current synthetic/API-mocked captures. They are layout evidence only.

**Desktop Chrome (9):** [01 heatmap](./pm029-final-screenshots/desktop-chrome-01-heatmap.png) · [02 overview](./pm029-final-screenshots/desktop-chrome-02-overview.png) · [02b Manual R](./pm029-final-screenshots/desktop-chrome-02b-manual-r.png) · [02c multiple decisions](./pm029-final-screenshots/desktop-chrome-02c-details-multiple.png) · [02c Technicals](./pm029-final-screenshots/desktop-chrome-02c-technicals.png) · [02d Scores](./pm029-final-screenshots/desktop-chrome-02d-scores.png) · [02e Evidence](./pm029-final-screenshots/desktop-chrome-02e-evidence.png) · [03 columns](./pm029-final-screenshots/desktop-chrome-03-columns.png) · [04 Swing without setup](./pm029-final-screenshots/desktop-chrome-04-swing-no-setup.png)

**Tablet (9):** [01 heatmap](./pm029-final-screenshots/tablet-01-heatmap.png) · [02 overview](./pm029-final-screenshots/tablet-02-overview.png) · [02b Manual R](./pm029-final-screenshots/tablet-02b-manual-r.png) · [02c multiple decisions](./pm029-final-screenshots/tablet-02c-details-multiple.png) · [02c Technicals](./pm029-final-screenshots/tablet-02c-technicals.png) · [02d Scores](./pm029-final-screenshots/tablet-02d-scores.png) · [02e Evidence](./pm029-final-screenshots/tablet-02e-evidence.png) · [03 columns](./pm029-final-screenshots/tablet-03-columns.png) · [04 Swing without setup](./pm029-final-screenshots/tablet-04-swing-no-setup.png)

**Samsung S21 (384 px; 10):** [01 heatmap](./pm029-final-screenshots/mobile-s21-01-heatmap.png) · [02 overview](./pm029-final-screenshots/mobile-s21-02-overview.png) · [02a overview levels](./pm029-final-screenshots/mobile-s21-02a-overview-levels.png) · [02b Manual R](./pm029-final-screenshots/mobile-s21-02b-manual-r.png) · [02c multiple decisions](./pm029-final-screenshots/mobile-s21-02c-details-multiple.png) · [02c Technicals](./pm029-final-screenshots/mobile-s21-02c-technicals.png) · [02d Scores](./pm029-final-screenshots/mobile-s21-02d-scores.png) · [02e Evidence](./pm029-final-screenshots/mobile-s21-02e-evidence.png) · [03 columns](./pm029-final-screenshots/mobile-s21-03-columns.png) · [04 Swing without setup](./pm029-final-screenshots/mobile-s21-04-swing-no-setup.png)

**Samsung S23 (360 px; 10):** [01 heatmap](./pm029-final-screenshots/mobile-s23-01-heatmap.png) · [02 overview](./pm029-final-screenshots/mobile-s23-02-overview.png) · [02a overview levels](./pm029-final-screenshots/mobile-s23-02a-overview-levels.png) · [02b Manual R](./pm029-final-screenshots/mobile-s23-02b-manual-r.png) · [02c multiple decisions](./pm029-final-screenshots/mobile-s23-02c-details-multiple.png) · [02c Technicals](./pm029-final-screenshots/mobile-s23-02c-technicals.png) · [02d Scores](./pm029-final-screenshots/mobile-s23-02d-scores.png) · [02e Evidence](./pm029-final-screenshots/mobile-s23-02e-evidence.png) · [03 columns](./pm029-final-screenshots/mobile-s23-03-columns.png) · [04 Swing without setup](./pm029-final-screenshots/mobile-s23-04-swing-no-setup.png)

## 9. Prioritized correction plan — not authorization to implement

| Priority | Proposed bounded work | Required guard |
|---|---|---|
| **P0 — test evidence** | Replace independently distributed mock component scores with fixture evidence consistent with the displayed D20/D60 values and indicator fields. Add a small cross-layer invariant test for row heat, detail windows, indicators, S2/T2, and missing/unmapped cases. | Test/fixture changes only. Do not change production score thresholds or persisted data. |
| **P0 — clean defaults** | Apply the exact distinct Swing/Strategic default-column lists from the approved brief; retain existing user-saved preference migration and per-profile customizations. | User/owner confirms the precise lists; focused store tests and clean-profile UI checks. |
| **P1 — requirement decision** | Decide whether “Sector Heat” remains relative stock-vs-benchmark performance or whether a separate absolute momentum metric is required. | Approve metric, source, lookback, thresholds, missing-data behavior, and label before UI/backend changes. |
| **P1 — per-security evidence** | Review a sanitized, immutable row export for the 17 listed symbols and verify instrument type, classification source, analysis identity, benchmark, and freshness. | No account/quantity/balance data; no mapping activation during audit. |
| **P1 — valuation proposal** | Prepare and approve the shared CAD/FX contract and affected-consumer inventory. | Preserve PM-035 invariants; no score or accounting side effects. |
| **P1 — stop/target shadow** | Produce a reproducible historical shadow/replay report before any stop-policy change. | Explicitly no production activation; define no-lookahead and corporate-action safeguards. |
| **P2 — UI polish** | Improve phone tab-label discoverability and tablet horizontal-scroll affordance; consider a shorter responsive column-dialog title. | Keep desktop unchanged; test keyboard/touch access and all four supported viewports. |
| **P2 — SQL Server assurance** | Add isolated-provider tests for writes, failed-refresh preservation, retention, and cross-process concurrency. | Dedicated test database only; never use the protected portfolio database. |

## 10. UAT/sign-off recommendation

Do not sign off PM-029 yet. The immediate blocker is trustworthy evidence: a clean score/indicator fixture and the required perspective-specific defaults. Production scoring is not shown to be defective, so do not reopen the scoring formula on this evidence. Defer Sector Heat semantics, CAD/FX, new mappings, structural stops/targets, and SQL Server concurrency until their owners approve a specific contract and isolated verification plan.

PM-029 remains **OPEN**. No PM-035 change was made.

## 11. Post-report preservation verification

Post-report verification confirms the branch and HEAD match §1; staged paths remain 55 with the same raw diff hash; there are 0 unstaged tracked paths and 39 untracked paths (this report plus the 38 PNGs). Nothing was staged.

`git diff --check` passed. `git diff --cached --check` reported existing trailing whitespace in the staged `docs/pm029-master-implementation-progress.md` at lines 3, 4, 29, 44, and 58. This file was not changed by the audit.
