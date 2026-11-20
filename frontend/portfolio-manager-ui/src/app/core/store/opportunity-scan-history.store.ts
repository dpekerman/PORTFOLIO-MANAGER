import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { exhaustMap, filter, pipe, switchMap, tap } from 'rxjs';
import { OpportunityScanHistory } from '../models/opportunities.models';
import { OpportunitiesApiService } from '../services/opportunities-api.service';

interface OpportunityScanHistoryState {
  history: OpportunityScanHistory[];
  loading: boolean;
  refreshing: boolean;
  error: string | null;
}

/** Recent Trading Opportunities scan results and the on-demand refresh action. */
export const OpportunityScanHistoryStore = signalStore(
  { providedIn: 'root' },
  withState<OpportunityScanHistoryState>({
    history: [],
    loading: false,
    refreshing: false,
    error: null,
  }),
  withMethods((store, api = inject(OpportunitiesApiService), snackBar = inject(MatSnackBar)) => {
    const load = rxMethod<void>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() =>
          api.getHistory().pipe(
            tapResponse({
              next: (history) => patchState(store, { history, loading: false, error: null }),
              error: () =>
                patchState(store, {
                  loading: false,
                  error: 'Failed to load Trading Opportunities scan results.',
                }),
            }),
          ),
        ),
      ),
    );

    return {
      load,
      refreshNow: rxMethod<void>(
        pipe(
          filter(() => !store.refreshing()),
          tap(() => patchState(store, { refreshing: true })),
          exhaustMap(() =>
            api.refresh().pipe(
              tapResponse({
                next: (snapshot) => {
                  patchState(store, { refreshing: false, error: null });
                  load();
                  const failed = snapshot.lastAttempt?.status === 'Failed';
                  snackBar.open(
                    failed
                      ? (snapshot.lastAttempt?.errorSummary ?? 'Trading Opportunities scan failed.')
                      : snapshot.status === 'Partial'
                        ? 'Trading Opportunities scan completed with warnings.'
                        : 'Trading Opportunities scan completed.',
                    'Dismiss',
                    { duration: failed ? 8000 : 5000 },
                  );
                },
                error: (err: unknown) => {
                  patchState(store, { refreshing: false });
                  load();
                  const message =
                    err instanceof HttpErrorResponse && err.status === 409
                      ? 'A Trading Opportunities scan is already running.'
                      : 'Failed to start the Trading Opportunities scan.';
                  snackBar.open(message, 'Dismiss', { duration: 7000 });
                },
              }),
            ),
          ),
        ),
      ),
    };
  }),
);
