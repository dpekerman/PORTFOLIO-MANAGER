import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuditApiService } from '../services/audit-api.service';
import { AuditStore, resolveRange } from './audit.store';
import { AuthStore } from './auth.store';

describe('AuditStore', () => {
  const isAuthenticated = signal(false);
  const api = { query: vi.fn(), getFilters: vi.fn() };
  const page = (total = 1) => ({ items: [{ id: 1 }], total, page: 1, pageSize: 25 });

  beforeEach(() => {
    isAuthenticated.set(false);
    vi.clearAllMocks();
    api.query.mockReturnValue(of(page()));
    api.getFilters.mockReturnValue(of({ categories: ['Stock'], actions: ['Create'], users: [] }));
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthStore, useValue: { isAuthenticated } },
        { provide: AuditApiService, useValue: api },
      ],
    });
  });

  function login() {
    const store = TestBed.inject(AuditStore);
    TestBed.tick();
    isAuthenticated.set(true);
    TestBed.tick();
    return store;
  }

  it('does not load before login, then loads items and filters', () => {
    const store = TestBed.inject(AuditStore);
    TestBed.tick();
    expect(api.query).not.toHaveBeenCalled();
    isAuthenticated.set(true);
    TestBed.tick();
    expect(store.total()).toBe(1);
    expect(store.filters().categories).toEqual(['Stock']);
  });

  it('resets to page 1 and refetches with the new filter', () => {
    const store = login();
    store.setPage(3, 50);
    store.setCategory(['Cash']);
    expect(store.page()).toBe(1);
    expect(api.query).toHaveBeenLastCalledWith(
      expect.objectContaining({ page: 1, pageSize: 50, category: ['Cash'] }),
    );
  });

  it('sends sort changes and reports active filters', () => {
    const store = login();
    expect(store.hasActiveFilters()).toBe(false);
    store.setSort('symbol', 'asc');
    expect(api.query).toHaveBeenLastCalledWith(
      expect.objectContaining({ sortBy: 'symbol', sortDir: 'asc' }),
    );
    store.setSearch('aapl');
    expect(store.hasActiveFilters()).toBe(true);
    store.resetFilters();
    expect(store.hasActiveFilters()).toBe(false);
  });

  it('surfaces a load error', () => {
    api.query.mockReturnValue(throwError(() => new Error('x')));
    const store = login();
    expect(store.error()).toBe('Failed to load audit log');
    expect(store.loading()).toBe(false);
  });

  it('resolves date ranges', () => {
    expect(resolveRange('all', null, null)).toEqual({ from: null, to: null });
    expect(resolveRange('today', null, null).from).not.toBeNull();
    const custom = resolveRange('custom', '2026-01-01', '2026-01-31');
    expect(new Date(custom.from!).getTime()).toBeLessThan(new Date(custom.to!).getTime());
  });
});
