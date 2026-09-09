import { inject, Injectable, signal } from '@angular/core';
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
import { AutomationApiService } from './automation-api.service';

/** Signal-based state for the Configuration → Automation tab. Polls /status/{runId} while a run
 * is in flight (202 Accepted response), then falls back to /last-run once complete. */
@Injectable({ providedIn: 'root' })
export class AutomationStateService {
  private readonly api = inject(AutomationApiService);

  readonly settings = signal<AutomationSettingsDto | null>(null);
  readonly lastRun = signal<AutomationRunLogDto | null>(null);
  readonly history = signal<AutomationRunLogDto[]>([]);
  readonly taskStatus = signal<AutomationTaskStatusDto | null>(null);
  readonly timezoneDiagnostics = signal<AutomationTimezoneDiagnosticsDto | null>(null);

  readonly loadingSettings = signal(false);
  readonly savingSettings = signal(false);
  readonly loadingLastRun = signal(false);
  readonly loadingHistory = signal(false);
  readonly loadingTaskStatus = signal(false);
  readonly loadingTimezoneDiagnostics = signal(false);
  readonly runInFlight = signal(false);
  readonly settingUp = signal(false);
  readonly rotatingSecret = signal(false);
  readonly cancelling = signal(false);
  readonly clearingHistory = signal(false);
  readonly recovering = signal(false);
  readonly recoveryResult = signal<MissedDataRecoveryResultDto | null>(null);
  readonly backingUp = signal(false);
  readonly backupResult = signal<DatabaseBackupResultDto | null>(null);
  readonly activeOperation = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  private pollSub: Subscription | null = null;

  loadSettings(): void {
    this.loadingSettings.set(true);
    this.api.getSettings().subscribe({
      next: (s) => {
        this.settings.set(s);
        this.loadingSettings.set(false);
      },
      error: () => {
        this.error.set('Failed to load Automation settings');
        this.loadingSettings.set(false);
      },
    });
  }

  saveSettings(request: UpdateAutomationSettingsRequest): void {
    this.activeOperation.set('Saving Automation settings…');
    this.savingSettings.set(true);
    this.api.updateSettings(request).subscribe({
      next: (s) => {
        this.settings.set(s);
        this.savingSettings.set(false);
        this.activeOperation.set(null);
      },
      error: () => {
        this.error.set('Failed to save Automation settings');
        this.savingSettings.set(false);
        this.activeOperation.set('Saving Automation settings timed out or failed.');
      },
    });
  }

  loadLastRun(): void {
    this.loadingLastRun.set(true);
    this.api.getLastRun().subscribe({
      next: (r) => {
        this.lastRun.set(r);
        this.loadingLastRun.set(false);
      },
      error: () => {
        this.lastRun.set(null);
        this.loadingLastRun.set(false);
      }, // 404 = no runs yet
    });
  }

  loadHistory(page = 1, pageSize = 50): void {
    this.loadingHistory.set(true);
    this.api.getHistory(page, pageSize).subscribe({
      next: (h) => {
        this.history.set(h);
        this.loadingHistory.set(false);
      },
      error: () => {
        this.error.set('Failed to load Automation history');
        this.loadingHistory.set(false);
      },
    });
  }

  loadTaskStatus(): void {
    this.loadingTaskStatus.set(true);
    this.api.getTaskStatus().subscribe({
      next: (t) => {
        this.taskStatus.set(t);
        this.loadingTaskStatus.set(false);
      },
      error: () => {
        this.taskStatus.set(null);
        this.loadingTaskStatus.set(false);
      },
    });
  }

  loadTimezoneDiagnostics(): void {
    this.loadingTimezoneDiagnostics.set(true);
    this.api.getTimezoneDiagnostics().subscribe({
      next: (d) => {
        this.timezoneDiagnostics.set(d);
        this.loadingTimezoneDiagnostics.set(false);
      },
      error: () => {
        this.timezoneDiagnostics.set(null);
        this.loadingTimezoneDiagnostics.set(false);
      },
    });
  }

  /** "Run Automation Now" — same production path as the scheduled trigger; respects all
   * existing business-time gates (RSI EOD Window, Snapshot eligibility, Value Screener schedule). */
  runNow(): void {
    this.activeOperation.set('Starting Run Automation Now…');
    this.runInFlight.set(true);
    this.api.runNow().subscribe({
      next: (r) => this.pollUntilComplete(r.runId),
      error: () => {
        this.error.set('Failed to start Run Automation Now');
        this.runInFlight.set(false);
        this.activeOperation.set('Run Automation Now timed out or failed.');
      },
    });
  }

  /** Non-destructive infra check — never persists RSI/signals/snapshot/cash/transactions. */
  testWake(): void {
    this.activeOperation.set('Starting Test Wake…');
    this.runInFlight.set(true);
    this.api.testWake().subscribe({
      next: (r) => this.pollUntilComplete(r.runId),
      error: () => {
        this.error.set('Failed to start Test Wake');
        this.runInFlight.set(false);
        this.activeOperation.set('Test Wake timed out or failed.');
      },
    });
  }

