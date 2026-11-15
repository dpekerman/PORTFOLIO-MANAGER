# PM-029 — Trading Opportunities Heatmap (Insights → Trading Opportunities)

Status: **OPEN — implementation in progress; stays open until manual UAT and explicit sign-off.**
Scoring version: **`pm029-v2`** (Swing technical score; Strategic remains `pm029-v1`).

> **Swing-v2 is an evidence-adjusted technical ranking, not a probability of success or an executable buy recommendation.**
> Market setup and execution readiness are separate from the technical score.

## 1. Principles

- **Nothing is hard-coded from the prototypes.** Every score, sector comparison, exposure, price and decision comes from authoritative application data or a calculation named in this document. The prototype numbers are illustrative only.
- **Four independent fields.** Opportunity Score, Portfolio Fit, FINAL ACTION and Sector Heat are separate and never derived from each other.
- **One authority for FINAL ACTION.** `FinalActionService` / `FinalActionEngine` is the only source. The heatmap copies each account/lot decision verbatim and never synthesises an action from scores or colours.
- **Read-only.** There is no order, position, cash, risk-plan or broker mutation. The universe (TSX 60) is evaluated in a side-effect-free scanner mode.
- **Portfolio data never enters a score.** Ownership, cash, risk plans and weights affect only Portfolio Fit.
- **Determinism.** The same inputs and scoring version always produce identical output. Every threshold lives in `OpportunityScoringConfig`; changing any threshold requires a new version string.

## 2. Universe

The universe is the union of:
- Watchlist (all tiers)
- open holdings (all accounts and roles; non-manual lots, the same filter Final Action uses)
- active `IndexConstituents` rows with `IndexCode = 'TSX60'`

The row key is the uppercase trading ticker, with the suffix preserved. The analysis security is resolved with `SecurityAnalysisResolver`; for universe-only symbols this is done read-only, so no mapping is written. An unresolved identity keeps the row with status `IdentityUnresolved` and gives it no scores.

## 3. Scoring — Swing-v2 and Strategic-v1

### 3.0 Common rules

- **Evidence session E** = `TradingCalendar.LastCompletedSession(analysis exchange)`. Swing-v2 requires `scan.TradingDate == E`, a positive analysis price, and `ScannedAt` between the actual exchange session close and evaluation time. Otherwise the Swing score is unavailable.
- Inputs:
  - P = `CurrentPrice` of that completed bar.
  - ATR = `DailyAtr`; `0` means missing.
  - SMA200 is available only if `Has200Dma && Sma200 > 0`.
- A component is made of sub-items.
  - A missing sub-item removes its points from both *awarded* and *available max*; the component becomes **Partial**.
  - A missing component removes its whole max from the available max.
- Arithmetic is in `decimal`.
  - **Swing-v2 headline** = round-half-away-from-zero(Σawarded S1–S5 / 80 × 100); `ScoreDenominatorMax` is 80.
  - **Swing-v2 completeness %** = Σavailable S1–S5 max / 80 × 100; `AvailableMax` remains the observed evidence maximum.
  - **Strategic-v1 headline** retains its original available-evidence calculation. Existing Swing-v1 snapshots remain immutable and are presented with the legacy disclaimer.

### 3.1 Swing-v2 technical opportunity (80) — essential: S1, S3

| Comp | Max | Sub-items (points) | Missing when |
|---|---|---|---|
| S1 Trend & Structure | 25 | P > EMA20 **7** · EMA20 > SMA50 **6** · P > SMA50 **6** · P > SMA200 **6** (sub-item missing if there is no SMA200). A hard structural negative sets the component to 0 (observed) | EMA20 or SMA50 ≤ 0 |
| S2 Relative Strength | 20 | **20D diff** (pp, §4): ≥ +5.0 → **14** · ≥ +2.0 → **11** · ≥ +0.5 → **8** · neutral → **5** · ≥ −2.0 → **2** · else **0**. **60D diff**: ≥ +2.0 → **6** · ≥ +0.5 → **4** · neutral → **2** · else **0** | Both windows unavailable (one unavailable ⇒ Partial) |
| S3 RSI/MACD Momentum | 15 | **RSI(14)**: 50–65 → **6** · 45–<50 or >65–70 → **4** · 40–<45 → **2** · >70 → **1** · <40 → **0**. **RSI > RSI signal** → **3** (sub-item missing if `!RsiSignalAvailable`). **MACD**: hist > 0 & Δ > 0 → **6** · hist > 0 & Δ ≤ 0 → **4** · hist ≤ 0 & Δ > 0 → **2** · else **0** | Stale or no scan |
| S4 Volume Confirmation | 10 | **Up day** (`ChangePercent` > 0): VolRatio ≥ 1.5 → **8** · ≥ 1.0 → **6** · ≥ 0.7 → **4** · else **2**. **Down day**: ≥ 1.5 → **0** · ≥ 1.0 → **2** · else **4**. **Flat day**: **4**. `VolumeSignal == "Validated"` on an up day adds **+2** (component capped at 10) | Volume is zero or VolumeRatio ≤ 0 |
| S5 Entry Quality | 10 | **Extension** x = (P − EMA20)/ATR: −0.5 ≤ x ≤ 1.0 → **6** · 1.0 < x ≤ 2.0 → **3** · x > 2.0 → **0** · −1.5 ≤ x < −0.5 → **4** · x < −1.5 → **1**. **Chase risk**: `ChaseRisk != "Elevated"` → **2**. **Fib zone**: Normal Pullback / Value Zone / Key Fib Support → **2** · Shallow Pullback → **1** · Deep Pullback / Trend Damage → **0** (sub-item missing if empty) | ATR missing |

