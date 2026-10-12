/**
 * Static catalogue of every configurable data grid: types and the column definitions themselves.
 *
 * Data only — no Angular, no state. {@link GridColumnStore} layers the user's saved order and
 * visibility on top, which keeps the registry importable from pure unit tests and from the
 * column-config UI without pulling in the store.
 */

// ── Public Interfaces ─────────────────────────────────────────────────────────

export interface ColumnDef {
  key: string;
  label: string;
  /** Pinned columns are always visible and always rendered last; they cannot be hidden or reordered. */
  pinned?: boolean;
  /** When true, this column is hidden by default for users who have not saved a preference. */
  defaultHidden?: boolean;
  /**
   * Controls **MOBILE visibility only; never affects desktop**.
   *
   * On a handset a 20-40 column grid is rendered as one card per row, so only the few columns a
   * user actually decides on should survive. `'primary'` marks those columns; everything else
   * defaults to `'secondary'` and is reachable through the "Show all fields" toggle.
   */
  priority?: 'primary' | 'secondary';
}

export interface ColumnPreference {
  key: string;
  visible: boolean;
}

export interface GridDef {
  id: string;
  label: string;
  page: string;
  pageIcon: string;
  columns: ColumnDef[];
}

// ── Responsive projection helpers ─────────────────────────────────────────────
// Pure: a handset layout is a *view* over the user's saved preferences, never a mutation of them.
// Keeping the rules here (next to `priority`) means they can be unit-tested without the store and
// reused by any future breakpoint-aware grid.

/** Key of the column-config gear column. It is chrome, not data, so cards never render it. */
export const CONFIG_COLUMN_KEY = 'colConfig';

/**
 * Projects the keys a grid should render on a handset.
 *
 * @param gridDef registry entry the keys belong to.
 * @param visibleKeys the user's currently visible keys, in their configured order (the desktop
 *   result of `resolveKeys`), so hiding a column on desktop also hides it on mobile.
 * @param showAll when true the user asked for every visible field, so only the config column is
 *   dropped.
 * @returns `visibleKeys` filtered to primary columns plus pinned columns (e.g. `actions`), always
 *   without {@link CONFIG_COLUMN_KEY}. Grids that declare no primary column fall back to all
 *   visible keys, so adding `priority` is opt-in and a grid is never emptied by accident.
 */
export function resolveHandsetKeys(
  gridDef: GridDef,
  visibleKeys: readonly string[],
  showAll = false,
): string[] {
  const withoutConfig = visibleKeys.filter((key) => key !== CONFIG_COLUMN_KEY);
  if (showAll) return withoutConfig;

  const primary = new Set(
    gridDef.columns.filter((c) => c.priority === 'primary').map((c) => c.key),
  );
  if (primary.size === 0) return withoutConfig;

  const pinned = new Set(gridDef.columns.filter((c) => c.pinned).map((c) => c.key));
  return withoutConfig.filter((key) => primary.has(key) || pinned.has(key));
}

// ── Grid Registry ─────────────────────────────────────────────────────────────

