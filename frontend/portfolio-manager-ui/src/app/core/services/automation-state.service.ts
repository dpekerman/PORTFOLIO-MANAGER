import { inject, Injectable, signal } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import {
  AutomationRunLogDto,
  AutomationSettingsDto,
  AutomationTaskStatusDto,
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

  readonly loadingSettings = signal(false);
  readonly savingSettings = signal(false);
  readonly loadingLastRun = signal(false);
  readonly loadingHistory = signal(false);
  readonly loadingTaskStatus = signal(false);
  readonly runInFlight = signal(false);
  readonly settingUp = signal(false);
  readonly rotatingSecret = signal(false);
  readonly error = signal<string | null>(null);

  private pollSub: Subscription | null = null;

  loadSettings(): void {
    this.loadingSettings.set(true);
    this.api.getSettings().subscribe({
      next: (s) => { this.settings.set(s); this.loadingSettings.set(false); },
      error: () => { this.error.set('Failed to load Automation settings'); this.loadingSettings.set(false); },
    });
  }

  saveSettings(request: UpdateAutomationSettingsRequest): void {
    this.savingSettings.set(true);
    this.api.updateSettings(request).subscribe({
      next: (s) => { this.settings.set(s); this.savingSettings.set(false); },
      error: () => { this.error.set('Failed to save Automation settings'); this.savingSettings.set(false); },
    });
  }

  loadLastRun(): void {
    this.loadingLastRun.set(true);
    this.api.getLastRun().subscribe({
      next: (r) => { this.lastRun.set(r); this.loadingLastRun.set(false); },
      error: () => { this.lastRun.set(null); this.loadingLastRun.set(false); }, // 404 = no runs yet
    });
  }

  loadHistory(page = 1, pageSize = 50): void {
    this.loadingHistory.set(true);
    this.api.getHistory(page, pageSize).subscribe({
      next: (h) => { this.history.set(h); this.loadingHistory.set(false); },
      error: () => { this.error.set('Failed to load Automation history'); this.loadingHistory.set(false); },
    });
  }

  loadTaskStatus(): void {
    this.loadingTaskStatus.set(true);
    this.api.getTaskStatus().subscribe({
      next: (t) => { this.taskStatus.set(t); this.loadingTaskStatus.set(false); },
      error: () => { this.taskStatus.set(null); this.loadingTaskStatus.set(false); },
    });
  }

  /** "Run Automation Now" — same production path as the scheduled trigger; respects all
   * existing business-time gates (RSI EOD Window, Snapshot eligibility, Value Screener schedule). */
  runNow(): void {
    this.runInFlight.set(true);
    this.api.runNow().subscribe({
      next: (r) => this.pollUntilComplete(r.runId),
      error: () => { this.error.set('Failed to start Run Automation Now'); this.runInFlight.set(false); },
    });
  }

  /** Non-destructive infra check — never persists RSI/signals/snapshot/cash/transactions. */
  testWake(): void {
    this.runInFlight.set(true);
    this.api.testWake().subscribe({
      next: (r) => this.pollUntilComplete(r.runId),
      error: () => { this.error.set('Failed to start Test Wake'); this.runInFlight.set(false); },
    });
  }

  rotateSecret(): void {
    this.rotatingSecret.set(true);
    this.api.rotateSecret().subscribe({
      next: () => { this.rotatingSecret.set(false); this.loadSettings(); },
      error: () => { this.error.set('Failed to rotate the automation secret'); this.rotatingSecret.set(false); },
    });
  }

  /** Registers/updates the Windows Scheduled Task — triggers a single UAC prompt. */
  setup(): void {
    this.settingUp.set(true);
    this.api.setup().subscribe({
      next: () => { this.settingUp.set(false); this.loadSettings(); this.loadTaskStatus(); },
      error: () => { this.error.set('Setup failed — UAC prompt may have been declined'); this.settingUp.set(false); },
    });
  }

  private pollUntilComplete(runId: string): void {
    this.pollSub?.unsubscribe();
    this.pollSub = interval(3000)
      .pipe(switchMap(() => this.api.getStatus(runId)))
      .subscribe({
        next: (r) => {
          this.lastRun.set(r);
          if (r.overallStatus !== 'Running') {
            this.runInFlight.set(false);
            this.pollSub?.unsubscribe();
            this.loadHistory();
          }
        },
        error: () => { this.runInFlight.set(false); this.pollSub?.unsubscribe(); },
      });
  }
}
