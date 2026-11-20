# Phase 2 — UAT readiness report (completion audit, gap closure, runtime verification)

Branch `fix/final-action-cls-brk`. **Nothing was committed or pushed.** No live portfolio data, cash, options, EOD snapshot or recommendation history was modified. Builds on `final-action-phase2-reliability-report-2026-10-27.md`.

## Final readiness classification: **READY WITH DOCUMENTED LIMITATIONS**
No coding defect is known to remain. The limitation is verification depth: the behaviour was verified with unit/integration tests, an isolated LocalDB run and deterministic mocked Playwright data — **not** by exercising the live API/scanner against your portfolio (that needs your restart and your own UAT, see §6). Limitations are listed in §7.

## 1. Implementation completion matrix (Revision 3, checked against code — not against the earlier report)

| Requirement | Status | Evidence (code) | Tests | Remaining gap |
|---|---|---|---|---|
| Legacy Action Center fallback removed | **Verified** | `PortfolioActionsService.ResolveDecision` is the only label source; `DeriveAction/ApplyEodContext/DeriveChannelAction` deleted. Remaining label code: `DashboardSignalActionInterpreter` only for scanner symbols that are neither owned nor watched → descriptive WATCH/AVOID/WAIT/CAUTION words, never `isActionRequired`, never Entry/Trim/Exit | `PortfolioActionsServiceTests` (6), `LegacyFallbackGuardTests` (new, reflection + public-behaviour) | none |
| Evidence health ≠ lifecycle ≠ effective state | **Verified** | `FinalActionStatusResolver`, `EffectiveStateProvider` (new) | `FinalActionStatusResolverTests` (29+) | none |
| Awaiting Data (non-actionable, last valid kept separately) | **Verified** | `FinalActionService.ComputeAsync/AwaitingDto`, catalog row | `FinalActionAwaitingDataTests`, Playwright `awaiting-data.spec.ts` | none |
| Per-symbol publish identity, original timestamps | **Verified (+ defect fixed)** | `PublishPortfolioActionsAsync`, `PatchFinalActionsAsync`; **fixed:** `PortfolioSnapshotService.SaveAsync` rebuilt the snapshot on every market-data refresh and wiped every published action/timestamp (seen in the live baseline: 0 of 128 items had a Final Action) → `CarryForwardFinalActions` | `MarketDataRefresh_RebuildingTheSnapshot…`, `CarryForward_…` | live snapshot still shows no published actions until the next evaluation publishes |
| PartialSuccess run status | **Verified** | `EodAutomationOrchestratorService.CaptureSignalsAsync/DeriveOverallStatus` | existing + resolver tests | no dedicated orchestrator integration test (orchestrator not unit-hostable) |
| Watchlist canonical + quote freshness | **Verified (gap closed)** | watchlist quote timestamps now read from the stored Watchlist snapshot (no new calls); `QuoteVerification` Verified/Unavailable; missing timestamp ⇒ Fresh by bar date, explicitly "not independently verified"; never Awaiting on that alone | 3 new `FinalActionAwaitingDataTests` | quote timestamp = last-trade time; very illiquid symbols could look stale (documented) |
| Freshness vs latest completed session, calendar | **Verified** | `TradingCalendar` | 32 tests incl. exact-second grace boundary, DST (Nov 1 2026, Mar 14 2027), early close, Good Friday, 2028 New-Year observance, year boundary, next-morning evidence | grace (2 h) and heartbeat (15 min) remain documented constants (not config) |
| Exchange resolution | **Improved** | `TradingCalendar.Resolve`: `AnalysisMarket`/Yahoo exchange metadata → ticker suffix → explicit NYSE assumption; result exposes `ExchangeBasis` (Metadata/Suffix/Assumed) shown in the details dialog | 6 theory cases | `OpenDecisionsService`/`SignalsAuditService` still resolve by suffix only (no scan in hand); `OpenDecisionsService` validation banner uses TSX calendar |
| Deterministic Evaluating | **Verified** | `FinalActionStatusResolver.RunState` + `EvaluationStatusReader` | boundary tests (15:00 running / 15:01 interrupted), recovery test | — |
| Strategic/Core risk persistence | **Verified** | `SignalsAuditService.OriginatingRiskPersists`, `HistoricalWarning` state | 3 tests | only structural-breakdown originated risks are held open; allocation-originated ones close when the condition is no longer produced |
| Mixed-role / multi-account allocation | **Verified** | `FinalActionService.BuildEvidence`, `PortfolioEvidenceGate` | `FinalActionAllocationTests` (12) | profit-taking tiers stay lot-level by decision |
| Explainability | **Verified** | `FinalActionExplainer`, details dialog | allocation tests + Playwright dialog assertions | — |
| **Signal Tracker effective state / export parity (gap closed)** | **Verified** | `SignalAuditRowDto` + `EffectiveStateProvider` (same read-only provider as the Action Center: no scan, no publish, no timestamp change); UI tags "Lifecycle: Active" + "Current: Awaiting Data · not actionable"; CSV/Excel columns `Lifecycle Status, Effective State, Actionable, Evidence Health, Last Evaluated (UTC), Data As Of, Reason Codes` | 3 backend tests (incl. no `ComputeAsync` call, timestamps unchanged), 2 export specs, 3 Playwright tests | — |
| **UI states never blank / never legacy BUY-SELL (new requirement)** | **Verified (mocked)** | Portfolio + Watchlist Final Action cells: `Loading` (shimmer), `Not evaluated`, `Awaiting Data` badge, `API error`; none is a decision; badge opens an accessible details dialog | `awaiting-data.spec.ts` (12 scenarios × 4 device projects) | — |
| Dashboard / Action Center counts | **Verified** | `OpenDecisionsService` (+ shared provider), Dashboard `ActionRequiredCount` needs `IsActionable` | 6 + Playwright dashboard scenario incl. "no `fresh=true` request on navigation" | — |

