# EOD Email Notification — Conditions & Troubleshooting Report

**Date:** 2026-07-30

---

## Summary

EOD confirmation emails are sent by `RsiAlertBackgroundService` only when **all** gate conditions pass simultaneously. Standard "Confirmed" signal emails are intentionally suppressed — only `EodConfirm` signals send emails.

---

## Gate 1 — Background Service Must Be Running

- Registered as a hosted service in `Program.cs`
- Polls every `ScanIntervalSeconds` (default: **60 s** in Development, **300 s** in Production)
- If the backend process is stopped or crashed, **no emails can be sent**

---

## Gate 2 — Live Data (Not Demo)

**File:** `Services/RsiAlertBackgroundService.cs`

```csharp
if (result.IsDemo) return;  // skip all notification logic
```

- If Yahoo Finance is unreachable, the scanner returns demo data
- `IsDemo == true` → entire notification block is skipped
- **Common silent failure cause** if Yahoo crumb/cookie expires or network is blocked

---

## Gate 3 — Inside EOD Window

**File:** `Services/ScannerRuntimeConfig.cs` → `IsEodWindowActive()`

```csharp
if (!_eodWindowEnabled) return false;
return currentTime >= start && currentTime <= end;  // Eastern Time
```

| Setting            | Default    |
| ------------------ | ---------- |
| `EodWindowEnabled` | `true`     |
| `EodWindowStart`   | `15:30` ET |
| `EodWindowEnd`     | `16:30` ET |

- Config lives in `appsettings.Development.json` under `ScannerSettings`
- Can be overridden at runtime via `PUT /api/scanner/eod-settings` (persisted to `scanner-eod-config.json`, which takes **highest priority**)
- If the server time zone is wrong or the config file has `EodWindowEnabled: false`, this gate always fails

---

## Gate 4 — Signal Must Meet All 4 EOD Technical Rules

**File:** `Services/RsiScannerService.cs`

All four conditions must be true simultaneously for a symbol's status to become `EodConfirm`.

### Oversold Setup (Buy Signal)

| #   | Condition                                                    | Value                                       |
| --- | ------------------------------------------------------------ | ------------------------------------------- |
| 1   | RSI < extreme threshold                                      | Default **RSI < 25**                        |
| 2   | Price > 9-period EMA                                         | Buyers defending trend                      |
| 3   | Projected daily volume > 1.5× 20-day avg volume              | Volume projected to full-session equivalent |
| 4   | Price > Daily Open **AND** Price ≥ (Daily High − 0.25 × ATR) | Strong reversal into close                  |

### Overbought Setup (Sell Signal)

| #   | Condition                                                   | Value                          |
| --- | ----------------------------------------------------------- | ------------------------------ |
| 1   | RSI > extreme threshold                                     | Default **RSI > 75**           |
| 2   | Price < 9-period EMA                                        | Sellers overcoming buyers      |
| 3   | Projected daily volume > 1.5× 20-day avg volume             | Same projection logic          |
| 4   | Price < Daily Open **AND** Price ≤ (Daily Low + 0.25 × ATR) | Strong distribution into close |

**Volume Projection Logic:** If the scan runs before 4:00 PM, raw volume is scaled up:

```
scaleFactor = 390 / elapsedTradingMinutes   (capped at 2.0×)
```

This prevents partial-day volume from being unfairly penalized.

RSI thresholds (25/75) are overridable via `PUT /api/scanner/eod-settings`.

---

## Gate 5 — Signal Not Already Sent (Deduplication)

**File:** `Services/SignalNotificationTracker.cs` → `GetNewlyEodConfirmedAndSync()`

- Tracks sent signals with key `"EOD|{Symbol}|{ScanType}"`
- Each symbol+direction triggers an EOD email **only once per EOD window**
- Keys are cleared only **after** the EOD window closes
- If the backend was restarted mid-window, the tracker resets and could re-send

---

## Gate 6 — Email Service Configuration

**File:** `Services/EmailNotificationService.cs`

### 6a. Email Must Be Enabled

```json
"EmailNotification": {
  "Enabled": true
}
```

### 6b. SMTP Credentials Must Be Present

```csharp
if (string.IsNullOrWhiteSpace(settings.Username) ||
    string.IsNullOrWhiteSpace(settings.Password)) {
    _logger.LogWarning("SMTP credentials not configured — skipping email");
    return;
}
```

Config in `appsettings.Development.json` (or environment secret):

- `SmtpHost`: `smtp.gmail.com`
- `SmtpPort`: `587`
- `UseStartTls`: `true`
- `Username` / `Password`: Gmail app password

### 6c. Recipient List Must Not Be Empty

- Loaded from `notification-recipients.json`
- If the file is empty or missing, no emails are sent silently

---

## Full Decision Tree

```
Background service wakes up (every 60–300 s)
  │
  ├─ result.IsDemo == true?          → STOP (Yahoo Finance unavailable)
  │
  ├─ Not inside EOD window?          → STOP (outside 15:30–16:30 ET)
  │                                         or EodWindowEnabled = false
  │
  ├─ No symbols meet all 4 rules?    → STOP (market conditions not extreme enough)
  │
  ├─ All signals already tracked?    → STOP (deduplication — already sent today)
  │
  ├─ Enabled = false?                → STOP (email disabled in config)
  │
  ├─ No SMTP credentials?            → STOP (logs warning)
  │
  ├─ No recipients?                  → STOP (empty notification-recipients.json)
  │
  └─ ALL PASS → Send EOD email
```

---

## Email Properties

| Property | Value                                                       |
| -------- | ----------------------------------------------------------- |
| Subject  | `🎯 EOD CONFIRM — {Ticker} (RSI {value}) End-of-Day Signal` |
| Priority | `MailPriority.High`                                         |
| Headers  | `X-Priority: 1`, `Importance: High`                         |
| Format   | HTML with signal table and technical details                |

---

## Manual Trigger

`POST /api/notification/scan-now`

Forces an immediate scan and **resets the signal tracker**, so all currently-confirmed signals fire again regardless of deduplication state. Useful for testing.

---

## Most Likely Reasons Emails Have Stopped Arriving

1. **Yahoo Finance returning demo/cached data** — `IsDemo == true`, all notification logic skipped
2. **EOD window config changed** — check `scanner-eod-config.json` for stale overrides with wrong times or `EodWindowEnabled: false`
3. **Market conditions simply not meeting all 4 rules** — RSI never reached < 25 or > 75 with volume + ATR confirmation simultaneously
4. **Gmail app password expired or revoked** — SMTP auth fails silently (logged as warning only)
5. **Backend not running during 15:30–16:30 ET window** — service was stopped or crashed
6. **Server time zone misconfiguration** — Eastern Time conversion producing wrong window times

---

## Key Files Reference

| File                                    | Purpose                              |
| --------------------------------------- | ------------------------------------ |
| `Services/RsiAlertBackgroundService.cs` | Main orchestration loop              |
| `Services/RsiScannerService.cs`         | 4-rule EOD signal detection          |
| `Services/ScannerRuntimeConfig.cs`      | EOD window + threshold config        |
| `Services/EmailNotificationService.cs`  | SMTP send + all send guards          |
| `Services/SignalNotificationTracker.cs` | Per-session deduplication            |
| `appsettings.Development.json`          | Base config (email, window times)    |
| `scanner-eod-config.json`               | Runtime overrides (highest priority) |
| `notification-recipients.json`          | Recipient list                       |
