# PM-029 Phase 12 — Isolated CAD Valuation and Swing-v2 Replay

**Disposition: PM-029 remains OPEN.** The valuation candidate is isolated in the test project only. No production calculation was activated, and the available stored inputs do not support an exact 224-listing Swing-v2 replay.

## Executive summary

| Work item | Result |
|---|---|
| CAD-equivalent valuation prototype | Implemented as deterministic test-only code; not registered or called by the API |
| Real Swing-v2 replay | **Blocked**: the saved rows do not retain all scorer inputs; the latest raw RSI snapshot is partial and later than the opportunity snapshot |
| Cash and option contracts | Gaps confirmed; currency, quote provenance, ownership, and option terms require authoritative contracts |
| Production activation / refresh | **Not authorized and not performed** |
| Database writes, migrations, seeds | **None** |
| UI changes / new screenshots | **None**; this phase is backend-test-only |

Phase 12 turns the previously approved FX direction into an executable, isolated contract for review. It does not choose a final freshness policy, provide a Bank of Canada client, change any financial record, or modify `FinalActionService`, Portfolio Fit, FINAL ACTION, the production database, or scoring methodology.

## 1. Baseline and safety boundary

- The prototype worktree is `D:\PM029-PHASE12`, branch `feature/pm-029-phase-12-prototype`, based on `develop` at `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22`.
- The existing worktree `D:\PORTFOLIO-MANAGER` remains on `feature/pm-029-final-review`, HEAD `5a5e1ea14709ce426c9992adfc848316b096dc2e`. Its staged-diff fingerprint at Phase 12 start and final verification is `bf0fb8b9560f9fda3cbaf9e26baac67c4c4974d4`.
- Only the two new test-project source files and this report are Phase 12 changes. Nothing was staged or committed. The preserved feature worktree and index were not changed.
- The Phase 12 database inspection used only SELECT queries through the already configured local connection. No application startup, refresh endpoint, save, migration, seed, or database write was run.
- No live price or FX request was made by the prototype. All prototype input is supplied explicitly to a pure calculation.

## 2. Isolated valuation candidate

Implementation:

- `backend/PortfolioManager.Tests/Pm029CadValuationPrototype.cs`
- `backend/PortfolioManager.Tests/Pm029CadValuationPrototypeTests.cs`

The prototype accepts explicit component and FX evidence and returns per-component native and CAD values, provenance, fail-closed issue codes, the consolidated denominator, and stock-only total. It is internal to the test assembly and has no API registration or production call site.

### Candidate contract

| Concern | Isolated prototype behavior |
|---|---|
| Reporting currency | CAD only; other requested base currencies fail closed |
| FX authority / pair | Accepts only Bank of Canada `FXUSDCAD`, explicitly `USD` base to `CAD` quote; multiplies USD market value by CAD-per-USD rate without rounding |
| FX observation timing | Observation date must be no later than the valued component's quote session and the valuation session; the publication timestamp must be known and no later than the requested as-of time |
| Weekend / holiday handling | Reuses the latest eligible observation; age is counted against caller-supplied publication sessions and publication cutoffs |
| FX freshness | Configurable in the prototype, default test value three publication sessions; **three sessions remains provisional**, not an approved production policy |
| Security quotes | Requires a positive price, source, source timestamp, matching expected exchange session, and timestamp at/after the expected close and no later than valuation as-of |
| Missing price | No average-cost fallback; native and CAD market value remain unavailable |
| Cash | Requires verified currency, ledger balance, ledger source and ledger as-of time; cash is not treated as investment return |
| Options | Requires verified currency, quote evidence, signed contracts and an explicitly verified positive multiplier; does not infer currency or contract side |
| Scope | Filters by requesting user and account scope; null owner is treated as shared. Duplicate component IDs in the selected scope are rejected |
| Denominator | Stocks, cash and option market values are summed once from the supplied component list. Non-positive totals fail closed |
| Allocation evidence | Position weight uses the consolidated denominator; sector weight uses stock-only value. Position limits remain distinct from sector targets. Mixed roles aggregate position value but do not select a role-specific limit |

The calendar, expected exchange session, currency verification, quote source, ledger evidence and option-term verification are caller inputs. The prototype does not independently establish these facts. It also does not fetch Bank of Canada data, validate a holiday calendar, implement production caching, deduplicate cash against transaction legs, or make a current market valuation.

### Focused test coverage

The 19 tests cover:

- CAD-only and mixed CAD/USD conversion with unrounded rates
- Weekend reuse, delayed publication, caller-supplied no-publication dates and stale-rate rejection
- Security-session cutoff and historical no-lookahead for future-dated or later-published FX
- Missing FX, unsupported currency, missing quote, wrong quote session and no cost-basis fallback
- User/account scope and shared components
- Signed option value and unverified option terms
- Zero/negative denominator
- Separate position/sector denominators, target tolerance, and multi-role limit ambiguity

This is synthetic unit evidence for the candidate contract only. It is not production-universe or market-data validation.