### Complete canonical path (traced in code)
Market data → `RsiScannerService` → `FinalActionService.ComputeAsync` (evidence assessment, engine, `PortfolioEvidenceGate`, explainer) → `PublishPortfolioActionsAsync` (fresh symbols only) → snapshot item / audit event → `FinalActionStatusResolver` via `EffectiveStateProvider` / `PortfolioActionsService.ResolveDecision` → Portfolio/Watchlist (`FinalActionStore`), Dashboard rows, Action Center (`OpenDecisionsService`), Signal Tracker (`QueryAsync`), exports. No other code produces an Add/Trim/Exit/Buy/Sell label for an owned or watched symbol.

## 2. Changes made in this pass
* **Defect fixed:** `PortfolioSnapshotService.SaveAsync` → `CarryForwardFinalActions`.
* **Signal Tracker:** `EffectiveStateProvider` (new, shared with Action Center), `SignalAuditRowDto` (+`EffectiveState, IsActionable, EvidenceHealth, DataAsOfDate, ReasonCodes`), `SignalsAuditService.ToRowsAsync`, `OpenDecisionsService` refactor, tracker page tags + export (`signal-tracker-export.ts`, `signal-tracker-page.component.*`).
* **Watchlist freshness:** `FinalActionService.ComputeAsync` (+`QuoteVerification`, `ExchangeBasis` on `FinalActionResultDto`), `TradingCalendar.Resolve/ExchangeBasis`.
* **UI states:** `FinalActionStore.uiState`, Portfolio and Watchlist Final Action cells, details dialog (quote verification, assumed exchange).
* **Tests added/changed:** `LegacyFallbackGuardTests`, `TradingCalendarTests` (+boundaries, exchange), `FinalActionStatusResolverTests` (heartbeat boundary), `FinalActionAwaitingDataTests` (+watchlist quote, carry-forward), `SignalsAuditServiceTests` (+3 tracker), `signal-tracker-export.spec.ts`, `e2e/awaiting-data.spec.ts` (new).
* **Deleted-test audit:** `AtdRegression_HardStructuralNegativeProducesAvoidDespiteBullTurn` and `AtdRegression_EodContextCannotDowngradeHardNegativeAvoid` asserted the removed `DeriveAction`/`ApplyEodContext`. Replaced by public-behaviour tests: the canonical watchlist engine never returns an Entry for a hard structural negative (`LegacyFallbackGuardTests`; note the canonical wording is WAIT FOR RECLAIM, not the legacy AVOID), the legacy methods no longer exist (reflection guard), and `PortfolioActionsServiceTests` proves no legacy label reaches a row.