S6 is not part of Swing-v2 scoring. An unavailable stop, target, or Reward/Risk does not by itself make a technical Swing score unavailable.

### 3.2 Strategic-v1 (100) — essential: T1, T7

| Comp | Max | Sub-items (points) | Missing when |
|---|---|---|---|
| T1 Long-Term Trend | 20 | P > SMA200 **8** · SMA50 > SMA200 **7** · `Dma200Deviation`: 0–25 % → **5** · > 25 % → **2** · < 0 → **0** | No SMA200, or stale |
| T2 Relative Strength | 10 | **60D diff**: ≥ +5.0 → **10** · ≥ +2.0 → **8** · ≥ +0.5 → **6** · neutral → **4** · ≥ −2.0 → **2** · else **0** | 60D unavailable |
| T3 Momentum | 5 | MACD hist > 0 → **3**. `MomentumState` contains "Accelerating", or is "Positive" / "Active Buy Trigger" → **2** · other → **0** (sub-item missing if null) | Stale or no scan |
| T4 Volume | 5 | VolRatio ≥ 1.0 on an up day → **5** · < 1.0 on any day → **3** · ≥ 1.0 on a down day → **0** · ≥ 1.0 on a flat day → **3** | VolumeRatio ≤ 0 |
| T5 Entry Timing | 10 | **RSI**: 40–60 → **4** · 35–<40 or >60–70 → **2** · else **0**. **P / 52W high**: 0.75–0.95 → **3** · > 0.95 → **1** · < 0.75 → **2** (sub-item missing if the 52W high ≤ 0). **Fib zone**: as in S5, scaled to **3 / 1 / 0** | Stale or no scan |
| T6 Downside Risk | 10 | A hard structural negative sets the component to **0**. **(P − SMA200)/ATR**: 0–3 → **5** · >3–8 → **3** · > 8 → **1** · < 0 → **0**. **ATR % of P**: ≤ 2 % → **5** · ≤ 3.5 % → **3** · ≤ 5 % → **1** · > 5 % → **0** | ATR missing |
| T7 Valuation | 25 | (F1 + F2 + F3) / (available max of F1–F3) × 25 | Fewer than 2 of F1–F3 available; screener evaluation older than 7 days; identity mismatch. If the market-cap fallback was used, the factors that divide by market cap are treated as unavailable: F2 (FCF yield) and, for REITs, F3 (P/FFO proxy) |
| T8 Business Quality | 15 | (F4 + F5) / (available max) × 15. F5 is accepted from ROIC or ROE; the EY/P/B proxy makes F5 unavailable | F4 unavailable (fewer than 4 Piotroski signals), or stale |

Value Screener factor maxima: F1 Earnings Yield 3 · F2 FCF Yield 2 · F3 P/B 1 · F4 Piotroski 3 · F5 ROIC 1.

A value-trap warning (`ActionTrigger == ValueTrapWarning`) appears as an Evidence warning and is **not** subtracted from the score, to avoid double-counting with T7.

### 3.3 Status and completeness

| Status | Rule | Presentation |
|---|---|---|
| **Verified** | Completeness = 100 % and all essentials observed | Solid band colour and band label |
| **Provisional** | 70 % ≤ completeness < 100 % and all essentials observed | Band colour plus hatch and a "P" marker. The label is prefixed "Provisional", and the details banner reads "Incomplete evidence — not a verified opportunity". Hidden by the **Verified only** filter |
| **Insufficient** | Swing completeness < 70 %, **or** any Swing essential component missing | Grey "N/A" with the reasons |
| **Unavailable** | No usable scan, unresolved identity, or failed evaluation | Neutral "—" with the reasons |

