import { computed, Injectable, signal } from '@angular/core';

// ── Public Interfaces ─────────────────────────────────────────────────────────

export interface ColumnDef {
  key: string;
  label: string;
  /** Pinned columns are always visible and always rendered last; they cannot be hidden or reordered. */
  pinned?: boolean;
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
      { key: 'holdingRole', label: 'Role' },
      { key: 'trendSetup', label: 'Trend Setup' },
      { key: 'momentumShift', label: 'Momentum Shift' },
      { key: 'finalAction', label: 'Final Action' },
      { key: 'actions', label: 'Actions', pinned: true },
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
      { key: 'opt_actions', label: 'Actions', pinned: true },
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
      { key: 'cashActions', label: 'Actions', pinned: true },
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
      { key: 'tx_actions', label: 'Actions', pinned: true },
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
      { key: 'otx_actions', label: 'Actions', pinned: true },
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
      { key: 'rsiSignal', label: 'RSI (9 EMA)' },
      { key: 'price', label: 'Price' },
      { key: 'change', label: 'Change' },
      { key: 'analystUpside', label: 'Analyst Target' },
      { key: 'probability', label: 'Reversal P.' },
      { key: 'trendSetup', label: 'Trend Setup' },
      { key: 'momentumShift', label: 'Momentum Shift' },
      { key: 'baseAction', label: 'Base Action' },
      { key: 'status', label: 'Signal' },
      { key: 'trigger', label: 'Trigger / Analysis' },
      { key: 'signalHistory', label: 'History' },
    ],
  },
  {
    id: 'eod-signals',
    label: 'EOD Signals',
    page: 'EOD Signals',
    pageIcon: 'timeline',
    columns: [
      { key: 'signalDate', label: 'Date' },
      { key: 'symbol', label: 'Ticker' },
      { key: 'scanType', label: 'Scan' },
      { key: 'signalType', label: 'Signal' },
      { key: 'rsi', label: 'RSI' },
      { key: 'price', label: 'Price' },
      { key: 'reversalProbability', label: 'Reversal' },
      { key: 'volumeSignal', label: 'Volume' },
      { key: 'ruleVersion', label: 'Mode' },
      { key: 'signalState', label: 'State' },
      { key: 'actions', label: 'Actions', pinned: true },
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
      { key: 'change', label: 'Change' },
      { key: 'analystTarget', label: 'Analyst Target' },
      { key: 'week52', label: '52W Range' },
      { key: 'sector', label: 'Sector' },
      { key: 'rsi', label: 'RSI (14)' },
      { key: 'trendSetup', label: 'Trend Setup' },
      { key: 'momentumShift', label: 'Momentum Shift' },
      { key: 'buyScore', label: 'Buy Score' },
      { key: 'finalAction', label: 'Final Action' },
      { key: 'notes', label: 'Notes', pinned: true },
      { key: 'actions', label: 'Actions', pinned: true },
    ],
  },
];

// ── Service ───────────────────────────────────────────────────────────────────

const LS_KEY = 'pm_grid_columns_v1';

type StoredPrefs = Record<string, ColumnPreference[]>;

@Injectable({ providedIn: 'root' })
export class GridColumnService {
  private readonly _prefs = signal<StoredPrefs>(this.loadFromStorage());

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
        result.push({ key: col.key, visible: true });
      }
    }

    return result;
  }

  /** Persist updated preferences for a grid. */
  updatePrefs(gridId: string, prefs: ColumnPreference[]): void {
    this._prefs.update((current) => ({ ...current, [gridId]: prefs }));
    this.saveToStorage(this._prefs());
  }

  /** Clear saved preferences for one grid (restores defaults). */
  resetGrid(gridId: string): void {
    this._prefs.update((current) => {
      const next = { ...current };
      delete next[gridId];
      return next;
    });
    this.saveToStorage(this._prefs());
  }

  /** Clear ALL saved column preferences. */
  resetAll(): void {
    this._prefs.set({});
    try {
      localStorage.removeItem(LS_KEY);
    } catch {
      // storage unavailable — ignore
    }
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
