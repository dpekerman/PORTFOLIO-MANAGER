# Financial Calculation Analysis & Improvement Roadmap — 2026-08-18

Portfolio Manager v2. Compares current backend calculations against professional trading
platforms (Interactive Brokers, Bloomberg Terminal, TradingView, Wealthsimple Trade, TD
Direct Investing) and identifies gaps, algorithmic issues, and improvement opportunities.

---

## Current Calculation Inventory (What Is Already Good)

| Calculation               | Implementation                     | Notes                                          |
| ------------------------- | ---------------------------------- | ---------------------------------------------- |
| RSI(14)                   | Wilder's Smoothed                  | Correct per TradingView standard               |
| RSI Signal (9-EMA)        | EMA of full RSI series             | Good leading indicator                         |
| MACD (12,26,9)            | Full histogram + slope detection   | Above average — histogram delta is a real edge |
| ATR(14)                   | Wilder's Smoothed                  | Correct                                        |
| Bollinger Bands (20, ±2σ) | Standard                           | Correct                                        |
| Stochastic %K (14)        | Fast (raw) %K                      | Partially complete — see Issue 3               |
| Fibonacci Retracement     | Swing high/low, 4 levels           | Good zone classification                       |
| Portfolio Beta            | Weighted average, sector fallback  | See Issue 7                                    |
| Value Screener (5-factor) | EBIT/EV, FCF, P/B, Piotroski, ROIC | Solid engine — see Issues 8–12                 |
| ATR Stop Loss             | 1.5× ATR from extreme              | Good dynamic stop methodology                  |
| Volume Ratio              | Current / 20-day SMA               | Acceptable                                     |
| EOD Volume Projection     | Linear scaling with 2× cap         | See Issue 14                                   |
| Portfolio Value History   | Daily EOD snapshot                 | Good foundation for performance analytics      |

---

## Issue 1 — No Portfolio Performance Metrics ✅ Analysis Complete

**What is currently calculated**: Simple unrealized gain = Market Value − (Average Cost × Shares).

**What professional platforms provide**:

- **Time-Weighted Return (TWRR)** — Industry standard. Eliminates distortion from cash flows. Used by every Canadian broker (TD, RBC, Questrade) in performance reporting. Formula chains sub-period returns: `TWRR = [(1+R1) × (1+R2) × … (1+Rn)] − 1`.
- **Money-Weighted Return (MWRR/IRR)** — Accounts for the timing and size of cash flows. Better for personal performance measurement.
- **Annualized Return** — `(1 + totalReturn)^(365/days) − 1` for cross-period comparisons.
- **Sharpe Ratio** — Risk-adjusted return: `(Portfolio Return − Risk-Free Rate) / StdDev(Returns)`. 1.0+ is considered good.
- **Sortino Ratio** — Like Sharpe but penalises only downside volatility: `(Portfolio Return − Risk-Free Rate) / StdDev(Negative Returns)`.
- **Maximum Drawdown** — Largest peak-to-trough decline. Critical risk assessment metric.
- **Calmar Ratio** — `Annual Return / Max Drawdown`. Efficiency of risk-taking.

**Current gap**: `PortfolioValueHistory` table already captures daily total values — the correct data foundation. The analytics layer is missing.

**Recommendation**: Add a `PerformanceAnalyticsService` that computes TWRR, Sharpe, max drawdown, and annualized return from `PortfolioValueHistories`. The data is already there. **Effort: Medium.**

---

## Issue 2 — No Risk-Adjusted Metrics ✅ Analysis Complete

**Missing metrics**:

- **Value at Risk (VaR)** — 95%/99% confidence interval loss estimate. Standard in institutional platforms.
- **Alpha** — `Portfolio Return − (RiskFreeRate + Beta × (Market Return − RiskFreeRate))`. Answers whether skill or market beta drove returns.
- **Tracking Error** — Standard deviation of return differences vs. benchmark.
- **Correlation Matrix** — Pairwise correlation between positions. Two correlated positions provide no true diversification benefit.

**Current state**: Beta is calculated (weighted average) but unused for return attribution or risk analysis.

**Recommendation**: Start with correlation matrix (uses historical price data already fetched) and max drawdown. VaR requires 30+ daily history snapshots. **Effort: Medium.**

---

## Issue 3 — Stochastic Oscillator Missing %D Line ✅ Analysis Complete

