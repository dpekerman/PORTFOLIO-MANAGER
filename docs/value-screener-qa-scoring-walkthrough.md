# Value Screener — QA Scoring Calculation Walkthrough (Engine v2)

**Purpose:** Step-by-step documentation of every calculation in the Capital Valuation scoring engine
for QA review, test case design, and audit trail.

---

## Overview

The scoring engine produces a **0–10 normalized composite score** from 5 independent factors.
v2 uses real accounting data from `v10/finance/quoteSummary` (6 modules), not just the v7 quote stream.

```
COMPOSITE SCORE = (EarnedPts / MaxAvailPts) × 10   [dynamic normalization]

 Factor   Name                   Max Pts   Primary Data Source
 ──────   ─────────────────────  ───────   ──────────────────────────────────────────────
   F1     Valuation Anchor        +3.0     EBIT / EnterpriseValue  (EV/EBIT earnings yield)
                                           Fallback: 1 / TrailingPE
   F2     Cash Sufficiency        +2.0     FreeCashFlow / MarketCap  (real FCF Yield)
                                           = (OperatingCashFlow − CapEx) / MarketCap
   F3     Asset Utilization       +1.0     PriceToBook (standard)
                                           REIT: Price/FFO; Financials: P/B ≤ 2.5
   F4     Fundamental Health      +3.0     Real Piotroski F-Score (9 accounting signals)
                                           NONE depend on PE ratio
   F5     Capital Efficiency      +1.0     ROIC = EBIT / (TotalAssets − CurrentLiabilities)
                                           Fallback: ReturnOnEquity; Fallback2: EY/P/B
                                  ─────
                  THEORETICAL MAX  10.0   (if all factors available)
```

### Dynamic Normalization
If a factor cannot be calculated (missing data), its weight is excluded from BOTH sides:
- `EarnedPts` = sum of points scored on available factors only
- `MaxAvailPts` = sum of max points for factors that had data
- `Score = (EarnedPts / MaxAvailPts) × 10` → rescaled to base-10

**Example:** Company with no CapEx data → F2 excluded.
- Available: F1(3)+F3(1)+F4(3)+F5(1) = 8 pts max
- Earned: F1(3)+F3(1)+F4(3)+F5(0) = 7 pts
- Score = (7/8) × 10 = **8.75** rather than 7/10 = 7.0

---

## Factor 1 — Valuation Anchor: Earnings Yield  (max +3.0 pts)

### Formula
```
PRIMARY:  EarningsYield (%) = (EBIT / EnterpriseValue) × 100
FALLBACK: EarningsYield (%) = (1 / TrailingPE) × 100
```
EBIT = Earnings Before Interest & Tax (from incomeStatementHistory)
EV = Enterprise Value (from defaultKeyStatistics)
This is the true Joel Greenblatt Magic Formula metric — measures earning power relative to true asset cost.

### Score Assignment

| Condition | Points |
|---|---|
| EarningsYield ≥ 8.0 % | **+3.0** (exceptional capital efficiency) |
| EarningsYield ≥ 5.0 % and < 8.0 % | **+1.5** (solid earnings power) |
| EarningsYield < 5.0 % (or no data) | **+0.0** / factor excluded |

### QA Test Cases

| EBIT ($M) | EV ($M) | EarningsYield | Expected F1 |
|---|---|---|---|
| 500 | 5,000 | 10.0 % | +3.0 |
| 400 | 7,500 | 5.33 % | +1.5 |
| 200 | 8,000 | 2.5 % | +0.0 |
| 0 (loss) | 4,000 | 0 % → falls back to TrailingPE | varies |
| No EV data, TrailingPE=10 | — | 10.0 % (fallback) | +3.0 |

---

## Factor 2 — Cash Sufficiency: Real FCF Yield  (max +2.0 pts)

