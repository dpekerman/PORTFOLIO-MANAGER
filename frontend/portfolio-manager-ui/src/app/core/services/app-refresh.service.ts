import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { EMPTY, Subject, filter, interval, switchMap, takeUntil } from 'rxjs';
import { ConfigService } from './config.service';
import { DashboardStateService } from './dashboard-state.service';
import { EodSignalsStateService } from './eod-signals-state.service';
import { PortfolioApiService } from './portfolio-api.service';
import { PortfolioStateService } from './portfolio-state.service';
import { WatchlistStateService } from './watchlist-state.service';

export interface RefreshStep {
  key: string;
  label: string;
  status: 'idle' | 'loading' | 'done' | 'error';
  totalCount?: number; // e.g., 15 tickers
  completedCount?: number; // e.g., 3 done
  currentItem?: string; // e.g., "AAPL"
}

export type RefreshReason = 'auto-timer' | 'user-click' | 'feature-refresh';

@Injectable({ providedIn: 'root' })
export class AppRefreshService {
  private readonly api = inject(PortfolioApiService);
  private readonly configService = inject(ConfigService);
  private readonly portfolioState = inject(PortfolioStateService);
  private readonly watchlistState = inject(WatchlistStateService);
  private readonly dashboardState = inject(DashboardStateService);
  private readonly eodSignalsState = inject(EodSignalsStateService);

  private readonly _isRefreshing = signal(false);
  private readonly _steps = signal<RefreshStep[]>(this.buildSteps());
  private readonly _lastRefreshedAt = signal<Date | null>(this.loadLastRefreshed());
  private readonly _error = signal<string | null>(null);
  private readonly _isOffline = signal(false);
  private readonly _offlineReason = signal<'network' | 'server' | 'timeout' | null>(null);
  private readonly _retryCount = signal(0);
  private readonly _nextRetryIn = signal<number | null>(null);
  private readonly _refreshReason = signal<RefreshReason>('auto-timer');
  private readonly cancel$ = new Subject<void>();
  private retryTimeoutId: number | null = null;

  readonly isRefreshing = this._isRefreshing.asReadonly();
  readonly refreshSteps = this._steps.asReadonly();
  readonly lastRefreshedAt = this._lastRefreshedAt.asReadonly();
  readonly error = this._error.asReadonly();
  readonly isOffline = this._isOffline.asReadonly();
  readonly offlineReason = this._offlineReason.asReadonly();
  readonly retryCount = this._retryCount.asReadonly();
  readonly nextRetryIn = this._nextRetryIn.asReadonly();
  readonly refreshReason = this._refreshReason.asReadonly();

  readonly secondsSinceRefresh = computed(() => {
    const last = this._lastRefreshedAt();
    if (!last) return null;
    return Math.floor((Date.now() - last.getTime()) / 1000);
  });

  constructor() {
    // Single auto-refresh timer driven by appRefreshSeconds
    toObservable(this.configService.config)
      .pipe(
        takeUntilDestroyed(),
        switchMap((cfg) =>
          cfg.appRefreshSeconds > 0 ? interval(cfg.appRefreshSeconds * 1000) : EMPTY,
        ),
        filter(() => document.visibilityState === 'visible'),
      )
      .subscribe(() => {
        this._refreshReason.set('auto-timer');
        this.refreshAll();
      });

    // Listen to online/offline state changes
    effect(() => {
      const handleOnline = () => {
        if (this._isOffline()) {
          this._isOffline.set(false);
          this._offlineReason.set(null);
          this._nextRetryIn.set(null);
        }
      };

      const handleOffline = () => {
        this._isOffline.set(true);
      };

      window.addEventListener('online', handleOnline);
      window.addEventListener('offline', handleOffline);

      return () => {
        window.removeEventListener('online', handleOnline);
        window.removeEventListener('offline', handleOffline);
      };
    });
  }

