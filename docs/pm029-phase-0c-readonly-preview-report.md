# PM-029 Phase 0C — Read-Only Preview and FINAL ACTION Parity

**Disposition:** Phase 0C implementation and isolated verification are complete. PM-029 remains **OPEN**. Do not start Phase 1 scoring, change scoring policy, or activate new stop calculations without the required approval.

## 1. Baseline and preservation

- Branch: `feature/pm-029-final-review`
- `HEAD`: `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22`
- `develop` at verification: the same commit.
- The original 18 staged PM-029 paths remain staged and unchanged. Their staged binary-diff hash remains `708a965eab9e5167192fd554e21c6a4398e0ab86`; no index changes were made, and Phase 0C edits remain unstaged.
- The Phase 0 untracked test was preserved and extended for Phase 0C. Its recorded pre-change SHA-256 was `3AFD4992C2767E88ADDB37A88666CE99130C25E89B6DDAB86D29A1E247EDB369`.
- Current tracked unstaged diff hash: `bade080920cce141393891308e47b1279bfa2813` (does not include untracked files).
- No reset, clean, checkout, commit, push, pull, merge, rebase, migration, seed, live/protected refresh, or deployment was performed.

## 2. Implemented architecture

### Shared staged-signal transition calculation

`StagedSignalService` now calculates one deterministic transition from the existing staged state, scan result, one captured UTC evaluation time, and one Eastern-local evaluation date. Normal and preview execution both call that same calculation for first observations, current/previous RSI and price, extremes, expiry, trend/turn/status fields, channel-state updates, and `DynamicStopLoss`.

- **Normal path:** reads tracked rows, applies calculated state or adds a newly created row, then calls `SaveChangesAsync`.
- **Preview path:** reads with `AsNoTracking`, copies existing state before calculating, changes only the returned scan DTOs, and never attaches entities, adds rows, or calls `SaveChangesAsync`.
- An expired preview returns the same unenriched scan result as normal expiry would, without deactivating the stored signal.
- Preview enrichment errors are logged and rethrown. The FINAL ACTION evaluation then treats affected symbols as unknown instead of reporting successful but incomplete preview evidence. Existing normal scanner error handling remains unchanged.
- Each staged transition captures one UTC evaluation timestamp and derives its Eastern-local date once; the scan's source timestamp and market session date are not rewritten by preview.

### Scanner and security-resolution modes

The scanner now has explicit **Persist**, **ReadOnly**, and **Preview** modes. Preview combines the same technical scan and staged-signal enrichment as normal evaluation with non-persisting security resolution. Existing normal scanner entry points retain their persistence behavior; universe-only read-only scanning remains available without staged-signal enrichment.

Security mapping precedence remains user-specific over shared, and USER over AUTO at equal scope. When multiple mappings tie at the highest priority but resolve to different identities, both normal and read-only resolution now return explicit `NeedsUserInput` evidence instead of selecting one by row order. An unmapped wrapper detected from market data is returned as unresolved in preview without inserting an AUTO mapping. Resolution does not silently substitute a different listing, underlying, or currency.

### FINAL ACTION and Opportunity integration

`EvaluatePreviewAsync` is a new invocation mode on `FinalActionService`. It enters the same per-user evaluation gate and shared `ComputeCoreAsync` decision/evidence pipeline used by regular FINAL ACTION. It always computes a fresh bundle, does not read or populate the regular FINAL ACTION cache, and does not publish decisions. `OpportunityService` now calls this preview method. Existing `EvaluateAsync` behavior remains the default for other consumers.

No separate recommendation engine was added. Role-aware decisions, holdings and lot context, portfolio evidence, rationale, freshness checks, and missing-data behavior continue through the existing FINAL ACTION engine.

## 3. Write-boundary inventory

