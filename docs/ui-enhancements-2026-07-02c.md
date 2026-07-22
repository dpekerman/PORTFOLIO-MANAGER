# UI Enhancements — 2026-07-02 (Batch 3)

## Summary of Changes

---

## 1. Global: Yellow / Orange / Grey Badge → Black Text

All status badges that previously used low-opacity yellow, orange, or grey backgrounds with matching-color text are now **solid with black text** for much better readability. Background opacity raised from ~8-14% to ~60-82%.

### Affected Classes

| Class                | Color Family | Old Text  | New Text | New Opacity |
| -------------------- | ------------ | --------- | -------- | ----------- |
| `.ms-warning`        | Orange/amber | `#ffa726` | `#000`   | 82%         |
| `.ms-consolidation`  | Yellow       | `#ffc107` | `#000`   | 82%         |
| `.ms-breakdown`      | Red-orange   | `#ef5350` | `#000`   | 78%         |
| `.ms-uptrend`        | Blue         | `#42a5f5` | `#000`   | 72%         |
| `.ms-downtrend`      | Grey         | `#9e9e9e` | `#000`   | 65%         |
| `.ms-neutral`        | Dark grey    | `#757575` | `#000`   | 60%         |
| `.ma-early-warning`  | Orange/amber | `#ffa726` | `#000`   | 82%         |
| `.ma-avoid`          | Grey         | `#757575` | `#000`   | 65%         |
| `.ma-hold`           | Blue         | `#42a5f5` | `#000`   | 72%         |
| `.ma-accumulate`     | Teal/cyan    | `#00bcd4` | `#000`   | 78%         |
| `.ma-reduce`         | Orange-red   | `#ff7043` | `#000`   | 82%         |
| `.ma-standby`        | Dark grey    | `#616161` | `#000`   | 60%         |
| `.ma-tfsa-profit`    | Amber/gold   | `#ffca28` | `#000`   | 82%         |
| `.ts-extended`       | Orange       | `#ffa000` | `#000`   | 82%         |
| `.ts-quality`        | Teal/cyan    | `#00bcd4` | `#000`   | 78%         |
| `.ts-constructive`   | Blue         | `#42a5f5` | `#000`   | 72%         |
| `.ts-early-reversal` | Yellow       | `#ffc107` | `#000`   | 82%         |
| `.ts-cooling`        | Grey         | `#9e9e9e` | `#000`   | 65%         |
| `.ts-caution`        | Orange-red   | `#ff7043` | `#000`   | 82%         |
| `.ts-neutral`        | Dark grey    | `#616161` | `#000`   | 60%         |

### Files Changed

- `features/scanner/rsi-scanner-table.component.scss`
- `features/portfolio/portfolio-page.component.scss`
- `features/watchlist-page/watchlist-page.component.scss`
- `features/allocation/sector-exposition/sector-exposition.component.scss`

---

## 2. Allocation Page — Default Collapsed State

### Change

Cash and Options sections now **start collapsed** when navigating to the Allocation page. Users can expand them individually by clicking the section header.

Previously: `cashExpanded = signal(true)` / `optionsExpanded = signal(true)`  
Now: `cashExpanded = signal(false)` / `optionsExpanded = signal(false)`

### Files Changed

- `features/allocation/allocation-page.component.ts`

---

## 3. Portfolio — % Total Column

### Change

A new **`% TOTAL`** column has been added to the stocks grid, positioned immediately after **MKT VALUE**.

- **Formula**: `Position Market Value / Portfolio Total Value × 100`
- For **aggregated rows** (multi-account tickers): sum of all accounts / portfolio total
- Positions representing **≥ 5%** of the portfolio are highlighted in amber (`#ffa726`) to flag concentration risk

### Column Definition

```
Column ID: portfolioPct
Header: % TOTAL
Position: After MKT VALUE, before GAIN/LOSS
```

### Files Changed

- `features/portfolio/portfolio-page.component.html` — column definition added
- `features/portfolio/portfolio-page.component.ts` — `portfolioPct` added to `gridDisplayedColumns`
- `features/portfolio/portfolio-page.component.scss` — `.pg-pct-total`, `.pg-pct-pill`, `.pg-pct-large` styles

---

