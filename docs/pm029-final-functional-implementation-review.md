# PM-029 final functional implementation review

Review date: 2026-11-15 UTC. **PM-029 remains OPEN.**

Branch: `feature/pm-029-final-functional-implementation`, based on `develop` at
`ce6f990c3d43b2ec7eb86873619c381fc76335f0`. Changes are unstaged and uncommitted.

## Review outcome

The safe implementation and isolated verification are complete. This is **not**
production financial activation, full-universe validation, or final real-portfolio UAT.

| Requested outcome | Verified result | Remaining boundary |
| --- | --- | --- |
| Real individual-stock Swing and Strategic scores | Actual NPI.TO Swing-v2 59; NVDA Swing-v2 6 and Strategic 27 | NPI Strategic is correctly unavailable; full 224-listing refresh not authorized |
| Evidence-based SETUP QUALITY | Advisory column, details, filter, sorting and export; structural evidence validated | Proposed grading policy is not approved; actual samples have no existing dynamic stop |
| Reliable Portfolio Fit | Shared-snapshot projection tested with stocks, ETF, cash and signed net options | Real cash/option provenance missing; production integration inactive |
| Company names in Symbol column | Provider metadata preserved; listing names preferred; ticker placeholders and underlying names rejected | Immutable legacy snapshot still contains 181 missing/ticker-only names |
| Highest scores first in each perspective | Independent defaults and legacy preference migration tested on four devices | Explicit saved user sorts remain respected |
| Safe FINAL ACTION compatibility | Existing policy unchanged; parity tests pass; sampled Add-like stored decisions are non-actionable | Fresh real-data/manual parity still pending |

**Why the live screen may still look stale:** the running backend was not restarted,
no write-producing opportunity refresh was invoked, and historical snapshot data was
not rewritten. Rebuilding code does not regenerate the immutable 224-row snapshot.
The new UI can explain missing historical evidence but cannot manufacture it.

### Evidence categories

- **Synthetic:** unit fixtures, financial arithmetic and all 28 screenshots.
- **Actual public market:** bounded capture/replay of NPI.TO, NVDA and SMH using the actual scanner and scoring engines.
- **Actual portfolio, read-only:** aggregate legacy snapshot coverage, schema checks and protected-table fingerprints.
- **Production integration:** NOT performed.
- **User manual UAT:** NOT completed.

## 1. Implemented changes by workstream

### Market and scoring data flow

- [YahooFinanceService](../backend/PortfolioManager.Api/Services/YahooFinanceService.cs)
  requests missing quote fields and preserves instrument type, currency and company
  metadata. Historical bars carry original-OHLC and currency provenance without
  changing reconstruction behavior for older consumers.
- [RsiScannerService](../backend/PortfolioManager.Api/Services/RsiScannerService.cs)
  prefers provider long/short company names and carries instrument type.
- [ValueScreenerService](../backend/PortfolioManager.Api/Services/ValueScreenerService.cs)
  prefers observed summary market cap, then observed quote market cap, before the
  existing estimate. Genuine estimates retain `MarketCapFallbackUsed`.
  Existing debug logging and valuation/tier formulas are preserved.
- [OpportunityScoringEngine](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityScoringEngine.cs)
  defers confirmed funds as `NotEvaluated` in both perspectives, after identity and
  failure checks. S1-S5 weights, Strategic formulas and FINAL ACTION are unchanged.
- [OpportunityService](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityService.cs)
  carries listing metadata and advisory evidence into future authorized snapshots.
  Existing historical readers remain immutable; batch throttling is retained.

### Advisory setup, Fit and presentation

- [SetupQualityCalculator](../backend/PortfolioManager.Api/Services/Opportunities/SetupQualityCalculator.cs)
  uses the existing confirmed pivot/move-away rules, rejecting unproven OHLC,
  currency mismatch, stale/future evidence and discontinuous sessions.
- [ValuationConsumerAdapter](../backend/PortfolioManager.Api/Services/Valuation/ValuationConsumerAdapter.cs)
  projects existing Fit from a shared shadow snapshot. It always remains
  non-authoritative and fails closed on incomplete financial inputs or policy mismatch.
- [Grid registry](../frontend/portfolio-manager-ui/src/app/core/store/grid-registry.ts),
  [view store](../frontend/portfolio-manager-ui/src/app/core/store/opportunities.store.ts)
  and [opportunity logic](../frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunities-logic.ts)
  provide Swing-only Setup Quality visibility/filtering, sorting/export, independent
  score defaults and safe migration of legacy saved preferences.
