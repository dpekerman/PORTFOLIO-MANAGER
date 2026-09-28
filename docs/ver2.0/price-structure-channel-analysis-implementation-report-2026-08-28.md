# Price Structure / Channel Analysis Implementation Report

Date: 2026-08-28

## Summary

Implemented a first compact Price Structure / Channel Analysis layer using the existing two-year daily Yahoo Finance OHLCV request already used by the RSI scanner. No external market-data source or separate channel page was added.

The decision hierarchy remains:

1. Existing RSI and momentum establish the base technical state.
2. Channel state enriches only explicit channel setup states.
3. Ownership and role determine Portfolio versus Watchlist vocabulary.
4. Allocation remains the final gate for capital-deploying actions.

## Backend Changes

### Channel calculation

Added `ChannelAnalysisService` with:

- Five-bar pivot high and pivot low detection.
- 3M, 6M, 12M, and 24M lookback candidates.
- Rising lower and upper rail fitting.
- Parallel-slope validation.
- ATR-based rail distance checks.
- Independent-touch spacing of at least 10 trading sessions.
- Prior-touch bounce confirmation using a 1.5 ATR move within the following 20 sessions.
- Channel quality scoring from touch count, spacing, and rail parallelism.
- Minimum quality threshold of 70.
- Simple open-gap detection and fill checks.
- Yahoo chart timestamps for lower-rail touch dates.

The calculation returns the requested metrics:

- Direction and slope.
- Current lower and upper rails.
- Channel quality.
- Confirmed lower-touch count and last touch date.
- Distance to lower rail in percent and ATR.
- Nearest open gap above and below.
- Channel state.

The only channel states emitted are `NONE`, `CHANNEL_ACTIVE`, `THIRD_TOUCH_APPROACHING`, `THIRD_TOUCH_TEST`, and `CHANNEL_BROKEN`. Reversal and bounce states are supported in the action contract and mapping, but are intentionally dependent on the existing momentum confirmation rather than being generated from location alone.

### Scanner integration

`RsiScannerService` now runs channel analysis from the same OHLCV arrays and ATR(14) used for RSI, MACD, Bollinger, Fibonacci, and EOD calculations. This avoids an additional market-data request and preserves the existing Yahoo batching and throttling behavior.

### Persistence

Added `TechnicalChannel` as a latest-value aggregate keyed by `(Ticker, Timeframe)` with a unique EF index. Added `TechnicalChannelPersistenceService` to upsert the current channel result during the existing live scan flow.

Generated migration:

- `20260828201934_AddTechnicalChannels`

Apply it with the repository's normal database update command before using persisted channel records in a deployed database.

### Action Center precedence

Extended `PortfolioActionDto` and `PortfolioActionsService` with channel context. Explicit channel states map as follows:

| Context                                               | Result              |
| ----------------------------------------------------- | ------------------- |
| Watchlist + third-touch approaching                   | `WATCH CHANNEL`     |
| Watchlist + third-touch test + still falling          | `WAIT FOR REVERSAL` |
| Watchlist + third-touch test + stabilizing            | `REVERSAL WATCH`    |
| Watchlist + third-touch test + bull turn              | `BUY WATCH`         |
| Watchlist + bounce confirmed                          | `ENTRY CANDIDATE`   |
| Watchlist + channel broken                            | `AVOID`             |
| Core/Strategic holding + third-touch test + bull turn | `ADD CANDIDATE`     |
| Swing holding + third-touch test + bull turn          | `STAGED ADD / HOLD` |
| Core/Strategic holding + broken channel               | `TECHNICAL REVIEW`  |
| Swing holding + broken channel                        | `EXIT REVIEW`       |

Existing RSI/momentum behavior remains unchanged for `NONE` and `CHANNEL_ACTIVE`. Channel logic does not replace the existing technical engine globally.

When a channel action would deploy capital and the allocation status is `over`, the result is changed to an allocation-blocked holding/watchlist action instead of a BUY, ADD, or ENTRY recommendation.

## Frontend Changes

