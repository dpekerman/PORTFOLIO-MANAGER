import { computed, effect, inject, untracked } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap } from 'rxjs';
import { FinalActionResult } from '../models/portfolio.models';
import type {
  BaseAction,
  GapStatus,
  MomentumShift,
  PageDecision,
  TrendSetup,
} from '../services/decision-engine.service';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { AppRefreshStore } from './app-refresh.store';
import { AuthStore } from './auth.store';
import { onceAuthenticated } from './features/auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';
import { PortfolioStore } from './portfolio.store';
import { WatchlistStore } from './watchlist.store';

interface FinalActionState {
  watchlist: FinalActionResult[];
  portfolio: FinalActionResult[];
  computedAtUtc: string | null;
}

const SYMBOL_RELOAD_DEBOUNCE_MS = 1500;

/** Adapts a backend result to the decision shape the grids and widgets already render. */
export function toPageDecision(r: FinalActionResult): PageDecision {
  return {
    trendSetup: r.trendSetup as TrendSetup,
    trendSetupReason: r.trendSetupReason,
    momentumShift: r.momentumShift as MomentumShift,
    momentumShiftReason: r.momentumShiftReason,
    baseAction: r.baseAction as BaseAction,
    baseActionReason: r.baseAction,
    finalAction: r.finalAction,
    hoverDescription: r.hoverDescription,
    finalActionClass: r.finalActionClass,
    trendSetupClass: r.trendSetupClass,
    momentumShiftClass: r.momentumShiftClass,
    gapStatus: r.gapStatus as GapStatus,
    gapScore: r.gapScore,
    gapPct: r.gapPct,
    finalActionReason: r.reason ?? undefined,
  };
}

/**
 * The canonical Final Action for every watchlist symbol and open holding, computed by the backend engine
 * (the same one Action Center, Priority Candidates, Signals Audit and EOD automation use). Components only
 * read decisions from here; no recommendation rules run in the browser.
 */
export const FinalActionStore = signalStore(
  { providedIn: 'root' },
  withState<FinalActionState>({ watchlist: [], portfolio: [], computedAtUtc: null }),
  withRequestStatus(),
  withComputed(({ watchlist, portfolio, loading, error, computedAtUtc }) => ({
    /**
     * What a Final Action cell without a result means: 'loading' (first evaluation pending), 'error' (the request failed) or
     * 'ready' (loaded, so a missing result means that symbol was Not Evaluated). None of these is ever a trade decision.
     */
    uiState: computed<'loading' | 'error' | 'ready'>(() =>
      error() ? 'error' : loading() || computedAtUtc() === null ? 'loading' : 'ready',
    ),
    watchlistBySymbol: computed(() => new Map(watchlist().map((r) => [r.symbol.toUpperCase(), r]))),
    portfolioByItemId: computed(
      () => new Map(portfolio().filter((r) => r.itemId !== null).map((r) => [r.itemId as number, r])),
    ),
    portfolioBySymbol: computed(() => {
      const map = new Map<string, FinalActionResult>();
      for (const r of portfolio()) if (!map.has(r.symbol.toUpperCase())) map.set(r.symbol.toUpperCase(), r);
      return map;
    }),
  })),
  withMethods((store, api = inject(PortfolioApiService)) => {
    const load = rxMethod<boolean>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap((fresh) =>
          api.getFinalActions(fresh).pipe(
            tapResponse({
              next: (s) =>
                patchState(
                  store,
                  { watchlist: s.watchlist, portfolio: s.portfolio, computedAtUtc: s.computedAtUtc },
                  setLoaded(),
                ),
              error: () => patchState(store, setError('Failed to load Final Actions')),
            }),
          ),
        ),
      ),
    );

    return {
      load,
      /** Re-evaluates with a live scan. */
      refresh(): void {
        load(true);
      },
      decisionForWatchlist(symbol: string): PageDecision | null {
        const r = store.watchlistBySymbol().get(symbol.toUpperCase());
        return r ? toPageDecision(r) : null;
      },
      /** Holding-specific decision (account, gain and position-size aware); falls back to the symbol's first holding. */
      decisionForPortfolio(symbol: string, itemId?: number | null): PageDecision | null {
        const r =
          (itemId != null ? store.portfolioByItemId().get(itemId) : undefined) ??
          store.portfolioBySymbol().get(symbol.toUpperCase());
        return r ? toPageDecision(r) : null;
      },
      /** The full canonical result for a holding (actionability, reason codes), for exports and tooltips. */
      resultForPortfolio(symbol: string, itemId?: number | null): FinalActionResult | null {
        return (
          (itemId != null ? store.portfolioByItemId().get(itemId) : undefined) ??
          store.portfolioBySymbol().get(symbol.toUpperCase()) ??
          null
        );
      },
      /** The watchlist evaluation of a symbol (never the holding's). */
      resultForWatchlist(symbol: string): FinalActionResult | null {
        return store.watchlistBySymbol().get(symbol.toUpperCase()) ?? null;
      },
      /** The result for a symbol on either list, preferring the holding. */
      resultForSymbol(symbol: string): FinalActionResult | null {
        const key = symbol.toUpperCase();
        return store.portfolioBySymbol().get(key) ?? store.watchlistBySymbol().get(key) ?? null;
      },
    };
  }),
  withHooks({
    onInit(store) {
      const auth = inject(AuthStore);
      const appRefresh = inject(AppRefreshStore);
      const watchlist = inject(WatchlistStore);
      const portfolio = inject(PortfolioStore);

      onceAuthenticated(() => store.load(false));

      // A completed app refresh rescans the market, so re-evaluate with fresh data.
      let firstRefresh = true;
      effect(() => {
        appRefresh.lastRefreshedAt();
        if (firstRefresh) {
          firstRefresh = false;
          return;
        }
        untracked(() => {
          if (auth.isAuthenticated()) store.refresh();
        });
      });

      // Adding or removing a symbol changes what must be evaluated; debounce bursts of changes.
      let timer: ReturnType<typeof setTimeout> | undefined;
      let lastKey: string | null = null;
      effect(() => {
        const key = [
          ...watchlist.items().map((w) => `W:${w.item.symbol.toUpperCase()}`),
          ...portfolio
            .summaries()
            .filter((s) => s.item.transactionType !== 'CLOSE' && !s.item.isManual)
            .map((s) => `P:${s.item.id}:${s.item.symbol.toUpperCase()}`),
        ]
          .sort()
          .join(',');
        untracked(() => {
          const previous = lastKey;
          lastKey = key;
          if (previous === null || previous === key || !auth.isAuthenticated()) return;
          clearTimeout(timer);
          timer = setTimeout(() => store.refresh(), SYMBOL_RELOAD_DEBOUNCE_MS);
        });
      });
    },
  }),
);
