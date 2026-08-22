import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { UserPreferencesStateService } from './user-preferences-state.service';

// ── Public Interfaces ─────────────────────────────────────────────────────────

export interface ColumnDef {
  key: string;
  label: string;
  /** Pinned columns are always visible and always rendered last; they cannot be hidden or reordered. */
  pinned?: boolean;
  /** When true, this column is hidden by default for users who have not saved a preference. */
  defaultHidden?: boolean;
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

// ── Grid Registry ─────────────────────────────────────────────────────────────

export const GRID_REGISTRY: GridDef[] = [
  {
    id: 'portfolio-stocks',
    label: 'Stocks',
    page: 'Portfolio',
    pageIcon: 'account_balance_wallet',
    columns: [
      { key: 'symbol', label: 'Ticker' },
      { key: 'company', label: 'Company' },
      { key: 'accountType', label: 'Account' },
      { key: 'sector', label: 'Sector' },
      { key: 'industry', label: 'Industry' },
      { key: 'shares', label: 'Shares' },
      { key: 'avgCost', label: 'Avg Cost' },
      { key: 'price', label: 'Last Price' },
      { key: 'analystTarget', label: 'Analyst Target' },
      { key: 'changePct', label: 'Day %' },
      { key: 'dayGain', label: 'Day $' },
      { key: 'marketValue', label: 'Mkt Value' },
      { key: 'portfolioPct', label: '% Total' },
      { key: 'gainLoss', label: 'Gain/Loss' },
      { key: 'gainLossPct', label: 'Gain %' },
      { key: 'rsi', label: 'RSI (14)' },
      { key: 'technical', label: 'Technical' },
      { key: 'holdingRole', label: 'Role' },
      { key: 'decisionSource', label: 'Decision Source' },
      { key: 'trendSetup', label: 'Trend Setup' },
      { key: 'reversalP', label: 'Reversal P.' },
      { key: 'momentumShift', label: 'Momentum Shift' },
      { key: 'gapStatus', label: 'Gap Status' },
      { key: 'finalAction', label: 'Final Action' },
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
      { key: 'opt_ticker', label: 'Underlying' },
      { key: 'opt_type', label: 'Type' },
      { key: 'opt_expiry', label: 'Expiry' },
      { key: 'opt_strike', label: 'Strike' },
      { key: 'opt_premium', label: 'Premium' },
      { key: 'opt_contracts', label: 'Contracts' },
      { key: 'opt_cmp', label: 'CMP' },
      { key: 'opt_stockPrice', label: 'Stock Price' },
      { key: 'opt_dte', label: 'DTE' },
      { key: 'opt_cost', label: 'Cost' },
      { key: 'opt_mv', label: 'Mkt Value' },
      { key: 'opt_gl', label: 'Gain/Loss' },
      { key: 'opt_glp', label: 'Gain %' },
      { key: 'opt_state', label: 'Option State' },
      { key: 'opt_action', label: 'Action' },
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
      { key: 'tx_type', label: 'Type' },
      { key: 'tx_account', label: 'Account' },
      { key: 'tx_symbol', label: 'Ticker' },
      { key: 'tx_company', label: 'Company' },
      { key: 'tx_shares', label: 'Shares' },
      { key: 'tx_avg_cost', label: 'Avg Cost' },
      { key: 'tx_open_date', label: 'Open Date' },
      { key: 'tx_close_date', label: 'Close Date' },
      { key: 'tx_closing_price', label: 'Closing Price' },
      { key: 'tx_gain_loss', label: 'Gain/Loss' },
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
      { key: 'otx_type', label: 'Type' },
      { key: 'otx_account', label: 'Account' },
      { key: 'otx_ticker', label: 'Underlying' },
      { key: 'otx_position', label: 'Position' },
      { key: 'otx_expiry', label: 'Expiry' },
      { key: 'otx_strike', label: 'Strike' },
      { key: 'otx_premium', label: 'Premium' },
      { key: 'otx_contracts', label: 'Contracts' },
      { key: 'otx_open_date', label: 'Open Date' },
      { key: 'otx_close_date', label: 'Close Date' },
      { key: 'otx_closing_price', label: 'Closing Price' },
      { key: 'otx_gain_loss', label: 'Gain/Loss' },
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
      { key: 'symbol', label: 'Ticker' },
      { key: 'rsi', label: 'RSI (14)' },
      { key: 'rsiDelta1D', label: 'RSI Δ1D' },
      { key: 'rsiSignal', label: 'RSI (9 EMA)' },
      { key: 'price', label: 'Price' },
      { key: 'change', label: 'Change' },
      { key: 'momentumShift', label: 'Trend Shift' },
      { key: 'indicators', label: 'Technical Signals' },
      { key: 'sma200', label: 'SMA 200' },
      { key: 'trendSetup200', label: 'Trend Setup' },
      { key: 'stopLoss', label: 'Stop Loss' },
      { key: 'ema9Confirmed', label: 'EMA9 Confirm' },
      { key: 'probability', label: 'Reversal P.' },
      { key: 'analystUpside', label: 'Analyst Target' },
      { key: 'gapStatus', label: 'Gap Status' },
      { key: 'status', label: 'Legacy Signal', defaultHidden: true },
      { key: 'trendSetup', label: 'Decision Trend', defaultHidden: true },
      { key: 'baseAction', label: 'Legacy Action', defaultHidden: true },
      { key: 'trigger', label: 'Trigger / Analysis', defaultHidden: true },
      { key: 'fib61_8', label: 'Fib 61.8', defaultHidden: true },
      { key: 'fibZone', label: 'Fib Zone', defaultHidden: true },
      { key: 'fibStatus', label: 'Fib Status', defaultHidden: true },
      { key: 'fibDist', label: 'Dist. to Fib 61.8 %', defaultHidden: true },
      { key: 'signalHistory', label: 'History' },
      { key: 'colConfig', label: '', pinned: true },
    ],
  },
  {
    id: 'eod-signals',
    label: 'EOD Signals',
    page: 'EOD Signals',
    pageIcon: 'timeline',
    columns: [
      { key: 'signalDate', label: 'Date' },
      { key: 'daysPassed', label: 'Days Passed' },
      { key: 'symbol', label: 'Ticker' },
      { key: 'scanType', label: 'Scan' },
      { key: 'signalType', label: 'Signal' },
      { key: 'trendShift', label: 'Trend Shift' },
      { key: 'rsi', label: 'RSI' },
      { key: 'rsiDelta1D', label: 'RSI Δ1D' },
      { key: 'entryPrice', label: 'Entry Price' },
      { key: 'stopLoss', label: 'Stop Loss' },
      { key: 'riskPerShare', label: 'Risk / Share' },
      { key: 'riskPercent', label: 'Risk %' },
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
      { key: 'symbol', label: 'Ticker' },
      { key: 'company', label: 'Description' },
      { key: 'role', label: 'Role' },
      { key: 'price', label: 'Last Price' },
      { key: 'change', label: 'Change $' },
      { key: 'changePct', label: 'Change %' },
      { key: 'analystTarget', label: 'Analyst Target' },
      { key: 'week52', label: '52W Range' },
      { key: 'sector', label: 'Sector' },
      { key: 'rsi', label: 'RSI (14)' },
      { key: 'trendSetup', label: 'Trend Setup' },
      { key: 'reversalP', label: 'Reversal P.' },
      { key: 'momentumShift', label: 'Momentum Shift' },
      { key: 'buyScore', label: 'Buy Score' },
      { key: 'gapStatus', label: 'Gap Status' },
      { key: 'finalAction', label: 'Final Action' },
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

// ── Service ───────────────────────────────────────────────────────────────────

const LS_KEY = 'pm_grid_columns_v1';

type StoredPrefs = Record<string, ColumnPreference[]>;

@Injectable({ providedIn: 'root' })
export class GridColumnService {
  private readonly userPrefs = inject(UserPreferencesStateService);
  private readonly _prefs = signal<StoredPrefs>(this.loadFromStorage());

  constructor() {
    // When DB preferences load, overlay them onto the signal (DB wins over localStorage)
    effect(() => {
      const dbValue = this.userPrefs.get<StoredPrefs>(LS_KEY);
      if (dbValue) {
        this._prefs.set(dbValue);
      }
    });
  }

  /** All registered grids. */
  readonly grids = GRID_REGISTRY;

  /**
   * Returns a computed signal of visible column keys for a grid, in user-configured order.
   * Results are cached per gridId so the same Signal instance is returned on repeated calls.
   */
  private readonly _colSignals = new Map<string, ReturnType<typeof computed<string[]>>>();

  getColumnKeys(gridId: string): ReturnType<typeof computed<string[]>> {
    if (!this._colSignals.has(gridId)) {
      this._colSignals.set(
        gridId,
        computed(() => {
          const gridDef = GRID_REGISTRY.find((g) => g.id === gridId);
          if (!gridDef) return [];
          return this.resolveKeys(gridId, gridDef);
        }),
      );
    }
    return this._colSignals.get(gridId)!;
  }

  /**
   * Returns a mutable working copy of preferences for editing in the dialog.
   * Non-pinned columns only — pinned columns are always appended automatically.
   */
  getEditablePrefs(gridId: string): ColumnPreference[] {
    const gridDef = GRID_REGISTRY.find((g) => g.id === gridId);
    if (!gridDef) return [];

    const nonPinned = gridDef.columns.filter((c) => !c.pinned);
    const storedPrefs = this._prefs()[gridId];

    if (!storedPrefs) {
      return nonPinned.map((c) => ({ key: c.key, visible: true }));
    }

    const validKeys = new Set(nonPinned.map((c) => c.key));
    const result: ColumnPreference[] = [];

    // Respect stored order; skip removed columns
    for (const pref of storedPrefs) {
      if (validKeys.has(pref.key)) {
        result.push({ ...pref });
      }
    }

    // Append any NEW default columns not yet in storage (forward-compat)
    const prefKeys = new Set(storedPrefs.map((p) => p.key));
    for (const col of nonPinned) {
      if (!prefKeys.has(col.key)) {
        result.push({ key: col.key, visible: !col.defaultHidden });
      }
    }

    return result;
  }

  /** Persist updated preferences for a grid. */
  updatePrefs(gridId: string, prefs: ColumnPreference[]): void {
    this._prefs.update((current) => ({ ...current, [gridId]: prefs }));
    this.saveToStorage(this._prefs());
    this.userPrefs.set(LS_KEY, this._prefs()); // write-through to DB
  }

  /** Clear saved preferences for one grid (restores defaults). */
  resetGrid(gridId: string): void {
    this._prefs.update((current) => {
      const next = { ...current };
      delete next[gridId];
      return next;
    });
    this.saveToStorage(this._prefs());
    this.userPrefs.set(LS_KEY, this._prefs());
  }

  /** Clear ALL saved column preferences. */
  resetAll(): void {
    this._prefs.set({});
    try {
      localStorage.removeItem(LS_KEY);
    } catch {
      // storage unavailable — ignore
    }
    this.userPrefs.remove(LS_KEY);
  }

  // ── Private helpers ─────────────────────────────────────────────────────────

  private resolveKeys(gridId: string, gridDef: GridDef): string[] {
    const prefs = this._prefs()[gridId];
    const pinnedKeys = gridDef.columns.filter((c) => c.pinned).map((c) => c.key);
    const nonPinned = gridDef.columns.filter((c) => !c.pinned);

    if (!prefs) {
      // No customisation — return all defaults
      return gridDef.columns.map((c) => c.key);
    }

    const validKeys = new Set(nonPinned.map((c) => c.key));
    const prefKeys = new Set(prefs.map((p) => p.key));
    const result: string[] = [];

    // Restore saved order & visibility
    for (const pref of prefs) {
      if (validKeys.has(pref.key) && pref.visible) {
        result.push(pref.key);
      }
    }

    // Forward-compat: append any NEW default columns not yet in storage
    for (const col of nonPinned) {
      if (!prefKeys.has(col.key)) {
        result.push(col.key);
      }
    }

    // Pinned columns always come last
    result.push(...pinnedKeys);

    return result;
  }

  private loadFromStorage(): StoredPrefs {
    try {
      const raw = localStorage.getItem(LS_KEY);
      return raw ? (JSON.parse(raw) as StoredPrefs) : {};
    } catch {
      return {};
    }
  }

  private saveToStorage(prefs: StoredPrefs): void {
    try {
      localStorage.setItem(LS_KEY, JSON.stringify(prefs));
    } catch {
      // Quota exceeded or storage unavailable — fail silently
    }
  }
}
