import { computed, inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { Observable, forkJoin, of, pipe, switchMap, tap } from 'rxjs';
import {
  AllocationRiskTarget,
  AllocationSectorTarget,
  SinglePositionLimit,
} from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

export type AllocationListKey = 'risk' | 'sector' | 'limit';

/** A row of any of the three lists, normalised to a label + percentage. */
export interface AllocationRow {
  id: number;
  label: string;
  targetPct: number;
}

interface AllocationRiskState {
  riskTargets: AllocationRiskTarget[];
  sectorTargets: AllocationSectorTarget[];
  positionLimits: SinglePositionLimit[];
  /** Server ids removed locally that still need a DELETE on save. */
  pendingDeletes: Record<AllocationListKey, number[]>;
  dirty: boolean;
}

let tempId = -1;

/**
 * Allocation targets (by role / sector) and single-position limits. Edits are staged locally and
 * flushed by `save()`; new rows carry negative temporary ids until the server assigns real ones.
 */
export const AllocationRiskStore = signalStore(
  { providedIn: 'root' },
  withState<AllocationRiskState>({
    riskTargets: [],
    sectorTargets: [],
    positionLimits: [],
    pendingDeletes: { risk: [], sector: [], limit: [] },
    dirty: false,
  }),
  withRequestStatus(),
  withComputed(({ riskTargets, sectorTargets, positionLimits }) => ({
    riskRows: computed<AllocationRow[]>(() =>
      riskTargets().map((r) => ({ id: r.id, label: r.role, targetPct: r.targetPct })),
    ),
    sectorRows: computed<AllocationRow[]>(() =>
      sectorTargets().map((s) => ({ id: s.id, label: s.sector, targetPct: s.targetPct })),
    ),
    limitRows: computed<AllocationRow[]>(() =>
      positionLimits().map((l) => ({ id: l.id, label: l.role, targetPct: l.targetPct })),
    ),
  })),
  withMethods((store, api = inject(PortfolioApiService)) => {
    const load = rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap(() =>
          api.getAllocationRiskConfig().pipe(
            tapResponse({
              next: (cfg) =>
                patchState(
                  store,
                  {
                    riskTargets: cfg.riskTargets,
                    sectorTargets: cfg.sectorTargets,
                    positionLimits: cfg.positionLimits,
                  },
                  setLoaded(),
                ),
              error: () => patchState(store, setLoaded()),
            }),
          ),
        ),
      ),
    );

    return {
      load,
      /** Adds a row (id === null) or updates the row with the given id. */
      upsert(list: AllocationListKey, id: number | null, label: string, targetPct: number): void {
        const next = (items: { id: number }[]) => items.length;
        if (list === 'risk') {
          patchState(store, ({ riskTargets }) => ({
            riskTargets:
              id === null
                ? [
                    ...riskTargets,
                    { id: tempId--, role: label, targetPct, displayOrder: next(riskTargets) },
                  ]
                : riskTargets.map((r) => (r.id === id ? { ...r, role: label, targetPct } : r)),
            dirty: true,
          }));
        } else if (list === 'sector') {
          patchState(store, ({ sectorTargets }) => ({
            sectorTargets:
              id === null
                ? [
                    ...sectorTargets,
                    { id: tempId--, sector: label, targetPct, displayOrder: next(sectorTargets) },
                  ]
                : sectorTargets.map((s) => (s.id === id ? { ...s, sector: label, targetPct } : s)),
            dirty: true,
          }));
        } else {
          patchState(store, ({ positionLimits }) => ({
            positionLimits:
              id === null
                ? [
                    ...positionLimits,
                    { id: tempId--, role: label, targetPct, displayOrder: next(positionLimits) },
                  ]
                : positionLimits.map((l) => (l.id === id ? { ...l, role: label, targetPct } : l)),
            dirty: true,
          }));
        }
      },
      remove(list: AllocationListKey, id: number): void {
        patchState(store, ({ pendingDeletes, riskTargets, sectorTargets, positionLimits }) => ({
          pendingDeletes:
            id > 0 ? { ...pendingDeletes, [list]: [...pendingDeletes[list], id] } : pendingDeletes,
          riskTargets: list === 'risk' ? riskTargets.filter((i) => i.id !== id) : riskTargets,
          sectorTargets:
            list === 'sector' ? sectorTargets.filter((i) => i.id !== id) : sectorTargets,
          positionLimits:
            list === 'limit' ? positionLimits.filter((i) => i.id !== id) : positionLimits,
          dirty: true,
        }));
      },
      save(): Observable<unknown> {
        const { risk, sector, limit } = store.pendingDeletes();
        const calls: Observable<unknown>[] = [
          ...risk.map((id) => api.deleteRiskTarget(id)),
          ...sector.map((id) => api.deleteSectorTarget(id)),
          ...limit.map((id) => api.deletePositionLimit(id)),
          ...store
            .riskTargets()
            .map((r) => api.upsertRiskTarget(r.id > 0 ? r.id : null, r.role, r.targetPct)),
          ...store
            .sectorTargets()
            .map((s) => api.upsertSectorTarget(s.id > 0 ? s.id : null, s.sector, s.targetPct)),
          ...store
            .positionLimits()
            .map((l) => api.upsertPositionLimit(l.id > 0 ? l.id : null, l.role, l.targetPct)),
        ];
        return (calls.length ? forkJoin(calls) : of([])).pipe(
          tap(() => {
            patchState(store, {
              pendingDeletes: { risk: [], sector: [], limit: [] },
              dirty: false,
            });
            load();
          }),
        );
      },
    };
  }),
);