**Current**: Only Fast %K(14) is calculated. Confirmation threshold: `stochK < 20` for oversold, `stochK > 80` for overbought.

**What professional platforms use**: Both %K and %D where `%D = 3-period SMA of %K`. The tradeable signal is the **%K/%D crossover**, not just %K level.

- **Interactive Brokers / TradingView**: Display both lines; alert on crossover.
- **Slow Stochastic** (industry standard): 3-period SMA of %K creates %D; further smoothing creates the signal line.

**Impact**: The current implementation may flag false confirmations when %K is below 20 but still declining (no reversal yet). A %K cross above %D within the oversold zone is the actual reversal trigger.

**Fix**: Compute `%D = 3-period SMA of last 3 %K values`. Add `StochasticD` field to `RsiScanResult`. **Effort: Low.**

---

## Issue 4 — RSI Divergence Not Detected ✅ Analysis Complete

**Current**: `CalculateRsiSeries()` computes the full RSI series (correctly implemented). The **divergence pattern** — higher price highs + lower RSI highs = bearish divergence; lower price lows + higher RSI lows = bullish divergence — is not detected algorithmically.

The demo data hardcodes one divergence label (`"Marginal new 52-wk high, but RSI momentum diverging"`), confirming this was conceptually planned but never implemented as a calculation.

**Algorithm approach**:

1. Use the existing `CalculateRsiSeries()` output (already available).
2. Find the 2 most recent RSI peaks (overbought) or troughs (oversold) within a 20–60 bar window.
3. Compare with corresponding price peaks/troughs.
4. If price makes new high but RSI peak is lower → bearish divergence.
5. If price makes new low but RSI trough is higher → bullish divergence.

This is a significant signal quality enhancement used by TradingView, StockCharts, and Bloomberg. **Effort: Medium.**

---

## Issue 5 — MACD EMA Seeding Diverges from TradingView at Startup ✅ Analysis Complete

**Current**: `ema12 = closes.Take(12).Average()` used as the EMA seed.

**TradingView method**: Seeds the EMA with the **first closing price** (not an average), then applies the multiplier from bar 1. This is more consistent with exponential smoothing theory.

**Impact**: Minor divergence in MACD values for the first ~100 bars. With 2 years of daily data (500 bars), this resolves well before the most recent bars. **Low priority** for practical use, but worth noting if users compare to TradingView charts.

---

## Issue 6 — Bollinger Band %B and Bandwidth Not Exposed ✅ Analysis Complete

**Current**: The calculation returns (upper, middle, lower). `BollingerBreakout = true/false` is exposed.

**What is missing**:

- **%B** = `(Price − Lower) / (Upper − Lower)`. Normalises position within the bands (0 = at lower, 1 = at upper, <0 or >1 = outside). More useful than a binary breakout flag.
- **Bandwidth** = `(Upper − Lower) / Middle`. Measures band squeeze. A tight squeeze before a breakout (Bollinger Squeeze) is a key momentum setup used on TradingView, StockCharts, and Bloomberg.

**Fix**: Add `BollingerPctB` (0.0–1.0) and `BollingerBandwidth` fields to `RsiScanResult`. Both are computable from values already calculated inside `CalculateBollingerBands`. **Effort: Low.**

---

## Issue 7 — Beta Uses Wrong Benchmark for Canadian Stocks ✅ Analysis Complete

**Current**: Portfolio beta uses Yahoo Finance's beta field, which measures covariance vs. the **S&P 500**. For Canadian TSX-listed stocks, this creates a systematic issue because TSX and S&P 500 are not perfectly correlated (~0.80 historically).

**Example**: Royal Bank (RY.TO) has a Yahoo Finance beta of ~0.85 (vs. S&P 500), but vs. the TSX Composite it is ~1.0 (RY is the largest TSX component). The portfolio appears lower-risk than it actually is relative to a Canadian investor's benchmark.

**Sector fallback issue**: ~40% of TSX small/mid-cap symbols have no Yahoo Finance beta → sector proxy used with ±12–20% error.

**What professional Canadian platforms do**: Use TSX Composite (^GSPTSE) as benchmark. Regression beta = `Cov(Stock, TSX) / Var(TSX)` from historical returns.

**Recommendation**: Calculate regression beta against TSX Composite using the 2-year daily price data already fetched per symbol in `AnalyzeSymbolAsync`. This eliminates the proxy requirement entirely. **Effort: Medium.**