- [Details panel](../frontend/portfolio-manager-ui/src/app/features/trading-opportunities/opportunity-details-panel.component.html)
  shows source dates, support/resistance, original/conservative R and approval/missing
  reasons. Monetary evidence is masked and displayed in its analysis currency.
- [Progress register](pm029-master-implementation-progress.md) keeps PM-029 OPEN and
  records PM-036/PM-037 as deferred P2 items. No existing ID collision was found.

### Reproducible isolated evidence

[BoundedMarketCapture](../scripts/pm029/Phase14Verification/BoundedMarketCapture.cs)
invokes the actual scanner, quote enrichment, fundamental builder and replay engine.
Persistence/resolver/staged-service dependencies throw if used. It does not start
the API host or access the database. Only public response bodies are retained;
headers, cookies and credentials are excluded.

## 2. Actual Swing coverage and version

### Current-source public replay

Capture cutoff: `2026-11-15T20:34:32.4914643Z`; expected market session:
`2026-11-13`.

Scorer identity:
`ce6f990-api-working-tree-sha256-39a321a8401e2719cb1660362726b1f0d29ec7c005e0c4a4b52c649d7fa107f4`.
This identifies the base plus the API diff and new calculator content, not a new
committed release.

| Listing | Swing version | Numeric score | Status | Completeness |
| --- | --- | ---: | --- | ---: |
| NPI.TO | pm029-v2 | 59 | Provisional | 72.5% |
| NVDA | pm029-v2 | 6 | Provisional | 75% |
| SMH | pm029-v2 | null | NotEvaluated | 0% |

Two of two sampled individual stocks have numeric Swing scores despite unavailable
setup grades. Their denominator is 80; original Market R is not a score input.
Missing verified sector benchmarks produce `SectorHeat:NotEvaluated`.
Neither stock is claimed Verified.

### Stored real 224-row snapshot

The latest stored successful snapshot was computed at
`2026-11-15 10:38:22.7272988` UTC for session `2026-11-13`, with version `pm029-v1`.

| Swing status | Rows |
| --- | ---: |
| Insufficient | 219 |
| Provisional | 2 |
| Verified | 3 |
| Numeric scores | 5 / 224 |

The bounded v2 replay does not replace this snapshot or establish 224-listing v2
coverage. Controlled refresh and complete same-as-of inputs remain approval gates.

## 3. Actual Strategic coverage and missing reasons

Strategic retains `pm029-v1`; its methodology was not changed.

| Listing | Numeric Strategic score | Status | Completeness |
| --- | ---: | --- | ---: |
| NPI.TO | null | Insufficient | 62% |
| NVDA | 27 | Provisional | 90% |
| SMH | null | NotEvaluated | 0% |

NPI has `MissingEssential:T7`, `FundamentalsInsufficientFactors`,
`LowCompleteness` and `SectorHeat60D:NotEvaluated`.

- F1 has no usable positive EBIT/earnings-yield basis.
- Standard `marketCap` is absent from the observed quote and summary.
- The existing estimate is flagged; T7 excludes its F2 yield.
- F3 alone cannot satisfy the existing minimum valuation-factor requirement.
- Yahoo's separate `nonDilutedMarketCap` observation, approximately
  6,239,438,769, was **not** silently substituted for standard market cap.

The legacy Value Screener still reports NPI's estimated FCF-yield proxy of
4232.96% and its existing HighConviction tier. That is not an accepted PM-029
valuation factor or proof of investment quality. Correcting that separate
methodology requires explicit approval; the estimate is excluded here.

NVDA's observed cap and F1-F5 evidence are available. T7 is Observed even though
its factor points are zero: a low score is distinct from missing evidence.
The final enriched recapture yields **27**, superseding the preliminary 25.

Stored 224-row Strategic coverage: **20 Insufficient, 80 Provisional,
124 Verified; 204 numeric**. These are legacy results, not current-source
full-universe verification.

## 4. ETF exclusions and classification accuracy

Observed provider types: NPI.TO `EQUITY`, NVDA `EQUITY`, SMH `ETF`.
SMH returns `NotEvaluated` / `EtfScoringDeferred` in both perspectives, not zero
or a manufactured stock-methodology score.