### Formula
```
FreeCashFlow = OperatingCashFlow − |CapitalExpenditures|
FCFYield (%) = (FreeCashFlow / MarketCap) × 100

Fallback (if FCF unavailable): OCF / MarketCap
```
Both OCF and CapEx come from `cashflowStatementHistory.cashflowStatements[0]`
(most recent annual period).
Yahoo reports CapEx as a negative number — the engine stores absolute value.

### Score Assignment

| Condition | Points |
|---|---|
| FCFYield ≥ 6.0 % | **+2.0** (strong free-cash generation) |
| FCFYield ≥ 3.0 % and < 6.0 % | **+1.0** |
| FCFYield < 3.0 % (or no cashflow data) | **+0.0** / factor excluded |

### QA Test Cases

| OCF ($M) | CapEx ($M) | MarketCap ($B) | FCF ($M) | FCFYield | Expected F2 |
|---|---|---|---|---|---|
| 2,000 | 400 | 20 | 1,600 | 8.0 % | +2.0 |
| 1,200 | 800 | 20 | 400 | 2.0 % | +0.0 |
| 3,000 | 500 | 30 | 2,500 | 8.33 % | +2.0 |
| 0 | 0 | 20 | 0 | — | excluded |

---

## Factor 3 — Asset Utilization (max +1.0 pts)

### Sector Routing

| Sector | Metric Used | Threshold |
|---|---|---|
| **Standard** | Price-to-Book | P/B ≤ 1.5 |
| **Financial Services / Banks** | Price-to-Book (relaxed) | P/B ≤ 2.5 |
| **Real Estate (REITs)** | Price/FFO proxy (FFO ≈ OCF / est. shares) | P/FFO ≤ 12 |

### QA Test Cases

| Sector | Metric | Value | Expected F3 |
|---|---|---|---|
| Technology | P/B | 1.2 | +1.0 |
| Technology | P/B | 2.0 | +0.0 |
| Financial Services | P/B | 2.1 | +1.0 (≤ 2.5) |
| Real Estate | P/FFO | 10.5 | +1.0 (≤ 12) |
| Real Estate | P/FFO | 15.0 | +0.0 |
| Any | P/B = 0 (no data) | — | excluded |

---

## Factor 4 — Fundamental Health: Real Piotroski F-Score  (max +3.0 pts)

### Philosophy
The 9 signals check **accounting realities** — profitability, cash generation, leverage, and efficiency.
**None depend on the P/E ratio.** A company with negative GAAP earnings (PE=0) can still pass
signals 4, 5, 6, 7, 8, 9 if its cash flow, balance sheet, and operations are healthy.

### 9 Signals (each 0 or 1)

| # | Signal | Condition | Data Source |
|---|---|---|---|
| 1 | Positive Earnings | `netIncome > 0` OR `roa > 0` | incomeStatementHistory / financialData |
| 2 | Positive OCF | `operatingCashFlow > 0` | cashflowStatementHistory |
| 3 | ROA Quality | `returnOnAssets > 0.01` (>1%) | financialData.returnOnAssets |
| 4 | Accrual Quality | `operatingCashFlow > netIncome` | CF vs IS cross-check |
| 5 | Leverage Declining | `debtToEquity < 1.5` | financialData.debtToEquity |
| 6 | Liquidity OK | `currentRatio > 1.0` | financialData.currentRatio |
| 7 | No Dilution Proxy | `revenueGrowth > 0` | financialData.revenueGrowth |
| 8 | Gross Margin Positive | `profitMargins > 0` | defaultKeyStatistics.profitMargins |
| 9 | Asset Turnover OK | `totalRevenue / totalAssets > 0.30` | IS + BS cross-check |

### Dynamic Piotroski Scoring
- Only signals where data is **available** are checked (minimum 4 required for F4 to be scored)
- `pioRatio = signalsPassed / signalsAvailable`

| pioRatio | Points |
|---|---|
| ≥ 0.77 (≈ 7/9) | **+3.0** |
| ≥ 0.44 (≈ 4/9) | **+1.5** |
| < 0.44 | **+0.0** |

### QA Test Cases

