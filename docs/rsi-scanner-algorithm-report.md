# RSI Scanner — How It Works: Plain-English Algorithm Guide

> **Purpose:** This document explains exactly how the RSI Scanner calculates every number you
> see on screen, what makes a signal appear or disappear, and — critically — **why a stock like
> GRGD.TO can appear in Ad-Hoc analysis but NOT in the Oversold Chain.**

---

## 1. The Two Scan Modes

The scanner has two completely separate entry points:

| Mode                                     | Where does it get symbols?                       | When does it run?                       |
| ---------------------------------------- | ------------------------------------------------ | --------------------------------------- |
| **TSX Scan (Oversold/Overbought Chain)** | A hard-coded list of 50 large/mid-cap TSX stocks | On page load and when you click Refresh |
| **Ad-Hoc Symbol Analysis**               | Whatever tickers you type into the input box     | When you click "Analyze"                |

These two chains **never share results.** A stock you type in Ad-Hoc will **only** appear in the
Ad-Hoc results section below, never in the Oversold/Overbought Chain headers.

---

## 2. Why GRGD.TO Did NOT Appear in the Oversold Chain

**Simple answer:** GRGD.TO is not in the 50-symbol TSX watchlist that the main scan covers.

The scanner maintains a fixed list of liquid TSX stocks (RY.TO, TD.TO, ENB.TO, SHOP.TO, etc.).
It does NOT scan all stocks on the TSX — only those 50 symbols. GRGD.TO is a smaller-cap name not
included in that curated list.

When you entered GRGD.TO in the Ad-Hoc box, the engine ran the exact same calculation logic and
correctly identified it as oversold (RSI 22.4). That result **is legitimate** — the algorithm
worked perfectly. The stock just isn't in the automatic nightly watchlist.

**How to fix this permanently:** You can add GRGD.TO to the `TsxWatchlist` array in
`RsiScannerService.cs` and it will appear in future scans automatically.

---

## 3. Step-by-Step: How a Single Stock Is Analyzed

### Step 1 — Fetch Historical Candles

The engine calls Yahoo Finance for **2 years of daily candles** (open, high, low, close, volume).
This gives ~500 trading days — enough for all the indicators below. If Yahoo is unavailable or
returns an error, the stock is silently skipped.

### Step 2 — Calculate RSI (14-period, Wilder's Smoothed Method)

RSI measures how "tired" a price trend is on a 0–100 scale.

1. For each day, calculate the price change vs. the previous day.
2. Separate the changes into **gains** (positive) and **losses** (negative).
3. Calculate the average gain and average loss over the first 14 days (simple average to seed).
4. For every subsequent day, use Wilder's smoothing:
   - `AvgGain = (AvgGain × 13 + Today's Gain) / 14`
   - `AvgLoss = (AvgLoss × 13 + Today's Loss) / 14`
5. `RS = AvgGain / AvgLoss`
6. `RSI = 100 − (100 / (1 + RS))`

- **RSI ≤ threshold (default 30)** → stock enters **Oversold** consideration
- **RSI ≥ threshold (default 75)** → stock enters **Overbought** consideration
- **Between thresholds** → `Neutral` — excluded from both chains entirely

### Step 3 — Calculate 5 Supporting Indicators

These are calculated from the same 2-year candle data and used to confirm or strengthen the
primary RSI signal.

#### A. Stochastic %K (14-period Fast)

Measures where today's close sits within the recent 14-day high/low range.

```
%K = (Close − Lowest Low[14]) / (Highest High[14] − Lowest Low[14]) × 100
```

- **Confirms oversold** when %K < 20 (price near the bottom of its recent range)
- **Confirms overbought** when %K > 80

#### B. MACD (12, 26, 9)

Uses three exponential moving averages to detect momentum direction.

1. `EMA12` = 12-day EMA of close prices
2. `EMA26` = 26-day EMA of close prices
3. `MACD Line` = EMA12 − EMA26
4. `Signal Line` = 9-day EMA of the MACD Line
5. `Histogram` = MACD Line − Signal Line
6. `Histogram Delta (Δhist)` = today's histogram − yesterday's histogram

The **histogram delta** is the key insight: when the delta turns positive while the histogram is
still negative, selling pressure is weakening _before_ the MACD lines actually cross. This is
the "Enhanced" mode's early warning mechanism.

| Crossover                                 | Meaning                                           |
| ----------------------------------------- | ------------------------------------------------- |
| Histogram flips from negative to positive | Bullish crossover                                 |
| Histogram flips from positive to negative | Bearish crossover                                 |
| No sign change                            | Uses position of MACD vs Signal lines as fallback |

#### C. Bollinger Bands (20-period, ±2σ)

1. `Middle Band` = 20-day simple moving average of close
2. `Upper Band` = Middle + 2 × standard deviation
3. `Lower Band` = Middle − 2 × standard deviation

A **Bollinger Breakout** is when the price is outside the bands:

- Price < Lower Band → breakout to downside (confirms oversold)
- Price > Upper Band → breakout to upside (confirms overbought)

#### D. Volume Signal

Compares today's volume to the 20-day average volume (excluding today):

- `VolumeRatio = TodayVolume / 20-DayAvgVolume`
- ≥ 1.3× → **"Validated"** (institutional participation, confirming the move)
- < 0.8× → **"Low-Volume Trap"** (weak conviction, warns against acting)
- Otherwise → **"Neutral"**

#### E. 50-Day and 200-Day Moving Average Deviation

- `Deviation = (Close − SMA) / SMA × 100%`
- Shows how far the stock has stretched from its medium and long-term averages
- A stock far below its 200 DMA is deeply oversold on a macro basis