  rotateSecret(): void {
    this.activeOperation.set('Rotating automation secret…');
    this.rotatingSecret.set(true);
    this.api.rotateSecret().subscribe({
      next: () => {
        this.rotatingSecret.set(false);
        this.activeOperation.set(null);
        this.loadSettings();
      },
      error: () => {
        this.error.set('Failed to rotate the automation secret');
        this.rotatingSecret.set(false);
        this.activeOperation.set('Rotating the automation secret timed out or failed.');
      },
    });
  }

  /** Requests cancellation of the in-flight run. The next poll tick will reflect the resulting
   * Cancelled status once the orchestrator finishes unwinding — this does not stop polling itself. */
  cancelRun(): void {
    this.activeOperation.set('Stopping the automation run…');
    this.cancelling.set(true);
    this.api.cancel().subscribe({
      next: () => {
        this.cancelling.set(false);
        this.activeOperation.set(null);
      },
      error: () => {
        this.error.set('Failed to cancel the automation run');
        this.cancelling.set(false);
        this.activeOperation.set('Stopping the automation run timed out or failed.');
      },
    });
  }

  /** Deletes all run history rows (this feature's own audit table only — never portfolio/cash data). */
  clearHistory(): void {
    this.activeOperation.set('Clearing automation history…');
    this.clearingHistory.set(true);
    this.api.clearHistory().subscribe({
      next: () => {
        this.history.set([]);
        this.lastRun.set(null);
        this.clearingHistory.set(false);
        this.activeOperation.set(null);
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? 'Failed to clear automation history');
        this.clearingHistory.set(false);
        this.activeOperation.set('Clearing automation history timed out or failed.');
      },
    });
  }

  /** Registers/updates the Windows Scheduled Task — triggers a single UAC prompt. */
  setup(): void {
    this.activeOperation.set('Repairing the Windows Scheduled Task…');
    this.settingUp.set(true);
    this.api.setup().subscribe({
      next: () => {
        this.settingUp.set(false);
        this.activeOperation.set(null);
        this.loadSettings();
        this.loadTaskStatus();
      },
      error: () => {
        this.error.set('Setup failed — UAC prompt may have been declined');
        this.settingUp.set(false);
        this.activeOperation.set('Scheduled Task repair timed out or failed.');
      },
    });
  }

  /** "Fix Missing Data" — replays EOD signals + snapshot + Value Screener for today. Safe to
   * click any number of times: every underlying write is upsert/dedupe-by-day. */
  recoverMissedData(): void {
    this.activeOperation.set(
      'Fix Missing Data is running. EOD Signals, Snapshot, and Value Screener are being checked…',
    );
    this.recovering.set(true);
    this.recoveryResult.set(null);
    this.api.recoverMissedData().subscribe({
      next: (r) => {
        this.recoveryResult.set(r);
        this.recovering.set(false);
        this.activeOperation.set(
          r.status === 'Completed' ? null : `Fix Missing Data ended with status: ${r.status}.`,
        );
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? 'Failed to fix missing data');
        this.recovering.set(false);
        this.activeOperation.set('Fix Missing Data timed out or failed.');
      },
    });
  }

  /** On-demand full database backup. If today's scheduled backup already ran, a new timestamped
   * file is added alongside it rather than being skipped. */
  backupNow(): void {
    this.activeOperation.set('Backup Now is running. Database and SQL scripts are being copied…');
    this.backingUp.set(true);
    this.backupResult.set(null);
    this.api.backupNow().subscribe({
      next: (r) => {
        this.backupResult.set(r);
        this.backingUp.set(false);
        this.activeOperation.set(null);
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? 'Failed to back up the database');
        this.backingUp.set(false);
        this.activeOperation.set('Backup Now timed out or failed.');
      },
    });
  }

  private pollUntilComplete(runId: string): void {
    this.pollSub?.unsubscribe();
    this.activeOperation.set('Automation run is in progress; checking the server status…');
    // timer(0, 3000) (not interval(3000)) — fetches status immediately instead of waiting 3s for
    // the first tick, so the UI reflects the just-started run without a blind initial delay.
    const maxPollMinutes = this.settings()?.maxPollMinutes ?? 90;
    this.pollSub = timer(0, 3000)
      .pipe(takeUntil(timer(maxPollMinutes * 60_000)))
      .pipe(switchMap(() => this.api.getStatus(runId)))
      .subscribe({
        next: (r) => {
          this.lastRun.set(r);
          if (r.overallStatus !== 'Running') {
            this.runInFlight.set(false);
            this.activeOperation.set(null);
            this.pollSub?.unsubscribe();
            this.loadHistory();
          }
        },
        error: () => {
          this.runInFlight.set(false);
          this.activeOperation.set('Automation status polling timed out or failed.');
          this.pollSub?.unsubscribe();
        },
        complete: () => {
          if (this.runInFlight()) {
            this.runInFlight.set(false);
            this.activeOperation.set('Automation exceeded its configured maximum wait time.');
          }
        },
      });
  }
}
