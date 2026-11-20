import { inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { Observable, pipe, switchMap, tap } from 'rxjs';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

export interface ValueScreenerSchedule {
  scheduledTimeEt: string;
  enabled: boolean;
}

interface ValueScreenerScheduleState {
  schedule: ValueScreenerSchedule | null;
}

/** Run time (ET) and on/off switch of the scheduled Value Screener. */
export const ValueScreenerScheduleStore = signalStore(
  { providedIn: 'root' },
  withState<ValueScreenerScheduleState>({ schedule: null }),
  withRequestStatus(),
  withMethods((store, api = inject(PortfolioApiService)) => ({
    load: rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap(() =>
          api.getValueScreenerSchedule().pipe(
            tapResponse({
              next: (s) =>
                patchState(
                  store,
                  {
                    schedule: {
                      scheduledTimeEt: s.scheduledTimeEt ?? '17:00',
                      enabled: s.enabled ?? true,
                    },
                  },
                  setLoaded(),
                ),
              error: () => patchState(store, setLoaded()),
            }),
          ),
        ),
      ),
    ),
    save(schedule: ValueScreenerSchedule): Observable<void> {
      return api
        .updateValueScreenerSchedule(schedule.scheduledTimeEt, schedule.enabled)
        .pipe(tap(() => patchState(store, { schedule })));
    },
  })),
);
