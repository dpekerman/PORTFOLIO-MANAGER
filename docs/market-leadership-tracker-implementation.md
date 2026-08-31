# Market Leadership Tracker Implementation

## Scope

Market Leadership is now a per-user market/theme radar on the Dashboard. It tracks user-selected Yahoo Finance symbols and does not create Buy, Sell, RSI, Channel, or Final Action recommendations. MARKET INDICES remains unchanged.

## Implemented

- Per-user `MarketLeadershipTrackers` persistence with symbol, display name, tracker type, sort order, active status, and creation time.
- Database migration `20260830155509_AddMarketLeadershipTrackers`, applied to the configured database.
- Yahoo Finance symbol validation and automatic name population when a custom display name is not supplied.
- Add action in the Market Leadership card header with a compact dialog for symbol, display name, and tracker type.
- Small hover/focus remove action for each user tracker.
- Compact signal counters that filter rows while retaining totals from the unfiltered result set.
- Desktop columns: Name/Symbol, Price, Day, 5D, 20D, MA Structure, Momentum, and Signal.
- Responsive layout: Day is removed at tablet width; mobile retains Name/Symbol, 20D, MA Structure, and Signal, with an accessible expander for Price, Day, 5D, and Momentum.
- Positive, negative, and near-flat return formatting, plus subtle row emphasis for Emerging, Leading, Cooling, and Weak states.
- Dated SMA50/SMA200 crossover detection, including Golden Cross, Death Cross, 20-trading-day recency, and directional 2% near-cross detection.
- MA, Momentum, and Signal tooltips with calculated values and deterministic explanations.

## Calculation Rules

Yahoo provides two years of daily closing history. Calculations use valid trading-day closes only.

- Current 5D return: $C[N-1] / C[N-6] - 1$.
- Previous 5D return: $C[N-6] / C[N-11] - 1$.
- Current 20D return: $C[N-1] / C[N-21] - 1$.
- Previous 20D return: $C[N-21] / C[N-41] - 1$.
- Accelerating momentum requires positive 5D return and both current 5D and current 20D returns to exceed their immediately preceding non-overlapping periods.
- Emerging permits a negative current 20D return when it is improving. Leading requires a positive current 20D return.
- At least 200 valid closes are required for SMA200-based classification. Unavailable technical data is shown without fabricated values.

## Manual Test Procedure

1. Start the backend:

```powershell
Set-Location backend\PortfolioManager.Api
dotnet run --launch-profile http
```

2. Start the frontend:

```powershell
Set-Location frontend\portfolio-manager-ui
npx ng serve
```

3. Sign in and open Dashboard. Expand Market Leadership.
4. Select `Add`, enter `URA`, leave Display Name empty, choose `Theme`, then select `Add`. Confirm that Yahoo resolves the name and a row appears immediately.
5. Add `GC=F` as `Commodity` and use a custom display name such as `Gold`. Confirm the custom name is shown above the ticker.
6. Attempt to add `URA` again. Confirm the dialog reports that the symbol is already tracked.
7. Attempt an invalid symbol. Confirm the dialog reports that Yahoo Finance could not load it.
8. Hover or keyboard-focus a row and select the close icon. Confirm the row disappears and does not reappear after dashboard refresh.
9. Select each signal counter. Confirm it filters matching rows and selecting it again clears the filter; totals remain unchanged.
10. Hover MA, Momentum, and Signal badges. Confirm their tooltips contain technical values and calculated rationale.
11. Resize to tablet and mobile widths. Confirm the required compact columns are shown and the row expander reveals the hidden metrics.
12. Confirm MARKET INDICES, Watchlist, RSI Scanner, Momentum, Channel, and Final Action workflows are unchanged.

## Automated Verification

The following commands passed after implementation:

```powershell
Set-Location backend\PortfolioManager.Tests
dotnet test

Set-Location frontend\portfolio-manager-ui
npm run build
```

Backend result: 104 tests passed. The Angular production build completed without errors. It retains existing stylesheet-size budget warnings outside this feature.

## 2026-08-31 Corrections and Price Structure Enhancement

### Classification Corrections

- Momentum now classifies positive 20D with negative or deteriorating 5D as `Weakening`, before `Positive` can be considered.
- `Positive` now requires both current 5D and current 20D returns to be positive.
- MA Structure now emits only ordered relationships between Price, SMA50, and SMA200. Ambiguous labels such as `P > 50 < 200` are not emitted.
- Leadership Signals use the corrected momentum state. `Cooling` requires a constructive trend and `Weakening` momentum. `Weak` requires both `Declining` momentum and weak/bearish structure.
- The header includes five unfiltered, clickable totals: Emerging, Leading, Cooling, Neutral, and Weak.

### Price Structure

- Yahoo daily history now carries aligned OHLCV values for shared technical analysis.
- The existing `ChannelAnalysisService` now supplies quality-gated Falling Wedge and Rising Wedge analysis. No new scanner, benchmark, breadth calculation, or stock-action behavior was added.
- Wedges require at least two pivot highs and lows, correctly directed converging rails, at least 30% actual rail-width contraction, a future apex, and quality at least 70.
- Projected wedge apex dates advance by trading days, excluding weekends.
- Falling Wedge breakout and Rising Wedge breakdown use a 0.25 ATR rail threshold. Falling Wedge breakout additionally requires price above EMA9 and Positive or Accelerating momentum.
- When no quality-gated wedge applies, Market Leadership maps the existing shared channel states to `3rd Rail Approaching`, `3rd Rail Test`, `Lower Rail Retest`, `Bounce Confirmed`, or `Channel Broken`; otherwise it displays `—`.
- Market Leadership shows a `Price Structure` column after Momentum. Its tooltip exposes the pattern, quality, rail values, contraction, apex, pivot counts, ATR, momentum, and Volume Ratio 20. `—` means no meaningful quality-gated structure is available.
- Price Structure provides context to the Market Leadership signal tooltip only. It does not independently create a Buy or Sell recommendation.

### Sorting and Filtering

- All Market Leadership data columns are sortable by clicking their header.
- Numeric columns sort numerically. MA Structure, Momentum, Price Structure, and Signal use deterministic attention ranks.
- Signal filters are applied before sorting. Header totals remain based on the complete unfiltered tracker set.
- With no explicit column selected, the server keeps the default attention order: Emerging, Leading, Cooling, Neutral, Weak, then 20D descending.

### Additional Automated Verification

```powershell
Set-Location backend\PortfolioManager.Tests
dotnet test

Set-Location frontend\portfolio-manager-ui
npm run build
```

Result on 2026-08-31: 113 backend tests passed and the Angular production build completed without errors. Existing stylesheet-size budget warnings in unrelated feature stylesheets remain unchanged.
