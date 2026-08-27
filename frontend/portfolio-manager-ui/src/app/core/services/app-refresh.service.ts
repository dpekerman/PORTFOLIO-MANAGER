import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { EMPTY, Subject, filter, interval, switchMap, takeUntil } from 'rxjs';
import { ConfigService } from './config.service';
import { DashboardStateService } from './dashboard-state.service';
import { PortfolioApiService } from './portfolio-api.service';
import { PortfolioStateService } from './portfolio-state.service';
import { WatchlistStateService } from './watchlist-state.service';

export interface RefreshStep {
  key: string;
  label: string;
  status: 'idle' | 'loading' | 'done' | 'error';
}

const LAST_REFRESHED_KEY = 'pm_last_refreshed';

@Injectable({ providedIn: 'root' })
export class AppRefreshService {
  private readonly api = inject(PortfolioApiService);
  private readonly configService = inject(ConfigService);
  private readonly portfolioState = inject(PortfolioStateService);
  private readonly watchlistState = inject(WatchlistStateService);
  private readonly dashboardState = inject(DashboardStateService);

  private readonly _isRefreshing = signal(false);
  private readonly _steps = signal<RefreshStep[]>(this.buildSteps());
  private readonly _lastRefreshedAt = signal<Date | null>(this.loadLastRefreshed());
  private readonly _error = signal<string | null>(null);
  private readonly cancel$ = new Subject<void>();

  readonly isRefreshing = this._isRefreshing.asReadonly();
  readonly refreshSteps = this._steps.asReadonly();
  readonly lastRefreshedAt = this._lastRefreshedAt.asReadonly();
  readonly error = this._error.asReadonly();

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
      .subscribe(() => this.refreshAll());
  }

  refreshAll(): void {
    if (this._isRefreshing()) return;

    this._isRefreshing.set(true);
    this._error.set(null);
    this._steps.set([
      { key: 'fetch', label: 'Fetching live quotes (portfolio & watchlist)', status: 'loading' },
      { key: 'dashboard', label: 'Rebuilding dashboard snapshot', status: 'idle' },
      { key: 'actions', label: 'Reloading action scores & signals', status: 'idle' },
      { key: 'ui', label: 'Updating all views', status: 'idle' },
    ]);

    this.api
      .refreshAll()
      .pipe(takeUntil(this.cancel$))
      .subscribe({
        next: (result) => {
          this.setStepStatus('fetch', 'done');
          this.setStepStatus('dashboard', 'loading');

          // Push fresh data directly — no loading state, no extra HTTP round-trips
          this.portfolioState.setFromRefresh(result.portfolioSummaries);
          this.watchlistState.setFromRefresh(result.watchlistSummaries);
          this.dashboardState.load();

          this.setStepStatus('dashboard', 'done');
          this.setStepStatus('actions', 'loading');

          // Force-reload action scores and performance summary after snapshot rebuild
          this.dashboardState.loadPortfolioActions();
          this.dashboardState.loadStateChanges();
          this.dashboardState.loadMarketLeadership();
          this.dashboardState.loadPerformanceSummary();

          this.setStepStatus('actions', 'done');
          this.setStepStatus('ui', 'loading');
          this.setStepStatus('ui', 'done');
          this._isRefreshing.set(false);
          const now = new Date();
          this._lastRefreshedAt.set(now);
          localStorage.setItem(LAST_REFRESHED_KEY, now.toISOString());
        },
        error: (err) => {
          const msg = err?.status === 0 ? 'Network error' : 'Refresh failed';
          this._error.set(msg);
          this._steps.update((steps) =>
            steps.map((s) =>
              s.status === 'loading' || s.status === 'idle' ? { ...s, status: 'error' } : s,
            ),
          );
          this._isRefreshing.set(false);
        },
      });
  }

  cancelRefresh(): void {
    if (!this._isRefreshing()) return;
    this.cancel$.next();
    this._isRefreshing.set(false);
    this._steps.update((steps) =>
      steps.map((s) =>
        s.status === 'loading' || s.status === 'idle' ? { ...s, status: 'idle' } : s,
      ),
    );
  }

  private setStepStatus(key: string, status: RefreshStep['status']): void {
    this._steps.update((steps) => steps.map((s) => (s.key === key ? { ...s, status } : s)));
  }

  private buildSteps(): RefreshStep[] {
    return [
      { key: 'fetch', label: 'Fetching live quotes (portfolio & watchlist)', status: 'idle' },
      { key: 'dashboard', label: 'Rebuilding dashboard snapshot', status: 'idle' },
      { key: 'actions', label: 'Reloading action scores & signals', status: 'idle' },
      { key: 'ui', label: 'Updating all views', status: 'idle' },
    ];
  }

  private loadLastRefreshed(): Date | null {
    try {
      const raw = localStorage.getItem(LAST_REFRESHED_KEY);
      return raw ? new Date(raw) : null;
    } catch {
      return null;
    }
  }
}
