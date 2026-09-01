# Shared Price Structure Decision Levels Implementation Report

Date: 2026-08-31

## Summary

The shared Price Structure engine now represents two independent facts: the primary structural pattern and the technical decision level affecting the ticker now. It reuses the existing two-year OHLCV history and `TechnicalSnapshotService`; it adds no scanner, Yahoo request path, database table, persisted role-transition state, or ticker-specific production logic.

## 1. Shared Price Structure Fields

`PriceStructureResult` retains pattern and level diagnostics and adds `Symbol`, `PatternHorizon`, `PatternLookbackSessions`, `KeyLevelLow`, `KeyLevelHigh`, `DailyHigh`, `DailyLow`, `EodClose`, and `ChannelTouchDetails`.

`SharedTechnicalFacts` was added to portfolio/watchlist snapshot DTOs with nullable RSI, MA Structure, MA cross, Momentum, Price Structure, nullable Buy Score, and a shared calculation timestamp. Pattern and key level remain separate concepts.

## 2. Candidate-Level Logic

The common candidate builder evaluates meaningful swing highs/lows, Fib 38.2/50/61.8, EMA20, SMA50, SMA200, wedge rails, validated channel rails, nearest unfilled gap boundaries, and independent-source confluence. Swing candidates require a pivot followed by at least a one-ATR move away within 15 sessions. MA Structure remains a separate fact.

## 3. Proximity Thresholds

- Relevance: absolute distance at most `1.0 ATR`.
- Testing: Daily High/Low or close within `0.35 ATR`.
- Approaching: directional movement between `0.35` and `1.0 ATR`.

Relevance is applied before confluence and ranking. Ranking prioritizes ATR proximity, then state, quality, and a deterministic type tie-breaker. A farther confluence cannot replace a nearer active level.

## 4. Support and Resistance Roles

Roles are not assigned from current close versus level. Swing highs, upper rails, and gaps above originate as resistance; swing lows, lower rails, and gaps below originate as support. Fib and MA context derives from historical price location.

The engine replays completed EOD closes from formation through the penultimate bar. Resistance changes to support only after a close above `level + 0.25 ATR`; support changes to resistance only after a close below `level - 0.25 ATR`. The current bar uses the role in force before that bar, preventing premature same-bar role flips. No transition state is persisted.

## 5. Breakout and Breakdown Logic

- Breakout trigger: `resistance + 0.25 * ATR`.
- Breakdown trigger: `support - 0.25 * ATR`.
- Daily Low controls support interaction.
- Daily High controls resistance interaction.
- EOD Close confirms breaks; intraday pierces do not.

Hard negatives include `BREAKDOWN_CONFIRMED`, `SUPPORT_BROKEN`, `FAILED_BREAKOUT`, wedge `BREAKDOWN`, and `CHANNEL_BROKEN`.

## 6. Confluence

Confluence runs after relevance filtering and requires at least two independent source families. Multiple Fib levels count as one family, as do multiple moving averages. Sources and independent confluence count are returned. Confluence raises quality but cannot override proximity.

## 7. Tight Wedges

Tight windows of 15, 20, 30, and 40 sessions are evaluated independently from structural windows of 60, 126, and 250 sessions. Tight wedges require converging geometry, at least two independent touches per rail, spacing, one-ATR move-away, 40% contraction, quality of at least 70, and current price inside/interacting with the structure.

Maturity distinguishes `DEVELOPING`, `TIGHTENING`, and `NEAR_APEX`. EOD Close plus the quarter-ATR trigger confirms breakout/breakdown. Near apex alone is not a trade signal.

## 8. Channel/Wedge Regression Impact

The validated rising-channel algorithm and touch-quality rules remain. Exactly two prior confirmed lower touches retain third-touch terminology; 3+ use lower-rail retest terminology. Daily Low now controls approach/test while EOD Close controls `CHANNEL_BROKEN`.

Channel and key-level results are composed rather than mutually exclusive, so a valid channel can coexist with a nearer Fib, MA, swing, gap, or confluence decision level.

## 9. Action Center UI

Primary columns are now `RSI`, `MA Structure`, `Momentum`, and `Price Structure`. Holdings retain Allocation and Position Action; Watchlist retains Entry Status. Trend, Fib Zone, and Channel were removed as primary columns. Their details remain in the Price Structure tooltip.

Unavailable RSI and Momentum display `—`, not `0.0` or a synthetic `Waiting` state.

## 10. Action Center Shared Integration

Action Center merges real scanner facts with persisted portfolio/watchlist `SharedTechnicalFacts`. Holdings remain separate from Active Watchlist entries, and unowned securities cannot receive HOLD/TRIM/EXIT actions. Active watchlist securities with meaningful current structure can enter as developing setups even with neutral RSI status.

Inclusion diagnostics include `PRICE_STRUCTURE_SUPPORT_TEST`, `PRICE_STRUCTURE_BREAKOUT_WATCH`, `PRIORITY_TECHNICAL_SETUP`, `HARD_STRUCTURE_NEGATIVE`, `RSI_SIGNAL`, and `NO_CURRENT_DECISION_LEVEL`. Price Structure adjusts narrowly scoped inclusion/severity behavior; the global role-adjusted Final Action engine was not rewritten.

## 11. Priority Candidate Integration

The existing 30-point technical component receives bounded modifiers:

