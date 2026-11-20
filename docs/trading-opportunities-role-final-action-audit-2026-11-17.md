# Trading Opportunities — Investment Role & FINAL ACTION audit

**Audit only.** No source, database, threshold or policy was changed, and nothing was run live. This file is the only addition.
Branch: `fix/final-action-listing-fibonacci`; its uncommitted source changes are untouched.

**Evidence limits**
- Findings come from code and data contracts. Items marked *(code reading)* were not executed.
- The 17 Nov 2026 Trading Opportunities snapshot and live ownership are **not in the repository**, and I did not query the database. Ownership and roles in section 5 are your stated scenarios, not verified facts.
- Repository evidence is older: `docs/pm029-gate2-final-portfolio-fit.md` shows FFH.TO at 3.43 % and NVDA.TO at 4.48 %, with limits Core/Strategic 5, Swing 3, Speculative 2. Limits live in `SinglePositionLimits` and can differ now.

## 1. Current architecture

```text
EvaluatePreviewAsync(user)  ── Watchlist + open lots, read-only/preview scans ──► per-symbol scans (kept in memory)
   ├─ Portfolio rows : one decision PER OPEN LOT, using the lot's HoldingRole        (TranslateForPortfolio + PortfolioEvidenceGate)
   └─ Watchlist rows : one decision per watchlist symbol, using the watchlist Role    (TranslateForWatchlist + ConditionEntry / ConditionOwnedEntry)
EvaluateUniverseAsync(user, symbols that are neither watchlisted nor held)
   └─ read-only scans; the same Watchlist path with a hard-coded role "Strategic"      (FinalActionService.cs: `const string role = "Strategic"`)
OpportunityService.BuildAsync: one immutable snapshot per refresh
   ├─ Swing score and Strategic score are BOTH computed for every row (OpportunityScoringEngine.ScoreSwing / ScoreStrategic)
   ├─ SetupQuality (Swing) and StrategicSetupQuality are both computed
   ├─ Fit: one value per row, from ExposureFor(symbol, WatchlistRole ?? "Strategic")        (OpportunityService.cs:307)
   └─ FINAL ACTION: copied from the rows above, not re-evaluated   (faRows :283, per-symbol :311, SummarizeDecisions :408)
```

- **The tab is client-only.** `perspective` lives in the browser store's saved view. `OpportunitiesApiService` calls `GET /api/opportunities` with no perspective or role parameter. The backend never sees the tab.
- **What the tab changes:** active score column, default sort, score filters, Market R, Setup Quality, Setup/Entry/Stop/Target columns and export.
- **What the tab does not change:** FINAL ACTION, Fit, Role, and the role used by FINAL ACTION.

### Which source supplies FINAL ACTION

| Symbol state | Source of the grid's FINAL ACTION | Role used |
| --- | --- | --- |
| Held (any role or account) | The Portfolio lot decisions only. A Watchlist "ADD" view of a held symbol appears only in the drawer. | The lot's real `HoldingRole`. A blank role is treated as Strategic. |
| Not held, on Watchlist | The Watchlist decision. | The watchlist item's Role. Blank means Strategic. |
| Not held, TSX 60 only | The Universe decision. | Hard-coded Strategic. |
| Held in several roles or accounts | `Single` if all lots agree. Otherwise `Multiple (n)` with the distinct labels, and the drawer lists each lot as source · role. | Each lot's own role. |

Because of this, the Swing tab shows Strategic-style decisions for unheld symbols and the holding's own decision for held ones. The grid labels none of this.

### How many decision paths exist

- **Three engine entry points** in `FinalActionEngine`:
  - `TranslateForWatchlist`: the new-entry rule. It is shared by the Watchlist, owned-Watchlist and Universe paths.
  - `TranslateForPortfolio`: the holding rules.
  - `TranslateForRsiScanner`: a base scanner rule with no role.
- **Six role tables** inside the holding rules: Core, Strategic, Swing, Speculative, Options, and a base fallback for unknown roles.
- **Layers on top of the holding tables:** profit-taking overrides (12 outputs), TFSA, risk-control, gap rules and the Fibonacci modifier.
- **Gate layers** in `PortfolioEvidenceGate`:
  - reductions: Trim, LossExit, ProfitExit, ProfitTake, ProfitWatch;
  - add conditioning: limits, sector, role ambiguity, allocation, and the new Strategic reversal safeguard;
  - Strategic tactical profit protection;
  - Swing stop risk.

### Duplication

- The Angular `DecisionEngineService` is a parity-locked copy of the backend engine. It now serves only the legacy scanner table.
- `PortfolioFitCalculator` re-implements the gate's limit and sector comparisons. Its own comment says they are "the same comparisons".
- Opportunities itself copies decisions verbatim, so there is no third copy of the rules.