---

## Issue 8 — Value Screener F7 (Share Dilution) Uses Wrong Proxy ✅ Analysis Complete

**Current**: Piotroski Signal F7 (no new shares issued) is proxied by `RevenueGrowth > 0`. Revenue growth has no relationship to share dilution.

**Correct F7**: `SharesOutstanding_CurrentYear < SharesOutstanding_PreviousYear`. Yahoo Finance `v10/quoteSummary` returns `sharesOutstanding` directly.

**Impact**: A company issuing 20% new shares but growing revenue will incorrectly pass F7, inflating its Piotroski score.

**Fix**: Add `SharesOutstanding` to `FundamentalsSnapshot` and compute year-over-year change. **Effort: Low.**

---

## Issue 9 — Value Screener FCF Fallback Overestimates Capital-Intensive Sectors ✅ Analysis Complete

**Current**: When real FCF is unavailable, `FCFYield = OperatingCashFlow / MarketCap`.

**Problem**: For pipelines (Enbridge, TC Energy), utilities (Fortis, Emera), and telecoms (BCE, Telus), capital expenditures consume 30–60% of operating cash flow. Using OCF inflates FCF yield by 2–3× for these sectors.

**Example**: Enbridge (ENB.TO) OCF ~$9B, CapEx ~$5B → Real FCF ~$4B. OCF-based yield is 2.25× the real FCF yield.

**Fix**: When `FreeCashFlow` is missing but `CapitalExpenditures` is available, compute `FCF = OCF − CapEx`. Only fall back to pure OCF if both FCF and CapEx are absent. **Effort: Low.**

---

## Issue 10 — No Dividend Tracking or Total Return ✅ Analysis Complete

**Current**: Price return only. Dividends received are not tracked.

**Impact**: For TSX dividend payers (banks 4–5%, REITs 5–8%, pipelines 6–8%), ignoring dividends understates total return by 4–8% per year. A position showing +8% price gain may have a +12% total return.

**Industry standard**: Every Canadian broker (TD, RBC, CIBC, Questrade) reports **total return** (dividends reinvested) as the primary performance metric.

**Recommendation**:

1. Add a `DividendIncome` table (symbol, amount, date, account).
2. Expose total return = price return + cumulative dividends / original cost.
3. Alternative: integrate Yahoo Finance `dividends` history from `/v8/finance/chart` response (already fetched alongside OHLCV). **Effort: Medium.**

---

## Issue 11 — No Canadian Adjusted Cost Base (ACB) Tracking ✅ Analysis Complete

**Current**: `AverageCostBasis` is a single averaged cost per position. Multiple buy lots are merged.

**Canadian tax law requirement**: The ACB must be tracked precisely for capital gains reporting. It must include all purchase prices, commissions, reinvested dividends, and superficial loss adjustments.

**What competing Canadian tools offer**: Sharesight (AU/CA) and Wealthica (CA) track ACB per lot with full audit trail and capital gains reports by tax year.

**Recommendation**: Track tax lots separately (buy date, price, shares, account type). Full ACB engine requires superficial loss rules (30-day repurchase). **Effort: High. Priority: High for compliance-conscious Canadian investors.**

---

## Issue 12 — No Options Analytics (Break-Even, Intrinsic Value, Time Value) ✅ Analysis Complete

**Current**: Options tracked as positions only. Technical indicators run on the underlying stock. No options-specific analytics exist.

**Simpler immediate wins** (no option pricing model required):

- **Break-even price**: `Strike ± Premium / ContractSize` — trivial formula.
- **Intrinsic value**: `Max(0, CurrentPrice − Strike)` for calls, `Max(0, Strike − CurrentPrice)` for puts.
- **Time value**: `Premium − IntrinsicValue`.
- **Days to expiry**.

**Full Greeks** (Delta, Gamma, Theta, Vega) require Black-Scholes + implied volatility — either a live IV data source or complex calculation.

**Recommendation**: Add break-even, intrinsic, time value, and days-to-expiry to `OptionItem`. These require no pricing model. **Effort: Low.**

---

## Issue 13 — Currency Risk Not Quantified ✅ Analysis Complete

**Current**: CDR symbols (US stocks listed on TSX in CAD) are identified for beta lookups only. No currency allocation or exposure calculation exists.

