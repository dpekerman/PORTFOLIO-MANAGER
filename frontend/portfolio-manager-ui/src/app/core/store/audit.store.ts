import { computed, inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap } from 'rxjs';
import {
  AuditAction,
  AuditCategory,
  AuditFilters,
  AuditLogEntry,
  AuditQueryParams,
  AuditSource,
} from '../models/portfolio.models';
import { AuditApiService } from '../services/audit-api.service';
import { onceAuthenticated } from './features/auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

export type AuditRange = 'today' | '7d' | '30d' | 'all' | 'custom';

interface AuditState {
  items: AuditLogEntry[];
  total: number;
  page: number;
  pageSize: number;
  sortBy: string;
  sortDir: 'asc' | 'desc';
  range: AuditRange;
  customFrom: string | null;
  customTo: string | null;
  category: AuditCategory[];
  action: AuditAction[];
  source: AuditSource | null;
  userId: string | null;
  q: string;
  filters: AuditFilters;
}

const initialState: AuditState = {
  items: [],
  total: 0,
  page: 1,
  pageSize: 25,
  sortBy: 'time',
  sortDir: 'desc',
  range: '7d',
  customFrom: null,
  customTo: null,
  category: [],
  action: [],
  source: null,
  userId: null,
  q: '',
  filters: { categories: [], actions: [], users: [] },
};

const startOfDayIso = (daysAgo: number): string => {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  d.setDate(d.getDate() - daysAgo);
  return d.toISOString();
};

/** Resolves the selected range into UTC bounds for the API. */
export function resolveRange(
  range: AuditRange,
  customFrom: string | null,
  customTo: string | null,
): { from: string | null; to: string | null } {
  switch (range) {
    case 'today':
      return { from: startOfDayIso(0), to: null };
    case '7d':
      return { from: startOfDayIso(6), to: null };
    case '30d':
      return { from: startOfDayIso(29), to: null };
    case 'custom': {
      const from = customFrom ? new Date(`${customFrom}T00:00:00`).toISOString() : null;
      const to = customTo ? new Date(`${customTo}T23:59:59.999`).toISOString() : null;
      return { from, to };
    }
    default:
      return { from: null, to: null };
  }
}

/**
 * Server-side filtered, sorted and paged audit trail. Every filter/sort/page change refetches;
 * a filter change resets to page 1.
 */
export const AuditStore = signalStore(
  { providedIn: 'root' },
  withState<AuditState>(initialState),
  withRequestStatus(),
  withComputed((s) => ({
    hasActiveFilters: computed(
      () =>
        s.category().length > 0 ||
        s.action().length > 0 ||
        s.source() !== null ||
        s.userId() !== null ||
        s.q().trim() !== '' ||
        s.range() !== '7d',
    ),
  })),
  withMethods((store, api = inject(AuditApiService)) => {
    const params = (): AuditQueryParams => ({
      page: store.page(),
      pageSize: store.pageSize(),
      sortBy: store.sortBy(),
      sortDir: store.sortDir(),
      ...resolveRange(store.range(), store.customFrom(), store.customTo()),
      category: store.category(),
      action: store.action(),
      source: store.source(),
      userId: store.userId(),
      q: store.q(),
    });

    const load = rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap(() =>
          api.query(params()).pipe(
            tapResponse({
              next: (r) => patchState(store, { items: r.items, total: r.total }, setLoaded()),
              error: () => patchState(store, setError('Failed to load audit log')),
            }),
          ),
        ),
      ),
    );

    const loadFilters = rxMethod<void>(
      pipe(
        switchMap(() =>
          api.getFilters().pipe(
            tapResponse({
              next: (filters) => patchState(store, { filters }),
              error: () => undefined,
            }),
          ),
        ),
      ),
    );

    /** Applies filter changes, returns to page 1 and refetches. */
    const applyFilter = (changes: Partial<AuditState>): void => {
      patchState(store, { ...changes, page: 1 });
      load();
    };

    return {
      load,
      loadFilters,
      setRange: (range: AuditRange, customFrom: string | null = null, customTo: string | null = null) =>
        applyFilter({ range, customFrom, customTo }),
      setCategory: (category: AuditCategory[]) => applyFilter({ category }),
      setAction: (action: AuditAction[]) => applyFilter({ action }),
      setSource: (source: AuditSource | null) => applyFilter({ source }),
      setUser: (userId: string | null) => applyFilter({ userId }),
      setSearch: (q: string) => applyFilter({ q }),
      setSort(sortBy: string, sortDir: 'asc' | 'desc'): void {
        applyFilter({ sortBy, sortDir });
      },
      setPage(page: number, pageSize: number): void {
        patchState(store, { page, pageSize });
        load();
      },
      resetFilters(): void {
        applyFilter({
          range: initialState.range,
          customFrom: null,
          customTo: null,
          category: [],
          action: [],
          source: null,
          userId: null,
          q: '',
        });
      },
    };
  }),
  withHooks({
    onInit(store) {
      onceAuthenticated(() => {
        store.loadFilters();
        store.load();
      });
    },
  }),
);