Only this single actual ETF classification is validated. Missing type does not
imply ETF, and a wrapper is not classified from its underlying. Identity errors
still fail closed before fund deferral.

The 15 captured public responses include the existing summary-provider fallback:
SMH and SMH.TO summary requests both return 404. SMH.TO is not scored or added as
a fourth listing. No bulk-universe acquisition was performed.

ETF-specific scoring/benchmarks remain PM-036, P2 deferred.

## 5. Setup Quality policy and approvals

Policy identifier: `pm029-setup-proposal-v1`.
**All real call sites use `classificationApproved=false`.**
Good/Caution/Poor screenshots simulate approval in fixtures only.

Proposed baseline:

| Grade | Proposed rule, after trustworthy evidence checks |
| --- | --- |
| Poor | Known invalid existing stop bounds, or conservative R below 1.5 |
| Caution | Stop below 1 ATR, above confirmed support, target beyond first resistance, or conservative R from 1.5 to below 2 |
| Good | Otherwise valid candidate with conservative R at least 2 |
| Unavailable | Missing/unverified evidence or unapproved grading policy |

Existing stop bounds are preserved: 0.5-4 ATR, maximum 15% risk, existing
actionable R threshold 1.5. The proposal does not assign a broker stop, replace
the existing candidate, add a score factor or alter FINAL ACTION.

The existing pivot definition uses two bars on either side and the existing
meaningful move-away predicate of at least 1 ATR. Later closes breaking a level
exclude it. Continuous market sessions and original OHLC/currency provenance
are required.

## 6. Stop/target evidence and conservative R

The credible target is the lesser of the existing proposed target and first
confirmed resistance. Entry and stop remain unchanged.

| Synthetic fixture | Entry | Stop | Proposed target | Resistance | Conservative R | Simulated grade |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Valid baseline | 100 | 95 | 110 | 111 | 2.00 | Good |
| Optimistic target capped | 100 | 95 | 120 | 111 | 2.20 | Caution |
| Stop above support 95 | 100 | 96 | 110 | 111 | 2.50 | Caution |
| Weak payoff | 100 | 92 | 103 | 111 | 0.375 | Poor |

These are synthetic arithmetic cases, not trade recommendations.

Actual NPI evidence: CAD entry 23.86000061, ATR 0.5622, support 21.43000031
on 2026-10-15 and resistance 23.95000076 on 2026-04-28. The actual standalone
scanner supplies no dynamic stop; therefore no target/R or grade is fabricated.
Reasons include `NoValidStop`, `DynamicStopMissing`,
`SetupPolicyApprovalRequired`.

NVDA/SMH structural inputs also report `StructuralSessionGap` and unavailable
confirmed support/resistance. Provider history and the existing exchange calendar
do not fully reconcile for this range. No calendar exceptions were guessed or
shared calendar policy changed. A complete verified package is required before
those levels can support a grade.

## 7. Portfolio Fit status and financial blockers

The inactive projection is executable and tested against the same consolidated
CAD snapshot consumed by all five adapters.

Synthetic NVDA/SMH case: USD stocks converted to CAD 2,800; cash CAD 780;
long option +20; short option liability -100; consolidated net **CAD 3,500**.
Stock-only sector denominator remains CAD 2,800. NVDA weight is 40%, sector
weight 50%, Fit Eligible. There is no duplicate option-underlying stock component.
Unknown option currency makes Fit Unavailable, not zero or an assumed CAD value.

**Actual stored Portfolio Fit remains Unavailable for all 224 rows.**

Remaining authoritative evidence:

1. 71 cash records have no schema-level currency; five have null owners.
   Account/ownership mapping, currency and complete ledger reconciliation must be
   confirmed before isolated backfill/rehearsal.
2. 30 historical option transactions lack complete authoritative currency,
   signed position side, contract multiplier and quote/session provenance.
   They are transactions, not automatically 30 open positions. Open-position
   reconciliation and closed/history handling must be explicit.
3. Stocks/ETFs, currency-specific cash and signed net options must share the
   approved valuation as-of and verified FX availability.
4. Owner-approved schema/backfill, isolated rehearsal and controlled consumer
   activation remain separate steps.

No cash currency, ownership, option side or multiplier was inferred. No production
valuation authority was registered. The existing Bank of Canada availability
safeguards remain; expected publication time is not proof of actual availability.

## 8. Symbol/company-name verification