## 3. Actual-data evidence and replay feasibility

The authorized local database snapshot inspected for this phase was the same stored evidence described in Phase 11:

| Evidence | Stored result |
|---|---:|
| Distinct normalized listings in the Watchlist + open non-manual portfolio + active TSX 60 union | 224 |
| Latest opportunity snapshot | 224 rows; `pm029-v1`; market session 2026-11-13 |
| Stored Swing-v1 | 5 with scores (3 Verified, 2 Provisional); 219 Insufficient |
| Stored Strategic-v1 | 124 Verified, 80 Provisional, 20 Insufficient |
| Stored Sector Heat | 190 Matched, 21 Unmapped, 13 Unclassified |
| Latest standalone RSI snapshot | 29 symbols; only 25 overlap the opportunity set on the same trading date |
| `DailySignals` | 145 rows across 95 symbols; not an exact input bundle aligned to the opportunity evaluation |

The persisted opportunity rows contain scores, reasons and display indicators, not the complete `OpportunityScoringInput`. The persisted indicators omit evidence required by the current v2 scoring path, including `MacdHistDelta`, raw `Volume`, `VolumeSignal`, `RsiSignalAvailable`, `Has200Dma`, and `PriceStructure`. The 29-symbol RSI snapshot is later than the opportunity snapshot and is not a full-universe, same-evaluation source. Combining these artifacts would mix times and cannot reproduce the actual v2 evaluation.

**Conclusion: exact 224-listing replay is not feasible from the available stored evidence.** No approximate replay or synthetic substitution was presented as real coverage. A current refresh/rebuild would write snapshot/state and may persist fundamentals; it remains outside this authorization. A sanitized, complete, same-as-of export of the exact raw scoring inputs is an alternative if it can be produced without exposing private data.

### Legacy Swing findings are not v2 findings

The 5 scored / 219 insufficient rows and the legacy `MissingEssential:S6` / stop-related reasons belong to the stored `pm029-v1` snapshot. The preserved feature worktree contains Swing `pm029-v2`, with a fixed 80-point S1–S5 technical pool. Its scorer does not take a saved stop, setup, Manual R, Market R, or ownership as an input. The existing test `Swing_NoValidStop_DoesNotAffectTechnicalScore_AndStrategicStillScores` verifies stop/setup independence. The focused `Pm029CalculatorTests` run in that preserved v2 worktree passed **72/72**.

This establishes current scorer independence and validates its unit cases; it does **not** establish current v2 coverage counts for the real 224-listing universe.

### Timestamp caution

The stored timestamps are anomalously later than the observed host UTC time: opportunity snapshot `2026-11-15T15:38:22Z`, latest portfolio snapshot `2026-11-15T18:53:05Z`, and latest RSI scan `2026-11-15T19:35:45Z`, while the observed host time was `2026-11-15T14:37:46Z`. The cause was not established. Treat the snapshot-time relationship as an evidence-quality concern until clocks/time zones and source timestamp semantics are reconciled.

## 4. Cash, options, denominator and FINAL ACTION compatibility

The existing production denominator in `FinalActionService.GrandTotalAsync` is open stock market value plus cash ledger amounts plus open option value. Stock quotes fall back to average cost when absent; options are valued as `MarketPrice × NumberOfContracts × 100`. `ClassifyValuation` uses provider stock currency with a ticker-suffix fallback, infers option currency from its underlying ticker, and treats cash as CAD unless account/description text has a USD marker. It fails closed for mixed-currency allocation because there is no FX conversion. Portfolio Fit and FINAL ACTION consume this existing reliability result.

Model limitations confirmed in the current `develop` base:

- `CashItem` has no currency field. Its ledger classification and trade-link fields do not establish the balance's currency.
- `OptionItem` has no quote currency, quote source timestamp, explicit long/short field, or verified contract multiplier. `PositionType` describes CALL/PUT, not position side.
- The existing option arithmetic's `×100` and positive contract-count treatment are implementation behavior, not an approved confirmation of the correct multiplier or sign policy for every option.
- Cash flow type is available to distinguish external deposits/withdrawals from investment profit. The prototype values a supplied balance only; it is not a performance or P&L calculation.
- The prototype does not carry transaction-link identifiers. A production adapter must reconcile cash rows and linked trade legs so the same economic value is not added twice.

The prototype does not feed or replace the existing FINAL ACTION result. If later approved, one authoritative common-currency valuation result must feed Portfolio, Dashboard, PM-029 and FINAL ACTION consistently. Until then, the current mixed-currency fail-closed behavior remains unchanged. Average cost remains cost-basis evidence and must not be used as current market exposure without an explicit pre-existing approved policy.

## 5. Production design proposal for approval

This is a proposal only; none of the following production integration is implemented.

