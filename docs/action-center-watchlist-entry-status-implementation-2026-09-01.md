# Action Center and Watchlist Entry Status Implementation

Date: 2026-09-01

## Summary

This change adds persistent Action Center filtering, sortable Action Center tables, and one canonical non-owned Watchlist Entry Status evaluator. Portfolio Position Action behavior remains unchanged.

The implementation does not alter Price Structure calculations, ATR thresholds, wedge/channel/Fib/MA logic, Buy Score formula, roles, allocation calculations, or database schema.

## 1. Files and Classes Changed

- `core/services/decision-engine.service.ts`
  - Added `WatchlistEntryStatus` canonical vocabulary.
  - Added pure `evaluateWatchlistEntry()` hierarchy and diagnostics.
  - Routed `translateForWatchlist()` through the canonical evaluator.
- `core/services/decision-engine.spec.ts`
  - Added synthetic named regression cases and hard-negative tests.
- `features/dashboard/portfolio-actions-widget/portfolio-actions-widget.component.ts`
  - Added filter persistence, independent table sorting, and shared Watchlist Entry Status use.
- `portfolio-actions-widget.component.html`
  - Added clickable sortable headers and canonical Entry Status display for Watchlist only.
- `portfolio-actions-widget.component.scss`
  - Added sort-header hover and keyboard-focus states.
- `portfolio-actions-widget.component.spec.ts`
  - Added persistence and sorting tests.
- `core/price-structure-display.ts` and its tests
  - Updated two wedge labels and lifecycle-aware level wording.

## 2. Canonical Final Action Model

Non-owned Watchlist securities now return only:

- `ENTRY CANDIDATE`
- `STARTER ENTRY`
- `BUY WATCH`
- `REVERSAL WATCH`
- `WATCH / NO CHASE`
- `WAIT FOR PULLBACK`
- `WAIT FOR REVERSAL`
- `WAIT FOR RECLAIM`
- `AVOID`
- `WATCH` as a neutral fallback

The evaluator returns a `WatchlistEntryDecision` containing the canonical action, reason, technical context, and blocker diagnostics.

## 3. Legacy Labels Removed or Mapped

`translateForWatchlist()` no longer exposes legacy variants such as `Stand By`, `Accumulate Starter`, `Watch / Small Entry OK`, `Buy Candidate / Staged Entry`, or `No Chase / Extended`.

Legacy role-specific helpers remain private because Portfolio and Scanner behavior must not be redesigned in this task. Their output is no longer the final Watchlist result; current technical facts are re-evaluated into the canonical vocabulary.

## 4. Hard-Negative Override

The evaluator recognizes current and equivalent hard-negative states including:

- `FAILED_BREAKOUT` / `BREAKOUT_FAILED`
- `SUPPORT_BROKEN`
- `BREAKDOWN_CONFIRMED`
- `CHANNEL_BROKEN` / `CHANNEL_SUPPORT_BROKEN`
- `WEDGE_BREAKDOWN`
- `TIGHT_WEDGE_BREAKDOWN`
- rising-wedge breakdown equivalents
- shared `hasHardStructuralNegative`

Hard structure is evaluated before Momentum, MA Structure, RSI, Buy Score, or Role. It can only produce `WAIT FOR RECLAIM` or `AVOID`; entry-permission wording is impossible.

## 5. Role Modifier

Role is applied after technical facts:

- Core, Strategic, and Strategic-Income can use `STARTER ENTRY` for constructive support with Buy Score 4+ and positive momentum.
- Swing commonly resolves to `BUY WATCH` until timing is confirmed.
- Speculative and Options require stronger confirmation.
- No role can bypass hard structural damage.

## 6. Resistance Cap

Active resistance states are checked before constructive entry logic:

- `RESISTANCE_TEST`
- `APPROACHING_RESISTANCE`
- `BREAKOUT_WATCH` while current role remains resistance

These resolve to `WATCH / NO CHASE` until a completed breakout is confirmed, even with Buy Score 4-5.

