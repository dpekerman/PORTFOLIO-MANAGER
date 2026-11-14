import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { concatMap, pipe, switchMap, tap } from 'rxjs';
import {
  IndexConstituent,
  IndexConstituentRequest,
  SectorBenchmarkMapping,
  SectorBenchmarkRequest,
} from '../models/opportunities.models';
import { OpportunitiesApiService } from '../services/opportunities-api.service';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

interface OpportunityReferenceState {
  constituents: IndexConstituent[];
  benchmarks: SectorBenchmarkMapping[];
  saving: boolean;
}

/** Problem-details title/detail, or a status-specific fallback; never a raw stack trace. */
export function referenceErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 403) return 'Only administrators can change PM-029 reference data.';
    const body = err.error as { title?: string; detail?: string; errors?: Record<string, string[]> } | null;
    const firstValidation = body?.errors ? Object.values(body.errors).flat()[0] : undefined;
    return body?.detail ?? firstValidation ?? body?.title ?? fallback;
  }
  return fallback;
}

/**
 * PM-029 reference data (TSX 60 constituents and sector benchmark mappings). Imports are never automatic:
 * each row is entered and verified by an administrator, and constituents are retired, never deleted.
 */
export const OpportunityReferenceStore = signalStore(
  { providedIn: 'root' },
  withState<OpportunityReferenceState>({ constituents: [], benchmarks: [], saving: false }),
  withRequestStatus(),
  withComputed(({ constituents }) => ({
    currentConstituents: computed(() => constituents().filter((c) => !c.effectiveTo)),
    retiredConstituents: computed(() => constituents().filter((c) => !!c.effectiveTo)),
  })),
  withMethods((store, api = inject(OpportunitiesApiService), snackBar = inject(MatSnackBar)) => {
    const fail = (err: unknown, fallback: string) => {
      patchState(store, { saving: false });
      snackBar.open(referenceErrorMessage(err, fallback), 'Dismiss', { duration: 5000 });
    };
    const upsertConstituent = (row: IndexConstituent) =>
      patchState(store, {
        saving: false,
        constituents: store.constituents().some((c) => c.id === row.id)
          ? store.constituents().map((c) => (c.id === row.id ? row : c))
          : [...store.constituents(), row],
      });

    return {
      load: rxMethod<void>(
        pipe(
          tap(() => patchState(store, setLoading())),
          switchMap(() =>
            api.getReference().pipe(
              tapResponse({
                next: (ref) => patchState(store, { constituents: ref.constituents, benchmarks: ref.benchmarks }, setLoaded()),
                error: (err) =>
                  patchState(
                    store,
                    setError(referenceErrorMessage(err, 'Could not load PM-029 reference data (has the migration been applied?).')),
                  ),
              }),
            ),
          ),
        ),
      ),
      addConstituent: rxMethod<IndexConstituentRequest>(
        pipe(
          tap(() => patchState(store, { saving: true })),
          concatMap((req) =>
            api.addConstituent(req).pipe(
              tapResponse({
                next: (row) => {
                  upsertConstituent(row);
                  snackBar.open(`${row.symbol} added to ${row.indexCode}.`, 'OK', { duration: 3000 });
                },
                error: (err) => fail(err, 'Could not add constituent.'),
              }),
            ),
          ),
        ),
      ),
      retireConstituent: rxMethod<{ id: number; effectiveTo: string | null }>(
        pipe(
          tap(() => patchState(store, { saving: true })),
          concatMap(({ id, effectiveTo }) =>
            api.retireConstituent(id, effectiveTo).pipe(
              tapResponse({
                next: (row) => {
                  upsertConstituent(row);
                  snackBar.open(`${row.symbol} retired from ${row.indexCode}.`, 'OK', { duration: 3000 });
                },
                error: (err) => fail(err, 'Could not retire constituent.'),
              }),
            ),
          ),
        ),
      ),
      upsertBenchmark: rxMethod<SectorBenchmarkRequest>(
        pipe(
          tap(() => patchState(store, { saving: true })),
          concatMap((req) =>
            api.upsertBenchmark(req).pipe(
              tapResponse({
                next: (row) => {
                  const exists = store.benchmarks().some((b) => b.id === row.id);
                  patchState(store, {
                    saving: false,
                    benchmarks: exists ? store.benchmarks().map((b) => (b.id === row.id ? row : b)) : [...store.benchmarks(), row],
                  });
                  snackBar.open(`Benchmark for ${row.sector} (${row.market}) saved.`, 'OK', { duration: 3000 });
                },
                error: (err) => fail(err, 'Could not save benchmark mapping.'),
              }),
            ),
          ),
        ),
      ),
    };
  }),
);
