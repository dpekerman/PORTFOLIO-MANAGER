# Final Action reliability — Phase 2 (Awaiting Data, freshness, explainability)

Branch `fix/final-action-cls-brk`. Phase 2 changes are **uncommitted** (nothing committed or pushed by this work). No database migration, no data change, no cash/valuation change.

## 1. What changed and why

| Requirement | Implementation |
|---|---|
| **No legacy fallback** | `PortfolioActionsService.DeriveAction`, `ApplyEodContext`, `DeriveChannelAction` and the ET-calendar-day check are **deleted**. Row label/severity/priority/state come only from `ResolveDecision`: fresh canonical result → retained prior recommendation (published holding action or active Signal Tracker event) while the resolver says it is valid → otherwise **Awaiting Data**. Reads are cache-only (`IFinalActionService.TryGetCached`): opening the Dashboard never scans or renews anything. Watchlist rows are canonical too. |
| **Awaiting Data** | New catalog row `Awaiting Data` (Advisory, `ma-standby`, hold/INFORMATIONAL, never actionable). `FinalActionService.ComputeAsync` emits it (instead of silently skipping) when the scan failed/absent, price invalid, a required input is missing, or the bar/quote is stale. The last published action stays separately in `LastValidAction` + `LastSuccessfulEvaluationUtc`. Awaiting symbols are added to `Meta.UnknownSymbols`, so lifecycle never confirms, renews, invalidates or expires on them. |
| **Evidence health ≠ lifecycle ≠ display state** | `FinalActionStatusResolver` (pure). Evidence health: Fresh/Stale/Missing/Invalid/Failed. Lifecycle: existing statuses and windows (Entry/Add 5, Trim/Exit 3 trading days; Strategic/Core exits no calendar expiry). Effective state: CurrentValid / PriorSessionValid / AwaitingData / Evaluating / Expired / HistoricalWarning. Actionable only with fresh evidence and a valid evaluation, or a prior-session recommendation inside its window with no newer failure. |
| **Freshness = latest completed exchange session** | `TradingCalendar`: TSX/NYSE holidays (rule-based, observed-date rules), early closes, Toronto clock with DST, `LastCompletedSession` (session counts as completed 2 h after close). Morning before the EOD scan stays **Fresh**. An old source quote wrapped in a new scan is Stale (`QUOTE_OLDER_THAN_SCAN`); required evidence depends on the rule (Entry needs RSI/trend averages; optional indicators never invalidate). |
| **Per-symbol EOD publishing** | `PublishPortfolioActionsAsync` publishes only freshly evaluated symbols; each gets its own `FinalActionUpdatedAt` (= evaluation time, not write time, never moved backwards) and `FinalActionSession`. Awaiting symbols keep their previous record and timestamp **byte-for-byte**. A wholly failed pass publishes nothing. The EOD run is `PartialSuccess` with the affected symbols in the run log (`ErrorStep = FinalActionPartial`). |
| **Timestamps** | Creation (`CapturedAtUtc`/`SignalDate`/`SignalPrice`) is never changed by revalidation; cached reads and UI refreshes never touch `LastValidatedAtUtc`/`FinalActionUpdatedAt`. |
| **Deterministic Evaluating** | `AutomationRunLog` `Running` + heartbeat within 15 min = Evaluating; stale heartbeat = Interrupted → Awaiting Data (`EVALUATION_INTERRUPTED`). Cannot stay stuck. |
| **Counts** | `OpenDecisionsService` counts only actionable states. New `NotActionable` list (Awaiting Data, Evaluating, HistoricalWarning) is always visible, never counted; summary adds `AwaitingData`/`HistoricalWarnings`. Dashboard `ActionRequiredCount` requires `IsActionable`. EOD summary rows show `AwaitingData`. |
| **Strategic/Core risk** | Historical risk events are kept permanently. A successful evaluation does not close a long-horizon exit/Risk Review raised by a confirmed structural breakdown while that breakdown persists (`OriginatingRiskPersists`); stale evidence → `HistoricalWarning` (listed, uncounted). Events without recorded reason codes keep the old behaviour. |
| **Mixed-role / multi-account allocation** | Symbol-level exposure (all lots, all accounts) ÷ grand total; the limit is the **target lot's role** limit; no unrelated-role limit is applied. Unrecognised role with configured limits on other lots ⇒ non-actionable **Add / Needs Review** (`ADD_ROLE_POLICY_AMBIGUOUS`). Sector resolved once per symbol (blank-sector lots count toward their symbol's sector); unknown sector ⇒ no sector limit. **Profit-taking tiers remain lot-level (unchanged).** |
| **Explainability** | `FinalActionExplainer`: reason codes + one-line rationale for actionable Add (signal, exposure vs limit, sector), profit-taking, "Hold / No Add" (constraint), Hold/Risk Review & Technical Caution (warning, not instruction), Awaiting Data (missing/stale inputs). Plain Holds stay empty. Gate outcomes keep their own text. |
| **UI** | Accessible `FinalActionDetailsDialog` (opened from the Final Action badge in Portfolio and Watchlist): portfolio decision, technical readings kept separate with a conflict note, reason codes in plain language, last valid action, evidence freshness. Action Center widget: "Prior session · N trading days old", "Not actionable" section for Awaiting Data / historical warnings. |
| **Exports** | Portfolio and Watchlist exports: `Technical Trend Setup`, `Technical Momentum Shift`, `Final Action (Portfolio Decision)`, `Actionable`, `Decision Reason Codes`, `Decision Status`, `Last Evaluated (UTC)`, `Data As Of` — all from the same canonical result. |

