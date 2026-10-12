import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { PortfolioRsiAnalysisService } from '../services/portfolio-rsi-analysis.service';
import { AppRefreshStore } from './app-refresh.store';
import { ConfigStore } from './config.store';
import { DashboardStore } from './dashboard.store';
import { EodSignalsStore } from './eod-signals.store';
import { PortfolioStore } from './portfolio.store';
import { WatchlistRsiStore } from './watchlist-rsi.store';
import { WatchlistStore } from './watchlist.store';

const refreshResult = {
  portfolioSymbols: ['AAPL', 'MSFT'],
  watchlistSymbols: ['NVDA'],
  portfolioSummaries: [],
  watchlistSummaries: [],
};

describe('AppRefreshStore', () => {
  const config = signal({ appRefreshSeconds: 0 });
  const api = { refreshAll: vi.fn() };
  const portfolio = { setFromRefresh: vi.fn() };
  const watchlist = { setFromRefresh: vi.fn() };
  const dashboard = {
    refresh: vi.fn(),
    loadPortfolioActions: vi.fn(),
    loadEodSummary: vi.fn(),
    loadMarketLeadership: vi.fn(),
    loadPerformanceSummary: vi.fn(),
  };
  const eodSignals = { loadSignals: vi.fn() };
  const watchlistRsi = { triggerRefresh: vi.fn() };

  beforeEach(() => {
    vi.useFakeTimers();
    localStorage.clear();
    config.set({ appRefreshSeconds: 0 });
    api.refreshAll.mockReset().mockReturnValue(of(refreshResult));
    [portfolio, watchlist, dashboard, eodSignals, watchlistRsi].forEach((dep) =>
      Object.values(dep).forEach((fn) => fn.mockReset()),
    );
    TestBed.configureTestingModule({
      providers: [
        { provide: PortfolioApiService, useValue: api },
        { provide: ConfigStore, useValue: { config } },
        { provide: PortfolioStore, useValue: portfolio },
        { provide: WatchlistStore, useValue: watchlist },
        { provide: DashboardStore, useValue: dashboard },
        { provide: EodSignalsStore, useValue: eodSignals },
        { provide: WatchlistRsiStore, useValue: watchlistRsi },
        { provide: PortfolioRsiAnalysisService, useValue: {} },
      ],
    });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  function store(): InstanceType<typeof AppRefreshStore> {
    const s = TestBed.inject(AppRefreshStore);
    TestBed.tick();
    return s;
  }

  it('pushes fresh data into the stores before rebuilding the dashboard', () => {
    const s = store();

    s.refreshAll();

    expect(portfolio.setFromRefresh).toHaveBeenCalledOnce();
    expect(watchlist.setFromRefresh).toHaveBeenCalledOnce();
    expect(dashboard.refresh).toHaveBeenCalledOnce();
    expect(watchlistRsi.triggerRefresh).toHaveBeenCalledWith(['NVDA']);
    expect(dashboard.loadPerformanceSummary).toHaveBeenCalledOnce();
    expect(eodSignals.loadSignals).toHaveBeenCalledOnce();
    expect(s.isRefreshing()).toBe(true);
  });

  it('finishes and persists the timestamp 1.5s after the response', () => {
    const s = store();

    s.refreshAll();
    vi.advanceTimersByTime(1500);

    expect(s.isRefreshing()).toBe(false);
    expect(s.refreshSteps().every((step) => step.status === 'done')).toBe(true);
    expect(s.lastRefreshedAt()).toBeInstanceOf(Date);
    expect(localStorage.getItem('pm_last_refreshed')).toBeTruthy();
  });

  it('ignores a second refresh while one is in flight', () => {
    const s = store();

    s.refreshAll();
    s.refreshAll();

    expect(api.refreshAll).toHaveBeenCalledOnce();
  });

  it('reports portfolio and watchlist counts on their steps', () => {
    const s = store();

    s.refreshAll();

    const portfolioStep = s.refreshSteps().find((x) => x.key === 'fetch-portfolio')!;
    expect(portfolioStep.totalCount).toBe(2);
    expect(portfolioStep.completedCount).toBe(2);
  });

  describe('failures', () => {
    it('marks a network error offline and schedules an auto-retry', () => {
      api.refreshAll.mockReturnValue(throwError(() => ({ status: 0 })));
      const s = store();

      s.refreshAll();

      expect(s.isOffline()).toBe(true);
      expect(s.offlineReason()).toBe('network');
      expect(s.error()).toBe('No internet connection');
      expect(s.nextRetryIn()).toBe(5);
      expect(s.refreshSteps().every((step) => step.status === 'error')).toBe(true);

      api.refreshAll.mockReturnValue(of(refreshResult));
      vi.advanceTimersByTime(5000);

      // The retry starts a fresh attempt, which resets retryCount to 0.
      expect(s.retryCount()).toBe(0);
      expect(s.nextRetryIn()).toBeNull();
      expect(api.refreshAll).toHaveBeenCalledTimes(2);
    });

    it('does not auto-retry a server error', () => {
      api.refreshAll.mockReturnValue(throwError(() => ({ status: 503 })));
      const s = store();

      s.refreshAll();

      expect(s.offlineReason()).toBe('server');
      expect(s.nextRetryIn()).toBeNull();
      vi.advanceTimersByTime(10_000);
      expect(api.refreshAll).toHaveBeenCalledOnce();
    });

    it('treats an unclassified failure as a plain error, not offline', () => {
      api.refreshAll.mockReturnValue(throwError(() => ({ status: 404 })));
      const s = store();

      s.refreshAll();

      expect(s.isOffline()).toBe(false);
      expect(s.error()).toBe('Refresh failed');
    });

    it('counts down one second at a time and keeps retrying while offline', () => {
      api.refreshAll.mockReturnValue(throwError(() => ({ status: 0 })));
      const s = store();

      s.refreshAll();
      expect(s.nextRetryIn()).toBe(5);

      vi.advanceTimersByTime(2000);
      expect(s.nextRetryIn()).toBe(3);

      vi.advanceTimersByTime(3000);
      expect(api.refreshAll).toHaveBeenCalledTimes(2);

      vi.advanceTimersByTime(5000);
      expect(api.refreshAll).toHaveBeenCalledTimes(3);
    });
  });

  it('cancelRefresh aborts the in-flight request and resets the steps', () => {
    const pending = new Subject<typeof refreshResult>();
    api.refreshAll.mockReturnValue(pending);
    const s = store();
    s.refreshAll();

    s.cancelRefresh();

    expect(s.isRefreshing()).toBe(false);
    expect(s.refreshSteps().every((step) => step.status === 'idle')).toBe(true);

    pending.next(refreshResult);
    expect(dashboard.refresh).not.toHaveBeenCalled();
  });

  it('cancelRefresh is a no-op when nothing is running', () => {
    const s = store();
    s.cancelRefresh();
    expect(s.isRefreshing()).toBe(false);
  });

  it('runs the auto-timer only when appRefreshSeconds is positive', () => {
    const s = store();
    vi.advanceTimersByTime(60_000);
    expect(api.refreshAll).not.toHaveBeenCalled();

    config.set({ appRefreshSeconds: 10 });
    TestBed.tick();
    vi.advanceTimersByTime(10_000);

    expect(api.refreshAll).toHaveBeenCalledOnce();
    expect(s.isRefreshing()).toBe(true);
  });

  it('clears the offline flag when the browser reports it is back online', () => {
    api.refreshAll.mockReturnValue(throwError(() => ({ status: 0 })));
    const s = store();
    s.refreshAll();
    expect(s.isOffline()).toBe(true);

    window.dispatchEvent(new Event('online'));

    expect(s.isOffline()).toBe(false);
    expect(s.offlineReason()).toBeNull();
  });

  it('secondsSinceRefresh is null until a refresh has completed', () => {
    const s = store();
    expect(s.secondsSinceRefresh()).toBeNull();

    s.refreshAll();
    vi.advanceTimersByTime(1500);

    expect(s.secondsSinceRefresh()).toBe(0);
  });
});
