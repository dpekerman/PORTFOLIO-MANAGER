import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { OpenDecision, OpenDecisionsResponse } from '../models/portfolio.models';
import { DashboardApiService } from '../services/dashboard-api.service';
import { AuthStore } from './auth.store';
import {
  ACTION_CENTER_FILTER_STORAGE_KEY,
  filterDecisions,
  OpenDecisionsStore,
  parseActionCenterFilter,
} from './open-decisions.store';

const decision = (
  id: number,
  symbol: string,
  context: OpenDecision['context'],
  bucket: OpenDecision['bucket'],
): OpenDecision => ({
  id,
  symbol,
  context,
  role: null,
  actionLabel: 'Action',
  actionId: 'ACTION',
  bucket,
  urgency: 'Normal',
  rationale: null,
  evidence: 'Signal Tracker / EOD',
  signalDate: '2026-10-23',
  lastValidatedAtUtc: '2026-10-23T21:00:00Z',
  isStale: false,
  status: 'Active',
  myAction: 'Pending',
  signalPrice: 10,
  origin: 'Live',
});

const response: OpenDecisionsResponse = {
  summary: { entryAdd: 2, trimExit: 1, riskReview: 1, totalOpen: 4, asOfUtc: '2026-10-26T13:00:00Z', lastValidatedAtUtc: null },
  validation: { state: 'Validated', lastCompleteEvaluationUtc: '2026-10-23T21:00:00Z', lastAttemptUtc: '2026-10-23T21:00:00Z', pendingCount: 0 },
  items: [
    decision(1, 'RSK', 'Holding', 'RiskReview'),
    decision(2, 'TRM', 'Holding', 'TrimExit'),
    decision(3, 'ADD', 'Holding', 'EntryAdd'),
    decision(4, 'ENT', 'Watchlist', 'EntryAdd'),
  ],
};

describe('parseActionCenterFilter', () => {
  it('accepts the current filters and maps anything else (including the retired priority filters) to All', () => {
    for (const f of ['All', 'Entry', 'ReduceExit', 'Risk', 'Holdings', 'Watchlist']) {
      expect(parseActionCenterFilter(f)).toBe(f);
    }
    for (const legacy of ['REQUIRED', 'DEVELOPING', 'INFORMATIONAL', 'ALL', '', null]) {
      expect(parseActionCenterFilter(legacy)).toBe('All');
    }
  });
});

describe('filterDecisions', () => {
  it('narrows the list without changing the underlying set', () => {
    expect(filterDecisions(response.items, 'All')).toHaveLength(4);
    expect(filterDecisions(response.items, 'Entry').map((d) => d.symbol)).toEqual(['ADD', 'ENT']);
    expect(filterDecisions(response.items, 'ReduceExit').map((d) => d.symbol)).toEqual(['TRM']);
    expect(filterDecisions(response.items, 'Risk').map((d) => d.symbol)).toEqual(['RSK']);
    expect(filterDecisions(response.items, 'Holdings')).toHaveLength(3);
    expect(filterDecisions(response.items, 'Watchlist').map((d) => d.symbol)).toEqual(['ENT']);
  });
});

describe('OpenDecisionsStore', () => {
  const api = { getActionCenter: vi.fn() };

  beforeEach(() => {
    localStorage.clear();
    api.getActionCenter.mockReset().mockReturnValue(of(response));
    TestBed.configureTestingModule({
      providers: [
        { provide: DashboardApiService, useValue: api },
        { provide: AuthStore, useValue: { isAuthenticated: signal(true) } },
      ],
    });
  });

  function store(): InstanceType<typeof OpenDecisionsStore> {
    const s = TestBed.inject(OpenDecisionsStore);
    TestBed.tick();
    return s;
  }

  it('loads once authenticated and exposes the summary and list from the same response', () => {
    const s = store();
    expect(api.getActionCenter).toHaveBeenCalledOnce();
    expect(s.summary()?.totalOpen).toBe(4);
    // The summary total always equals the unfiltered Action Center list.
    expect(s.allItems().length).toBe(s.summary()!.totalOpen);
  });

  it('filtering narrows the list but never the summary counts', () => {
    const s = store();
    s.setFilter('Risk');
    expect(s.items().map((d) => d.symbol)).toEqual(['RSK']);
    expect(s.summary()?.totalOpen).toBe(4);
    expect(localStorage.getItem(ACTION_CENTER_FILTER_STORAGE_KEY)).toBe('Risk');
  });

  it('clicking the active filter again clears it', () => {
    const s = store();
    s.toggleFilter('Entry');
    expect(s.filter()).toBe('Entry');
    s.toggleFilter('Entry');
    expect(s.filter()).toBe('All');
  });

  it('starts from a legacy stored filter as All', () => {
    localStorage.setItem(ACTION_CENTER_FILTER_STORAGE_KEY, 'REQUIRED');
    expect(store().filter()).toBe('All');
  });

  it('keeps the previous decisions on screen when a reload fails', () => {
    const s = store();
    api.getActionCenter.mockReturnValue(throwError(() => new Error('boom')));
    s.load();
    expect(s.error()).toBe('Could not load current decisions');
    expect(s.allItems()).toHaveLength(4);
  });

  it('shows zero counts for an empty response and keeps the validation state', () => {
    api.getActionCenter.mockReturnValue(
      of({
        summary: { entryAdd: 0, trimExit: 0, riskReview: 0, totalOpen: 0, asOfUtc: '', lastValidatedAtUtc: null },
        validation: { state: 'Pending', lastCompleteEvaluationUtc: null, lastAttemptUtc: null, pendingCount: 6 },
        items: [],
      }),
    );
    const s = store();
    expect(s.summary()?.totalOpen).toBe(0);
    expect(s.items()).toEqual([]);
    // Zero decisions with nothing validated is "pending", never an all-clear.
    expect(s.validation()?.state).toBe('Pending');
    expect(s.validation()?.pendingCount).toBe(6);
  });

  it('exposes the validation state from the same response', () => {
    expect(store().validation()?.state).toBe('Validated');
  });
});