## 2. Files and services changed
Backend: `Services/TradingCalendar.cs` (new), `FinalAction/FinalActionStatusResolver.cs` (new), `EvaluationStatusReader.cs` (new), `FinalActionExplainer.cs` (new), `PortfolioEvidenceGate.cs` (role-policy ambiguity), `FinalActionService.cs` (Awaiting Data, per-symbol evidence, publish identity, allocation scope, `TryGetCached`), `FinalActionCatalog.cs`, `PriorityCandidateEligibility.cs`, `PortfolioActionsService.cs` (legacy removed), `OpenDecisionsService.cs`, `SignalsAuditService.cs` (trading-day windows, risk persistence, richer audit context), `PortfolioSnapshotService.cs`, `EodAutomationOrchestratorService.cs` (PartialSuccess), `DashboardService.cs`, `DashboardEodSummaryService.cs`, `Models/Dtos.cs`, `Models/DashboardModels.cs`.
Frontend: `portfolio.models.ts`, `final-action.store.ts`, `open-decisions.store.ts`, `shared/final-action-details-dialog/*` (new), Portfolio and Watchlist pages (badge → details dialog, exports), Action Center widget.
All contract changes are **additive optional fields**.

## 3. Test results
* Backend: **767 passed, 0 failed, 3 skipped** (pre-existing SQL-Server-only skips). New: `TradingCalendarTests` (16), `FinalActionStatusResolverTests` (29, incl. the "absence of evidence is never actionable" invariant over roles × priors × runs × bad evidence), `FinalActionAwaitingDataTests` (8), `PortfolioActionsServiceTests` (6), `FinalActionAllocationTests` (12), plus new cases in `OpenDecisionsServiceTests` and `SignalsAuditServiceTests`. Phase-1 suites (gate, CLS.TO/BRK.TO service test, 10k-case parity corpus, Signal Tracker) pass unchanged (262 in that subset).
* Frontend: `tsc` clean, Angular build clean, **382 unit tests passed** (48 files).
* Playwright (desktop-chrome, tablet, mobile-s21, mobile-s23; run on a temporary port because your dev server holds 4200 — temp config removed): **567 passed, 0 failed, 89 skipped** (pre-existing skips).
* Two legacy reflection tests that asserted the removed `DeriveAction` / `ApplyEodContext` rules were deleted on purpose; their intent (hard structural negative ⇒ AVOID) remains covered by the canonical engine and its parity corpus. One existing test (`Validation_FailedOrIncompleteLatestPass…`) was updated: a failed pass now makes a Core exit a *historical warning* (visible, uncounted) instead of a counted decision.

## 4. Data and history preserved
No migration, no schema change; all tests use in-memory databases and no repository data or database was modified. Tests assert that Awaiting Data / failed evaluations never delete, close or renew a recommendation, never move `LastValidatedAtUtc`/`FinalActionUpdatedAt`, keep original creation time and initial evidence, and create no duplicates on repeated EOD.

## 5. How to verify (UAT)
1. **Restart the API** (the running instance still has old code).
2. *Morning before EOD:* load Dashboard / Portfolio before the day's EOD run — yesterday's valid action is still shown (Prior session / current), not Awaiting Data.
3. *Failed refresh:* a symbol whose data cannot be fetched shows **Awaiting Data** (click the badge: reason, last valid action, freshness), is absent from the Entry/Trim/Risk counters and appears in the Action Center "Not actionable" list; its Signal Tracker history is unchanged.
4. *Partial EOD:* Automation log shows `PartialSuccess` listing the symbols; successful symbols have new timestamps, failed ones keep the old ones; counters equal the actionable list.
5. CLS.TO → Hold / Catalyst Monitoring, BRK.TO → Hold / Monitor (Phase 1 behaviour unchanged); CSV exports show technical columns and the Final Action separately.

## 6. Known limitations / risks
* Signal Tracker export still shows lifecycle status (not effective state); the tracker page itself is unchanged.
* Source-quote freshness is checked for holdings (snapshot quotes carry a timestamp); watchlist evaluation relies on the bar date (`QUOTE_TIMESTAMP_UNAVAILABLE` is recorded).
* Exchange is inferred from the ticker suffix (`.TO/.V/.CN` ⇒ TSX, else NYSE); the holiday tables are rule-based and a unit test fails when the current/next year is missing.
* Two operational constants were introduced (not investment thresholds): evidence grace 2 h after the close; Evaluating heartbeat window 15 min.
* A cached Final Action snapshot lives 10 min; until the first Final Actions load after a restart, Dashboard rows without a published action show Awaiting Data (by design — no legacy fallback).
* Profit-taking tiers stay lot-level by decision; a symbol with several lots may therefore show different profit-taking guidance per lot.
* No dedicated Playwright scenario for the new Awaiting Data UI was added (covered by unit tests and the existing suite).

## 7. Rollback
Additive contract; revert the branch changes to return to Phase 1 behaviour. No schema or persisted-data change to undo; published snapshot items only gain optional `FinalActionSession`.