**Test A — Healthy cash-generating company (e.g., RY.TO):**

| Signal | Condition | Result |
|---|---|---|
| 1. Positive Earnings | NetIncome > 0 | ✓ |
| 2. Positive OCF | OCF > 0 | ✓ |
| 3. ROA > 1% | 1.2% | ✓ |
| 4. OCF > NI | OCF 4B > NI 3B | ✓ |
| 5. D/E < 1.5 | 0.8 | ✓ |
| 6. Current Ratio > 1 | 1.3 | ✓ |
| 7. Rev Growth > 0 | +5% | ✓ |
| 8. Profit Margins > 0 | 18% | ✓ |
| 9. Asset Turnover > 0.3 | 0.05 (bank!) | ✗ (banks have low turnover) |
| **Result** | **8/9 = 0.89** | **+3.0 pts** |

**Test B — Loss-making company with good cash flow (e.g., BCE.TO):**

| Signal | Condition | Result |
|---|---|---|
| 1. Positive Earnings | NI < 0 (GAAP loss) | ✗ |
| 2. Positive OCF | OCF > 0 (still cash-positive) | ✓ |
| 3. ROA > 1% | -0.5% (GAAP) | ✗ |
| 4. OCF > NI | OCF 3B > NI -2B (yes!) | ✓ |
| 5. D/E < 1.5 | 2.1 (high) | ✗ |
| 6. Current Ratio > 1 | 0.7 (below 1) | ✗ |
| 7. Rev Growth > 0 | -2% | ✗ |
| 8. Profit Margins > 0 | -8% | ✗ |
| 9. Asset Turnover | 0.15 | ✗ |
| **Result** | **2/9 = 0.22** | **+0.0 pts** |
> Note: Under the OLD engine BCE would have scored 3/9 only because of DividendYield signals.
> Under v2 it correctly scores 0 because its accounting health is genuinely weak.

---

## Factor 5 — Capital Efficiency: ROIC  (max +1.0 pts)

### Formula
```
PRIMARY:  ROIC (%) = (EBIT / InvestedCapital) × 100
          where InvestedCapital = TotalAssets − CurrentLiabilities

FALLBACK1: ROE (%) = ReturnOnEquity × 100  (from financialData)
FALLBACK2: EarningsYield / PriceToBook  (simplified proxy)
```

### Score Assignment

| Condition | Points |
|---|---|
| ROIC ≥ 10.0 % | **+1.0** |
| ROIC < 10.0 % (or no data) | **+0.0** / excluded |

### QA Test Cases

| EBIT ($M) | TotalAssets ($B) | CurrLiab ($B) | IC ($B) | ROIC | Expected F5 |
|---|---|---|---|---|---|
| 500 | 4.0 | 0.8 | 3.2 | 15.6% | +1.0 |
| 200 | 5.0 | 1.0 | 4.0 | 5.0% | +0.0 |
| No data | — | — | — | — | excluded |

---

## Technical State Classification

Same as v1 — determined from RSI(14), volumeRatio, 52-week proximity.
**RSI Scanner always runs in Enhanced mode** (MACD histogram momentum, strict candle-close state machine).

| Priority | State | Condition |
|---|---|---|
| 1 | HighVolumeExhaustion | volumeRatio ≥ 2.5 AND rsi ≥ 65 |
| 2 | DeepValueReversal | rsi < 35 AND price ≤ 52wkLow × 1.20 |
| 3 | OverboughtMomentum | rsi ≥ 72 AND price ≥ 52wkHigh × 0.90 |
| 4 | OverboughtPullback | rsi ∈ [55, 72) AND price ≥ 52wkHigh × 0.80 |
| 5 | SidewaysConsolidation | rsi ∈ [42, 62] |
| 6 | MeanReversion | everything else |

---

## Tier + Action Trigger Logic

| Score | Tier |
|---|---|
| ≥ 8.0 | HighConviction |
| ≥ 5.0 | FairValue |
| < 5.0 | ValueTrap |

