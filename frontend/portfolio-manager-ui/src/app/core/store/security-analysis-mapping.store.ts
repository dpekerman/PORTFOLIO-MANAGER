import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import {
  SaveSecurityAnalysisMappingRequest,
  SecurityAnalysisMapping,
} from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

interface SecurityAnalysisMappingState {
  mapping: SecurityAnalysisMapping | null;
}

function messageFor(error: unknown): string {
  if (typeof error === 'object' && error !== null && 'error' in error) {
    const body = (error as { error?: string | { title?: string } }).error;
    if (typeof body === 'string') return body;
    if (body?.title) return body.title;
  }
  return 'Unable to update the underlying security mapping.';
}

/**
 * The underlying-security mapping of one trading ticker at a time (e.g. a leveraged ETF analysed
 * through its underlying). Methods resolve to a result instead of throwing so the dialog can
 * branch on the outcome and read `error()`.
 */
export const SecurityAnalysisMappingStore = signalStore(
  { providedIn: 'root' },
  withState<SecurityAnalysisMappingState>({ mapping: null }),
  withRequestStatus(),
  withMethods((store, api = inject(PortfolioApiService)) => ({
    async load(tradingTicker: string): Promise<SecurityAnalysisMapping | null> {
      patchState(store, setLoading());
      try {
        const mapping = await firstValueFrom(api.getSecurityAnalysisMapping(tradingTicker));
        patchState(store, { mapping });
        return mapping;
      } catch (error: unknown) {
        patchState(store, setError(messageFor(error)));
        return null;
      } finally {
        patchState(store, setLoaded());
      }
    },

    validate(tradingTicker: string, underlyingTicker: string): Promise<void> {
      return firstValueFrom(
        api.validateUnderlyingTicker(tradingTicker, {
          underlyingTicker: underlyingTicker.trim().toUpperCase(),
          useUnderlyingForAnalysis: true,
        }),
      );
    },

    async save(tradingTicker: string, request: SaveSecurityAnalysisMappingRequest): Promise<boolean> {
      patchState(store, setLoading());
      try {
        const mapping = await firstValueFrom(
          api.saveSecurityAnalysisMapping(tradingTicker, request),
        );
        patchState(store, { mapping });
        return true;
      } catch (error: unknown) {
        patchState(store, setError(messageFor(error)));
        return false;
      } finally {
        patchState(store, setLoaded());
      }
    },

    async remove(tradingTicker: string): Promise<boolean> {
      patchState(store, setLoading());
      try {
        await firstValueFrom(api.removeSecurityAnalysisMapping(tradingTicker));
        patchState(store, { mapping: null });
        return true;
      } catch (error: unknown) {
        patchState(store, setError(messageFor(error)));
        return false;
      } finally {
        patchState(store, setLoaded());
      }
    },
  })),
);