| Path | Write behavior after Phase 0C | Status |
|---|---|---|
| PM-029 `OpportunityService.RefreshAsync` | Writes the Opportunity snapshot and refresh-state bookkeeping; retains the prior successful snapshot ID when a refresh fails. | Authorized PM-029 persistence; unchanged. |
| PM-029 FINAL ACTION/scanner evaluation | Uses `EvaluatePreviewAsync` → scanner Preview → staged-signal preview and read-only resolver. No FINAL ACTION cache/publication, staged-signal write, AUTO mapping write, or financial/broker write. | Corrected and tested. |
| Normal scanner `AnalyzeSymbolsAsync` | Continues normal staged-signal lifecycle persistence and AUTO mapping persistence. | Preserved for authorized scanner workflows. |
| `RsiScannerService.RunLiveScanAsync` | Continues its normal staged-signal and technical-channel persistence behavior. | Separate normal scanner workflow; unchanged. |
| Universe-only Value Screener refresh | May save/prune Universe-origin Value Screener snapshots when universe fundamentals are missing or stale. | Conditional existing Opportunity side effect; unchanged and outside scanner-preview write scope. Opportunity refresh is therefore not globally database-write-free. |
| `PublishPortfolioActionsAsync` | Publishes FINAL ACTION fields through portfolio snapshot persistence when separately invoked. | Not called by PM-029 refresh. |
| Holdings, transactions, cash, options, saved risk plans, broker-risk records, account types, and other portfolio/accounting records | Read by evaluation/evidence paths; no writes identified in the PM-029 preview path. | Protected-state hashes remained unchanged in isolated tests. |

## 4. FINAL ACTION parity and evidence

Parity is checked against the existing authoritative calculation, not against a second implementation:

- Normal and preview FINAL ACTION snapshots are compared after the repository's existing deterministic normalization. The only omitted values are clock-dependent metadata: `computedAtUtc`, `lastSuccessfulEvaluationUtc`, `dataAsOfDate`, `expectedSession`, `entryDate`, `portfolioUpdatedAt`, `evidenceAsOfUtc`, `calculatedAt`, and `portfolioSnapshotAtUtc`.
- Golden fixtures exercise multiple holdings, accounts, and investment roles through the common FINAL ACTION engine.
- A provider-failure test compares normal and preview behavior and asserts that all resulting rows are non-actionable **Awaiting Data** rows.
- The real-scanner Opportunity fixture compares normalized preview and normal FINAL ACTION output and checks scan values including RSI, price, staged delta, trend shift, and dynamic-stop evidence.
- Relational staged-signal tests compare preview and normal enrichment for a new signal, same-day and subsequent-session updates, oversold and overbought extremes, expiry, and cancelled/inactive signals. They verify the dynamic stop and channel-state fields.
- Additional tests cover neutral scans, failed market-data calls, unresolved wrapper mappings, conflicting top-priority mappings, repeated evaluations, and concurrent preview calls.

No recommendation rule or expected golden output was changed to force parity. The one mapping behavior clarification is limited to conflicting highest-priority identities: it now fails closed as unresolved in both modes.

## 5. Protected-state verification

Tests use synthetic data and isolated EF InMemory or SQLite databases only; no live or protected database was refreshed. The hash helper enumerates every non-owned, non-shared EF entity type, serializes its rows in deterministic order, and captures a SHA-256 hash.

- InMemory end-to-end Opportunity fixture: preview itself changed no entity hashes; the refresh changed only `OpportunitySnapshot` and `OpportunityRefreshState`. Staged signals, mappings, portfolio/watchlist snapshots, holdings, cash, options, risk plans, broker-stop records, and account types remained unchanged.
- SQLite relational scanner fixture: preview left all entity hashes unchanged, left no tracked entities, and created no AUTO mapping. A normal scanner call changed only staged signals and the test wrapper's AUTO mapping.
- SQLite staged-transition fixture: preview left the relational staged-signal state unchanged while matching the normal enrichment output. Normal execution persisted the calculated transition as expected.
- Repeated/concurrent preview tests observed deterministic normalized decisions and no preview cache population or protected-state changes.
- Existing Opportunity failure coverage verifies the last successful snapshot ID is retained after a failed refresh.

SQLite exercises relational queries, tracking behavior, and actual `SaveChangesAsync` boundaries. It does **not** substitute for SQL Server transaction-isolation or provider-specific concurrency testing.

