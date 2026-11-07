import { DestroyRef, computed, inject } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { EMPTY, Subject, filter, interval, switchMap, takeUntil } from 'rxjs';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { PortfolioRsiAnalysisService } from '../services/portfolio-rsi-analysis.service';
import { ConfigStore } from './config.store';
import { DashboardStore } from './dashboard.store';
import { EodSignalsStore } from './eod-signals.store';
import { OpenDecisionsStore } from './open-decisions.store';
import { PortfolioStore } from './portfolio.store';
import { WatchlistRsiStore } from './watchlist-rsi.store';
import { WatchlistStore } from './watchlist.store';

export interface RefreshStep {
  key: string;
  label: string;
  status: 'idle' | 'loading' | 'done' | 'error';
  totalCount?: number; // e.g., 15 tickers
  completedCount?: number; // e.g., 3 done
  currentItem?: string; // e.g., "AAPL"
}

export type RefreshReason = 'auto-timer' | 'user-click' | 'feature-refresh';

type OfflineReason = 'network' | 'server' | 'timeout';

const LAST_REFRESHED_KEY = 'pm_last_refreshed';
const REFRESH_WATCH_INTERVAL_MS = 100;
const REFRESH_WATCH_TIMEOUT_MS = 30_000;

interface AppRefreshState {
  isRefreshing: boolean;
  refreshSteps: RefreshStep[];
  lastRefreshedAt: Date | null;
  error: string | null;
  isOffline: boolean;
  offlineReason: OfflineReason | null;
  retryCount: number;
  /** Seconds left on the auto-retry countdown, or null when no retry is pending. */
  nextRetryIn: number | null;
  refreshReason: RefreshReason;
}

function buildSteps(): RefreshStep[] {
  return [
    {
      key: 'fetch-portfolio',
      label: 'Portfolio quotes',
      status: 'idle',
      totalCount: 0,
      completedCount: 0,
    },
    {
      key: 'fetch-watchlist',
      label: 'Watchlist quotes',
      status: 'idle',
      totalCount: 0,
      completedCount: 0,
    },
    { key: 'dashboard', label: 'Rebuilding dashboard', status: 'idle' },
    { key: 'actions', label: 'Reloading action scores & signals', status: 'idle' },
    { key: 'eod-signals', label: 'Polling for new EOD signals', status: 'idle' },
  ];
}

function loadLastRefreshed(): Date | null {
  try {
    const raw = localStorage.getItem(LAST_REFRESHED_KEY);
    return raw ? new Date(raw) : null;
  } catch {
    return null;
  }
}

function getErrorMessage(reason: OfflineReason): string {
  switch (reason) {
    case 'network':
      return 'No internet connection';
    case 'server':
      return 'Server unavailable';
    case 'timeout':
      return 'Request timed out';
    default:
      return 'Refresh failed';
  }
}

/**
 * Drives the one app-wide "refresh everything" pipeline and the progress UI around it.
 *
 * A single `/refresh-all` call fetches every quote once; the result is pushed straight into the
 * portfolio and watchlist stores (no second round-trip) before the dashboard is rebuilt, so the
 * dashboard total reflects the quotes just fetched. The auto-timer is driven by
 * `appRefreshSeconds` and skipped while the tab is hidden. Network and timeout failures auto-retry
 * up to three times; server failures do not.
 */
