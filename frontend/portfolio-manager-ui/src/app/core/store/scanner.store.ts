import { computed, inject } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { EMPTY, Subscription, filter, interval, switchMap } from 'rxjs';
import {
  LogicMode,
  MarketIndexDto,
  RsiScanResult,
  ScannerResponse,
  YesterdayEodResponse,
} from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { onceAuthenticated } from './features/auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';
import { ConfigStore } from './config.store';

interface ScannerState {
  response: ScannerResponse | null;
  logicMode: LogicMode;
  /** True while showing the cached DB snapshot (before the first live scan). */
  fromSnapshot: boolean;
  /** True when the EOD window is currently active on the server. */
  eodWindowActive: boolean;
  /** Summary of the last EOD window run result (e.g. "3 EOD Confirm signals"). */
  lastEodRunSummary: string | null;
  /** Yesterday's EOD CONFIRM signals (fetched on init and refreshed periodically). */
  yesterdayEod: YesterdayEodResponse | null;
  /** Live market index prices (Dow, Nasdaq 100, S&P 500). Refreshed with each scan. */
  marketIndices: MarketIndexDto[];
  adhocSymbols: string[];
  adhocResults: RsiScanResult[];
  adhocAnalyzed: boolean;
  /** True once the initial DB restore of the ad-hoc session has finished. */
  adhocSessionRestored: boolean;
}

const initialState: ScannerState = {
  response: null,
  logicMode: 'Enhanced',
  fromSnapshot: false,
  eodWindowActive: false,
  lastEodRunSummary: null,
  yesterdayEod: null,
  marketIndices: [],
  adhocSymbols: [],
  adhocResults: [],
  adhocAnalyzed: false,
  adhocSessionRestored: false,
};

/**
 * The RSI scanner's shared state: the latest scan, the EOD window, market indices and the ad-hoc
 * analyzer session. One store so every page reads the same scan and the ad-hoc session survives
 * route navigation. Shows the last DB snapshot first (no Yahoo call) and only scans live when no
 * snapshot exists. Background polling skips ticks while the tab is hidden.
 */
