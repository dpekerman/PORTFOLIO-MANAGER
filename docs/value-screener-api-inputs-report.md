# Value Screener — API Input Fields Audit Report

**Purpose:** Identify which data fields the scoring engine needs from external providers (Yahoo Finance),
which fields are actually returned, and which are often missing/zero for TSX-listed stocks.

---

## 1. Fields Required by the Scoring Engine

| Field                         | Used For                                                              | Source Endpoint                                       |
| ----------------------------- | --------------------------------------------------------------------- | ----------------------------------------------------- |
| `trailingPE`                  | Earnings Yield = 1/TrailingPE × 100 %                                 | Yahoo v7/finance/quote                                |
| `forwardPE`                   | FCF Yield proxy = 1/ForwardPE × 100 %                                 | Yahoo v7/finance/quote                                |
| `priceToBook`                 | Asset Utilization + Piotroski signals 6/7 + ROIC proxy denominator    | Yahoo v7/finance/quote                                |
| `trailingAnnualDividendYield` | Piotroski signals 8/9; AccumulateYield trigger                        | Yahoo v7/finance/quote                                |
| `currentPrice`                | ROIC ratio denominator; technical state proximity checks              | Yahoo v7/finance/quote                                |
| `week52High`                  | OverboughtMomentum / OverboughtPullback detection (≥90% of 52wk high) | Yahoo v7/finance/quote                                |
| `week52Low`                   | DeepValueReversal detection (≤ 52wk low × 1.20)                       | Yahoo v7/finance/quote                                |
| `rsi` (14-period)             | All 6 TechnicalState classifications                                  | Calculated from Yahoo v8/finance/chart                |
| `volumeRatio`                 | HighVolumeExhaustion detection (volRatio ≥ 2.5 AND RSI ≥ 65)          | Calculated from Yahoo v8/finance/chart                |
| `targetMeanPrice`             | Analyst Target column in RSI Scanner (NOT used in scoring)            | Yahoo v10/finance/quoteSummary (financialData module) |

---

## 2. Observed Missing Values by Ticker Type

### TSX-Listed Stocks (`.TO` suffix)

| Field                            | Reliability       | Notes                                                                                                           |
| -------------------------------- | ----------------- | --------------------------------------------------------------------------------------------------------------- |
| `trailingPE`                     | ⚠️ **Partial**    | Yahoo returns 0 for ~30% of TSX stocks (e.g., negative-earnings companies, trusts, REITs reporting FFO not EPS) |
| `forwardPE`                      | ⚠️ **Low**        | Only available if Yahoo aggregates analyst EPS estimates; missing for small/mid-cap TSX stocks                  |
| `priceToBook`                    | ⚠️ **Partial**    | Available for banks/industrials; often 0 for trusts, REITs, partnerships                                        |
| `trailingAnnualDividendYield`    | ✅ **Good**       | Generally populated when dividends are paid; zero for non-dividend payers (correct)                             |
| `week52High / Low`               | ✅ **Reliable**   | Populated from `fiftyTwoWeekHigh/Low` in v7 response                                                            |
| `targetMeanPrice` (v7)           | ❌ **Unreliable** | Yahoo v7 quote field is 0 for almost all TSX stocks — analyst data requires paid tier                           |
| `targetMeanPrice` (quoteSummary) | ⚠️ **Better**     | `v10/finance/quoteSummary?modules=financialData` returns analyst consensus for major TSX stocks                 |

### US-Listed Stocks (no suffix / `.` none)

| Field                         | Reliability | Notes                                                      |
| ----------------------------- | ----------- | ---------------------------------------------------------- |
| `trailingPE`                  | ✅ **Good** | Reliable for large/mid-cap US equities                     |
| `forwardPE`                   | ✅ **Good** | Available for most S&P 500 constituents                    |
| `priceToBook`                 | ✅ **Good** | Reliable for most equities                                 |
| `trailingAnnualDividendYield` | ✅ **Good** | Correct for dividend payers; 0 for growth stocks (correct) |
| `targetMeanPrice`             | ✅ **Good** | Populated via v7 quote for most US large-caps              |

---

## 3. Impact on Scoring When Fields Are Missing

When a field is 0 (missing/unavailable), the scoring engine behaves as follows:

