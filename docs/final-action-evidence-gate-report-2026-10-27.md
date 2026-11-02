# Final Action correctness — CLS.TO / BRK.TO and the general fix (2026-10-27)

Branch: `fix/final-action-cls-brk` (from `develop`). **Nothing is committed or pushed.** No database write, migration, cash or valuation change.

## 1. What was wrong (verified in code and in the local database, read-only)

| | CLS.TO | BRK.TO |
|---|---|---|
| Facts | Swing, 50 sh @ 382.20, opened **2026-10-27**, Decision Source *Earnings Catalyst*, price 388.42 (**+1.6 %**) | Strategic, 700 sh @ 36.34, opened 2026-08-24, price 39.30 (**+8.1 %**), ~3.5 % allocation |
| Displayed | `Exit / Cut Loss` (trim / REQUIRED) | `Trim / Review` (trim / REQUIRED), "review stop-loss levels" |
| Rule | `FinalActionEngine.SwingPortfolioAction`: `Cooling` or `Breakdown` → *Exit / Cut Loss* | `FinalActionEngine.StrategicPortfolioAction`: technical `Active Sell Trigger` → *Trim / Review* |
| Why it is a defect | A technical state became a loss exit with no loss, no post-entry evidence, and the entry date ignored | A candle-based technical status became a portfolio Trim; the "review stop-loss levels" reminder (`PortfolioHover`) comes from the same trigger. Allocation, P/L and configured limits were not consulted |

Both are instances of the same **defect classes**, which affect every holding and every role (Core, Strategic, Swing, Speculative, Options and any other role):

* **A – technical state promoted to a portfolio sell.** Every role map turns technical-only statuses into Trim / Exit / Cut Loss / Close.
* **B – pre-entry evidence treated as fresh.** The entry/last-add date was never compared with the evidence date.
* **C – reminder vs trigger.** The Action Center bucket (`DecisionEligibility`), Dashboard counters, Signal Tracker events and exports are all derived from the final action label, so a promoted reminder became an "actionable" recommendation everywhere.
* **D – purchases ignored configured limits.** `SinglePositionLimits` and `AllocationSectorTargets` were not used by the Final Action engine.
* **E – lifecycle.** The 3-day Trim/Exit expiry was applied to Strategic/Core exits too.

## 2. The fix (long term, rule-based — no symbol names, no invented thresholds)

One new pure step, **`PortfolioEvidenceGate`** ([PortfolioEvidenceGate.cs](../backend/PortfolioManager.Api/Services/FinalAction/PortfolioEvidenceGate.cs)), runs inside the canonical pipeline `FinalActionService.ComputeAsync` right after `FinalActionEngine.TranslateForPortfolio`. Every consumer (Portfolio, Action Center, Dashboard counters, Signal Tracker, Priority/EOD publishing, exports) already reads this pipeline, so they all agree automatically.

**Reductions** (Trim / Exit / Cut Loss / Close / Take-Profit rows of the role tables and base actions) are kept only when a portfolio-level trigger exists:

| Trigger | Applies to |
|---|---|
| Position size **greater than the configured single-position limit for the role** | Trim and profit exits, all roles |
| **Confirmed structural breakdown** (existing `HasHardStructuralNegative`) evaluated **after** the entry date | all roles |
| **Price below cost** with technical weakness (loss exit) | short-horizon roles (Swing, Speculative, Options) — also on the entry day, because it is a post-entry fact |
| Technical exit signal evaluated **after** the entry day | short-horizon roles |

Strategic / Core / any other role are long-horizon: a technical signal alone, or a loss alone, never creates a reduction. Existing profit-taking overrides (≥20/30/50 % rules), advisory actions, watch actions and watchlist decisions are untouched.