export const ScannerStore = signalStore(
  { providedIn: 'root' },
  withState<ScannerState>(initialState),
  withRequestStatus(),
  withComputed(({ response }) => {
    const oversold = computed(() => response()?.oversoldChain ?? []);
    const overbought = computed(() => response()?.overboughtChain ?? []);
    const eodConfirmOversold = computed(() => oversold().filter((r) => r.status === 'EodConfirm'));
    const eodConfirmOverbought = computed(() =>
      overbought().filter((r) => r.status === 'EodConfirm'),
    );
    return {
      /** Minutes since last scan. Null when no data loaded yet. */
      snapshotAgeMinutes: computed(() => {
        const scannedAt = response()?.scannedAt;
        if (!scannedAt) return null;
        return Math.round((Date.now() - new Date(scannedAt).getTime()) / 60_000);
      }),
      oversold,
      overbought,
      isDemo: computed(() => response()?.isDemo ?? true),
      market: computed(() => response()?.market ?? ''),
      scannedAt: computed(() => response()?.scannedAt ?? null),
      confirmedOversold: computed(() => oversold().filter((r) => r.status === 'Confirmed')),
      confirmedOverbought: computed(() => overbought().filter((r) => r.status === 'Confirmed')),
      eodConfirmOversold,
      eodConfirmOverbought,
      totalEodConfirm: computed(() => eodConfirmOversold().length + eodConfirmOverbought().length),
    };
  }),
  withMethods(
    (store, api = inject(PortfolioApiService), config = inject(ConfigStore)) => {
      let refreshSub: Subscription | null = null;

      const updateEodSummary = (): void => {
        const count = store.totalEodConfirm();
        if (count > 0) {
          const os = store.eodConfirmOversold().length;
          const ob = store.eodConfirmOverbought().length;
          const parts: string[] = [];
          if (os > 0) parts.push(`${os} oversold`);
          if (ob > 0) parts.push(`${ob} overbought`);
          patchState(store, {
            lastEodRunSummary: `${count} EOD Confirm signal(s): ${parts.join(', ')}`,
          });
        }
      };

      const loadMarketIndices = (): void => {
        api.getMarketIndices().subscribe({
          next: (data) => patchState(store, { marketIndices: data.indices }),
          error: () => {}, // Non-critical
        });
      };

      const refresh = (force = false): void => {
        patchState(store, setLoading());
        const cfg = config.config();
        refreshSub = api
          .getRsiScan(force, cfg.rsiOversoldThreshold, cfg.rsiOverboughtThreshold, store.logicMode())
          .subscribe({
            next: (response) => {
              patchState(store, { response, fromSnapshot: false }, setLoaded());
              updateEodSummary();
              loadMarketIndices(); // refresh indices on every live scan
            },
            error: () => patchState(store, setError('Scanner unavailable')),
          });
      };

      return {
        refresh,
        _loadMarketIndices: loadMarketIndices,

        /** Applies a background live scan (auto-refresh timer) without touching loading state. */
        _applyBackgroundScan(response: ScannerResponse): void {
          patchState(store, { response, fromSnapshot: false });
          updateEodSummary();
        },

        _checkEodWindowStatus(): void {
          api.getEodWindowStatus().subscribe({
            next: (status) => {
              const wasActive = store.eodWindowActive();
              patchState(store, { eodWindowActive: status.isActive });
              // When window closes, update the EOD summary
              if (wasActive && !status.isActive) {
                updateEodSummary();
              }
            },
            error: () => {}, // Silently fail: non-critical
          });
        },

        _loadYesterdayEod(): void {
          api.getYesterdayEod().subscribe({
            next: (yesterdayEod) => patchState(store, { yesterdayEod }),
            error: () => {}, // Non-critical
          });
        },

        /**
         * Load last scan from DB snapshot (instant, no Yahoo Finance call).
         * If no snapshot exists yet, does a single initial live scan to populate the DB.
         */
        _loadSnapshot(): void {
          patchState(store, { loading: true });
          api.getRsiSnapshot().subscribe({
            next: (response) => {
              if (response) {
                patchState(store, { response, fromSnapshot: true });
                updateEodSummary();
                patchState(store, setLoaded());
              } else {
                refresh(false);
              }
            },
            // Snapshot API unavailable: fall back to a live scan.
            error: () => refresh(false),
          });
        },

        /** Restores the ad-hoc analyzer session from the DB (first load only). */
        _restoreAdhocSession(): void {
          api.loadAdhocSession().subscribe({
            next: (session) => {
              if (session.symbols?.length && !store.adhocSessionRestored()) {
                patchState(store, { adhocSymbols: session.symbols });
                if (session.results?.length) {
                  patchState(store, { adhocResults: session.results, adhocAnalyzed: true });
                }
              }
              patchState(store, { adhocSessionRestored: true });
            },
            error: () => patchState(store, { adhocSessionRestored: true }),
          });
        },

        toggleLogicMode(): void {
          const next: LogicMode = store.logicMode() === 'Legacy' ? 'Enhanced' : 'Legacy';
          patchState(store, { logicMode: next });
          refresh(true);
        },

        /** Cancel an in-flight scan: stops listening for the response and re-enables the UI. */
        cancelRefresh(): void {
          refreshSub?.unsubscribe();
          refreshSub = null;
          patchState(store, setLoaded());
        },

        setAdhocSymbols(adhocSymbols: string[]): void {
          patchState(store, { adhocSymbols });
        },
        setAdhocResults(adhocResults: RsiScanResult[]): void {
          patchState(store, { adhocResults });
        },
        setAdhocAnalyzed(adhocAnalyzed: boolean): void {
          patchState(store, { adhocAnalyzed });
        },
      };
    },
  ),
  withHooks({
    onInit(store) {
      const config = inject(ConfigStore);
      const api = inject(PortfolioApiService);
      const visible = () => document.visibilityState === 'visible';

      // Wait for auth before the initial loads: loading earlier races the token refresh and 401s.
      onceAuthenticated(() => {
        // Persisted snapshot immediately: no Yahoo Finance call.
        store._loadSnapshot();
        store._restoreAdhocSession();
        store._checkEodWindowStatus();
        store._loadYesterdayEod();
        store._loadMarketIndices();
      });

      // Restart auto-refresh whenever the configured interval changes.
      // interval = 0 means disabled: emit EMPTY so no timer fires.
      // Skipped while the tab is hidden/backgrounded to avoid unnecessary API calls.
      toObservable(config.config)
        .pipe(
          takeUntilDestroyed(),
          switchMap((cfg) =>
            cfg.scanIntervalSeconds > 0 ? interval(cfg.scanIntervalSeconds * 1000) : EMPTY,
          ),
          filter(visible),
          switchMap(() => {
            const cfg = config.config();
            return api.getRsiScan(
              false,
              cfg.rsiOversoldThreshold,
              cfg.rsiOverboughtThreshold,
              store.logicMode(),
            );
          }),
        )
        .subscribe({ next: (r) => store._applyBackgroundScan(r) });

      // Poll EOD window status every 30 seconds.
      interval(30_000)
        .pipe(filter(visible), takeUntilDestroyed())
        .subscribe(() => store._checkEodWindowStatus());

      // Yesterday's EOD signals and market indices refresh every 5 minutes.
      interval(5 * 60_000)
        .pipe(filter(visible), takeUntilDestroyed())
        .subscribe(() => {
          store._loadYesterdayEod();
          store._loadMarketIndices();
        });
    },
  }),
);
