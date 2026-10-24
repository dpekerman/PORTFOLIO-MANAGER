import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap } from 'rxjs';
import {
  EvaluationResult,
  FeedbackRating,
  NewsSource,
  NewsSourceRequest,
  PreMarketConfig,
  PreMarketRunDetail,
  PreMarketRunSummary,
  PreMarketStatus,
  RunPreMarketRequest,
} from '../models/premarket.models';
import { PreMarketApiService } from '../services/premarket-api.service';
import { onceAuthenticated } from './features/auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

interface PreMarketState {
  config: PreMarketConfig | null;
  status: PreMarketStatus | null;
  report: PreMarketRunDetail | null;
  runs: PreMarketRunSummary[];
  sources: NewsSource[];
  running: boolean;
  evaluation: EvaluationResult | null;
}

/** State for the AI Market Intelligence feature. State is patched only here. */
export const PreMarketStore = signalStore(
  { providedIn: 'root' },
  withState<PreMarketState>({ config: null, status: null, report: null, runs: [], sources: [], running: false, evaluation: null }),
  withRequestStatus(),
  withMethods((store, api = inject(PreMarketApiService), snackBar = inject(MatSnackBar)) => {
    const notify = (m: string, ms = 4000) => snackBar.open(m, 'Close', { duration: ms });
    const fail = (m: string) => () => notify(m);

    const loadLatest = rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap(() =>
          api.getLatest().pipe(
            tapResponse({
              next: (report) => patchState(store, { report: report ?? null }, setLoaded()),
              error: () => patchState(store, setError('Failed to load the latest report')),
            }),
          ),
        ),
      ),
    );
    const loadRuns = rxMethod<void>(
      pipe(switchMap(() => api.listRuns().pipe(tapResponse({ next: (runs) => patchState(store, { runs }), error: fail('Failed to load history') })))),
    );
    const loadSources = rxMethod<void>(
      pipe(switchMap(() => api.listSources().pipe(tapResponse({ next: (sources) => patchState(store, { sources }), error: fail('Failed to load news sources') })))),
    );
    const loadAdmin = rxMethod<void>(
      pipe(
        switchMap(() =>
          api.getConfig().pipe(tapResponse({ next: (config) => patchState(store, { config }), error: fail('Failed to load settings') })),
        ),
        switchMap(() =>
          api.getStatus().pipe(tapResponse({ next: (status) => patchState(store, { status }), error: fail('Failed to load agent status') })),
        ),
      ),
    );

    const refreshAll = () => {
      loadLatest();
      loadRuns();
      loadSources();
      loadAdmin();
    };

    return {
      loadLatest,
      loadRuns,
      loadSources,
      loadAdmin,
      refreshAll,

      openRun: rxMethod<number>(
        pipe(
          switchMap((id) =>
            api.getRun(id).pipe(tapResponse({ next: (report) => patchState(store, { report }), error: fail('Failed to open report') })),
          ),
        ),
      ),

      loadEvaluation: rxMethod<number>(
        pipe(
          tap(() => patchState(store, { evaluation: null })),
          switchMap((id) =>
            api.getEvaluation(id).pipe(
              tapResponse({ next: (evaluation) => patchState(store, { evaluation }), error: fail('Failed to evaluate outcomes') }),
            ),
          ),
        ),
      ),

      runNow(req: RunPreMarketRequest): void {
        patchState(store, { running: true });
        api.run(req).subscribe({
          next: (r) => {
            patchState(store, { running: false });
            notify(r.status === 'FAILED' ? `Run failed: ${r.error ?? 'unknown error'}` : `Run ${r.status.toLowerCase()}`);
            loadLatest();
            loadRuns();
          },
          error: () => {
            patchState(store, { running: false });
            notify('Run failed to start');
          },
        });
      },

      saveConfig(cfg: PreMarketConfig): void {
        api.saveConfig(cfg).subscribe({
          next: (config) => {
            patchState(store, { config });
            notify('Settings saved');
            loadAdmin();
          },
          error: fail('Failed to save settings'),
        });
      },

      testEmail(): void {
        api.testEmail().subscribe({
          next: (r) => notify(r.message, 6000),
          error: fail('Test email failed'),
        });
      },

      resend(id: number): void {
        api.resend(id).subscribe({
          next: (r) => {
            notify(r.message, 6000);
            loadRuns();
          },
          error: fail('Resend failed'),
        });
      },

      sendFeedback(runId: number, rating: FeedbackRating, symbol: string | null): void {
        api.feedback(runId, rating, symbol).subscribe({
          next: () => notify('Feedback saved', 2500),
          error: fail('Failed to save feedback'),
        });
      },

      saveSource(id: number | null, req: NewsSourceRequest): void {
        const call = id === null ? api.addSource(req) : api.updateSource(id, req);
        call.subscribe({
          next: () => {
            notify(id === null ? 'Source added (pending approval)' : 'Source updated');
            loadSources();
          },
          error: (e) => notify(e?.error?.error ?? 'Failed to save source'),
        });
      },

      approveSource(id: number): void {
        api.approveSource(id).subscribe({
          next: () => {
            notify('Source approved');
            loadSources();
          },
          error: (e) => notify(e?.error?.error ?? 'Approval failed'),
        });
      },

      archiveSource(id: number): void {
        api.archiveSource(id).subscribe({
          next: () => loadSources(),
          error: fail('Failed to remove source'),
        });
      },

      testSource(id: number): void {
        api.testSource(id).subscribe({
          next: (r) => {
            notify(r.ok ? `Reachable: ${r.articles} articles in ${r.durationMs} ms` : `Failed: ${r.error}`, 6000);
            loadSources();
          },
          error: fail('Connectivity test failed'),
        });
      },
    };
  }),
  withHooks({
    onInit(store) {
      onceAuthenticated(() => store.refreshAll());
    },
  }),
);
