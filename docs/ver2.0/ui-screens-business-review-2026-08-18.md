# UI Screen Analysis & Business Improvement Report — 2026-08-18

Portfolio Manager v2. Analysis of all 8 current screens from technology and business
perspectives, compared against leading Canadian retail trading platforms (Wealthsimple
Trade, Questrade, TD Direct Investing, Wealthica) and international platforms
(Interactive Brokers, Robinhood, IBKR).

---

## Executive Summary

The application has a solid technical foundation — Angular 22 signals architecture,
real-time live prices, role-based access, and a sophisticated RSI signal engine. The
primary business gaps are: (1) no portfolio performance analytics, (2) no tax/ACB
reporting, (3) no dashboard/home screen, and (4) missing action-oriented features that
support trade decision-making (position sizing, alerts, earnings calendar). Technology
gaps centre on missing virtual scrolling for large portfolios, no push notifications, and
no benchmark comparison.

---

## Screen 1 — Portfolio (`/portfolio`) ✅ Analysed

**Purpose**: Central hub for tracking all open and closed positions (stocks, cash, options, manual entries).

### Strengths

- Card + Grid toggle appropriate for different use cases
- Column configuration lets power users customise the view
- 30-second auto-refresh of live prices
- Backup / Restore to JSON for data protection
- Export to CSV for external analysis
- Demo mode for safe screenshot sharing

### Technology Gaps

- No skeleton loading state in grid view (blank rows appear during load)
- Grid renders all rows at once — will be slow with 100+ positions (no virtual scrolling)
- Column configuration state stored in localStorage — lost if browser data is cleared
- No "Add to Watchlist" quick action directly from the grid

### Business Gaps

- **No performance metrics visible** — no annualized return, no Sharpe ratio, no maximum drawdown
- **No account type grouping** — positions are grouped by symbol, not by account (TFSA vs. RRSP vs. Margin). Canadian investors manage these accounts separately for tax purposes.
- **No realized P&L summary** — closed positions exist in the database but there is no KPI card showing total realized gain/loss
- **No benchmark comparison** — no way to compare portfolio return to TSX Composite or S&P 500
- **No position risk indicator** — no visual flag when a position exceeds the allocation limit configured in Config
- **No dividend income column** — high-yield TSX stocks (banks 4–5%, pipelines 6–8%, REITs 5–8%) produce significant income that is not tracked
- **No cost basis edit audit** — no history of when average cost was changed

### Recommended Improvements

1. Add a "Performance Summary" panel above the grid: total return, annualized return, max drawdown, Sharpe ratio.
2. Add account type sub-tabs or filter pill (TFSA / RRSP / Margin / Corp).
3. Add a realized P&L KPI card (sum of all closed position gains/losses).
4. Show a "Limit Alert" badge when a position exceeds its holding role size limit.

---

## Screen 2 — Transactions (`/transactions`) ✅ Analysed

**Purpose**: Audit trail of all position opens, closes, and options transactions.

### Strengths

- Open/Close filter toggle with clean separation
- Decision Source tracking (App Signal, Manual, Catalyst, etc.)
- Collapsible Stocks/Options sections
- Notes dialog per transaction
- Export stocks to CSV

### Technology Gaps

- OPEN records are filtered with a hardcoded date threshold (`2026-06-01`) — no UI control to adjust this
- Options transactions cannot be exported (stocks-only export)
- No bulk edit or bulk notes update
- No sort by date (rows display in fixed database order)

### Business Gaps

- **No P&L by account type** — critical for separating TFSA (tax-free) vs. non-registered (taxable) gains
- **No performance by Decision Source** — "Was App Signal more profitable than Manual?" — this metric directly validates the scanner's value
- **No tax year filter** — for capital gains reporting, users need all closes within a calendar year (January 1 – December 31)
- **No ACB column** — the legally required Adjusted Cost Base per lot is not tracked
- **No trade timeline visualization** — no chart of position opens/closes over time
- **No P&L attribution** — which sectors or decision sources generated the most profit?

