# PM-029 Phase 9B — Corrections, Verification, and UAT Evidence

**Disposition: PM-029 remains OPEN.** Automated checks and synthetic UI review are complete for this handoff. Full-universe data validation and user manual UAT remain pending.

**Evidence date:** 2026-11-15  
**Branch preserved:** `feature/pm-029-final-review`  
**HEAD preserved:** `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22`

## Executive summary

Phase 9B corrected the identified presentation and synthetic-evidence gaps without changing production scoring formulas, financial calculations, database contents, or authoritative FINAL ACTION behavior. Manual R is separate from calculated Market R and is now isolated by both symbol and score perspective. Swing and Strategic have distinct clean-profile defaults. Sector Heat now makes the signed relative return and benchmark-momentum color semantics distinct in the UI, and detail evidence exposes existing benchmark mapping provenance.

The E2E fixtures and all **66 screenshots** are visibly marked **SYNTHETIC | NOT PRODUCTION**. They are mocked layout/interaction evidence, not current securities, mappings, portfolio data, or live market results.

**No current 224-security or full-universe validation was possible.** SQL Server discovery found no available server; no real portfolio, security-master, or market-data snapshot was read. PM-029 must not be signed off based on the synthetic test rows.

## Phase 9B acceptance matrix

| Requirement | Status | Evidence / remaining limit |
|---|---|---|
| Current assessment | **BLOCKED for production sign-off** | Code and isolated tests were reviewed; no current live snapshot or data-universe validation was available. |
| Score/evidence consistency | **FIXED for synthetic fixtures; production not newly data-verified** | QRK fixture S2 is 17/20 from its displayed 20D/60D returns, the indicator fields agree, and CCA remains consistently unmapped. Existing backend score calculations were not changed. |
| Independent Swing/Strategic grid defaults | **FIXED** | Clean Swing and Strategic layouts and score sorting are separate; prior saved preferences and legacy normalization remain supported. |
| Market R vs Manual R | **VERIFIED / FIXED** | Market R stays the existing calculated setup value. Manual R is session-only and isolated by ticker and perspective for grid display, sorting, and export. |
| Sector Heat semantics and mapping evidence | **PRESENTATION FIXED; UNIVERSE MAPPING BLOCKED** | Badge text remains stock-minus-benchmark relative return; background reflects the benchmark's absolute-return sign. Existing mapping source, rationale, category, currency, and as-of metadata are exposed. No mappings or thresholds were changed. |
| Mobile/tablet corrections | **FIXED and tested** | Phones show tab-scroll guidance; tablets show a horizontal-grid scroll cue. No page-level horizontal overflow was detected in the tested scenarios. Desktop layout was not intentionally changed. |
| Real-data / full-universe validation | **BLOCKED** | No SQL Server endpoint or approved current sanitized universe export was available. Five synthetic rows do not validate the actual 224-security universe. |
| User manual UAT | **PENDING** | Screenshots are provided for review. Owner review and explicit acceptance are still required. |

### Decisions and evidence boundaries

- **Swing clean defaults:** Symbol, Swing Score, Market R, Manual R, Risk, Fit, Final Action, Sector / Theme Heat, Sector.
- **Strategic clean defaults:** Symbol, Strategic Score, Risk, Fit, Final Action, Sector / Theme Heat, Sector.
- The registry retains its 39 existing options and adds Manual R as a 40th option. Setup Status remains available in the column selector and is not added to the default grid.
- Market R remains sourced from `setup.rewardRisk`. Manual R accepts only positive prices with `stop < entry < target`; incomplete or invalid scenarios return no ratio. Values live only in page memory, keyed by perspective and ticker. They do not affect either score, Fit, saved plans, or FINAL ACTION.
- The existing setup resolver still uses the scanner DynamicStopLoss and nearest eligible channel rail, open gap, or 52-week-high level. The detail panel identifies the selected reference/source and says target quality is **not assessed** because no approved target-quality model is active. No stop, target, ATR, or trade-readiness methodology was activated.
- Sector Heat text continues to mean **relative stock return minus benchmark return** over the existing windows. Its background now communicates only whether the mapped benchmark return is positive, negative, or zero. This is a presentation distinction, not a new score or threshold. Mapping remains sector-level through existing `SectorBenchmarkMapping`; no industry/theme taxonomy or new mapping is claimed.
- Dashboard Market Trackers/Market Leadership and Opportunity Sector Heat are not the same measure. Dashboard trackers calculate an individual symbol's day/5D/20D returns, moving-average structure, momentum, and leadership classification. Opportunity Sector Heat compares a security's return with its mapped sector benchmark. No dashboard formula or mapping was reused as a substitute for missing security-master evidence.
- The API detail response now exposes existing benchmark category, currency, source, rationale, and source-as-of fields. This is read-only metadata exposure; no database schema or mapping record changed.

## Changes made

### Application and test changes

