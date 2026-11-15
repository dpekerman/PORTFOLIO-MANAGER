# PM-029 Phase 13 — Integration Readiness

**Disposition: PM-029 remains OPEN.** An inactive CAD valuation implementation and a deterministic Swing-v2 replay design are ready for review. Production valuation, schema changes, score refresh, and live consumer activation were not performed.

## Executive summary

| Workstream | Phase 13 result |
|---|---|
| Local SQL access | **CONNECTED**, using the existing local configuration and read-only SQL intent |
| Timestamp anomaly | The raw SQL values are not in the future when interpreted as UTC. The prior five-hour shift is consistent with UTC `datetime2` values being converted as Eastern local time. A converter regression test was added. |
| CAD valuation | Implemented as reusable API-assembly contracts and pure calculators; not registered, routed, or consumed |
| Cash and options contracts | Gaps confirmed from source and aggregate data; no guesses, migrations, or backfills |
| Bank of Canada evidence | Actual `FXUSDCAD` observation fixtures and an explicit, coverage-bounded publication calendar contract added |
| Three-session FX age | Exercised as a caller-supplied provisional test input; no production default or policy activation |
| Real Swing-v2 replay | **BLOCKED**: the saved data does not contain a complete, same-as-of v2 input set for the previously reported 224-listing universe |
| UI and screenshots | No UI changes or new screenshots; the existing Phase 10 ZIP was re-verified during final handoff |

This report distinguishes actual database aggregates, official Bank of Canada observations, mocked-provider/unit tests, and unperformed production UAT. The 224-listing count and legacy score coverage are Phase 11/12 findings; Phase 13 did not recompute the full universe.

## 1. Baseline and safety boundary

All work was isolated in `D:\PM029-PHASE13`, branch `feature/pm-029-phase-13-readiness`, created from `develop` at `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22`.

| Worktree | Branch / HEAD | Phase 13 treatment |
|---|---|---|
| `D:\PORTFOLIO-MANAGER` | `feature/pm-029-final-review` / `63662b1438ae1b1d4362f101633e58ddb224b2ce` | Preserved; clean before and after |
| `D:\PM029-PHASE12` | `feature/pm-029-phase-12-prototype` / `c07259ae5aa5dd234b9de0fac4fa30f5269cb87e` | Preserved; clean before and after |
| `D:\PM029-PHASE13` | `feature/pm-029-phase-13-readiness` / base `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22` | New isolated worktree; implementation and report are uncommitted |

The active feature branch is two commits and 63 changed paths ahead of `develop`; Phase 12 is one commit and three changed paths ahead. They are separate branches. No cherry-pick, merge, commit, push, deployment, migration, seed, application refresh, or database write occurred. The existing API process was left running and was not stopped.

The local database was queried through the existing integrated-auth configuration with `sqlcmd -K ReadOnly`, using aggregate `SELECT` statements only. No credentials or connection strings are included here.

## 2. Timestamp integrity finding

SQL Server reports `datetime2(7)` timestamps without timezone metadata. At the Phase 13 check, SQL Server reported:

- UTC: `2026-11-15 15:38:17.5178692`
- Local: `2026-11-15 10:38:17.5178692 -05:00`
- `OpportunitySnapshots.MAX(ComputedAtUtc)`: `2026-11-15 10:38:22.7272988`
- `OpportunitySnapshots.MAX(FinalActionComputedAtUtc)`: `2026-11-15 10:37:44.7432646`
- `PortfolioSnapshots.MAX(UpdatedAt)`: `2026-11-15 13:53:05.3787520`

The raw opportunity and portfolio values are earlier than the SQL UTC clock. Treating the raw `10:38` and `13:53` values as Eastern local time and converting them to UTC adds five hours, producing `15:38` and `18:53`—the apparent “future” values reported earlier. The raw opportunity value and the earlier displayed `15:38` value align to the same ticks. The Phase 13 evidence therefore identifies a UTC/local interpretation error in the earlier timestamp diagnostic, not a demonstrated future database write.

`AppDbContext` already applies a value converter to `DateTime` fields ending in `Utc` or `At`: writes preserve the ticks; reads mark them `DateTimeKind.Utc`. The new `AppDbContextUtcConverterTests` verifies this mapping behavior and UTC JSON serialization. This test is a model-converter regression test, not a live SQL read/write test. Historical timestamps were not modified.

