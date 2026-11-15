# PM-029 Phase 14 - Shadow Valuation and Integration Preparation

**Status: Phase 14 inactive implementation delivered for review. PM-029 remains OPEN.**

No production valuation source, financial record, scoring formula, benchmark mapping, FINAL ACTION recommendation, migration or live Opportunity refresh was changed or activated.

## 1. Executive approval package

| Classification | Result |
|---|---|
| READY TO ACTIVATE | **No protected production consumer is ready for activation.** The inactive adapters, shared snapshot projections and standalone verification harness are ready for a separately approved isolated rehearsal. |
| REQUIRES FINANCIAL POLICY APPROVAL | Cash account/currency authority; option identity, side, multiplier and mark conventions; verified FX availability/calendar source and maximum age; schema/backfill sequence; consumer cutover. |
| BLOCKED BY MISSING EVIDENCE | Authoritative legacy cash/option provenance and a complete, immutable same-as-of 224-listing Swing-v2 input package. |

Implemented:

- Currency-specific signed cash-ledger aggregation, explicit stable account identity, opening-balance and trade-link checks, external-flow separation, and user/shared visibility.
- Explicit option position contracts for identity, CALL/PUT, LONG/SHORT, signed open quantity, multiplier, quote and position provenance. Closed positions require closure evidence before exclusion.
- One inactive shared shadow service reusing the Phase 13 CAD calculator; no second FX or allocation calculator.
- Portfolio quote adapter and common projection contracts for Portfolio, Dashboard, Allocation, FINAL ACTION and PM-029 Portfolio Fit.
- FX observation/retrieval/availability separation; complete serialized component provenance.
- An executable read-only Swing replay harness invoking the actual current Swing-v2 engine.
- Review-only currency schema/index proposal and executable aggregate-only legacy candidate query.
- Deterministic synthetic shadow/replay JSON outputs.

**Final regression:** 352 tests passed, 0 failed, 0 skipped. Earlier focused runs overlap with this final set and are not added to its count. Release API and verification CLI build succeeded.

## 2. Branch/worktree preservation

Work is in **`D:\PORTFOLIO-MANAGER`**, branch **`feature/pm-029-phase-14-shadow-valuation`**, based on local `develop` at **`dc2260b2926200d42ebff9d68cf60468afcce778`**. No new worktree was created.

The original PM-029 feature HEAD `63662b1438ae1b1d4362f101633e58ddb224b2ce`, Phase 12 commit and Phase 13 commit were already ancestors of `develop`. Consequently, using `develop` preserves the complete feature, rather than porting a disconnected prototype.

| Worktree | Initial branch / HEAD | Treatment |
|---|---|---|
| Main folder | `develop` / `dc2260b2926200d42ebff9d68cf60468afcce778` | New Phase 14 branch, same HEAD; changes remain unstaged and uncommitted |
| `D:\PM029-PHASE12` | `feature/pm-029-phase-12-prototype` / `c07259ae5aa5dd234b9de0fac4fa30f5269cb87e` | Preserved, clean |
| `D:\PM029-PHASE13` | Detached / `dc2260b2926200d42ebff9d68cf60468afcce778` | Preserved, clean |

All three worktrees initially had zero tracked/untracked changes and zero staged changes. SHA-256 of both initial binary diff outputs was the empty-content hash:

`E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`

Main HEAD and `develop` remain at the baseline commit. No agent commit, stage, push, pull, merge, rebase, reset, clean, deploy, database initialization or application startup was performed. Existing auxiliary folders and running application processes were not removed or stopped.

## 3. Phase 13 integration and discovered conflict

The Phase 13 valuation model, services and tests matched the current `develop` versions exactly before editing; no port or cherry-pick was necessary.

The first combined test compilation uncovered a previously hidden integration conflict: Phase 12's test-only prototype types have the same names as Phase 13's API types. Both were merged into the test assembly, where the `PortfolioManager.Tests` namespace shadowed API types and prevented compilation.

**Correction:** moved only the Phase 12 prototype and its tests into `PortfolioManager.Tests.Phase12`. Their algorithms and test expectations are unchanged. This is necessary to validate the actual merged codebase, not a financial-policy change.

No production controller, EF entity/model mapping or DI registration was added. New internal evidence contracts do not change an existing HTTP response or Angular API contract.

## 4. Cash currency provenance

