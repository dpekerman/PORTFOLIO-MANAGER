# UI Enhancements — 2026-07-02 (Batch 2)

## Summary

---

## 1. Global: Green Badge → Black Text

### Change

All action/status badges with a green background now use **black text** (`#000`) for improved readability. Background opacity was increased to a solid-enough green so that black text is clearly legible.

### Affected Badge Classes

| Class                                  | Old Color               | New Color | Background Change                                |
| -------------------------------------- | ----------------------- | --------- | ------------------------------------------------ |
| `.ms-confirmed-buy`                    | `#00e676` (light green) | `#000`    | `rgba(0,200,83, 0.14)` → `rgba(0,200,83, 0.82)`  |
| `.ms-bullish`                          | `#4caf50`               | `#000`    | `rgba(0,200,83, 0.08)` → `rgba(76,175,80, 0.72)` |
| `.ma-confirmed-buy`                    | `#00e676`               | `#000`    | `rgba(0,200,83, 0.14)` → `rgba(0,200,83, 0.82)`  |
| `.ind-bull` (scanner chips)            | `#00c853`               | `#000`    | `rgba(0,200,83, 0.1)` → `rgba(0,200,83, 0.78)`   |
| `.status-badge.confirmed` (scanner)    | `#00c853`               | `#000`    | `rgba(0,200,83, 0.12)` → `rgba(0,200,83, 0.82)`  |
| `.ts-reversal` (watchlist trend setup) | `#4caf50`               | `#000`    | `rgba(0,200,83, 0.12)` → `rgba(0,200,83, 0.78)`  |

### Files Changed

- `features/scanner/rsi-scanner-table.component.scss`
- `features/portfolio/portfolio-page.component.scss`
- `features/watchlist-page/watchlist-page.component.scss`
- `features/allocation/sector-exposition/sector-exposition.component.scss`

---

## 2. Portfolio CSV Export — All Asset Classes + Role for Grouped Positions

### Changes

#### All Assets Included

The CSV export now includes **three asset sections**:

| Section | Row Type                                   | Notes             |
| ------- | ------------------------------------------ | ----------------- |
| Stocks  | One row per position / per aggregate group | Existing behavior |
| Cash    | One row per cash item                      | **NEW**           |
| Options | One row per open option position           | **NEW**           |

A new first column `Asset Type` contains `"Stock"`, `"Cash"`, or `"Option"` to distinguish rows.

#### Role on Grouped (Multi-Account) Rows

For positions grouped across multiple accounts, the `Role` column now exports `agg.holdingRole` (the role stored on the aggregate row) instead of the previous empty string `''`.

#### Cash Row Format

```
Cash | CASH | <description> | | | | | <amount> | | <amount> | 0.00 | <% of total> | ...
```

#### Option Row Format

```
Option | <ticker> | <PUT/CALL $strike exp date> | <account> | | | <contracts> | <premium> | <mktPrice> | <mktValue> | <G/L> | <G/L%> | ...
```

### Files Changed

- `features/portfolio/portfolio-page.component.ts` — `exportCsv()` method

---

## 3. Portfolio Final Action — 10 New Profit-Taking Rules

### Overview

Ten new profit-taking rules have been added to the Decision Engine for the **Portfolio page**. These rules evaluate after role-based logic and before (or instead of) the TFSA override. They fire based on:

- `UnrealizedPnLPct` — unrealized gain as %
- `DistanceFrom52WeekHighPct` — % distance from 52-week high (−2 or above = "near the high")
- `PositionSizePct` — position market value as % of total portfolio
- `MomentumShift` — computed decision from RSI engine

### Rule Table