**Issue**: A portfolio may have 30% USD exposure through CDRs. A 5% CAD weakening vs. USD creates a 1.5% portfolio tailwind — and vice versa. This risk is invisible.

**Recommendation**: Tag positions as CAD/USD exposure. Show currency breakdown in the Allocation screen. **Effort: Low** (CDR symbols already identified in beta service).

---

## Issue 14 — EOD Volume Projection Assumes Uniform Distribution ✅ Analysis Complete

**Current**: `ProjectIntradayVolume` scales raw volume linearly by `390 / elapsedMinutes`, capped at 2×.

**Issue**: TSX (and NYSE) volume follows a **U-curve** distribution:

- High at open (9:30–10:00 AM): ~20% of daily volume in first 30 minutes.
- Low at midday (11:00 AM–2:00 PM): ~40% over 3 hours.
- High at close (3:30–4:00 PM): ~25% of daily volume in last 30 minutes.

At 3:30 PM (360/390 elapsed), the linear method projects `1.08×` — but the final 30 minutes typically add 25% of daily volume, so the true projection should be `~1.33×`. This causes the `volRatio >= 1.5×` EOD confirm gate to be systematically harder to pass at 3:30–3:45 PM than at 4:00 PM.

**Recommendation**: Use a 30-minute bucket volume profile (13 buckets) with historical TSX distribution weights. **Effort: Medium.**

---

## Issue 15 — No Position Sizing Framework ✅ Analysis Complete

**Current**: Allocation role limits and position limits are configurable percentages. There is no calculation that recommends how many shares to buy given risk parameters.

**Professional platforms provide**:

- **ATR-based sizing** — `Shares = (Portfolio × RiskPercent) / (EntryPrice − StopLoss)`. The app already calculates both ATR and dynamic stop loss — this is one formula away.
- **Kelly Criterion** — `f* = (bp − q) / b`. Theoretically optimal but aggressive.
- **Fixed Fractional** — Risk a fixed % of portfolio per trade.

**Recommendation**: Expose an ATR-based position size recommendation on the EOD Signals page or as a scanner overlay. `SharesToBuy = (PortfolioValue × MaxRisk%) / RiskPerShare` where `RiskPerShare = EntryPrice − StopLoss`. **Effort: Low.**

---

## Priority & Decision Matrix

| #   | Improvement                                                | Impact   | Effort | Status                                         |
| --- | ---------------------------------------------------------- | -------- | ------ | ---------------------------------------------- |
| 1   | TWRR + performance metrics from existing history snapshots | High     | Medium | ✅ Analysed — awaiting implementation decision |
| 7   | Recalculate beta vs. TSX Composite (not S&P 500)           | High     | Medium | ✅ Analysed — awaiting implementation decision |
| 11  | Canadian ACB tax lot tracking                              | High     | High   | ✅ Analysed — awaiting implementation decision |
| 8   | Fix Piotroski F7 to use actual shares outstanding          | Medium   | Low    | ✅ Analysed — awaiting implementation decision |
| 9   | Fix FCF fallback for capital-intensive sectors             | Medium   | Low    | ✅ Analysed — awaiting implementation decision |
| 10  | Dividend tracking for total return                         | High     | Medium | ✅ Analysed — awaiting implementation decision |
| 3   | Add Stochastic %D line                                     | Medium   | Low    | ✅ Analysed — awaiting implementation decision |
| 4   | RSI divergence detection                                   | High     | Medium | ✅ Analysed — awaiting implementation decision |
| 12  | Options: break-even, intrinsic value, time value           | Medium   | Low    | ✅ Analysed — awaiting implementation decision |
| 15  | ATR-based position sizing recommendation                   | Medium   | Low    | ✅ Analysed — awaiting implementation decision |
| 2   | Sharpe/Sortino/Max Drawdown (needs 30+ history days)       | High     | Medium | ✅ Analysed — awaiting implementation decision |
| 6   | Bollinger %B and Bandwidth                                 | Low      | Low    | ✅ Analysed — awaiting implementation decision |
| 13  | Currency exposure tracking                                 | Low      | Low    | ✅ Analysed — awaiting implementation decision |
| 14  | Volume projection curve correction                         | Low      | Medium | ✅ Analysed — awaiting implementation decision |
| 5   | MACD seed alignment with TradingView                       | Very Low | Low    | ✅ Analysed — backlog                          |