## 3. Runtime verification (what was actually observed)
* **Processes (read-only inspection):** `PortfolioManager.Api` pid 20836 on :5000, started 14:30:36 from `bin\Debug\net8.0` via `dotnet run --launch-profile http` (pid 18852); `ng serve` pid 22116 on :4200; SQL Server `sqlservr` pid 5292 on :1433. Nothing was stopped, restarted or replaced.
* **Build in the running API:** contains the earlier Phase 2 types (`FinalActionStatusResolver`, `TradingCalendar`, …) but **not** this pass's fixes (carry-forward, tracker effective state, quote verification) — restart needed.
* **Database:** `PortfolioManagerLocal` on `localhost` (appsettings default). Only `SELECT` statements were run. No second API instance, no EOD, no publish, no `GET /api/final-actions` (it publishes), no DB copy.
* **Background automation:** the live API runs its normal schedulers; I neither started nor suppressed them.
* **Real symbols inspected (read-only SQL, baseline saved in the session folder):**
  * BRK.TO — 1 lot, Strategic, Corp_TD, +8% (price 39.30 vs cost 36.34); old event #33 `TRIM/REVIEW` Invalidated.
  * CLS.TO — 1 lot, Swing, TFSA_D_TD, opened 2026-10-27, `Earnings Catalyst`.
  * CABA — OPEN lot Strategic (TFSA), CLOSE lot Speculative (Corp); event #34 SMALL ADD Invalidated.
  * EFR.TO — OPEN lot Swing (opened 2026-10-15), earlier CLOSE lot; events #35 Invalidated, #45 `CUT LOSS/EXIT` Dismissed (history intact).
  * HUN.TO — 1 lot Speculative; event #43 `CUT LOSS/THESIS REVIEW` **Active** (validated 2026-10-26 21:43Z), #37 Superseded.
  * MDA.TO — **3 lots / mixed roles** (NULL→Strategic, Core, Strategic; account Corp_TD).
  * Configured limits: Core 5, Strategic 5, Strategic-Income 5, Swing 3, Speculative 2, Options 1 (%); sector targets e.g. Energy 25, Industrials 15, Technology 15, Healthcare 4.
  * Latest runs 123–127 (2026-10-26) all `Success`; no run `Running`.
* **Live finding:** the active user's `PortfolioSnapshots` row (updated 18:31Z, right after the API restart) has **0 of 128 items with a Final Action** — the defect above. Until the next successful evaluation publishes, rows with no cached evaluation will correctly show Awaiting Data (not a legacy label).
* **Not done on live data:** I did not compute today's Final Action/IsActionable/Evidence Health/Effective Status for these six symbols from the live scanner — that requires the live API (your restart) or an isolated copy with a stubbed scanner, which was not authorised. Their expected behaviour is covered by fixtures (CLS/BRK Phase 1 tests; mixed-role MDA-style lots in `FinalActionAllocationTests`).
* **Failure scenarios (mocks/isolated only):** missing scan, failed refresh, partial EOD, stale and old quotes, no published action, expired recommendation and recovery are covered by in-memory-DB tests and mocked Playwright payloads; historical records asserted unchanged in each.

## 4. Test results
| Suite | Passed | Failed | Skipped | Not executed |
|---|---|---|---|---|
| Backend xUnit (`PortfolioManager.Tests`) | **792** | 0 | 3 | — |
| …of which SQL Server integration (`AccountTypeSqlServerTests`, run separately with `PM_ACCOUNT_TYPES_SQL_TESTS=1`) | **3** | 0 | 0 | — |
| Phase 1 subset (gate, CLS.TO/BRK.TO service test, 10k-case parity corpus) | 262 | 0 | 0 | — |
| Angular `tsc` (app + spec) | clean | | | |
| Angular build (development) | success | | | production build not run |
| Angular unit tests (vitest) | **384** (48 files) | 0 | 0 | — |
| Playwright (desktop-chrome, tablet, mobile-s21, mobile-s23) | **615** (incl. 48 new Awaiting Data) | 0 (0 flaky) | 89 | — |
* **Skipped backend tests:** the 3 SQL Server tests are skipped by default (opt-in). They were executed with `PM_ACCOUNT_TYPES_SQL_TESTS=1`; the test creates and deletes its own uniquely named database on `(localdb)\MSSQLLocalDB` (never `PortfolioManagerLocal`); afterwards 0 `PmAccountTypesTest*` databases remained. (The tests resolve a repo-relative script path, so the output folder had to sit at the normal depth; the temporary output folder was removed.)
* **89 skipped Playwright tests** — all are viewport conditions: 50 "cards are handset-only" (desktop/tablet), 16 "only handset grids with secondary columns", 14 phones-only/not-phones dashboard collapse, 4 "secondary columns behind All fields on phones", 5 dashboard layouts (handset/tablet-only). None is Phase 2 related or lowers Phase 2 confidence; no test was removed or rewritten to pass.
* The Playwright run used a temporary config on port 4321 (your dev server holds 4200 over HTTPS); it was deleted afterwards.