## 7. DPRO.CN Regression

Synthetic regression input: failed breakout, positive momentum, Buy Score 5.

Result: `WAIT FOR RECLAIM`.

No Buy, Entry, Starter, Small Entry, or Accumulate wording is possible.

## 8. ATD.TO Regression

Synthetic regression input: oversold, declining momentum, tight rising-wedge breakdown, Buy Score 5.

Result: `AVOID`.

Oversold RSI and Buy Score do not override structural damage.

## 9. ATS.TO Regression

Synthetic regression input: RSI 29.7, oversold reversal setup, bullish shift, Buy Score 3.

Result: `REVERSAL WATCH`.

Early recovery is not promoted to Entry Candidate.

## 10. TSLA Regression

Synthetic regression input: active resistance test, accelerating momentum, Buy Score 5.

Result: `WATCH / NO CHASE`.

Resistance caps entry aggressiveness until breakout confirmation.

## 11. URA Regression

Synthetic regression input: support test, neutral momentum, Buy Score 2.

Result: `WATCH`.

A strong location alone does not create an entry signal.

## 12. PG Regression

Synthetic regression input: tight falling-wedge breakout, accelerating momentum, Buy Score 4, bullish MA structure.

Result: `ENTRY CANDIDATE`.

Confirmed constructive setups can still produce the strongest canonical entry status.

## 13. KHC Regression

Synthetic regression input: Strategic role, support test, positive momentum, Buy Score 4, bullish MA structure.

Result: `STARTER ENTRY`.

The role permits staged participation without weakening the technical hierarchy.

## 14. L.TO Regression

Synthetic regression input: tight falling wedge near breakout, neutral momentum, Buy Score 4.

Result: `BUY WATCH`.

Compression is treated as a setup, not confirmation.

## 15. GOOGL Regression

Synthetic regression input: support test, neutral momentum, Buy Score 3.

Result: `WATCH`.

Weak confirmation does not force an entry.

## 16. MRVL Regression

Synthetic regression input: tight rising-wedge breakdown plus nearby support test, neutral momentum, Buy Score 5.

Result: `WAIT FOR RECLAIM`.

The hard pattern event dominates the constructive nearby level.

## 17. Price Structure Wording

Human-readable labels changed only in the shared display layer:

- `TIGHT WEDGE SUPPORT BROKEN` becomes `TIGHT WEDGE BREAKDOWN`.
- `WEDGE SUPPORT BROKEN` becomes `WEDGE BREAKDOWN`.

Lifecycle-aware tooltip wording now uses current role. For example, an upper wedge resistance that has transitioned to support displays `Former Wedge Resistance — Now Support`.

Internal Price Structure enums remain unchanged.

## 18. Action Center Impact

### Persistent filter

The selected `All`, `Action Required`, `Developing`, or `Informational` filter is stored in localStorage under:

```text
dashboard_action_center_filter
```

It is restored when the Dashboard widget is created. Invalid stored values fall back to `ALL`. All filter buttons remain visible, including zero-count selections.

### Sorting

Holdings and Watchlist have independent sort state. Every visible column is sortable by clicking its header:

- Symbol
- Role
- RSI
- MA Structure
- Momentum
- Price Structure
- Allocation for Holdings
- Position Action or Entry Status

Clicking the same header reverses direction; selecting a different header starts ascending. Symbol is the deterministic tie-breaker. Null RSI sorts after real values in ascending order.

### Shared entry status

Holdings continue to show backend Position Action unchanged. Non-owned Action Center rows use the same `DecisionEngineService.translateForWatchlist()` result as Watchlist when the scanner row is available. Snapshot-only rows use the same pure canonical evaluator conservatively.

Action Severity and Priority Candidate remain separate concepts.

## 19. API and DTO Changes

None.

The implementation uses existing scanner and Action Center DTO fields. Diagnostics are exposed in the frontend `PageDecision.watchlistDiagnostics` result for regression testing and tooltips.

## 20. Database Migration

