import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CreateMarketLeadershipTrackerRequest } from '../models/portfolio.models';
import { DashboardStateService } from '../services/dashboard-state.service';
import { MarketLeadershipApiService } from '../services/market-leadership-api.service';
import { MarketLeadershipStore } from './market-leadership.store';

describe('MarketLeadershipStore', () => {
  const request = {} as CreateMarketLeadershipTrackerRequest;
  const api = { addTracker: vi.fn(), updateTracker: vi.fn(), removeTracker: vi.fn() };
  const dashboard = { loadMarketLeadership: vi.fn() };

  beforeEach(() => {
    vi.clearAllMocks();
    api.addTracker.mockReturnValue(of(undefined));
    api.updateTracker.mockReturnValue(of(undefined));
    api.removeTracker.mockReturnValue(of(undefined));
    TestBed.configureTestingModule({
      providers: [
        { provide: MarketLeadershipApiService, useValue: api },
        { provide: DashboardStateService, useValue: dashboard },
      ],
    });
  });

  it('add/update succeed, reload the dashboard and clear saving', async () => {
    const store = TestBed.inject(MarketLeadershipStore);
    expect(await store.addTracker(request)).toBe(true);
    expect(await store.updateTracker(3, request)).toBe(true);
    expect(dashboard.loadMarketLeadership).toHaveBeenCalledTimes(2);
    expect(store.saving()).toBe(false);
    expect(store.error()).toBeNull();
  });

  it('maps server messages and the stale-backend statuses', async () => {
    const store = TestBed.inject(MarketLeadershipStore);
    api.addTracker.mockReturnValue(throwError(() => ({ error: { message: 'Duplicate' } })));
    expect(await store.addTracker(request)).toBe(false);
    expect(store.error()).toBe('Duplicate');
    api.updateTracker.mockReturnValue(throwError(() => ({ status: 404 })));
    expect(await store.updateTracker(1, request)).toBe(false);
    expect(store.error()).toBe('The backend must be restarted before tracker changes can be saved.');
    expect(dashboard.loadMarketLeadership).not.toHaveBeenCalled();
    expect(store.saving()).toBe(false);
  });

  it('removeTracker reloads on success and reports failure', () => {
    const store = TestBed.inject(MarketLeadershipStore);
    store.removeTracker(1);
    expect(dashboard.loadMarketLeadership).toHaveBeenCalledOnce();
    api.removeTracker.mockReturnValue(throwError(() => ({})));
    store.removeTracker(2);
    expect(store.error()).toBe('Unable to update market trackers.');
    expect(store.saving()).toBe(false);
  });
});