## 4. Watchlist — BUY Score Column

### Change

A new **`BUY SCORE`** column has been added to the watchlist grid, positioned between **MOMENTUM SHIFT** and **FINAL ACTION**.

### Score Calculation

Each of 5 checks contributes **1 point** (maximum score = 5):

| #   | Check                    | Condition                                       |
| --- | ------------------------ | ----------------------------------------------- |
| 1   | Close > EMA9             | `currentPrice > ema9Price`                      |
| 2   | RSI14 > RSI9EMA          | `rsi > rsiSignal` (when signal available)       |
| 3   | MACD Histogram Improving | `macdHistDelta > 0`                             |
| 4   | CloseLocation ≥ 0.50     | `(close - dayLow) / (dayHigh - dayLow) >= 0.50` |
| 5   | VolumeRatio20 ≥ 1.0      | `volumeRatio >= 1.0`                            |

**BUY Score = sum of passed checks (0–5)**

### Score Badge Colors

| Score | Color                     |
| ----- | ------------------------- |
| 5     | Solid green + black text  |
| 4     | Medium green + black text |
| 3     | Yellow + black text       |
| 2     | Orange + black text       |
| 1     | Orange-red + black text   |
| 0     | Grey + black text         |

### Hover Tooltip

Hovering over any score badge shows which checks passed and which failed:

```
✅ Close > EMA9
✅ RSI14 > RSI9EMA
❌ MACD Histogram Improving
❌ CloseLocation >= 0.50
❌ VolumeRatio20 >= 1.0
```

### Files Changed

- `features/watchlist-page/watchlist-page.component.ts` — `buyScoreForSymbol()` method; `'buyScore'` added to `displayedColumns`
- `features/watchlist-page/watchlist-page.component.html` — `buyScore` column definition added
- `features/watchlist-page/watchlist-page.component.scss` — `.buy-score-badge`, `.bs-0` through `.bs-5`, `.wl-buy-score-header`, `.wl-buy-score-cell`

---

## 5. Watchlist — Column Width Optimisation

### Change

Added explicit `min-width`, `max-width`, and `width` constraints to all watchlist grid columns via `.watchlist-table .mat-column-*` selectors. The **CHANGE** column was the primary offender — now constrained to `100–120px`.

### Column Widths Applied

| Column         | Width                             |
| -------------- | --------------------------------- |
| TICKER         | 80–100px                          |
| DESCRIPTION    | 120–200px                         |
| ROLE           | 130–150px                         |
| LAST PRICE     | 70–90px                           |
| **CHANGE**     | **100–120px** (was unconstrained) |
| ANALYST TARGET | 80–100px                          |
| 52W RANGE      | 130–160px                         |
| SECTOR         | 100–160px                         |
| RSI            | 60–70px                           |
| TREND SETUP    | 140–200px                         |
| MOMENTUM SHIFT | 130–190px                         |
| BUY SCORE      | 68–80px                           |
| FINAL ACTION   | 160–260px                         |
| ACTIONS        | 44px fixed                        |

### Files Changed

- `features/watchlist-page/watchlist-page.component.scss` — column width block under `.watchlist-table`

---

## Build Status

| Target                                | Result                                              |
| ------------------------------------- | --------------------------------------------------- |
| `ng build --configuration production` | ✅ Success                                          |
| TypeScript diagnostics                | ✅ No errors                                        |
| CSS budget warnings                   | ⚠️ Pre-existing (watchlist +2.79KB from new styles) |

---

## Testing Checklist

- [ ] **Global badges**: Navigate to Scanner, Portfolio, Watchlist, Allocation — confirm yellow/orange/grey badges show black text
- [ ] **Allocation landing**: Navigate to `/allocation` — Cash and Options sections start collapsed
- [ ] **Portfolio % Total**: Stock grid shows `% TOTAL` column after MKT VALUE; positions ≥5% highlighted amber
- [ ] **Watchlist BUY Score**: Grid shows `BUY SCORE` column between MOMENTUM SHIFT and FINAL ACTION
- [ ] **Watchlist BUY Score hover**: Hover on score badge shows ✅/❌ breakdown tooltip
- [ ] **Watchlist column widths**: CHANGE column no longer takes excessive space
