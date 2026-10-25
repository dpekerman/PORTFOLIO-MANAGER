import { computed, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { tapResponse } from '@ngrx/operators';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { EMPTY, Observable, firstValueFrom, pipe, switchMap, tap } from 'rxjs';
import { PeriodChange, WatchlistSummary } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { onceAuthenticated } from './features/auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

interface WatchlistState {
  items: WatchlistSummary[];
  /** True when `items` came from the DB snapshot rather than a live Yahoo Finance call. */
  fromSnapshot: boolean;
  /** 5D / 1M change % keyed by upper-case symbol (Yahoo daily closes). */
  periodChanges: Record<string, PeriodChange>;
}

/**
 * The user's watchlist. Loads the last DB snapshot first (instant, no Yahoo call) and falls back to
 * a live refresh when no snapshot exists. Mutations update local state only after the server
 * confirms, so the list never shows a change that failed.
 */
export const WatchlistStore = signalStore(
  { providedIn: 'root' },
  withState<WatchlistState>({ items: [], fromSnapshot: false, periodChanges: {} }),
  withRequestStatus(),
  withComputed(({ items }) => ({
    count: computed(() => items().length),
  })),
  withMethods((store, api = inject(PortfolioApiService), snackBar = inject(MatSnackBar)) => {
    const notify = (message: string, duration = 3000) =>
      snackBar.open(message, 'Close', { duration });

    /** Merges `changes` into one item's record once the server call succeeds. */
    const updateItem = (
      request: Observable<unknown>,
      id: number,
      changes: Partial<WatchlistSummary['item']>,
      failureMessage: string,
    ) =>
      request.subscribe({
        next: () =>
          patchState(store, ({ items }) => ({
            items: items.map((s) => (s.item.id === id ? { ...s, item: { ...s.item, ...changes } } : s)),
          })),
        error: () => notify(failureMessage, 4000),
      });

    /** Live refresh from Yahoo Finance. */
    const refresh = rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading(), { fromSnapshot: false })),
        switchMap(() =>
          api.getWatchlist().pipe(
            tapResponse({
              next: (items) => patchState(store, { items }, setLoaded()),
              error: () => patchState(store, setError('Failed to load watchlist')),
            }),
          ),
        ),
      ),
    );

    /** Loads the last DB snapshot; falls back to a live refresh when none exists or it fails. */
    const loadSnapshot = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { loading: true })),
        switchMap(() =>
          api.getWatchlistSnapshot().pipe(
            tapResponse({
              next: (data) =>
                data
                  ? patchState(store, { items: data, fromSnapshot: true }, setLoaded())
                  : refresh(),
              error: () => refresh(),
            }),
          ),
        ),
      ),
    );

    return {
      refresh,
      loadSnapshot,

      /** Loads 5D / 1M change % for the given symbols. A failure leaves existing values untouched. */
      loadPeriodChanges: rxMethod<string[]>(
        pipe(
          switchMap((symbols) =>
            symbols.length === 0
              ? EMPTY
              : api.getPeriodChanges(symbols).pipe(
                  tapResponse({
                    next: (changes) =>
                      patchState(store, ({ periodChanges }) => ({
                        periodChanges: {
                          ...periodChanges,
                          ...Object.fromEntries(changes.map((c) => [c.symbol.toUpperCase(), c])),
                        },
                      })),
                    error: () => undefined,
                  }),
                ),
          ),
        ),
      ),

      /** Silently applies prices from a completed batch refresh: no loading state, no extra HTTP call. */
      setFromRefresh(items: WatchlistSummary[]): void {
        patchState(store, { items, fromSnapshot: false });
      },

      async addItem(symbol: string, role = 'Strategic'): Promise<void> {
        try {
          const response = await firstValueFrom(
            api.addWatchlistItem(symbol.toUpperCase(), '', role),
          );
          // Quote and technical data populate on the next refresh cycle.
          const newItem: WatchlistSummary = {
            item: {
              id: response.id,
              symbol: response.symbol,
              notes: response.notes,
              addedAt: response.addedAt,
              role: response.role,
              isFavorite: false,
              watchlistTier: 'Strategic',
              earningsDate: null,
            },
            quote: null,
            priceStructure: null,
            technicalFacts: null,
          };
          patchState(store, ({ items }) => ({ items: [...items, newItem] }));
          notify(`${symbol.toUpperCase()} added to watchlist`);
        } catch (err) {
          const title = (err as { error?: { title?: string } } | null)?.error?.title;
          notify(title ?? 'Failed to add symbol', 4000);
          throw err;
        }
      },

      deleteItem(id: number, symbol: string): void {
        api.deleteWatchlistItem(id).subscribe({
          next: () => {
            patchState(store, ({ items }) => ({ items: items.filter((s) => s.item.id !== id) }));
            notify(`${symbol} removed from watchlist`);
          },
          error: () => notify('Failed to remove', 4000),
        });
      },

      updateRole(id: number, role: string): void {
        updateItem(api.updateWatchlistRole(id, role), id, { role }, 'Failed to update role');
      },

      updateTier(id: number, watchlistTier: string): void {
        updateItem(
          api.updateWatchlistTier(id, watchlistTier),
          id,
          { watchlistTier },
          'Failed to update tier',
        );
      },

      updateEarningsDate(id: number, earningsDate: string | null): void {
        updateItem(
          api.updateWatchlistEarningsDate(id, earningsDate),
          id,
          { earningsDate },
          'Failed to update earnings date',
        );
      },

      updateFavorite(id: number, isFavorite: boolean): void {
        updateItem(
          api.updateWatchlistFavorite(id, isFavorite),
          id,
          { isFavorite },
          'Failed to update favourite',
        );
      },

      updateNotes(id: number, notes: string): void {
        updateItem(api.updateWatchlistNotes(id, notes), id, { notes }, 'Failed to update notes');
      },
    };
  }),
  withHooks({
    onInit(store) {
      // Wait for auth before loading: loading earlier races the token refresh and 401s.
      onceAuthenticated(() => store.loadSnapshot());
    },
  }),
);
