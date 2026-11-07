import { computed, inject } from '@angular/core';
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
import { pipe, switchMap, tap } from 'rxjs';
import { ActionCenterFilter, OpenDecision, OpenDecisionsResponse } from '../models/portfolio.models';
import { DashboardApiService } from '../services/dashboard-api.service';
import { onceAuthenticated } from './features/auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

export const ACTION_CENTER_FILTER_STORAGE_KEY = 'dashboard_action_center_filter';

const FILTERS: readonly ActionCenterFilter[] = ['All', 'Entry', 'ReduceExit', 'Risk', 'Holdings', 'Watchlist'];

/** Unknown values (including the retired REQUIRED / DEVELOPING / INFORMATIONAL filters) fall back to All. */
export function parseActionCenterFilter(value: string | null): ActionCenterFilter {
  return FILTERS.find((f) => f === value) ?? 'All';
}

export function filterDecisions(items: readonly OpenDecision[], filter: ActionCenterFilter): OpenDecision[] {
  switch (filter) {
    case 'Entry':
      return items.filter((d) => d.bucket === 'EntryAdd');
    case 'ReduceExit':
      return items.filter((d) => d.bucket === 'TrimExit');
    case 'Risk':
      return items.filter((d) => d.bucket === 'RiskReview');
    case 'Holdings':
      return items.filter((d) => d.context === 'Holding');
    case 'Watchlist':
      return items.filter((d) => d.context === 'Watchlist');
    default:
      return [...items];
  }
}

interface OpenDecisionsState {
  response: OpenDecisionsResponse | null;
  filter: ActionCenterFilter;
}

/**
 * The single source for the Dashboard Portfolio Action Summary and the Action Center list. Both read the
 * same response, so the counts always equal the Action Center total. The filter only narrows the list.
 */
export const OpenDecisionsStore = signalStore(
  { providedIn: 'root' },
  withState<OpenDecisionsState>(() => ({
    response: null,
    filter: parseActionCenterFilter(localStorage.getItem(ACTION_CENTER_FILTER_STORAGE_KEY)),
  })),
  withRequestStatus(),
  withComputed(({ response, filter }) => ({
    summary: computed(() => response()?.summary ?? null),
    validation: computed(() => response()?.validation ?? null),
    allItems: computed(() => response()?.items ?? []),
    /** Awaiting Data and unresolved historical warnings: always visible, never counted as decisions. */
    notActionable: computed(() => response()?.notActionable ?? []),
    items: computed(() => filterDecisions(response()?.items ?? [], filter())),
  })),
  withMethods((store, api = inject(DashboardApiService)) => ({
    load: rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap(() =>
          api.getActionCenter().pipe(
            tapResponse({
              // A failed reload keeps the previous decisions on screen rather than blanking the queue.
              next: (response) => patchState(store, { response }, setLoaded()),
              error: () => patchState(store, setError('Could not load current decisions')),
            }),
          ),
        ),
      ),
    ),

    /** Clicking the active filter again clears it. */
    toggleFilter(filter: ActionCenterFilter): void {
      const next = store.filter() === filter ? 'All' : filter;
      patchState(store, { filter: next });
      localStorage.setItem(ACTION_CENTER_FILTER_STORAGE_KEY, next);
    },

    setFilter(filter: ActionCenterFilter): void {
      patchState(store, { filter });
      localStorage.setItem(ACTION_CENTER_FILTER_STORAGE_KEY, filter);
    },
  })),
  withHooks({
    onInit(store) {
      onceAuthenticated(() => store.load());
    },
  }),
);
