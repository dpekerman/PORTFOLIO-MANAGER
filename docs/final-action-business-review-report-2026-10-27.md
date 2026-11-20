# Final Action Reliability — Business Review Report

Date: 2026-10-27 · Branch: `fix/final-action-cls-brk` (based on `develop`) · Status: **not committed to develop; branch review only**

Companion documents: [Phase 1 evidence gate](./final-action-evidence-gate-report-2026-10-27.md) · [Phase 2 reliability](./final-action-phase2-reliability-report-2026-10-27.md) · [UAT readiness](./final-action-phase2-uat-readiness-2026-10-27.md)

## 1. Executive summary

| Question | Answer |
|---|---|
| Were CLS.TO and BRK.TO fixed? | Yes, and fixed in the shared decision logic so every holding benefits, not just these two. Confirmed live in the UI on 2026-10-27. |
| Is the solution complete in code? | Yes for the approved scope. No unresolved coding defect is known. |
| Database work outstanding? | **None.** No EF migrations are pending and no schema change was made. One stale data row needs attention (§6). |
| Verified against a real EOD run? | **Yes.** Run 129 (manual, 16:41 ET) completed `Success` with Refresh, RSI and Snapshot all Succeeded and 4 EOD signals persisted. See §5. |
| Readiness | **READY WITH DOCUMENTED LIMITATIONS.** Remaining items are operational (§6) and optional code work (§7). |

## 2. What was wrong

| Symbol | Symptom | Root cause |
|---|---|---|
| CLS.TO (Swing, bought 2026-10-27, +1.6%) | "Exit / Cut Loss" on a position bought the same day, in profit | A technical Cooling/Breakdown reading mapped straight to Exit with no entry-date, P/L or post-entry check. |
| BRK.TO (Strategic, +8%, ~3.5% of portfolio) | "Trim / Review" with no real trigger | A technical "Active Sell Trigger" was promoted to a Trim decision for a long-term holding. |

Both came from the same weakness: technical signals could produce Exit or Trim decisions without supporting evidence.

## 3. What was implemented

**Phase 1: evidence gate**
- `PortfolioEvidenceGate` allows Exit/Trim/Sell only on real triggers: loss below cost, confirmed structural breakdown, allocation breach, or (short-horizon roles) a technical exit after the entry date.
- Otherwise it substitutes an explicit non-actionable label (Hold / Monitor, Hold / Catalyst Monitoring, Hold / No Add, Add / Needs Review).
- Added `IsActionable`, `ReasonCodes`, `EvidenceAsOfUtc`, `EntryDate` and `OriginalAction` to the API, plus Portfolio CSV columns.

**Phase 2: reliability**
- **Awaiting Data:** missing, stale or failed evidence yields Awaiting Data, never a Hold and never a trade. It is always non-actionable and the prior decision stays visible as history.
- **Evidence freshness:** judged against the latest completed exchange session, not the calendar date. An old quote inside a new scan is not Fresh. Required evidence depends on the decision rule; optional indicators never invalidate a decision.
- **Separate states:** recommendation lifecycle, evidence health and effective display state are tracked independently.
- **Per-symbol EOD publishing:** a partial failure keeps failed symbols' original records and timestamps, and the run is `PartialSuccess`.
- **Canonical Watchlist** Entry/Wait/Avoid decisions, and **allocation rules** across accounts and mixed roles (symbol-level exposure; role limit from the target lot; "Add / Needs Review" when ambiguous).
- **Strategic/Core historical risk:** unresolved warnings stay visible but are not counted as actionable decisions.
- **Evaluating** is bounded by a 15-minute heartbeat, and the exchange-specific `TradingCalendar` covers TSX/NYSE holidays, early closes and DST.
- Legacy code can no longer generate actionable recommendations; `LegacyFallbackGuardTests` enforces this.