If no trigger exists the action becomes advisory: **`Hold / Monitor`** (or **`Hold / Catalyst Monitoring`** when the lot's Decision Source records a catalyst). The original technical reading stays visible: the hover keeps the "Review stop-loss levels" **reminder** (not a sell), and the reason text lists the original action, signal/entry dates and evidence time. Nothing is hidden: the DTO exposes `originalAction`.

**Purchases**: an Add/Entry action on a holding at or above its configured single-position limit, or in a sector above its configured target (+2 % tolerance already used by the Action Center), becomes `Hold / No Add`.

**Entry date** = most recent open lot (latest addition) of the symbol. Unknown entry date never suppresses a short-horizon exit (`ENTRY_DATE_UNKNOWN`). **Evidence date** = America/New_York date of the scan.

**Canonical result fields** added to `FinalActionResultDto` and the Angular model: `isActionable`, `reasonCodes`, `evidenceAsOfUtc`, `entryDate`, `originalAction`. Reason codes: `TECHNICAL_ONLY_NO_PORTFOLIO_TRIGGER`, `EVIDENCE_NOT_POST_ENTRY`, `ENTRY_DATE_UNKNOWN`, `ALLOCATION_LIMIT_BREACH`, `POST_ENTRY_LOSS`, `CONFIRMED_STRUCTURAL_BREAKDOWN`, `TECHNICAL_EXIT_POST_ENTRY`, `ADD_BLOCKED_POSITION_LIMIT`, `ADD_BLOCKED_SECTOR_OVER`.

**Signal Tracker / lifecycle** ([SignalsAuditService.cs](../backend/PortfolioManager.Api/Services/SignalsAuditService.cs)): non-actionable results never create Trim/Exit events (they are advisory, so existing eligibility rules skip them); audit context now stores reason codes, evidence time, entry date and `isActionable`; Strategic/Core Trim/Exit recommendations are **not expired by the calendar window** (only by a complete evaluation that no longer produces them); Entry/Add 5-day and short-horizon Trim/Exit 3-day windows are unchanged. History, Acted/Dismissed states and original signal date are untouched.

**Exports**: the Portfolio CSV adds `Actionable` and `Decision Reason Codes` next to `Action`, taken from the same canonical result. The `Momentum Shift` column remains the *technical* reading (it can still say "Active Sell Trigger") and is no longer confused with the decision: `Action` = *Hold / Monitor*, `Actionable` = *No*.

### Files changed
* `backend/.../FinalAction/PortfolioEvidenceGate.cs` (new), `FinalActionService.cs`, `FinalActionCatalog.cs` (rows *Hold / Monitor*, *Hold / Catalyst Monitoring*), `SignalsAuditService.cs`
* `frontend/.../portfolio.models.ts`, `final-action.store.ts`, `portfolio-page.component.ts` (export)
* Tests: `PortfolioEvidenceGateTests.cs` (new), `FinalActionCatalogTests.cs`, `SignalsAuditServiceTests.cs`

### Deliberately not changed
* `TranslateForPortfolio` and the Angular/back-end parity golden corpus (10k+ cases still pass) — the gate is a separate layer, so technical rules stay a stable specification.
* Angular `decision-engine.service.ts#translateForPortfolio` has no production caller (portfolio/watchlist use the backend store), so no mirror was needed.
* `PortfolioActionsService.DeriveAction` (legacy Action Center fallback) is only used when no same-day canonical result was published; the canonical value always wins for holdings. Residual risk: a stale holding falls back to the legacy rules until the next publish (EOD or any Final Actions load).
* Unowned (watchlist) entry candidates are not constrained by allocation limits; cash ledger, valuation and migrations untouched.
* Dashboard: the counters already sit inside the Action Center card and filter the list (no change needed).

## 3. Verification performed
* Backend: **688 passed, 0 failed** (3 pre-existing skips). New: ~270 gate cases including a role × 13 technical reductions matrix (never actionable on entry day without loss; allocation breach never hides a policy trim; idempotent), CLS-type and BRK-type scenarios, missing catalyst/entry metadata, stop-review-only, real loss / breakdown still actionable, add-side limits, and an end-to-end `FinalActionService` test (new buy today, old winner, old loser, loser bought today). Signal Tracker: Strategic Trim/Exit not expired, Swing still expires, repeated evaluation creates no duplicates.
* Frontend: `tsc` clean, **374 unit tests passed**.
* Playwright was **not run**: the local dev server already occupies port 4200 and the config could not start. Only the export columns changed in the UI.

## 4. How to test
1. Stop the running API and restart it (`cd backend\PortfolioManager.Api; dotnet run --launch-profile http`) so the new code loads.
2. Open Portfolio (or call `GET /api/final-actions?fresh=true`): CLS.TO should show **Hold / Catalyst Monitoring** and BRK.TO **Hold / Monitor**; Dashboard → Action Center should no longer count them under Trim / Exit; the next EOD publish refreshes the persisted snapshot.
3. Portfolio → Export CSV: `Action` = Hold / Monitor, `Actionable` = No, reason codes present; `Momentum Shift` still shows the raw technical status.
4. Signal Tracker: no new `FA:TRIM/REVIEW` / `FA:CUT LOSS/EXIT` events for those symbols; old event #33 (BRK.TO, already Invalidated) remains as history.
5. Real-risk check: a swing lot with price below cost still shows *Exit / Cut Loss* (actionable); a Strategic holding over its configured position limit still shows *Trim / Review*.
6. Automated: `cd backend\PortfolioManager.Tests; dotnet test`, `cd frontend\portfolio-manager-ui; npx ng test --watch=false`, then `npx playwright test` when port 4200 is free.

## 5. Long-term design in one paragraph
Technical engine = *what the indicators show* (unchanged, parity-locked). `PortfolioEvidenceGate` = *what this holding should do*, using role horizon, entry date, P/L, configured allocation limits and structural evidence. `DecisionEligibility` classifies the canonical result once; Dashboard, Action Center, Signal Tracker and exports read that classification (and `isActionable`/reason codes) instead of re-deriving a sell from labels. New roles or actions only need a catalog row and, if short-horizon, an entry in the role set — no symbol logic exists anywhere.

## 6. Risks / follow-ups
* The gate intentionally makes sells rarer for long-horizon roles; policy owners can adjust triggers in one place (`PortfolioEvidenceGate.Apply`).
* Evidence-date comparison uses the scan's New York date (the engine has no per-bar history); a same-day re-scan after a purchase is treated as entry-day evidence.
* Pre-existing quirk noted: the shared class helper reads "Hold / No Add" as a buy; the gate sets `ma-hold` explicitly. Fixing the helper would break golden-corpus parity and was left alone.
* EOD reliability (P1.3): existing Unknown/complete-pass handling was left unchanged and is covered by the existing suite; Toronto trading-day edge cases still need runtime verification.
