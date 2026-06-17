# RSI Scanner — EOD CONFIRM Signal & Configurable EOD Window

**Feature Report — Implementation Summary**  
Date: 2026-06-17  
Branch: `develop`

---

## Overview

This document describes all changes applied to implement the **EOD CONFIRM** signal type for the RSI Scanner, the **configurable EOD execution window** (default 3:30–4:00 PM Eastern Time), the **UI indicator** showing when the window is active, and the **result status** displayed after a run.

---

## 1. New Signal Type: EOD CONFIRM

### 1.1 What It Is

**EOD CONFIRM** is a new, higher-priority signal that sits above `Confirmed` in the signal hierarchy. It fires when a stock in the Oversold or Overbought chain satisfies **all 4 end-of-day confirmation rules** simultaneously.

```
Priority order (highest → lowest):
  EodConfirm  → All 4 EOD rules met — highest conviction signal
  Confirmed   → Standard price-action trigger met
  EarlyWarning→ RSI threshold crossed, no confirmation yet
  Neutral     → No directional signal
```

### 1.2 Oversold EOD Confirm Rules

All 4 conditions must be true:

| #   | Rule                           | Threshold                                                        |
| --- | ------------------------------ | ---------------------------------------------------------------- |
| 1   | Daily RSI                      | < 25 (fixed — not configurable threshold)                        |
| 2   | Current Price vs 9-day EMA     | Price **>** 9-day EMA of close                                   |
| 3   | Daily Volume vs 20-day Average | Volume **> 1.5×** 20-day avg                                     |
| 4   | EOD Price Position             | Price **> Daily Open** AND Price **≥ (Daily High − 0.25 × ATR)** |

**Rule 4 logic (EOD price strength):** Near the close, the stock must be:

- Trading **above** its opening price (bulls in control)
- Within the top 25% of the daily ATR range from the high (not giving up gains near close)

### 1.3 Overbought EOD Confirm Rules

All 4 conditions must be true:

| #   | Rule                           | Threshold                                                       |
| --- | ------------------------------ | --------------------------------------------------------------- |
| 1   | Daily RSI                      | > 75 (fixed)                                                    |
| 2   | Current Price vs 9-day EMA     | Price **<** 9-day EMA of close                                  |
| 3   | Daily Volume vs 20-day Average | Volume **> 1.5×** 20-day avg                                    |
| 4   | EOD Price Position             | Price **< Daily Open** AND Price **≤ (Daily Low + 0.25 × ATR)** |

**Rule 4 logic (EOD price weakness):** Near the close, the stock must be:

- Trading **below** its opening price (bears in control)
- Within the bottom 25% of the daily ATR range from the low (sellers keeping it pinned near lows)

### 1.4 Ad-Hoc Symbol Analysis