- Added channel state and metric types to `portfolio.models.ts`.
- Added an optional `CHANNEL` column to the existing Watchlist grid registry.
- Added relevant channel labels only for approaching, testing, developing, confirmed bounce, and broken states.
- Added tooltip details for direction, quality, touches, lower rail, rail distance, last touch, and open gap above.
- Added a compact Channel column to the existing Dashboard Action Center holdings and watchlist tables.
- No separate channel widget or scanner page was created.

## Validation

Successful checks:

- `dotnet build backend/PortfolioManager.Api/PortfolioManager.Api.csproj --no-restore`
- Existing focused backend tests: 37 passed, 0 failed.
- `npm run build --prefix frontend/portfolio-manager-ui`
- EF migration generation completed successfully.

Existing non-blocking warnings remain:

- Three pre-existing nullable-reference warnings in `YahooFinanceService.cs`.
- Existing Angular SCSS component budget warnings.

## Scope Notes

This implementation keeps channel storage simple and recalculates touch history from the daily OHLCV series. A separate `TechnicalChannelTouch` table was not added.

The current Dashboard action source is the persisted RSI scanner snapshot, so channel-aware actions are available for symbols represented in that snapshot's oversold/overbought chains. Extending the snapshot to retain neutral symbols would be the next step if Action Center coverage is required for every portfolio/watchlist symbol regardless of RSI state.

Open gap prices are informational context only and are not treated as guaranteed targets or automatic trade signals.

## Dashboard Integration Phase

Completed the next UI and integration phase without adding a new Dashboard widget or scanner page.

### Channel labels and diagnostics

- Renamed visible channel labels to `3rd Rail Approaching` and `3rd Rail Test`.
- Continued suppressing ordinary `CHANNEL_ACTIVE` states as `—`.
- Added confirmed lower-rail touch diagnostics to the channel result:
  - Touch number
  - Touch date
  - Projected rail price
  - Actual daily low
  - Bounce distance in ATR
  - Confirmation status
- Persisted touch diagnostics in `TechnicalChannels.TouchDetailsJson`.
- Added touch detail output to Watchlist, Portfolio Stocks, and Action Center tooltips.
- Preserved the lower-rail rules: daily low tolerance, 10-session spacing, 1.5 ATR excursion, consecutive-candle de-duplication, and meaningful bounce confirmation.
- Existing staged momentum now promotes a tested rail state to `REVERSAL_DEVELOPING` or `BOUNCE_CONFIRMED` when appropriate.

### Action Center source coverage

Action Center evaluation now starts from active portfolio holdings and `Active` Watchlist items instead of only symbols found in the RSI oversold/overbought chains. Closed portfolio rows and non-active Watchlist tiers are excluded.

RSI data and channel data are merged by ticker. A channel-only setup can therefore enter Action Center even when the symbol has no current RSI-chain row. Existing RSI/momentum behavior remains the base technical behavior, and allocation remains the final gate for capital-deploying actions.

### Dashboard summary filters

Action Center summary counters are now interactive:

- `Action Required`
- `Developing`
- `Informational`

Each counter filters both Holdings and Watchlist sections. Selecting the active counter again restores the unfiltered view. Counter values remain calculated from the complete Action Center dataset.

Market Signals rows now carry explicit `isNewToday`, `isActionRequired`, and shared severity metadata. `New Today` is derived from the active staged signal entering its current lifecycle today, rather than counting every EOD record created today. `Action Required` uses the shared action-severity mapping used by the dashboard action layer.

Market Signals counters are now interactive for:

- Oversold
- Overbought
- New Today
- Action Required

Cross-cutting filters preserve the correct Oversold or Overbought subsection and hide empty subsections. Matching rows display compact `NEW` and `REQ` indicators without adding new columns.

### Database migration

Generated and applied:

- `20260828220959_AddChannelTouchDiagnostics`

This adds `TouchDetailsJson` to the existing `TechnicalChannels` table.

### Validation for this phase