[CashLedgerEvidence and integration contracts](../backend/PortfolioManager.Api/Models/ValuationIntegrationModels.cs) represent explicit currency/source/status/time, stable account identity, ownership, effective date, recorded time, signed amount, flow type, source-trade link and historical currency evidence.

[VerifiedFinancialEvidenceAdapter](../backend/PortfolioManager.Api/Services/Valuation/VerifiedFinancialEvidenceAdapter.cs):

- Selects authorized user-plus-shared rows and the requested account scope before aggregation.
- Aggregates movements by owner, stable account identity, account label and currency; detects conflicting account labels.
- Excludes future effective dates and rejects rows not yet recorded by the frozen cutoff.
- Includes the selected opening balance once. Multiple opening baselines in one balance bucket are rejected for review rather than added optimistically.
- Rejects duplicate row IDs, duplicate source-trade keys, incomplete links, unknown flow classifications and conflicting signs.
- Reuses `CashFlowTypeRules` for external-flow classification. Deposits/withdrawals affect cash balances, but the separate native-currency external-flow totals are **not investment P&L**.
- Requires verified currency on every included row. Missing currency cannot become CAD or zero.
- Keeps raw input evidence in the shadow snapshot; currency-source details are not lost by aggregation.

The input package must assert ledger completeness through its valuation session. It must contain one baseline and subsequent movements per currency bucket, not a set of overlapping exported balances. This adapter cannot prove an export's completeness without an authoritative source.

### Actual local candidate findings

Read-only queries on 2026-11-15 used the existing integrated-auth local SQL environment, database `PortfolioManagerLocal`, with `sqlcmd -K ReadOnly` and SELECT-only statements. No connection string or credentials are included.

| Actual aggregate | Count |
|---|---:|
| Cash rows with no authoritative schema currency field | **71** |
| Shared/unowned cash rows | 5 |
| Opening-balance rows | 6 |
| Trade-linked cash rows | 48 |
| Duplicate source-trade link groups | 0 |
| Missing flow type / account label | 0 / 0 |

All 71 rows are **UnknownCurrency from the existing schema**, not confirmed CAD or USD. This does not claim that broker evidence is unavailable outside the application.

[LegacyProvenanceClassifier](../backend/PortfolioManager.Api/Services/Valuation/LegacyProvenanceClassifier.cs) supports verified authoritative evidence, confirmation-required candidates, unknown currency and conflicting evidence when a local evidence sidecar is supplied. Descriptions and ticker suffixes are not verification.

## 5. Option provenance and accounting compatibility

`OptionPositionEvidence` represents an aggregated position, not an option transaction:

- Stable contract/account identity, ownership and investment role.
- CALL/PUT separately from LONG/SHORT.
- Signed open contract quantity and explicit open/closed state.
- Verified currency, multiplier and terms source/time.
- Positive marked price, quote currency/source/session/time.
- Position source and position-as-of time.

The adapter rejects side/sign conflicts, unknown identity, unverified position state, stale/future position evidence and duplicate contract/account position buckets. The Phase 13 calculator checks currency, terms, multiplier and quote evidence. A closed zero-quantity position is omitted only with current verified position provenance; it is not inferred from a legacy CLOSE transaction.

| Actual local aggregate | Count |
|---|---:|
| Legacy option transaction rows requiring authoritative terms/position confirmation | **30** |
| OPEN / CLOSE transaction rows | 10 / 20 |
| Nonpositive stored marks, all rows | 3 |
| Nonpositive stored marks, OPEN rows | 0 |
| Authoritative currency/multiplier/side/quote fields found in inspected schema | 0 |

These are transaction-row counts, **not certified open contract positions**. A positive stored price is not a verified current mark without provenance.

The future adapter values `mark x signed open contracts x verified multiplier`. It never adds the underlying stock's notional merely because an option references it. Option transaction cash is already represented by the cash ledger and is not added separately. Multipliers other than 100 and short liabilities are tested but still require owner approval of production mark conventions. PM-035 performance accounting is untouched.

## 6. Migration/backfill proposal - not executed

Deliverables:

- [Review-only schema/index proposal](../database/SCRIPTS/PM029_Phase14_CurrencyProvenance_PROPOSAL.sql)
- [Aggregate-only legacy candidate query](../database/SCRIPTS/PM029_Phase14_LegacyCandidates_READONLY.sql)

The schema proposal adds nullable provenance fields to cash/options plus append-only historical currency evidence. It does not initialize CAD, invent multipliers or backfill existing records. It aborts with its approval flag set to zero.

Opening-balance indexing proposal:

