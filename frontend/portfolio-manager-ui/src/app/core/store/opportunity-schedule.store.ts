import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { exhaustMap, pipe, switchMap, tap } from 'rxjs';
import { OpportunitySchedule, OpportunityScheduleRequest } from '../models/opportunities.models';
import { OpportunitiesApiService } from '../services/opportunities-api.service';
import { referenceErrorMessage } from './opportunity-reference.store';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

interface OpportunityScheduleState {
  schedule: OpportunitySchedule | null;
  saving: boolean;
}

/** Automatic post-close Trading Opportunities refresh schedule (Admin only). */
export const OpportunityScheduleStore = signalStore(
  { providedIn: 'root' },
  withState<OpportunityScheduleState>({ schedule: null, saving: false }),
  withRequestStatus(),
  withMethods((store, api = inject(OpportunitiesApiService), snackBar = inject(MatSnackBar)) => ({
    load: rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap(() =>
          api.getSchedule().pipe(
            tapResponse({
              next: (schedule) => patchState(store, { schedule }, setLoaded()),
              error: () => patchState(store, setError('Failed to load the refresh schedule')),
            }),
          ),
        ),
      ),
    ),
    save: rxMethod<OpportunityScheduleRequest>(
      pipe(
        tap(() => patchState(store, { saving: true })),
        exhaustMap((request) =>
          api.saveSchedule(request).pipe(
            tapResponse({
              next: (schedule) => {
                patchState(store, { schedule, saving: false });
                snackBar.open('Refresh schedule saved.', 'Dismiss', { duration: 4000 });
              },
              error: (err: unknown) => {
                patchState(store, { saving: false });
                snackBar.open(referenceErrorMessage(err, 'Failed to save the schedule.'), 'Dismiss', { duration: 5000 });
              },
            }),
          ),
        ),
      ),
    ),
  })),
);