**Completion pass (this session)**
- **Defect fixed:** `PortfolioSnapshotService.SaveAsync` rebuilt the snapshot on every price refresh and erased every published Final Action. The live snapshot had 0 of 128 items with a Final Action. It now carries published actions forward.
- Signal Tracker and its exports show lifecycle status separately from current effective state, actionability, evidence health, data-as-of time and reason codes, through a shared **read-only** provider that never scans or publishes.
- Watchlist uses stored quote timestamps, with no new API calls. If a timestamp is missing, it falls back to the bar date and marks verification unavailable, never Awaiting Data on that alone.
- Exchange resolution order is metadata, then ticker suffix, then an explicit NYSE assumption shown in the UI.
- Portfolio and Watchlist now distinguish Loading, Not evaluated, Awaiting Data and API error.

Scope: 57 files changed against `develop`, +4,380 / −325 lines.

## 4. Test evidence

| Suite | Passed | Failed | Skipped |
|---|---|---|---|
| Backend (.NET) | 792 | 0 | 3 (opt-in SQL tests) |
| SQL integration, run on a disposable LocalDB (3 tests) | 3 | 0 | 0 |
| Angular unit | 384 | 0 | 0 |
| Playwright (desktop, tablet, S21, S23; mocked API) | 615 | 0 | 89 |

- The 89 Playwright skips are viewport-conditional and not related to this work. 48 of the passes are the new Awaiting Data scenarios.
- Not run: production Angular build. `tsc` and the dev build are clean.
- These suites were last run before today's live testing; no code changed afterwards.

**Live UI check, 2026-10-27, logged-in session**
- **Portfolio:** every row has a Final Action, with none blank, Awaiting Data, Not evaluated or API error. CLS.TO shows Hold / Catalyst Monitoring and BRK.TO shows Hold / Monitor.
- **Watchlist:** all 29 visible rows show canonical decisions.
- **Dashboard Action Center:** it counts only validated decisions (1 Trim / Exit, WMT.TO) and lists others as "not actionable, not counted".
- **Signal Tracker:** Lifecycle and Current state appear separately, and BRK.TO's old Trim / Review is marked Invalidated.
- **Evaluating:** HUN.TO and SPGI.TO showed "Evaluating" after an interrupted run. When the 15-minute heartbeat window lapsed, they moved to "Historical warning — evidence stale (evaluation interrupted)", still non-actionable.

## 5. Post-EOD verification (run 129, 2026-10-27 16:41–16:43 ET, authorised manual "Run Automation Now")

- Run status `Success`; Refresh, RSI and Snapshot all `Succeeded`; 4 EOD signals persisted; no error step.
- **Portfolio:** no blank, Awaiting Data, Not evaluated or API error cells. CLS.TO is Hold / Catalyst Monitoring, BRK.TO is Hold / Monitor, MDA.TO is Hold / Technical Caution, SPGI.TO is Hold / Monitor, HUN.TO is Cut Loss / Exit (price below cost, -12.5%), EFR.TO is Add to Trade, CABA is Add / Staged Entry, WMT.TO is Trim / Review.
- **Dashboard Action Center:** 3 Entry/Add and 2 Trim/Exit (5 decisions: 4 holdings, 1 watchlist). These reconcile with the Portfolio rows. The HUN.TO Exit now shows its reason and evidence time (20:43 UTC).
- **Signal Tracker:** today's rows show Lifecycle Active and Current "Current evaluation". HUN.TO and SPGI.TO's 2026-10-26 events remain as "Unresolved historical warning · not actionable". The history is preserved, as designed.
- **Watchlist:** 29 rows, none Awaiting Data or Not evaluated.
- Run 128 is still marked `Running` (orphan, see §6).

Observation for business review: WMT.TO remains Trim / Review. It is allowed by the gate and was not changed by this work.

## 5a. Final re-validation (2026-10-27 17:00–17:25 ET, after a fresh restart of API, UI and SQL)

