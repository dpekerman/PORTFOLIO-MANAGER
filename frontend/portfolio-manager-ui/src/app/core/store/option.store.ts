import { computed, effect, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import {
  AddOptionItemRequest,
  OptionAnalysis,
  OptionItem,
  OptionState,
  OptionTechnicalData,
  UpdateOptionItemRequest,
} from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { TradeCashLinkService } from '../services/trade-cash-link.service';
import { AccountTypesStore } from './account-types.store';

interface OptionStoreState {
  items: OptionItem[];
  /** Technical data per underlying ticker, keyed UPPERCASE. Missing entries degrade to MONITOR. */
  technicalMap: Map<string, OptionTechnicalData>;
  loading: boolean;
}

/**
 * Open option positions plus the derived per-position analysis (state, suggested action, P&L).
 *
 * Premiums and market prices are per share, so every money figure multiplies by the 100-share
 * contract multiplier. Totals exclude CLOSE rows. The rules engine below is pure and verbatim from
 * the trading playbook — its thresholds, wording and ordering are the product, not an
 * implementation detail, so edit it only with a matching playbook change.
 */
export const OptionStore = signalStore(
  { providedIn: 'root' },
  withState<OptionStoreState>({ items: [], technicalMap: new Map(), loading: false }),
  withComputed(({ items, technicalMap }) => {
    const analyses = computed<OptionAnalysis[]>(() =>
      items().map((item) =>
        buildAnalysis(item, technicalMap().get(item.underlyingTicker.toUpperCase()) ?? null),
      ),
    );

    return {
      analyses,
      totalMarketValue: computed(() =>
        analyses()
          .filter((a) => a.item.transactionType !== 'CLOSE')
          .reduce((acc, a) => acc + a.marketValue, 0),
      ),
      totalCost: computed(() =>
        analyses()
          .filter((a) => a.item.transactionType !== 'CLOSE')
          .reduce((acc, a) => acc + a.cost, 0),
      ),
    };
  }),
  withMethods(
    (
      store,
      api = inject(PortfolioApiService),
      snackBar = inject(MatSnackBar),
      tradeCashLink = inject(TradeCashLinkService),
    ) => {
      const fetchTechnicalData = (symbol: string): void => {
        api.getOptionTechnicalData(symbol).subscribe({
          next: (data) => {
            patchState(store, ({ technicalMap }) => {
              const copy = new Map(technicalMap);
              copy.set(symbol.toUpperCase(), data);
              return { technicalMap: copy };
            });
          },
          error: () => {
            // Silently fail; option state will show MONITOR without technical data
          },
        });
      };

      return {
        refresh(): void {
          patchState(store, { loading: true });
          api.getOptionItems().subscribe({
            next: (data) => {
              patchState(store, { items: data, loading: false });
              // Load technical data for all unique underlying tickers
              const tickers = [...new Set(data.map((x) => x.underlyingTicker))];
              for (const ticker of tickers) {
                fetchTechnicalData(ticker);
              }
            },
            error: (err) => {
              patchState(store, { loading: false });
              console.error('Failed to load option items', err);
            },
          });
        },

        /** Applies an account-type rename to every position that referenced the old name. */
        applyAccountTypeRename(oldName: string, newName: string): void {
          patchState(store, ({ items }) => ({
            items: items.map((x) =>
              x.accountType === oldName ? { ...x, accountType: newName } : x,
            ),
          }));
        },

        addItem(request: AddOptionItemRequest): Promise<OptionItem> {
          return new Promise((resolve, reject) => {
            api.addOptionItem(request).subscribe({
              next: (item) => {
                patchState(store, ({ items }) => ({ items: [...items, item] }));
                fetchTechnicalData(item.underlyingTicker);
                snackBar.open('Option position added', 'Dismiss', { duration: 3000 });
                resolve(item);
              },
              error: (err) => {
                snackBar.open('Failed to add option position', 'Dismiss', { duration: 4000 });
                reject(err);
              },
            });
          });
        },

        updateItem(id: number, request: UpdateOptionItemRequest): Promise<void> {
          const prev = store.items().find((x) => x.id === id) ?? null;
          return new Promise((resolve, reject) => {
            api.updateOptionItem(id, request).subscribe({
              next: (updated) => {
                patchState(store, ({ items }) => ({
                  items: items.map((x) => (x.id === id ? updated : x)),
                }));
                fetchTechnicalData(updated.underlyingTicker);
                snackBar.open('Option position updated', 'Dismiss', { duration: 3000 });
                resolve();
                if (prev) void tradeCashLink.offerForOption(prev, updated);
              },
              error: (err) => {
                snackBar.open('Failed to update option position', 'Dismiss', { duration: 4000 });
                reject(err);
              },
            });
          });
        },

        deleteItem(id: number): void {
          const removed = store.items().find((x) => x.id === id);
          api.deleteOptionItem(id).subscribe({
            next: () => {
              patchState(store, ({ items }) => ({ items: items.filter((x) => x.id !== id) }));
              snackBar.open('Option position removed', 'Dismiss', { duration: 3000 });
              if (removed) {
                void tradeCashLink.offerRemoveForOption(
                  id,
                  `${removed.underlyingTicker} ${removed.positionType} $${removed.strike}`,
                );
              }
            },
            error: () => {
              snackBar.open('Failed to remove option position', 'Dismiss', { duration: 4000 });
            },
          });
        },

        patchItemNotes(id: number, notes: string | null): void {
          patchState(store, ({ items }) => ({
            items: items.map((x) => (x.id === id ? { ...x, notes } : x)),
          }));
        },
      };
    },
  ),
  withHooks({
    onInit(store, accountTypes = inject(AccountTypesStore)) {
      effect(() => {
        const rename = accountTypes.renamed();
        if (rename) store.applyAccountTypeRename(rename.oldName, rename.newName);
      });
      store.refresh();
    },
  }),
);

/** Pure: turns a position plus its (optional) technical data into the displayed analysis row. */
function buildAnalysis(item: OptionItem, technical: OptionTechnicalData | null): OptionAnalysis {
  // Premium and market price are per share; every option contract covers 100 shares.
  const cost = item.premium * item.numberOfContracts * 100;
  const marketValue = item.marketPrice * item.numberOfContracts * 100;
  const gainLoss = marketValue - cost;
  const gainLossPct = cost > 0 ? (gainLoss / cost) * 100 : 0;

  const today = new Date();
  const expiry = new Date(item.expirationDate);
  const dte = Math.max(
    0,
    Math.ceil((expiry.getTime() - today.getTime()) / (1000 * 60 * 60 * 24)),
  );

  const { state, stateDesc, action, actionDesc } = evaluateOptionState(
    item,
    technical,
    dte,
    cost,
    marketValue,
  );

  return {
    item,
    technical,
    optionState: state,
    stateDescription: stateDesc,
    action,
    actionDescription: actionDesc,
    stockPrice: technical?.currentPrice ?? null,
    dte,
    cost,
    marketValue,
    gainLoss,
    gainLossPct,
  };
}

// ── Option State Rules Engine ─────────────────────────────────────────────

function evaluateOptionState(
  item: OptionItem,
  td: OptionTechnicalData | null,
  dte: number,
  cost: number,
  marketValue: number,
): { state: OptionState; stateDesc: string; action: string; actionDesc: string } {
  const isPut = item.positionType === 'PUT';
  const isCall = item.positionType === 'CALL';

  // FREE_TRADE_MILESTONE: applies to all DTE buckets for both CALL and PUT
  if (cost > 0 && marketValue >= cost * 2) {
    return {
      state: 'FREE_TRADE_MILESTONE',
      stateDesc:
        'The option contract has doubled (100% gain) relative to your manual entry cost.',
      action: '🟩 SELL HALF / RIDE FREE',
      actionDesc:
        'Sell exactly 50% of your contracts. This extracts your initial capital, leaving a stress-free riskless trade.',
    };
  }

  if (dte < 14) {
    return evaluateShortDte(item, td, isPut, isCall);
  } else if (dte < 44) {
    return evaluateSwingDte(item, td, isPut, isCall);
  } else {
    return evaluateLongDte(item, td, isPut, isCall);
  }
}

// ── DTE < 14 ──────────────────────────────────────────────────────────────
function evaluateShortDte(
  item: OptionItem,
  td: OptionTechnicalData | null,
  isPut: boolean,
  isCall: boolean,
): { state: OptionState; stateDesc: string; action: string; actionDesc: string } {
  const price = td?.currentPrice;

  if (isPut && price !== undefined) {
    // INTRINSIC_CRACKED: stock > Strike * 1.005
    const intrinsicCrackedLevel = item.strike * 1.005;
    if (price > intrinsicCrackedLevel) {
      return {
        state: 'INTRINSIC_CRACKED',
        stateDesc:
          "The stock has moved past our safety threshold, destroying the option's intrinsic value buffer.",
        action: '🟥 CUT PUTS (DELTA DEFENSE)',
        actionDesc:
          'Exit immediately. As the option goes out-of-the-money, time decay will aggressively vaporize the remaining premium.',
      };
    }
    // TEMPORARILY_BROKEN: daily close above yesterdayHigh (approximation for 30-min bar)
    if (td && price > td.yesterdayHigh) {
      return {
        state: 'TEMPORARILY_BROKEN',
        stateDesc: "The stock broke above yesterday's high, invalidating our short thesis.",
        action: '🟥 CUT PUTS IMMEDIATELY',
        actionDesc:
          'Exit right now. Do not wait. Near-term options decay too fast to hold through an invalidation.',
      };
    }
    // TARGET_ACHIEVED: stock price touches or falls to 20-day SMA
    if (td && price <= td.sma20) {
      return {
        state: 'TARGET_ACHIEVED',
        stateDesc: 'The stock successfully pulled back to its monthly average baseline.',
        action: '🟩 TAKE PROFIT (MID-BAND)',
        actionDesc: 'Close your puts to harvest profits. Mid-band support often attracts buyers.',
      };
    }
    // VELOCITY_INVERSION: RSI crosses above its 9-period EMA Signal
    if (td?.rsiSignalAvailable && td.rsi14 > td.rsiSignal9) {
      return {
        state: 'VELOCITY_INVERSION',
        stateDesc: 'The daily selling speed has officially reversed and shifted upwards.',
        action: '🟨 TAKE PROFIT (MOMENTUM SHIFT)',
        actionDesc:
          'Close your puts. The momentum has changed gears; do not wait for the price to follow.',
      };
    }
  }

  if (isCall && price !== undefined) {
    // INTRINSIC_CRACKED: stock < Strike * 0.995
    const intrinsicCrackedLevel = item.strike * 0.995;
    if (price < intrinsicCrackedLevel) {
      return {
        state: 'INTRINSIC_CRACKED',
        stateDesc:
          "The stock has moved past our safety threshold, destroying the option's intrinsic value buffer.",
        action: '🟥 CUT CALLS (DELTA DEFENSE)',
        actionDesc:
          'Exit immediately. As the option goes out-of-the-money, time decay will aggressively vaporize the remaining premium.',
      };
    }
    // TEMPORARILY_BROKEN: daily close below yesterdayLow
    if (td && price < td.yesterdayLow) {
      return {
        state: 'TEMPORARILY_BROKEN',
        stateDesc:
          "The stock broke below yesterday's low on a daily basis, invalidating our long thesis.",
        action: '🟥 CUT CALLS IMMEDIATELY',
        actionDesc:
          'Exit right now. Near-term calls will bleed premium rapidly on a support breakdown.',
      };
    }
    // TARGET_ACHIEVED: stock price hits or climbs above 20-day SMA
    if (td && price >= td.sma20) {
      return {
        state: 'TARGET_ACHIEVED',
        stateDesc: 'The stock successfully bounced back to its monthly average baseline.',
        action: '🟩 TAKE PROFIT (MID-BAND)',
        actionDesc:
          'Close your calls to lock in gains. Mid-band resistance often attracts sellers.',
      };
    }
    // VELOCITY_INVERSION: RSI crosses back below its 9-period EMA Signal
    if (td?.rsiSignalAvailable && td.rsi14 < td.rsiSignal9) {
      return {
        state: 'VELOCITY_INVERSION',
        stateDesc: 'The daily buying speed has officially reversed and shifted downwards.',
        action: '🟨 TAKE PROFIT (MOMENTUM SHIFT)',
        actionDesc: 'Close your calls. Upward momentum has run out of gas.',
      };
    }
  }

  return {
    state: 'MONITOR',
    stateDesc:
      'The stock is performing within expected boundaries. No protective stops or targets have been triggered.',
    action: '⬜ STAND BY / MONITOR',
    actionDesc: 'No action required. Let the short momentum work toward the target zone.',
  };
}

// ── 14 ≤ DTE < 44 ─────────────────────────────────────────────────────────
function evaluateSwingDte(
  item: OptionItem,
  td: OptionTechnicalData | null,
  isPut: boolean,
  isCall: boolean,
): { state: OptionState; stateDesc: string; action: string; actionDesc: string } {
  const price = td?.currentPrice;

  if (isPut && price !== undefined) {
    // TREND_REVERSED: daily close above 21-EMA
    if (td && price > td.ema21) {
      return {
        state: 'TREND_REVERSED',
        stateDesc:
          'The daily candle close broke above the 21-day EMA cushion, proving the short-term trend has structurally shifted upward.',
        action: '🟥 EXIT PUTS (21-EMA TRIGGER)',
        actionDesc:
          'Close the position. Breaking the 21-EMA is a reliable sign that macro downward momentum has officially died.',
      };
    }
    // VOLATILITY_EXPANSION: stock falls below previousClose - 1.5×ATR
    if (td && price < td.previousClose - 1.5 * td.atr14) {
      return {
        state: 'VOLATILITY_EXPANSION',
        stateDesc: 'The stock has dropped deeper than its normal daily volatility allows.',
        action: '🟩 HARVEST PROFITS (ATR ALERT)',
        actionDesc:
          'Take profits on your puts. This is an extreme daily exhaustion move; expect a short-term bounce.',
      };
    }
    // TEMPORARILY_BROKEN: daily close above yesterdayHigh + 0.5×ATR
    if (td && price > td.yesterdayHigh + 0.5 * td.atr14) {
      return {
        state: 'TEMPORARILY_BROKEN',
        stateDesc: 'The stock breached our volatility-adjusted stop ceiling on a daily close.',
        action: '🟥 CUT PUTS IMMEDIATELY',
        actionDesc:
          'Exit the position. The break of this cushioned stop invalidates our swing thesis.',
      };
    }
    // TARGET_ACHIEVED: price < SMA20 AND RSI < 50
    if (td && price < td.sma20 && td.rsi14 < 50) {
      return {
        state: 'TARGET_ACHIEVED',
        stateDesc:
          'The stock reached its average price baseline and the overbought momentum has fully neutralized.',
        action: '🟩 TAKE PROFIT (NEUTRAL ZONE)',
        actionDesc:
          'Close puts here. The stock is back to its fair-value zone and the momentum advantage is gone.',
      };
    }
  }

  if (isCall && price !== undefined) {
    // TREND_REVERSED: daily close below 21-EMA
    if (td && price < td.ema21) {
      return {
        state: 'TREND_REVERSED',
        stateDesc:
          'The daily candle close broke below the 21-day EMA support, proving the short-term trend has structurally shifted downward.',
        action: '🟥 EXIT CALLS (21-EMA TRIGGER)',
        actionDesc:
          'Close the position. Breaking the 21-EMA is a reliable sign that macro upward momentum has officially died.',
      };
    }
    // TEMPORARILY_BROKEN: daily close below yesterdayLow - 0.5×ATR
    if (td && price < td.yesterdayLow - 0.5 * td.atr14) {
      return {
        state: 'TEMPORARILY_BROKEN',
        stateDesc: 'The stock breached our volatility-adjusted support floor on a daily close.',
        action: '🟥 CUT CALLS IMMEDIATELY',
        actionDesc:
          'Exit the position. The breakdown below this cushioned support invalidates our swing thesis.',
      };
    }
    // TARGET_ACHIEVED: price > SMA20 AND RSI > 50
    if (td && price > td.sma20 && td.rsi14 > 50) {
      return {
        state: 'TARGET_ACHIEVED',
        stateDesc:
          'The stock reached its average price baseline and the oversold momentum has fully neutralized.',
        action: '🟩 TAKE PROFIT (NEUTRAL ZONE)',
        actionDesc:
          'Close calls here. The stock is back to fair-value and the bounce momentum has cooled.',
      };
    }
  }

  return {
    state: 'MONITOR',
    stateDesc:
      'The swing structure is intact. Price is holding and has not reached the profit targets.',
    action: '⬜ STAND BY / MONITOR',
    actionDesc:
      'Hold the position. Let the swing develop and continue to monitor for boundary triggers.',
  };
}

// ── DTE ≥ 44 ──────────────────────────────────────────────────────────────
function evaluateLongDte(
  item: OptionItem,
  td: OptionTechnicalData | null,
  isPut: boolean,
  isCall: boolean,
): { state: OptionState; stateDesc: string; action: string; actionDesc: string } {
  const price = td?.currentPrice;

  if (isPut && price !== undefined) {
    // TEMPORARILY_BROKEN: daily close above yesterdayHigh + 1.0×ATR
    if (td && price > td.yesterdayHigh + td.atr14) {
      return {
        state: 'TEMPORARILY_BROKEN',
        stateDesc:
          'The stock has closed the day above our macro volatility-adjusted stop ceiling.',
        action: '🟥 EXIT PUTS (DAILY CLOSE)',
        actionDesc: 'Exit the position. The macro daily structure has broken down for the bears.',
      };
    }
    // TARGET_ACHIEVED: price ≤ bollingerLower OR price ≤ SMA50 OR RSI < 35
    if (td && (price <= td.bollingerLower || price <= td.sma50 || td.rsi14 < 35)) {
      return {
        state: 'TARGET_ACHIEVED',
        stateDesc: 'The stock has washed out to major cyclical floors or oversold extremes.',
        action: '🟩 TAKE PROFIT (OVERSOLD TARGET)',
        actionDesc:
          'Close puts to capture max macro gains. This is a primary long-term buying zone.',
      };
    }
  }

  if (isCall && price !== undefined) {
    // TEMPORARILY_BROKEN: daily close below yesterdayLow - 1.0×ATR
    if (td && price < td.yesterdayLow - td.atr14) {
      return {
        state: 'TEMPORARILY_BROKEN',
        stateDesc: 'The stock closed the day below our macro volatility-adjusted support floor.',
        action: '🟥 EXIT CALLS (DAILY CLOSE)',
        actionDesc: 'Exit the position. The macro trend support has structurally failed.',
      };
    }
    // TARGET_ACHIEVED: price ≥ bollingerUpper OR price ≥ SMA50 OR RSI > 65
    if (td && (price >= td.bollingerUpper || price >= td.sma50 || td.rsi14 > 65)) {
      return {
        state: 'TARGET_ACHIEVED',
        stateDesc: 'The stock has expanded to major cyclical ceilings or overbought extremes.',
        action: '🟩 TAKE PROFIT (OVERBOUGHT TARGET)',
        actionDesc:
          'Close calls to secure max macro gains. This is a primary long-term selling zone.',
      };
    }
  }

  return {
    state: 'MONITOR',
    stateDesc:
      'Macro parameters are normal. Noise is expected; no long-term targets or stops have been breached.',
    action: '⬜ STAND BY / MONITOR',
    actionDesc: 'Hold long-term position. Let the macro cycle play out over weeks or months.',
  };
}
