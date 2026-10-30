import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { SignalAuditRow } from '../models/portfolio.models';
import { SignalsAuditApiService } from '../services/signals-audit-api.service';
import { AuthStore } from './auth.store';
import { SignalsAuditStore } from './signals-audit.store';

describe('SignalsAuditStore', () => {
  const isAuthenticated = signal(false);
  const snack = { open: vi.fn() };
  const horizon = { pct: null, evalDate: null, evalPrice: null, status: 'Pending' as const };
  const row = (id: number, myAction: SignalAuditRow['myAction'] = 'Pending'): SignalAuditRow => ({
    id,
    symbol: 'LMT.TO',
    role: 'Core',
    signalDate: '2026-10-08',
    daysSince: 3,
    dateAnomaly: false,
    signalType: 'PRIORITY_CANDIDATE',
    signalTypeLabel: 'Priority Candidate',
    category: 'Entry',
    direction: 'Buy',
    source: 'Watchlist',
    signalPrice: 28.71,
    currency: 'CAD',
    score: 78,
    return5D: horizon,
    return20D: horizon,
    return1M: horizon,
    today: horizon,
    myAction,
    hasNote: false,
    linkedTransactionId: null,
    linkKind: 'None',
    origin: 'Live',
    episodeActive: true,
    eligibilityStatus: 'Verified',
    priceBasis: 'Verified',
  });
  const summary = { total: 1, priorityCandidate: 1, entryAdd: 1, trimExit: 0, actioned: 0, pendingReview: 1, effectiveness: [] };
  const effectivenessResult = {
    window: '90',
    windowStart: '2026-07-29',
    windowEnd: '2026-10-27',
    coverage: { cohortEvents: 0, withPriceHistory: 0, noPriceHistory: 0, overlappingEvents: 0, complete: true, batches: 0, note: null },
    rows: [],
  };
  const api = {
    query: vi.fn(),
    summary: vi.fn(),
    effectiveness: vi.fn(),
    getSignalTypes: vi.fn(),
    getSettings: vi.fn(),
    saveSettings: vi.fn(),
    saveAction: vi.fn(),
    detail: vi.fn(),
    transactionCandidates: vi.fn(),
  };

  beforeEach(() => {
    isAuthenticated.set(false);
    vi.clearAllMocks();
    api.query.mockReturnValue(of({ items: [row(1)], total: 1, page: 1, pageSize: 25 }));
    api.summary.mockReturnValue(of(summary));
    api.effectiveness.mockReturnValue(of(effectivenessResult));
    api.getSignalTypes.mockReturnValue(of([]));
    api.getSettings.mockReturnValue(of({ priorityEnabled: true, priorityThreshold: 75, activatedAtUtc: null, baselineCount: 0 }));
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthStore, useValue: { isAuthenticated } },
        { provide: SignalsAuditApiService, useValue: api },
        { provide: MatSnackBar, useValue: snack },
      ],
    });
  });

  it('does not call the API before the user is authenticated, then loads settings once', () => {
    const store = TestBed.inject(SignalsAuditStore);
    TestBed.tick();
    expect(api.getSettings).not.toHaveBeenCalled();

    isAuthenticated.set(true);
    TestBed.tick();
    expect(api.getSettings).toHaveBeenCalledTimes(1);
    expect(store.settings()?.priorityThreshold).toBe(75);
  });

  it('loads the page and its filter-matching summary together', () => {
    const store = TestBed.inject(SignalsAuditStore);
    store.load();
    expect(store.items()).toHaveLength(1);
    expect(store.summary()?.pendingReview).toBe(1);
    expect(store.loading()).toBe(false);
  });

  it('a filter change returns to page 1 and refetches with the filter', () => {
    const store = TestBed.inject(SignalsAuditStore);
    store.setPage(3, 25);
    store.setFilters({ source: 'Watchlist', myAction: 'Pending' });
    expect(store.page()).toBe(1);
    expect(api.query).toHaveBeenLastCalledWith(expect.objectContaining({ source: 'Watchlist', myAction: 'Pending', page: 1 }));
    expect(api.summary).toHaveBeenLastCalledWith(expect.objectContaining({ source: 'Watchlist' }));
    expect(store.hasActiveFilters()).toBe(true);
  });

  it('resetFilters restores defaults but keeps the sort', () => {
    const store = TestBed.inject(SignalsAuditStore);
    store.setSort('symbol', 'asc');
    store.setFilters({ ticker: 'lmt', role: ['Core'] });
    store.resetFilters();
    expect(store.hasActiveFilters()).toBe(false);
    expect(store.sortBy()).toBe('symbol');
  });

  it('PM-015: the landing load is Active-first and newest-first with NO implicit date window', () => {
    const store = TestBed.inject(SignalsAuditStore);
    expect(store.status()).toBe('Active');
    expect(store.sortBy()).toBe('signalDate');
    expect(store.sortDir()).toBe('desc');
    expect(store.hasActiveFilters()).toBe(false);

    store.loadInitial();
    // A single request: the old behaviour first asked for the newest day and then restricted from/to to it.
    expect(api.query).toHaveBeenCalledTimes(1);
    expect(api.query).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Active', from: '', to: '', sortBy: 'signalDate', sortDir: 'desc', page: 1 }));
    expect(store.from()).toBe('');
    expect(store.to()).toBe('');
  });

  it('PM-015: explicit user dates are still honoured and Reset returns to Active', () => {
    const store = TestBed.inject(SignalsAuditStore);
    store.setFilters({ from: '2026-10-01', to: '2026-10-05', status: 'All' });
    expect(api.query).toHaveBeenLastCalledWith(expect.objectContaining({ from: '2026-10-01', to: '2026-10-05', status: 'All' }));
    expect(store.hasActiveFilters()).toBe(true);
    store.resetFilters();
    expect(store.status()).toBe('Active');
    expect(store.from()).toBe('');
    expect(store.hasActiveFilters()).toBe(false);
  });

  it('PM-015: changing the lifecycle filter or sort returns to page 1 and refetches', () => {
    const store = TestBed.inject(SignalsAuditStore);
    store.setPage(4, 25);
    store.setFilters({ status: 'Acted' });
    expect(store.page()).toBe(1);
    expect(api.query).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Acted', page: 1 }));
    store.setPage(3, 25);
    store.setSort('lifecycleStatus', 'asc');
    expect(store.page()).toBe(1);
    expect(api.query).toHaveBeenLastCalledWith(expect.objectContaining({ sortBy: 'lifecycleStatus', sortDir: 'asc', page: 1 }));
  });

  it('PM-015: truncation metadata comes from the response, only flags computed sorts, and clears on a SQL-backed sort', () => {
    const store = TestBed.inject(SignalsAuditStore);
    api.query.mockReturnValue(of({ items: [row(1)], total: 5100, page: 1, pageSize: 25, evaluated: 5000, evaluationLimit: 5000, partial: true }));
    store.setSort('return20D', 'desc');
    expect(store.gridPartial()).toBe(true);
    expect(store.gridEvaluated()).toBe(5000);
    expect(store.total()).toBe(5100);

    api.query.mockReturnValue(of({ items: [row(1)], total: 5100, page: 1, pageSize: 25, evaluated: 5100, evaluationLimit: 5000, partial: false }));
    store.setSort('signalDate', 'desc');
    expect(store.gridPartial()).toBe(false);
    expect(store.gridEvaluated()).toBe(5100);
  });

  it('PM-015: fetchAllRows has no page cap, reports complete results, and reports partial ones', async () => {
    const store = TestBed.inject(SignalsAuditStore);
    // 60 pages of 200 = 12,000 rows: more than the old 50-page (10,000 row) limit.
    api.query.mockImplementation((p: { page: number }) =>
      of({ items: Array.from({ length: 200 }, (_, i) => row(p.page * 1000 + i)), total: 12000, page: p.page, pageSize: 200, evaluated: 12000, partial: false }),
    );
    const complete = await store.fetchAllRows();
    expect(complete.rows).toHaveLength(12000);
    expect(complete.partial).toBe(false);

    api.query.mockImplementation((p: { page: number }) =>
      of({ items: p.page <= 25 ? Array.from({ length: 200 }, (_, i) => row(p.page * 1000 + i)) : [], total: 5100, page: p.page, pageSize: 200, evaluated: 5000, partial: true }),
    );
    const partial = await store.fetchAllRows();
    expect(partial.rows).toHaveLength(5000);
    expect(partial.partial).toBe(true);
    expect(partial.total).toBe(5100);
  });

  it('effectiveness defaults to a 90-day window and loads independently of the table filters', () => {
    const store = TestBed.inject(SignalsAuditStore);
    store.loadEffectiveness();
    expect(store.effectivenessWindow()).toBe('90');
    expect(api.effectiveness).toHaveBeenLastCalledWith(expect.objectContaining({ window: '90' }));
    expect(store.effectiveness()?.windowEnd).toBe('2026-10-27');
    api.effectiveness.mockClear();

    // Ticker, dates, status and My Action never reach effectiveness.
    store.setFilters({ ticker: 'LMT' });
    store.setFilters({ from: '2026-10-27', to: '2026-10-27' });
    store.setFilters({ status: 'Open' });
    store.setFilters({ myAction: 'Pending' });
    expect(api.effectiveness).not.toHaveBeenCalled();
    expect(api.query).toHaveBeenCalled();
  });

  it('effectiveness reloads exactly once for a window change and for each context filter change', () => {
    const store = TestBed.inject(SignalsAuditStore);
    store.loadEffectiveness();
    api.effectiveness.mockClear();

    store.setEffectivenessWindow('180');
    expect(api.effectiveness).toHaveBeenCalledTimes(1);
    expect(api.effectiveness).toHaveBeenLastCalledWith(expect.objectContaining({ window: '180' }));
    store.setEffectivenessWindow('180');
    expect(api.effectiveness).toHaveBeenCalledTimes(1);

    store.setFilters({ role: ['Core'] });
    expect(api.effectiveness).toHaveBeenCalledTimes(2);
    expect(api.effectiveness).toHaveBeenLastCalledWith(expect.objectContaining({ role: ['Core'], window: '180' }));
    store.setFilters({ source: 'Watchlist', signalType: ['X'], includeBaselineInStats: true });
    expect(api.effectiveness).toHaveBeenCalledTimes(3);
    store.resetFilters();
    expect(api.effectiveness).toHaveBeenCalledTimes(4);
    store.resetFilters();
    expect(api.effectiveness).toHaveBeenCalledTimes(4);
  });

  it('surfaces an effectiveness failure without breaking the table', () => {
    api.effectiveness.mockReturnValue(throwError(() => new Error('boom')));
    const store = TestBed.inject(SignalsAuditStore);
    store.loadEffectiveness();
    store.load();
    expect(store.effectivenessError()).toBe('Failed to load signal effectiveness');
    expect(store.effectivenessLoading()).toBe(false);
    expect(store.items()).toHaveLength(1);
  });

  it('surfaces a load failure as an error', () => {
    api.query.mockReturnValue(throwError(() => new Error('boom')));
    const store = TestBed.inject(SignalsAuditStore);
    store.load();
    expect(store.error()).toBe('Failed to load signals');
    expect(store.loading()).toBe(false);
  });

  it('saveAction updates the row, refreshes the summary and reports success', async () => {
    api.saveAction.mockReturnValue(of(row(1, 'Actioned')));
    const store = TestBed.inject(SignalsAuditStore);
    store.load();
    api.query.mockClear();

    await expect(store.saveAction(1, 'Actioned', 'followed', 42)).resolves.toBe(true);
    expect(api.saveAction).toHaveBeenCalledWith(1, 'Actioned', 'followed', 42);
    expect(api.query).toHaveBeenCalled();
  });

  it('saveAction reports the server message and returns false on failure', async () => {
    api.saveAction.mockReturnValue(throwError(() => ({ error: { title: 'The selected transaction does not match this signal.' } })));
    const store = TestBed.inject(SignalsAuditStore);
    await expect(store.saveAction(1, 'Actioned', null, 9)).resolves.toBe(false);
    expect(snack.open).toHaveBeenCalledWith('The selected transaction does not match this signal.', 'Close', expect.anything());
  });

  it('saveSettings stores the returned settings and signals failure', async () => {
    api.saveSettings.mockReturnValueOnce(of({ priorityEnabled: false, priorityThreshold: 80, activatedAtUtc: null, baselineCount: 0 }));
    const store = TestBed.inject(SignalsAuditStore);
    await expect(store.saveSettings(false, 80)).resolves.toBe(true);
    expect(store.settings()?.priorityThreshold).toBe(80);

    api.saveSettings.mockReturnValueOnce(throwError(() => ({ error: { title: 'Priority score threshold must be between 0 and 100.' } })));
    await expect(store.saveSettings(true, 500)).resolves.toBe(false);
    expect(store.settingsSaving()).toBe(false);
  });
});