- Added independent Swing/Strategic clean grid defaults and profile-aware preference normalization; retained legacy preference compatibility.
- Kept score sort defaults independent by perspective.
- Added Manual R as a distinct grid, sort, and export value. A scenario key includes both strategy perspective and ticker so switching profiles cannot reuse the other profile's manual scenario.
- Clarified calculated setup evidence: Market R, selected target reference/source, scanner stop, data session, and the absence of an approved target-quality classification.
- Clarified Sector Heat badge semantics and added existing benchmark mapping provenance to detail evidence.
- Added phone tab-row guidance and tablet-only horizontal grid guidance/scrollbar styling.
- Reconciled QRK and CCA synthetic fixture evidence; marked the fixture source as synthetic in code.
- Extended Playwright coverage for perspective isolation, setup/evidence consistency, responsive affordances, and the Swing/Strategic screenshot matrix.
- Screenshot capture injects a visible synthetic watermark and prefixes every generated filename with `SYNTHETIC-`.

### Files with unstaged Phase 9B changes

Backend:
- `backend/PortfolioManager.Api/Services/Opportunities/OpportunityDtos.cs`
- `backend/PortfolioManager.Api/Services/Opportunities/OpportunityService.cs`
- `backend/PortfolioManager.Tests/Pm029OpportunityServiceTests.cs`

Frontend, tests, and screenshot coverage:
- `frontend/portfolio-manager-ui/e2e/fixtures/opportunities.ts`
- `frontend/portfolio-manager-ui/e2e/trading-opportunities.spec.ts`
- `frontend/portfolio-manager-ui/src/app/core/models/opportunities.models.ts`
- `frontend/portfolio-manager-ui/src/app/core/store/grid-column.store.spec.ts`
- `frontend/portfolio-manager-ui/src/app/core/store/grid-column.store.ts`
- `frontend/portfolio-manager-ui/src/app/core/store/grid-registry.ts`
- `frontend/portfolio-manager-ui/src/app/core/store/opportunities.store.spec.ts`
- `frontend/portfolio-manager-ui/src/app/core/store/opportunities.store.ts`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/cells/sector-heat-badge.component.ts`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunities-grid.component.html`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunities-grid.component.scss`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunities-grid.component.ts`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunities-logic.spec.ts`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunities-logic.ts`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/testing/opportunity-fixtures.ts`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunity-details-panel.component.html`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunity-details-panel.component.scss`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunity-details-panel.component.spec.ts`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunity-details-panel.component.ts`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/trading-opportunities-page.component.html`
- `frontend/portfolio-manager-ui/src/app/features/trading-opportunities/trading-opportunities-page.component.ts`
- `frontend/portfolio-manager-ui/src/app/shared/column-config-dialog/column-config-dialog.component.ts`
- `frontend/portfolio-manager-ui/src/app/shared/column-config-dialog/grid-column-btn.component.ts`
- `frontend/portfolio-manager-ui/src/styles/_opportunity-heat.scss`

The Phase 9B implementation files remain unstaged. The pre-existing staged index was not changed.

## Verification

| Check | Result |
|---|---|
| Focused Angular tests (logic, detail panel, grid-column store, opportunity store) | **88 passed / 0 failed** |
| Angular production build | **Passed**; existing component-style budget warnings remain in watchlist, dashboard, settings, scanner, portfolio, and signal-tracker styles. |
| Backend PM-029 test filter | **109 passed / 0 failed / 0 skipped** |
| PM-029 Trading Opportunities Playwright matrix | **52 passed / 0 failed** across desktop Chrome, tablet, mobile S21 (384 px), and mobile S23 (360 px). APIs were mocked. |
| Opt-in screenshot capture | **4/4 projects passed**; 66 PNGs verified, all filenames and visible watermarks marked synthetic. |
| `git diff --check` | Passed for unstaged changes. |
| Staged diff preservation | **55 paths**, SHA-256 `876892977f1b7a2e50c55d9af1a221f63cfee94995728a1679a41f558db05f7f`, unchanged. |
| SQL Server / real-universe validation | **Not run / blocked**; no SQL Server endpoint was available. No database was read or modified. |

The backend run used an isolated output directory because a pre-existing API process holds the normal build outputs; that process was not stopped. The backend build emitted existing platform/analyzer warnings. The optional Prettier check reported formatting differences across the already-modified frontend files; no broad formatting rewrite was applied. The staged diff check still reports historical trailing whitespace in `docs/pm029-master-implementation-progress.md` (lines 3, 4, 29, 44, and 58); this staged file was not edited.

### Baseline discrepancy — unresolved, preserved

Earlier PM-029 progress/final reports cite **41 staged paths** and hash `e64aabfc810aba6158da2d0379ca4a3f587717ee1842ca02b1ea2deccf5149fd`. The later Phase 9A audit and this Phase 9B handoff observe **55 staged paths** and hash `876892977f1b7a2e50c55d9af1a221f63cfee94995728a1679a41f558db05f7f`. No historical 41-path manifest was retained, so the 14-path difference cannot be reliably enumerated or attributed. Git does not establish authorship; all 55 staged paths are treated as pre-existing/user-owned and left untouched. No specific out-of-PM-029 ownership claim is made from the counts alone.