No migration or schema change was required. Filter persistence is browser-local, matching other Dashboard display settings.

## Diagnostics Added

`WatchlistEntryDecision` exposes:

- `finalAction`
- `finalActionReason`
- `hasHardStructuralNegative`
- `hardNegativeReason`
- `priceStructureState`
- `momentumState`
- `maStructure`
- `buyScore`
- `role`
- `entryBlockedByResistance`
- `entryBlockedByHardNegative`
- `entryBlockedByMomentum`
- `entryBlockedByRoleConfirmation`

## Automated Validation

### Focused frontend tests

```powershell
cd frontend\portfolio-manager-ui
npm run test -- --watch=false --include src/app/core/services/decision-engine.spec.ts --include src/app/features/dashboard/portfolio-actions-widget/portfolio-actions-widget.component.spec.ts --include src/app/core/price-structure-display.spec.ts
```

Result: 64/64 passed.

### Full backend regression

```powershell
dotnet test backend\PortfolioManager.Tests\PortfolioManager.Tests.csproj -c Release --nologo
```

Result: 164/164 passed.

### Angular build

```powershell
cd frontend\portfolio-manager-ui
npx ng build --configuration development
```

Result: passed.

### Full frontend suite

```powershell
cd frontend\portfolio-manager-ui
npm run test -- --watch=false
```

Result: 65 passed, 1 failed. The only failure is the pre-existing scaffold assertion in `app.spec.ts` expecting the removed default Angular heading.

### Static checks

- VS Code diagnostics: clean.
- `git diff --check`: clean.
- No migration/model-snapshot files changed.

## Manual Test Steps

### Action Center filter persistence

1. Open Dashboard and expand Action Center.
2. Select `Developing`.
3. Navigate away and return to Dashboard, or reload the page.
4. Confirm `Developing` remains selected and only Developing rows appear.
5. Repeat with All, Action Required, and Informational.
6. Optionally inspect localStorage and verify `dashboard_action_center_filter`.

### Action Center sorting

1. In Holdings, click Symbol and confirm ascending order.
2. Click Symbol again and confirm descending order.
3. Click RSI and confirm numeric ordering with missing values after real values when ascending.
4. Test MA Structure, Momentum, Price Structure, Allocation, and Position Action.
5. In Watchlist, repeat for all columns including Entry Status.
6. Confirm sorting one table does not change the other table's sort state.

### Canonical Watchlist vocabulary

1. Open Watchlist and review the Final Action column/cards.
2. Confirm every non-owned row uses only the ten canonical values listed above.
3. Confirm no row contains Hold, Add, Trim, Exit, Position Action, Accumulate, Small Entry, or Starter OK terminology.

### Hard-negative regressions

1. Refresh technical data.
2. For DPRO.CN, confirm an active Breakout Failed state yields Wait for Reclaim or Avoid.
3. For MRVL, confirm wedge breakdown dominates nearby support and yields Wait for Reclaim or Avoid.
4. For ATD.TO, confirm oversold RSI does not create Entry Candidate while structural damage remains.
5. Confirm Speculative role and high Buy Score never bypass these blocks.

### Constructive and resistance regressions

1. For TSLA at unconfirmed resistance, expect Watch / No Chase.
2. For L.TO near a tight-wedge breakout without confirmation, expect Buy Watch.
3. For ATS.TO early oversold recovery, expect Reversal Watch.
4. For confirmed PG-style wedge breakout with accelerating momentum and Buy Score 4+, expect Entry Candidate.
5. For a KHC-style Strategic support setup with positive momentum and Buy Score 4+, expect Starter Entry.
6. For URA/GOOGL weakly confirmed support setups, verify they are not forced into Entry Candidate.

### Lifecycle wording

1. Find a level whose original role differs from current role.
2. Hover Price Structure.
3. Confirm former wedge resistance now acting as support is described as `Former Wedge Resistance — Now Support`.
4. Confirm internal enum/type/state values remain visible in Technical Details and unchanged in API responses.