### Recommended Improvements

1. Add Decision Source performance analytics: win rate and average return per source.
2. Add a tax year date filter for realized gains/losses reporting.
3. Enable options transaction export.
4. Add a realized P&L column to closed transactions.

---

## Screen 3 — RSI Scanner (`/scanner`) ✅ Analysed

**Purpose**: Real-time RSI-based signal detection across 50 TSX stocks plus the user's portfolio and watchlist symbols.

### Strengths

- 5-indicator confirmation system (RSI, Stochastic, MACD, Bollinger, Volume)
- EOD window banner and morning briefing panel
- Portfolio/Watchlist badges highlighting held symbols
- Legacy/Enhanced logic mode toggle
- Ad-hoc analyzer for manual symbol research
- Auto-refresh every 5 minutes (configurable)

### Technology Gaps

- Timezone hardcoded to Eastern Time — users in other timezones see incorrect time context for the EOD window
- No push or browser notifications when signals appear (user must have the tab open)
- No sort controls on the scan results table (cannot sort by RSI value, volume ratio, or reversal probability)
- Column configuration is global — cannot have different columns for Oversold vs. Overbought views
- Ad-hoc session results stored to DB but not user-attributed

### Business Gaps

- **No signal performance history** — "Of the 15 Confirmed signals in the last 30 days, how many followed through?" This context would build confidence in the algorithm.
- **No price alert capability** — users cannot set "notify me when RY.TO RSI drops below 35"
- **No sector-level summary** — "3 Energy stocks oversold today, 0 Financials" — critical context for sector rotation decisions
- **No quick-add to Watchlist** — a scanned symbol cannot be added to the watchlist in one click from the scan results
- **Signal terminology is opaque** — EodConfirm, EarlyWarning, TrendShift, TurnStrength, ChaseRisk are not explained anywhere in the UI. New users have no context.

### Recommended Improvements

1. Add a "Signal Performance" KPI above the table: last 30 days Confirmed signal win rate.
2. Add a "Add to Watchlist" button per result row.
3. Add a sector summary bar ("Oversold today: 2 Energy, 1 Financials, 1 Materials").
4. Add a "Signal Legend" collapsible panel or tooltips explaining each status.
5. Add browser notification support (Web Notifications API) for EOD Confirm signals.

---

## Screen 4 — Allocation (`/allocation`) ✅ Analysed

**Purpose**: Portfolio composition analysis — sector breakdown, beta risk contribution, cash and options summary.

### Strengths

- Combined total value across all asset types (stocks + cash + options)
- Sector exposure with industry drill-down
- Weighted portfolio beta with per-symbol contribution
- Manual beta overrides per symbol

### Technology Gaps

- Beta overrides are not persisted to the backend — lost on page reload
- No loading skeleton for the beta calculation section
- Sector chart has no colour coding by risk level or divergence from target
- No allocation export to CSV or PDF

### Business Gaps

- **No target vs. actual allocation** — the Config screen allows sector targets and position limits, but the Allocation screen does not show how far over or under each target the portfolio is
- **No rebalancing suggestions** — "You are 8% overweight Financials relative to your 20% target. Consider reducing."
- **No geographic allocation** — Canada vs. US vs. International exposure (relevant for CDR holders)
- **No market-cap breakdown** — Large cap / Mid cap / Small cap
- **No holding role allocation vs. targets** — Core / Strategic / Swing / Speculative actual vs. configured targets
- **No correlation data** — two large positions that are 0.95 correlated provide effectively zero diversification benefit; this is invisible
- **Portfolio beta context missing** — a beta of 1.2 vs. TSX means 20% more volatile than the market, but no benchmark context is displayed

### Recommended Improvements

1. Add "Target vs. Actual" horizontal bars for each sector (showing % over/under target in red/green).
2. Show holding role allocation (Core/Strategic/Swing) vs. configured targets.
3. Persist beta overrides to the backend.
4. Add a geographic exposure tile (Canada / USA / International).

