# PM-029 Phase 11 — Real-Data Readiness and Valuation Decision

**Disposition: PM-029 remains OPEN.** Local data access is available and the stored portfolio universe was inspected with aggregate-only, read-only queries. Portfolio Fit remains blocked on a common-currency valuation policy, and the stored opportunity snapshot predates the current Swing-v2 implementation and the latest portfolio update.

## Executive summary

- **Real-data access: CONNECTED (local read-only).** The existing application connection reaches the default SQL Server instance on `HOME-LAPTOP`, database `PortfolioManagerLocal`, using the application's configured integrated-authentication connection. No credentials or connection-string values were printed.
- The current source tables contain **224 distinct normalized listing symbols**: 165 watchlist, 46 non-manual open-portfolio, and 60 active TSX 60 listings. These memberships overlap. The application deduplicates by normalized listing symbol, not by canonical company/underlying identity.
- The latest stored opportunity snapshot is **Succeeded**, covers 224 listing rows, and is dated to the last completed session, **2026-11-13**. It was computed on 2026-11-15 with `pm029-v1` scoring data. The checked-out implementation uses Swing `pm029-v2`.
- All 224 stored Fit rows are `Unavailable` with `AllocationUnverifiable`; their evidence says `MixedCurrency`. This is consistent with the current portfolio containing both CAD and USD-denominated components and no FX conversion service.
- The portfolio snapshot was updated at **2026-11-15 13:53:05 UTC**, after the opportunity snapshot's portfolio as-of time of **10:31:48 UTC**. The stored opportunity view is therefore flagged by the API as changed since evaluation and must not be treated as current Fit output.
- The stored Swing-v1 snapshot reports 5 scores and 219 insufficient scores, with `MissingEssential:S6` / stop-related reasons. This is **not** evidence that the checked-out Swing-v2 scorer requires a stop: v2 has no S6 component, and focused backend coverage already verifies stop/setup independence.
- The only Phase 11 code change is a perspective-aware explanatory footer for the Strategic grid, covered by one focused Playwright test. No financial or scoring policy, backend behavior, or database content was changed.

## Access, scope, and safety

The configured local connection was used for `SELECT`-only aggregate queries. The portfolio, watchlist, and opportunity snapshot user scopes match. No symbols, account identifiers, user identifiers, balances, or position values were returned or written to this report.

The prior Phase 10 SQL-tool server discovery returned no registered servers. Current direct access through the application's configured connection succeeds; the earlier discovery result was a tooling/access-discovery limitation, not proof the local SQL Server was unavailable. The exact cause at the earlier time cannot be established from available evidence.

The API was not started for this audit: application startup calls `Database.MigrateAsync()` in [Program.cs](../backend/PortfolioManager.Api/Program.cs). No migration, seed, refresh, or database write was run. An opportunity refresh would persist new snapshot/state data and can also persist universe fundamentals, so it was not used to produce v2 results.

## Workstream A — Universe and data coverage

### Current source-table counts

| Membership | Distinct listing symbols |
|---|---:|
| Watchlist | 165 |
| Non-manual open portfolio | 46 |
| Active TSX 60 constituents | 60 |
| Current deduplicated listing union | **224** |

The counts were recomputed from the current source tables and agree with the stored snapshot's membership counts. The union is listing-key based. Five rows in the stored snapshot use an underlying security for analysis; this is not sufficient to certify canonical CDR/underlying identity deduplication.

### Snapshot freshness and validity boundary

| Evidence | Result |
|---|---|
| Latest `OpportunitySnapshot` | Succeeded; 224 records |
| Snapshot market session | 2026-11-13 |
| Snapshot scoring version | `pm029-v1` for all stored Swing and Strategic results |
| Current checked-out scoring versions | Swing `pm029-v2`; Strategic `pm029-v1` |
| Stored row status / scan stale flags | 224 `Ok`; 0 stale |
| Portfolio changed after opportunity evaluation | **Yes**; portfolio updated at 13:53:05 UTC, snapshot portfolio as-of 10:31:48 UTC |
| Latest standalone RSI scan snapshot | 29 symbols; not a full-universe artifact |

The portfolio's current stored quote snapshot has 55 open equity rows across 46 listings. All 55 have positive quote values, provider source timestamps, and quote currencies; their source timestamp date is 2026-11-13. There are no missing-quote, non-positive-quote, missing-provider-timestamp, or average-cost-fallback candidates in that snapshot. The quote date matches the last completed session, but this aggregate check does not replace per-security exchange-session review.

