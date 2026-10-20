import { effect, inject, untracked } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { ValueScreenerResult } from '../models/portfolio.models';
import { DecisionEngineService } from '../services/decision-engine.service';
import { NotificationApiService } from '../services/notification-api.service';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { buildEligibleCandidates, mergeRsiMaps } from '../services/priority-candidate-eligibility';
import { AppRefreshStore } from './app-refresh.store';
import { ConfigStore } from './config.store';
import { DashboardStore } from './dashboard.store';
import { onceAuthenticated } from './features/auth-gate';
import { ScannerStore } from './scanner.store';
import { WatchlistRsiStore } from './watchlist-rsi.store';

interface ScoreAlertState {
  valueScreenerMap: Map<string, ValueScreenerResult>;
  /** Last "threshold|SYM,SYM" payload sent, so an unchanged candidate set is never re-posted. */
  lastSentKey: string;
}

/**
 * Emails registered recipients when eligible Priority Candidates reach the configured score.
 * After each refresh settles, the qualifying symbols are posted to the API once; the server
 * re-validates the scores and dedupes, so a symbol is only ever emailed once per trading day.
 */
export const ScoreAlertStore = signalStore(
  { providedIn: 'root' },
  withState<ScoreAlertState>({ valueScreenerMap: new Map(), lastSentKey: '' }),
  withMethods((store, api = inject(PortfolioApiService), notifications = inject(NotificationApiService)) => ({
    loadValueScreener: rxMethod<void>(
      pipe(
        switchMap(() =>
          api.getLatestValueScreener().pipe(
            tapResponse({
              next: (dto) => {
                const valueScreenerMap = new Map<string, ValueScreenerResult>();
                for (const r of [...(dto.watchlist ?? []), ...(dto.portfolio ?? [])])
                  valueScreenerMap.set(r.symbol.toUpperCase(), r);
                patchState(store, { valueScreenerMap });
              },
              error: () => {}, // Non-critical — candidates fall back to no value-screener context
            }),
          ),
        ),
      ),
    ),

    send(threshold: number, symbols: string[], key: string): void {
      patchState(store, { lastSentKey: key });
      notifications.sendScoreAlert(threshold, symbols).subscribe({
        // Outside the market window the server ignores the post; forget the key so the next
        // refresh inside the window sends it. Also retry after a failed request.
        next: (res) => {
          if (res.reason === 'OutsideMarketHours') patchState(store, { lastSentKey: '' });
        },
        error: () => patchState(store, { lastSentKey: '' }),
      });
    },
  })),
  withHooks({
    onInit(store) {
      const config = inject(ConfigStore);
      const dashboard = inject(DashboardStore);
      const scanner = inject(ScannerStore);
      const watchlistRsi = inject(WatchlistRsiStore);
      const appRefresh = inject(AppRefreshStore);
      const engine = inject(DecisionEngineService);

      onceAuthenticated(() => store.loadValueScreener());

      let firstRun = true;
      effect(() => {
        appRefresh.lastRefreshedAt();
        if (firstRun) {
          firstRun = false;
          return;
        }
        untracked(() => store.loadValueScreener());
      });

      effect(() => {
        const scores = dashboard.actionScores();
        const busy =
          appRefresh.isRefreshing() ||
          dashboard.actionScoresLoading() ||
          watchlistRsi.rsiLoading() ||
          scanner.loading();
        const threshold = config.scoreAlertThreshold();
        if (busy || scores.length === 0) return;

        const rsiMap = mergeRsiMaps(watchlistRsi.rsiMap(), [
          ...scanner.oversold(),
          ...scanner.overbought(),
        ]);
        const symbols = buildEligibleCandidates(scores, rsiMap, store.valueScreenerMap(), engine)
          .filter((row) => row.score.totalScore >= threshold)
          .map((row) => row.score.symbol.toUpperCase())
          .sort();
        if (symbols.length === 0) return;

        const key = `${threshold}|${symbols.join(',')}`;
        untracked(() => {
          if (key !== store.lastSentKey()) store.send(threshold, symbols, key);
        });
      });
    },
  }),
);
