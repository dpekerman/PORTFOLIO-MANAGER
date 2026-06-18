# RSI Alert Notification — Investigation Report

**Date:** 2026-06-18  
**Issue:** BABA (Oversold CONFIRMED) did not receive an email alert; RY.TO (Overbought CONFIRMED) did.

---

## 1. How the Notification System Works (Full Flow)

### 1.1 Background Service (`RsiAlertBackgroundService`)

A .NET `BackgroundService` runs continuously on the backend, independent of the frontend. Every N seconds (configured via `EmailSettings.ScanIntervalSeconds`):

```
[Timer fires]
  → RunScanCycleAsync()
      → Create DI scope
      → scanner.ScanAsync(extraSymbols, ...)    ← scans symbols
      → notifier.NotifyNewConfirmedSignalsAsync()   ← standard CONFIRMED email
      → if (IsEodWindowActive())
            notifier.NotifyNewEodConfirmedSignalsAsync()  ← EOD email
```

### 1.2 Signal Classification

Each scanned symbol goes through `AnalyzeSymbolAsync()` in `RsiScannerService` and is assigned one of:

| Status         | Meaning                                                  |
| -------------- | -------------------------------------------------------- |
| `Neutral`      | RSI between thresholds — no signal                       |
| `EarlyWarning` | RSI below/above threshold but price-action not confirmed |
| `Confirmed`    | RSI + candlestick pattern triggered (Enhanced logic)     |
| `EodConfirm`   | All 4 strict EOD conditions met (see §3)                 |

### 1.3 Signal Tracker (`SignalNotificationTracker`)

A singleton `HashSet<string>` prevents duplicate emails:

- **Key format (regular):** `"SYMBOL|ScanType"` e.g. `"BABA|Oversold"`
- **Key format (EOD):** `"EOD|SYMBOL|ScanType"` e.g. `"EOD|RY.TO|Overbought"`

When `GetNewlyConfirmedAndSync()` is called:

1. Collect all `Confirmed` + `EodConfirm` results
2. Return only those **not** already in the set
3. Add all current confirmed keys
4. Remove keys no longer confirmed (so they can re-fire if the signal reappears)

---

## 2. Root Cause — Bug #1: BABA Not Scanned by Background Service

### The Problem

In `RsiAlertBackgroundService.RunScanCycleAsync()`, the original code:

```csharp
// BEFORE (buggy)
var result = await scanner.ScanAsync(
    null,                 // extraSymbols: background service scans default TSX universe only
    Settings.OversoldThreshold,
    Settings.OverboughtThreshold,
    "Enhanced",
    ct);
```

`null` for `extraSymbols` means only the **hardcoded TSX watchlist** (50 TSX symbols) is scanned.

The `ScannerController.GetRsiScan()` (used by the frontend) works differently — it queries the database for portfolio and watchlist symbols and passes them as extra symbols:

```csharp
// ScannerController (correct — has database lookup)
var portfolioSymbols = await db.PortfolioItems.Where(...).Select(p => p.Symbol).ToListAsync(ct);
var watchlistSymbols = await db.WatchlistItems.Select(w => w.Symbol).ToListAsync(ct);
var extraSymbols = portfolioSymbols.Concat(watchlistSymbols)...ToList();
var result = await scanner.ScanAsync(extraSymbols, ...);
```

### Why RY.TO Got an Email But BABA Did Not

| Symbol  | In TSX Watchlist?             | Scanned by Background? | Email Sent? |
| ------- | ----------------------------- | ---------------------- | ----------- |
| `RY.TO` | ✅ Yes (Royal Bank of Canada) | ✅ Yes                 | ✅ Yes      |
| `BABA`  | ❌ No (NYSE/US stock)         | ❌ No                  | ❌ No       |

BABA is a US-listed stock (NYSE). It is visible in the frontend because it is in the user's **portfolio or watchlist**, which the frontend scan includes. The background service never saw it.

### The Fix

**File:** `backend/PortfolioManager.Api/Services/RsiAlertBackgroundService.cs`

```csharp
// AFTER (fixed)
using var scope = scopeFactory.CreateScope();
var scanner  = scope.ServiceProvider.GetRequiredService<IRsiScannerService>();
var notifier = scope.ServiceProvider.GetRequiredService<EmailNotificationService>();
var db       = scope.ServiceProvider.GetRequiredService<AppDbContext>();          // ← NEW

// Mirror ScannerController: include all user-defined portfolio + watchlist symbols
var portfolioSymbols = await db.PortfolioItems
    .Where(p => !p.IsManual)
    .Select(p => p.Symbol)
    .ToListAsync(ct);
var watchlistSymbols = await db.WatchlistItems
    .Select(w => w.Symbol)
    .ToListAsync(ct);
var extraSymbols = portfolioSymbols
    .Concat(watchlistSymbols)
    .Select(s => s.Trim().ToUpperInvariant())
    .Distinct()
    .ToList();

var result = await scanner.ScanAsync(
    extraSymbols,         // ← was null, now includes portfolio/watchlist symbols
    Settings.OversoldThreshold,
    ...
```

---

## 3. Root Cause — Bug #2: EOD Tracker Keys Wiped by Regular Scan

### The Problem

`SignalNotificationTracker` uses a single shared `_notifiedKeys` `HashSet<string>`. The `GetNewlyConfirmedAndSync()` method (for regular CONFIRMED emails) had:

```csharp
// BEFORE (buggy)
_notifiedKeys.RemoveWhere(k => !confirmedKeys.Contains(k));
```

`confirmedKeys` only contains `"SYMBOL|ScanType"` format keys. EOD keys have `"EOD|SYMBOL|ScanType"` format. Because `confirmedKeys.Contains("EOD|RY.TO|Overbought")` is always `false`, **every call to `GetNewlyConfirmedAndSync()` deleted all EOD tracking keys.**