The current scope includes 71 cash ledger rows and 10 open option rows. Six cash rows contain USD markers; the remaining rows follow the code's default-CAD convention. The [CashItem](../backend/PortfolioManager.Api/Models/CashItem.cs) model has no currency field. All 10 open option rows have positive stored market price and contract count. Nine underlying symbols have a recognized Canadian suffix; the remaining suffix is not one of `.TO`, `.V`, `.CN`, or `.NE`, so the current resolver assumes USD. The [OptionItem](../backend/PortfolioManager.Api/Models/OptionItem.cs) model has no provider quote-currency or source-timestamp field.

## Workstream B — Portfolio Fit root cause

### Verified data flow and root cause

`OpportunityService` calls `FinalActionService.EvaluatePreviewAsync`, passes its `FinalActionPortfolioBasis` to `ExposureFor`, and then `PortfolioFitCalculator`. Fit is derived from that same basis; it is not an independent valuation and does not feed back into scores.

The existing valuation behavior is:

1. The stock denominator uses open stock rows, preferring the stored quote's current price and falling back to average cost when the quote is null. Manual assets use manual market value or cost basis.
2. The consolidated denominator adds in-scope cash ledger amounts and open options valued as `MarketPrice × NumberOfContracts × 100`.
3. Position weights use whole-symbol value divided by that consolidated denominator. Sector weight uses open stock market value divided by the stock-only total.
4. Stock currency comes from provider quote currency, with a ticker-suffix fallback; option currency is inferred from its underlying ticker; cash is treated as CAD unless account/description text has a USD marker.
5. There is no FX conversion. If the currency set contains more than one currency, `FinalActionService` marks valuation unreliable. `PortfolioFitCalculator` then returns `Unavailable / AllocationUnverifiable` and does not calculate weights or headroom.

The latest stored snapshot confirms `MixedCurrency` and `AllocationUnverifiable` on all 224 rows. Current aggregate quote/cash data also contains both CAD and USD components. The snapshot is older than the latest portfolio edit, so its per-row result is historical; a new evaluation would still need an approved valuation rule before any mixed-currency percentages could be considered reliable.

`FinalActionService` uses the same valuation reliability when producing position and sector exposure evidence. Its portfolio evidence gate explicitly prevents an ADD from passing when allocation is unverifiable. Portfolio Fit itself remains a separate display result and does not change `FINAL ACTION`.

The average-cost fallback is present in the authoritative arithmetic but was not triggered by the current 55-row portfolio quote snapshot. It must not be treated as verified current market exposure if a future quote is missing.

### CAD-equivalent valuation proposal — approval required

**Recommendation for owner review, not implemented:** create one shared CAD-equivalent valuation authority consumed by Portfolio, Dashboard, Final Action, and PM-029 rather than adding independent conversions to each surface.