| Check | Result |
|---|---|
| Backend tests | 792 passed, 0 failed, 3 skipped (opt-in SQL tests) |
| The 3 opt-in SQL tests (disposable LocalDB) | 3 passed |
| Angular `tsc` and unit tests | Clean; 384 passed (48 files) |
| Playwright, 4 device projects (desktop, tablet, S21, S23) | 615 passed, 0 failed, 89 viewport-conditional skips |
| Latest EOD runs | 129 and 130 both `Success`; run 128 still an orphan `Running` row |
| Live UI, 5 surfaces | No blank, Awaiting Data, Not evaluated or API error cells. CLS.TO and BRK.TO are unchanged. Dashboard counts reconcile with Portfolio (3 Entry/Add, 2 Trim/Exit). Watchlist has 29 canonical rows. Signal Tracker shows Lifecycle and Current state separately. |

Observation: the 2026-10-26 HUN.TO and SPGI.TO events are listed as "Unresolved historical warning · not actionable" with the note "The latest evaluation did not include this symbol". They were superseded by today's evaluations (HUN.TO now Cut Loss / Exit, SPGI.TO Hold / Monitor). The wording is slightly misleading for these cases. It is cosmetic, because the warnings are non-actionable and the history is preserved, but it is worth a wording fix.

## 5b. What is not verified

1. **Partial-failure EOD run.** Run 129 was a full success, so per-symbol publishing during a real partial failure was verified only by tests.
2. **Real-data end to end** in an isolated copy (not authorised).
3. **The Export file in the live browser,** and mobile/tablet in the live browser. Mocked suites cover both.

## 6. Database status

| Item | Status |
|---|---|
| EF migrations | None pending. None were added. |
| Schema changes | None. |
| Data row needing attention | `AutomationRunLogs` Id 128 is stuck at `Running` with its heartbeat stopped at 19:22 UTC. The app ignores it after 15 minutes, but the row is untidy. It is superseded by the next EOD run, or a one-row cleanup needs your approval. |
| Scheduled Task drift | Config → Automation reports the Windows Scheduled Task's next run as today 4:45 PM ET, but the expected next run is tomorrow 3:15 PM ET (~1,350 min drift). The task's own trigger is out of line with the Eastern Time config. It is not part of this work and I did not change it. Use "Repair Automation" on that page when convenient. A run at 4:45 PM would sit after the 15:30–16:30 EOD window. |
| Live portfolio data | Not modified: transactions, cash, options and history. Published Final Actions and timestamps did change, because loading the pages publishes them. |

## 7. Remaining code work, in priority order

| # | Item | Why |
|---|---|---|
| 1 | Run and verify one EOD cycle (see §8) | Only remaining acceptance gap. |
| 2 | Make evidence grace (2 h) and heartbeat (15 min) configurable | Currently fixed constants; works, but changing them needs a release. |
| 3 | Use exchange metadata in `OpenDecisionsService` and `SignalsAuditService` | They infer the exchange from the ticker suffix only, and the Open Decisions banner uses the TSX calendar. Other paths use metadata first. |
| 4 | Hold allocation-originated Strategic risks open | Only structural-breakdown risks are held as unresolved warnings today. |
| 5 | Mark or remove dead frontend code | `decision-engine.service.ts` `translateForPortfolio` has no production caller. |
| 6 | Review the "WAIT FOR RECLAIM" outcome | A legacy hard-structural-negative case now reads "WAIT FOR RECLAIM" instead of "AVOID". This is by design but is a business-visible label change. |
| 7 | Quote timestamp is the last-trade time | Illiquid names may look older than they are. |

None of these is a blocker. Items 3, 4 and 6 are the ones I would raise with the business.

## 8. Recommended next steps

1. Business review of this report and the label changes (§9).
2. Repair the Windows Scheduled Task drift (§6) so tomorrow's 3:15 PM wake and EOD run happen on time. Confirm the next scheduled run records `Success`.
3. Decide on the code items in §7 and on cleaning the stale run 128 row.
4. Commit and push is your decision. Nothing has been committed or pushed by me.

## 9. Risks

- The first page load after a refresh publishes Final Actions. This is expected, but it means opening the UI changes published records.
- The orphan run 128 remains until superseded.
- This branch changes decision labels for existing positions; business reviewers should expect some Exit/Trim labels to become Hold / Monitor.