- Full backend test project: **86 passed, 0 failed**.
- Angular production build: **passed**.
- Database migration application: **completed successfully**.
- Remaining warnings are existing Angular SCSS budget warnings. The running API process also needs to be restarted to load the updated backend assembly.

## Final Completion Phase

Completed the remaining plan items for the current scope.

### Action Center

- Applied the shared `ActionSeverityMapper` to Action Center output.
- Preserved the final allocation gate for capital-deploying actions.
- Allowed material allocation-overweight conditions to qualify an active universe row even when no RSI-chain result exists.
- Kept active Portfolio holdings and `Active` Watchlist items as the supported universe.
- Added an explicit `All` reset control for Action Center severity filters.

### Market Signals

- Added context-aware footer behavior for filtered views.
- Category filters continue to preserve Oversold and Overbought subsections.
- Cross-cutting filters route users to EOD Signals rather than showing misleading category totals.

### Channel diagnostics

- Added persisted touch diagnostics to the existing `TechnicalChannels` table.
- Added touch-detail fields to Action Center responses and tooltips.
- Added focused unit coverage for shared severity behavior and touch diagnostic fields.

### Final validation

- Full backend test project: **96 passed, 0 failed**.
- Backend apphost-free build: **passed**.
- Angular production build: **passed**.
- Database migration `AddChannelTouchDiagnostics`: **applied successfully**.

The only remaining build output consists of existing SCSS budget warnings and three existing nullable-reference warnings in `YahooFinanceService.cs`. Restart the backend and frontend services before manual UI verification.

### Dashboard snapshot compatibility fix

Strengthened `DashboardStateService` compatibility handling so an older persisted snapshot is automatically rebuilt when either of these conditions is detected:

- Market Signal rows are missing `isNewToday` or `isActionRequired` metadata.
- Summary counter totals do not match the row-level metadata.

The compatibility refresh is guarded so it runs at most once per service instance. This prevents the `New Today` and `Action Required` counters from displaying values whose filtered result sets are empty after loading an older snapshot.

Final verification: Angular production build passed after this fix. Existing SCSS budget warnings remain unchanged.

## Validation Fix Pass

Completed the follow-up channel validation and Action Center presentation fixes.

### Mature channel state naming

Channel states now use `PriorConfirmedLowerTouches` together with EOD rail distance:

- Two prior touches plus 0.35–1.0 ATR above the rail: `3rd Rail Approaching`.
- Two prior touches within +/-0.35 ATR: `3rd Rail Test`.
- Three or more prior touches plus 0.35–1.0 ATR above the rail: `Lower Rail Approaching`.
- Three or more prior touches within +/-0.35 ATR: `Lower Rail Retest`.
- More than 0.5 ATR below the lower rail: `Channel Broken`.

The existing pivot, ATR tolerance, spacing, excursion, de-duplication, and bounce-confirmation calculations were retained.

### EOD and action behavior

- Channel state continues to be calculated from the completed daily candle values.
- Tooltips now identify the structural price as `EOD Close`.
- Overbought lower-rail setups no longer produce BUY or ENTRY actions; they remain contextual warnings handled by the existing overbought logic.
- Mature rail approach/retest states use the same momentum timing rules as third-touch states without forcing a buy from location alone.

### Tooltip and table layout

- Channel tooltips now have `RISING CHANNEL`, `CURRENT STRUCTURE`, `TOUCH HISTORY`, and `GAP` sections.
- Touch diagnostics show touch number, date, rail price, actual low, and bounce ATR.
- Tooltip line breaks are preserved by the global Material tooltip style.
- Action Center Holdings and Watchlist tables use fixed shared percentage tracks, aligning Symbol, Role, RSI, Trend, Fib Zone, and Channel columns.

### Tests and validation

- Added state resolver tests for two-touch approach/test, mature retest/approach, and broken-channel thresholds.
- Full backend tests: **101 passed, 0 failed**.
- Angular production build: **passed**.
- Existing warnings remain limited to the repository's SCSS budget warnings and three Yahoo Finance nullable-reference warnings.