## 2. Role matrix (existing behaviour)

| | **Swing** | **Strategic** | **Core** |
| --- | --- | --- | --- |
| **New entry** (Watchlist / Universe rule) | `ENTRY CANDIDATE` needs confirmed structure, accelerating momentum, Buy Score ≥ 4, acceptable MAs and no trend damage. **No `STARTER ENTRY`** (role "needs more confirmation"). `BUY WATCH` otherwise. | Same rule plus **`STARTER ENTRY`** on confirmed *or support-test* structure with positive momentum and Buy Score ≥ 4. | Same as Strategic. |
| **Gate for a new entry** | `ConditionEntry`: allocation reliable, role limit (configured in `SinglePositionLimits`; Swing 3 % in older repo evidence), sector over target by more than 2 pp. | Same gate with the Strategic limit. | Same gate with the Core limit. |
| **Existing holding, add trigger** (`Active Buy Trigger`) | `Add to Trade` | `Add / Staged Entry` | `Add to Position` |
| **Existing holding, falling trend** | `Cut Loss / Exit`; the gate turns it into an advisory hold unless a loss or structural trigger exists. | `Hold / Risk Review`. A *new* reversal safeguard blocks an add. | `Hold / Risk Review` |
| **Role-specific extras** | Stop-risk escalation (`Review Stop Breach`) for open Swing lots only. | Tactical profit protection (`Profit Protection Watch` / `Consider Partial Trim`) and the **reversal safeguard**. | None. |
| **Shared by every role** | Profit-taking overrides, TFSA rule, risk-control and gap/Fibonacci modifiers on holdings. | | |

**Safeguard coverage today**

| Safeguard | Applies to |
| --- | --- |
| Position limit, sector limit, allocation reliability, role ambiguity | Every Entry-category result: Portfolio adds, owned and unowned Watchlist entries, Universe entries. |
| Stale or missing evidence → `Awaiting Data` | Every path. |
| Strategic reversal safeguard | **Held Strategic lots only.** `BuildEvidence` sets `TargetRole`; `BuildNewPositionEvidence` does not. |
| Risk category, Setup Quality, Market R | **Advisory only.** They never change FINAL ACTION; a code comment records this as owner-approved policy. |

The safeguard applies correctly to Strategic holdings and to owned Watchlist adds. It does not apply to Core or Swing adds, or to any new entry.

## 3. Confirmed problems

1. **The tab does not drive FINAL ACTION or Fit, and the grid does not say so.** A held symbol's Strategic or Core lot action shows under a Swing score. A held Core symbol can show `Add to Position` beside a Swing score of 80.
2. **Unheld candidate role is implicit.**
   - Universe-only rows are hard-coded Strategic and cannot be evaluated as Swing.
   - Watchlist rows use their own role, regardless of the tab.
   - The Role column is blank for unheld symbols and hidden by default.
3. **Fit ignores the tab.**
   - Unheld rows measure headroom against the Strategic limit, or the watchlist role's limit.
   - Held rows use the held role's limit, with `Review: role policy ambiguous` for mixed roles.
   - A Swing candidate is never measured against the Swing limit.
4. **The Role filter mixes held and watchlist roles.** `row.roles` and `row.watchlistRole` go into one set. The Role column shows held roles only and the export lists the same.
5. **There is no candidate evaluation for a held symbol.** `BuildNewPositionEvidence` assumes exposure 0 (`positionSize = 0`). Reusing it as-is for a held symbol would ignore existing exposure. The owned-Watchlist path uses the lot's role limit and the lot's own decision.
6. **Reversal safeguard scope gap.** Core (`Add to Position`) and Swing (`Add to Trade`) use the same `Active Buy Trigger` path with no guard. *(code reading; Swing may be intentional)*
7. **Possible equivalent gap for new entries.** *(code reading)*
   - `STARTER ENTRY` accepts `supportConstructive` (support test or approaching support), which is not a confirmed reversal, while the trend can still be `Waterfall / Falling Knife`.
   - The Watchlist rule checks hard negatives and declining momentum first. `Active Buy Trigger` counts as positive.
   - So an unheld Strategic or Core symbol can plausibly reach `STARTER ENTRY` before reversal confirmation.
   - I did not execute this.
8. **Four distinct states exist, but no single candidate verdict.**
   - **Eligible** is Fit only: allocation headroom, with weight 0 for unheld symbols. It says nothing about technicals.
   - **Actionable** means the catalog category is Entry (or a held Trim/Exit).
   - **WATCH, WAIT, AVOID and BUY WATCH** are Advisory and never actionable.
   - **ADD / ENTER** is `ENTRY CANDIDATE`, `STARTER ENTRY` or a Portfolio add, after the gate.
   - A high score plus Fit `Eligible` does not imply an entry; only the FINAL ACTION category does.
