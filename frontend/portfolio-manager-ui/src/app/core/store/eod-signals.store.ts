import { computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { filter, interval } from 'rxjs';
import { DailySignal, EodSignalsMeta } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { onceAuthenticated } from './features/auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

interface EodSignalsState {
  signals: DailySignal[];
  meta: EodSignalsMeta | null;
  eodWindowActive: boolean;
  /** Set after every load attempt, successful or not. */
  lastPollAt: Date | null;
}

/**
 * Latest EOD signals plus the server's EOD-window status, kept fresh by background polling
 * (skipped while the tab is hidden) and by the unified refresh in AppRefreshStore.
 */
export const EodSignalsStore = signalStore(
  { providedIn: 'root' },
  withState<EodSignalsState>({ signals: [], meta: null, eodWindowActive: false, lastPollAt: null }),
  withRequestStatus(),
  withComputed(({ signals }) => ({
    /** Signals belonging to the most recent trading date in the loaded page. */
    newSignalCount: computed(() => {
      const all = signals();
      const latestTradingDate = all.reduce<string | null>((latest, signal) => {
        const tradingDate = signal.tradingDate ?? signal.signalDate;
        return latest === null || tradingDate > latest ? tradingDate : latest;
      }, null);
      return latestTradingDate === null
        ? 0
        : all.filter((signal) => (signal.tradingDate ?? signal.signalDate) === latestTradingDate)
            .length;
    }),
  })),
  withMethods((store, api = inject(PortfolioApiService)) => ({
    /** Fetch new EOD signals from API (called during unified refresh). */
    loadSignals(): void {
      patchState(store, setLoading());

      api.getEodSignals({ page: 1, pageSize: 10 }).subscribe({
        next: (response) =>
          patchState(store, { signals: response.items, lastPollAt: new Date() }, setLoaded()),
        error: (err) => {
          console.error('Failed to load EOD signals:', err);
          patchState(store, setError('Failed to load EOD signals'), { lastPollAt: new Date() });
        },
      });

      api.getEodSignalsMeta().subscribe({
        next: (meta) => patchState(store, { meta }),
        error: () => {}, // Non-critical
      });
    },

    /** Check if the EOD window is currently active on the server. */
    _checkEodWindowStatus(): void {
      api.getEodWindowStatus().subscribe({
        next: (status) => patchState(store, { eodWindowActive: status.isActive }),
        error: (err) => {
          console.error('Failed to check EOD window status:', err);
        },
      });
    },
  })),
  withHooks({
    onInit(store) {
      const visible = () => document.visibilityState === 'visible';

      // Wait for auth before loading: loading earlier races the token refresh and 401s.
      onceAuthenticated(() => {
        store.loadSignals();
        store._checkEodWindowStatus();
      });

      interval(5 * 60_000)
        .pipe(filter(visible), takeUntilDestroyed())
        .subscribe(() => store.loadSignals());

      interval(30_000)
        .pipe(filter(visible), takeUntilDestroyed())
        .subscribe(() => store._checkEodWindowStatus());
    },
  }),
);
