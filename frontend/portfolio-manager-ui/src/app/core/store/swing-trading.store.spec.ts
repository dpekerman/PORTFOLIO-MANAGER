import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { unprotected } from '@ngrx/signals/testing';
import { of, Subject, throwError } from 'rxjs';
import {
  OpenTradesResponse,
  RiskPosition,
  SwingTradeRow,
} from '../models/swing-trading.models';
import { SwingTradingApiService } from '../services/swing-trading-api.service';
import { AuthStore } from './auth.store';
import { AppRefreshStore } from './app-refresh.store';
import { FinalActionStore } from './final-action.store';
import { SwingTradingStore } from './swing-trading.store';

const row = (over: Partial<SwingTradeRow>): SwingTradeRow =>
  ({
    key: 'A|X',
    symbol: 'X',
    companyName: 'X Corp',
    account: 'A',
    lotIds: [1],
    needsAttention: false,
    protection: 'No Stop',
    riskStatuses: [],
    ...over,
  }) as SwingTradeRow;

const response = (rows: SwingTradeRow[]): OpenTradesResponse => ({
  rows,
  summary: { openTrades: rows.length, needsAttention: 0, totals: [], asOfUtc: null, anyStale: false, anyUnavailable: true },
  approachingThresholdPct: 5,
  thresholdNote: 'advisory',
});

const position = { accountType: 'A', symbol: 'X', hasOpenPosition: true, brokerStops: [], history: [], plan: null } as unknown as RiskPosition;