### Step 4 — Classify the Signal (Legacy vs Enhanced Mode)

The scanner has two logic modes. The **Enhanced** mode is stricter and more forward-looking.

#### Legacy Mode Logic

For **Oversold**:
| Trigger | Status | Condition |
|---|---|---|
| 1 | **Confirmed** | Close > previous day's high AND volume ≥ 1.3× average |
| 2 | **Confirmed** | Wide-range candle closing in the top 25% of its day range |
| 3 | **Confirmed** | Gap-down open followed by aggressive intraday recovery |
| 4 | **Early Warning** | RSI < 25 (extreme reading) |
| Default | **Early Warning** | RSI below 30 but no price-action trigger yet |

For **Overbought**:
| Trigger | Status | Condition |
|---|---|---|
| 1 | **Confirmed** | Close < previous day's low (distribution break) |
| 2 | **Confirmed** | Wide-range candle closing in the bottom 25% of its day range |
| 3 | **Confirmed** | Gap-up open followed by immediate intraday reversal |
| 4 | **Early Warning** | RSI > 80 (extreme reading) |
| Default | **Early Warning** | RSI above 75 but no distribution trigger yet |

#### Enhanced Mode Logic (Strict State Machine)

**Enhanced Confirmed (Oversold)** requires ALL of:

- Candle closed in the upper half of today's range (buyers defended price)
- AND at least one of: volume ≥ 1.3× OR histogram delta turned positive

**Enhanced Confirmed (Overbought)** requires ALL of:

- Candle closed in the lower half of today's range (sellers defended price)
- AND at least one of: volume ≥ 1.3× OR histogram delta turned negative

If these conditions aren't met, the stock is **Early Warning** — a watch-and-wait state.

### Step 5 — Reversal Probability Score

A score from 0–5 is built by checking each of these conditions:

| Condition                  | Oversold point | Overbought point |
| -------------------------- | -------------- | ---------------- |
| RSI extreme (< 25 or > 80) | +1             | +1               |
| Stochastics confirm        | +1             | +1               |
| MACD confirms direction    | +1             | +1               |
| Bollinger breakout         | +1             | +1               |
| Volume validated           | +1             | +1               |

- Score ≥ 4 → **"High"** probability
- Score ≥ 2 → **"Medium"** probability
- Score < 2 → **"Low"** probability

### Step 6 — Enrich with Analyst Data

After all calculations, the engine makes a second call to Yahoo Finance to fetch:

- **Analyst 1-year consensus target price** (with percentage upside)
- **52-week high and low** (for context)

These are displayed in the "Analyst Target" column.

### Step 7 — Sort and Display

- **Oversold Chain:** Confirmed signals first, then sorted by RSI ascending (most oversold at top)
- **Overbought Chain:** Confirmed signals first, then sorted by RSI descending (most overbought at top)
- **Neutral stocks are never shown** in the main chains — they are silently excluded

---

## 4. What "Early Warning" vs "Confirmed" Means in Practice

| Status            | What it means                                                                                                                        | Suggested action                                                              |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------- |
| **Confirmed**     | A specific price-action or volume trigger has been met. The move has structural backing.                                             | Highest-conviction entry candidates. Still requires your own risk management. |
| **Early Warning** | RSI has crossed the threshold but no candle/volume trigger yet. The stock is oversold/overbought but hasn't shown a reversal signal. | Watch it. Set a price alert. Don't act yet in strict risk-managed strategies. |

---

## 5. Scan Execution Notes

- **Rate limiting:** The engine processes 3 symbols at a time with a 1.5-second pause between
  batches. For 50 symbols, the full scan takes approximately 25 seconds.
- **Retry logic:** If Yahoo returns HTTP 429 (rate limited), the engine waits and retries up to 3
  times with exponential backoff before skipping that symbol.
- **Demo fallback:** If the entire scan fails (no network, firewall, etc.), the UI shows hardcoded
  demo data so the page never appears broken.
- **Minimum data requirement:** A stock needs at least 30 days of candle data to calculate RSI.
  Fewer than that → skipped entirely.

---

## 6. Known Limitations

| Limitation                                 | Impact                                                              |
| ------------------------------------------ | ------------------------------------------------------------------- |
| Fixed 50-symbol watchlist                  | Smaller-cap stocks like GRGD.TO are never auto-scanned              |
| Data is from Yahoo Finance free tier       | Delay of ~15 minutes during market hours                            |
| RSI Signal (EMA of RSI) not yet calculated | No smoothed RSI trend line — **planned addition**                   |
| No persistence                             | Each page load triggers a fresh scan; no historical signal tracking |
| Single time frame (daily)                  | Intraday signals are not detected                                   |

---

## 7. Data Flow Summary

```
User opens Scanner page
       │
       ▼
Backend: fetch 2y daily candles for each of 50 TSX symbols (batches of 3)
       │
       ▼
For each symbol:
  ├─ Calculate RSI(14) → is it ≤30 or ≥75?
  │      If neither → NEUTRAL → skip
  ├─ Calculate Stochastic %K, MACD histogram, Bollinger Bands, Volume ratio, DMA devs
  ├─ Classify signal: Legacy or Enhanced state machine → Confirmed or Early Warning
  └─ Calculate reversal probability score
       │
       ▼
Enrich all results with analyst targets + 52-week range (second Yahoo call)
       │
       ▼
Sort: Confirmed first, then by RSI extremity
       │
       ▼
Return JSON → Frontend renders Oversold Chain + Overbought Chain

User types GRGD.TO in Ad-Hoc box → same pipeline runs for that single symbol
→ result shown in Ad-Hoc section ONLY (never merged into main chains)
```
