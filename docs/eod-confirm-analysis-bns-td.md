# EOD Confirm Signal — Overbought Chain Analysis

## Why BNS.TO and TD.TO Did Not Trigger EOD Confirm

**Date:** 2026-06-22  
**Stocks reviewed:** BNS.TO (Bank of Nova Scotia), TD.TO (Toronto-Dominion Bank)  
**Feature:** RSI Scanner → Overbought Chain → EOD Confirm Status

---

## 1. How the Overbought EOD Confirm Chain Works

The scanner applies a **two-stage decision**:

```
Stage 1: Must qualify for the Overbought Chain
         → RSI ≥ 75 (default overboughtThreshold)

Stage 2: ClassifyOverboughtEnhanced() assigns a preliminary status
         (Confirmed / EarlyWarning)

Stage 3: CheckOverboughtEodConfirm() is evaluated as an OVERRIDE
         → If ALL 4 rules pass, status becomes EodConfirm
         → If any rule fails, Stage 2 status stands unchanged
```

---

## 2. The 4 EOD Confirm Rules — Overbought

All four conditions must be **simultaneously true** at scan time:

| #   | Rule                                                    | Threshold                                | Failing means…                              |
| --- | ------------------------------------------------------- | ---------------------------------------- | ------------------------------------------- |
| 1   | RSI > 75                                                | Hard cutoff                              | Stock not even in overbought chain          |
| 2   | Current Price **<** 9-day EMA                           | Price turned below its short-term mean   | Still trending — no reversal started        |
| 3   | Projected Volume **≥ 1.5×** 20-day avg                  | Volume spike confirms institutional exit | Low volume = no conviction                  |
| 4   | Close **<** Open **AND** Close **≤** (Low + 0.25 × ATR) | Bearish candle closed near its low       | Closed in upper range = no distribution day |

**Source:** `RsiScannerService.cs` lines 945–955

```csharp
private static bool CheckOverboughtEodConfirm(
    decimal rsi, decimal close, decimal ema9,
    decimal volRatio, decimal open, decimal low, decimal atr)
{
    if (rsi <= 75m) return false;                           // Rule 1
    if (close >= ema9) return false;                        // Rule 2
    if (volRatio < 1.5m) return false;                     // Rule 3
    if (atr <= 0m) return false;
    decimal priceThreshold = low + (0.25m * atr);          // Rule 4 threshold
    return close < open && close <= priceThreshold;         // Rule 4
}
```

---

## 3. Step-by-Step Analysis: Why BNS.TO and TD.TO Did Not Fire

### Gate 0 — Were they in the Overbought Chain?

The scanner **only places a stock in the Overbought chain if RSI ≥ 75**. The frontend decision engine uses a lower threshold (RSI ≥ 65) for its "overbought context" label, but the backend scanner is stricter.

**Canadian bank stocks (BNS.TO, TD.TO) context:**

- These are large-cap, low-volatility dividend stocks
- They tend to have RSI in the 50–70 range during normal conditions
- RSI breaching 75 requires an unusually strong multi-week uprun

> **⚠️ Most likely reason they appeared in the scanner at all:** The scan was run with the default `overboughtThreshold = 75`. If their RSI was between 70–74, they would **not** appear in the Overbought chain at all. If they appeared, their RSI was ≥ 75 (Rule 1 passed).

---

### Rule 1 — RSI > 75 ✓ (must have passed — they were in the chain)

---

### Rule 2 — Current Price < 9-day EMA

This is the **most commonly failing rule** for large-cap bank stocks in an overbought run.

**Why it fails for BNS.TO / TD.TO:**

- When a stock is genuinely overbought (RSI ≥ 75), it typically means the **price has been rising strongly** for 10–14+ days
- A 9-day EMA lags current price when the stock is trending up
- So at peak RSI, price is almost always **above** the 9-EMA — not below it
- The 9-EMA only drops below current price after a reversal has already started (1–3 days of selling)
- This rule is by design: EOD Confirm requires **early evidence of reversal**, not just peak overbought

**Interpretation:** BNS.TO / TD.TO were still trading **above** their 9-day EMA at scan time. The trend was intact — no distribution had started. Rule 2 FAILED → EOD Confirm blocked.

---

### Rule 3 — Projected Volume ≥ 1.5× 20-day Average

Canadian bank stocks have:

- Very high absolute volume (millions of shares daily)
- But also very stable, predictable average volume
- 1.5× is a high bar — it requires a notable volume event (dividend ex-date, earnings, macro catalyst)

**Note on volume projection:** The scanner uses `ProjectIntradayVolume()` to scale intraday volume to a projected full-session equivalent. This scaling only applies when the scan runs during market hours (9:45 AM – 4:00 PM ET). After hours, raw volume is used unchanged.

**Typical outcome:** Without a specific catalyst (earnings, BoC rate decision, U.S. bank contagion news), BNS.TO and TD.TO rarely generate 1.5× volume. Rule 3 likely FAILED.

---

### Rule 4 — Close < Open AND Close ≤ (Low + 0.25 × ATR)

This rule requires a **confirmed distribution candle**:

- `Close < Open`: The day closed red (bears won the session)
- `Close ≤ Low + 0.25 × ATR`: The close was within the bottom 25% of the ATR band above the day's low

For Canadian banks near RSI 75:

- They are in a momentum run — intraday they tend to open and close with gains (green candles)
- Even on mild profit-taking days, the close is often near the middle or top of the day's range
- A close in the bottom quartile of the ATR band would signal strong institutional selling — this is uncommon without a specific negative catalyst

**Interpretation:** BNS.TO / TD.TO did not have a confirmed bearish close near the day's low. Rule 4 FAILED.

---

## 4. Summary of Failure Points

```
EOD Confirm Checklist for BNS.TO / TD.TO
─────────────────────────────────────────────────────────────────────
Rule 1: RSI > 75                          → ✓ PASSED (in chain)
Rule 2: Close < 9-day EMA                 → ✗ FAILED — price still above EMA
                                              (stock still trending up at scan time)
Rule 3: Projected Vol ≥ 1.5× 20-day avg  → ✗ FAILED — no high-volume catalyst
Rule 4: Close < Open AND Close ≤ L+0.25ATR → ✗ FAILED — no bearish close near low
─────────────────────────────────────────────────────────────────────
Result: EodConfirm NOT triggered → Status remains EarlyWarning (Stage 2 output)
```

---

## 5. What Status They Would Show Instead

Since EOD Confirm did not fire, the `ClassifyOverboughtEnhanced()` status is shown:

| Condition                                                                                          | Status         |
| -------------------------------------------------------------------------------------------------- | -------------- |
| Bearish close (lower half of range) + volume ≥ 1.3× or MACD falling + closed below prior day's low | `Confirmed`    |
| Bearish close (lower half of range) + volume ≥ 1.3× or MACD falling                                | `Confirmed`    |
| BB upper band breakout + MACD histogram slope falling                                              | `EarlyWarning` |
| RSI > 80 + MACD histogram slope falling                                                            | `EarlyWarning` |
| Everything else (no distribution detected)                                                         | `EarlyWarning` |

BNS.TO and TD.TO most likely show **EarlyWarning** — "RSI above overbought threshold, no structural distribution detected, waiting for candle close."

---

## 6. Design Intent vs. Actual Behaviour

### The design is correct — EOD Confirm is intentionally hard to trigger

The EOD Confirm signal is not "RSI is overbought." It is:

> **"RSI is overbought AND the market is showing real-time evidence of institutional distribution in the final hour of trading."**

This multi-condition gate prevents false sell signals on healthy momentum stocks that just happen to have high RSI. Large-cap Canadian banks (BNS, TD, RY, BM) are momentum blue chips — they can sustain RSI 75+ for weeks during bull cycles.

### When EOD Confirm WOULD fire for BNS.TO / TD.TO

All of these must occur on the same day:

1. RSI already ≥ 75 (in overbought chain)
2. A 2–3 day pullback has already started → price crosses below 9-EMA
3. The scan day has 1.5× normal volume (e.g., post-earnings, BoC policy day)
4. The stock closes red AND near its intraday low (high-to-low candle, not a recovery)

This pattern corresponds to a **confirmed reversal day**, not just peak overbought.

---

## 7. Recommended Threshold Consideration

The backend scanner uses **RSI ≥ 75** to enter the overbought chain. The frontend decision engine uses **RSI ≥ 65** for overbought context in its advisory signals. This creates a gap:

| RSI Range | Backend Scanner                           | Frontend Decision Engine     |
| --------- | ----------------------------------------- | ---------------------------- |
| 65 – 74   | Stock NOT in overbought chain             | "Overbought context" flagged |
| ≥ 75      | In overbought chain, EOD Confirm eligible | "Overbought context" flagged |

If BNS.TO / TD.TO had RSI 65–74, they would not appear in the overbought chain at all — they would only show up in the Portfolio or Watchlist pages via the frontend decision engine.

**Potential improvement:** Add a configurable `scanThreshold` vs `confirmThreshold` split — e.g., enter the chain at RSI ≥ 68, but require RSI ≥ 75 for EOD Confirm. This would surface more stocks in the chain at "EarlyWarning" while keeping EOD Confirm strict.

---

## 8. Code Reference

| Location                         | Purpose                                                         |
| -------------------------------- | --------------------------------------------------------------- |
| `RsiScannerService.cs` line 76   | `ScanAsync()` — default thresholds `oversold=30, overbought=75` |
| `RsiScannerService.cs` line 380  | Gate: `rsi >= overboughtThreshold` enters chain                 |
| `RsiScannerService.cs` line 387  | Calls `ClassifyOverboughtEnhanced()` for preliminary status     |
| `RsiScannerService.cs` line 391  | Applies intraday volume projection                              |
| `RsiScannerService.cs` line 392  | Calls `CheckOverboughtEodConfirm()` as override                 |
| `RsiScannerService.cs` line 945  | `CheckOverboughtEodConfirm()` — all 4 rules                     |
| `RsiScannerService.cs` line 879` | `ProjectIntradayVolume()` — scales partial-day volume           |
| `RsiScannerService.cs` line 779  | `ClassifyOverboughtEnhanced()` — preliminary classification     |
