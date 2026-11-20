import { computed, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { firstValueFrom, forkJoin, pipe, switchMap, tap } from 'rxjs';
import {
  EffectivenessWindow,
  SignalAuditDetail,
  SignalAuditRow,
  SignalAuditSummary,
  SignalEffectivenessResult,
  SignalMyAction,
  SignalsAuditQueryParams,
  SignalsAuditSettings,
  AuditedActionEntry,
  FinalActionCatalogItem,
  SignalTransactionCandidate,
  SignalTypeOption,
} from '../models/portfolio.models';
import { SignalsAuditApiService } from '../services/signals-audit-api.service';
import { onceAuthenticated } from './features/auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

export type SignalsAuditFilters = Pick<
  SignalsAuditQueryParams,
  'ticker' | 'signalType' | 'source' | 'role' | 'myAction' | 'origin' | 'includeBaselineInStats' | 'includeUnaudited' | 'status' | 'from' | 'to'
>;

/** The only table filters that also shape the effectiveness view (documented on the page). */
const EFFECTIVENESS_FILTER_KEYS: (keyof SignalsAuditFilters)[] = ['signalType', 'source', 'role', 'includeBaselineInStats'];

export const DEFAULT_EFFECTIVENESS_WINDOW: EffectivenessWindow = '90';

interface SignalsAuditState extends SignalsAuditFilters {
  items: SignalAuditRow[];
  total: number;
  page: number;
  pageSize: number;
  sortBy: string;
  sortDir: 'asc' | 'desc';
  summary: SignalAuditSummary | null;
  /** Same filters minus signalType/myAction, so the KPI cards stay stable while a card filter is active. */
  overview: SignalAuditSummary | null;
  /** Rolling-cohort effectiveness: independent of the table's ticker, date, status and My Action filters. */
  effectiveness: SignalEffectivenessResult | null;
  effectivenessWindow: EffectivenessWindow;
  effectivenessLoading: boolean;
  effectivenessError: string | null;
  signalTypes: SignalTypeOption[];
  settings: SignalsAuditSettings | null;
  finalActionCatalog: FinalActionCatalogItem[];
  settingsSaving: boolean;
  detail: SignalAuditDetail | null;
  detailLoading: boolean;
  candidates: SignalTransactionCandidate[];
  candidatesLoading: boolean;
  /**
   * True when the current page was ranked by a computed sort (returns, signal type) over only the newest
   * `evaluated` of `total` rows. Always false for SQL-backed sorts, which page the complete filtered set.
   */
  gridPartial: boolean;
  gridEvaluated: number;
  evaluationLimit: number;
}

/** Sort keys the server pages in SQL over the complete filtered set; every other key is a bounded computed sort. */
export const SQL_SORT_KEYS = ['signalDate', 'daysSince', 'symbol', 'role', 'source', 'signalPrice', 'myAction', 'lifecycleStatus'] as const;

export const DEFAULT_SIGNAL_FILTERS: SignalsAuditFilters = {
  ticker: '',
  signalType: [],
  source: 'All',
  role: [],
  myAction: 'All',
  origin: 'All',
  includeBaselineInStats: false,
  includeUnaudited: false,
  status: 'Active',
  from: '',
  to: '',
};

const initialState: SignalsAuditState = {
  ...DEFAULT_SIGNAL_FILTERS,
  items: [],
  total: 0,
  page: 1,
  pageSize: 25,
  sortBy: 'signalDate',
  sortDir: 'desc',
  summary: null,
  overview: null,
  effectiveness: null,
  effectivenessWindow: DEFAULT_EFFECTIVENESS_WINDOW,
  effectivenessLoading: false,
  effectivenessError: null,
  signalTypes: [],
  settings: null,
  finalActionCatalog: [],
  settingsSaving: false,
  detail: null,
  detailLoading: false,
  candidates: [],
  candidatesLoading: false,
  gridPartial: false,
  gridEvaluated: 0,
  evaluationLimit: 5000,
};

/**
 * Signals Audit: server-side filtered/sorted/paged history of recommendations, the matching
 * summary and effectiveness figures, capture settings, and the investor-decision workflow.
 * State is patched only here; components read signals and call methods.
 */
export const SignalsAuditStore = signalStore(
  { providedIn: 'root' },
  withState<SignalsAuditState>(initialState),
  withRequestStatus(),
  withComputed((s) => ({
    hasActiveFilters: computed(
      () =>
        s.ticker().trim() !== '' ||
        s.signalType().length > 0 ||
        s.source() !== 'All' ||
        s.role().length > 0 ||
        s.myAction() !== 'All' ||
        s.origin() !== 'All' ||
        s.includeBaselineInStats() ||
        s.includeUnaudited() ||
        s.status() !== DEFAULT_SIGNAL_FILTERS.status ||
        s.from() !== '' ||
        s.to() !== '',
    ),
  })),
  withMethods((store, api = inject(SignalsAuditApiService), snackBar = inject(MatSnackBar)) => {
    const notify = (message: string, duration = 3500) => snackBar.open(message, 'Close', { duration });

    const params = (): SignalsAuditQueryParams => ({
      ticker: store.ticker(),
      signalType: store.signalType(),
      source: store.source(),
      role: store.role(),
      myAction: store.myAction(),
      origin: store.origin(),
      includeBaselineInStats: store.includeBaselineInStats(),
      includeUnaudited: store.includeUnaudited(),
      status: store.status(),
      from: store.from(),
      to: store.to(),
      sortBy: store.sortBy(),
      sortDir: store.sortDir(),
      page: store.page(),
      pageSize: store.pageSize(),
    });

    /** Fetches the page and its filter-matching summary together so cards and table never disagree. */
    const load = rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap(() =>
          forkJoin({
            page: api.query(params()),
            summary: api.summary(params()),
            // The cards ignore the card-driven filters (type, My Action, status) so they stay put while you click them.
            overview: api.summary({ ...params(), signalType: [], myAction: 'All', status: 'All' }),
          }).pipe(
            tapResponse({
              next: ({ page, summary, overview }) =>
                patchState(
                  store,
                  {
                    items: page.items,
                    total: page.total,
                    summary,
                    overview,
                    gridPartial: page.partial ?? false,
                    gridEvaluated: page.evaluated ?? page.total,
                    evaluationLimit: page.evaluationLimit ?? 5000,
                  },
                  setLoaded(),
                ),
              error: () => patchState(store, setError('Failed to load signals')),
            }),
          ),
        ),
      ),
    );

    const loadSignalTypes = rxMethod<void>(
      pipe(
        switchMap(() =>
          api.getSignalTypes().pipe(
            tapResponse({
              next: (signalTypes) => patchState(store, { signalTypes }),
              error: () => undefined,
            }),
          ),
        ),
      ),
    );

    const loadSettings = rxMethod<void>(
      pipe(
        switchMap(() =>
          api.getSettings().pipe(
            tapResponse({
              next: (settings) => patchState(store, { settings }),
              error: () => notify('Failed to load Signals Audit settings', 4000),
            }),
          ),
        ),
      ),
    );

    const loadEffectiveness = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { effectivenessLoading: true, effectivenessError: null })),
        switchMap(() =>
          api
            .effectiveness({
              window: store.effectivenessWindow(),
              signalType: store.signalType(),
              source: store.source(),
              role: store.role(),
              includeBaselineInStats: store.includeBaselineInStats(),
            })
            .pipe(
              tapResponse({
                next: (effectiveness) => patchState(store, { effectiveness, effectivenessLoading: false }),
                error: () =>
                  patchState(store, { effectivenessLoading: false, effectivenessError: 'Failed to load signal effectiveness' }),
              }),
            ),
        ),
      ),
    );

    const contextKey = (): string =>
      JSON.stringify(EFFECTIVENESS_FILTER_KEYS.map((k) => store[k]()));

    return {
      load,
      loadEffectiveness,
      loadSignalTypes,
      loadSettings,

      setFilters(changes: Partial<SignalsAuditFilters>): void {
        const before = contextKey();
        patchState(store, { ...changes, page: 1 });
        load();
        // Only the documented context filters (type, role, source, baseline) reach effectiveness.
        if (contextKey() !== before) loadEffectiveness();
      },

      resetFilters(): void {
        const before = contextKey();
        patchState(store, { ...DEFAULT_SIGNAL_FILTERS, page: 1 });
        load();
        if (contextKey() !== before) loadEffectiveness();
      },

      setEffectivenessWindow(effectivenessWindow: EffectivenessWindow): void {
        if (effectivenessWindow === store.effectivenessWindow()) return;
        patchState(store, { effectivenessWindow });
        loadEffectiveness();
      },

      /**
       * Landing view: every Active recommendation (the default status), newest first, with NO implicit date window.
       * Dates are only applied when the user chooses them.
       */
      loadInitial(): void {
        load();
      },

      setSort(sortBy: string, sortDir: 'asc' | 'desc'): void {
        patchState(store, { sortBy, sortDir, page: 1 });
        load();
      },

      setPage(page: number, pageSize: number): void {
        patchState(store, { page, pageSize });
        load();
      },

      /** Persists capture settings. Resolves true on success so the caller can clear its dirty state. */
      async saveSettings(
        priorityEnabled: boolean,
        priorityThreshold: number,
        finalActionEnabled?: boolean,
        entries?: AuditedActionEntry[],
      ): Promise<boolean> {
        patchState(store, { settingsSaving: true });
        try {
          const settings = await firstValueFrom(
            api.saveSettings(priorityEnabled, priorityThreshold, finalActionEnabled, entries),
          );
          patchState(store, { settings, settingsSaving: false });
          return true;
        } catch (err) {
          patchState(store, { settingsSaving: false });
          const e = err as { error?: { title?: string; detail?: string; message?: string } } | null;
          notify(e?.error?.detail ?? e?.error?.message ?? e?.error?.title ?? 'Failed to save Signals Audit settings', 4500);
          return false;
        }
      },

      /**
       * Every row matching the current filters and sort (not just the visible page), for export.
       * `partial` is true when the server could only rank the newest `evaluated` of `total` rows (computed sort);
       * such a result must never be presented as a complete export.
       */
      async fetchAllRows(): Promise<{ rows: SignalAuditRow[]; total: number; partial: boolean; evaluated: number }> {
        const rows: SignalAuditRow[] = [];
        const pageSize = 200;
        let total = 0;
        let partial = false;
        let evaluated = 0;
        for (let page = 1; ; page++) {
          const res = await firstValueFrom(api.query({ ...params(), page, pageSize }));
          total = res.total;
          partial = res.partial ?? false;
          evaluated = res.evaluated ?? res.total;
          rows.push(...res.items);
          if (rows.length >= res.total || res.items.length === 0) break;
        }
        return { rows, total, partial: partial || rows.length < total, evaluated };
      },

      loadFinalActionCatalog(): void {
        if (store.finalActionCatalog().length > 0) return;
        api.getFinalActionCatalog().subscribe({
          next: (finalActionCatalog) => patchState(store, { finalActionCatalog }),
          error: () => notify('Failed to load the Final Action list', 4000),
        });
      },

      openDetail(id: number): void {
        patchState(store, { detail: null, detailLoading: true });
        api.detail(id).subscribe({
          next: (detail) => patchState(store, { detail, detailLoading: false }),
          error: () => {
            patchState(store, { detailLoading: false });
            notify('Failed to load signal details', 4000);
          },
        });
      },

      clearDetail(): void {
        patchState(store, { detail: null });
      },

      loadCandidates(id: number): void {
        patchState(store, { candidates: [], candidatesLoading: true });
        api.transactionCandidates(id).subscribe({
          next: (candidates) => patchState(store, { candidates, candidatesLoading: false }),
          error: () => {
            patchState(store, { candidatesLoading: false });
            notify('Failed to search for matching transactions', 4000);
          },
        });
      },

      /** Saves the investor decision; updates the row and refreshes the summary on success. */
      async saveAction(
        id: number,
        myAction: SignalMyAction,
        note: string | null,
        linkedTransactionId: number | null,
      ): Promise<boolean> {
        try {
          const row = await firstValueFrom(api.saveAction(id, myAction, note, linkedTransactionId));
          patchState(store, ({ items }) => ({ items: items.map((r) => (r.id === id ? row : r)) }));
          load();
          notify(`${row.symbol}: ${row.myAction}`);
          return true;
        } catch (err) {
          const title = (err as { error?: { title?: string } } | null)?.error?.title;
          notify(title ?? 'Failed to save action', 4500);
          return false;
        }
      },

      /** Explicit dismissal of an Active recommendation. Independent of the My Action response. */
      async dismiss(id: number): Promise<boolean> {
        try {
          const row = await firstValueFrom(api.dismiss(id, null));
          patchState(store, ({ items }) => ({ items: items.map((r) => (r.id === id ? row : r)) }));
          load();
          notify(`${row.symbol}: dismissed`);
          return true;
        } catch (err) {
          const title = (err as { error?: { title?: string } } | null)?.error?.title;
          notify(title ?? 'Failed to dismiss', 4500);
          return false;
        }
      },
    };
  }),
  withHooks({
    onInit(store) {
      // Load after auth: loading earlier races the token refresh and 401s.
      onceAuthenticated(() => {
        store.loadSignalTypes();
        store.loadSettings();
      });
    },
  }),
);