export const GRID_REGISTRY: GridDef[] = [
  {
    id: 'portfolio-stocks',
    label: 'Stocks',
    page: 'Portfolio',
    pageIcon: 'account_balance_wallet',
    columns: [
      { key: 'symbol', label: 'Ticker', priority: 'primary' },
      { key: 'company', label: 'Company', priority: 'primary' },
      { key: 'currency', label: 'Currency' },
      { key: 'accountType', label: 'Account', priority: 'primary' },
      { key: 'sector', label: 'Sector' },
      { key: 'industry', label: 'Industry' },
      { key: 'shares', label: 'Shares' },
      { key: 'avgCost', label: 'Avg Cost' },
      { key: 'price', label: 'Last Price', priority: 'primary' },
      { key: 'analystTarget', label: 'Analyst Target' },
      { key: 'changePct', label: 'Day %', priority: 'primary' },
      { key: 'dayGain', label: 'Day $' },
      { key: 'marketValue', label: 'Mkt Value', priority: 'primary' },
      { key: 'portfolioPct', label: '% Total' },
      { key: 'gainLoss', label: 'Gain/Loss', priority: 'primary' },
      { key: 'gainLossPct', label: 'Gain %', priority: 'primary' },
      { key: 'rsi', label: 'RSI (14)' },
      { key: 'technical', label: 'Technical' },
      { key: 'holdingRole', label: 'Role' },
      { key: 'decisionSource', label: 'Decision Source' },
      { key: 'trendSetup', label: 'Trend Setup' },
      { key: 'reversalP', label: 'Reversal P.' },
      { key: 'momentumShift', label: 'Momentum Shift' },
      { key: 'channel', label: 'CHANNEL' },
      { key: 'priceStructure', label: 'Price Structure' },
      { key: 'gapStatus', label: 'Gap Status' },
      { key: 'finalAction', label: 'Final Action', priority: 'primary' },
      { key: 'age', label: 'Age (days)' },
      { key: 'maStatus', label: 'MA Status' },
      { key: 'fibSwing', label: 'Fib Swing High/Low', defaultHidden: true },
      { key: 'fib38_2', label: 'Fib 38.2', defaultHidden: true },
      { key: 'fib50', label: 'Fib 50', defaultHidden: true },
      { key: 'fib61_8', label: 'Fib 61.8', defaultHidden: true },
      { key: 'fib78_6', label: 'Fib 78.6', defaultHidden: true },
      { key: 'fibZone', label: 'Fib Zone', defaultHidden: true },
      { key: 'fibStatus', label: 'Fib Status', defaultHidden: true },
      { key: 'fibDist', label: 'Dist. to Fib 61.8 %', defaultHidden: true },
      { key: 'actions', label: 'Actions', pinned: true },
      { key: 'colConfig', label: '', pinned: true },
    ],
  },
  {
    id: 'portfolio-options',
    label: 'Options',
    page: 'Portfolio',
    pageIcon: 'account_balance_wallet',
    columns: [
      { key: 'opt_ticker', label: 'Underlying', priority: 'primary' },
      { key: 'opt_currency', label: 'Currency' },
      { key: 'opt_type', label: 'Type', priority: 'primary' },
      { key: 'opt_expiry', label: 'Expiry', priority: 'primary' },
      { key: 'opt_strike', label: 'Strike', priority: 'primary' },
      { key: 'opt_premium', label: 'Premium' },
      { key: 'opt_contracts', label: 'Contracts' },
      { key: 'opt_cmp', label: 'CMP' },
      { key: 'opt_stockPrice', label: 'Stock Price' },
      { key: 'opt_dte', label: 'DTE' },
      { key: 'opt_cost', label: 'Cost' },
      { key: 'opt_mv', label: 'Mkt Value', priority: 'primary' },
      { key: 'opt_gl', label: 'Gain/Loss', priority: 'primary' },
      { key: 'opt_glp', label: 'Gain %' },
      { key: 'opt_state', label: 'Option State' },
      { key: 'opt_action', label: 'Action', priority: 'primary' },
      { key: 'opt_account', label: 'Account Type' },
      { key: 'opt_decision_source', label: 'Decision Source' },
      { key: 'opt_age', label: 'Age (days)' },
      { key: 'opt_actions', label: 'Actions', pinned: true },
      { key: 'colConfig', label: '', pinned: true },
    ],
  },
  {
    id: 'portfolio-cash',
    label: 'Cash',
    page: 'Portfolio',
    pageIcon: 'account_balance_wallet',
    columns: [
      { key: 'description', label: 'Description' },
      { key: 'amount', label: 'Amount' },
      { key: 'addedAt', label: 'Added' },
      { key: 'cashAccountType', label: 'Account Type' },
      { key: 'cashActions', label: 'Actions', pinned: true },
      { key: 'colConfig', label: '', pinned: true },
    ],
  },
  {
    id: 'transactions-stocks',
    label: 'Stock Transactions',
    page: 'Transactions',
    pageIcon: 'receipt_long',
    columns: [
      { key: 'tx_type', label: 'Type', priority: 'primary' },
      { key: 'tx_account', label: 'Account' },
      { key: 'tx_symbol', label: 'Ticker', priority: 'primary' },
      { key: 'tx_currency', label: 'Currency' },
      { key: 'tx_company', label: 'Company' },
      { key: 'tx_shares', label: 'Shares', priority: 'primary' },
      { key: 'tx_avg_cost', label: 'Avg Cost', priority: 'primary' },
      { key: 'tx_open_date', label: 'Open Date', priority: 'primary' },
      { key: 'tx_close_date', label: 'Close Date' },
      { key: 'tx_closing_price', label: 'Closing Price' },
      { key: 'tx_gain_loss', label: 'Gain/Loss', priority: 'primary' },
      { key: 'tx_gain_pct', label: 'Gain %' },
      { key: 'tx_last_price', label: 'Last Price' },
      { key: 'tx_price_diff', label: 'Price Diff' },
      { key: 'tx_diff_dollar', label: 'Diff $' },
      { key: 'tx_trans_date', label: 'Trans Date' },
      { key: 'tx_decision_source', label: 'Decision Source' },
      { key: 'tx_decision_source_closed', label: 'Decision Source - Closed' },
      { key: 'tx_age', label: 'Age (days)' },
      { key: 'tx_actions', label: 'Actions', pinned: true },
      { key: 'colConfig', label: '', pinned: true },
    ],
  },
  {
    id: 'transactions-options',
    label: 'Option Transactions',
    page: 'Transactions',
    pageIcon: 'receipt_long',
    columns: [
      { key: 'otx_type', label: 'Type', priority: 'primary' },
      { key: 'otx_account', label: 'Account' },
      { key: 'otx_ticker', label: 'Underlying', priority: 'primary' },
      { key: 'otx_currency', label: 'Currency' },
      { key: 'otx_position', label: 'Position', priority: 'primary' },
      { key: 'otx_expiry', label: 'Expiry', priority: 'primary' },
      { key: 'otx_strike', label: 'Strike', priority: 'primary' },
      { key: 'otx_premium', label: 'Premium' },
      { key: 'otx_contracts', label: 'Contracts' },
      { key: 'otx_open_date', label: 'Open Date' },
      { key: 'otx_close_date', label: 'Close Date' },
      { key: 'otx_closing_price', label: 'Closing Price' },
      { key: 'otx_gain_loss', label: 'Gain/Loss', priority: 'primary' },
      { key: 'otx_gain_pct', label: 'Gain %' },
      { key: 'otx_mkt_value', label: 'Current Mkt Value' },
      { key: 'otx_decision_source', label: 'Decision Source' },
      { key: 'otx_decision_source_closed', label: 'Decision Source — Closed' },
      { key: 'otx_age', label: 'Age (days)' },
      { key: 'otx_actions', label: 'Actions', pinned: true },
      { key: 'colConfig', label: '', pinned: true },
    ],
  },
  {
    id: 'scanner',
    label: 'RSI Scanner',
    page: 'Scanner',
    pageIcon: 'radar',
    columns: [
      { key: 'tracking', label: 'Tracking' },
      { key: 'symbol', label: 'Ticker', priority: 'primary' },
      { key: 'currency', label: 'Currency' },
      { key: 'rsi', label: 'RSI (14)', priority: 'primary' },
      { key: 'rsiDelta1D', label: 'RSI Δ1D' },
      { key: 'rsiSignal', label: 'RSI (9 EMA)' },
      { key: 'price', label: 'Price', priority: 'primary' },
      { key: 'change', label: 'Change', priority: 'primary' },
      { key: 'momentumShift', label: 'Trend Shift', priority: 'primary' },
      { key: 'indicators', label: 'Technical Signals' },
      { key: 'sma200', label: 'SMA 200' },
      { key: 'trendSetup200', label: 'Trend Setup' },
      { key: 'stopLoss', label: 'Stop Loss' },
      { key: 'ema9Confirmed', label: 'EMA9 Confirm' },
      { key: 'probability', label: 'Reversal P.', priority: 'primary' },
      { key: 'analystUpside', label: 'Analyst Target' },
      { key: 'gapStatus', label: 'Gap Status' },
      { key: 'priceStructure', label: 'Price Structure' },
      { key: 'status', label: 'Legacy Signal', defaultHidden: true },
      { key: 'trendSetup', label: 'Decision Trend', defaultHidden: true },
      { key: 'baseAction', label: 'Legacy Action', defaultHidden: true },
      { key: 'trigger', label: 'Trigger / Analysis', defaultHidden: true },
      { key: 'fib61_8', label: 'Fib 61.8', defaultHidden: true },
      { key: 'fibZone', label: 'Fib Zone', defaultHidden: true },
      { key: 'fibStatus', label: 'Fib Status', defaultHidden: true },
      { key: 'fibDist', label: 'Dist. to Fib 61.8 %', defaultHidden: true },
      { key: 'signalHistory', label: 'History', priority: 'primary' },
      { key: 'colConfig', label: '', pinned: true },
    ],
  },
  {
    id: 'eod-signals',
    label: 'EOD Signals',
    page: 'EOD Signals',
    pageIcon: 'timeline',
    columns: [
      { key: 'signalDate', label: 'Trading Date' },
      { key: 'daysPassed', label: 'Trading Sessions Passed' },
      { key: 'symbol', label: 'Ticker' },
      { key: 'currency', label: 'Currency' },
      { key: 'scanType', label: 'Scan' },
      { key: 'signalType', label: 'Signal' },
      { key: 'trendShift', label: 'Trend Shift' },
      { key: 'rsi', label: 'RSI' },
      { key: 'rsiDelta1D', label: 'RSI Δ1D' },
      { key: 'entryPrice', label: 'Entry Price' },
      { key: 'stopLoss', label: 'Stop Loss' },
      { key: 'riskPerShare', label: 'Risk / Share' },
      { key: 'riskPercent', label: 'Risk %' },
      { key: 'positionSizingShares', label: 'Suggested Shares' },
      { key: 'sma200', label: 'SMA 200' },
      { key: 'ema9Confirmed', label: 'EMA9 at Entry' },
      { key: 'price', label: 'Price' },
      { key: 'lastPrice', label: 'Last Price' },
      { key: 'priceDiff', label: 'Price Diff' },
      { key: 'diffPct', label: 'Diff %' },
      { key: 'reversalProbability', label: 'Reversal' },
      { key: 'volumeSignal', label: 'Volume' },
      { key: 'ruleVersion', label: 'Mode' },
      { key: 'signalState', label: 'State' },
      { key: 'actions', label: 'Actions', pinned: true },
      { key: 'colConfig', label: '', pinned: true },
    ],
  },
  {
    id: 'value-screener',
    label: 'Value Screener',
    page: 'Value Screener',
    pageIcon: 'analytics',
    columns: [
      { key: 'ticker', label: 'Ticker' },
      { key: 'currency', label: 'Currency' },
      { key: 'description', label: 'Description' },
      { key: 'technicalState', label: 'Technical State' },
      { key: 'score', label: 'Score' },
      { key: 'actionTrigger', label: 'Action Trigger' },
      { key: 'colConfig', label: '', pinned: true },
    ],
  },
  {
    id: 'watchlist',
    label: 'Watchlist',
    page: 'Watchlist',
    pageIcon: 'visibility',
    columns: [
      { key: 'symbol', label: 'Ticker', priority: 'primary' },
      { key: 'company', label: 'Description', priority: 'primary' },
      { key: 'currency', label: 'Currency' },
      { key: 'role', label: 'Role' },
      { key: 'earningsDate', label: 'Earnings Date' },
      { key: 'price', label: 'Last Price', priority: 'primary' },
      { key: 'change', label: 'Change $' },
      { key: 'changePct', label: 'Change %', priority: 'primary' },
      { key: 'analystTarget', label: 'Analyst Target' },
      { key: 'week52', label: '52W Range' },
      { key: 'sector', label: 'Sector' },
      { key: 'rsi', label: 'RSI (14)', priority: 'primary' },
      { key: 'trendSetup', label: 'Trend Setup' },
      { key: 'reversalP', label: 'Reversal P.' },
      { key: 'momentumShift', label: 'Momentum Shift' },
      { key: 'channel', label: 'CHANNEL' },
      { key: 'priceStructure', label: 'Price Structure' },
      { key: 'buyScore', label: 'Buy Score' },
      { key: 'gapStatus', label: 'Gap Status' },
      { key: 'finalAction', label: 'Final Action', priority: 'primary' },
      { key: 'technical', label: 'Technical' },
      { key: 'valueScore', label: 'Value Score' },
      { key: 'valueStatus', label: 'Value Status' },
      { key: 'maStatus', label: 'MA Status' },
      { key: 'fibSwing', label: 'Fib Swing High/Low', defaultHidden: true },
      { key: 'fib38_2', label: 'Fib 38.2', defaultHidden: true },
      { key: 'fib50', label: 'Fib 50', defaultHidden: true },
      { key: 'fib61_8', label: 'Fib 61.8', defaultHidden: true },
      { key: 'fib78_6', label: 'Fib 78.6', defaultHidden: true },
      { key: 'fibZone', label: 'Fib Zone', defaultHidden: true },
      { key: 'fibStatus', label: 'Fib Status', defaultHidden: true },
      { key: 'fibDist', label: 'Dist. to Fib 61.8 %', defaultHidden: true },
      { key: 'actions', label: 'Actions', pinned: true },
      { key: 'colConfig', label: '', pinned: true },
    ],
  },
];