---

## Screen 5 — Watchlist (`/watchlist`) ✅ Analysed

**Purpose**: Curated research list of symbols under active monitoring, with integrated technical and value scoring.

### Strengths

- Card + Grid toggle with rich technical data per symbol
- Favourite flagging for quick access
- Role assignment (Core/Strategic/Swing/Speculative/Options)
- Integration with RSI scanner and Value Screener data
- Backup / Restore / Export to Excel

### Technology Gaps

- Value Screener data is loaded from the last DB snapshot — may be weeks old with no visible freshness indicator
- No per-symbol RSI refresh (must trigger the full scanner)
- No comparison mode (view two watchlist symbols side by side)
- No skeleton loading in card view during initial data fetch

### Business Gaps

- **No earnings date** — knowing when a watchlist symbol reports earnings is critical for swing traders (entry/exit timing around catalysts)
- **No price alerts** — cannot set a target entry price with a notification
- **No sector grouping** — symbols listed flat; no visual grouping by sector for portfolio construction context
- **No conviction scoring** — the "Role" field provides categorisation but no relative conviction level (e.g., 1–5 stars)
- **Value Screener data freshness** — a score from 3 weeks ago may be misleading; users have no awareness of staleness unless they check the screener page

### Recommended Improvements

1. Show "Value Score last updated: X days ago" on each watchlist card.
2. Add sector grouping option in the grid view.
3. Add an earnings date column (available from Yahoo Finance `v10/quoteSummary`).
4. Add a per-symbol "Refresh Value Score" button.

---

## Screen 6 — EOD Signals (`/eod-signals`) ✅ Analysed

**Purpose**: Historical record of all confirmed signals with lifecycle state tracking (Active → FollowThrough / Invalidated / Expired / Reversed).

### Strengths

- Paginated table with comprehensive filters (ticker, scan type, signal type, state, rule version, date range)
- Signal lifecycle state tracking with notes
- Auto-polling for new signals with snackbar notification
- Current price enrichment (live price vs. entry price, % difference)
- Export to Excel
- Per-signal notes

### Technology Gaps

- Auto-refresh polls every 30 seconds even when no signals are expected (e.g., weekends, outside market hours)
- "Delete All" deletes all records with only a confirmation dialog — no scope limitation, no soft delete, no undo
- Sorting is client-side on the current page only (accurate for 50 rows, misleading when comparing across pages)

### Business Gaps

- **No performance analytics** — What percentage of FollowThrough signals were profitable? What was the average return and average holding period? This is the most important metric for validating the entire signal engine.
- **No visual signal timeline** — a chart of signal count per week over the last 90 days would reveal whether the algorithm is generating more or fewer signals over time
- **No sector concentration** — which sectors appear most often in the signal history?
- **No signal aging alerts** — a signal that has been "Active" for 30+ days with no state update may be forgotten
- **No bulk state update** — cannot select 10 old Active signals and change them to Expired at once
- **No rule version comparison** — "Enhanced vs. Legacy: which generated more profitable signals per quarter?"

### Recommended Improvements

1. Add a performance summary panel: win rate, average return, average holding period on FollowThrough signals.
2. Add bulk state update (multi-select checkbox + change state dropdown).
3. Add an "Age" column: days since signal date. Visually highlight signals Active > 20 days.
4. Add a signal count chart over the last 90 days (bar chart by week).

---

## Screen 7 — Value Screener (`/value-screener`) ✅ Analysed

**Purpose**: Multi-factor value investing analysis: earnings yield, FCF yield, Price-to-Book, Piotroski F-Score, ROIC.

### Strengths

- Three source modes (Portfolio, Watchlist, Ad-hoc symbols)
- Tier classification (High Conviction / Fair Value / Value Trap)
- Last run timestamp displayed
- Technical state integration with RSI scanner
- Export to CSV

### Technology Gaps