The existing per-user semaphore serializes normal and preview FINAL ACTION evaluations within one application process; concurrent same-user previews were exercised. Database reads use the configured provider's default statement consistency and `AsNoTracking`; there is no transaction spanning all mapping and staged-signal reads, and the semaphore does not coordinate another application process or an independent scanner workflow. Therefore a concurrently committed mapping or staged-signal update may be observed by a later query in the same evaluation. Stronger cross-query/cross-process snapshot consistency was not claimed or tested; SQL Server integration remains a follow-up limitation.

## 6. Snapshot and CCA.TO diagnostic status

The saved Opportunity snapshot, including the preferred snapshot ID 5, remains inaccessible: no SQL Server is configured/available through the existing local integration. No refresh was run to recreate it, no credentials or private financial values were read, and no snapshot-level claims are made.

The 224-row score-availability diagnostics and CCA.TO factor/benchmark/valuation matrix therefore remain unresolved. To complete them, provide either an approved local read-only SQL Server connection or a sanitized user-operated export containing snapshot metadata and the required opportunity evidence. Do not include account identifiers, quantities, private balances, or credentials.

## 7. Tests and checks

Targeted command:

```powershell
dotnet test backend\PortfolioManager.Tests\PortfolioManager.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Pm029|FullyQualifiedName~Pm024Stage1MarketDataTests|FullyQualifiedName~FinalActionIntegrityTests|FullyQualifiedName~FinalActionAwaitingDataTests|FullyQualifiedName~SecurityAnalysisResolverTests"
```

Result: **172 passed, 0 failed, 0 skipped**.

Full backend command:

```powershell
dotnet test backend\PortfolioManager.Tests\PortfolioManager.Tests.csproj -c Release --no-restore
```

Result: **1,240 passed, 0 failed, 4 skipped**. The four skips are SQL Server-dependent tests: three `AccountTypeSqlServerTests` and one `Pm033IsolatedSqlTests`; they require an explicitly configured isolated SQL Server environment.

`git diff --check` and `git diff --cached --check` passed. No Angular or Playwright files were changed, so frontend suites and screenshots were not applicable to this backend-only phase.

## 8. Files changed

Production:

- `backend/PortfolioManager.Api/Services/StagedSignalService.cs`
- `backend/PortfolioManager.Api/Services/RsiScannerService.cs`
- `backend/PortfolioManager.Api/Services/SecurityAnalysisResolver.cs`
- `backend/PortfolioManager.Api/Services/FinalAction/FinalActionService.cs`
- `backend/PortfolioManager.Api/Services/Opportunities/OpportunityService.cs`

Tests:

- `backend/PortfolioManager.Tests/Pm029FinalActionGoldenTests.cs`
- `backend/PortfolioManager.Tests/Pm029FinalActionRefactorTests.cs`
- `backend/PortfolioManager.Tests/SecurityAnalysisResolverTests.cs`
- `backend/PortfolioManager.Tests/Pm029PhaseZeroRefreshSafetyTests.cs` (preserved Phase 0 test, extended for Phase 0C)
- `backend/PortfolioManager.Tests/Pm029StagedSignalPreviewTests.cs` (new relational transition tests)

No frontend, PM-035, migration, seed, scoring-formula, stop-methodology, accounting, FX, or Allocation changes were made.

## 9. Remaining risks and recommendation

- A SQL Server integration run remains outstanding; SQLite and EF InMemory do not prove SQL Server-specific isolation or concurrency behavior.
- Conditional Universe Value Screener snapshot persistence remains reachable from Opportunity refresh as a separate existing side effect.
- Saved snapshot ID 5 and CCA.TO evidence remain unavailable until an approved read-only data source is provided.
- No UI screenshots were generated because Phase 0C made no frontend changes.

**Recommendation:** The PM-029 scanner refresh boundary is safe for the next separately approved scoring phase to evaluate, subject to the above SQL Server and snapshot-evidence limitations. This is not authorization to begin Phase 1 or activate any scoring/stop-policy changes. Stop here; PM-029 remains **OPEN** pending review and explicit approval.
