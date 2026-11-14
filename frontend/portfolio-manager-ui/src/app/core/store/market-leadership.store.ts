import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { Observable, finalize, firstValueFrom } from 'rxjs';
import { CreateMarketLeadershipTrackerRequest } from '../models/portfolio.models';
import { DashboardStore } from './dashboard.store';
import { MarketLeadershipApiService } from '../services/market-leadership-api.service';

interface MarketLeadershipState {
  saving: boolean;
  error: string | null;
}

function messageFor(error: unknown): string {
  if (typeof error === 'object' && error !== null && 'error' in error) {
    const body = (error as { error?: { message?: string } }).error;
    if (body?.message) return body.message;
  }
  if (typeof error === 'object' && error !== null && 'status' in error) {
    const status = (error as { status?: number }).status;
    if (status === 404 || status === 405) {
      return 'The backend must be restarted before tracker changes can be saved.';
    }
  }
  return 'Unable to update market trackers.';
}

/**
 * Write side of the dashboard's market-leadership trackers. The tracker list itself lives in
 * `DashboardStore`; every successful change asks it to reload so the widget stays in sync.
 */
export const MarketLeadershipStore = signalStore(
  { providedIn: 'root' },
  withState<MarketLeadershipState>({ saving: false, error: null }),
  withMethods(
    (
      store,
      api = inject(MarketLeadershipApiService),
      dashboard = inject(DashboardStore),
    ) => {
      const save = async (request$: Observable<unknown>): Promise<boolean> => {
        patchState(store, { saving: true, error: null });
        try {
          await firstValueFrom(request$);
          dashboard.loadMarketLeadership();
          return true;
        } catch (error: unknown) {
          patchState(store, { error: messageFor(error) });
          return false;
        } finally {
          patchState(store, { saving: false });
        }
      };

      return {
        addTracker: (request: CreateMarketLeadershipTrackerRequest): Promise<boolean> =>
          save(api.addTracker(request)),

        updateTracker: (
          trackerId: number,
          request: CreateMarketLeadershipTrackerRequest,
        ): Promise<boolean> => save(api.updateTracker(trackerId, request)),

        removeTracker(trackerId: number): void {
          patchState(store, { saving: true, error: null });
          api
            .removeTracker(trackerId)
            .pipe(finalize(() => patchState(store, { saving: false })))
            .subscribe({
              next: () => dashboard.loadMarketLeadership(),
              error: (error: unknown) => patchState(store, { error: messageFor(error) }),
            });
        },
      };
    },
  ),
);
