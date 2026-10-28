import { HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, untracked } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { Observable, pipe, switchMap, tap } from 'rxjs';
import {
  BrokerStopTransitionRequest,
  JournalRow,
  OpenTradesSummary,
  RiskPosition,
  SaveBrokerStopRequest,
  SaveRiskPlanRequest,
  SwingTradeRow,
} from '../models/swing-trading.models';
import { FinalActionResult } from '../models/portfolio.models';
import { SwingTradingApiService } from '../services/swing-trading-api.service';
import {
  EMPTY_SWING_FILTERS,
  SwingFilters,
  distinctSorted,
  filterSwingRows,
} from '../../features/swing-trading/swing-trading-logic';
import { FinalActionStore } from './final-action.store';
import { AppRefreshStore } from './app-refresh.store';
import { onceAuthenticated } from './features/auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

interface SwingTradingState {
  rows: SwingTradeRow[];
  summary: OpenTradesSummary | null;
  thresholdPct: number;
  thresholdNote: string;
  filters: SwingFilters;
  journal: JournalRow[];
  journalLoading: boolean;
  journalLoaded: boolean;
  journalError: string | null;
  refreshPhase: 'quotes' | 'trades' | null;
  refreshError: string | null;
  lastRefreshedAt: Date | null;
  /** The position shown in the Manage Risk dialog (plan, broker records, history). */
  position: RiskPosition | null;
  positionLoading: boolean;
  saving: boolean;
  positionError: string | null;
  /** True after a 409: someone else changed the record, the dialog must reload before saving. */
  conflict: boolean;
}

const initialState: SwingTradingState = {
  rows: [],
  summary: null,
  thresholdPct: 5,
  thresholdNote: '',
  filters: EMPTY_SWING_FILTERS,
  journal: [],
  journalLoading: false,
  journalLoaded: false,
  journalError: null,
  refreshPhase: null,
  refreshError: null,
  lastRefreshedAt: null,
  position: null,
  positionLoading: false,
  saving: false,
  positionError: null,
  conflict: false,
};

function errorMessage(err: unknown, fallback: string): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error as { error?: string } | null;
    if (body?.error) return body.error;
  }
  return fallback;
}

/**
 * Swing Trading workspace state: open trades with their recorded stops, the journal, and the position shown in
 * the shared Manage Risk dialog. Stops here are what the USER recorded about broker orders; nothing in this store
 * places, changes or verifies a brokerage order, and the Final Action is only read from {@link FinalActionStore}.
 */