  refreshAll(reason: RefreshReason = 'user-click'): void {
    if (this._isRefreshing()) return;

    this._refreshReason.set(reason);
    this._isRefreshing.set(true);
    this._error.set(null);
    this._isOffline.set(false);
    this._offlineReason.set(null);
    this._retryCount.set(0);
    this._nextRetryIn.set(null);
    this._steps.set(this.buildSteps());

    this.api
      .refreshAll()
      .pipe(takeUntil(this.cancel$))
      .subscribe({
        next: (result) => {
          // Set portfolio and watchlist counts from the response
          this.setStepProgress(
            'fetch-portfolio',
            result.portfolioSymbols.length,
            result.portfolioSymbols.length,
          );
          this.setStepProgress(
            'fetch-watchlist',
            result.watchlistSymbols.length,
            result.watchlistSymbols.length,
          );
          this.setStepStatus('fetch-portfolio', 'done');
          this.setStepStatus('fetch-watchlist', 'done');
          this.setStepStatus('dashboard', 'loading');

          // Push fresh data directly — no loading state, no extra HTTP round-trips
          this.portfolioState.setFromRefresh(result.portfolioSummaries);
          this.watchlistState.setFromRefresh(result.watchlistSummaries);
          this.dashboardState.load();

          this.setStepStatus('dashboard', 'done');
          this.setStepStatus('actions', 'loading');

          // Force-reload action scores and performance summary after snapshot rebuild
          this.dashboardState.loadPortfolioActions();
          this.dashboardState.loadEodSummary();
          this.dashboardState.loadMarketLeadership();
          this.dashboardState.loadPerformanceSummary();

          this.setStepStatus('actions', 'done');
          this.setStepStatus('eod-signals', 'loading');

          // Poll for new EOD signals
          this.eodSignalsState.loadSignals();

          // Simulate EOD signals completion (loadSignals is async, completes within seconds)
          setTimeout(() => {
            this.setStepStatus('eod-signals', 'done');
            this._isRefreshing.set(false);
            const now = new Date();
            this._lastRefreshedAt.set(now);
            localStorage.setItem('pm_last_refreshed', now.toISOString());
          }, 1500);
        },
        error: (err) => {
          this.handleRefreshError(err);
        },
      });
  }

  retry(): void {
    this.refreshAll('user-click');
  }

  cancelRefresh(): void {
    if (!this._isRefreshing()) return;

    if (this.retryTimeoutId !== null) {
      clearTimeout(this.retryTimeoutId);
      this.retryTimeoutId = null;
    }

    this.cancel$.next();
    this._isRefreshing.set(false);
    this._isOffline.set(false);
    this._offlineReason.set(null);
    this._nextRetryIn.set(null);
    this._steps.update((steps) =>
      steps.map((s) =>
        s.status === 'loading' || s.status === 'idle' ? { ...s, status: 'idle' } : s,
      ),
    );
  }

  setStepProgress(key: string, current: number, total: number): void {
    this._steps.update((steps) =>
      steps.map((s) => (s.key === key ? { ...s, completedCount: current, totalCount: total } : s)),
    );
  }

  setCurrentItem(key: string, symbol: string): void {
    this._steps.update((steps) =>
      steps.map((s) => (s.key === key ? { ...s, currentItem: symbol } : s)),
    );
  }

  private setStepStatus(key: string, status: RefreshStep['status']): void {
    this._steps.update((steps) => steps.map((s) => (s.key === key ? { ...s, status } : s)));
  }

  private handleRefreshError(err: any): void {
    // Detect error type
    let reason: 'network' | 'server' | 'timeout' | null = null;

    if (err?.status === 0) {
      reason = 'network';
    } else if (err?.status >= 500) {
      reason = 'server';
    } else if (err?.name === 'TimeoutError') {
      reason = 'timeout';
    }

    if (reason) {
      this._isOffline.set(true);
      this._offlineReason.set(reason);
      this._error.set(this.getErrorMessage(reason));

      // Auto-retry for network and timeout errors
      if (reason !== 'server') {
        this.scheduleAutoRetry();
      }
    } else {
      this._error.set('Refresh failed');
    }

    this._steps.update((steps) =>
      steps.map((s) =>
        s.status === 'loading' || s.status === 'idle' ? { ...s, status: 'error' } : s,
      ),
    );
    this._isRefreshing.set(false);
  }

  private scheduleAutoRetry(delaySeconds: number = 5, maxRetries: number = 3): void {
    const currentRetry = this._retryCount();
    if (currentRetry >= maxRetries) {
      return; // Max retries exceeded
    }

    let countdownSeconds = delaySeconds;
    this._nextRetryIn.set(countdownSeconds);

    const countdownInterval = setInterval(() => {
      countdownSeconds--;
      if (countdownSeconds > 0) {
        this._nextRetryIn.set(countdownSeconds);
      } else {
        clearInterval(countdownInterval);
        this._nextRetryIn.set(null);
        this._retryCount.set(currentRetry + 1);
        this.refreshAll(this._refreshReason());
      }
    }, 1000);

    this.retryTimeoutId = countdownInterval as unknown as number;
  }

  private getErrorMessage(reason: 'network' | 'server' | 'timeout'): string {
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

  private buildSteps(): RefreshStep[] {
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

  private loadLastRefreshed(): Date | null {
    try {
      const raw = localStorage.getItem('pm_last_refreshed');
      return raw ? new Date(raw) : null;
    } catch {
      return null;
    }
  }
}
