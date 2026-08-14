# RSI 2-Stage Engine — Implementation Report

**Date:** 2026-08-14  
**Branch:** develop  
**Triggered by:** First real-world results from ATS.TO, CPH.TO, DR.TO

---

## What Was Built

This report describes all changes made to the RSI Scanner / EOD Signals 2-stage workflow.  
It also explains the rules in plain language so you can reason about future signals.

---

## The Architecture (Unchanged)

The 4-layer architecture was not changed:

| Layer | Purpose |
|---|---|
| **RSI Scanner** | Active dynamic setups — what you see right now |
| **StagedSignals** (DB table) | Short-term memory — tracks each setup day over day |
| **DailySignals** (DB table) | Permanent frozen snapshot — confirmed signals only |
| **EOD Signals** (dashboard page) | Display of DailySignals with live price updates |

---

## How the 2-Stage Engine Works

### Stage 1 — Entering the Engine

A stock enters the engine the moment its RSI crosses the extreme threshold (below 30 for Oversold, above 75 for Overbought).

On this first day:
- A `StagedSignal` record is created in the database
- The stock appears on the RSI Scanner
- Stage Status = **STAGED**
- No RSI delta exists yet (Day 1)

### Stage 2 — Day-Over-Day Tracking

On every subsequent day the stock is scanned:

1. The engine compares today's RSI to yesterday's RSI → **RSI Δ1D**
2. From RSI Δ1D it calculates **Trend Shift**
3. From Trend Shift it calculates **Stage Status**
4. If the trend shifted enough → it evaluates Price and Volume confirmation

### The 3 Stage Statuses

| Status | Meaning |
|---|---|
| **STAGED** | Day 1. No RSI delta yet. Engine is waiting for the next day. |
| **TRACKING** | RSI delta exists, but momentum has not yet reversed meaningfully. Can be Still Falling, Stabilizing, Still Rising. |
| **CONFIRMING** | RSI momentum has reversed (Bull Turn or Bear Turn). Engine is now evaluating price and volume confirmation. |

### The 5 Trend Shift States

| Trend Shift | When |
|---|---|
| Waiting | Day 1 (no delta) |
| 🟢 Bull Turn | Oversold setup, RSI Δ1D > +0.25 |
| 🔴 Still Falling | Oversold setup, RSI Δ1D < -0.25 |
| 🟡 Stabilizing | Either scan type, RSI Δ1D between -0.25 and +0.25 |
| 🟢 Bear Turn | Overbought setup, RSI Δ1D < -0.25 |
| 🔴 Still Rising | Overbought setup, RSI Δ1D > +0.25 |

---

## The Final Promotion Gate

**The most important rule:**

> Bull Turn or Bear Turn **alone does NOT promote a signal**.

A signal is only promoted to DailySignals (and displayed on EOD Signals) when **all three conditions are true**:

```
TrendShift  = Bull Turn  (or Bear Turn for overbought)
AND
VolumeSignal = Validated
AND
PriceConfirmation = true (EOD confirm rules passed: Price vs EMA9, Price vs Open, ATR position)
```

If any condition fails, the signal:
- Stays on RSI Scanner
- Remains `IsActiveWatch = 1`
- Stage Status stays **CONFIRMING**
- Is re-evaluated the next trading day

### The ATS.TO Fix

Before this update, ATS.TO was at risk of being promoted because the promotion gate only checked TrendShift, not volume. 

The fix: `EodSignalPersistenceService.SaveAsync()` now explicitly requires `VolumeSignal == "Validated"` before inserting into DailySignals.

**ATS.TO correct expected result:**
```
RSI         27.8
RSI Δ1D     +0.37
Trend Shift Bull Turn — Early
Volume      Low-Volume Trap
Status      CONFIRMING         ← correct
Promote     NO                 ← correct
```

---

## Turn Strength

Not all Bull Turns are equal. The velocity of the RSI reversal is now labeled.

**Thresholds (configurable in `ScannerRuntimeConfig`):**

| RSI Δ1D absolute value | Turn Strength |
|---|---|
| > 0.25 and < 1.0 | Early |
| ≥ 1.0 and < 5.0 | Normal |
| ≥ 5.0 and < 10.0 | Strong |
| ≥ 10.0 | Explosive |

**Display format in RSI Scanner:**