export const SwingTradingStore = signalStore(
  { providedIn: 'root' },
  withState<SwingTradingState>(initialState),
  withRequestStatus(),
  withComputed((store, finalActions = inject(FinalActionStore), appRefresh = inject(AppRefreshStore)) => {
    const finalActionOf = (row: SwingTradeRow): string | null => {
      const byId = finalActions.portfolioByItemId();
      for (const id of row.lotIds) {
        const hit = byId.get(id);
        if (hit) return hit.finalAction;
      }
      return null;
    };
    const filteredRows = computed(() => filterSwingRows(store.rows(), store.filters(), finalActionOf));
    return {
      filteredRows,
      refreshing: computed(() => store.refreshPhase() !== null || appRefresh.isRefreshing()),
      accounts: computed(() => distinctSorted(store.rows().map((r) => r.account))),
      finalActionOptions: computed(() => distinctSorted(store.rows().map(finalActionOf))),
      attentionCount: computed(() => filteredRows().filter((r) => r.needsAttention).length),
      hasFilters: computed(() => JSON.stringify(store.filters()) !== JSON.stringify(EMPTY_SWING_FILTERS)),
    };
  }),
  withMethods((store, api = inject(SwingTradingApiService), finalActions = inject(FinalActionStore), appRefresh = inject(AppRefreshStore)) => {
    /** The canonical Final Action result for a trade: the first of its lots the engine evaluated. */
    const finalActionResultFor = (row: SwingTradeRow): FinalActionResult | null => {
      const byId = finalActions.portfolioByItemId();
      for (const id of row.lotIds) {
        const hit = byId.get(id);
        if (hit) return hit;
      }
      return null;
    };
    const finalActionOf = (row: SwingTradeRow): string | null => finalActionResultFor(row)?.finalAction ?? null;

    const loadOpenTrades = rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap(() =>
          api.getOpenTrades().pipe(
            tapResponse({
              next: (res) =>
                patchState(
                  store,
                  {
                    rows: res.rows,
                    summary: res.summary,
                    thresholdPct: res.approachingThresholdPct,
                    thresholdNote: res.thresholdNote,
                  },
                  setLoaded(),
                ),
              error: () => patchState(store, setError('Failed to load Swing trades')),
            }),
          ),
        ),
      ),
    );

    const loadJournal = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { journalLoading: true, journalError: null })),
        switchMap(() =>
          api.getJournal().pipe(
            tapResponse({
              next: (journal) => patchState(store, { journal, journalLoading: false, journalLoaded: true }),
              error: () =>
                patchState(store, { journalLoading: false, journalError: 'Failed to load the Trade Journal' }),
            }),
          ),
        ),
      ),
    );

    let refreshBaseline: Date | null = null;
    let refreshJournal = false;
    effect(() => {
      const phase = store.refreshPhase();
      const quotesBusy = appRefresh.isRefreshing();
      const quotesError = appRefresh.error();
      const quotesRefreshedAt = appRefresh.lastRefreshedAt();
      const tradesBusy = store.loading();
      const journalBusy = store.journalLoading();
      const tradesError = store.error();
      const journalError = store.journalError();
      untracked(() => {
        if (phase === 'quotes' && !quotesBusy) {
          if (quotesError || quotesRefreshedAt === refreshBaseline) {
            patchState(store, { refreshPhase: null, refreshError: quotesError ?? 'Refresh was cancelled. Displayed data was not refreshed.' });
            return;
          }
          patchState(store, { refreshPhase: 'trades' });
          loadOpenTrades();
          if (refreshJournal) loadJournal();
        } else if (phase === 'trades' && !tradesBusy && !journalBusy) {
          const error = tradesError ?? (refreshJournal ? journalError : null);
          patchState(store, {
            refreshPhase: null,
            refreshError: error,
            ...(error ? {} : { lastRefreshedAt: new Date() }),
          });
        }
      });
    });

    const loadPosition = rxMethod<{ account: string; symbol: string }>(
      pipe(
        tap(() => patchState(store, { positionLoading: true, positionError: null, conflict: false })),
        switchMap(({ account, symbol }) =>
          api.getRiskPosition(account, symbol).pipe(
            tapResponse({
              next: (position) => patchState(store, { position, positionLoading: false }),
              error: (err) =>
                patchState(store, {
                  positionLoading: false,
                  positionError: errorMessage(err, 'Failed to load risk details'),
                }),
            }),
          ),
        ),
      ),
    );

    /** Runs a write; on success shows the returned position and refreshes the grid. Resolves true when saved. */
    const mutate = (call: Observable<RiskPosition>): Promise<boolean> => {
      patchState(store, { saving: true, positionError: null, conflict: false });
      return new Promise<boolean>((resolve) => {
        call.subscribe({
          next: (position) => {
            patchState(store, { position, saving: false });
            loadOpenTrades();
            resolve(true);
          },
          error: (err: unknown) => {
            patchState(store, {
              saving: false,
              conflict: err instanceof HttpErrorResponse && err.status === 409,
              positionError: errorMessage(err, 'Could not save. Nothing was changed.'),
            });
            resolve(false);
          },
        });
      });
    };

    return {
      finalActionResultFor,
      finalActionOf,
      loadOpenTrades,
      loadJournal,
      loadPosition,
      refresh(includeJournal = false): void {
        if (store.refreshing() || store.loading() || store.journalLoading()) return;
        refreshBaseline = appRefresh.lastRefreshedAt();
        refreshJournal = includeJournal || store.journalLoaded();
        patchState(store, { refreshPhase: 'quotes', refreshError: null });
        appRefresh.refreshAll('user-click');
      },
      setFilters(patch: Partial<SwingFilters>): void {
        patchState(store, ({ filters }) => ({ filters: { ...filters, ...patch } }));
      },
      resetFilters(): void {
        patchState(store, { filters: EMPTY_SWING_FILTERS });
      },
      clearPosition(): void {
        patchState(store, { position: null, positionError: null, conflict: false, saving: false });
      },
      savePlan: (request: SaveRiskPlanRequest) => mutate(api.savePlan(request)),
      saveBrokerStop: (request: SaveBrokerStopRequest) =>
        mutate(request.id === null ? api.addBrokerStop(request) : api.updateBrokerStop(request.id, request)),
      transitionBrokerStop: (id: number, request: BrokerStopTransitionRequest) =>
        mutate(api.transitionBrokerStop(id, request)),
    };
  }),
  withHooks({
    onInit(store) {
      // Wait for auth: loading earlier races the token refresh and 401s.
      onceAuthenticated(() => store.loadOpenTrades());
    },
  }),
);
