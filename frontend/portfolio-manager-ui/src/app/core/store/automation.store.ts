import { inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { Subscription, timer } from 'rxjs';
import { switchMap, takeUntil } from 'rxjs/operators';
import {
  AutomationRunLogDto,
  AutomationSettingsDto,
  AutomationTaskStatusDto,
  AutomationTimezoneDiagnosticsDto,
  DatabaseBackupResultDto,
  MissedDataRecoveryResultDto,
  UpdateAutomationSettingsRequest,
} from '../models/portfolio.models';
import { AutomationApiService } from '../services/automation-api.service';

interface AutomationState {
  settings: AutomationSettingsDto | null;
  lastRun: AutomationRunLogDto | null;
  history: AutomationRunLogDto[];
  taskStatus: AutomationTaskStatusDto | null;
  timezoneDiagnostics: AutomationTimezoneDiagnosticsDto | null;

  loadingSettings: boolean;
  savingSettings: boolean;
  loadingLastRun: boolean;
  loadingHistory: boolean;
  loadingTaskStatus: boolean;
  loadingTimezoneDiagnostics: boolean;
  runInFlight: boolean;
  settingUp: boolean;
  rotatingSecret: boolean;
  cancelling: boolean;
  clearingHistory: boolean;
  recovering: boolean;
  recoveryResult: MissedDataRecoveryResultDto | null;
  backingUp: boolean;
  backupResult: DatabaseBackupResultDto | null;
  /** Human-readable description of what is currently running, or the failure that ended it. */
  activeOperation: string | null;
  error: string | null;
}

const initialState: AutomationState = {
  settings: null,
  lastRun: null,
  history: [],
  taskStatus: null,
  timezoneDiagnostics: null,
  loadingSettings: false,
  savingSettings: false,
  loadingLastRun: false,
  loadingHistory: false,
  loadingTaskStatus: false,
  loadingTimezoneDiagnostics: false,
  runInFlight: false,
  settingUp: false,
  rotatingSecret: false,
  cancelling: false,
  clearingHistory: false,
  recovering: false,
  recoveryResult: null,
  backingUp: false,
  backupResult: null,
  activeOperation: null,
  error: null,
};

/** State for the Configuration → Automation tab. Polls /status/{runId} while a run is in flight
 * (202 Accepted response), then falls back to /last-run once complete. */
export const AutomationStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store, api = inject(AutomationApiService)) => {
    let pollSub: Subscription | null = null;

    const loadSettings = (): void => {
      patchState(store, { loadingSettings: true });
      api.getSettings().subscribe({
        next: (s) => patchState(store, { settings: s, loadingSettings: false }),
        error: () =>
          patchState(store, {
            error: 'Failed to load Automation settings',
            loadingSettings: false,
          }),
      });
    };

    const loadHistory = (page = 1, pageSize = 50): void => {
      patchState(store, { loadingHistory: true });
      api.getHistory(page, pageSize).subscribe({
        next: (h) => patchState(store, { history: h, loadingHistory: false }),
        error: () =>
          patchState(store, { error: 'Failed to load Automation history', loadingHistory: false }),
      });
    };

    const loadTaskStatus = (): void => {
      patchState(store, { loadingTaskStatus: true });
      api.getTaskStatus().subscribe({
        next: (t) => patchState(store, { taskStatus: t, loadingTaskStatus: false }),
        error: () => patchState(store, { taskStatus: null, loadingTaskStatus: false }),
      });
    };

    const pollUntilComplete = (runId: string): void => {
      pollSub?.unsubscribe();
      patchState(store, {
        activeOperation: 'Automation run is in progress; checking the server status…',
      });
      // timer(0, 3000) (not interval(3000)) — fetches status immediately instead of waiting 3s for
      // the first tick, so the UI reflects the just-started run without a blind initial delay.
      const maxPollMinutes = store.settings()?.maxPollMinutes ?? 90;
      pollSub = timer(0, 3000)
        .pipe(takeUntil(timer(maxPollMinutes * 60_000)))
        .pipe(switchMap(() => api.getStatus(runId)))
        .subscribe({
          next: (r) => {
            patchState(store, { lastRun: r });
            if (r.overallStatus !== 'Running') {
              patchState(store, { runInFlight: false, activeOperation: null });
              pollSub?.unsubscribe();
              loadHistory();
            }
          },
          error: () => {
            patchState(store, {
              runInFlight: false,
              activeOperation: 'Automation status polling timed out or failed.',
            });
            pollSub?.unsubscribe();
          },
          complete: () => {
            if (store.runInFlight()) {
              patchState(store, {
                runInFlight: false,
                activeOperation: 'Automation exceeded its configured maximum wait time.',
              });
            }
          },
        });
    };

    return {
      loadSettings,
      loadHistory,
      loadTaskStatus,

      saveSettings(request: UpdateAutomationSettingsRequest): void {
        patchState(store, {
          activeOperation: 'Saving Automation settings…',
          savingSettings: true,
        });
        api.updateSettings(request).subscribe({
          next: (s) =>
            patchState(store, { settings: s, savingSettings: false, activeOperation: null }),
          error: () =>
            patchState(store, {
              error: 'Failed to save Automation settings',
              savingSettings: false,
              activeOperation: 'Saving Automation settings timed out or failed.',
            }),
        });
      },

      loadLastRun(): void {
        patchState(store, { loadingLastRun: true });
        api.getLastRun().subscribe({
          next: (r) => patchState(store, { lastRun: r, loadingLastRun: false }),
          error: () => patchState(store, { lastRun: null, loadingLastRun: false }), // 404 = no runs yet
        });
      },

      loadTimezoneDiagnostics(): void {
        patchState(store, { loadingTimezoneDiagnostics: true });
        api.getTimezoneDiagnostics().subscribe({
          next: (d) =>
            patchState(store, { timezoneDiagnostics: d, loadingTimezoneDiagnostics: false }),
          error: () =>
            patchState(store, { timezoneDiagnostics: null, loadingTimezoneDiagnostics: false }),
        });
      },

      /** "Run Automation Now" — same production path as the scheduled trigger; respects all
       * existing business-time gates (RSI EOD Window, Snapshot eligibility, Value Screener schedule). */
      runNow(): void {
        patchState(store, {
          activeOperation: 'Starting Run Automation Now…',
          runInFlight: true,
        });
        api.runNow().subscribe({
          next: (r) => pollUntilComplete(r.runId),
          error: () =>
            patchState(store, {
              error: 'Failed to start Run Automation Now',
              runInFlight: false,
              activeOperation: 'Run Automation Now timed out or failed.',
            }),
        });
      },

      /** Non-destructive infra check — never persists RSI/signals/snapshot/cash/transactions. */
      testWake(): void {
        patchState(store, { activeOperation: 'Starting Test Wake…', runInFlight: true });
        api.testWake().subscribe({
          next: (r) => pollUntilComplete(r.runId),
          error: () =>
            patchState(store, {
              error: 'Failed to start Test Wake',
              runInFlight: false,
              activeOperation: 'Test Wake timed out or failed.',
            }),
        });
      },

      rotateSecret(): void {
        patchState(store, {
          activeOperation: 'Rotating automation secret…',
          rotatingSecret: true,
        });
        api.rotateSecret().subscribe({
          next: () => {
            patchState(store, { rotatingSecret: false, activeOperation: null });
            loadSettings();
          },
          error: () =>
            patchState(store, {
              error: 'Failed to rotate the automation secret',
              rotatingSecret: false,
              activeOperation: 'Rotating the automation secret timed out or failed.',
            }),
        });
      },

      /** Requests cancellation of the in-flight run. The next poll tick will reflect the resulting
       * Cancelled status once the orchestrator finishes unwinding — this does not stop polling itself. */
      cancelRun(): void {
        patchState(store, {
          activeOperation: 'Stopping the automation run…',
          cancelling: true,
        });
        api.cancel().subscribe({
          next: () => patchState(store, { cancelling: false, activeOperation: null }),
          error: () =>
            patchState(store, {
              error: 'Failed to cancel the automation run',
              cancelling: false,
              activeOperation: 'Stopping the automation run timed out or failed.',
            }),
        });
      },

      /** Deletes all run history rows (this feature's own audit table only — never portfolio/cash data). */
      clearHistory(): void {
        patchState(store, {
          activeOperation: 'Clearing automation history…',
          clearingHistory: true,
        });
        api.clearHistory().subscribe({
          next: () =>
            patchState(store, {
              history: [],
              lastRun: null,
              clearingHistory: false,
              activeOperation: null,
            }),
          error: (err) =>
            patchState(store, {
              error: err?.error?.message ?? 'Failed to clear automation history',
              clearingHistory: false,
              activeOperation: 'Clearing automation history timed out or failed.',
            }),
        });
      },

      /** Registers/updates the Windows Scheduled Task — triggers a single UAC prompt. */
      setup(): void {
        patchState(store, {
          activeOperation: 'Repairing the Windows Scheduled Task…',
          settingUp: true,
        });
        api.setup().subscribe({
          next: () => {
            patchState(store, { settingUp: false, activeOperation: null });
            loadSettings();
            loadTaskStatus();
          },
          error: () =>
            patchState(store, {
              error: 'Setup failed — UAC prompt may have been declined',
              settingUp: false,
              activeOperation: 'Scheduled Task repair timed out or failed.',
            }),
        });
      },

      /** "Fix Missing Data" — replays EOD signals + snapshot + Value Screener for today. Safe to
       * click any number of times: every underlying write is upsert/dedupe-by-day. */
      recoverMissedData(): void {
        patchState(store, {
          activeOperation:
            'Fix Missing Data is running. EOD Signals, Snapshot, and Value Screener are being checked…',
          recovering: true,
          recoveryResult: null,
        });
        api.recoverMissedData().subscribe({
          next: (r) =>
            patchState(store, {
              recoveryResult: r,
              recovering: false,
              activeOperation:
                r.status === 'Completed' ? null : `Fix Missing Data ended with status: ${r.status}.`,
            }),
          error: (err) =>
            patchState(store, {
              error: err?.error?.message ?? 'Failed to fix missing data',
              recovering: false,
              activeOperation: 'Fix Missing Data timed out or failed.',
            }),
        });
      },

      /** On-demand full database backup. If today's scheduled backup already ran, a new timestamped
       * file is added alongside it rather than being skipped. */
      backupNow(): void {
        patchState(store, {
          activeOperation: 'Backup Now is running. Database and SQL scripts are being copied…',
          backingUp: true,
          backupResult: null,
        });
        api.backupNow().subscribe({
          next: (r) =>
            patchState(store, { backupResult: r, backingUp: false, activeOperation: null }),
          error: (err) =>
            patchState(store, {
              error: err?.error?.message ?? 'Failed to back up the database',
              backingUp: false,
              activeOperation: 'Backup Now timed out or failed.',
            }),
        });
      },

      /** Stops the status poll without touching any other state. */
      stopPolling(): void {
        pollSub?.unsubscribe();
        pollSub = null;
      },
    };
  }),
  withHooks({
    onDestroy(store) {
      store.stopPolling();
    },
  }),
);