### 3.4 Bands (applied to the integer headline)

| Range | Band | Colour |
|---|---|---|
| 85–100 | Strong | deep green |
| 70–84 | Attractive | green |
| 60–69 | Moderate | amber |
| 50–59 | Caution | orange |
| < 50 | Weak | red |
| N/A | none | neutral |

**Weak** (a scored value below 50) and **Unscorable** (N/A) are disjoint concepts and use disjoint filters.

### 3.5 Reason codes

Scoring reasons are stored per row and per perspective. The first five reasons below belong only to the separate setup result and do not make a Swing-v2 score unscorable. Scoring reasons appear in the Scores tab, exclusion summary and export; setup reasons appear with the calculated setup.

| Code | Meaning |
|---|---|
| `NoValidStop` | Setup only: scanner stop is missing or not below entry (U2 — never fabricated); paired with a detail code below |
| `DynamicStopMissing` | Setup only: `DynamicStopLoss` is null/zero; no stop value is inferred |
| `StopAtOrAboveEntry` | Setup only: observed `DynamicStopLoss` is at or above the long entry |
| `StopOutsideVolatilityBand` | Setup only: stop distance outside 0.5–4 ATR, or risk > 15 % |
| `NoStructuralTarget` | Setup only: no overhead level above entry + 0.5 ATR |
| `StaleScan` | Scan session ≠ E |
| `NoScan` | Scanner returned nothing for the symbol |
| `MissingEssential:<comp>` | An essential component is missing |
| `LowCompleteness` | Completeness < 70 % |
| `IdentityUnresolved` | CDR/wrapper mapping needs user input |
| `FundamentalsStale` | Value Screener evaluation older than 7 days |
| `FundamentalsMissing` | No Value Screener evaluation for the symbol |
| `FundamentalsLegacy` | Value Screener evaluation written before PM-029 (no per-factor availability) — fail-closed |
| `FundamentalsInsufficientFactors` | Fewer than 2 of F1–F3 (T7) or no usable Piotroski (T8) |
| `NoEntryPrice` | Completed close ≤ 0 |
| `EvaluationFailed` | Evaluation of the symbol failed |

**Score filter:** `70+` (default) · `50+` · `All scored` · `All (incl. unscorable)`, plus the **Show unavailable** and **Verified only** toggles.

The **exclusion summary** is computed from the snapshot and shows how many rows fall below the threshold and how many are unscorable, broken down by mutually exclusive primary reason. Selecting a reason shows only unscorable rows with that specific reason; all-reason score views remain available.

The perspectives are independent: a row whose Swing score is N/A still shows its Strategic score.

Filters include sector, industry, Sector Heat, risk, Portfolio Fit, Final Action, ownership, source, account, role and evidence freshness. The grid retains the full 39-column selector; these filters do not add or hide saved columns.

## 4. Sector Heat (stock vs sector benchmark)

| Rule | Definition |
|---|---|
| Sessions | E = the last completed session of the analysis exchange. B20 / B60 = the 20th / 60th completed session **before** E (holidays excluded) |
| Return | r = Close[E] / Close[B] − 1, for both the stock and the benchmark |
| Intraday trimming | Bars dated after E are dropped |
| Alignment | Both series need bars on E and B. Interior coverage must be ≥ 95 % of expected sessions (20D: ≤ 1 missing; 60D: ≤ 3 missing); otherwise `InsufficientObservations` |
| Calendar | The benchmark trades on the analysis ticker's exchange calendar |
| Currency | Benchmark currency must equal analysis currency; otherwise `CurrencyMismatch` |
| Adjustment | Split-adjusted `close` for both series. Any daily move > 40 % inside the window gives `PossibleCorporateAction` |
| Diff | `diffPp = (r_stock − r_bench) × 100`, stored at full precision and displayed to 0.1 with a sign |
| Neutral | Applied to the **displayed** value: \|diff₀.₁\| < 0.5 ⇒ Neutral (+0.4 is neutral, +0.5 is positive) |
| Intensity | 0.5–< 2.0 → 1 · 2.0–< 5.0 → 2 · ≥ 5.0 → 3 |
| Status | Matched · Unmapped · Stale · InsufficientObservations · CurrencyMismatch · PossibleCorporateAction · Unclassified |

**Disclosure:** "Price return (split-adjusted, dividends excluded) vs {Benchmark}, 20 completed sessions to {E}".

## 5. Calculated trade setup