- No per-symbol progress indicator during refresh (only a spinner — no "analyzing RY.TO 12/25")
- Ad-hoc results are not persisted — lost on navigation away
- Scheduled background refresh runs at a fixed time; no manual trigger from this screen

### Business Gaps

- **No factor breakdown visible** — for each result, which of the 5 factors scored well? Currently only a total score is shown. Users cannot understand why a stock scored 8.5 vs. 4.0.
- **No historical score comparison** — was TD Bank higher value 3 months ago? No score history is tracked.
- **No dividend yield in scoring** — for Canadian income investors, dividend yield is often the primary valuation metric. Banks, pipelines, and REITs are the most common TSX holdings and all have significant yields.
- **No minimum score filter** — users cannot filter to "only show symbols with score > 7.0"
- **No sector-adjusted scoring context** — a P/B of 1.5 is "undervalued" for a tech stock but "normal" for a bank. The engine routes REITs and financials correctly, but the user sees no explanation.

### Recommended Improvements

1. Show per-factor scores inline (5 mini-indicator bars or icons per row: F1–F5).
2. Persist ad-hoc results linked to user session.
3. Add a minimum score filter slider (e.g., "Show only scores ≥ 7.0").
4. Add dividend yield as an optional scoring factor with configurable weight.
5. Track and display score history (last 3 runs) per symbol.

---

## Screen 8 — Configuration (`/config`) ✅ Analysed

**Purpose**: Global application settings, user management, notification configuration, scanner parameters.

### Strengths

- Comprehensive tab organisation covering all configurable areas
- Role-based access control (some tabs admin-only)
- Inline form validation with clear error states
- Snackbar feedback on save and error

### Technology Gaps

- "Save All" saves all tabs simultaneously — a validation error in one tab blocks saving another
- No per-section Save button
- No settings backup/restore to JSON (ironic given portfolio data has this feature)
- No audit trail — no log of who changed what setting and when

### Business Gaps

- **User management lacks last-login visibility** — admins cannot see when each user last accessed the system
- **No email preview** — the notification system sends formatted HTML emails, but admins cannot preview the template from the Config screen
- **No notification test send** — must wait for a real signal to test email delivery
- **No inline help text** — the Config screen has no tooltips or explanatory text for settings like `TrendShiftThreshold` or `EodWindowStart`

### Recommended Improvements

1. Add per-section Save buttons alongside the global Save All.
2. Add a "Send Test Email" button on the Alerts tab.
3. Add settings change audit log (user, timestamp, old value, new value).
4. Add inline help text / ? icon tooltips for each setting.

---

## Missing Screens — High Business Value

### Missing Screen A — Dashboard / Home ✅ Analysed

**Business need**: Every professional investing platform has a dashboard page. Currently the app opens directly to Portfolio with no daily overview context.

**Recommended content**:

- Today's portfolio value change ($ and %) vs. yesterday's close
- Active RSI signal count: X oversold, Y overbought
- Market indices bar (already exists in Scanner — promote to dashboard)
- Upcoming earnings for portfolio + watchlist symbols (next 7 days)
- Quick action tiles: Scan Now, Analyze Watchlist, Add Transaction

**Competitive gap**: Wealthsimple Trade, TD Direct Investing, and Questrade all lead with a dashboard page.

---

### Missing Screen B — Performance Analytics ✅ Analysed

**Business need**: Historical portfolio performance vs. a benchmark, with risk-adjusted metrics.

**Recommended content**:

- Portfolio value chart with date range selector (1M / 3M / 6M / 1Y / All)
- TWRR and annualized return vs. TSX Composite benchmark overlay
- Rolling Sharpe ratio
- Maximum drawdown visualization
- Best/worst months table
- Return attribution by sector and holding role

**Data source**: The `PortfolioValueHistories` table already captures daily total values. The benchmark line requires fetching TSX Composite (`^GSPTSE`) historical data from Yahoo Finance (same endpoint already used for stocks).

**Competitive gap**: Wealthica (Canadian) and Sharesight provide exactly this. Both are popular with TSX-focused retail investors precisely because brokers' performance reports are inadequate.