| Example | Display |
|---|---|
| ATS.TO, Δ +0.37 | `🟢 Bull Turn — Early` |
| CPH.TO, Δ +1.42 | `🟢 Bull Turn` (Normal has no suffix) |
| DR.TO, Δ +14.82 | `🟢 Bull Turn — Explosive` |

Turn Strength is a **label only**. It does not confirm or block a trade by itself.

---

## Chase Risk

When Turn Strength is **Explosive**, the engine flags **Chase Risk: Elevated**.

**Meaning:** A very large one-day RSI jump may indicate a V-bottom, but it can also mean the bulk of the rebound already happened before confirmation. The signal may still be valid — the flag is informational only.

If price and volume confirmation pass, the signal is still promoted. The EOD Signals page displays an additional indicator: `⚡ Chase Risk`.

**DR.TO example:**
```
Turn Strength    Explosive
Chase Risk       Elevated
Promoted         YES (if price + volume confirm)
EOD Signals      Shows ⚡ Chase Risk indicator
```

---

## Failed Bull Turn (Dynamic Reversion)

A Bull Turn can fail on a subsequent day.

**Example sequence:**

| Day | RSI | Δ1D | Trend Shift | Stage Status |
|---|---|---|---|---|
| Day 1 | 27.0 | — | Waiting | STAGED |
| Day 2 | 27.5 | +0.5 | 🟢 Bull Turn | CONFIRMING |
| Day 3 | 26.4 | -1.1 | 🔴 Still Falling | TRACKING |

On Day 3, the Bull Turn reverted to Still Falling. The signal goes back to TRACKING automatically. **No DailySignal was inserted.** The setup remains active and will be re-evaluated on Day 4.

This works naturally because StageStatus is always computed fresh from the current RsiDelta1D.

---

## RSI Can Leave the Oversold Zone Before Confirmation

The engine does **not** require current RSI to stay below 30 for an Oversold setup.

If a stock was staged with RSI = 27, and on Day 3 its RSI is 32, the setup is still valid. The `ScanType = Oversold` represents the **origin** of the setup, not a filter on current RSI.

This prevents valid confirmations from being discarded simply because the RSI recovered.

---

## Reversal Probability — Low-Volume Trap Cap

**New rule:** `ReversalProbability` can never be "High" when `VolumeSignal = Low-Volume Trap`.

The reversal probability scoring was updated in both `CalculateReversalProbability` and `CalculateReversalProbabilityEnhanced`. If the score would result in "High" but volume is a Low-Volume Trap, the result is capped at "Medium".

---

## Setup Expiration

A configurable maximum `MaxActiveTradingDays` (default: 7) is now enforced.

If a setup reaches 7 trading sessions without being promoted, it is automatically deactivated:
- `IsActiveWatch = 0`
- The staged record is preserved in the database for future backtesting
- Nothing is inserted into DailySignals (the setup simply expires)

The threshold is configurable via `ScannerRuntimeConfig`.

---

## SMA200 — Context, Not Confirmation

This rule did not change.

```
Price > SMA200  → Trend-Aligned
Price ≤ SMA200  → Counter-Trend
```

SMA200 classification is shown on both the RSI Scanner and EOD Signals pages as context. It does not block promotion.

The counter-trend classification materially changes interpretation:
- ATS.TO ~30% below SMA200 → Counter-Trend
- CPH.TO ~11% below SMA200 → Counter-Trend
- DR.TO ~7% below SMA200 → Counter-Trend

All three can still be valid reversals. The SMA200 label tells you whether you are trading with or against the long-term trend.

---

## Risk % Added to EOD Signals

The EOD Signals page now shows `Risk %` alongside `Risk / Share`:

```
Risk %  =  RiskPerShare / EntryPrice × 100
```

**Examples from first real signals:**

| Symbol | Risk/Share | Entry | Risk % |
|---|---|---|---|
| ATS.TO | $2.93 | $28.17 | 10.4% |
| CPH.TO | $1.84 | $14.11 | 13.0% |
| DR.TO | $1.42 | $15.43 | 9.2% |

This value is computed dynamically. No new database column was added.

**Color coding:**
- > 12% → Red (high risk per trade)
- 8–12% → Orange (moderate)
- ≤ 8% → Grey (normal)

---

## Post-Promotion Freeze

Once a signal is promoted and written to DailySignals, the following values are **frozen** at that exact moment:

- TrendShift
- RsiDelta1D
- EntryPrice
- StopLossPrice
- RiskPerShare
- Sma200

