import { computed, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { Observable } from 'rxjs';
import { PortfolioValueHistoryDto, SnapshotStatus } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { onceAuthenticated } from './features/auth-gate';
import { setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

export interface HistoryDateRange {
  from: string | null;
  to: string | null;
}

interface PortfolioValueHistoryState {
  /** Most-recent-first, exactly as the API returns them. */
  snapshots: PortfolioValueHistoryDto[];
  /** Message of the last failed admin action; cleared when the next one starts. */
  actionError: string | null;
  dateRange: HistoryDateRange;
  statusFilter: SnapshotStatus | null;
}

const extractErrorMessage = (err: unknown): string | null => {
  const e = err as { error?: { message?: string } | string };
  if (typeof e?.error === 'string') return e.error;
  return e?.error?.message ?? null;
};

/**
 * Snapshots-tab state for the Portfolio Value History page (the Cash Ledger tab uses CashStore).
 * Filtering is client-side over the fetched window (the server defaults to the last 90 days);
 * every admin action refetches so the table always reflects what the server recorded.
 */
export const PortfolioValueHistoryStore = signalStore(
  { providedIn: 'root' },
  withState<PortfolioValueHistoryState>({
    snapshots: [],
    actionError: null,
    dateRange: { from: null, to: null },
    statusFilter: null,
  }),
  withRequestStatus(),
  withComputed(({ snapshots, dateRange, statusFilter }) => ({
    filteredSnapshots: computed(() => {
      const { from, to } = dateRange();
      const status = statusFilter();
      return snapshots().filter((s) => {
        if (from && s.recordedDate < from) return false;
        if (to && s.recordedDate > to) return false;
        if (status && s.snapshotStatus !== status) return false;
        return true;
      });
    }),
    latestSnapshot: computed<PortfolioValueHistoryDto | null>(() => snapshots()[0] ?? null),
  })),
  withMethods((store, api = inject(PortfolioApiService), snackBar = inject(MatSnackBar)) => {
    const refresh = (): void => {
      patchState(store, setLoading());
      api.getPortfolioValueHistoryRange().subscribe({
        next: (snapshots) => patchState(store, { snapshots }, setLoaded()),
        error: (err) => {
          patchState(store, setLoaded());
          console.error('Failed to load portfolio value history', err);
        },
      });
    };

    /** Runs a server-side admin action, then refetches; failures surface as `actionError` + snackbar. */
    const runAction = (
      request: Observable<unknown>,
      successMessage: string,
      failureMessage: string,
    ): void => {
      patchState(store, { actionError: null });
      request.subscribe({
        next: () => {
          snackBar.open(successMessage, 'Dismiss', { duration: 3000 });
          refresh();
        },
        error: (err) => {
          const message = extractErrorMessage(err) ?? failureMessage;
          patchState(store, { actionError: message });
          snackBar.open(message, 'Dismiss', { duration: 5000 });
        },
      });
    };

    return {
      refresh,

      setDateRange(dateRange: HistoryDateRange): void {
        patchState(store, { dateRange });
      },

      setStatusFilter(statusFilter: SnapshotStatus | null): void {
        patchState(store, { statusFilter });
      },

      resetFilters(): void {
        patchState(store, { dateRange: { from: null, to: null }, statusFilter: null });
      },

      recordTodayNow(): void {
        runAction(
          api.recordPortfolioValueNow(),
          "Today's snapshot recorded",
          "Failed to record today's snapshot",
        );
      },

      reconcileToday(): void {
        runAction(api.reconcileCashToday(), "Today's cash reconciled", "Failed to reconcile today's cash");
      },

      recalculateCashFrom(fromDate: string): void {
        runAction(
          api.recalculateCashFromDate(fromDate),
          `Cash recalculated from ${fromDate}`,
          'Failed to recalculate cash',
        );
      },
    };
  }),
  withHooks({
    onInit(store) {
      // Wait for auth before loading: loading earlier races the token refresh and 401s.
      onceAuthenticated(() => store.refresh());
    },
  }),
);
