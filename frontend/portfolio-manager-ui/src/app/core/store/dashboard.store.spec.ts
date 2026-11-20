import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { DashboardResponse } from '../models/portfolio.models';
import { DashboardApiService } from '../services/dashboard-api.service';
import { DashboardStore } from './dashboard.store';

const rsiSection = (
  overrides: Partial<NonNullable<DashboardResponse['rsiSection']>> = {},
): DashboardResponse['rsiSection'] =>
  ({
    oversoldSignals: [],
    overboughtSignals: [],
    newTodayCount: 0,
    actionRequiredCount: 0,
    ...overrides,
  }) as NonNullable<DashboardResponse['rsiSection']>;

const snapshot = (section?: DashboardResponse['rsiSection']): DashboardResponse =>
  ({ rsiSection: section }) as unknown as DashboardResponse;

const row = (isNewToday: unknown, isActionRequired: unknown) =>
  ({ isNewToday, isActionRequired }) as never;

describe('DashboardStore', () => {
  const api = {
    getLatest: vi.fn(),
    refresh: vi.fn(),
    getPortfolioActions: vi.fn(),
    getEodSummary: vi.fn(),
    getMarketLeadership: vi.fn(),
    getActionScores: vi.fn(),
    getPerformanceSummary: vi.fn(),
    getDecisionPerformance: vi.fn(),
  };

  beforeEach(() => {
    Object.values(api).forEach((fn) => fn.mockReset());
    api.getLatest.mockReturnValue(of(snapshot()));
    api.refresh.mockReturnValue(of(snapshot()));
    api.getPortfolioActions.mockReturnValue(of([]));
    api.getEodSummary.mockReturnValue(of(null));
    api.getMarketLeadership.mockReturnValue(of(null));
    api.getActionScores.mockReturnValue(of([]));
    api.getPerformanceSummary.mockReturnValue(of(null));
    api.getDecisionPerformance.mockReturnValue(of({ rows: [] }));
    TestBed.configureTestingModule({
      providers: [{ provide: DashboardApiService, useValue: api }],
    });
  });

  function store(): InstanceType<typeof DashboardStore> {
    const s = TestBed.inject(DashboardStore);
    TestBed.tick();
    return s;
  }

  it('loads the snapshot and the eagerly-loaded widgets on creation', () => {
    const s = store();

    expect(api.getLatest).toHaveBeenCalledOnce();
    expect(api.getPortfolioActions).toHaveBeenCalledOnce();
    expect(api.getEodSummary).toHaveBeenCalledOnce();
    expect(api.getMarketLeadership).toHaveBeenCalledOnce();
    expect(api.getPerformanceSummary).toHaveBeenCalledOnce();
    // Action scores and decision performance are loaded on demand only.
    expect(api.getActionScores).not.toHaveBeenCalled();
    expect(s.loading()).toBe(false);
    expect(s.error()).toBeNull();
  });

  it('surfaces an error when the snapshot fails', () => {
    api.getLatest.mockReturnValue(throwError(() => new Error('boom')));

    const s = store();

    expect(s.error()).toBe('Dashboard snapshot unavailable');
    expect(s.loading()).toBe(false);
  });

  it('keeps widget failures silent and only clears their loading flag', () => {
    api.getPortfolioActions.mockReturnValue(throwError(() => new Error('boom')));

    const s = store();

    expect(s.error()).toBeNull();
    expect(s.actionsLoading()).toBe(false);
    expect(s.portfolioActions()).toEqual([]);
  });

  it('requests one metadata recompute when the rsi counts disagree with the rows', () => {
    api.getLatest.mockReturnValue(
      of(snapshot(rsiSection({ oversoldSignals: [row(true, false)], newTodayCount: 0 }))),
    );

    store();

    expect(api.refresh).toHaveBeenCalledOnce();
  });

  it('does not recompute when the rsi metadata is consistent', () => {
    api.getLatest.mockReturnValue(
      of(
        snapshot(
          rsiSection({
            oversoldSignals: [row(true, true)],
            newTodayCount: 1,
            actionRequiredCount: 1,
          }),
        ),
      ),
    );

    store();

    expect(api.refresh).not.toHaveBeenCalled();
  });

  it('only ever requests the metadata recompute once', () => {
    api.getLatest.mockReturnValue(
      of(snapshot(rsiSection({ oversoldSignals: [row(undefined, undefined)] }))),
    );

    const s = store();
    expect(api.refresh).toHaveBeenCalledOnce();

    s.load();
    TestBed.tick();

    expect(api.getLatest).toHaveBeenCalledTimes(2);
    expect(api.refresh).toHaveBeenCalledOnce();
  });

  it('refresh reloads every widget and clears action scores first', () => {
    const s = store();
    api.getPortfolioActions.mockClear();

    s.refresh();
    TestBed.tick();

    expect(api.getPortfolioActions).toHaveBeenCalledOnce();
    expect(api.getEodSummary).toHaveBeenCalledTimes(2);
    expect(api.getMarketLeadership).toHaveBeenCalledTimes(2);
    expect(api.getActionScores).toHaveBeenCalledOnce();
    expect(api.getPerformanceSummary).toHaveBeenCalledTimes(2);
    expect(s.actionScores()).toEqual([]);
  });

  it('reports a refresh failure separately from a snapshot failure', () => {
    api.refresh.mockReturnValue(throwError(() => new Error('boom')));
    const s = store();

    s.refresh();
    TestBed.tick();

    expect(s.error()).toBe('Dashboard refresh failed');
  });

  it('loads decision performance only once', () => {
    const s = store();

    s.loadDecisionPerformance();
    TestBed.tick();
    s.loadDecisionPerformance();
    TestBed.tick();

    expect(api.getDecisionPerformance).toHaveBeenCalledOnce();
    expect(s.decisionPerformance()).toEqual({ rows: [] });
    expect(s.performanceLoading()).toBe(false);
  });
});