9. **Role defaults are silent.**
   - A blank holding role is evaluated as Strategic.
   - An unknown role such as `Income` falls to the base action and has no configured limit.
   - Per-account rules exist only for TFSA profit-taking and Swing stop risk. There is no account-eligibility concept for new entries.

Not a defect: the existing behaviour for multiple holdings. Differing lot decisions already show `Multiple (n)` and a per-lot drawer.

## 4. Can the current backend support Candidate Action and Holding Action?

| Need | Status |
| --- | --- |
| **Holding Action** (real role, account, exposure, risk) | **Already supported.** It is the Portfolio rows plus `SummarizeDecisions`. |
| **Candidate Action** for an explicit Swing or Strategic role | **Not exposed; new orchestration is required, but no new rules.** Evidence: `TranslateForWatchlist(input, role, …)` is pure and role-parameterised, and `BuildNewPositionEvidence(symbol, role, …)` and `ConditionEntry` already exist. The only caller hard-codes Strategic. Scans for every row are already in memory (`evaluation.Scans`, `universe.Scans`), so no extra market data is needed. |
| **Candidate rows must not touch the portfolio or Signal Tracker** | **Feasible.** Universe evaluation already uses read-only scans and writes nothing. Signal capture reads only the Watchlist and Portfolio snapshot, so candidate rows kept out of that snapshot stay out of capture. |
| **Swing and Strategic candidate results differing materially** | **Small today.** For the Watchlist rule the roles differ only in `STARTER ENTRY` availability and flags. The gate differs by the role's position limit. Both tabs will often produce the same label. |

## 5. Example results

All are conditional on ownership you must confirm against the live snapshot. "Candidate supported" means the engine could calculate it with the explicit role added. I did not run a Swing candidate evaluation anywhere.

| Symbol (scenario) | Role FINAL ACTION uses today | Source today | Swing candidate | Strategic candidate | Would Candidate differ from Holding? | Respects current exposure? |
| --- | --- | --- | --- | --- | --- | --- |
| **PPL.TO** (unheld, Swing 94) | Strategic if TSX 60 only. Its Watchlist role if watchlisted. | Universe or Watchlist row. | Not exposed. Engine can: no `STARTER ENTRY`, Swing limit. | Yes (the implicit current result). | No holding. Candidate action is the only action. | Yes for sector and the Strategic limit. The Swing limit is not applied. |
| **MTL.TO** (unheld, Swing 90) | Same as PPL.TO. | Same. | Same. | Same. | Same. | Same. |
| **TRP.TO** (Strategic holding, Swing 84) | Strategic (lot role). | Portfolio lot decision. | Not exposed. It must also not become an add to the Strategic lot. | Would be a Strategic add judged by the Strategic gate. | Yes. A new Swing lot is a different decision from managing the Strategic lot. | Fit uses whole-symbol exposure against the **Strategic** limit. Swing limit and combined exposure are not shown. |
| **MRU.TO** (Core holding, Swing 80) | Core. | Portfolio lot decision. | Not exposed. | Not exposed. | Yes. The Core decision can be `Add to Position` under the Swing tab. | Core limit and whole-symbol exposure. |
| **FFH.TO** (Core holding, Strategic 88) | Core. | Portfolio lot decision. | Not exposed. | Not exposed. | Yes. The Strategic tab shows a Core decision. | Core limit. Repo evidence shows 3.43 % (older). |
| **NVDA.TO** (Strategic holding, adverse structure) | Strategic (lot role). | Portfolio lot decision. After the new safeguard: `REVERSAL WATCH — Wait for Confirmation` if unconfirmed. | Not exposed. | The Watchlist rule on the NVDA scan gives `REVERSAL WATCH` when unconfirmed. | Distinct only if it were framed as a *new* Swing lot. | Holding exposure is the CAD listing (about 4.3 % in your scenario). Technical evidence is NVDA in USD. |

## 6. Proposed solution — minimal, no new rules

**Fields**

| Field | Meaning |
| --- | --- |
| **Evaluation Role** | `Swing` or `Strategic`. It follows the tab and is passed explicitly to the evaluation. Core is excluded. |
| **Held Role** | The actual lot roles. `None` if unheld. Never overwritten or reinterpreted. |
| **Candidate Action** | The Watchlist-rule decision for the Evaluation Role, then the existing gate. |
| **Holding Action** | The existing authoritative Portfolio decisions, unchanged. |