| Priority | Action Trigger | Conditions |
|---|---|---|
| 1 | ValueTrapWarning | tier = ValueTrap |
| 2 | HoldRideTrend | techState = OverboughtMomentum (any tier) |
| 3 | AccumulateYield | tier = HighConviction AND DeepValueReversal AND divYield ≥ 2% |
| 4 | AccumulateValue | tier = HighConviction AND DeepValueReversal AND piotroski ≥ 7 |
| 5 | BuyLimitAlert | tier = HighConviction (other states) |
| 6 | Observe | tier = FairValue |

---

## End-to-End Example: RY.TO (Royal Bank of Canada)

Assumed data (annual):

| Input | Value |
|---|---|
| EBIT | $9.2B |
| EnterpriseValue | $210B |
| OCF | $12.5B |
| CapEx | $1.1B |
| MarketCap | $195B |
| FCF | $11.4B |
| P/B | 1.8 (Financial sector) |
| TotalAssets | $2,100B |
| CurrentLiabilities | $1,800B |
| IC | $300B |
| ROIC | 9.2/300 = 3.07% |
| NetIncome | $7.5B |
| ROA | 0.4% |
| D/E | 0.85 |
| Current Ratio | 1.05 |
| Rev Growth | +5% |
| Profit Margins | 25% |
| Asset Turnover | 9.2B/2100B = 0.004 (bank → ✗) |
| RSI | 48 |
| VolumeRatio | 1.0 |
| Price | $155 | 52wk H/L | $165/$130 |

**Calculation:**

```
F1: EY = 9200/210000 × 100 = 4.38%  → F1 = 0.0  (< 5%)      [max 3.0]
F2: FCFYield = 11400/195000 × 100 = 5.85%  → F2 = 1.0        [max 2.0]
F3: Sector=Financial, P/B=1.8 ≤ 2.5  → F3 = 1.0             [max 1.0]
F4: Piotroski: 1(NI>0)✓ 2(OCF>0)✓ 3(ROA=0.4%<1%✗) 4(OCF>NI✓)
           5(D/E=0.85<1.5✓) 6(CR=1.05>1✓) 7(RevGrowth>0✓)
           8(Margin>0✓) 9(Turnover=0.004<0.3✗)
    = 7/9 = 0.78 ≥ 0.77  → F4 = 3.0                         [max 3.0]
F5: ROIC = 3.07%  → F5 = 0.0                                  [max 1.0]

EarnedPts = 0 + 1 + 1 + 3 + 0 = 5.0
MaxAvail  = 3 + 2 + 1 + 3 + 1 = 10.0
Score = (5/10) × 10 = 5.0

Tier: FairValue (5.0 ≤ 5.0 < 8.0)
TechState: SidewaysConsolidation (RSI 48 ∈ [42,62])
Action: Observe
```

---

## Key v1 → v2 Differences

| Aspect | v1 (Old) | v2 (New) |
|---|---|---|
| F1 Data | 1/TrailingPE only | EBIT/EV (true); 1/PE fallback |
| F2 Data | 1/ForwardPE (flawed) | Real FCF = OCF − CapEx |
| F3 REITs | P/B (wrong for REITs) | Price/FFO proxy |
| F3 Financials | P/B ≤ 1.5 (too strict) | P/B ≤ 2.5 |
| F4 Piotroski | PE-dependent (broke when PE=0) | 9 pure accounting signals |
| F5 ROIC | EY/P/B proxy | EBIT/InvestedCapital (real) |
| Normalization | Always /10 (penalizes missing data) | Dynamic: score/(available max) × 10 |
| Sector routing | None | REIT and Financial sector differentiation |
| API layer | v7/quote only | v10/quoteSummary (6 modules) + v7 price |
| RSI mode | Legacy | Enhanced (always) |

---

*Generated: 2026-06-14 | Portfolio Manager v2.x | Scoring Engine: ValueScreenerService.cs (v2)*