1. Preserve the existing account-label/date unique protection for unknown-currency legacy opening balances.
2. Add stable-account/currency/date uniqueness for identified currency balances.
3. Preserve `IX_CashItems_TradeLink` unchanged.
4. Add uniqueness for identified open option positions per stable account/contract.

The stable valuation account ID must be globally unambiguous and linked to approved ownership. Its authoritative broker/account registry is still an owner decision; an account label is not that registry.

**Backfill procedure:** first produce a local per-record broker/owner evidence manifest; classify conflicts; obtain explicit approval; apply verified rows only in a disposable restored test database; leave unknown/conflicting rows unavailable. Preserve effective dates and append provenance instead of rewriting the original cash amount or option trade history. Reconcile total signed ledger amounts and trade-source uniqueness before/after.

**Not tested or executed:** the DDL, generated EF migration, data backfill, SQL rollback and database rehearsal. The SQL is a review proposal, not an applied EF migration. Before application, generate a matching EF migration so migration history/model snapshots remain consistent; do not apply this proposal and then independently apply duplicate EF DDL.

Rollback requires a verified database backup and an approved script/rehearsal. Do not drop populated provenance columns or evidence history as an automatic rollback. Runtime rollback must remain fail-closed, never return unverified mixed-currency weights.

## 7. Shared snapshot and inactive consumer boundary

[SharedShadowValuationService](../backend/PortfolioManager.Api/Services/Valuation/SharedShadowValuationService.cs) adapts verified evidence, invokes the existing Phase 13 calculator once, and projects its position/sector exposure evidence.

[ValuationConsumerAdapter](../backend/PortfolioManager.Api/Services/Valuation/ValuationConsumerAdapter.cs) supplies the same snapshot evidence to the five prepared consumer contracts. It does not recompute FX/weights or return a trading action. Its Portfolio adapter uses provider source time, not fetch time, and never uses average cost as a missing quote replacement. Manual assets remain unavailable in this adapter until an explicit verified mark contract is approved.

Financial invariants:

- Consolidated denominator: securities + aggregated cash + signed option market values.
- Sector denominator: securities only, separate from consolidated value.
- Full decimal precision; native values and CAD equivalents both retained.
- Missing evidence suppresses the complete denominator and all dependent weights/headroom.
- Position limits remain hard constraints. Sector targets retain supplied tolerance; the current authority's 2 pp rule is not changed.
- Mixed-role symbols retain total exposure but do not silently choose a role limit.
- Unclassified stock-sector evidence does not silently become zero sector exposure.
- No new action, ADD eligibility override or score feedback.

The inactive snapshot includes source inputs, component reasons, selected FX evidence, adapter issues, external-flow diagnostics and legacy differences. Polymorphic serialization preserves derived quote/currency/terms evidence.

**Activation boundary:** no runtime registration, controller, scheduler or protected service call site exists. `ProductionActivationAllowed` is false for evaluator output; consumer projections are non-authoritative. The current `FinalActionService` denominator and business rules remain unchanged. The safety boundary is absence of production wiring, not a permission to flip a boolean.

## 8. FX publication and timestamp assurance

The FX contract distinguishes:

- Observation date and CAD-per-USD rate.
- Expected publication cutoff from the explicit BOC calendar.
- Captured first-observed/retrieval time.
- Separately verified availability time and its source, when provided.
- Data-quality label and frozen valuation cutoff.

Scheduled 16:30 ET is used only for publication-session age accounting, **not** availability proof. A delayed observation cannot be used before capture. Verified historical availability can permit an earlier cutoff only with a persisted supporting source; absent that evidence, retrieval is the conservative earliest demonstrated availability. Invalid or contradictory availability evidence fails closed.

Provider capture proves receipt through Valet at that retrieval, not the website's earliest release or an earlier historical publication. Phase 14 did not create a persisted FX availability log or a complete production BOC holiday calendar.

The calendar remains explicit and coverage-bounded, independent of TSX/NYSE holidays. Existing tests cover weekends, bank holidays, NYSE-closed/BOC-open dates, DST and missing calendar coverage. The three-publication-session age remains an explicit synthetic argument, not a universal runtime default.

### UTC verification

Read-only raw SQL checks:

- Opportunity max UTC ticks: `2026-11-15 10:38:22.7272988`.
- Portfolio max UTC ticks: `2026-11-15 13:53:05.3787520`.
- SQL UTC clock at one check: `2026-11-15 16:20:40.8657877`.
- Opportunity/portfolio rows later than SQL UTC at the subsequent check: **0 / 0**.

