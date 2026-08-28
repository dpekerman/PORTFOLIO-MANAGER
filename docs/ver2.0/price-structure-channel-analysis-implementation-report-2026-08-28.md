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
