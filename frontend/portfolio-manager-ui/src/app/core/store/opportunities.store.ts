import { HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, untracked } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { tapResponse } from '@ngrx/operators';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { EMPTY, catchError, exhaustMap, filter, pipe, switchMap, tap } from 'rxjs';
import {
  FitStatus,
  OpportunityConfig,
  OpportunityDetail,
  OpportunitySnapshot,
  RiskCategory,
  ScorePerspective,
  SectorHeatDirection,
  SetupQualityStatus,
} from '../models/opportunities.models';
import { OpportunitiesApiService } from '../services/opportunities-api.service';
import { onceAuthenticated } from './features/auth-gate';
import {
  setError,
  setLoaded,
  setLoading,
  withRequestStatus,
} from './features/request-status.feature';
import { UserPreferencesStore } from './user-preferences.store';

export const OPPORTUNITIES_VIEW_KEY = 'pm_opportunities_view_v1';

/** Poll interval while another tab/device is refreshing (the server reports `refreshInFlight`). */
export const REFRESH_POLL_MS = 5000;
const MAX_POLLS = 120;

/**
 * Score filter (C2). Thresholds are never literals here: `attractive`/`moderate` use the published config
 * (`attractiveMin`/`moderateMin`), `scored` keeps every row with a score, `all` also keeps unscorable rows.
 */
export type ScoreFilter = 'strong' | 'attractive' | 'moderate' | 'scored' | 'all';
export type OwnershipFilter = 'all' | 'owned' | 'notOwned';
export type FreshnessFilter = 'all' | 'current' | 'stale';
export type SortDirection = 'asc' | 'desc' | '';

export interface OpportunitySort {
  active: string;
  direction: SortDirection;
}

/** The persisted (per-user) view: perspective, filters and sort. Free-text search and the reason chip are transient. */
export interface OpportunityView {
  perspective: ScorePerspective;
  scoreFilter: ScoreFilter;
  showUnavailable: boolean;
  verifiedOnly: boolean;
  sectors: string[];
  industries: string[];
  riskCategories: RiskCategory[];
  accounts: string[];
  roles: string[];
  freshness: FreshnessFilter;
  fit: FitStatus[];
  finalActionLabels: string[];
  ownership: OwnershipFilter;
  sources: string[];
  heat: SectorHeatDirection[];
  setupQuality: SetupQualityStatus[];
  sort: OpportunitySort;
}

export interface OpportunityViewPreferences {
  perspective: ScorePerspective;
  profiles: Record<ScorePerspective, OpportunityView>;
}

export const DEFAULT_OPPORTUNITY_VIEW: OpportunityView = {
  perspective: 'Swing',
  scoreFilter: 'attractive',
  showUnavailable: false,
  verifiedOnly: false,
  sectors: [],
  industries: [],
  riskCategories: [],
  accounts: [],
  roles: [],
  freshness: 'all',
  fit: [],
  finalActionLabels: [],
  ownership: 'all',
  sources: [],
  heat: [],
  setupQuality: [],
  sort: { active: 'swing', direction: 'desc' },
};

function defaultOpportunityView(perspective: ScorePerspective): OpportunityView {
  return {
    ...DEFAULT_OPPORTUNITY_VIEW,
    perspective,
    sort: { active: perspective === 'Swing' ? 'swing' : 'strategic', direction: 'desc' },
  };
}

interface OpportunitiesState {
  snapshot: OpportunitySnapshot | null;
  config: OpportunityConfig | null;
  refreshing: boolean;
  /** At most one automatic refresh per app session when the stored snapshot misses a completed session (U4). */
  autoRefreshAttempted: boolean;
  view: OpportunityView;
  viewProfiles: Record<ScorePerspective, OpportunityView>;
  search: string;
  /** Exclusion-summary chip: show only rows unscorable for this reason code in the active perspective. */
  reasonFilter: string | null;
  selectedSymbol: string | null;
  detail: OpportunityDetail | null;
  detailLoading: boolean;
  detailError: string | null;
}

