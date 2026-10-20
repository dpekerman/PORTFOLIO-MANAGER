import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { PortfolioApiService } from '../services/portfolio-api.service';

export interface BackfilledDay {
  recordedDate: string;
  totalValue: number;
}

const LOOKBACK_DAYS = 30;

interface HistoryBackfillState {
  missingDays: string[];
  scanning: boolean;
  reconstructing: boolean;
  scanned: boolean;
  /** Days written by the last reconstruction; null before one has run. */
  result: BackfilledDay[] | null;
}

/** Detects and backfills missing end-of-day portfolio value snapshots. */
export const HistoryBackfillStore = signalStore(
  { providedIn: 'root' },
  withState<HistoryBackfillState>({
    missingDays: [],
    scanning: false,
    reconstructing: false,
    scanned: false,
    result: null,
  }),
  withMethods((store, api = inject(PortfolioApiService), snackBar = inject(MatSnackBar)) => ({
    scan(): void {
      patchState(store, { scanning: true, scanned: false, result: null });
      api.getMissingHistoryDays(LOOKBACK_DAYS).subscribe({
        next: (missingDays) => patchState(store, { missingDays, scanned: true, scanning: false }),
        error: () => {
          patchState(store, { scanning: false });
          snackBar.open('Failed to scan for missing days.', 'Dismiss', { duration: 4000 });
        },
      });
    },
    reconstruct(): void {
      patchState(store, { reconstructing: true, result: null });
      api.backfillMissingHistory(LOOKBACK_DAYS).subscribe({
        next: (filled) => {
          patchState(store, {
            reconstructing: false,
            result: filled.map((d) => ({ recordedDate: d.recordedDate, totalValue: d.totalValue })),
            missingDays: [],
            scanned: false,
          });
          snackBar.open(
            filled.length > 0
              ? `${filled.length} day(s) reconstructed successfully.`
              : 'No missing days found — history is up to date.',
            'OK',
            { duration: 4000 },
          );
        },
        error: () => {
          patchState(store, { reconstructing: false });
          snackBar.open('Reconstruction failed. Check backend logs.', 'Dismiss', {
            duration: 5000,
          });
        },
      });
    },
  })),
);