| #   | UnrealizedPnL | 52W High Condition | Position Size | Momentum            | Result Action                                |
| --- | ------------- | ------------------ | ------------- | ------------------- | -------------------------------------------- |
| 1   | ≥ 20%         | ≥ −2% from high    | any           | any                 | `Profit Watch / No Add / Trail Stop`         |
| 2   | ≥ 30% & < 50% | ≥ −2% from high    | < 2%          | any                 | `Trim 10–20% / Hold Runner`                  |
| 3   | ≥ 30% & < 50% | ≥ −2% from high    | 2% – 4%       | any                 | `Trim 20–33% / Hold Runner`                  |
| 4   | ≥ 30% & < 50% | ≥ −2% from high    | ≥ 4%          | any                 | `Trim 25–40% / Hold Runner`                  |
| 5   | ≥ 50%         | ≥ −2% from high    | < 2%          | any                 | `Trim 25% / Keep Runner`                     |
| 6   | ≥ 50%         | ≥ −2% from high    | 2% – 4%       | any                 | `Trim 33–50% / Keep Runner`                  |
| 7   | ≥ 50%         | ≥ −2% from high    | ≥ 4%          | any                 | `Sell 50% / Keep Runner / Rebuy on Pullback` |
| 8   | ≥ 20%         | any                | any           | Active Sell Trigger | `Take Partial Profit / Protect Gain`         |
| 9   | ≥ 30%         | any                | ≥ 2%          | Active Sell Trigger | `Trim 33–50% / Trail Remainder`              |
| 10  | ≥ 50%         | any                | any           | Active Sell Trigger | `Sell Majority / Keep Small Runner`          |

**Priority order** (highest to lowest): Rule 10 > Rule 9 > Rule 8 > Rule 7 > Rule 6 > Rule 5 > Rule 4 > Rule 3 > Rule 2 > Rule 1

Momentum (Active Sell Trigger) rules override the 52W High rules of the same gain tier.

### Context Fields Added to `PortfolioItemContext`

```typescript
interface PortfolioItemContext {
  accountType?: string | null; // existing
  unrealizedGainPct?: number | null; // existing
  holdingDays?: number | null; // existing
  distanceFrom52WeekHighPct?: number | null; // NEW — e.g. -1.5 = 1.5% below 52W high
  positionSizePct?: number | null; // NEW — e.g. 3.2 = 3.2% of portfolio
}
```

### Where Context Is Populated

In `portfolio-page.component.ts → decisionForPortfolio()`:

```typescript
const distanceFrom52WeekHighPct =
  r.week52High > 0 ? ((price - r.week52High) / r.week52High) * 100 : null;

const positionSizePct =
  grandTotal > 0 ? (marketValue / grandTotal) * 100 : null;
```

Grand total = stocks + cash + options (consistent with Allocation page).

### CSS Badge Classes

The new profit-taking action strings map to `ma-tfsa-profit` (amber/gold) or `ma-reduce` (orange) via `finalActionClass()`:

| Action contains                                                                                             | CSS class                |
| ----------------------------------------------------------------------------------------------------------- | ------------------------ |
| `profit watch`, `partial profit`, `protect gain`                                                            | `ma-tfsa-profit` (amber) |
| `trim`, `sell 50%`, `sell majority`, `keep runner`, `hold runner`, `trail remainder`, `no add / trail stop` | `ma-reduce` (orange)     |

### Files Changed

- `core/services/decision-engine.service.ts` — `PortfolioItemContext`, new `profitTakingAction()` method, updated `finalActionClass()`
- `features/portfolio/portfolio-page.component.ts` — `decisionForPortfolio()` now passes `distanceFrom52WeekHighPct` and `positionSizePct`

---

## Build Status

| Target                                | Result                                               |
| ------------------------------------- | ---------------------------------------------------- |
| `ng build --configuration production` | ✅ Success (CSS budget warnings only — pre-existing) |
| TypeScript diagnostics                | ✅ No errors                                         |

---

## Files Modified Summary

| File                                                                     | Change                                                                                                                     |
| ------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------- |
| `features/scanner/rsi-scanner-table.component.scss`                      | Green badge → black text (`.ms-confirmed-buy`, `.ms-bullish`, `.ind-bull`, `.status-badge.confirmed`, `.ma-confirmed-buy`) |
| `features/portfolio/portfolio-page.component.scss`                       | Green badge → black text (`.ms-confirmed-buy`, `.ms-bullish`, `.ma-confirmed-buy`)                                         |
| `features/watchlist-page/watchlist-page.component.scss`                  | Green badge → black text (`.ms-confirmed-buy`, `.ms-bullish`, `.ma-confirmed-buy`, `.ts-reversal`)                         |
| `features/allocation/sector-exposition/sector-exposition.component.scss` | Green badge → black text (`.ms-confirmed-buy`, `.ms-bullish`)                                                              |
| `features/portfolio/portfolio-page.component.ts`                         | `exportCsv()` — Cash+Options export, Role on grouped rows; `decisionForPortfolio()` — 52W high + position size context     |
| `core/services/decision-engine.service.ts`                               | Extended `PortfolioItemContext`; new `profitTakingAction()` Rules 1–10; updated `finalActionClass()`                       |