function loadViews(): Pick<OpportunitiesState, 'view' | 'viewProfiles'> {
  try {
    const raw = localStorage.getItem(OPPORTUNITIES_VIEW_KEY);
    const saved = normaliseViewPreferences(raw ? JSON.parse(raw) : null);
    return { view: saved.profiles[saved.perspective], viewProfiles: saved.profiles };
  } catch {
    const saved = normaliseViewPreferences(null);
    return { view: saved.profiles[saved.perspective], viewProfiles: saved.profiles };
  }
}

/** Fills fields missing from an older saved view with defaults so a stored value can never break the page. */
export function normaliseView(
  value: Partial<OpportunityView> | null | undefined,
  perspectiveFallback?: ScorePerspective,
): OpportunityView {
  const perspective =
    perspectiveFallback ??
    (value?.perspective === 'Strategic'
      ? 'Strategic'
      : value?.perspective === 'Swing'
        ? 'Swing'
        : 'Swing');
  const defaults = defaultOpportunityView(perspective);
  if (!value || typeof value !== 'object') return defaults;
  const sort = { ...defaults.sort, ...(value.sort ?? {}) };
  if (sort.active === 'score') sort.active = perspective === 'Swing' ? 'swing' : 'strategic';
  return {
    ...defaults,
    ...value,
    setupQuality: Array.isArray(value.setupQuality)
      ? value.setupQuality.filter((quality) =>
          ['Good', 'Caution', 'Poor', 'Unavailable'].includes(quality),
        )
      : [],
    perspective,
    sort,
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

/** Migrates the legacy single view into both profiles; new profile data is normalized independently. */
export function normaliseViewPreferences(value: unknown): OpportunityViewPreferences {
  const record = isRecord(value) ? value : {};
  const legacyView = { ...record };
  delete legacyView['profiles'];
  const savedProfiles = isRecord(record['profiles']) ? record['profiles'] : {};
  const perspective: ScorePerspective =
    record['perspective'] === 'Strategic' ? 'Strategic' : 'Swing';
  const swing = isRecord(savedProfiles['Swing'])
    ? normaliseView(savedProfiles['Swing'] as Partial<OpportunityView>, 'Swing')
    : normaliseView(
        {
          ...legacyView,
          sort: perspective === 'Swing' ? legacyView['sort'] : undefined,
        } as Partial<OpportunityView>,
        'Swing',
      );
  const strategic = isRecord(savedProfiles['Strategic'])
    ? normaliseView(savedProfiles['Strategic'] as Partial<OpportunityView>, 'Strategic')
    : normaliseView(
        {
          ...legacyView,
          sort: perspective === 'Strategic' ? legacyView['sort'] : undefined,
        } as Partial<OpportunityView>,
        'Strategic',
      );
  return {
    perspective,
    profiles: {
      Swing: { ...swing, perspective: 'Swing' },
      Strategic: { ...strategic, perspective: 'Strategic' },
    },
  };
}

/**
 * PM-029 Trading Opportunities. The snapshot is the only data source for the grid, the details panel and the export,
 * so all three always agree. This store never computes a recommendation: scores, Sector Heat, setups and the
 * authoritative Final Action summary all come from the backend snapshot.
 */
export const OpportunitiesStore = signalStore(
  { providedIn: 'root' },
  withState<OpportunitiesState>(() => ({
    snapshot: null,
    config: null,
    refreshing: false,
    autoRefreshAttempted: false,
    ...loadViews(),
    search: '',
    reasonFilter: null,
    selectedSymbol: null,
    detail: null,
    detailLoading: false,
    detailError: null,
  })),
  withRequestStatus(),
  withComputed(({ snapshot, selectedSymbol, view }) => ({
    rows: computed(() => snapshot()?.rows ?? []),
    selectedRow: computed(() => {
      const symbol = selectedSymbol();
      return symbol ? (snapshot()?.rows.find((r) => r.symbol === symbol) ?? null) : null;
    }),
    hasSnapshot: computed(() => (snapshot()?.snapshotId ?? null) !== null),
    /**
     * True when the snapshot has rows but none carries a score in the active perspective (e.g. refreshed before the
     * session counts as completed). A score-threshold filter would then hide every row, so the page lists them all.
     * Derived, never written to the saved view, so a later preference reload cannot undo it.
     */
    nothingScored: computed(() => {
      const rows = snapshot()?.rows ?? [];
      const perspective = view().perspective;
      return (
        rows.length > 0 &&
        !rows.some((r) => (perspective === 'Swing' ? r.swing : r.strategic).score !== null)
      );
    }),
  })),
  withMethods(
    (
      store,
      api = inject(OpportunitiesApiService),
      userPrefs = inject(UserPreferencesStore),
      snackBar = inject(MatSnackBar),
    ) => {
      let polls = 0;
      let pollTimer: ReturnType<typeof setTimeout> | undefined;

      const currentPreferences = (): OpportunityViewPreferences => ({
        perspective: store.view().perspective,
        profiles: store.viewProfiles(),
      });

      const persistView = (): void => {
        const preferences = currentPreferences();
        try {
          localStorage.setItem(OPPORTUNITIES_VIEW_KEY, JSON.stringify(preferences));
        } catch {
          // storage unavailable — the DB copy still applies
        }
        userPrefs.set(OPPORTUNITIES_VIEW_KEY, preferences);
      };

      const detailRequest = rxMethod<{ symbol: string; snapshotId: number | null }>(
        pipe(
          tap(() => patchState(store, { detailLoading: true, detailError: null })),
          // switchMap drops the response of a previously selected row; the symbol check below guards the rest.
          switchMap(({ symbol, snapshotId }) =>
            api.getDetail(symbol, snapshotId).pipe(
              tap((detail) => {
                if (store.selectedSymbol() !== symbol) return;
                patchState(store, { detail, detailLoading: false });
              }),
              catchError((err: unknown) => {
                if (store.selectedSymbol() !== symbol) return EMPTY;
                if (err instanceof HttpErrorResponse && err.status === 410) {
                  // The snapshot was replaced (e.g. refreshed in another tab): reload everything so grid and panel agree.
                  patchState(store, { detail: null, detailLoading: false, detailError: null });
                  load();
                } else {
                  patchState(store, {
                    detail: null,
                    detailLoading: false,
                    detailError:
                      err instanceof HttpErrorResponse && err.status === 404
                        ? 'This symbol is not in the current snapshot.'
                        : 'Failed to load details.',
                  });
                }
                return EMPTY;
              }),
            ),
          ),
        ),
      );

      const applySnapshot = (snapshot: OpportunitySnapshot): void => {
        const previousId = store.snapshot()?.snapshotId ?? null;
        patchState(store, { snapshot }, setLoaded());
        const selected = store.selectedSymbol();
        if (selected && snapshot.snapshotId !== previousId) {
          if (snapshot.rows.some((r) => r.symbol === selected)) {
            detailRequest({ symbol: selected, snapshotId: snapshot.snapshotId });
          } else {
            patchState(store, { selectedSymbol: null, detail: null });
          }
        }
        schedulePollIfNeeded(snapshot);
      };

      const schedulePollIfNeeded = (snapshot: OpportunitySnapshot): void => {
        clearTimeout(pollTimer);
        if (!snapshot.refreshInFlight || store.refreshing() || polls >= MAX_POLLS) {
          if (!snapshot.refreshInFlight) polls = 0;
          return;
        }
        polls++;
        pollTimer = setTimeout(() => load(), REFRESH_POLL_MS);
      };

      const maybeAutoRefresh = (snapshot: OpportunitySnapshot): void => {
        if (store.autoRefreshAttempted() || snapshot.refreshInFlight) return;
        if (snapshot.snapshotId !== null && !snapshot.isStale) return;
        patchState(store, { autoRefreshAttempted: true });
        refresh();
      };

      const load = rxMethod<void>(
        pipe(
          tap(() => patchState(store, setLoading())),
          switchMap(() =>
            api.getLatest().pipe(
              tapResponse({
                next: (snapshot) => {
                  applySnapshot(snapshot);
                  maybeAutoRefresh(snapshot);
                },
                error: () => patchState(store, setError('Failed to load trading opportunities')),
              }),
            ),
          ),
        ),
      );

      const refresh = rxMethod<void>(
        pipe(
          filter(() => !store.refreshing()),
          tap(() => patchState(store, { refreshing: true })),
          exhaustMap(() =>
            api.refresh().pipe(
              tapResponse({
                next: (snapshot) => {
                  patchState(store, { refreshing: false });
                  applySnapshot(snapshot);
                  if (snapshot.status === 'Partial') {
                    snackBar.open(
                      'Refreshed with warnings — some evidence is unavailable.',
                      'Close',
                      { duration: 6000 },
                    );
                  }
                },
                error: (err: unknown) => {
                  patchState(store, { refreshing: false });
                  if (err instanceof HttpErrorResponse && err.status === 409) {
                    snackBar.open(
                      'A refresh is already running. Results will appear when it finishes.',
                      'Close',
                      {
                        duration: 5000,
                      },
                    );
                    polls = 0;
                    pollTimer = setTimeout(() => load(), REFRESH_POLL_MS);
                  } else {
                    snackBar.open(
                      'Refresh failed — the last successful snapshot is still shown.',
                      'Close',
                      {
                        duration: 6000,
                      },
                    );
                    load();
                  }
                },
              }),
            ),
          ),
        ),
      );

      const loadConfig = rxMethod<void>(
        pipe(
          switchMap(() =>
            api.getConfig().pipe(
              tapResponse({
                next: (config) => patchState(store, { config }),
                error: () => undefined, // thresholds then render as unavailable; never fall back to literals
              }),
            ),
          ),
        ),
      );

      return {
        load,
        refresh,
        loadConfig,

        select(symbol: string | null): void {
          if (symbol === store.selectedSymbol()) return;
          patchState(store, {
            selectedSymbol: symbol,
            detail: null,
            detailError: null,
            detailLoading: false,
          });
          if (symbol) detailRequest({ symbol, snapshotId: store.snapshot()?.snapshotId ?? null });
        },

        updateView(patch: Partial<OpportunityView>): void {
          patchState(store, (s) => {
            const perspective = patch.perspective ?? s.view.perspective;
            const nextView = { ...s.viewProfiles[perspective], ...patch, perspective };
            return {
              view: nextView,
              viewProfiles: { ...s.viewProfiles, [perspective]: nextView },
            };
          });
          persistView();
        },

        resetFilters(): void {
          patchState(store, (s) => {
            const reset = {
              ...DEFAULT_OPPORTUNITY_VIEW,
              perspective: s.view.perspective,
              sort: s.view.sort,
            };
            return {
              view: reset,
              viewProfiles: { ...s.viewProfiles, [reset.perspective]: reset },
              search: '',
              reasonFilter: null,
            };
          });
          persistView();
        },

        restoreDefaultSort(): void {
          patchState(store, (s) => {
            const perspective = s.view.perspective;
            const reset = { ...s.view, sort: defaultOpportunityView(perspective).sort };
            return {
              view: reset,
              viewProfiles: { ...s.viewProfiles, [perspective]: reset },
            };
          });
          persistView();
        },

        setSearch(search: string): void {
          patchState(store, { search });
        },

        setReasonFilter(reasonFilter: string | null): void {
          patchState(store, { reasonFilter });
        },

        /** Overlays the DB copy of the view once it loads (DB wins, as for grid columns). */
        applyViewFromDb(view: unknown): void {
          const preferences = normaliseViewPreferences(view);
          patchState(store, {
            view: preferences.profiles[preferences.perspective],
            viewProfiles: preferences.profiles,
          });
        },

        /** Clears the pending poll (used by tests and on logout). */
        stopPolling(): void {
          clearTimeout(pollTimer);
          polls = 0;
        },
      };
    },
  ),
  withHooks({
    onInit(store, userPrefs = inject(UserPreferencesStore)) {
      onceAuthenticated(() => {
        store.loadConfig();
        store.load();
      });

      effect(() => {
        const dbValue = userPrefs.get<unknown>(OPPORTUNITIES_VIEW_KEY);
        if (dbValue) untracked(() => store.applyViewFromDb(dbValue));
      });
    },
    onDestroy(store) {
      store.stopPolling();
    },
  }),
);