After promotion, the EOD Signals page dynamically calculates and updates:

- Current Price (fetched live)
- Price Diff
- Gain / Loss %
- Days Since Signal

These live updates describe what happened **after** confirmation. They do not overwrite the frozen confirmation snapshot.

---

## Files Changed

### Backend

| File | Change |
|---|---|
| `Services/ScannerRuntimeConfig.cs` | Added turn strength thresholds + MaxActiveTradingDays (configurable, persisted to JSON) |
| `Models/ScannerModels.cs` | Added `StageStatus`, `TurnStrength`, `ChaseRisk` to `RsiScanResult` |
| `Services/StagedSignalService.cs` | Computes StageStatus, TurnStrength, ChaseRisk; enforces setup expiration |
| `Services/EodSignalPersistenceService.cs` | Promotion gate now requires `VolumeSignal == "Validated"` (fixes ATS Low-Volume Trap) |
| `Services/RsiScannerService.cs` | ReversalProbability capped at Medium when VolumeSignal = Low-Volume Trap |
| `PortfolioManager.Api.csproj` | Added InternalsVisibleTo for test project |

### Backend Tests (new)

| File | Tests |
|---|---|
| `backend/PortfolioManager.Tests/StagedSignalEngineTests.cs` | 5 regression tests (all passing) |

### Frontend

| File | Change |
|---|---|
| `core/models/portfolio.models.ts` | Added `stageStatus`, `turnStrength`, `chaseRisk` to `RsiScanResult` |
| `features/scanner/rsi-scanner-table.component.ts` | Added `trendShiftDisplay()`, `stageStatusClass()` helpers |
| `features/scanner/rsi-scanner-table.component.html` | Trend Shift column shows TurnStrength suffix + StageStatus badge + Chase Risk badge |
| `features/scanner/rsi-scanner-table.component.scss` | Added styles for new badges |
| `features/eod-signals/eod-signals-page.component.ts` | Added `riskPercent()`, `turnStrength()`, `chaseRisk()` helpers; `riskPercent` sort column |
| `features/eod-signals/eod-signals-page.component.html` | Risk/Share column shows Risk %; standalone Risk % column; Trend Shift shows TurnStrength + Chase Risk |
| `features/eod-signals/eod-signals-page.component.scss` | Added styles for new elements |
| `core/services/grid-column.service.ts` | Added Risk % column to EOD Signals grid config |

---

## Regression Tests

5 automated tests are included in `backend/PortfolioManager.Tests/StagedSignalEngineTests.cs`.

| Test | Input | Expected |
|---|---|---|
| **Test 1 — ATS Low-Volume Trap** | Bull Turn = true, Volume = Low-Volume Trap | NO promotion, Status = CONFIRMING |
| **Test 2 — CPH Standard Reversal** | Bull Turn = true, Volume = Validated | Promotion = YES |
| **Test 3 — DR Explosive Reversal** | Bull Turn = true, Δ +14.82, Volume = Validated | Promotion = YES, TurnStrength = Explosive, ChaseRisk = Elevated |
| **Test 4 — Failed Bull Turn** | Day 2 Bull Turn, Day 3 delta negative | Day 3 = Still Falling, TRACKING, no promotion |
| **Test 5 — RSI Leaves Oversold** | Origin Oversold, current RSI > 30, Bull Turn, Validated | Promotion = YES |

Run: `cd backend/PortfolioManager.Tests && dotnet test --configuration Release`

---

## What Was NOT Changed

- Database schema — no new columns added
- The existing EOD Confirm rules (4-rule set for RSI/Price/Volume/ATR)
- SMA200 confirmation behavior
- Background service timing or scan interval
- Email notification logic
- The 50-symbol TSX watchlist

---

## Configurable Values

All tunable thresholds live in `ScannerRuntimeConfig` and persist to `scanner-eod-config.json`:

| Setting | Default | Purpose |
|---|---|---|
| `TurnStrengthEarlyMin` | 0.25 | Minimum RSI Δ1D for "Early" label |
| `TurnStrengthNormalMin` | 1.0 | Minimum RSI Δ1D for "Normal" label |
| `TurnStrengthStrongMin` | 5.0 | Minimum RSI Δ1D for "Strong" label |
| `TurnStrengthExplosiveMin` | 10.0 | Minimum RSI Δ1D for "Explosive" + Chase Risk |
| `MaxActiveTradingDays` | 7 | Sessions before a setup auto-expires |