- **FX source:** Bank of Canada Valet daily average series `FXUSDCAD`, whose published unit is Canadian dollars per US dollar ([official series response](https://www.bankofcanada.ca/valet/observations/FXUSDCAD/json?recent=1)). This is a proposed source; the owner must approve it or name another authority.
- **Rate direction:** store `CAD per USD`. CAD values remain unchanged; USD values are multiplied by the approved rate. Do not infer a rate from prices, ticker labels, or synthetic data. Unsupported currencies remain unavailable until an approved pair/source exists.
- **As-of alignment:** use the FX observation dated no later than the market-price session being valued. For weekends and holidays, use the most recent published business-day observation not later than that session. Proposed maximum age: three business days; beyond that, classify the affected common-currency exposure as unavailable/review.
- **Quote requirements:** require a positive market quote with provider source time consistent with the latest completed exchange session. A missing, non-positive, or stale quote cannot be replaced by average cost for current exposure. Keep average cost as cost-basis information only.
- **Currency authority:** use provider-reported listing/position currency when present. A suffix fallback is evidence with limited confidence, not a silent substitute for missing currency. For CDRs, use the listed holding's trading currency for valuation; underlying currency is for analysis only. Cash needs explicit ledger currency rather than description heuristics. Option currency, market price, contract multiplier, and long/short treatment need explicit confirmation.
- **Scope and double-counting:** preserve the existing user-plus-shared-row visibility rule unless the owner changes it. Include each open stock lot once, cash ledger rows once, and open options once; exclude closed lots. Do not add an option's underlying value merely because the option references that underlying. Confirm the existing `×100` option multiplier and its applicability before carrying it forward.
- **Allocation semantics:** retain approved role-specific position limits as limits. Treat sector percentages as targets with the existing 2-percentage-point tolerance, not as newly invented hard limits. Keep the existing distinction between consolidated denominator (stocks + cash + options) and stock-only sector denominator unless the owner explicitly approves changing it.
- **One authority and audit trail:** persist the quote currency/source timestamp and FX series/rate/date/source with the valuation result. Consumers should use that same result and expose unavailable evidence rather than re-derive a percentage.
- **Regression and rollback:** add pure tests for CAD-only, USD-only, mixed CAD/USD, missing/stale quote, missing/stale FX, weekend/holiday rate reuse, shared cash, options, multi-account lots, and sector-target boundaries. Roll back to fail-closed `Unavailable/Review`, never to an unverified mixed-currency percentage. No schema or migration change is proposed in this audit.

## Workstream C — Swing score diagnostics

The current `OpportunityScoringEngine` uses a fixed 80-point S1-S5 Swing pool. It gates on resolved identity, evaluation success, a scan for the expected session, scan freshness, and positive analysis price. Essential evidence and the configured completeness threshold govern `Insufficient`; setup, saved stops, Manual R, Market R, and ownership are not inputs to `ScoreSwing`.

The latest persisted rows are all `pm029-v1`, not v2:

| Stored Swing-v1 result | Rows |
|---|---:|
| Scored | 5 (3 Verified, 2 Provisional) |
| Insufficient | 219 |
| Unavailable | 0 |
| `MissingEssential:S6` reason | 219 |
| `NoValidStop` reason | 217 |
| `DynamicStopMissing` reason | 210 |
| `LowCompleteness` reason | 32 |

Reason counts overlap. They demonstrate that the stored legacy result used an S6/stop-dependent rule, not that the current S1-S5 v2 scorer does. The current backend regression `Swing_NoValidStop_DoesNotAffectTechnicalScore_AndStrategicStillScores` verifies setup independence and asserts S6 is absent. No scoring or identity policy was changed.

**Current v2 full-universe Swing coverage remains unverified.** Producing it requires a current opportunity refresh/rebuild, which writes snapshot/state and may persist fundamentals. It was not run under the read-only restriction. After separate authorization, inspect any remaining v2 insufficient rows by actual reason category; do not force scores when essential evidence is absent.

## Workstream D — Sector Heat and Strategic fundamentals

### Sector Heat / benchmark mappings

The latest stored snapshot reports 190 `Matched`, 21 `Unmapped`, and 13 `Unclassified` rows:

| Listing market | Matched | Unmapped | Unclassified |
|---|---:|---:|---:|
| TSX | 114 | 21 | 5 |
| NYSE | 76 | 0 | 8 |
| **Total** | **190** | **21** | **13** |

There are 19 active mapping records (8 TSX, 11 NYSE); all have a source and source-as-of date. Mapping uses active exact sector names or configured provider-sector aliases. `ETFs & Funds` is explicitly not a sector; no sector is inferred from an ETF issuer. These counts establish stored coverage, not that each benchmark is semantically correct for every ETF/CDR.

### Strategic fundamentals

The stored snapshot has a fundamentals as-of timestamp for all 224 rows, with no timestamp older than seven days at snapshot time. Stored result origin counts are 130 Watchlist, 70 Portfolio, and 24 Universe.

| Stored Strategic-v1 result | Rows |
|---|---:|
| Verified | 124 |
| Provisional | 80 |
| Insufficient | 20 |

Reason-code occurrences include `PiotroskiInsufficientSignals` (28), `MissingEssential:T7` (17), `FundamentalsInsufficientFactors` (17), `LowCompleteness` (16), and `MissingEssential:T1` (4). Reason counts can overlap. Missing/insufficient evidence is distinct from a legitimately weak numeric score; this aggregate does not certify factor-level quality or validate current market data after the portfolio update.

## Workstream E — Strategic footer correction

The page previously rendered the snapshot's Swing disclaimer regardless of selected perspective. It now renders the stored disclaimer for Swing and a short Strategic-appropriate description for Strategic. Calculation semantics and score data are unchanged.

Changed:

- [trading-opportunities-page.component.html](../frontend/portfolio-manager-ui/src/app/features/trading-opportunities/trading-opportunities-page.component.html)
- [trading-opportunities.spec.ts](../frontend/portfolio-manager-ui/e2e/trading-opportunities.spec.ts)

## Verification

| Check | Result |
|---|---|
| Production Angular build | **Passed.** Existing component SCSS-budget warnings remain in six unrelated stylesheets. |
| Focused desktop Playwright test for Swing/Strategic disclaimer | **1 passed, 0 failed, 0 skipped.** Run on alternate port 4321 because port 4200 had a non-responsive listener. |
| Backend tests | Not rerun; no backend code changed. The previous Phase 10 report records its own test results; they are not Phase 11 re-runs. |
| Database access | Read-only SELECTs only; no API startup, refresh, migration, seed, or write. |
| Screenshot capture | Not run, as required by the Phase 11 stop condition. |

An initial Playwright attempt could not start because port 4200 was occupied; a subsequent test-discovery attempt used a Windows path separator that did not match Playwright's file pattern. The focused test then ran successfully using the file name and an alternate port.

## Remaining blockers and requested owner decisions

| Priority | Blocker | Needed |
|---|---|---|
| P0 | No approved common-currency valuation policy; current Fit is fail-closed for mixed currencies. | Approve the proposed Bank of Canada `FXUSDCAD` daily-average authority, direction, freshness, missing-evidence behavior, and whether the quote fallback is disallowed for exposure; or specify alternatives. |
| P0 | Current local opportunity snapshot is Swing-v1 and predates the latest portfolio update. | Authorize one controlled refresh/rebuild (which writes opportunity snapshot/state and may persist universe fundamentals), or provide an approved current sanitized v2 export. |
| P1 | Actual current v2 Swing reason counts are unavailable. | Re-evaluate only after P0 refresh/export authorization; preserve current scoring rules. |
| P1 | Option currency/quote provenance and cash currency are inferred or incomplete. | Confirm cash and option scope, explicit currency, multiplier, price source, and consolidated versus per-account valuation. |
| P1 | Sector/ETF/CDR mapping correctness is not certified by coverage counts. | Owner review of source/as-of mappings and underlying/listing identities; do not add mappings without approval. |
| P1 | User manual UAT is pending. | Review the report and existing synthetic screenshot package; production acceptance remains a separate gate. |
| P2 | Strategic disclaimer mismatch. | Corrected and covered by the focused UI test. |

### Approval request

1. Approve or replace the proposed Bank of Canada `FXUSDCAD` daily-average source and the stated as-of/freshness rules.
2. Confirm that missing/stale market prices must make market exposure unavailable rather than use average cost.
3. Confirm cash, options, account scope, option multiplier/currency treatment, and whether shared rows remain in the consolidated valuation.
4. Authorize a controlled v2 opportunity refresh/rebuild, or provide a sanitized v2 snapshot, so current-universe Swing/Strategic/sector coverage can be verified without fabricating evidence.
5. Complete manual UAT before closing PM-029.

## Screenshots and closeout

No new screenshots were created in Phase 11. The existing [70-screen Phase 10 synthetic UAT archive](./screenshots/pm029-phase-10-integrity-uat.zip) remains available for review. It currently contains 70 PNGs plus one directory entry (16,751,830 bytes; SHA-256 `E137271DCCF672F771A5F0D920442F8E995897059070CE28E8AA3103A38214CF`). The previous Phase 10 report recorded a different checksum (`DC2BD3C2641F42CDF259FA9BFBC2179C5F0D8C4BD40129FD8E92C4E5B42284CA`); the discrepancy is unexplained. The current archive's images are watermarked synthetic and are not evidence of live portfolio data.

### Source references

- [OpportunityService.cs](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityService.cs) — persisted snapshot read, universe union, scoring, Fit mapping, and evidence.
- [FinalActionService.cs](../backend/PortfolioManager.Api/Services/FinalAction/FinalActionService.cs) — market-value arithmetic, denominator, currency reliability, and shared exposure.
- [PortfolioFitCalculator.cs](../backend/PortfolioManager.Api/Services/Opportunities/PortfolioFitCalculator.cs) — fail-closed Fit status.
- [PortfolioEvidenceGate.cs](../backend/PortfolioManager.Api/Services/FinalAction/PortfolioEvidenceGate.cs) — allocation-unverifiable ADD behavior.
- [RiskSupport.cs](../backend/PortfolioManager.Api/Services/Risk/RiskSupport.cs) — ticker-suffix currency fallback.
- [OpportunityScoringEngine.cs](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityScoringEngine.cs) and [Pm029CalculatorTests.cs](../backend/PortfolioManager.Tests/Pm029CalculatorTests.cs) — current Swing-v2 inputs and setup-independence test.
- [SectorClassificationResolver.cs](../backend/PortfolioManager.Api/Services/Opportunities/SectorClassificationResolver.cs) — classification precedence and active exact/alias mapping.

**Git baseline:** branch `feature/pm-029-final-review`; HEAD `5a5e1ea14709ce426c9992adfc848316b096dc2e`; 10 staged paths. The staged diff-stream fingerprint (`git hash-object` over `git diff --cached --binary`) was `3f94fc490b68cb1603a351376ba31f8dfec1a7ec` before Phase 11 edits and is to be rechecked at closeout. No commit, push, stage, or database write was performed.

**PM-029 remains OPEN.** No new FX, accounting, scoring, benchmark-mapping, database, or production policy was implemented. Phase 11 edits remain unstaged.