| Missing Field        | Score Impact                                                                                                                                                                     |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `trailingPE = 0`     | **F1 = 0** (no EarningsYield score, max -3 pts). Piotroski signals 1, 2, 3, 4, 5 all fail → **F4 = 0** (-3 pts). ROIC proxy = 0 → **F5 = 0** (-1 pt). **Total max lost: -7 pts** |
| `forwardPE = 0`      | **F2 = 0** (no FCF proxy score, max -2 pts). Piotroski signal 5 fails. **Total max lost: -2 pts**                                                                                |
| `priceToBook = 0`    | **F3 = 0** (no asset utilization score, -1 pt). Piotroski signals 6, 7 fail. ROIC denominator = 0 → F5 = 0 (-1 pt). **Total max lost: -2 pts**                                   |
| `dividendYield = 0`  | Piotroski signals 8, 9 fail. **Total max lost: -0.5 pts (from F4 partial)**                                                                                                      |
| `rsi = 0`            | TechnicalState defaults to `MeanReversion`. No score impact, but misleading state.                                                                                               |
| `week52High/Low = 0` | TechnicalState proximity checks fail → may misclassify as `MeanReversion` instead of `DeepValueReversal` or `OverboughtMomentum`.                                                |

---

## 4. Stocks Likely to Show Low/Incorrect Scores

The following **TSX stock categories** will consistently show low scores due to data gaps:

1. **REITs** (`-UN.TO`): Report FFO/unit instead of GAAP EPS → `trailingPE` and `priceToBook` often 0
   - Example: `CAR-UN.TO`, `DIR-UN.TO`, `AP-UN.TO`, `HR-UN.TO`
2. **Income Trusts / Royalty Companies**: No standard PE ratio
   - Example: `BEP-UN.TO`, `INE.TO`

3. **Financial Companies with Unusual PE**: Banks/insurance may have PE but not PB from Yahoo
   - Example: `MFC.TO`, `SLF.TO`

4. **Negative Earnings Companies**: PE undefined when EPS < 0
   - Example: `BCE.TO` (currently reporting losses), `OTEX.TO`

5. **Small-Cap / Thinly Traded TSX**: Yahoo provides minimal analyst coverage
   - Forward PE, analyst target often 0

---

## 5. Recommended Improvements (Future Backlog)

| Priority  | Improvement                                                                                                    | Rationale                                         |
| --------- | -------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| 🔴 High   | Use `marketCap` and `enterpriseValue` from Yahoo `defaultKeyStatistics` module for true EV/EBIT Earnings Yield | Current 1/PE proxy is imprecise                   |
| 🔴 High   | Use `totalCashPerShare` and `revenuePerShare` for FCF yield instead of 1/ForwardPE                             | ForwardPE is missing for 60%+ of TSX stocks       |
| 🟡 Medium | Add REIT-specific scoring: replace PB with Price/FFO or Price/NAV                                              | PB is meaningless for real-estate trusts          |
| 🟡 Medium | Add `returnOnEquity` and `debtToEquity` from `financialData` module                                            | Direct Piotroski ROE and leverage signals         |
| 🟢 Low    | Cache quoteSummary analyst targets per symbol (15-min TTL)                                                     | Avoid per-symbol HTTP calls on every analysis run |
| 🟢 Low    | Add `numberOfAnalystOpinions` threshold (e.g., require ≥ 3 analysts) before showing analyst target             | Avoid spurious single-analyst targets             |

---

## 6. How to Verify Missing Inputs in Real Time

The backend logs at `DEBUG` level when fundamentals are populated:

```
dotnet run --project PortfolioManager.Api --environment Development
# Then watch for lines like:
# [DBG] Analyst target for RY.TO (RY.TO): 165.50
# [DBG] Sector for ENB.TO (ENB.TO): Energy / Oil & Gas Midstream
```

To check what Yahoo actually returns for a specific symbol, call the backend debug endpoint:

```http
GET /api/stocks/quote?symbol=RY.TO
```

Response includes all fields: `trailingPE`, `forwardPE`, `priceToBook`, `dividendYield`, `targetMeanPrice`, `week52High`, `week52Low`.

---

_Generated: 2026-06-14 | Portfolio Manager v1.x_