**Backend (reuse; no rule changes)**
1. In `FinalActionService`, add one pure method, for example `EvaluateCandidates(scans, basis, symbols, roles)`.
   - It reuses `WatchlistDecision`, `AssessEvidence` and `ConditionEntry`.
   - It writes nothing and calls no market data.
   - Refactor `EvaluateUniverseAsync` to call it with Strategic, so its current output is unchanged.
2. Add a candidate-evidence variant of `BuildNewPositionEvidence` that sets the existing whole-symbol exposure and uses the Evaluation Role's limit.
   - For a symbol held in another role, make the result explicit: "separate new lot; combined exposure X %".
   - Never convert it into an add to the existing lot.
3. Compute Fit per Evaluation Role using `ExposureFor`'s existing inputs.
4. Additive DTO fields on `OpportunityRowDto`, with the existing fields kept:
   - `HeldRoles`: the same data as today's `Roles`.
   - `CandidateActions`: results keyed by role, with label, id, category, actionable flag, reason codes, evidence health and Fit.
   - Existing `FinalAction` stays as the Holding Action alias.
   - `FitByRole`.

**Frontend**
- Add `Candidate Action` (tab-driven, shown by default) and rename the visible `FINAL ACTION` header to `Holding Action`, keeping the column key `finalAction` so saved column preferences and exports stay valid. Add a `Held Role` column.
- Show the Evaluation Role in the tab/header, not in each row.
- Split the Role filter into Held Role and Evaluation Role.
- Keep the grid layout, sorting, filters, column registry and export columns; the new columns are additive.
- Older snapshots show `Not evaluated — refresh` for Candidate Action; no migration is needed because the snapshot payload is JSON.

**Tests (focused, offline)**
- Same stock evaluated as Swing and as Strategic with the same scan. Only the role-dependent parts change.
- Unheld, held Core, held Strategic, held Swing, and mixed-role symbols.
- Candidate evaluation never creates an add to an existing Core or Strategic lot.
- Stale, missing and CDR price-basis cases.
- Candidate rows never enter the Signal Tracker snapshot.
- Holding Action output is byte-identical to today (golden/parity suites stay green).
- Sorting, filtering, column visibility and export for the new columns.

## 7. Impact and risks

- **Signal Tracker and history.** Candidate rows are kept out of the Watchlist and Portfolio snapshot, so no recommendation is created or rewritten. Holding Actions and past events are unchanged.
- **CDR.** Candidate technicals use the analysis security (USD). Position and Fit use the CAD listing. The row's `AnalysisTicker`, `UsesUnderlying` and currency fields already separate them. Candidate Action for a CDR must not claim a CAD entry price from the USD evidence.
- **Allocation.** Candidate evidence must use the existing reliable-currency check. An unreliable allocation gives `Add / Needs Review`, never an approval.
- **Snapshot size and refresh time.** Two small candidate rows per symbol; no new market calls.
- **Behaviour surprise.** Because both roles share one rule, the two tabs will often show the same candidate label. Say so in the tooltip.
- **Saved views and export.** Keeping the `finalAction` key avoids breaking them.

## 8. Implementation plan (after approval)

1. Backend: candidate evaluator (pure) and refactor the Universe path onto it, with a unit test that Universe output is unchanged.
2. Candidate evidence with whole-symbol exposure and the Evaluation Role's limit, plus held-in-another-role handling.
3. Per-role Fit.
4. DTO and snapshot additive fields; API contract tests.
5. Frontend model, columns, Evaluation Role in the header, filter split, export columns.
6. Focused tests, with Holding Action output proven unchanged.
7. Manual UAT on the next refresh with the six example symbols.

## 9. Decisions requiring owner approval

1. Is Candidate Action for a symbol held in another role shown as a **separate new lot** (limit = Evaluation Role's, exposure = combined), or suppressed with a note?
2. Should Core and Swing holding adds in a falling knife get the same reversal confirmation as Strategic? (Not changed today.)
3. Should `STARTER ENTRY` on an unconfirmed support test during a Waterfall be allowed for unheld Core or Strategic candidates? (Finding 7; verify with a scan first.)
4. Should High risk, Setup Quality or Market R gate candidate entries? Today they are advisory by approved policy; no change proposed.
5. Which account does a candidate use for per-account rules? None exists for entries today.
6. When the Watchlist role disagrees with the tab, does the tab (Evaluation Role) win for Candidate Action? Recommended: yes, and show the Watchlist role as Held/Intended.
7. Final labels and default visibility of `Candidate Action`, `Holding Action`, `Held Role`, `Evaluation Role`.

**Stopping here. Awaiting approval before any implementation.**
