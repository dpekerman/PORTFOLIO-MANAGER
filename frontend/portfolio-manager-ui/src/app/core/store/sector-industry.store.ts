import { computed, inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { Observable, pipe, switchMap, tap } from 'rxjs';
import { SectorIndustryLists } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import {
  setError,
  setLoaded,
  setLoading,
  withRequestStatus,
} from './features/request-status.feature';

interface SectorIndustryState {
  sectors: string[];
  industries: string[];
  dirty: boolean;
}

/** Sector and industry picklists. Edits stay local (dirty) until `save()` persists them. */
export const SectorIndustryStore = signalStore(
  { providedIn: 'root' },
  withState<SectorIndustryState>({ sectors: [], industries: [], dirty: false }),
  withRequestStatus(),
  withComputed(({ sectors, industries }) => ({
    sectorCount: computed(() => sectors().length),
    industryCount: computed(() => industries().length),
  })),
  withMethods((store, api = inject(PortfolioApiService)) => {
    const apply = (lists: SectorIndustryLists) =>
      patchState(store, { sectors: lists.sectors, industries: lists.industries, dirty: false });

    const addTo = (key: 'sectors' | 'industries', value: string) => {
      const v = value.trim();
      if (!v || store[key]().includes(v)) return;
      patchState(store, { [key]: [...store[key](), v].sort(), dirty: true });
    };

    const removeFrom = (key: 'sectors' | 'industries', value: string) =>
      patchState(store, { [key]: store[key]().filter((x) => x !== value), dirty: true });

    return {
      load: rxMethod<void>(
        pipe(
          tap(() => patchState(store, setLoading())),
          switchMap(() =>
            api.getSectorIndustryLists().pipe(
              tapResponse({
                next: (lists) => {
                  apply(lists);
                  patchState(store, setLoaded());
                },
                error: () =>
                  patchState(store, setError('Could not load sector/industry lists from backend.')),
              }),
            ),
          ),
        ),
      ),
      addSector: (value: string) => addTo('sectors', value),
      removeSector: (value: string) => removeFrom('sectors', value),
      addIndustry: (value: string) => addTo('industries', value),
      removeIndustry: (value: string) => removeFrom('industries', value),
      /** Persists both lists; `decisionSources` is forwarded when the caller wants it kept in sync. */
      save(decisionSources?: string[]): Observable<SectorIndustryLists> {
        return api
          .saveSectorIndustryLists({
            sectors: store.sectors(),
            industries: store.industries(),
            ...(decisionSources ? { decisionSources } : {}),
          })
          .pipe(tap(apply));
      },
    };
  }),
);