Converter/serialization tests cover portfolio, opportunity, cash, options and daily-signal timestamps, including nullable timestamps. The replay rejects timezone-naive scanner evidence and future/unverified structure evidence. Quote timestamps use Unix provider time. FX timestamps use `DateTimeOffset`.

Relevant Opportunity/FINAL ACTION scanner paths use explicit UTC handling; their persisted entities use `AppDbContext` converters. The EOD controller's UTC conversion relies on the daily-signal converter. Direct SQL diagnostics bypass EF and must retain raw UTC ticks without calling local-to-UTC conversion. JSON payloads inside snapshot columns are not fixed by EF scalar converters; replay input timestamps must include UTC offsets.

This is focused timestamp assurance, not certification of every historical payload/reader. Historical timestamps were not rewritten.

## 9. Executed deterministic synthetic shadow results

Source: [synthetic shadow fixture](../scripts/pm029/Phase14Verification/synthetic-shadow.json).
Output: [shadow result JSON](pm029-phase-14-shadow-results.json).

The fixture's **1.4 FX rate is synthetic**, not an actual BOC observation. Official observation fixtures are tested separately. No synthetic value is claimed as a price/rate for the real portfolio.

| Component | Native value | CAD value | Legacy raw value | Difference classification |
|---|---:|---:|---:|---|
| CAD stock | CAD 1,000 | 1,000 | 1,000 | Parity |
| USD stock | USD 1,000 | 1,400 | 1,000 | Expected currency correction |
| Shared CAD cash balance | CAD 500 | 500 | 500 | Parity |
| USD cash balance | USD 200 | 280 | 200 | Expected currency correction |
| Long option: 2 contracts, multiplier 10, mark 1 | CAD 20 | 20 | 200 | Policy decision required |
| Short option: -1 contract, multiplier 100, mark 1 | CAD -100 | -100 | 100 | Policy decision required |

- CAD consolidated denominator: **3,100**.
- Stock-only sector denominator: **2,400**.
- Legacy mixed-native raw sum: **3,000**; it is not a valid CAD denominator.
- CAD stock position weight: **32.258064516129...%**.
- Position limit 40%: headroom **7.741935483870... pp**.
- Technology weight over stock-only denominator: **100%**.
- Sector target 60%: deviation **+40 pp**, above the supplied 2 pp tolerance.
- Shared cash: `500 opening + 200 deposit - 200 linked purchase = 500`, included once.
- External cash-flow diagnostic: **CAD +200**, separate from P&L.

The legacy comparison is an explicit model of current raw-sum, positive-count/x100 conventions, not a live execution of legacy services against real records. Tests additionally cover CAD-only, USD-only, missing/invalid evidence, CDR listing currency, roles, shared records, trade links, closed/long/short options, account scope, duplicate components and zero/negative denominators.

Repeated evaluator output and input preservation were checked. Classification of differences is diagnostic; it does not approve an accounting policy or alter FINAL ACTION.

Final CLI reruns were byte-identical to the delivered JSON, and both input-file hashes were unchanged:

- Shadow output SHA-256: `670F4363CF21953CB0441A4B5E125FF2918A7AF0B6B4CD237ED0C147BA9AB7D1`.
- Replay output SHA-256: `35CB86085EC0090D2D001F959E879764BAEDBB5A00978FBAA1EF484A887BA71E`.

## 10. Swing-v2 replay: executable path, real-data blocker

[SwingReplayRunner](../backend/PortfolioManager.Api/Services/Opportunities/SwingReplayRunner.cs) has no database, persistence, refresh or HTTP dependency. It receives immutable arrays/scan JSON with a frozen UTC cutoff, dataset label, scorer commit and expected unique membership count.

It validates canonical listing identity/provenance, complete raw scoring fields, scanner UTC/provenance, mapping/history availability and non-conflicting history sessions. It calls the existing `SectorRelativeCalculator`, `TradeSetupResolver` and **actual `OpportunityScoringEngine.ScoreSwing`**. It does not translate old v1 components.

Tests verify:

- Determinism and unchanged inputs.
- Score without stop, Market R, ownership or Manual R.
- Missing MACD delta cannot silently deserialize as an observed zero.
- Essential-evidence insufficiency and stale-session reasons.
- Missing/unverified benchmark evidence remains provisional or insufficient according to the existing scoring engine.
- Duplicate/incomplete membership cannot be reported as full-universe coverage.