The Phase 9A report also claims 38 screenshots in `docs/pm029-final-screenshots/`, while the Phase 9B starting worktree did not show that folder. The cause was not inferred. The 66 Phase 9B screenshots below are a new, independently captured, explicitly synthetic set.

## Screenshot index — synthetic / API-mocked

All files are in [`pm029-phase-9b-screenshots/`](./pm029-phase-9b-screenshots/). The **SYNTHETIC | NOT PRODUCTION** watermark appears in each image. They show layout and interaction only.

**Desktop Chrome (16):** [01 heatmap](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-01-heatmap.png) · [02 overview](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-02-overview.png) · [02b Manual R](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-02b-manual-r.png) · [02c Technicals](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-02c-technicals.png) · [02d Scores](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-02d-scores.png) · [02e Evidence](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-02e-evidence.png) · [02f multiple decisions](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-02f-details-multiple.png) · [03 columns](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-03-columns.png) · [04 Strategic defaults](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-04-strategic-defaults.png) · [05 Strategic grid](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-05-strategic-grid.png) · [05a Strategic columns](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-05a-strategic-columns.png) · [06 Manual R grid](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-06-manual-r-grid.png) · [07 CCA overview](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-07-cca-overview.png) · [07a CCA Technicals](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-07a-cca-technicals.png) · [07b CCA Swing without setup](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-07b-cca-swing-no-setup.png) · [07c CCA Evidence](./pm029-phase-9b-screenshots/SYNTHETIC-desktop-chrome-07c-cca-evidence.png)

**Tablet (16):** [01 heatmap](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-01-heatmap.png) · [02 overview](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-02-overview.png) · [02b Manual R](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-02b-manual-r.png) · [02c Technicals](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-02c-technicals.png) · [02d Scores](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-02d-scores.png) · [02e Evidence](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-02e-evidence.png) · [02f multiple decisions](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-02f-details-multiple.png) · [03 columns](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-03-columns.png) · [04 Strategic defaults](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-04-strategic-defaults.png) · [05 Strategic grid](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-05-strategic-grid.png) · [05a Strategic columns](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-05a-strategic-columns.png) · [06 Manual R grid](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-06-manual-r-grid.png) · [07 CCA overview](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-07-cca-overview.png) · [07a CCA Technicals](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-07a-cca-technicals.png) · [07b CCA Swing without setup](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-07b-cca-swing-no-setup.png) · [07c CCA Evidence](./pm029-phase-9b-screenshots/SYNTHETIC-tablet-07c-cca-evidence.png)

**Samsung S21 (384 px; 17):** [01 heatmap](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-01-heatmap.png) · [02 overview](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-02-overview.png) · [02a setup levels](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-02a-overview-levels.png) · [02b Manual R](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-02b-manual-r.png) · [02c Technicals](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-02c-technicals.png) · [02d Scores](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-02d-scores.png) · [02e Evidence](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-02e-evidence.png) · [02f multiple decisions](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-02f-details-multiple.png) · [03 columns](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-03-columns.png) · [04 Strategic defaults](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-04-strategic-defaults.png) · [05 Strategic grid](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-05-strategic-grid.png) · [05a Strategic columns](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-05a-strategic-columns.png) · [06 Manual R grid](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-06-manual-r-grid.png) · [07 CCA overview](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-07-cca-overview.png) · [07a CCA Technicals](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-07a-cca-technicals.png) · [07b CCA Swing without setup](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-07b-cca-swing-no-setup.png) · [07c CCA Evidence](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s21-07c-cca-evidence.png)

**Samsung S23 (360 px; 17):** [01 heatmap](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-01-heatmap.png) · [02 overview](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-02-overview.png) · [02a setup levels](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-02a-overview-levels.png) · [02b Manual R](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-02b-manual-r.png) · [02c Technicals](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-02c-technicals.png) · [02d Scores](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-02d-scores.png) · [02e Evidence](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-02e-evidence.png) · [02f multiple decisions](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-02f-details-multiple.png) · [03 columns](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-03-columns.png) · [04 Strategic defaults](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-04-strategic-defaults.png) · [05 Strategic grid](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-05-strategic-grid.png) · [05a Strategic columns](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-05a-strategic-columns.png) · [06 Manual R grid](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-06-manual-r-grid.png) · [07 CCA overview](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-07-cca-overview.png) · [07a CCA Technicals](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-07a-cca-technicals.png) · [07b CCA Swing without setup](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-07b-cca-swing-no-setup.png) · [07c CCA Evidence](./pm029-phase-9b-screenshots/SYNTHETIC-mobile-s23-07c-cca-evidence.png)

## Sign-off gates

1. Owner reviews the screenshots and confirms whether the presentation corrections meet expectations.
2. Provide an approved sanitized current-universe/security-master export or isolated read-only environment to validate instrument identity, mapping coverage, strategic fundamentals, FX allocation evidence, and Swing completeness. Do not substitute synthetic fixtures or historical claims for this validation.
3. Keep all unapproved scoring, target-quality, stop-selection, FX/accounting, and mapping methodology changes deferred.

Until these gates pass, retain **PM-029 OPEN**. No commits, pushes, migrations, seeds, deployments, or database writes were performed.
