import { inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { PortfolioBetaResult } from '../models/portfolio.models';
import { PortfolioBetaApiService } from '../services/portfolio-beta-api.service';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

const BETA_OVERRIDES_KEY = 'pm_beta_overrides_v1';

interface PortfolioBetaState {
  result: PortfolioBetaResult | null;
  /** User-editable beta overrides, keyed by symbol (upper-case). Persisted to localStorage. */
  betaOverrides: Record<string, number>;
  /** Fetched beta values from Yahoo Finance, keyed by symbol (upper-case). */
  fetchedBetas: Record<string, number>;
}

function loadOverrides(): Record<string, number> {
  try {
    const raw = localStorage.getItem(BETA_OVERRIDES_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

/**
 * Portfolio beta plus the user's per-symbol beta overrides. An override always wins over the
 * fetched Yahoo value, and changing one reloads the result so the card reflects it.
 */
export const PortfolioBetaStore = signalStore(
  { providedIn: 'root' },
  withState<PortfolioBetaState>(() => ({
    result: null,
    betaOverrides: loadOverrides(),
    fetchedBetas: {},
  })),
  withRequestStatus(),
  withMethods((store, api = inject(PortfolioBetaApiService)) => {
    const fetch = rxMethod<void>(
      pipe(
        switchMap(() => {
          const overrides = store.betaOverrides();
          return api.getBeta(Object.keys(overrides).length > 0 ? overrides : undefined).pipe(
            tapResponse({
              next: (r) => {
                // Populate fetched betas from ALL contributors (not just top 5)
                const fetched: Record<string, number> = {};
                for (const c of r.topContributors) fetched[c.symbol.toUpperCase()] = c.beta;
                patchState(
                  store,
                  ({ fetchedBetas }) => ({ result: r, fetchedBetas: { ...fetchedBetas, ...fetched } }),
                  setLoaded(),
                );
              },
              error: (err) => {
                console.error('[PortfolioBeta] Failed to load beta', err);
                patchState(store, setError('Failed to load portfolio beta'));
              },
            }),
          );
        }),
      ),
    );

    const load = (force = false): void => {
      if (store.loading() && !force) return;
      patchState(store, setLoading());
      fetch();
    };

    return {
      load,

      /** Returns the effective beta for a symbol (override wins over fetched). */
      betaForSymbol(symbol: string): number | null {
        const key = symbol.toUpperCase();
        const override = store.betaOverrides()[key];
        if (override !== undefined) return override;
        const fetched = store.fetchedBetas()[key];
        return fetched !== undefined ? fetched : null;
      },

      setOverride(symbol: string, beta: number | null): void {
        const key = symbol.toUpperCase();
        patchState(store, ({ betaOverrides }) => {
          const next = { ...betaOverrides };
          if (beta === null) delete next[key];
          else next[key] = beta;
          return { betaOverrides: next };
        });
        localStorage.setItem(BETA_OVERRIDES_KEY, JSON.stringify(store.betaOverrides()));
        // Reload beta card with new override
        load(true);
      },
    };
  }),
);