Standalone [synthetic replay output](pm029-phase-14-replay-results.json): **1 synthetic listing, score 75, 60/80 points, 75% completeness, Provisional, no Market R**. The fixture deliberately lacks benchmark evidence. A separate full-history synthetic unit case is Verified. Neither is real-universe evidence.

### Current actual read-only findings

The current scoped union of open non-manual holdings, watchlist and active TSX60 listings was recomputed: **224 unique normalized listing symbols**. This uses current application listing-symbol membership semantics; it is not new CDR/company-identity deduplication policy.

Latest stored opportunity snapshot: **`pm029-v1`, Succeeded, session 2026-11-13**, computed `2026-11-15 10:38:22.7272988`.

Latest stored RSI scan: **29 symbols**, scanned `2026-11-15 16:29:17.9161157` at the later check. The existing application was already running and may update its data independently; these read-only queries are not a single frozen transaction/package.

**Real Swing-v2 replay remains BLOCKED.** The 29-symbol scan is not 224-symbol coverage; it cannot be combined with older partial evidence to claim one same-as-of replay.

Smallest controlled acquisition plan:

1. Freeze a single evaluation cutoff and an authorized user's union manifest.
2. Obtain listing identity/currency provenance, complete current raw scanner results and same-as-of adjusted analysis/benchmark histories plus existing mappings.
3. Use the scanner's existing read-only evaluation path in a separately approved acquisition tool, preserving its throttling; do not call `OpportunityService.RefreshAsync` or persist universe fundamentals.
4. Keep acquisitions local, mark each source/availability time, export one complete immutable package and hash it.
5. Execute the harness below; reconcile declared membership to the actual current manifest, record per-category score/status/reason coverage, and repeat deterministically.

Phase 14 implements the runner, not a new market-data acquisition integration. Live Opportunity refresh/backfill remains separately approval-gated.

## 11. Executable verification commands

The CLI references the API assembly but never starts its web host or executes `Program.cs`:

```powershell
dotnet build scripts\pm029\Phase14Verification\Phase14Verification.csproj --configuration Release -p:UseAppHost=false -p:BaseOutputPath=D:\PORTFOLIO-MANAGER\artifacts\pm029-phase14-build\
dotnet artifacts\pm029-phase14-build\Release\net8.0\Phase14Verification.dll shadow scripts\pm029\Phase14Verification\synthetic-shadow.json <new-local-output.json>
dotnet artifacts\pm029-phase14-build\Release\net8.0\Phase14Verification.dll replay <complete-local-replay-package.json> <new-local-output.json>
```

Outputs must be new paths: the CLI uses `FileMode.CreateNew` and will not overwrite existing files. Input errors and nondeterminism surface as failures. Build output used during implementation is temporary and can be recreated by this command.

Actual aggregate candidate query:

```powershell
sqlcmd -S HOME-LAPTOP -d PortfolioManagerLocal -E -K ReadOnly -b -i database\SCRIPTS\PM029_Phase14_LegacyCandidates_READONLY.sql
```

Do **not** use the production application startup as a read-only validator: it has existing startup migration behavior.

## 12. Regression outcomes

| Verification | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Final combined valuation/provider/UTC, cash ledger/link, PM-029 and FINAL ACTION set | **352** | **0** | **0** |
| Release API + CLI build | Succeeded | 0 errors | N/A |
| CLI synthetic shadow/replay, repeated in-process equality | Succeeded | 0 | N/A |
| SQL aggregate candidate query | Succeeded, read-only | 0 | N/A |

The initial merged test build failed before test execution due to the Phase 12 namespace collision; that issue was corrected. Release compilation reports two existing `Program.cs` CA1416 warnings. Existing unrelated test warnings remain; no warning originated in the new code.

Tests used the repository's xUnit/VSTest configuration and isolated build output to avoid a running API's apphost lock.

```powershell
dotnet test backend\PortfolioManager.Tests\PortfolioManager.Tests.csproj --filter "FullyQualifiedName~SharedShadowValuationTests|FullyQualifiedName~SwingReplayRunnerTests|FullyQualifiedName~Pm029|FullyQualifiedName~BankOfCanadaFxProviderTests|FullyQualifiedName~AppDbContextUtcConverterTests|FullyQualifiedName~CashLedgerTests|FullyQualifiedName~CashTradeLinkTests|FullyQualifiedName~FinalAction" -p:UseAppHost=false -p:BaseOutputPath=D:\PORTFOLIO-MANAGER\artifacts\pm029-phase14-build\ --logger "console;verbosity=minimal"
```