The Symbol column presents company name with the symbol. Provider metadata is
preserved by the scanner; future rows prefer the listing quote name, reject
ticker-only placeholders, and do not use an underlying's company name for a wrapper.
Lot, mapping and value-description fallbacks remain available. Cached quote
name/type metadata must match the listing identity; mismatches are rejected with
an explicit warning, without modifying existing financial calculations.

Actual captured names include **Northland Power Inc.** and **NVIDIA Corporation**;
SMH is provider-classified as ETF. Four-device UI tests verify company-name
visibility using clearly synthetic securities.

The stored snapshot still has **181/224 missing or ticker-only names**.
Those immutable rows were not rewritten. Fresh authorized data-flow validation is
needed to measure actual universe-wide name coverage; fixture screenshots do not
prove all real names are fixed.

## 9. Default sorting verification

Swing opens by descending Swing score; Strategic opens by descending Strategic
score, with unavailable/deferred scores at the bottom. The Setup Quality column
is Swing-only and does not replace either default numeric score sort.

Legacy migration no longer leaks one perspective's explicit sort into the other.
Saved explicit sorts and column visibility remain respected. Default ordering,
switching, fund deferral and responsive presentation pass on desktop, tablet,
384px S21 and 360px S23.

## 10. FINAL ACTION compatibility

No FINAL ACTION formulas, policy, sizing authority or immutable decisions changed.
Focused backend parity and UI tests preserve actions through score/quality changes.
Manual R remains separate from Market R, Swing score and advisory quality.

Read-only inspection found one `Add / Needs Review` row and one
`Hold / Add on Pullback` row despite unavailable Fit. Inspecting their constituent
decision metadata shows **all sampled decisions are `isActionable=false`** with
`allocationStatus=MixedCurrency`, not authorized add instructions.
The Add / Needs Review decision explicitly includes
`ALLOCATION_EVIDENCE_UNAVAILABLE` and `STOP_EVIDENCE_UNVERIFIED`.

Their `evaluationState=CurrentValid` describes retained legacy decision evaluation,
not approved shared financial evidence. Add-like wording still warrants manual
review for clarity, but this inspection does not establish actionable unsafe trades.
No labels or financial policies were silently changed.

## 11. Tests and focused screenshots

| Validation | Result |
| --- | --- |
| Focused backend calculators/setup/replay/shadow/Fit/opportunity/CAD/cash/trade links | 199 passed |
| Final OpportunityService name/identity regression run | 14 passed: 11 existing reruns plus 3 new matching/mismatched/ticker-name cases |
| Focused Angular feature/store/registry tests | 94 passed |
| Four-device focused Playwright/regression suite, retries disabled | 72 passed, 8 intentional handset-only skips on desktop/tablet |
| Final screenshot recapture | 4 passed; reruns, not additional distinct coverage |
| Angular production build | Passed; six existing unrelated SCSS budget warnings |
| API Release build | Passed; two existing Windows EventLog platform warnings |
| Verification CLI Release build | Passed, zero warnings/errors |
| Release CLI replay of the final public input | Deterministic; exactly matches captured results |
| Whitespace check | Passed |

The backend runs cover 202 distinct focused cases in total, not 199 plus 14
additional cases. One new Angular masking test initially omitted its detail
fixture; supplying the full existing detail fixture resolved it. A new backend
assertion initially mixed an enum with the string DTO status; it was corrected
before the final 14-case pass. No failure is hidden by retries.

**28 fresh focused screenshots**, seven per device:

1. Inactive Swing quality and company names.
2. Historical detail missing-evidence explanation.
3. Simulated Good/Caution/Poor grid.
4. Simulated advisory detail.
5. Lower structural evidence/Market R detail.
6. Fund deferral and preserved action context.
7. Strategic highest-score-first default.

Directory: [focused screenshots](screenshots/pm029-final-functional-20261115-2010Z/).
The timestamp in the directory name is an initial capture label; the manifest
records final per-file UTC timestamps and SHA256 hashes. All images carry
`SYNTHETIC | NOT PRODUCTION`. Simulated grades are not production approval.

Delivery:
[fresh review ZIP](pm029-final-functional-review-20261115-2035Z.zip), containing this
report, 28 PNGs, a screenshot manifest and current public-market replay/source bodies.
The public evidence package retains provider observations and source hashes:
[current market evidence](pm029-final-market-evidence-current/).

## 12. Git state and database integrity

- Separate branch from the verified develop base; no staging, commit, push,
  merge, deployment or automatic trade.
