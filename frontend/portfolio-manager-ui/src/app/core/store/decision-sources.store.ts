import { inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { Observable, pipe, switchMap, tap } from 'rxjs';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { ConfigStore } from './config.store';
import { setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

export const DECISION_SOURCE_DEFAULTS = [
  'App Signal',
  'Manual',
  'Catalyst',
  'Rebalance',
  'Risk Control',
  'Loss Harvest',
];

interface DecisionSourcesState {
  items: string[];
  dirty: boolean;
}

/** User-managed "Decision Source" picklist, mirrored into {@link ConfigStore} for the forms. */
export const DecisionSourcesStore = signalStore(
  { providedIn: 'root' },
  withState<DecisionSourcesState>({ items: [], dirty: false }),
  withRequestStatus(),
  withMethods((store, api = inject(PortfolioApiService), config = inject(ConfigStore)) => {
    const apply = (items: string[] | undefined) => {
      const list = items && items.length > 0 ? items : DECISION_SOURCE_DEFAULTS;
      patchState(store, { items: list, dirty: false });
      config.update({ decisionSources: list });
    };

    return {
      load: rxMethod<void>(
        pipe(
          tap(() => patchState(store, setLoading())),
          switchMap(() =>
            api.getDecisionSources().pipe(
              tapResponse({
                next: (data) => {
                  apply(data.items);
                  patchState(store, setLoaded());
                },
                // Non-critical: fall back to the picklist already cached in ConfigStore
                error: () =>
                  patchState(store, { items: config.config().decisionSources }, setLoaded()),
              }),
            ),
          ),
        ),
      ),
      /** Adds a value, or replaces the one at `index`. Duplicates are ignored on add. */
      upsert(value: string, index: number | null): void {
        const v = value.trim();
        if (!v) return;
        if (index === null) {
          if (store.items().includes(v)) return;
          patchState(store, { items: [...store.items(), v], dirty: true });
        } else {
          patchState(store, {
            items: store.items().map((item, i) => (i === index ? v : item)),
            dirty: true,
          });
        }
      },
      removeAt(index: number): void {
        patchState(store, { items: store.items().filter((_, i) => i !== index), dirty: true });
      },
      save(): Observable<{ items: string[] }> {
        return api.saveDecisionSourcesList(store.items()).pipe(tap((data) => apply(data.items)));
      },
    };
  }),
);