The timestamps remain a data-quality concern if any other reader bypasses this converter or applies `ToUniversalTime()` to an unspecified SQL value. Future freshness comparisons must preserve the UTC convention end-to-end.

## 3. Existing cash and option contracts

Read-only aggregate results from the local database:

| Table | Aggregate |
|---|---:|
| `CashItems` | 71 rows; 5 shared/unowned rows; 0 rows with null `AccountType`; 6 `OpeningBalance` rows; 48 trade-linked rows |
| Duplicate `(SourceType, SourceItemId)` cash links | 0 |
| `OptionItems` | 30 rows; 10 `OPEN`; 20 `CLOSE`; 3 stored `MarketPrice` values are non-positive |
| Currency columns on the inspected cash/option/account/portfolio tables | None |

The existing filtered unique indexes are evidence worth preserving:

- `IX_CashItems_TradeLink` prevents more than one cash row for a `(SourceType, SourceItemId)` trade leg.
- `IX_CashItems_Account_OpeningBalance` permits at most one opening-balance row per account and date.

`CashService` records signed cash-ledger entries, and `CashLedgerQueryService` sums entries through an effective date, including the opening balance. `CashFlowTypeRules` distinguishes deposits and withdrawals as external cash flows. These are ledger movements, not independent portfolio assets or investment profit. A future adapter must value an aggregated currency-specific ledger balance once; it must not add linked trade entries again as separate holdings.

The current contract is insufficient for common-currency valuation:

- `CashItem` has no currency field. `AccountType` is a label, not a currency-bearing cash account. One account may contain both CAD and USD cash.
- The current opening-balance unique key omits currency. If a multi-currency account needs same-date CAD and USD opening balances, the index must be revised or a currency subledger introduced.
- `CashLedgerQueryService.GetTotalAsOfAsync` scopes by account type but cannot produce per-currency balances.
- `OptionItem` has CALL/PUT and OPEN/CLOSE fields, but no authoritative quote currency, quote timestamp/source, signed position side, or verified contract multiplier. CALL/PUT is not long/short. The current positive contract count and `×100` convention are not adequate evidence for every contract.
- Three option rows have a stored mark at or below zero; those rows were not individually inspected or repaired.

The inactive valuation input contracts require verified currency evidence, quote/ledger timestamps, explicit signed option contracts, and a verified multiplier. They do not parse descriptions or ticker suffixes. The valuation input for cash is one complete balance per currency bucket, not a list of transaction rows.

### Schema and backfill proposal — approval required

1. Decide whether cash needs a stable account identity in addition to the current account-type label. Represent CAD and USD balances separately, either with explicit currency on each ledger row or a currency-specific subledger.
2. Persist ISO currency and verification provenance (`source`, verification time/status). Preserve the source-trade unique key. If using per-row currency, revise the opening-balance key to include currency or define an equivalent per-currency subledger invariant.
3. Backfill only from authoritative broker records or owner confirmation. Keep ambiguous legacy rows explicitly unknown and exclude them from CAD weights; never infer currency from account descriptions.
4. Add option contract identity, currency, signed side/count, verified multiplier/terms source, and quote price source/time. Leave unknown legacy contract terms unvalued until confirmed; do not infer side from CALL/PUT or currency from the underlying ticker.
5. Add migrations only after a separate approval. First produce a read-only candidate/backfill report; never apply guessed values.

## 4. Inactive CAD valuation implementation

The reusable contracts are in `backend/PortfolioManager.Api/Models/Pm029CadValuationModels.cs` and `backend/PortfolioManager.Api/Services/Valuation/`.

Implemented behavior:

- `BankOfCanadaFxProvider` requests only the official `FXUSDCAD` series and verifies its unit is CAD per USD. It records the injected clock’s retrieval/first-observed time; the Valet response does **not** provide an item-level official publication timestamp.
- `ExplicitFxPublicationCalendar` requires every date in its declared coverage range to be classified as a publication session or non-publication day. Publication sessions use the Bank of Canada page’s 16:30 ET expected cutoff, with Eastern daylight/standard-time conversion. Incomplete calendar coverage fails closed. It does not derive BOC sessions from TSX or NYSE calendars.
- The calculator requires a caller-supplied maximum FX age; there is no production default. USD conversion uses CAD-per-USD; CAD does not require FX; unsupported currency, missing evidence, conflicting same-date rates, future/unobserved data, stale FX, or incomplete calendar coverage suppress the denominator.
- Security exposure requires a positive, timestamped market quote for the expected listing session and matching listing/quote currency. Average cost is not an input and cannot substitute for a quote. A CDR is valued in its verified listing/quote currency, not its analysis-underlying currency.
- Cash requires verified currency and a balance complete through the valuation session. Duplicate linked-trade keys across selected cash balances fail closed.
- Options require verified currency/terms, a quote and timestamp, signed integer contract count, and positive verified multiplier. Signed market value is `price × signed contracts × multiplier`.
- Ownership includes the requesting user’s rows and shared (`UserId == null`) rows. Account-specific scope is supported. Duplicate component identifiers are rejected.
- Each component result retains its complete input evidence alongside the CAD value and availability status, including currency verification, quote/ledger/option provenance, and FX observation details.
- The consolidated denominator includes stock, cash, and option values. Sector exposure uses the stock-only denominator. Position limits remain hard limits; sector targets use the supplied tolerance. Mixed-role holdings do not silently select one role’s position limit.

This code is compiled into the API assembly but is deliberately inactive: no controller, DI registration, EF mapping, schema change, or consumer was added. There is no `Program.cs` registration or call site. Existing Portfolio, Dashboard, PM-029, FINAL ACTION, and current Portfolio Fit behavior are unchanged.

### Bank of Canada evidence and calendar limits

Official references checked on 2026-11-15:

- [Bank of Canada daily exchange rates](https://www.bankofcanada.ca/rates/exchange/daily-exchange-rates/) states daily average rates are published once each business day by 16:30 ET, expressed as one unit of foreign currency in CAD, and publication can be delayed.
- [Valet FXUSDCAD observations, 2025-11-26 through 2025-12-03](https://www.bankofcanada.ca/valet/observations/FXUSDCAD/json?start_date=2025-11-26&end_date=2025-12-03) provided actual observations including 1.4059 (Nov 26), 1.4034 (Nov 27), 1.3979 (Nov 28), and 1.3979 (Dec 1).
- [Valet FXUSDCAD observations, 2026-11-09 through 2026-11-13](https://www.bankofcanada.ca/valet/observations/FXUSDCAD/json?start_date=2026-11-09&end_date=2026-11-13) provided 1.4123 (Nov 9), 1.4181 (Nov 10), 1.4255 (Nov 12), and 1.4257 (Nov 13). Nov 11 is explicitly marked “Bank holiday” on the daily page and has no series observation.

The 2025-11-27 observation is present even though the NYSE is closed that day. This demonstrates why the BOC publication calendar cannot be inferred from a stock-exchange calendar. The tests use official observation values but mock HTTP and inject retrieval times; they do not prove an actual historical release timestamp. The explicit calendar is a bounded contract and test fixture, **not** a complete production holiday calendar. A verified full calendar source and actual delayed-publication evidence remain prerequisites to activation.

The three-publication-session limit is exercised at the boundary (3 accepted, 2 rejected) as an explicit test argument only. It remains provisional; the test does not constitute approval of a three-session production rule.

## 5. Swing-v2 replay and write boundary

The preserved feature branch contains `pm029-v2`; the isolated Phase 13 worktree is based on `develop`, so the v2 implementation was not copied or merged. On the preserved branch, `OpportunityScoringEngine.ScoreSwing` is a pure calculation. Its input does not require a saved stop, Manual R, Market R, ownership, or a holding. The existing `Swing_NoValidStop_DoesNotAffectTechnicalScore_AndStrategicStillScores` test and the focused scorer suite passed.

The actual stored opportunity snapshot was reported in Phase 12 as `pm029-v1`; its scores cannot establish current v2 coverage. The saved rows do not contain a complete same-evaluation set of v2 inputs (including the relevant MACD histogram delta, volume confirmation, price-structure evidence, current scan and benchmark history). The later partial RSI snapshot cannot safely be combined with the older opportunity snapshot. Thus the previously reported 224-listing universe still has **no verified Phase 13 v2 score coverage result**. No refresh or approximate replay was run.

`OpportunityService.RefreshAsync` is not a read-only replay route: it writes refresh state and opportunity snapshots, prunes older snapshots, and its build path can refresh/persist universe fundamentals. It also calls market-data and scoring dependencies. Those effects remain outside authorization.

### Reproducible replay design

The next authorized replay should be a separate, in-memory runner over an immutable, complete same-as-of input package:

1. Freeze `AsOfUtc`, each listing’s verified identity/listing currency/exchange and expected market session.
2. Capture for every required symbol the exact `RsiScanResult` evidence used by Swing-v2, including price, moving averages, RSI/signal, MACD histogram/delta, volume/confirmation, ATR, chase/Fibonacci, and price-structure inputs.
3. Include the same-as-of sector classification, verified benchmark mapping, split-adjusted listing and benchmark histories needed for 20/60-session relative strength, and the complete deduplicated union membership.
4. Call only the checked-out v2 pure calculator for each row. Record scorer commit/version and per-row missing-evidence reasons. Do not mix later snapshots or substitute synthetic inputs for real records.
5. Guard the runner by giving it no `AppDbContext`, refresh service, persistence service, or live HTTP client. Tests must assert deterministic serialized output on repeat runs and unchanged protected-state hashes. If a database-backed integration test is required, use a disposable isolated test database and deny production connection configuration.

Required sources are an exact raw scan/input snapshot for all 224 listings, same-as-of benchmark histories and mapping records, and a single frozen evaluation time. The current saved snapshot set is incomplete, so the exact replay remains blocked pending an authorized read-only acquisition/export path. No new replay runner or database refresh was introduced in Phase 13.

## 6. Verification

| Check | Result |
|---|---|
| Phase 13 valuation/provider/UTC tests plus existing cash-ledger and trade-link tests | **66 passed, 0 failed, 0 skipped** |
| Preserved feature branch PM-029 calculator/service/final-action/safety tests | **93 passed, 0 failed, 0 skipped** |
| Preserved feature branch cash-ledger, trade-link and Swing-v2 calculator tests | **112 passed, 0 failed, 0 skipped** |
| API build in Phase 13 worktree | **Succeeded, 0 errors** |
| Full backend suite | Not run |
| Real 224-listing Swing-v2 replay | **Blocked**; no complete same-as-of raw inputs |
| Production DB writes, migrations, refresh, or score backfill | Not run; not authorized |
| UI / responsive / screenshot tests | Not applicable; no UI change |

The preserved feature branch already had an API process holding its normal apphost file. The first test-build attempt from that worktree was blocked before test execution by that file lock. It was rerun successfully with an isolated output directory; the running process was not stopped. The API's initial compile emitted two pre-existing Windows-only `CA1416` warnings in `Program.cs`; existing unrelated test warnings also appeared. No warning was emitted from the new valuation code or tests.

## 7. Screenshots

No Phase 13 screenshots were created because no UI changed. The previously captured Phase 10 package was re-verified during final handoff:

- `D:\PORTFOLIO-MANAGER\docs\screenshots\pm029-phase-10-integrity-uat.zip`
- Verified archive: 71 ZIP entries, including 70 PNGs
- Verified SHA-256: `E137271DCCF672F771A5F0D920442F8E995897059070CE28E8AA3103A38214CF`

This is synthetic UI evidence, not real-data validation. Its hash differs from the older Phase 10 report’s recorded hash; that discrepancy remains unresolved.

## 8. Approval gates and recommendation

Previously approved in principle: Bank of Canada `FXUSDCAD`, CAD-per-USD direction, and no average-cost fallback for current market exposure. These approvals do not authorize production activation.

Before Phase 14 integration, the owner must decide:

1. The authoritative cash account/currency contract, including how CAD and USD opening balances coexist and how unknown legacy rows remain excluded.
2. The authoritative options source for currency, side, multiplier, contract identity, and marked-value treatment.
3. The source and coverage of the full BOC publication calendar, the publication/availability timestamp to persist, and whether to approve the provisional three-publication-session age after calendar and delay tests.
4. The migration/backfill sequence and the approved shadow-validation scope across Portfolio, Dashboard, PM-029, and FINAL ACTION.
5. Whether to authorize a separate read-only same-as-of input acquisition for the 224-listing Swing-v2 replay. A production refresh/backfill must be authorized separately.

**Recommendation:** retain the inactive calculator and keep existing Portfolio Fit fail-closed. Do not register or consume the new valuation components until cash/options provenance, account scope, FX publication availability, calendar coverage, and a shadow comparison are approved and verified. Keep PM-029 **OPEN**; no scoring formula change is indicated or authorized.