- No migration/backfill/seed scripts applied; no protected opportunity refresh.
- No API host started or restarted. User-started frontend/backend were preserved.
- The isolated Playwright helper on port 4307 terminated after testing.
- Manual changes are confined to related contracts, source adapters, advisory/Fit
  projection, presentation, tests and requested evidence. Some touched frontend
  diffs include installed-Prettier formatting; diff line count is not a feature count.
- SHA256 full-row fingerprints were compared for eight protected tables.
  CashItems (71), OptionItems (30), PortfolioItems (141), WatchlistItems (165),
  OpportunitySnapshots (5), OpportunityRefreshStates (1), and
  ValueScreenerSnapshots (11) were unchanged.
- **RsiScanSnapshots (one row) changed during the shared-running-application
  validation window.** Before:
  `20EEF0BE661A6566530EADDB4A50F0A6A3B9CB6BE67BE15C812028F866FF8550`;
  after:
  `F68A4D207F36F8CED6455F9DE580D93F371AC1D40C1427C3D5BFB9AA09221380`.
  This session did not invoke its writer. The writer cannot be attributed from
  these hashes; the database was not frozen. No claim that all database rows
  remained unchanged is made.

Only aggregate portfolio observations are included. Private approval manifests,
account details and raw portfolio records are excluded from the ZIP.

To reproduce the final replay locally without provider acquisition or an API host
(use a new output file):

```powershell
dotnet run --project scripts\pm029\Phase14Verification\Phase14Verification.csproj -p:UseAppHost=false -- replay docs\pm029-final-market-evidence-current\replay-input.json <new-output.json>
```

When a user-started API locks normal build outputs, also supply an isolated
`-p:BaseOutputPath=<isolated-directory>` as done for this validation.

## 13. Remaining explicit owner approvals

1. Approve or revise the Setup Quality proposal and its evidence/candidate rules.
   Until then classifications remain inactive, even when structural evidence exists.
2. Confirm authoritative cash/account ownership and option position/currency/side/
   multiplier/quote evidence. Approve isolated schema/backfill rehearsal before
   separately approving real financial integration.
3. Decide whether an alternative NPI cap basis is acceptable. Do not automatically
   use non-diluted cap or weaken T7 to force a score.
4. Approve a controlled full-universe acquisition/refresh with verified benchmarks,
   consistent as-of data, calendar reconciliation and captured scoring version.
5. Approve controlled backend restart/integration and live financial UAT after
   the preceding safeguards. No production valuation activation is implied by
   accepting this report.

There is no remaining broad architecture investigation proposed. The blockers are
specific source/policy/financial-data approvals and controlled validation.

## 14. Final manual UAT checklist

### Safe presentation review now

- [ ] Review all seven screenshot states on each of the four devices.
- [ ] Check Symbol/company-name readability and no horizontal page overflow.
- [ ] Check independent descending score defaults with fresh preferences, then
  explicit saved sorts.
- [ ] Verify Swing-only Setup Quality, historical Unavailable reasons and masked
  evidence currency.
- [ ] Confirm ETF NotEvaluated is not displayed as zero.
- [ ] Verify Manual R editing stays separate from Market R, numeric scores and
  FINAL ACTION.

### Real-data closure checks after explicit approvals

- [ ] Run approved complete 224-listing refresh; reconcile score counts, versions,
  identity, benchmarks and missing-factor reasons against raw inputs.
- [ ] Inspect real NPI/NVDA and representative CAD/USD stocks. Low scores must
  remain distinguishable from missing inputs.
- [ ] Reconcile names across the full real universe; do not count synthetic names.
- [ ] Approve/test setup evidence on actual candidates with an existing valid stop;
  no invented stop or target to force availability.
- [ ] Reconcile stocks, ETFs, each cash currency and signed net options against
  broker/account evidence, with no double counting.
- [ ] Verify the same approved CAD snapshot across Portfolio, Dashboard,
  Allocation, FINAL ACTION and PM-029 Fit; check failed provenance remains blocked.
- [ ] Check real FINAL ACTION recommendations, non-actionable review states and
  portfolio-aware safety against the approved fresh evidence.
- [ ] Owner completes real desktop/mobile/tablet UAT before closing PM-029.

**Conclusion:** build-ready gated implementation and review evidence delivered;
real Portfolio Fit restoration, complete Swing-v2 coverage, activation and PM-029
closure remain explicitly unapproved/unverified.