---

### Missing Screen C — Tax Report / ACB Calculator ✅ Analysed

**Business need**: Canadian investors must calculate capital gains and losses for their annual tax return using the Adjusted Cost Base (ACB) method.

**Recommended content**:

- Realized gains/losses by calendar year
- ACB per symbol with full lot purchase history
- TFSA / RRSP / Non-Registered account segregation (gains are only taxable in non-registered accounts)
- T5008 / Schedule 3 format summary export

**Competitive gap**: Every Canadian broker provides this in their year-end tax packages. Third-party tools Wealthica and AdjustedCostBase.ca are widely used by DIY investors because broker reports are often incomplete or formatted incorrectly.

---

### Missing Screen D — Earnings Calendar ✅ Analysed

**Business need**: Knowing when portfolio and watchlist companies report earnings is critical for risk management — avoid holding through earnings on a large position, or time a swing entry before an expected positive catalyst.

**Data source**: Yahoo Finance `v10/quoteSummary` returns `earningsDate` (next scheduled earnings) for each symbol.

**Recommended content**:

- Calendar or list view: next 30 days of earnings dates
- Colour-coded by source: portfolio holding vs. watchlist vs. market scan symbol
- Expected move estimate (based on ATR as a proxy for historical volatility)
- Link to Value Screener score for each upcoming reporter

---

### Missing Screen E — Price Alerts & Notifications ✅ Analysed

**Business need**: Users should not need to check the scanner manually. A price drop below a configured RSI threshold or price target should trigger a notification automatically.

**Recommended content**:

- Alert setup: symbol, condition (RSI below X, price below Y, price above Z)
- Notification method: in-app, email (SMTP already configured)
- Alert history log: when each alert was triggered
- Active vs. resolved alert management

**Technical path**: The background scanner service already runs every 5 minutes and evaluates RSI conditions. Extending it to check user-defined alert conditions is incremental work on an existing pipeline.

---

## Overall Priority & Decision Matrix

| Improvement                         | Screen         | Business Value | Tech Effort | Status                          |
| ----------------------------------- | -------------- | -------------- | ----------- | ------------------------------- |
| Performance Analytics screen        | New (B)        | Very High      | Medium      | ✅ Analysed — awaiting decision |
| Dashboard / Home screen             | New (A)        | High           | Medium      | ✅ Analysed — awaiting decision |
| Target vs. Actual allocation bars   | Allocation     | High           | Low         | ✅ Analysed — awaiting decision |
| Signal performance analytics panel  | EOD Signals    | High           | Medium      | ✅ Analysed — awaiting decision |
| Account type grouping in Portfolio  | Portfolio      | High           | Medium      | ✅ Analysed — awaiting decision |
| Decision Source win rate analytics  | Transactions   | High           | Low         | ✅ Analysed — awaiting decision |
| Per-factor scores in Value Screener | Value Screener | Medium         | Low         | ✅ Analysed — awaiting decision |
| Earnings calendar screen            | New (D)        | High           | Medium      | ✅ Analysed — awaiting decision |
| Sector summary bar in Scanner       | Scanner        | Medium         | Low         | ✅ Analysed — awaiting decision |
| Quick-add to Watchlist from Scanner | Scanner        | Medium         | Low         | ✅ Analysed — awaiting decision |
| Persist beta overrides to backend   | Allocation     | Medium         | Low         | ✅ Analysed — awaiting decision |
| Signal legend / onboarding tooltips | Scanner        | Medium         | Low         | ✅ Analysed — awaiting decision |
| Tax Report / ACB screen             | New (C)        | Very High      | High        | ✅ Analysed — awaiting decision |
| Price Alerts & Notifications screen | New (E)        | High           | High        | ✅ Analysed — awaiting decision |
| Send Test Email button in Config    | Config         | Low            | Low         | ✅ Analysed — awaiting decision |
| Settings audit log                  | Config         | Low            | Medium      | ✅ Analysed — awaiting decision |
