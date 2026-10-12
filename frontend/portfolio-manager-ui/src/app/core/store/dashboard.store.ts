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
import { filter, pipe, switchMap, tap } from 'rxjs';
import {
  ActionScoreDto,
  AnalyticsDecisionPerformanceResponse,
  DashboardEodSummary,
  DashboardResponse,
  MarketLeadershipResponse,
  PerformanceSummaryResponse,
  PortfolioActionDto,
} from '../models/portfolio.models';
import { DashboardApiService } from '../services/dashboard-api.service';

interface DashboardState {
  data: DashboardResponse | null;
  loading: boolean;
  error: string | null;
  portfolioActions: PortfolioActionDto[];
  actionsLoading: boolean;
  eodSummary: DashboardEodSummary | null;
  eodSummaryLoading: boolean;
  decisionPerformance: AnalyticsDecisionPerformanceResponse | null;
  performanceLoading: boolean;
  marketLeadership: MarketLeadershipResponse | null;
  marketLeadershipLoading: boolean;
  actionScores: ActionScoreDto[];
  actionScoresLoading: boolean;
  performanceSummary: PerformanceSummaryResponse | null;
  performanceSummaryLoading: boolean;
}

const initialState: DashboardState = {
  data: null,
  loading: false,
  error: null,
  portfolioActions: [],
  actionsLoading: false,
  eodSummary: null,
  eodSummaryLoading: false,
  decisionPerformance: null,
  performanceLoading: false,
  marketLeadership: null,
  marketLeadershipLoading: false,
  actionScores: [],
  actionScoresLoading: false,
  performanceSummary: null,
  performanceSummaryLoading: false,
};

/**
 * True when the server's RSI section looks stale: rows are missing the per-row signal metadata, or
 * the aggregate counts disagree with the rows. That means the snapshot predates the metadata and a
 * recompute is needed.
 */
function needsSignalMetadataRefresh(section: DashboardResponse['rsiSection']): boolean {
  if (!section) return false;
  const rows = [...section.oversoldSignals, ...section.overboughtSignals];
  const rowNewCount = rows.filter((row) => row.isNewToday === true).length;
  const rowRequiredCount = rows.filter((row) => row.isActionRequired === true).length;
  return (
    rows.length > 0 &&
    (rows.some(
      (row) => typeof row.isNewToday !== 'boolean' || typeof row.isActionRequired !== 'boolean',
    ) ||
      rowNewCount !== section.newTodayCount ||
      rowRequiredCount !== section.actionRequiredCount)
  );
}

/**
 * The dashboard snapshot and its independently-loading widgets (actions, EOD summary, market
 * leadership, scores, performance).
 *
 * Each widget keeps its own loading flag so one slow or failing endpoint never blanks the page;
 * widget failures are deliberately silent and leave the previous data on screen. Only the main
 * snapshot surfaces `error`. Unlike most stores this loads eagerly on creation rather than waiting
 * for auth, because the dashboard is the landing route and is created after the auth guard runs.
 */
export const DashboardStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ data }) => ({
    /** Legacy alias for {@link data}, kept so existing call sites keep compiling. */
    hasData: computed(() => data()),
  })),
  withMethods((store, api = inject(DashboardApiService)) => {
    const loadPortfolioActions = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { actionsLoading: true })),
        switchMap(() =>
          api.getPortfolioActions().pipe(
            tapResponse({
              next: (portfolioActions) =>
                patchState(store, { portfolioActions, actionsLoading: false }),
              error: () => patchState(store, { actionsLoading: false }),
            }),
          ),
        ),
      ),
    );

    const loadEodSummary = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { eodSummaryLoading: true })),
        switchMap(() =>
          api.getEodSummary().pipe(
            tapResponse({
              next: (eodSummary) => patchState(store, { eodSummary, eodSummaryLoading: false }),
              error: () => patchState(store, { eodSummaryLoading: false }),
            }),
          ),
        ),
      ),
    );

    const loadMarketLeadership = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { marketLeadershipLoading: true })),
        switchMap(() =>
          api.getMarketLeadership().pipe(
            tapResponse({
              next: (marketLeadership) =>
                patchState(store, { marketLeadership, marketLeadershipLoading: false }),
              error: () => patchState(store, { marketLeadershipLoading: false }),
            }),
          ),
        ),
      ),
    );

    const loadActionScores = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { actionScoresLoading: true })),
        switchMap(() =>
          api.getActionScores().pipe(
            tapResponse({
              next: (actionScores) =>
                patchState(store, { actionScores, actionScoresLoading: false }),
              error: () => patchState(store, { actionScoresLoading: false }),
            }),
          ),
        ),
      ),
    );

    const loadPerformanceSummary = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { performanceSummaryLoading: true })),
        switchMap(() =>
          api.getPerformanceSummary().pipe(
            tapResponse({
              next: (performanceSummary) =>
                patchState(store, { performanceSummary, performanceSummaryLoading: false }),
              error: () => patchState(store, { performanceSummaryLoading: false }),
            }),
          ),
        ),
      ),
    );

    const refresh = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() =>
          api.refresh().pipe(
            tapResponse({
              next: (data) => {
                patchState(store, { data, loading: false });
                loadPortfolioActions();
                loadEodSummary();
                loadMarketLeadership();
                patchState(store, { actionScores: [] });
                loadActionScores();
                loadPerformanceSummary();
              },
              error: () => patchState(store, { error: 'Dashboard refresh failed', loading: false }),
            }),
          ),
        ),
      ),
    );

    // Guard: only ever request one metadata recompute per app session, so a server that keeps
    // returning stale counts cannot put us in a refresh loop.
    let signalMetadataRefreshRequested = false;

    const load = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() =>
          api.getLatest().pipe(
            tapResponse({
              next: (data) => {
                patchState(store, { data, loading: false });
                if (
                  data?.rsiSection &&
                  !signalMetadataRefreshRequested &&
                  needsSignalMetadataRefresh(data.rsiSection)
                ) {
                  signalMetadataRefreshRequested = true;
                  refresh();
                }
              },
              error: () =>
                patchState(store, { error: 'Dashboard snapshot unavailable', loading: false }),
            }),
          ),
        ),
      ),
    );

    return {
      load,
      refresh,
      loadPortfolioActions,
      loadEodSummary,
      loadMarketLeadership,
      loadActionScores,
      loadPerformanceSummary,

      /** Loaded once and cached: the analytics query is expensive and the data is end-of-day. */
      loadDecisionPerformance: rxMethod<void>(
        pipe(
          filter(() => !store.decisionPerformance()),
          tap(() => patchState(store, { performanceLoading: true })),
          switchMap(() =>
            api.getDecisionPerformance().pipe(
              tapResponse({
                next: (decisionPerformance) =>
                  patchState(store, { decisionPerformance, performanceLoading: false }),
                error: () => patchState(store, { performanceLoading: false }),
              }),
            ),
          ),
        ),
      ),
    };
  }),
  withHooks({
    onInit(store) {
      store.load();
      store.loadPortfolioActions();
      store.loadEodSummary();
      store.loadMarketLeadership();
      store.loadPerformanceSummary();
    },
  }),
);