Full backend suite, Angular unit tests, production database-backed valuation, SQL DDL rehearsal, production refresh, real 224-listing replay and manual UAT were **not run**. No UI or existing HTTP contract changed. Following the owner's screenshot recapture request, the four-viewport Playwright capture test passed separately; it is API-mocked evidence, not production UAT. Blocked/unperformed validations are not counted as skipped test cases.

## 13. Screenshots

No new UI was introduced in Phase 14. At the owner's subsequent explicit request, the current checked-out UI was recaptured on 2026-11-15 using the existing API-mocked screenshot harness.

**New package:** [70 freshly captured Phase 14 UI screenshots ZIP](screenshots/pm029-phase-14-ui-20261115-114112.zip).

- Images captured locally between **11:41:57 and 11:42:27 ET**.
- ZIP created **11:43:17 ET**, last written **11:43:18 ET**, on 2026-11-15.
- Desktop Chrome: 17 PNGs; Pixel Tablet: 17; Samsung S21: 18; Samsung S23: 18.
- **4 capture tests passed, 0 failed, 0 skipped.** No repository test or UI source changes were needed.
- All 70 ZIP entries were hash-checked against their newly captured source images.
- ZIP size: 16,746,921 bytes.
- SHA-256: `6D6DFFB59FC9D65E28DA49F8BF9791C1483A27A09E748E2C7C93D88BC48B0B61`.

Every screenshot is visibly marked **SYNTHETIC | NOT PRODUCTION**. Dates displayed inside the application are fixture evaluation dates, not capture timestamps. Layouts remain visually unchanged because Phase 14's valuation engine is inactive. This ZIP does not demonstrate real Portfolio Fit or real 224-listing coverage.

The old Phase 10 archive was absent when recapture began; it was not restored or overwritten. The new dated ZIP supersedes it for this handoff. The earlier historical Phase 10 report/hash discrepancy is not a claim about this new archive.

## 14. Exact production integration checklist and approvals

1. **Owner:** approve authoritative cash account IDs, currency evidence source and unknown/conflicting-record treatment.
2. **Owner:** approve option position source, contract identity, signed side/quantity, verified multiplier and market-mark convention; explicitly decide zero marks and manual-asset treatment.
3. **Owner/data provider integration:** approve full BOC publication calendar source/coverage, persisted actual availability evidence and maximum-age policy. Three sessions remains provisional.
4. **Engineering + owner:** generate/review matching EF schema migration and nullable provenance backfill manifest. Review account/currency opening uniqueness and preserved source-trade links.
5. **Engineering, after approval:** rehearse schema, verified-only backfill, ledger/value reconciliations and rollback in a disposable restored database. Do not guess unknown values.
6. **Engineering:** bind the prepared evidence adapters to authoritative sources, produce real shadow snapshots and compare each consumer on the same frozen account/role scope. Do not register the new runtime authority before this passes.
7. **Owner:** approve explicit consumer cutover, then wire Portfolio/Dashboard/Allocation/FINAL ACTION/PM-029 to the same valuation snapshot. Retain consumer-specific action rules and fail-closed fallback.
8. **Owner:** separately authorize complete same-as-of read-only Swing acquisition/replay; authorize any live Opportunity write-producing refresh only after verifying the write boundary.
9. **Owner/manual UAT:** reconcile the actual universe, scores, benchmarks, Portfolio Fit and FINAL ACTION, then consider closure.

| Priority | Remaining blocker | Owner / prerequisite |
|---|---|---|
| P0 | Real Portfolio Fit still inactive and unavailable | Financial owner: currency/terms/FX decisions; engineering: isolated rehearsal and verified source adapters |
| P0 | Legacy currency/option evidence is not authoritative | Broker/owner confirmation and approved provenance manifest |
| P1 | 224-listing current Swing-v2 coverage unknown | Complete same-as-of acquisition package; no protected refresh |
| P1 | Full FX publication calendar/availability history missing | Approved source and persistence contract |
| P1 | Schema/backfill/rollback untested | Separate Phase 15 approval and disposable database rehearsal |
| P2 | Final UI/manual production UAT not performed | Actual financial and scoring evidence must first be validated |

**Recommendation:** approve the Phase 14 inactive code and rehearsal package, not production activation. Do not close PM-029 or interpret synthetic shadow success as restored real Portfolio Fit.
