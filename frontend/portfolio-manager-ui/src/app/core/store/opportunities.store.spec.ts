import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { OpportunityDetail, OpportunitySnapshot } from '../models/opportunities.models';
import { OpportunitiesApiService } from '../services/opportunities-api.service';
import { AuthStore } from './auth.store';
import {
  OPPORTUNITIES_VIEW_KEY,
  OpportunitiesStore,
  REFRESH_POLL_MS,
  normaliseView,
  normaliseViewPreferences,
} from './opportunities.store';
import { UserPreferencesStore } from './user-preferences.store';
import {
  opportunityDetail,
  opportunityRow,
  opportunitySnapshot,
} from '../../features/trading-opportunities/testing/opportunity-fixtures';

describe('OpportunitiesStore', () => {
  const authenticated = signal(false);
  const dbView = signal<unknown>(null);
  const api = {
    getLatest: vi.fn(),
    getDetail: vi.fn(),
    refresh: vi.fn(),
    getConfig: vi.fn(),
  };
  const snackBar = { open: vi.fn() };
  const userPrefs = { get: vi.fn(() => dbView()), set: vi.fn(), remove: vi.fn() };

  const fresh = opportunitySnapshot([opportunityRow('AAA.TO'), opportunityRow('BBB.TO')]);

  beforeEach(() => {
    localStorage.clear();
    authenticated.set(false);
    dbView.set(null);
    Object.values(api).forEach((fn) => fn.mockReset());
    snackBar.open.mockReset();
    userPrefs.set.mockReset();
    api.getLatest.mockReturnValue(of(fresh));
    api.getConfig.mockReturnValue(of({ version: 'pm029-v1', attractiveMin: 71, moderateMin: 52 }));
    TestBed.configureTestingModule({
      providers: [
        { provide: OpportunitiesApiService, useValue: api },
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AuthStore, useValue: { isAuthenticated: authenticated } },
        { provide: UserPreferencesStore, useValue: userPrefs },
      ],
    });
  });

  afterEach(() => vi.useRealTimers());

  function store(): InstanceType<typeof OpportunitiesStore> {
    const s = TestBed.inject(OpportunitiesStore);
    TestBed.tick();
    return s;
  }

  function loaded(): InstanceType<typeof OpportunitiesStore> {
    const s = store();
    authenticated.set(true);
    TestBed.tick();
    return s;
  }

  it('loads the snapshot and config only after authentication, without refreshing a current snapshot', () => {
    const s = store();
    expect(api.getLatest).not.toHaveBeenCalled();

    authenticated.set(true);
    TestBed.tick();

    expect(api.getLatest).toHaveBeenCalledTimes(1);
    expect(api.getConfig).toHaveBeenCalledTimes(1);
    expect(api.refresh).not.toHaveBeenCalled();
    expect(s.rows().map((r) => r.symbol)).toEqual(['AAA.TO', 'BBB.TO']);
    expect(s.config()?.attractiveMin).toBe(71);
    expect(s.loading()).toBe(false);
  });

  it('auto-refreshes at most once when the stored snapshot misses a completed session', () => {
    const stale = opportunitySnapshot([opportunityRow('AAA.TO')], { isStale: true });
    api.getLatest.mockReturnValue(of(stale));
    api.refresh.mockReturnValue(of({ ...stale, snapshotId: 12 }));
    const s = loaded();

    expect(api.refresh).toHaveBeenCalledTimes(1);
    expect(s.snapshot()?.snapshotId).toBe(12);

    s.load();
    expect(api.refresh).toHaveBeenCalledTimes(1);
  });

  it('auto-refreshes when no snapshot exists yet, but not while another refresh is in flight', () => {
    api.getLatest.mockReturnValue(
      of(opportunitySnapshot([], { snapshotId: null, refreshInFlight: true })),
    );
    loaded();
    expect(api.refresh).not.toHaveBeenCalled();
  });

  it('reports a load failure', () => {
    api.getLatest.mockReturnValue(throwError(() => new Error('boom')));
    const s = loaded();
    expect(s.error()).toBe('Failed to load trading opportunities');
    expect(s.loading()).toBe(false);
  });

  it('requests the detail from the snapshot the grid shows and discards a late response for another row', () => {
    const s = loaded();
    const first = new Subject<OpportunityDetail>();
    const second = new Subject<OpportunityDetail>();
    api.getDetail.mockReturnValueOnce(first).mockReturnValueOnce(second);

    s.select('AAA.TO');
    s.select('BBB.TO');
    expect(api.getDetail).toHaveBeenNthCalledWith(1, 'AAA.TO', 11);
    expect(api.getDetail).toHaveBeenNthCalledWith(2, 'BBB.TO', 11);

    first.next(opportunityDetail(fresh.rows[0]));
    expect(s.detail()).toBeNull();

    second.next(opportunityDetail(fresh.rows[1]));
    expect(s.detail()?.row.symbol).toBe('BBB.TO');
    expect(s.selectedRow()?.symbol).toBe('BBB.TO');
  });

  it('reloads the whole view when the snapshot behind the details panel has expired (410)', () => {
    const s = loaded();
    const newer: OpportunitySnapshot = { ...fresh, snapshotId: 15 };
    api.getDetail
      .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 410 })))
      .mockReturnValueOnce(of(opportunityDetail(fresh.rows[0], 15)));
    api.getLatest.mockReturnValue(of(newer));

    s.select('AAA.TO');

    expect(api.getLatest).toHaveBeenCalledTimes(2);
    expect(api.getDetail).toHaveBeenLastCalledWith('AAA.TO', 15);
    expect(s.detail()?.snapshotId).toBe(15);
    expect(s.snapshot()?.snapshotId).toBe(15);
  });

  it('keeps the last snapshot when a refresh fails', () => {
    const s = loaded();
    api.refresh.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    s.refresh();

    expect(s.snapshot()?.snapshotId).toBe(11);
    expect(s.refreshing()).toBe(false);
    expect(snackBar.open).toHaveBeenCalledWith(
      expect.stringContaining('last successful snapshot'),
      'Close',
      expect.anything(),
    );
  });

  it('polls instead of starting a second refresh when one is already running (409)', () => {
    vi.useFakeTimers();
    const s = loaded();
    api.refresh.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409 })));

    s.refresh();
    expect(snackBar.open).toHaveBeenCalledWith(
      expect.stringContaining('already running'),
      'Close',
      expect.anything(),
    );
    expect(api.getLatest).toHaveBeenCalledTimes(1);

    vi.advanceTimersByTime(REFRESH_POLL_MS);
    expect(api.getLatest).toHaveBeenCalledTimes(2);
    s.stopPolling();
  });

  it('persists view changes locally and to the DB, and reset keeps the perspective and sort', () => {
    const s = loaded();
    expect(s.view().sort.active).toBe('swing');
    s.updateView({ perspective: 'Strategic', scoreFilter: 'all', sectors: ['Energy'] });
    expect(s.view().sort.active).toBe('strategic');

    expect(JSON.parse(localStorage.getItem(OPPORTUNITIES_VIEW_KEY)!).perspective).toBe('Strategic');
    expect(
      JSON.parse(localStorage.getItem(OPPORTUNITIES_VIEW_KEY)!).profiles.Strategic.scoreFilter,
    ).toBe('all');
    expect(userPrefs.set).toHaveBeenCalledWith(
      OPPORTUNITIES_VIEW_KEY,
      expect.objectContaining({
        profiles: expect.objectContaining({
          Strategic: expect.objectContaining({ scoreFilter: 'all' }),
        }),
      }),
    );

    s.setSearch('aaa');
    s.resetFilters();
    expect(s.view().perspective).toBe('Strategic');
    expect(s.view().scoreFilter).toBe('attractive');
    expect(s.view().sectors).toEqual([]);
    expect(s.search()).toBe('');
  });

  it('keeps filters and sort independent per perspective and resets only the active profile', () => {
    const s = loaded();
    s.updateView({ scoreFilter: 'scored', sort: { active: 'symbol', direction: 'asc' } });
    s.updateView({ perspective: 'Strategic', scoreFilter: 'all', sectors: ['Energy'] });
    s.updateView({ perspective: 'Swing' });

    expect(s.view().scoreFilter).toBe('scored');
    expect(s.view().sort).toEqual({ active: 'symbol', direction: 'asc' });

    s.updateView({ perspective: 'Strategic' });
    expect(s.view().scoreFilter).toBe('all');
    expect(s.view().sectors).toEqual(['Energy']);
    s.resetFilters();
    s.updateView({ perspective: 'Swing' });

    expect(s.view().scoreFilter).toBe('scored');
    expect(s.view().sort).toEqual({ active: 'symbol', direction: 'asc' });
  });

  it('applies the DB copy of the view once it loads', () => {
    const s = loaded();
    dbView.set({ perspective: 'Strategic', showUnavailable: true });
    TestBed.tick();
    expect(s.view().perspective).toBe('Strategic');
    expect(s.view().showUnavailable).toBe(true);
    expect(s.view().sort.active).toBe('strategic');
  });

  it('normalises a malformed saved view to defaults', () => {
    expect(normaliseView(null).scoreFilter).toBe('attractive');
    expect(normaliseView({ verifiedOnly: true }).sort.direction).toBe('desc');
    expect(normaliseView(null, 'Strategic').sort.active).toBe('strategic');
  });

  it('does not leak a legacy explicit Swing sort into the new Strategic profile', () => {
    const migrated = normaliseViewPreferences({
      perspective: 'Swing',
      sort: { active: 'symbol', direction: 'asc' },
    });
    expect(migrated.profiles.Swing.sort).toEqual({ active: 'symbol', direction: 'asc' });
    expect(migrated.profiles.Strategic.sort).toEqual({ active: 'strategic', direction: 'desc' });
  });

  it('restores only the active perspective default sort and preserves other view preferences', () => {
    const s = loaded();
    s.updateView({ perspective: 'Swing', sort: { active: 'symbol', direction: 'asc' } });
    s.updateView({ perspective: 'Strategic', scoreFilter: 'all', sectors: ['Energy'], sort: { active: 'price', direction: 'asc' } });

    s.restoreDefaultSort();

    expect(s.view().sort).toEqual({ active: 'strategic', direction: 'desc' });
    expect(s.view().scoreFilter).toBe('all');
    expect(s.view().sectors).toEqual(['Energy']);
    expect(s.viewProfiles().Swing.sort).toEqual({ active: 'symbol', direction: 'asc' });
  });

  it('migrates a legacy view to both perspective profiles and preserves explicit profile differences', () => {
    const legacy = normaliseViewPreferences({
      perspective: 'Strategic',
      scoreFilter: 'scored',
      sectors: ['Tech'],
    });
    expect(legacy.perspective).toBe('Strategic');
    expect(legacy.profiles.Swing.scoreFilter).toBe('scored');
    expect(legacy.profiles.Swing.sort.active).toBe('swing');
    expect(legacy.profiles.Strategic.sectors).toEqual(['Tech']);
    expect(legacy.profiles.Strategic.sort.active).toBe('strategic');

    const profiled = normaliseViewPreferences({
      perspective: 'Swing',
      profiles: {
        Swing: { scoreFilter: 'attractive' },
        Strategic: { scoreFilter: 'all', industries: ['Banks'] },
      },
    });
    expect(profiled.profiles.Swing.scoreFilter).toBe('attractive');
    expect(profiled.profiles.Strategic.scoreFilter).toBe('all');
    expect(profiled.profiles.Strategic.industries).toEqual(['Banks']);
  });
});