| Step | Rule | Failure |
|---|---|---|
| Freshness | scan `TradingDate == E` | Unavailable (`StaleScan`) |
| Entry | Completed close P > 0 | Unavailable |
| Stop | `DynamicStopLoss` > 0 and < entry | Unavailable (`NoValidStop`, plus `DynamicStopMissing` or `StopAtOrAboveEntry`) |
| Volatility | ATR > 0; 0.5 ATR ≤ entry − stop ≤ 4 ATR; risk ≤ 15 % | Unavailable (`StopOutsideVolatilityBand`) |
| Target | Nearest level above entry + 0.5 ATR, from `UpperRailToday` (channel ≠ NONE), `NearestOpenGapAbove` and `Week52High` | Unavailable (`NoStructuralTarget`) |
| R:R | (target − entry) / (entry − stop) | — |
| Acceptability | R:R ≥ 1.5 ⇒ **Valid candidate**; otherwise **Not actionable** | — |

**Calculated candidate** and **Your saved risk plan** are separate cards.
- The saved plan is display only, never scored, and shown in the trading-security currency.
- Neither card says "Buy" or "Enter". FINAL ACTION is the only action.

**Risk category** is the worse of:
- risk-to-stop % (≤ 5 Low / ≤ 10 Medium / > 10 High)
- ATR % (≤ 2.5 Low / ≤ 5 Medium / > 5 High)

A hard structural negative makes it High.

## 6. Portfolio Fit and FINAL ACTION

- **Fit** uses the Final Action exposure and the `GrandTotalAsync` denominator (stocks + cash + options), `SinglePositionLimits` and `AllocationSectorTargets` (2 pp tolerance). Possible values: Eligible / Review / No Add / Unavailable. Rules (worst wins):
  - Allocation not single-currency, or owned exposure unknown → **Unavailable**.
  - Weight ≥ role limit → **No Add** (same comparison as the Final Action gate); headroom < 1 pp → **Review**; no configured limit or ambiguous role policy → **Review**.
  - Sector weight > target + 2 pp → **No Add** (same as the gate); above target within tolerance → **Review**; sector unknown → **Review**; no sector target → informational only.
- Portfolio Fit does not introduce its own FX conversion or infer a currency. Mixed-currency allocation remains Unavailable until verified common-currency valuation is provided by the authoritative portfolio/allocation services.
- **External cash flows (PM-035):** deposits and withdrawals never change scores, Sector Heat or the setup, and create no P&L. They do recalculate weight, headroom and Fit. The authoritative FINAL ACTION may legitimately change, and the heatmap always shows the current decision.
- **Multiple decisions:** when an owned symbol's authoritative decisions differ (e.g. per role), the grid shows a neutral **"Multiple (n)"** chip (no buy/sell colour) and the drawer lists each original decision as source · role with its own badge; each opens the existing Final Action details dialog with the original reason, evaluation state and evidence freshness. The authoritative `FinalActionResult` carries no account field, so accounts are shown in the row header, not per decision.

## 7. Data integrity

- TSX 60-only evaluation uses scanner `ReadOnly` mode. It writes no StagedSignals, DailySignals, SignalAuditEvents, RsiScanSnapshots, TechnicalChannels, Action Center items, notifications, PortfolioSnapshot patch or user Final Action cache.
- A refresh may only write `OpportunitySnapshots`, `OpportunityRefreshState` and `ValueScreenerSnapshots` rows with `Origin = Universe`. A whole-database state-hash test enforces this.
- Snapshots are immutable (the last 5 per user are kept). The grid, drawer and export all read the same `snapshotId`. A failed refresh never replaces the last successful snapshot.

## 8. Verification

- Backend: `PortfolioManager.Tests` (scoring tables, Sector Heat boundaries, setup validator, Final Action bundle parity, read-only scanner, database state-hash refresh test).
- Angular unit: `opportunities-logic.spec.ts`, `opportunity-details-panel.component.spec.ts`, `opportunities.store.spec.ts`, `opportunity-reference.store.spec.ts`, `opportunity-reference-logic.spec.ts`.
- Playwright (API mocked, synthetic fixtures in `e2e/fixtures/opportunities.ts`): `e2e/trading-opportunities.spec.ts` and the `trading opportunities table` case in `e2e/responsive-cards-pages.spec.ts`, on all four device projects.
- Guard: `e2e/pm029-no-prototype-literals.spec.ts` fails if a prototype value (ticker, price, pp, ratio, exposure or score) appears in production source.
- UAT screenshots: `$env:PM029_SCREENSHOTS='<dir>'; npx playwright test trading-opportunities -g "UAT screenshots"`.
