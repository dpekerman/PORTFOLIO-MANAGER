# PM-029 Phase 0B — Read-Only Refresh Safety

**Disposition:** The unintended PM-029 refresh writes are confirmed, but a parity-preserving correction cannot be implemented within the current approval boundary. No production code was changed. Stop here for approval; do not begin Phase 1. PM-029 remains **OPEN** and PM-035 remains separate.

## 1. Baseline and preservation

- Branch: `feature/pm-029-final-review`
- Starting/current `HEAD`: `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22` (same commit as `develop` at baseline).
- Rechecked all 18 previously staged PM-029 files against the Phase 0 index blob IDs: **18/18 match**.
- No staged content was changed; no unstaged tracked source changes were made.
- The Phase 0 isolated test remains untracked: `backend/PortfolioManager.Tests/Pm029PhaseZeroRefreshSafetyTests.cs`.
- This report is the only new file in Phase 0B.
- No reset, clean, checkout, commit, push, pull, merge, migration, seed, live refresh, or deployment was performed.

## 2. Write-boundary inventory

| Path | Write-capable operation | Reachable from PM-029 refresh? | Assessment |
|---|---|---:|---|
| `OpportunityService.RefreshAsync` | Sets `OpportunityRefreshState` to Running, inserts an `OpportunitySnapshot`, prunes older snapshots, and updates refresh state. A failed attempt records failure state without replacing `LastSuccessfulSnapshotId`. | Yes | Expected PM-029 snapshot/refresh bookkeeping. |
| `OpportunityService.BuildAsync` → `FinalActionService.EvaluateAsync(fresh: true)` → `ScanSymbolsAsync(readOnly: false)` | The watchlist/holding scan dispatches to `AnalyzeSymbolsAsync`, not the read-only scanner method. | Yes | Confirmed path into scanner lifecycle writes. |
| `RsiScannerService.AnalyzeCoreAsync` → `StagedSignalService.UpsertAndEnrichAsync` | For non-neutral scan results, creates staged signals or updates current/previous RSI and price, extremes, tracking/expiry state, trend/turn fields, and `DynamicStopLoss`, then calls `SaveChangesAsync`. The scanner catches persistence exceptions, but successful calls still mutate the database. | Yes | **Unintended PM-029 refresh side effect.** |
| `RsiScannerService.ResolveSymbolsAsync` → `SecurityAnalysisResolver.ResolveAsync` | Persistent resolution can insert an AUTO `NeedsUserInput` wrapper mapping and save it. | Yes | **Unintended PM-029 refresh side effect.** |
| `OpportunityService.RefreshUniverseFundamentalsAsync` → `ValueScreenerPersistenceService.SaveAsync` | When Universe-only fundamentals are missing or stale, inserts a Universe Value Screener snapshot and prunes old snapshots. The persistence service does not update schedule configuration for the Universe origin. | Conditionally | Separate, non-financial snapshot persistence used by PM-029. It is not a holdings, transaction, cash, option, risk-plan, or broker-record write. |
| `RsiScannerService.RunLiveScanAsync` | Calls staged-signal enrichment and `TechnicalChannelPersistenceService.UpsertAsync`. | No, not through the `AnalyzeSymbolsAsync` path used by FINAL ACTION evaluation | Normal scanner behavior; do not globally disable it. |
| `FinalActionService.PublishPortfolioActionsAsync` | Publishes FINAL ACTION fields through `PortfolioSnapshotService.PatchFinalActionsAsync`. | No; it is a separate operation and is not called by Opportunity refresh. | No FINAL ACTION record publication was observed on this path. |
| Saved risk plans and portfolio/financial records | Read by the evaluation/evidence path. | No write identified | Phase 0 fixture hashes remained unchanged for holdings, snapshots, watchlist, cash, options, risk plans, broker-stop records, and account types. |

The current `AnalyzeSymbolsReadOnlyAsync` path uses read-only security resolution and does not call staged-signal upsert/enrichment. It is therefore not a drop-in replacement: Phase 0's real-scanner fixture showed RSI/current-price parity, but `DynamicStopLoss` was zero on the read-only result and positive after persisted staged-signal enrichment.

## 3. Approval boundary and proposed correction

The existing FINAL ACTION computation for watchlist and held symbols consumes scanner output enriched from staged-signal state. Switching that invocation to the existing read-only scan would change the evidence supplied to the authoritative decision engine. Reimplementing FINAL ACTION decisions inside Opportunity would violate the prohibition on a parallel recommendation engine.

A parity-preserving approach appears to require a shared scanner/FINAL ACTION capability that:

1. Resolves securities without persisting AUTO mappings.
2. Reads staged signals without tracking and computes the same would-be lifecycle enrichment (including first observation, same-day and next-day updates, expiry, turn fields, channel-state updates, and dynamic stop) as a pure preview.
3. Passes that preview through the existing FINAL ACTION decision/evidence logic without changing its rules.
4. Is invoked by PM-029 without saving staged signals, mappings, or financial/broker records.
5. Leaves normal scanner/scheduler persistence behavior unchanged.

This requires a shared contract or behavior change to the scanner and/or FINAL ACTION path. The Phase 0B authorization explicitly requires stopping for approval if such a shared change is needed. **No production correction or parity claim is made.** Approval is needed for a focused design/implementation change to add the preview and route PM-029 through the same authoritative decision logic.

### Required parity and protected-state proof after approval

- Run existing and proposed paths against cloned isolated fixtures with identical market inputs, user roles, holdings, and staged-signal state.
- Compare normalized action labels, actionability, reasons/rationale, role-aware decisions, signal evidence, scan/setup fields, portfolio evidence, and Opportunity payloads; exclude only documented nondeterministic timestamps.
- Cover existing/new/missing/expired staged signals, wrapper mappings, canceled signals, multi-account holdings, missing data, and provider failures.
- Capture before/after hashes for staged-signal lifecycle, mappings, holdings, cash, options, transactions, saved risk plans, broker-risk data, and FINAL ACTION records; assert equality across repeated and failed refreshes.
- Assert no duplicate signals or mappings and verify last-successful Opportunity snapshots are preserved on failure.
- Do not run these tests against a live or protected database.

## 4. Saved snapshot availability

The MSSQL integration has **zero configured servers**. Application configuration obtains `ConnectionStrings:DefaultConnection` through the standard configuration providers; no usable local SQL Server connection was available, and no secret values were read or printed. Snapshot ID 5 was not queried, no refresh was run to recreate it, and no snapshot-level claims are made here.

The previously recorded 224-security snapshot and counts in the Phase 0 report remain historical evidence only. A sanitized export or approved read-only SQL Server connection is required to inspect Swing/Strategic availability, S6/T7 reasons, Heat mappings, fundamental freshness, Portfolio Fit evidence, CCA.TO components, and expected versus actual sessions.

## 5. CCA.TO status

No CCA.TO row-level payload or component evidence was accessible. Account identifiers, quantities, and private financial amounts were not retrieved. The Phase 0 CCA.TO matrix therefore remains unresolved; no root cause, valuation evidence, benchmark mapping, or session provenance is inferred.

## 6. Protected-state evidence

The Phase 0 test `Pm029PhaseZeroRefreshSafetyTests.OpportunityRefresh_UsesRealScannerAndChangesOnlyKnownLifecycleTables` uses the real scanner, resolver, staged-signal service, and Opportunity service with an EF InMemory database and synthetic market data. It established that:

- Direct read-only scanner evaluation left all captured entity hashes unchanged.
- A controlled Opportunity refresh changed Opportunity snapshot/state plus staged signals and security-analysis mappings.
- Captured portfolio, watchlist, cash, options, risk-plan, broker-stop, and account-type hashes remained unchanged.

This is isolated-fixture evidence, not proof for every provider or production database. Phase 0B made no production-path change and did not execute a refresh against a live/protected database.

## 7. Validation

Command:

```powershell
dotnet test backend\PortfolioManager.Tests\PortfolioManager.Tests.csproj --no-build --filter "FullyQualifiedName~Pm029|FullyQualifiedName~Pm024Stage1MarketDataTests|FullyQualifiedName~FinalActionIntegrityTests|FullyQualifiedName~FinalActionAwaitingDataTests|FullyQualifiedName~SecurityAnalysisResolverTests"
```

Result: **165 passed, 0 failed, 0 skipped**. This covered PM-029, selected FINAL ACTION/scanner, and resolver regressions. `git diff --check` and `git diff --cached --check` passed; the Phase 0 test file also had no trailing whitespace. Angular and Playwright suites were not run because no frontend code changed. The full backend suite was not run.

## 8. Files and stop decision

- Added: `docs/pm029-phase-0b-refresh-safety-report.md`.
- Preserved from Phase 0: `backend/PortfolioManager.Tests/Pm029PhaseZeroRefreshSafetyTests.cs` (untracked).
- Preserved: all 18 staged PM-029 files and their original index blob IDs.
- Production source changes in Phase 0B: **none**.

**Decision:** Phase 0B confirms the write paths and provides regression evidence, but the authorized boundary does not permit the shared contract/behavior change needed to preserve FINAL ACTION evidence parity. Request approval for the focused read-only preview design before changing shared code. Do not start Phase 1 or activate new scoring. PM-029 remains **OPEN**.