describe('SwingTradingStore', () => {
  let api: {
    getOpenTrades: ReturnType<typeof vi.fn>;
    getJournal: ReturnType<typeof vi.fn>;
    getRiskPosition: ReturnType<typeof vi.fn>;
    savePlan: ReturnType<typeof vi.fn>;
    addBrokerStop: ReturnType<typeof vi.fn>;
    updateBrokerStop: ReturnType<typeof vi.fn>;
    transitionBrokerStop: ReturnType<typeof vi.fn>;
  };
  const finalActions = new Map<number, { finalAction: string }>();
  const appRefresh = {
    isRefreshing: signal(false),
    error: signal<string | null>(null),
    lastRefreshedAt: signal<Date | null>(null),
    refreshAll: vi.fn(() => appRefresh.isRefreshing.set(true)),
  };

  beforeEach(() => {
    finalActions.clear();
    appRefresh.isRefreshing.set(false);
    appRefresh.error.set(null);
    appRefresh.lastRefreshedAt.set(null);
    appRefresh.refreshAll.mockClear();
    api = {
      getOpenTrades: vi.fn(() => of(response([row({}), row({ key: 'B|Y', symbol: 'Y', account: 'B', lotIds: [2], needsAttention: true })]))),
      getJournal: vi.fn(() => of([])),
      getRiskPosition: vi.fn(() => of(position)),
      savePlan: vi.fn(() => of(position)),
      addBrokerStop: vi.fn(() => of(position)),
      updateBrokerStop: vi.fn(() => of(position)),
      transitionBrokerStop: vi.fn(() => of(position)),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: SwingTradingApiService, useValue: api },
        { provide: AuthStore, useValue: { isAuthenticated: signal(false) } },
        { provide: FinalActionStore, useValue: { portfolioByItemId: () => finalActions } },
        { provide: AppRefreshStore, useValue: appRefresh },
      ],
    });
  });

  it('does not load before the user is authenticated', () => {
    TestBed.inject(SwingTradingStore);
    expect(api.getOpenTrades).not.toHaveBeenCalled();
  });

  it('loads open trades and summary', () => {
    const store = TestBed.inject(SwingTradingStore);
    store.loadOpenTrades();
    expect(store.rows()).toHaveLength(2);
    expect(store.summary()?.anyUnavailable).toBe(true);
    expect(store.thresholdPct()).toBe(5);
    expect(store.loading()).toBe(false);
    expect(store.error()).toBeNull();
  });

  it('reports a load failure without losing the screen', () => {
    api.getOpenTrades.mockReturnValueOnce(throwError(() => new Error('boom')));
    const store = TestBed.inject(SwingTradingStore);
    store.loadOpenTrades();
    expect(store.error()).toBe('Failed to load Swing trades');
    expect(store.loading()).toBe(false);
  });

  it('refreshes quotes before reloading trades, prevents duplicates and retains completion time', () => {
    const pending = new Subject<OpenTradesResponse>();
    api.getOpenTrades.mockReturnValueOnce(pending.asObservable());
    const store = TestBed.inject(SwingTradingStore);
    store.refresh();
    store.refresh();
    TestBed.tick();
    expect(appRefresh.refreshAll).toHaveBeenCalledExactlyOnceWith('user-click');
    expect(api.getOpenTrades).not.toHaveBeenCalled();
    expect(store.refreshPhase()).toBe('quotes');
    expect(store.refreshing()).toBe(true);

    appRefresh.lastRefreshedAt.set(new Date());
    appRefresh.isRefreshing.set(false);
    TestBed.tick();
    expect(api.getOpenTrades).toHaveBeenCalledTimes(1);
    expect(store.refreshPhase()).toBe('trades');
    expect(store.lastRefreshedAt()).toBeNull();
    pending.next(response([row({ currentPrice: 42 })]));
    pending.complete();
    TestBed.tick();
    expect(store.rows()[0].currentPrice).toBe(42);
    expect(store.refreshing()).toBe(false);
    expect(store.lastRefreshedAt()).toBeInstanceOf(Date);
    expect(store.refreshError()).toBeNull();
    expect(api.getJournal).not.toHaveBeenCalled();
  });

  it('refreshes an opened journal after quotes and retries the active journal even if its first load failed', () => {
    const store = TestBed.inject(SwingTradingStore);
    store.refresh(true);
    appRefresh.lastRefreshedAt.set(new Date());
    appRefresh.isRefreshing.set(false);
    TestBed.tick();
    expect(api.getJournal).toHaveBeenCalledTimes(1);
    expect(store.journalLoaded()).toBe(true);
    store.refresh();
    appRefresh.lastRefreshedAt.set(new Date());
    appRefresh.isRefreshing.set(false);
    TestBed.tick();
    expect(api.getJournal).toHaveBeenCalledTimes(2);
  });

  it('reports quote refresh failure without reloading old snapshots or claiming success', () => {
    const store = TestBed.inject(SwingTradingStore);
    store.refresh();
    appRefresh.error.set('Server unavailable');
    appRefresh.isRefreshing.set(false);
    TestBed.tick();
    expect(store.refreshError()).toBe('Server unavailable');
    expect(store.refreshing()).toBe(false);
    expect(store.lastRefreshedAt()).toBeNull();
    expect(api.getOpenTrades).not.toHaveBeenCalled();
  });

  it('reports cancellation instead of claiming the quotes refreshed', () => {
    const store = TestBed.inject(SwingTradingStore);
    store.refresh();
    appRefresh.isRefreshing.set(false);
    TestBed.tick();
    expect(store.refreshError()).toContain('cancelled');
    expect(store.lastRefreshedAt()).toBeNull();
    expect(api.getOpenTrades).not.toHaveBeenCalled();
  });

  it('reports a trade reload failure and can successfully retry', () => {
    api.getOpenTrades.mockReturnValueOnce(throwError(() => new Error('boom')));
    const store = TestBed.inject(SwingTradingStore);
    store.refresh();
    appRefresh.lastRefreshedAt.set(new Date());
    appRefresh.isRefreshing.set(false);
    TestBed.tick();
    expect(store.refreshError()).toBe('Failed to load Swing trades');
    expect(store.lastRefreshedAt()).toBeNull();
    store.refresh();
    expect(store.refreshError()).toBeNull();
    appRefresh.lastRefreshedAt.set(new Date());
    appRefresh.isRefreshing.set(false);
    TestBed.tick();
    expect(store.lastRefreshedAt()).toBeInstanceOf(Date);
    expect(store.refreshError()).toBeNull();
  });

  it('does not claim success when the journal reload fails', () => {
    api.getJournal.mockReturnValueOnce(throwError(() => new Error('boom')));
    const store = TestBed.inject(SwingTradingStore);
    store.refresh(true);
    appRefresh.lastRefreshedAt.set(new Date());
    appRefresh.isRefreshing.set(false);
    TestBed.tick();
    expect(store.refreshError()).toBe('Failed to load the Trade Journal');
    expect(store.lastRefreshedAt()).toBeNull();
    expect(store.refreshing()).toBe(false);
  });

  it('filters and clears filters', () => {
    const store = TestBed.inject(SwingTradingStore);
    store.loadOpenTrades();
    store.setFilters({ account: 'B' });
    expect(store.filteredRows().map((r) => r.symbol)).toEqual(['Y']);
    expect(store.hasFilters()).toBe(true);
    store.setFilters({ attentionOnly: true });
    expect(store.attentionCount()).toBe(1);
    store.resetFilters();
    expect(store.filteredRows()).toHaveLength(2);
    expect(store.hasFilters()).toBe(false);
  });

  it('reads the canonical Final Action from the existing store and never invents one', () => {
    const store = TestBed.inject(SwingTradingStore);
    store.loadOpenTrades();
    finalActions.set(2, { finalAction: 'Hold Swing' });
    expect(store.finalActionOf(store.rows()[0])).toBeNull();
    expect(store.finalActionOf(store.rows()[1])).toBe('Hold Swing');
    store.setFilters({ finalAction: 'Hold Swing' });
    expect(store.filteredRows().map((r) => r.symbol)).toEqual(['Y']);
    expect(store.finalActionOptions()).toEqual(['Hold Swing']);
  });

  it('saves a plan, shows the returned position and refreshes the grid', async () => {
    const store = TestBed.inject(SwingTradingStore);
    const saved = await store.savePlan({ accountType: 'A', symbol: 'X', plannedStopPrice: 9, targetPrice: null, stopType: 'Fixed', stopRationale: null, tradeRationale: null, riskNotes: null, reason: null, rowVersion: null });
    expect(saved).toBe(true);
    expect(store.position()).toEqual(position);
    expect(store.saving()).toBe(false);
    expect(api.getOpenTrades).toHaveBeenCalled();
  });

  it('routes a new broker stop to POST and an existing one to PUT', async () => {
    const store = TestBed.inject(SwingTradingStore);
    const req = { id: null, accountType: 'A', symbol: 'X', orderType: 'StopMarket' as const, triggerPrice: 9, limitPrice: null, coveredQuantity: null, placedDate: null, brokerOrderRef: null, lastUserVerifiedDate: null, notes: null, reason: null, allowOverCoverage: false, rowVersion: null };
    await store.saveBrokerStop(req);
    expect(api.addBrokerStop).toHaveBeenCalledTimes(1);
    await store.saveBrokerStop({ ...req, id: 7 });
    expect(api.updateBrokerStop).toHaveBeenCalledWith(7, expect.objectContaining({ id: 7 }));
  });

  it('flags a 409 as a concurrency conflict and keeps the user message', async () => {
    api.savePlan.mockReturnValueOnce(
      throwError(() => new HttpErrorResponse({ status: 409, error: { error: 'The risk plan was changed by someone else.' } })),
    );
    const store = TestBed.inject(SwingTradingStore);
    const saved = await store.savePlan({ accountType: 'A', symbol: 'X', plannedStopPrice: 9, targetPrice: null, stopType: 'Fixed', stopRationale: null, tradeRationale: null, riskNotes: null, reason: null, rowVersion: 'old' });
    expect(saved).toBe(false);
    expect(store.conflict()).toBe(true);
    expect(store.positionError()).toContain('changed by someone else');
    expect(store.saving()).toBe(false);
  });

  it('surfaces a validation message from the server on a 400', async () => {
    api.addBrokerStop.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'Trigger price must be a positive price.' } })));
    const store = TestBed.inject(SwingTradingStore);
    const ok = await store.saveBrokerStop({ id: null, accountType: 'A', symbol: 'X', orderType: 'StopMarket', triggerPrice: -1, limitPrice: null, coveredQuantity: null, placedDate: null, brokerOrderRef: null, lastUserVerifiedDate: null, notes: null, reason: null, allowOverCoverage: false, rowVersion: null });
    expect(ok).toBe(false);
    expect(store.conflict()).toBe(false);
    expect(store.positionError()).toBe('Trigger price must be a positive price.');
  });

  it('loads a position for the dialog and clears it on close', () => {
    const store = TestBed.inject(SwingTradingStore);
    store.loadPosition({ account: 'A', symbol: 'X' });
    expect(store.position()).toEqual(position);
    store.clearPosition();
    expect(store.position()).toBeNull();
  });

  it('keeps journal state separate and loads it on demand', () => {
    const store = TestBed.inject(SwingTradingStore);
    expect(store.journalLoaded()).toBe(false);
    store.loadJournal();
    expect(api.getJournal).toHaveBeenCalledTimes(1);
    expect(store.journalLoaded()).toBe(true);
  });

  it('can be patched in tests through unprotected()', () => {
    const store = TestBed.inject(SwingTradingStore);
    expect(unprotected(store)).toBeDefined();
  });
});
