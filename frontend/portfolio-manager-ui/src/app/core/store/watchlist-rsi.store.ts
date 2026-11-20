import { computed, inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import {
  Subject,
  catchError,
  distinctUntilChanged,
  filter,
  forkJoin,
  map,
  of,
  pipe,
  switchMap,
  takeUntil,
  tap,
} from 'rxjs';
import { RsiScanResult } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { WatchlistStore } from './watchlist.store';

interface WatchlistRsiState {
  rsiMap: Map<string, RsiScanResult>;
  rsiLoading: boolean;
}

/**
 * RSI results for the watchlist. Root-scoped so results survive navigation — no re-scan when
 * returning to the watchlist page.
 *
 * Reading `rsiMap()`/`rsiLoading()` is always safe (passive cache read, no side effect). Only
 * `triggerRefresh()` and `enableAutoRefreshOnWatchlistChange()` cause a live Yahoo Finance batch
 * scan (`POST /api/scanner/analyze`) — callers that only need the cached data (e.g. Dashboard)
 * must never call either.
 */
export const WatchlistRsiStore = signalStore(
  { providedIn: 'root' },
  withState<WatchlistRsiState>({ rsiMap: new Map(), rsiLoading: false }),
  withMethods(
    (store, watchlist = inject(WatchlistStore), api = inject(PortfolioApiService)) => {
      const cancel$ = new Subject<void>();
      let autoRefreshEnabled = false;

      // Pipeline: batches symbols (max 50/request), cancels in-flight on new trigger.
      // Merely wiring this up has no side effect — nothing runs until it is invoked.
      const scan = rxMethod<string[]>(
        pipe(
          tap((symbols) => {
            console.log(
              `[Watchlist RSI] Scan started — ${symbols.length} symbols @ ${new Date().toISOString()}`,
            );
            patchState(store, { rsiLoading: true });
          }),
          switchMap((symbols) => {
            const batchSize = 50;
            const batches: string[][] = [];
            for (let i = 0; i < symbols.length; i += batchSize)
              batches.push(symbols.slice(i, i + batchSize));

            return forkJoin(
              batches.map((batch) =>
                api.analyzeSymbols(batch, 30, 75, 'Enhanced').pipe(
                  catchError((err) => {
                    console.warn('[Watchlist RSI] Batch fetch failed', err);
                    return of([] as RsiScanResult[]);
                  }),
                ),
              ),
            ).pipe(
              map((batchResults) => batchResults.flat()),
              takeUntil(cancel$),
              tapResponse({
                next: (results) => {
                  const rsiMap = new Map<string, RsiScanResult>();
                  for (const r of results) rsiMap.set(r.symbol.toUpperCase(), r);
                  patchState(store, { rsiMap, rsiLoading: false });
                  console.log(
                    `[Watchlist RSI] Scan complete — ${results.length} results @ ${new Date().toISOString()}`,
                  );
                },
                error: () => {
                  patchState(store, { rsiLoading: false });
                  console.error(`[Watchlist RSI] Scan failed @ ${new Date().toISOString()}`);
                },
              }),
            );
          }),
        ),
      );

      /** Sorted symbol key — changes only when symbols are added or removed. */
      const symbolKey = computed(() =>
        [...watchlist.items().map((w) => w.item.symbol)].sort().join(','),
      );

      // Created here, in the store's root injection context, so it outlives whichever component
      // enables it, matching the "survives navigation" contract.
      const autoRefresh = rxMethod<string>(
        pipe(
          distinctUntilChanged(),
          filter((key) => key.length > 0),
          map((key) => key.split(',')),
          tap((symbols) => scan(symbols)),
        ),
      );

      return {
        /** Force a fresh RSI scan (e.g., when the user clicks manual refresh). */
        triggerRefresh(symbols: string[]): void {
          if (symbols.length > 0) scan(symbols);
        },

        /** Cancel an in-flight RSI scan — stops listening for the response and re-enables the UI. */
        cancelRefresh(): void {
          cancel$.next();
          patchState(store, { rsiLoading: false });
        },

        /**
         * Opt-in: automatically re-analyzes the watchlist whenever its symbol set changes
         * (add/remove). This triggers a live batch scan — call only from a consumer that wants
         * that side effect (the Watchlist page). Idempotent; safe to call more than once.
         */
        enableAutoRefreshOnWatchlistChange(): void {
          if (autoRefreshEnabled) return;
          autoRefreshEnabled = true;
          autoRefresh(symbolKey);
        },
      };
    },
  ),
);
