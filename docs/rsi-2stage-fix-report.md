# RSI 2-Stage Engine Fix Report

**Date:** 2026-08-14  
**Status:** Implemented

---

## Problem Summary

The RSI Scanner contained legacy single-day classification logic (`ClassifyOversold`, `ClassifyOversoldEnhanced`) that could produce `SignalStatus.Confirmed` based on single-candle patterns (e.g., close above prior day's high on 1.3× volume). This legacy `Status` field was being used as the **gating filter** before any signal reached the Stage-2 engine, causing two categories of failure:

1. **False promotions were possible** — A signal with `Status=Confirmed` (legacy) that also had a Bull Turn, Price > EMA9, and VolumeRatio between 1.30× and 1.49× could be promoted, even though the Stage-2 volume threshold requires ≥ 1.5×.

2. **Valid Stage-2 signals were blocked** — A staged signal with TrendShift = Bull Turn, Price > EMA9, and VolumeRatio ≥ 1.5×, but `Status = EarlyWarning` or `Status = Neutral` (RSI recovered above threshold), was **never sent to the Stage-2 gate** and therefore could never be promoted.

---

## Aug 14 Regression Failures

All five stocks had `Status = Confirmed` (legacy) but should **not** have been promoted:

| Symbol | Price   | EMA9    | Volume | PricePass | VolumePass | Expected     |
| ------ | ------- | ------- | ------ | --------- | ---------- | ------------ |
| ENB.TO | $70.61  | $72.53  | 1.40×  | FALSE     | FALSE      | NOT PROMOTED |
| ATS.TO | $28.16  | $30.40  | 0.81×  | FALSE     | FALSE      | NOT PROMOTED |
| DR.TO  | $15.37  | $15.67  | 2.79×  | FALSE     | TRUE       | NOT PROMOTED |
| CPH.TO | $14.14  | $15.73  | 2.04×  | FALSE     | TRUE       | NOT PROMOTED |
| DVA    | $180.06 | $190.00 | 0.36×  | FALSE     | FALSE      | NOT PROMOTED |

**Root cause for ENB.TO appearing as "Confirmed":** The enhanced classifier marks a candle as `Confirmed` when it closes in the upper half of the range with volume ≥ 1.3× OR MACD histogram rising. ENB.TO at 1.40× satisfied the display threshold (1.3×). The Stage-2 gate price check (Price > EMA9) correctly blocked it, but the display and email wording were misleading.

---

## Changes Implemented

### 1. `EodSignalPersistenceService.cs` — Volume Threshold Fix + Diagnostics

**Before:**

```csharp
var confirmed = resultList
    .Where(r => r.RsiDelta1D.HasValue
        && (r.TrendShift == "🟢 Bull Turn" || r.TrendShift == "🟢 Bear Turn")
        && r.VolumeSignal == "Validated"   // ← 1.3× display threshold
        && IsPriceConfirmed(r))
    .ToList();
```

**After:**

```csharp
foreach (var r in resultList)
{
    bool bullBearTurnPassed = r.RsiDelta1D.HasValue
        && (r.TrendShift.Contains("Bull Turn") || r.TrendShift.Contains("Bear Turn"));
    bool priceConfirmationPassed = IsPriceConfirmed(r);
    bool volumeConfirmationPassed = r.VolumeRatio >= 1.5m;  // ← correct Stage-2 threshold
    bool promoted = bullBearTurnPassed && priceConfirmationPassed && volumeConfirmationPassed;

    _logger.LogInformation(
        "[Stage2Gate] {Symbol} {ScanType} | BullBearTurn={Turn} | Price={Price} | Volume={Vol} ({Ratio:F2}x) | Promoted={Promoted}",
        r.Symbol, r.ScanType, bullBearTurnPassed, priceConfirmationPassed,
        volumeConfirmationPassed, r.VolumeRatio, promoted);

    if (promoted) confirmed.Add(r);
}
```

**Effect:** Volume confirmation now correctly requires ≥ 1.5× (not ≥ 1.3×). Each Stage-2 candidate logs its four boolean gates for debugging.

---

### 2. `RsiAlertBackgroundService.cs` — Remove Legacy Status Filter

**Before:**

```csharp
// Only signals with legacy Confirmed/EodConfirm status were considered
var allQualified = allResults
    .Where(r => r.Status == SignalStatus.EodConfirm || r.Status == SignalStatus.Confirmed)
    .ToList();
// ...
var promoted = await eodPersistence.SaveAsync(allQualified, ct);
```

**After:**

```csharp
// All signals with a Bull/Bear Turn are candidates — legacy Status is ignored
var bullBearTurns = allResults
    .Where(r => r.TrendShift.Contains("Bull Turn") || r.TrendShift.Contains("Bear Turn"))
    .ToList();

if (bullBearTurns.Count > 0)
{
    // Stage-2 gate inside SaveAsync decides (TrendShift + Price + Volume >= 1.5x)
    var promoted = await eodPersistence.SaveAsync(bullBearTurns, ct);
    // ...
}
```

**Effect:** The Stage-2 engine is now the **sole** promotion path. Signals with `Status = EarlyWarning` or `Status = Neutral` (RSI recovered) that have a Bull/Bear Turn are now correctly evaluated.

---

### 3. `EodSignalsController.cs` — PersistNow Filter Fix

Same fix as #2 applied to the manual `POST /api/eod-signals/persist-now` endpoint. Now uses TrendShift filter instead of legacy Status filter.

---

### 4. `EmailNotificationService.cs` — Awaiting Section Volume Display Fix

**Before:**

```csharp
var volPill = r.VolumeSignal == "Validated"   // "Validated" at 1.3x
    ? "✓ Validated"
    : r.VolumeSignal == "Low-Volume Trap"
        ? $"⚠ {r.VolumeRatio:F1}x — Low-Volume Trap"
        : $"{r.VolumeRatio:F1}x";
```

**After:**

```csharp
// Stage-2 volume threshold is 1.5x — display pass/fail against that threshold
var volPill = r.VolumeRatio >= 1.5m
    ? $"✓ {r.VolumeRatio:F2}x — Validated"
    : r.VolumeRatio < 0.8m
        ? $"⚠ {r.VolumeRatio:F2}x — Low-Volume Trap"
        : $"❌ {r.VolumeRatio:F2}x — Below 1.5x";
```

**Effect:** The email's "Awaiting Confirmation" section now correctly shows ENB.TO at 1.40× as **❌ 1.40x — Below 1.5x** instead of "✓ Validated".

---

### 5. `EodPromotionGateTests.cs` — Tests Updated + Aug 14 Regression Suite Added

- `WouldPromote()` helper updated from `volumeSignal == "Validated"` to `volumeRatio >= 1.5m`
- New test: `Oversold_BullTurn_Volume_1_4x_BelowThreshold_ShouldBlock` — verifies 1.40× is blocked
- New Aug 14 regression tests for ENB.TO, ATS.TO, DR.TO, CPH.TO, DVA
- New aggregate test: `Aug14_ZeroPromotions_Expected_WhenNoStockPassesBothPriceAndVolume`

**Test result:** 16/16 passed ✅

---

## What Was NOT Changed

The following are intentionally unchanged:

- **`VolumeSignal` display threshold (1.3×)** — still used in the scanner indicators grid for general display. The Stage-2 gate now uses `VolumeRatio` directly.
- **Legacy `ClassifyOversold`/`ClassifyOversoldEnhanced`** — these still produce the `Status` field (Confirmed/EarlyWarning) which is displayed in the scanner UI for context. They no longer control promotion.
- **`StagedSignalService.UpsertAndEnrichAsync`** — unchanged. Still correctly computes TrendShift, StageStatus, TurnStrength from staged RSI delta.
- **EOD Confirm single-day check** — `CheckOversoldEodConfirm` still runs and sets `Status = EodConfirm`. This no longer gates promotion but is retained for display/reference.
- **Frontend scanner UI** — `StageStatus` badge is already shown alongside the legacy `Status` badge. No UI changes required.
- **Database schema** — no migrations needed.

---

## Authoritative Promotion Path (Post-Fix)

```
All scan results (Oversold + Overbought chains)
        ↓
Filter: TrendShift contains "Bull Turn" or "Bear Turn"
        ↓
EodSignalPersistenceService.SaveAsync() — Stage-2 gate:
  ✅ RsiDelta1D != null (not Day 1)
  ✅ TrendShift = Bull Turn / Bear Turn
  ✅ PriceConfirmation: Price > EMA9 (Oversold) / Price < EMA9 (Overbought)
  ✅ VolumeRatio >= 1.5x
        ↓ (all four TRUE)
Insert dbo.DailySignals
Set StagedSignal.IsActiveWatch = 0
Display on EOD Signals Page
Include in ✅ CONFIRMED section of EOD email
```

If any gate fails, the signal appears in the **⏳ REVERSALS AWAITING CONFIRMATION** section with explicit boolean diagnostics showing which condition(s) failed.

---

## Diagnostic Log Format (per candidate)

```
[Stage2Gate] ENB.TO Oversold | BullBearTurn=True | Price=False | Volume=False (1.40x) | Promoted=False
[Stage2Gate] DR.TO Oversold  | BullBearTurn=True | Price=False | Volume=True (2.79x)  | Promoted=False
[Stage2Gate] CPH.TO Oversold | BullBearTurn=True | Price=False | Volume=True (2.04x)  | Promoted=False
[Stage2Gate] DVA Oversold    | BullBearTurn=True | Price=False | Volume=False (0.36x) | Promoted=False
```

A correctly promoted signal looks like:

```
[Stage2Gate] XYZ.TO Oversold | BullBearTurn=True | Price=True | Volume=True (2.10x) | Promoted=True
```
