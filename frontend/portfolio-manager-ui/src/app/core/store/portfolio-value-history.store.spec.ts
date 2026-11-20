import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { PortfolioValueHistoryDto } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { AuthStore } from './auth.store';
import { PortfolioValueHistoryStore } from './portfolio-value-history.store';

const snap = (recordedDate: string, snapshotStatus: string) =>
  ({ recordedDate, snapshotStatus }) as unknown as PortfolioValueHistoryDto;

describe('PortfolioValueHistoryStore', () => {
  const authenticated = signal(false);
  const api = {
    getPortfolioValueHistoryRange: vi.fn(),
    recordPortfolioValueNow: vi.fn(),
    reconcileCashToday: vi.fn(),
    recalculateCashFromDate: vi.fn(),
  };
  const snackBar = { open: vi.fn() };

  beforeEach(() => {
    authenticated.set(false);
    Object.values(api).forEach((fn) => fn.mockReset());
    snackBar.open.mockReset();
    api.getPortfolioValueHistoryRange.mockReturnValue(
      of([snap('2026-03-03', 'Final'), snap('2026-03-02', 'Draft'), snap('2026-03-01', 'Final')]),
    );
    TestBed.configureTestingModule({
      providers: [
        { provide: PortfolioApiService, useValue: api },
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AuthStore, useValue: { isAuthenticated: authenticated } },
      ],
    });
  });

  function store(): InstanceType<typeof PortfolioValueHistoryStore> {
    const s = TestBed.inject(PortfolioValueHistoryStore);
    TestBed.tick();
    authenticated.set(true);
    TestBed.tick();
    return s;
  }

  it('loads after authentication and exposes the most recent row first', () => {
    const s = store();
    expect(s.snapshots()).toHaveLength(3);
    expect(s.latestSnapshot()?.recordedDate).toBe('2026-03-03');
    expect(s.loading()).toBe(false);
  });

  it('filters client-side by date range and status, and resets', () => {
    const s = store();
    s.setDateRange({ from: '2026-03-02', to: null });
    expect(s.filteredSnapshots().map((x) => x.recordedDate)).toEqual(['2026-03-03', '2026-03-02']);

    s.setStatusFilter('Final' as never);
    expect(s.filteredSnapshots().map((x) => x.recordedDate)).toEqual(['2026-03-03']);

    s.resetFilters();
    expect(s.filteredSnapshots()).toHaveLength(3);
  });

  it('clears loading when the load fails', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    api.getPortfolioValueHistoryRange.mockReturnValue(throwError(() => new Error('x')));
    const s = store();
    expect(s.loading()).toBe(false);
  });

  it('refetches and notifies after a successful admin action', () => {
    api.recordPortfolioValueNow.mockReturnValue(of({}));
    const s = store();
    api.getPortfolioValueHistoryRange.mockClear();

    s.recordTodayNow();

    expect(snackBar.open).toHaveBeenCalledWith("Today's snapshot recorded", 'Dismiss', {
      duration: 3000,
    });
    expect(api.getPortfolioValueHistoryRange).toHaveBeenCalledTimes(1);
  });

  it('surfaces the server message when an admin action fails', () => {
    api.reconcileCashToday.mockReturnValue(throwError(() => ({ error: { message: 'Nope' } })));
    const s = store();

    s.reconcileToday();

    expect(s.actionError()).toBe('Nope');
    expect(snackBar.open).toHaveBeenCalledWith('Nope', 'Dismiss', { duration: 5000 });
  });

  it('falls back to a default message and clears it when the next action starts', () => {
    api.recalculateCashFromDate.mockReturnValueOnce(throwError(() => ({})));
    api.recalculateCashFromDate.mockReturnValueOnce(of([]));
    const s = store();

    s.recalculateCashFrom('2026-01-01');
    expect(s.actionError()).toBe('Failed to recalculate cash');

    s.recalculateCashFrom('2026-01-01');
    expect(s.actionError()).toBeNull();
  });
});
