import { computed, effect, inject, untracked } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
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
import { UserPreferencesStore } from './user-preferences.store';

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

export type AuditSavedFilters = Pick<
  AuditState,
  'range' | 'customFrom' | 'customTo' | 'category' | 'action' | 'source' | 'userId' | 'q'
>;

const PREF_KEY = 'audit.filters';

const DEFAULT_FILTERS: AuditSavedFilters = {
  range: initialState.range,
  customFrom: null,
  customTo: null,
  category: [],
  action: [],
  source: null,
  userId: null,
  q: '',
};

const RANGES: AuditRange[] = ['today', '7d', '30d', 'all', 'custom'];

/** Keeps only well-formed saved values so a stale or corrupt preference can't break the page. */
function sanitize(saved: Partial<AuditSavedFilters> | null): Partial<AuditSavedFilters> {
  if (!saved || typeof saved !== 'object') return {};
  const out: Partial<AuditSavedFilters> = {};
  if (saved.range && RANGES.includes(saved.range)) out.range = saved.range;
  if (typeof saved.customFrom === 'string') out.customFrom = saved.customFrom;
  if (typeof saved.customTo === 'string') out.customTo = saved.customTo;
  if (Array.isArray(saved.category)) out.category = saved.category;
  if (Array.isArray(saved.action)) out.action = saved.action;
  if (typeof saved.source === 'string') out.source = saved.source;
  if (typeof saved.userId === 'string') out.userId = saved.userId;
  if (typeof saved.q === 'string') out.q = saved.q;
  return out;
}

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
    /** Number of non-default filters, shown in the collapsed mobile header. */
    activeFilterCount: computed(
      () =>
        (s.category().length > 0 ? 1 : 0) +
        (s.action().length > 0 ? 1 : 0) +
        (s.source() ? 1 : 0) +
        (s.userId() ? 1 : 0) +
        (s.q().trim() ? 1 : 0) +
        (s.range() !== '7d' ? 1 : 0),
    ),
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
  withMethods((store, api = inject(AuditApiService), snackBar = inject(MatSnackBar), userPrefs = inject(UserPreferencesStore)) => {
    const saveFilters = (): void => {
      const saved: AuditSavedFilters = {
        range: store.range(),
        customFrom: store.customFrom(),
        customTo: store.customTo(),
        category: store.category(),
        action: store.action(),
        source: store.source(),
        userId: store.userId(),
        q: store.q(),
      };
      userPrefs.set(PREF_KEY, saved);
    };

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
              error: () => patchState(store, setError('Failed to load audit')),
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
      if (changes.sortBy === undefined) saveFilters();
      load();
    };

    return {
      load,
      loadFilters,
      /** Deletes several entries (admin) and refetches; the server records the deletion itself. */
      deleteEntries(ids: number[], done: () => void): void {
        api.deleteMany(ids).subscribe({
          next: ({ deleted }) => {
            snackBar.open(`${deleted} audit entries deleted`, 'Close', { duration: 3000 });
            done();
            load();
          },
          error: () => snackBar.open('Failed to delete audit entries', 'Close', { duration: 4000 }),
        });
      },
      /** Downloads the current filter as CSV. */
      exportCsv(): void {
        api.exportCsv(params()).subscribe({
          next: (blob) => {
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `audit-${new Date().toISOString().slice(0, 10)}.csv`;
            a.click();
            URL.revokeObjectURL(url);
          },
          error: () => snackBar.open('Failed to export audit', 'Close', { duration: 4000 }),
        });
      },
      /** Deletes one entry (admin) and refetches; the server records the deletion itself. */
      deleteEntry(id: number): void {
        api.delete(id).subscribe({
          next: () => {
            snackBar.open('Audit entry deleted', 'Close', { duration: 3000 });
            load();
          },
          error: () => snackBar.open('Failed to delete audit entry', 'Close', { duration: 4000 }),
        });
      },
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
      const userPrefs = inject(UserPreferencesStore);
      // Restore the signed-in user's saved filters once their preferences have loaded; with none
      // saved the grid shows the defaults. Runs again for the next user after a logout/login.
      effect(() => {
        if (!userPrefs.isLoaded()) return;
        const saved = untracked(() => userPrefs.get<Partial<AuditSavedFilters>>(PREF_KEY));
        untracked(() => {
          patchState(store, { ...DEFAULT_FILTERS, ...sanitize(saved), page: 1 });
          store.load();
        });
      });
      onceAuthenticated(() => {
        store.loadFilters();
        store.load();
      });
    },
  }),
);