export const AppRefreshStore = signalStore(
  { providedIn: 'root' },
  withState<AppRefreshState>(() => ({
    isRefreshing: false,
    refreshSteps: buildSteps(),
    lastRefreshedAt: loadLastRefreshed(),
    error: null,
    isOffline: false,
    offlineReason: null,
    retryCount: 0,
    nextRetryIn: null,
    refreshReason: 'auto-timer',
  })),
  withComputed(({ lastRefreshedAt }) => ({
    secondsSinceRefresh: computed(() => {
      const last = lastRefreshedAt();
      if (!last) return null;
      return Math.floor((Date.now() - last.getTime()) / 1000);
    }),
  })),
  withMethods(
    (
      store,
      api = inject(PortfolioApiService),
      portfolioState = inject(PortfolioStore),
      watchlistState = inject(WatchlistStore),
      dashboardState = inject(DashboardStore),
      openDecisions = inject(OpenDecisionsStore),
      eodSignalsState = inject(EodSignalsStore),
      watchlistRsi = inject(WatchlistRsiStore),
    ) => {
      const cancel$ = new Subject<void>();
      let retryTimeoutId: number | null = null;
      let currentRun = 0;

      const setSteps = (project: (steps: RefreshStep[]) => RefreshStep[]): void =>
        patchState(store, ({ refreshSteps }) => ({ refreshSteps: project(refreshSteps) }));

      const setStepStatus = (key: string, status: RefreshStep['status']): void =>
        setSteps((steps) => steps.map((s) => (s.key === key ? { ...s, status } : s)));

      const setStepProgress = (key: string, current: number, total: number): void =>
        setSteps((steps) =>
          steps.map((s) =>
            s.key === key ? { ...s, completedCount: current, totalCount: total } : s,
          ),
        );

      const refreshAll = (reason: RefreshReason = 'user-click'): void => {
        if (store.isRefreshing()) return;

        patchState(store, {
          refreshReason: reason,
          isRefreshing: true,
          error: null,
          isOffline: false,
          offlineReason: null,
          retryCount: 0,
          nextRetryIn: null,
          refreshSteps: buildSteps(),
        });

        api
          .refreshAll()
          .pipe(takeUntil(cancel$))
          .subscribe({
            next: (result) => {
              // Set portfolio and watchlist counts from the response
              setStepProgress(
                'fetch-portfolio',
                result.portfolioSymbols.length,
                result.portfolioSymbols.length,
              );
              setStepProgress(
                'fetch-watchlist',
                result.watchlistSymbols.length,
                result.watchlistSymbols.length,
              );
              setStepStatus('fetch-portfolio', 'done');
              setStepStatus('fetch-watchlist', 'done');
              setStepStatus('dashboard', 'loading');

              // Push fresh data directly — no loading state, no extra HTTP round-trips
              portfolioState.setFromRefresh(result.portfolioSummaries);
              watchlistState.setFromRefresh(result.watchlistSummaries);
              // /refresh-all already rebuilt the dashboard snapshot server-side from these quotes, so just
              // read it. A second POST /dashboard/refresh repeated the rebuild plus the slow earnings fetch.
              dashboardState.load();

              setStepStatus('actions', 'loading');

              // Force-reload action scores and performance summary after snapshot rebuild
              dashboardState.loadPortfolioActions();
              openDecisions.load();
              dashboardState.loadEodSummary();
              dashboardState.loadMarketLeadership();
              dashboardState.loadActionScores();
              dashboardState.loadPerformanceSummary();

              setStepStatus('eod-signals', 'loading');

              // Poll for new EOD signals
              eodSignalsState.loadSignals();

              // Step statuses follow the real in-flight requests. The full-watchlist RSI scan starts
              // only after the numbers are on screen so it does not compete with them.
              const runId = ++currentRun;
              const startedAt = Date.now();
              const watch = setInterval(() => {
                if (runId !== currentRun || !store.isRefreshing()) {
                  clearInterval(watch);
                  return;
                }
                const dashboardDone = !dashboardState.loading();
                const actionsDone =
                  !dashboardState.actionsLoading() &&
                  !openDecisions.loading() &&
                  !dashboardState.eodSummaryLoading() &&
                  !dashboardState.marketLeadershipLoading() &&
                  !dashboardState.actionScoresLoading() &&
                  !dashboardState.performanceSummaryLoading();
                const eodDone = !eodSignalsState.loading();
                if (dashboardDone) setStepStatus('dashboard', 'done');
                if (actionsDone) setStepStatus('actions', 'done');
                if (eodDone) setStepStatus('eod-signals', 'done');

                const timedOut = Date.now() - startedAt > REFRESH_WATCH_TIMEOUT_MS;
                if (!(dashboardDone && actionsDone && eodDone) && !timedOut) return;

                clearInterval(watch);
                setSteps((steps) =>
                  steps.map((s) => (s.status === 'loading' ? { ...s, status: 'done' } : s)),
                );
                const now = new Date();
                patchState(store, { isRefreshing: false, lastRefreshedAt: now });
                localStorage.setItem(LAST_REFRESHED_KEY, now.toISOString());
                watchlistRsi.triggerRefresh(result.watchlistSymbols);
              }, REFRESH_WATCH_INTERVAL_MS);
            },
            error: (err) => {
              handleRefreshError(err);
            },
          });
      };

      const scheduleAutoRetry = (delaySeconds = 5, maxRetries = 3): void => {
        const currentRetry = store.retryCount();
        if (currentRetry >= maxRetries) {
          return; // Max retries exceeded
        }

        let countdownSeconds = delaySeconds;
        patchState(store, { nextRetryIn: countdownSeconds });

        const countdownInterval = setInterval(() => {
          countdownSeconds--;
          if (countdownSeconds > 0) {
            patchState(store, { nextRetryIn: countdownSeconds });
          } else {
            clearInterval(countdownInterval);
            patchState(store, { nextRetryIn: null, retryCount: currentRetry + 1 });
            refreshAll(store.refreshReason());
          }
        }, 1000);

        retryTimeoutId = countdownInterval as unknown as number;
      };

      function handleRefreshError(err: { status?: number; name?: string } | null): void {
        // Detect error type
        let reason: OfflineReason | null = null;

        if (err?.status === 0) {
          reason = 'network';
        } else if ((err?.status ?? 0) >= 500) {
          reason = 'server';
        } else if (err?.name === 'TimeoutError') {
          reason = 'timeout';
        }

        if (reason) {
          patchState(store, {
            isOffline: true,
            offlineReason: reason,
            error: getErrorMessage(reason),
          });

          // Auto-retry for network and timeout errors
          if (reason !== 'server') {
            scheduleAutoRetry();
          }
        } else {
          patchState(store, { error: 'Refresh failed' });
        }

        setSteps((steps) =>
          steps.map((s) =>
            s.status === 'loading' || s.status === 'idle' ? { ...s, status: 'error' } : s,
          ),
        );
        patchState(store, { isRefreshing: false });
      }

      return {
        refreshAll,
        setStepProgress,

        /**
         * Auto-timer tick. Records the reason before delegating, mirroring the original service:
         * `refreshAll()` then overwrites it with its own default, which is why retries report
         * 'user-click'.
         */
        autoTick(): void {
          patchState(store, { refreshReason: 'auto-timer' });
          refreshAll();
        },

        retry(): void {
          refreshAll('user-click');
        },

        cancelRefresh(): void {
          if (!store.isRefreshing()) return;

          if (retryTimeoutId !== null) {
            clearTimeout(retryTimeoutId);
            retryTimeoutId = null;
          }

          cancel$.next();
          patchState(store, {
            isRefreshing: false,
            isOffline: false,
            offlineReason: null,
            nextRetryIn: null,
          });
          setSteps((steps) =>
            steps.map((s) =>
              s.status === 'loading' || s.status === 'idle' ? { ...s, status: 'idle' } : s,
            ),
          );
        },

        setCurrentItem(key: string, symbol: string): void {
          setSteps((steps) => steps.map((s) => (s.key === key ? { ...s, currentItem: symbol } : s)));
        },

        /** Marks the connection healthy again after the browser reports it is back online. */
        clearOffline(): void {
          if (store.isOffline()) {
            patchState(store, { isOffline: false, offlineReason: null, nextRetryIn: null });
          }
        },

        markOffline(): void {
          patchState(store, { isOffline: true });
        },
      };
    },
  ),
  withHooks({
    onInit(store, config = inject(ConfigStore), destroyRef = inject(DestroyRef)) {
      // Referenced only to force this root singleton to instantiate at app boot (this store is
      // itself created early via the layout shell) so RSI analysis + Action Center sync run without
      // requiring the Portfolio page to ever be opened.
      inject(PortfolioRsiAnalysisService);

      // Single auto-refresh timer driven by appRefreshSeconds
      toObservable(config.config)
        .pipe(
          takeUntilDestroyed(destroyRef),
          switchMap((cfg) =>
            cfg.appRefreshSeconds > 0 ? interval(cfg.appRefreshSeconds * 1000) : EMPTY,
          ),
          filter(() => document.visibilityState === 'visible'),
        )
        .subscribe(() => store.autoTick());

      // Listen to online/offline state changes
      const handleOnline = () => store.clearOffline();
      const handleOffline = () => store.markOffline();
      window.addEventListener('online', handleOnline);
      window.addEventListener('offline', handleOffline);
      destroyRef.onDestroy(() => {
        window.removeEventListener('online', handleOnline);
        window.removeEventListener('offline', handleOffline);
      });
    },
  }),
);