- support reclaim or confirmed breakout: `+6`;
- support/resistance test: `+4`;
- approaching support/resistance: `+2`;
- tight falling wedge near apex: `+2`;
- positive/accelerating Momentum: `+2`;
- declining Momentum: `-4`;
- hard structural negative: technical score becomes `0`.

The score remains capped at 30. `HIGH_PRIORITY` requires a technical score of at least 10, so Price Structure alone cannot promote a candidate. Buy Score remains nullable because no backend Buy Score source exists; no synthetic value was introduced.

## 12. Database/Schema Changes

None. Snapshot JSON gains additive fields, but there is no EF migration, relational change, or transition-state table.

## 13. API/DTO Changes

- Portfolio/Watchlist summaries add optional `TechnicalFacts`.
- Scanner rows add nullable `MaStructure`, `MaCrossState`, and `MomentumState`.
- Action Center RSI is nullable and adds MA Structure, Momentum, Price Structure, reasons, and technical timestamp.
- TypeScript interfaces mirror the backend.

RSI Scanner now routes its already-fetched OHLCV candles through `TechnicalSnapshotService.FromHistory`, removing its direct Price Structure calculation without another Yahoo call.

## 14. Shared Frontend Presentation

`price-structure-display.ts` is the common source for labels, diagnostic tooltips, monetary masking, and sorting. Portfolio, Watchlist, RSI Scanner, Market Leadership, and Action Center use it.

Tooltips cover pattern/horizon/quality, rails, contraction/apex/touches, key-level type/role/state/zone, distances, Daily High/Low, EOD Close, ATR, triggers, sources, confluence, and channel touch history.

## 15. Tests and Validation

Deterministic tests cover the one-ATR gate, Daily High/Low interaction, EOD confirmation, historical role transitions, no same-bar flip, TSLA-like near-Fib selection, TTD-like tight-wedge geometry, MRVL-like EOD breakdown, GDX-like channels, URA-like `NONE`, ordinary false-positive control, bounded scoring, and hard negatives. Ticker names are test context only; production has no ticker branches and tests do not call Yahoo.

### Backend

```text
dotnet test backend\PortfolioManager.Tests\PortfolioManager.Tests.csproj -c Release --nologo
Total: 164, Passed: 164, Failed: 0, Skipped: 0
```

Release was used because the running development backend held the Debug executable.

### Frontend Build

```text
npx ng build --configuration development
Application bundle generation complete.
```

### Frontend Tests

```text
npm run test -- --watch=false
Tests: 1 passed, 1 failed
```

The failure is the pre-existing scaffold assertion in `app.spec.ts` expecting an `h1` containing `Hello, portfolio-manager-ui`; the routed application does not render that scaffold heading. The implementation build and compiler checks pass.

### Diagnostics

VS Code reports no errors across backend, tests, or frontend source.

## Assumptions

- The latest supplied candle is the completed EOD candle used for confirmation.
- Additive JSON fields are backward compatible and populate on normal refresh.
- Existing legacy Fib fields remain unchanged; Price Structure consumes equivalent levels without replacing the Fib feature.
- Existing RSI, Momentum, MA Structure, Buy Score, Value, EOD Signals, Allocation, and global role-adjusted Final Action formulas remain unchanged except for the shared context described above.

## Focused Regression Fixes

### 1. Display Priority

Compact Price Structure display now ranks the strongest event across the independent pattern and key-level facts. Hard structural negatives rank first, confirmed constructive events second, active level interactions third, and approaching/developing events fourth. Candidate-level ATR proximity ranking remains unchanged.

### 2. MRVL

An MRVL-style `TIGHT_RISING_WEDGE / BREAKDOWN` plus `CHANNEL_RAIL / SUPPORT_TEST` result displays `Tight Rising Wedge Breakdown`. The tooltip retains `Testing Channel Support @ 208.07`. The explicit hard-negative flag remains true and Priority technical scoring remains zero despite the constructive nearby support test.

### 3. Support Reclaim Role

Price Structure now exposes `KeyLevelOriginalRole` and derives the current `KeyLevelRole` after the current completed EOD transition. `SUPPORT_RECLAIM` and confirmed breakout expose current role `SUPPORT`; confirmed support failure exposes current role `RESISTANCE`. No transition state is persisted.

### 4. TSLA

The existing `<= 1.0 ATR` relevance gate, `<= 0.35 ATR` test interaction, proximity-first current-level selection, and confluence behavior are unchanged. The distant support regression remains covered. A reclaim from original resistance now reports current support without discarding the original role.

### 5. Channel Tooltip and GDX

Channel tooltips show confirmed lower-rail touch count and channel touch history. Wedge-only independent upper/lower touch counters are omitted for channels. Existing rising-channel, third-touch, and ATR approach semantics are unchanged.

### 6. Trigger Labels and URA

Support-oriented details label the numeric thresholds `Hold / Confirmation Trigger` and `Breakdown Trigger`. Resistance-oriented details use `Breakout Trigger` and `Failure / Rejection Trigger`. Underlying formulas and URA EMA20/Fib confluence selection are unchanged.

### 7. Action Center and DTO Impact

Action Center receives the corrected display automatically through the shared formatter. `HasHardStructuralNegative` is derived on `PriceStructureResult` and is consumed by Action Center severity and Priority scoring even when the current key level is constructive. TypeScript mirrors `hasHardStructuralNegative` and `keyLevelOriginalRole`. Final Action rules were not globally changed.

### 8. Database

No database migration or schema change was required.