## 5. Financial data protection
Verification only issued `SELECT` against `PortfolioManagerLocal`; all write paths were exercised on in-memory databases or the disposable LocalDB database. No transaction, holding, cash (45 rows), option, EOD snapshot, recommendation event, or published action in the live database was created, changed or deleted by this work. Source changes are uncommitted in the working tree.

## 6. UAT instructions (your portfolio)
1. **Restart the API safely:** stop only the running `dotnet run --launch-profile http` (pid 18852 → child 20836) in its terminal (Ctrl+C), then `cd backend\PortfolioManager.Api; dotnet run --launch-profile http`. Do it outside the EOD window and not during a run (check Automation page: no run "Running"). Keep `ng serve` as is (hot reload) or restart `npx ng serve`.
2. Open the app and log in. Portfolio and Watchlist trigger one `GET /api/final-actions` (that evaluation also publishes fresh symbols — expected, normal behaviour).
3. **Awaiting Data:** any symbol with missing/stale/failed evidence shows an **Awaiting Data** badge (not blank). Click the badge: reason codes, last valid action, data-as-of, "Source quote time … not independently verified" when no quote timestamp exists. A temporary row with no result shows **Not evaluated**; a failed request shows **API error**; first load shows a shimmer.
4. **CLS.TO / BRK.TO:** CLS.TO → *Hold / Catalyst Monitoring* on its entry day (then *Hold / Monitor* unless a post-entry loss/breakdown); BRK.TO → *Hold / Monitor* (allocation below the Strategic 5% limit). Details dialog shows the technical status (e.g. Active Sell Trigger) separately with the "not a portfolio decision" note.
5. **MDA.TO** (3 lots, mixed roles): each lot's row uses its own role limit against the symbol's total exposure; an Add on a role without a limit shows **Add / Needs Review**.
6. **Dashboard / Action Center:** counters = actionable only; the "Not actionable" section lists Awaiting Data and historical warnings (e.g. HUN.TO's Active Risk Review if its evidence is stale) with "Last valid action …". Opening the Dashboard must not trigger a scan.
7. **Signal Tracker:** Final Action rows show **Lifecycle: Active** and **Current: …** tags; export CSV/Excel and check the seven new columns (Lifecycle Status, Effective State, Actionable, Evidence Health, Last Evaluated, Data As Of, Reason Codes). Original signal date/price unchanged.
8. **Freshness:** before the 18:00 ET evidence cut-off, Monday's session is still "latest completed" on Tuesday morning; Portfolio export shows `Last Evaluated (UTC)` and `Data As Of`.
9. After a manual *Refresh all*, confirm Final Action values on the Portfolio are still present (the carry-forward fix) — previously every refresh erased them.

## 7. Remaining limitations / risks
* No live-data end-to-end run (see §3). If you want it, I need approval for an isolated database copy (source `PortfolioManagerLocal` → destination e.g. `PortfolioManagerPhase2Verify`, `BACKUP … COPY_ONLY` + restore, API started with automation disabled via configuration and a stubbed scanner) — not created.
* Evidence grace (2 h) and heartbeat window (15 min) are code constants, documented and tested at the boundary, not configurable.
* Exchange: `OpenDecisionsService`/`SignalsAuditService` infer the exchange from the ticker suffix; `FinalActionService` prefers scan market metadata. A TSX-suffixed symbol analysed on a US underlying could differ between the two (rare).
* Quote timestamp = last trade time; very illiquid symbols could be flagged Stale although the bar is current.
* `AccountType`-level exposure limits are not part of the policy (symbol/role/sector only), as approved.
* Production Angular build and a live-browser (non-mocked) pass were not run.

## 8. Final readiness
**READY WITH DOCUMENTED LIMITATIONS** — ready for your user acceptance testing after the API restart in §6; the remaining limitation is real-data integration verification, not a known coding defect.
