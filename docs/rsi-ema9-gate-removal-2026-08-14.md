# RSI 2-Stage Engine: EMA9 Gate Removal & Cleanup — 2026-08-14

## Summary

Removes EMA9 price confirmation as a mandatory Stage-2 promotion gate and replaces it with the existing EOD ATR structural price rule. EMA9 becomes supporting context only. Legacy UI columns are hidden by default. Volume display corrected to three tiers.

---

## 1. `EodSignalPersistenceService.cs` — Promotion gate

**Old gate:** `BullBearTurnPassed AND (Price > EMA9) AND (Volume >= 1.5x)`

**New gate:** `BullBearTurnPassed AND EodPriceConfirmationPassed AND (Volume >= 1.5x)`

| Helper                | Logic                                                                         | Role                |
| --------------------- | ----------------------------------------------------------------------------- | ------------------- |
| `IsEodPriceConfirmed` | Oversold: `close > open AND close >= DayHigh − 0.25×ATR`; Overbought: inverse | **Required**        |
| `IsEma9Confirmed`     | `Price > EMA9` (oversold) / `Price < EMA9` (overbought)                       | **Supporting only** |

**Diagnostic log format changed:**

```
[Stage2Gate] DR.TO Oversold | Turn=True | EodPrice=True | Volume=True (2.79x) | EMA9=False | Promoted=True
```

---

## 2. `RsiScannerService.cs` — VolumeSignal tiers

- `>= 1.5x` → `"Validated"` (Stage-2 gate threshold)
- `1.3x–1.49x` → `"Elevated"` (new — above display threshold, below promotion gate)
- `< 0.8x` → `"Low-Volume Trap"`
- Otherwise → `"Neutral"`

---

## 3. `EmailNotificationService.cs` — Email sections

Confirmed section: added EOD Price row (✓ Passed), EMA9 (Supporting) row (✓ Confirmed / ⏳ Pending), volume now shows ratio.

Awaiting section: "Price Confirmation" (EMA9) → "EOD Price (Required)" (ATR structural). EMA9 added as "EMA9 (Supporting)" row.

---

## 4. `grid-column.service.ts` — Scanner defaults

Added `defaultHidden?: boolean` to `ColumnDef`. Columns hidden by default for new users:

- `status` → renamed `Legacy Signal`
- `baseAction` → renamed `Legacy Action`
- `trendSetup` → `Decision Trend` (hidden)

`momentumShift` (Trend Shift) moved to primary visible position.

---

## 5. `rsi-scanner-table.component.ts` — Frontend

- `volSignalClass`/`volSignalIcon`: added `"Elevated"` tier handling
- `exportToExcel`: columns updated per diagnostic spec (EodPriceConfirmationPassed, Ema9Confirmed, PromotionReady, BlockingReason, etc.)

---

## 6. `rsi-scanner-table.component.scss`

Added `ind-elevated` CSS class (amber).

---

## 7. `EodPromotionGateTests.cs` — 86 tests, all passing

Helpers: `EodPriceConfirmed` (ATR structural), `Ema9Confirmed` (supporting), `WouldPromote` (no EMA9 param).

Key new tests:

- **TestA**: DR.TO EMA9 pending + all required gates pass → **Promoted = TRUE**
- **TestB**: EMA9 reclaimed + EOD candle fails → **Promoted = FALSE**
- **TestC**: Low volume → **Promoted = FALSE**
- **TestD**: No Bull Turn → **Promoted = FALSE**
- **Ema9Independence**: EMA9 state has no effect on gate → both scenarios promote

Old EMA9-as-gate tests removed/replaced.