This meant: during the EOD window, every scan cycle would re-treat the EOD signal as new and send a duplicate EOD email. With a 5-minute scan interval and a 30-minute EOD window, this could produce up to **6 duplicate EOD emails** per signal.

### The Fix

**File:** `backend/PortfolioManager.Api/Services/SignalNotificationTracker.cs`

```csharp
// AFTER (fixed)
// Do NOT remove EOD-prefixed keys — managed separately by GetNewlyEodConfirmedAndSync
_notifiedKeys.RemoveWhere(k => !k.StartsWith("EOD|") && !confirmedKeys.Contains(k));
```

---

## 4. EOD Confirmation Window — Full Logic

### 4.1 What Is It?

The EOD (End-of-Day) Confirm window is active **3:30 PM – 4:00 PM Eastern Time** (configurable). During this window, a stricter set of rules is evaluated. The intent is to identify signals that have high probability of closing strong, making the daily candle a more reliable confirmation bar.

### 4.2 EOD Confirm Conditions — Oversold

All 4 conditions must be true:

| Rule | Condition                                                                                     |
| ---- | --------------------------------------------------------------------------------------------- |
| 1    | Daily RSI < 25 (stricter than the standard < 30 threshold)                                    |
| 2    | Current price > 9-day EMA (price is recovering above momentum anchor)                         |
| 3    | Projected daily volume > 1.5× 20-day average (elevated participation)                         |
| 4    | Price > today's open AND price ≥ (today's high − 0.25 × ATR) (closing near the high of range) |

### 4.3 EOD Confirm Conditions — Overbought

All 4 conditions must be true (mirror image):

| Rule | Condition                                                   |
| ---- | ----------------------------------------------------------- |
| 1    | Daily RSI > 75                                              |
| 2    | Current price < 9-day EMA                                   |
| 3    | Projected daily volume > 1.5× 20-day average                |
| 4    | Price < today's open AND price ≤ (today's low + 0.25 × ATR) |

### 4.4 Volume Projection

To avoid penalizing partial-day intraday volume against a 20-day average of full-session volume, `ProjectIntradayVolume()` scales raw volume:

```
scaleFactor = min(2.0,  390 minutes / elapsed_session_minutes)
projectedVolume = rawVolume × scaleFactor
```

At 3:30 PM ET (360 minutes elapsed), `scaleFactor = 390/360 = 1.083`. Volume is boosted ~8.3% to project a full session equivalent.

### 4.5 When Are EOD Conditions Evaluated?

**Important:** The 4-condition EOD check runs on **every scan**, regardless of time of day. If conditions are met, the signal is classified as `EodConfirm` in the data model. This is what you see in the UI (`0 EOD Confirm` shown in the header).

The **EOD window** only controls whether the background service sends the **additional EOD Confirm email**. Regular CONFIRMED emails fire regardless of the window.

### 4.6 Will You Get a BABA EOD Confirm Email Today?

**From the screenshot:** BABA has `Status = Confirmed` (not `EodConfirm`). The header shows "1 Confirmed · 0 EOD Confirm" for the oversold chain, meaning BABA does **not** currently meet all 4 EOD conditions.

For BABA to reach `EodConfirm` status between now and 4:00 PM ET, it would need:

- RSI remains < 25 ✓ (currently 24.2)
- Price moves above 9-day EMA
- Volume surges above 1.5× 20-day average
- Price closes above today's open AND near the top of today's range

With the bug fix applied, **the regular CONFIRMED email for BABA will fire on the next background scan cycle** since BABA was never added to the tracker (the tracker is in-memory and resets on restart). If the backend was restarted today and BABA was already confirmed when it started, it will send an email on the next scan.

---

## 5. Email Sending Logic Summary

```
Per scan cycle:
┌─────────────────────────────────────────────────────────────┐
│ 1. ScanAsync(portfolioSymbols + watchlistSymbols + TSX)     │ ← FIXED
│                                                             │
│ 2. NotifyNewConfirmedSignalsAsync()                         │
│    → tracker.GetNewlyConfirmedAndSync()                     │
│    → picks: Confirmed + EodConfirm signals NOT yet notified │
│    → sends 1 email per new batch                            │
│    → adds keys "SYMBOL|ScanType"                            │
│    → removes stale keys (but NOT "EOD|..." keys) ← FIXED    │
│                                                             │
│ 3. if (15:30–16:00 ET)                                      │
│    NotifyNewEodConfirmedSignalsAsync()                       │
│    → tracker.GetNewlyEodConfirmedAndSync()                  │
│    → picks: EodConfirm signals NOT yet EOD-notified         │
│    → sends 1 EOD email per new batch                        │
│    → adds/removes keys "EOD|SYMBOL|ScanType"                │
└─────────────────────────────────────────────────────────────┘
```

---

## 6. Files Changed

| File                                    | Change                                                                                            |
| --------------------------------------- | ------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `Services/RsiAlertBackgroundService.cs` | Inject `AppDbContext`; query portfolio + watchlist symbols; pass to `ScanAsync` instead of `null` |
| `Services/SignalNotificationTracker.cs` | Protect `"EOD                                                                                     | ..."`keys from being wiped by`GetNewlyConfirmedAndSync`'s `RemoveWhere` |

---

## 7. Testing Verification

After restarting the backend:

1. On the next scan cycle, BABA will be included in the scan
2. Since the in-memory tracker is empty after restart, BABA will be treated as a new CONFIRMED signal
3. An email will be sent for BABA (assuming `EmailSettings.Enabled = true` and SMTP credentials are configured)
4. On subsequent scans, BABA will be in the tracker and will NOT send duplicate emails unless it drops off confirmed status and re-enters it
