# RSI 2-Stage EOD Email — Implementation Report

**Date:** August 14, 2026  
**Branch:** develop

---

## Summary

Updated the RSI EOD email notification system to correctly reflect the 2-stage signal promotion model. The old email used the scanner's legacy `EodConfirm`/`Confirmed` status to determine what was "confirmed", which could incorrectly label a staged `Bull Turn` as `CONFIRMED` even when Price and Volume confirmation had not passed.

---

## Root Cause

The previous flow sent the email **before** the Stage-2 promotion gate ran:

```
❌ Old flow:
  notifier.NotifyNewEodConfirmedSignalsAsync(result)   ← email sent from scanner status
  eodPersistence.SaveAsync(allQualified, ct)            ← gate applied here (too late)
```

`NotifyNewEodConfirmedSignalsAsync` queried `SignalStatus.EodConfirm || Confirmed` from the scanner, bypassing the `VolumeSignal == "Validated"` gate entirely. DVA (RSI=26.9, Vol=0.3×, Price<EMA9) would appear as `CONFIRMED` in the email even though it failed both Stage-2 gates.

---

## Changes Made

### 1. `EodSignalPersistenceService.cs`

- **Added Price Confirmation gate** to the Stage-2 promotion filter:
  - Oversold: `CurrentPrice > Ema9Price`
  - Overbought: `CurrentPrice < Ema9Price`
- **Changed return type** from `Task` to `Task<List<RsiScanResult>>`, returning the list of signals actually promoted to `DailySignals`.

```csharp
// Before (volume only):
&& r.VolumeSignal == "Validated"

// After (price + volume):
&& r.VolumeSignal == "Validated"
&& IsPriceConfirmed(r)
```

### 2. `SignalNotificationTracker.cs`

Added `HasNewEodActivity(promoted, awaiting)` method that tracks:

- `PROMO|SYMBOL|ScanType` keys for confirmed signals
- `AWAIT|SYMBOL|ScanType` keys for awaiting signals

Returns `true` only when a signal has not yet been reported in the current EOD window, preventing duplicate emails on repeated scan cycles.

### 3. `RsiAlertBackgroundService.cs`

Restructured the EOD window block:

```
✅ New flow:
  SaveAsync(allQualified)         ← Stage-2 gate applied first
  ↓ returns promoted list
  awaiting = bullBearTurns not in promoted
  NotifyEodReportAsync(promoted, awaiting, scannedAt)   ← email reflects reality
```

- Collects all `Bull Turn` / `Bear Turn` signals (not just EodConfirm/Confirmed)
- Calls `SaveAsync` first, gets back only the signals that actually passed all three gates
- Builds `awaiting` from Bull/Bear Turn signals that were NOT promoted
- Calls `NotifyEodReportAsync` with both lists

### 4. `EmailNotificationService.cs`

Added `NotifyEodReportAsync(confirmed, awaiting, scannedAt)`:

- **New subject format:** `📊 RSI 2-Stage — N Confirmed: SYMBOL1, SYMBOL2`
- **Timezone:** All user-facing times displayed in Eastern Time (e.g. `Friday, August 14, 2026 — 3:38 PM ET`)
- **Section 1 — ✅ CONFIRMED & PROMOTED:** Only signals returned by `SaveAsync` (i.e., actually inserted into `DailySignals`)
  - Fields: RSI, RSI Δ1D with direction, Trend Shift + Turn Strength label, Entry, Stop, Risk/Share + Risk%, Volume (Validated pill), SMA200, Trend Setup (Counter-Trend / Trend-Aligned)
  - Chase Risk warning banner for Explosive reversals
- **Section 2 — ⏳ REVERSALS AWAITING CONFIRMATION:** Bull/Bear Turn signals that did NOT pass the Stage-2 gate
  - Fields: RSI, Trend Shift, Price Confirmation (✅/❌ with EMA9 value), Volume (pill with ratio)
  - Status: `CONFIRMING`
  - Action: `Continue Monitoring`

Removed from the email body:

- Raw ATR value
- MACD Histogram value / Delta
- Candle position %
- EMA raw calculations

Updated the legacy `BuildEodHtmlBody` (used by `NotifyNewEodConfirmedSignalsAsync`) to replace the old title and intro text per requirements #4 and #5.

Added helper methods:

- `FormatEasternTime(DateTime utc)` — formats UTC to `"dddd, MMMM d, yyyy — h:mm tt ET"` in Eastern Time
- `GetEasternTz()` — resolves `TimeZoneInfo` for Eastern Time (Windows/Linux compatible)

### 5. `StagedSignalEngineTests.cs`

- Updated `WouldPromote` helper to add `bool priceConfirmed = true` parameter (backward compatible with existing tests)
- Added **Test 6 — DVA Regression Test** per requirement #20:

```
Input:
  Ticker=DVA, RSI=26.9, TrendShift=Bull Turn
  Price=$180.82, EMA9=$190.24, VolumeRatio=0.3

Expected (verified by test):
  PriceConfirmation = false  (180.82 < 190.24)
  VolumeConfirmation = false (Low-Volume Trap)
  StageStatus = CONFIRMING
  WouldPromote = false       ← DailySignals insert BLOCKED
```

---

## Architecture Enforcement

The email is now a pure **reporter**, not a signal engine:

```
RSI Engine        → detects RSI extremes + TrendShift
StagedSignals     → tracks active setups
Stage-2 Gate      → EodSignalPersistenceService.SaveAsync()
                   requires: TrendShift + PriceConfirmation + VolumeConfirmation
DailySignals      → stores only promoted signals
EOD Email         → reports what is in DailySignals (confirmed) + StagedSignals (awaiting)
```

No Stage-2 decision logic exists inside `EmailNotificationService`. The email cannot disagree with RSI Scanner or EOD Signals page.

---

## Terminology Used in New Email

| Term           | Meaning                                                      |
| -------------- | ------------------------------------------------------------ |
| **Bull Turn**  | RSI momentum reversed (RsiDelta1D > threshold)               |
| **Confirming** | Reversal detected, waiting for price/volume                  |
| **Confirmed**  | RSI reversal + price + volume passed Stage-2 gate            |
| **Promoted**   | Signal inserted into `dbo.DailySignals`, `IsActiveWatch = 0` |

---

## Turn Strength Classification

| RSI Δ1D          | Label                                      |
| ---------------- | ------------------------------------------ |
| ≥ 0.25 and < 1.0 | Early                                      |
| ≥ 1.0 and < 5.0  | Normal (default, no suffix shown in email) |
| ≥ 5.0 and < 10.0 | Strong                                     |
| ≥ 10.0           | Explosive ⚠ Elevated Chase Risk warning    |

Overbought signals use the absolute magnitude of the negative delta.

---

## Files Changed

| File                                      | Change                                            |
| ----------------------------------------- | ------------------------------------------------- |
| `Services/EodSignalPersistenceService.cs` | Stage-2 gate + price confirmation + return type   |
| `Services/SignalNotificationTracker.cs`   | `HasNewEodActivity()` method                      |
| `Services/RsiAlertBackgroundService.cs`   | EOD flow restructured                             |
| `Services/EmailNotificationService.cs`    | New `NotifyEodReportAsync`, ET timezone, new HTML |
| `Tests/StagedSignalEngineTests.cs`        | DVA regression test, updated `WouldPromote`       |