1. **One valuation authority:** a shared service produces an auditable as-of valuation snapshot consumed by Portfolio, Dashboard, PM-029 and FINAL ACTION. Consumers must not independently convert, infer missing components, or calculate competing denominators.
2. **FX:** use Bank of Canada daily `FXUSDCAD`, in CAD per USD, with observation date, official publication time, retrieval time, source and rate recorded. Select no observation later than the component's market session and no publication later than the valuation cutoff.
3. **Calendar/freshness:** weekend and holiday reuse requires a verified Bank of Canada publication calendar and cutoff tests. The three-business-day age limit remains provisional until those tests and owner approval; missing, unknown or stale evidence must suppress common-currency weights.
4. **Market prices:** require a positive, timestamped quote for the expected exchange session. Missing or stale quotes make current exposure unavailable; never substitute average cost.
5. **Explicit contracts:** add/identify authoritative currency for cash and options, plus quote provider/time. Confirm option side, multiplier and mark semantics. Do not infer cash currency from descriptions or option currency from ticker suffix.
6. **Scope and reconciliation:** preserve user/shared-row visibility and account scope unless approved otherwise. Count each eligible stock lot, ledger balance and open option once; exclude closed positions. Establish how linked trade cash, deposits/withdrawals and legacy rows reconcile before computing totals.
7. **Allocation semantics:** keep the consolidated stocks + cash + options denominator distinct from the stock-only sector denominator. Position limits remain hard limits; sector allocations remain targets with the existing tolerance unless explicitly changed.
8. **Failure and rollback:** missing or inconsistent evidence returns unavailable/review, not an estimated percentage. Roll back to the existing fail-closed allocation status. Do not activate a partially converted denominator.

### Approval still required

- Confirm the Bank of Canada `FXUSDCAD` daily series and CAD-per-USD direction as the production authority (previously approved in principle).
- Approve a publication cutoff and holiday/weekend rule, and decide the maximum FX age after a real calendar test. Three business days is not yet final.
- Approve the authoritative cash-currency source and legacy-row handling.
- Approve option currency, quote provenance, side/sign, contract multiplier and whether marked market value (rather than underlying notional) is included in the denominator.
- Confirm shared-row and per-account/consolidated scope, plus transaction-linked cash reconciliation and external cash-flow treatment.
- Authorize a separate production integration plan and a controlled current-v2 replay/refresh, or provide an exact, same-as-of sanitized raw-input export.

## 6. Verification

| Check | Result |
|---|---|
| `Pm029CadValuationPrototypeTests` (VSTest/xUnit v2) | **19 passed, 0 failed, 0 skipped** |
| Current Swing-v2 `Pm029CalculatorTests` in preserved feature worktree | **72 passed, 0 failed, 0 skipped** |
| Production API build / full backend suite | Not run; the prototype is confined to the test assembly and no production code changed |
| Real-universe Swing-v2 replay | **Blocked**, for missing exact same-as-of raw inputs |
| Production database refresh/write | Not run; not authorized |
| UI / screenshot test | Not applicable; no UI changed |

The prototype test run required NuGet restore because the new worktree had no `obj/project.assets.json`. After restore, standard test discovery ran successfully. Existing API/platform analyzer and unrelated test warnings appeared during the initial build; no warnings remained from the new prototype tests after fixing their xUnit assertion pattern.

## 7. Screenshots

There are **no new Phase 12 screenshots** because no UI was changed and no new screenshot campaign was authorized. The existing 70-image Phase 10 package is synthetic UI evidence only, not Phase 12 output and not production data. It is available in the preserved worktree at:

`D:\PORTFOLIO-MANAGER\docs\screenshots\pm029-phase-10-integrity-uat.zip`

The current archive contains 70 PNG entries (71 ZIP entries total) and hashes to `E137271DCCF672F771A5F0D920442F8E995897059070CE28E8AA3103A38214CF`. The older Phase 10 report records `DC2BD3C2641F42CDF259FA9BFBC2179C5F0D8C4BD40129FD8E92C4E5B42284CA`. That discrepancy remains unexplained; the archive should not be treated as an immutable Phase 12 artifact.

## 8. Remaining gates and recommendation

| Priority | Gate | Owner / prerequisite |
|---|---|---|
| P0 | Approve exact cash/options currency, timestamp, sign and multiplier contracts | Product/financial-policy owner |
| P0 | Approve FX publication/freshness and calendar rules | Product/financial-policy owner; holiday/cutoff tests |
| P0 | Decide whether to authorize production integration and a controlled v2 refresh, or provide a complete sanitized export | Data owner; refresh has database write side effects |
| P1 | Establish current Swing-v2 score/status/reason counts across the actual universe | Requires exact raw inputs; do not infer from v1 |
| P1 | Reconcile anomalous database and host timestamps | Data/operations owner |
| P1 | Validate sector/ETF/CDR mapping provenance and fundamentals on same-as-of real evidence | Data owner; no mapping changes authorized |
| P1 | Complete final manual UAT | User, after authorized production behavior is available |

**Recommendation:** retain the test-only prototype for review, keep Portfolio Fit fail-closed, and keep PM-029 OPEN. Do not wire the prototype into production or refresh persisted scores until the approval questions above are answered.
