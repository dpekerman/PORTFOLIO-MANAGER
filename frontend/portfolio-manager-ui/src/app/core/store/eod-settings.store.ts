import { inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { Observable, pipe, switchMap, tap } from 'rxjs';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { ConfigStore } from './config.store';
import { setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

export interface EodSettings {
  eodWindowStart: string;
  eodWindowEnd: string;
  eodWindowEnabled: boolean;
  eodOversoldRsiThreshold: number;
  eodOverboughtRsiThreshold: number;
}

interface EodSettingsState {
  /** Last settings confirmed by the server; null until loaded. */
  settings: EodSettings | null;
}

/** Server-side EOD confirmation window; also mirrors the window times into {@link ConfigStore}. */
export const EodSettingsStore = signalStore(
  { providedIn: 'root' },
  withState<EodSettingsState>({ settings: null }),
  withRequestStatus(),
  withMethods((store, api = inject(PortfolioApiService), config = inject(ConfigStore)) => {
    const mirror = (s: EodSettings) =>
      config.update({
        eodWindowStart: s.eodWindowStart,
        eodWindowEnd: s.eodWindowEnd,
        eodWindowEnabled: s.eodWindowEnabled,
      });

    return {
      load: rxMethod<void>(
        pipe(
          tap(() => patchState(store, setLoading())),
          switchMap(() =>
            api.getEodSettings().pipe(
              tapResponse({
                next: (s) => {
                  const settings = {
                    ...s,
                    eodOversoldRsiThreshold: s.eodOversoldRsiThreshold ?? 25,
                    eodOverboughtRsiThreshold: s.eodOverboughtRsiThreshold ?? 75,
                  };
                  patchState(store, { settings }, setLoaded());
                  mirror(settings);
                },
                // Non-critical: the form keeps its defaults
                error: () => patchState(store, setLoaded()),
              }),
            ),
          ),
        ),
      ),
      save(settings: EodSettings): Observable<void> {
        return api.updateEodSettings(settings).pipe(
          tap(() => {
            patchState(store, { settings });
            mirror(settings);
          }),
        );
      },
    };
  }),
);