For Ad-Hoc Symbol Analysis, **both Oversold and Overbought EOD Confirm rules** are evaluated. Any symbol entered manually will have its EOD Confirm status computed using both sets of rules (whichever applies based on the symbol's scan type).

---

## 2. ATR (Average True Range) Calculation

### 2.1 Definition

The **14-day Average True Range** measures average daily volatility. Used in EOD Confirm Rule 4.

### 2.2 Algorithm

**Step 1 — True Range (TR) per bar** (use the largest of 3 values):

```
TR₁ = High − Low                        (today's full intraday range)
TR₂ = |High − Yesterday's Close|        (gap-up from prior close)
TR₃ = |Low  − Yesterday's Close|        (gap-down from prior close)

True Range = Max(TR₁, TR₂, TR₃)
```

**Step 2 — 14-day ATR** (simple average):

```
ATR₁₄ = Sum(TR₁ through TR₁₄) ÷ 14
```

**Example:** If the 14 True Range values sum to $28.00:

```
ATR = $28.00 ÷ 14 = $2.00
```

This means the stock typically moves ~$2.00 from high to low per day.

**How ATR is used in Rule 4:**

- Oversold: `threshold = Daily High − (0.25 × ATR)` — price must be ≥ threshold (in top 25% of range from high)
- Overbought: `threshold = Daily Low + (0.25 × ATR)` — price must be ≤ threshold (in bottom 25% of range from low)

### 2.3 Implementation

Added to `RsiScannerService.cs`:

```csharp
private static decimal CalculateAtr(List<decimal> highs, List<decimal> lows,
                                     List<decimal> closes, int period = 14)
```

Returns 0 when insufficient historical data. EOD Confirm rules require ATR > 0.

---

## 3. Configurable EOD Execution Window

### 3.1 Default Window

**3:30 PM – 4:00 PM Eastern Time** (configurable)

### 3.2 Backend Configuration

**New file:** `backend/PortfolioManager.Api/Services/ScannerRuntimeConfig.cs`  
Singleton registered in DI. Holds:

- `EodWindowStart` (default: `"15:30"`)
- `EodWindowEnd` (default: `"16:00"`)
- `EodWindowEnabled` (default: `true`)
- `IsEodWindowActive()` — converts UTC to Eastern Time and checks if current time is in window

**Default config in** `appsettings.json`:

```json
"ScannerSettings": {
  "EodWindowStart": "15:30",
  "EodWindowEnd": "16:00",
  "EodWindowEnabled": true
}
```

**New API endpoints** (in `ScannerController.cs`):
| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/scanner/eod-settings` | Read current EOD window config |
| `PUT` | `/api/scanner/eod-settings` | Update EOD window (runtime — no restart) |
| `GET` | `/api/scanner/eod-window-active` | Check if window is currently active |

### 3.3 Frontend Configuration Screen

**Config page** (`config-page.component.ts/.html`) now has a dedicated **EOD Confirmation Window** card:

- Time pickers for start and end time (HH:mm format, Eastern Time)
- Toggle to enable/disable the EOD window
- "Save EOD Window" button — pushes changes to the backend immediately (no restart needed)
- Shows animated "ACTIVE NOW" badge when the window is currently open
- Info banner explaining the 4 EOD rules

---

## 4. UI Indicator — EOD Window Active

### 4.1 Scanner Page Banner

When the EOD window is active, a **pulsing orange banner** appears at the top of the RSI Scanner page:

```
⏰ EOD WINDOW ACTIVE — Rules Executing
   EOD Confirm rules are being evaluated (RSI extreme · Price vs 9-EMA ·
   Volume ≥ 1.5× · ATR position). Matching signals are marked EOD CONFIRM
   and a dedicated email will be sent.
                                        [🎯 N EOD Confirm signal(s): X Oversold · Y Overbought]
```

When the window has **closed** (results available), a blue info banner shows:

```
✅ Last EOD Run Result: N EOD Confirm signal(s): X oversold, Y overbought
```

### 4.2 Scanner Section Headers

Both the **Oversold Chain** and **Overbought Chain** section headers now show:

- Total count badge (existing)
- New **🎯 N EOD** badge (orange, pulsing animation) when EOD Confirm signals exist
- Updated subtitle: `X Confirmed · Y EOD Confirm`

### 4.3 Signal Badge in Table

A new **EOD CONFIRM** status badge (orange, with pulsing glow animation) appears in the Signal column, replacing the standard `CONFIRMED` badge when the `EodConfirm` status is set.

---

## 5. Email Notifications

### 5.1 Existing Confirmed Signal Emails

Updated `SignalNotificationTracker.cs` to also track `EodConfirm` signals alongside `Confirmed` signals to prevent duplicate standard emails.

### 5.2 New EOD Confirm Email

**Method:** `NotifyNewEodConfirmedSignalsAsync()` in `EmailNotificationService.cs`

- Subject: `🎯 EOD CONFIRM — {N} Signals: {tickers}`
- Dark blue header with EOD branding
- Orange info banner explaining the EOD rules
- Separate table sections: **Oversold EOD Confirm** + **Overbought EOD Confirm**
- Table columns: Ticker · RSI · Price · 9-EMA · ATR (14) · Volume · EOD Confirm Details
- Each row shows: `🎯 EOD CONFIRM` orange badge + full trigger details with all 4 rule values
- Sent **only during the configured EOD window** (background service checks `runtimeConfig.IsEodWindowActive()`)
- De-duplicated via `GetNewlyEodConfirmedAndSync()` — fires only once per symbol per session

---

## 6. Background Service Changes

**File:** `RsiAlertBackgroundService.cs`

New constructor parameter: `ScannerRuntimeConfig runtimeConfig`

After the standard Confirmed signal scan:

```csharp
bool inEodWindow = runtimeConfig.IsEodWindowActive();
if (inEodWindow)
{
    // Check for new EOD Confirm signals
    await notifier.NotifyNewEodConfirmedSignalsAsync(result);
}
```

---

## 7. Complete File Change List

### Backend

| File                                    | Type     | Change Description                                                                                                                                                                                                                                                                                                        |
| --------------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Models/ScannerModels.cs`               | Modified | Added `EodConfirm` to `SignalStatus` enum; added `DailyAtr`, `Ema9Price` to `RsiScanResult`; added `EodWindowSettingsDto` class                                                                                                                                                                                           |
| `Services/ScannerRuntimeConfig.cs`      | **New**  | Singleton for runtime-overridable EOD window config; `IsEodWindowActive()` Eastern Time check                                                                                                                                                                                                                             |
| `Services/RsiScannerService.cs`         | Modified | Added `CalculateAtr()` (14-day ATR); added `CheckOversoldEodConfirm()` + `BuildOversoldEodTrigger()`; added `CheckOverboughtEodConfirm()` + `BuildOverboughtEodTrigger()`; updated `AnalyzeSymbolAsync()` to compute ATR + 9-EMA + apply EOD Confirm override; added `DailyAtr` + `Ema9Price` to returned `RsiScanResult` |
| `Services/SignalNotificationTracker.cs` | Modified | `GetNewlyConfirmedAndSync()` now also tracks `EodConfirm`; new `GetNewlyEodConfirmedAndSync()` method for EOD-specific tracking                                                                                                                                                                                           |
| `Services/RsiAlertBackgroundService.cs` | Modified | Added `ScannerRuntimeConfig` injection; added EOD window time-gating with `IsEodWindowActive()` check; calls `NotifyNewEodConfirmedSignalsAsync()` during window                                                                                                                                                          |
| `Services/EmailNotificationService.cs`  | Modified | Added `NotifyNewEodConfirmedSignalsAsync()`; `SendEodAlertEmailAsync()`; `BuildEodHtmlBody()`; `BuildEodSignalTable()` — dedicated EOD email with dark blue header, orange banner, ATR/EMA columns                                                                                                                        |
| `Controllers/ScannerController.cs`      | Modified | Added `ScannerRuntimeConfig` injection; added `GET /api/scanner/eod-settings`; `PUT /api/scanner/eod-settings`; `GET /api/scanner/eod-window-active` endpoints                                                                                                                                                            |
| `Program.cs`                            | Modified | Registered `ScannerRuntimeConfig` as singleton, seeded from `ScannerSettings` config section                                                                                                                                                                                                                              |
| `appsettings.json`                      | Modified | Added `ScannerSettings` section with default EOD window (`15:30`–`16:00`, enabled)                                                                                                                                                                                                                                        |

### Frontend

| File                                                | Type     | Change Description                                                                                                                                                                                                                           |
| --------------------------------------------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `core/models/portfolio.models.ts`                   | Modified | Added `'EodConfirm'` to `SignalStatus` type; added `dailyAtr: number` and `ema9Price: number` to `RsiScanResult` interface                                                                                                                   |
| `core/services/config.service.ts`                   | Modified | Added `eodWindowStart`, `eodWindowEnd`, `eodWindowEnabled` to `AppConfig` interface and `DEFAULTS`                                                                                                                                           |
| `core/services/portfolio-api.service.ts`            | Modified | Added `getEodSettings()`, `updateEodSettings()`, `getEodWindowStatus()` API methods                                                                                                                                                          |
| `core/services/scanner-state.service.ts`            | Modified | Added `eodWindowActive` and `lastEodRunSummary` signals; added `eodConfirmOversold`, `eodConfirmOverbought`, `totalEodConfirm` computed signals; added 30-second EOD window status polling; `updateEodSummary()` helper                      |
| `features/config/config-page.component.ts`          | Modified | Added `MatSlideToggleModule` import; added `eodForm` reactive form group; added `savingEodSettings` and `eodWindowActive` signals; added `saveEodSettings()` method; `ngOnInit()` loads EOD settings from backend; `reset()` resets EOD form |
| `features/config/config-page.component.html`        | Modified | Added **EOD Confirmation Window** card with time pickers, enable toggle, active indicator badge, info banner, save button                                                                                                                    |
| `features/config/config-page.component.scss`        | Modified | Added EOD card styles: `.eod-card`, `.eod-active-badge`, `.eod-info-banner`, `.eod-toggle-row`, `.eod-inline-badge`, pulse animation                                                                                                         |
| `features/scanner/scanner-page.component.ts`        | Modified | Added `eodConfirmSummary` computed signal; imports unchanged                                                                                                                                                                                 |
| `features/scanner/scanner-page.component.html`      | Modified | Added EOD window active/result banner at top of page; added 🎯 EOD count badges on section headers; updated subtitles to show EOD Confirm counts                                                                                             |
| `features/scanner/scanner-page.component.scss`      | Modified | Added `.eod-banner`, `.eod-banner--active`, `.eod-banner--result`, `.eod-pulse`, `.eod-result-badge`, `.eod-badge` styles with pulse animations                                                                                              |
| `features/scanner/rsi-scanner-table.component.html` | Modified | Added `EodConfirm` branch in status column (`@if (row.status === 'EodConfirm')`) before existing `Confirmed` check; shows orange 🎯 EOD CONFIRM badge                                                                                        |
| `features/scanner/rsi-scanner-table.component.scss` | Modified | Added `.eod-confirm` badge style with pulsing glow keyframe animation                                                                                                                                                                        |

---

## 8. Data Flow Summary

```
Backend Scan (ScannerService.AnalyzeSymbolAsync)
  │
  ├─ Compute: RSI, ATR(14), EMA9(price), VolRatio
  │
  ├─ If rsi ≤ oversoldThreshold:
  │     Apply base classification (Confirmed / EarlyWarning)
  │     THEN: CheckOversoldEodConfirm(rsi<25, price>EMA9, vol>1.5x, price near high)
  │           → Override status = EodConfirm if all 4 pass
  │
  ├─ If rsi ≥ overboughtThreshold:
  │     Apply base classification (Confirmed / EarlyWarning)
  │     THEN: CheckOverboughtEodConfirm(rsi>75, price<EMA9, vol>1.5x, price near low)
  │           → Override status = EodConfirm if all 4 pass
  │
  └─ Return RsiScanResult (includes DailyAtr, Ema9Price fields)

Background Service (every ScanIntervalSeconds)
  │
  ├─ Run scan → notify standard Confirmed signals
  │
  └─ If IsEodWindowActive() (3:30–4:00 PM ET):
        Get newly EodConfirm signals
        Send dedicated EOD Confirm email (if any new)

Frontend (ScannerStateService)
  │
  ├─ Poll /api/scanner/eod-window-active every 30s
  │     → sets eodWindowActive signal
  │
  ├─ On scan result:
  │     → computes eodConfirmOversold, eodConfirmOverbought, totalEodConfirm
  │     → updates lastEodRunSummary
  │
  └─ Scanner Page + Table:
        Shows EOD banner → active or result
        EOD count badges on section headers
        EOD CONFIRM badge in table Signal column
```

---

## 9. Configuration Reference

### EOD Window via Config Screen (Frontend → Backend)

1. Go to **Configuration** screen
2. Scroll to **EOD Confirmation Window** card
3. Set **Window start time** and **Window end time** (HH:mm, Eastern Time)
4. Toggle **EOD window enabled**
5. Click **Save EOD Window**

The change is pushed to `PUT /api/scanner/eod-settings` and takes effect immediately for the background service — **no application restart required**.

### EOD Window via appsettings.json (Backend Default)

```json
"ScannerSettings": {
  "EodWindowStart": "15:30",
  "EodWindowEnd": "16:00",
  "EodWindowEnabled": true
}
```

---

## 10. Signal Trigger Details Examples

**Oversold EOD Confirm trigger text:**

```
EOD CONFIRM — All 4 rules met: RSI 22.4 < 25 ✓ | Price $14.52 > 9-EMA $14.31 ✓ |
Volume 1.8x avg (>1.5x) ✓ | Price > Open $14.20 and Price $14.52 ≥ threshold $14.48
(High $14.60 − 0.25×ATR $0.0480) ✓
```

**Overbought EOD Confirm trigger text:**

```
EOD CONFIRM — All 4 rules met: RSI 78.1 > 75 ✓ | Price $194.10 < 9-EMA $194.85 ✓ |
Volume 2.1x avg (>1.5x) ✓ | Price < Open $195.50 and Price $194.10 ≤ threshold $194.22
(Low $193.90 + 0.25×ATR $0.1280) ✓
```
