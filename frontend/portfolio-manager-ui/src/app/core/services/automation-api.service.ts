import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, timeout } from 'rxjs';
import {
  AutomationRunLogDto,
  AutomationSettingsDto,
  AutomationTaskStatusDto,
  AutomationTimezoneDiagnosticsDto,
  AutomationTriggerResponseDto,
  DatabaseBackupResultDto,
  MissedDataRecoveryResultDto,
  UpdateAutomationSettingsRequest,
} from '../models/portfolio.models';

/** HTTP transport only, zero state — see AutomationStateService for signals/polling. */
@Injectable({ providedIn: 'root' })
export class AutomationApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/automation';
  private readonly defaultTimeoutMs: number = 30_000; // 30 seconds
  private readonly setupTimeoutMs: number = 2 * 60_000; // 2 minutes
  private readonly recoveryTimeoutMs: number = 16 * 60_000; // 16 minutes
  private readonly backupTimeoutMs: number = 6 * 60_000; // 6 minutes

  private withTimeout<T>(request: Observable<T>, timeoutMs = this.defaultTimeoutMs): Observable<T> {
    return request.pipe(timeout({ first: timeoutMs }));
  }

  getSettings(): Observable<AutomationSettingsDto> {
    return this.withTimeout(this.http.get<AutomationSettingsDto>(`${this.base}/settings`));
  }

  updateSettings(request: UpdateAutomationSettingsRequest): Observable<AutomationSettingsDto> {
    return this.withTimeout(this.http.put<AutomationSettingsDto>(`${this.base}/settings`, request));
  }

  getLastRun(): Observable<AutomationRunLogDto> {
    return this.withTimeout(this.http.get<AutomationRunLogDto>(`${this.base}/last-run`));
  }

  getHistory(page = 1, pageSize = 50): Observable<AutomationRunLogDto[]> {
    return this.withTimeout(
      this.http.get<AutomationRunLogDto[]>(`${this.base}/history`, {
        params: { page, pageSize },
      }),
    );
  }

  /** Deletes all AutomationRunLog rows (this feature's own audit table only). */
  clearHistory(): Observable<{ cleared: boolean; count: number }> {
    return this.withTimeout(
      this.http.delete<{ cleared: boolean; count: number }>(`${this.base}/history`),
    );
  }

  getStatus(runId: string): Observable<AutomationRunLogDto> {
    return this.withTimeout(this.http.get<AutomationRunLogDto>(`${this.base}/status/${runId}`));
  }

  /** "Run Automation Now" — respects all existing business-time gates; never bypasses them. */
  runNow(): Observable<AutomationTriggerResponseDto> {
    return this.withTimeout(
      this.http.post<AutomationTriggerResponseDto>(`${this.base}/run-now`, {}),
    );
  }

  /** Non-destructive infra check only — never touches RSI/signals/snapshot/cash/transactions. */
  testWake(): Observable<AutomationTriggerResponseDto> {
    return this.withTimeout(
      this.http.post<AutomationTriggerResponseDto>(`${this.base}/test-wake`, {}),
    );
  }

  /** Requests cancellation of the in-flight run, if any. */
  cancel(): Observable<{ cancelled: boolean }> {
    return this.withTimeout(this.http.post<{ cancelled: boolean }>(`${this.base}/cancel`, {}));
  }

  rotateSecret(): Observable<{ rotated: boolean }> {
    return this.withTimeout(this.http.post<{ rotated: boolean }>(`${this.base}/rotate-secret`, {}));
  }

  getTaskStatus(): Observable<AutomationTaskStatusDto> {
    return this.withTimeout(this.http.get<AutomationTaskStatusDto>(`${this.base}/task-status`));
  }

  /** Diagnostic/display-only comparison of business (Eastern) vs Windows local timezone, plus a
   * live check of whether the Scheduled Task's real next-run instant still matches Eastern Time. */
  getTimezoneDiagnostics(): Observable<AutomationTimezoneDiagnosticsDto> {
    return this.withTimeout(
      this.http.get<AutomationTimezoneDiagnosticsDto>(`${this.base}/timezone-diagnostics`),
    );
  }

  setup(): Observable<{ started: boolean; elevated: boolean }> {
    return this.withTimeout(
      this.http.post<{ started: boolean; elevated: boolean }>(`${this.base}/setup`, {}),
      this.setupTimeoutMs,
    );
  }

  /** "Fix Missing Data" — replays EOD signals + snapshot + Value Screener for today. Safe to call
   * any number of times; every underlying write is upsert/dedupe-by-day. */
  recoverMissedData(): Observable<MissedDataRecoveryResultDto> {
    return this.withTimeout(
      this.http.post<MissedDataRecoveryResultDto>(`${this.base}/recover-missed-data`, {}),
      this.recoveryTimeoutMs,
    );
  }

  /** On-demand full database backup. If today's scheduled backup already ran, a new timestamped
   * file is created alongside it rather than being skipped. */
  backupNow(): Observable<DatabaseBackupResultDto> {
    return this.withTimeout(
      this.http.post<DatabaseBackupResultDto>(`${this.base}/backup-now`, {}),
      this.backupTimeoutMs,
    );
  }
}
