import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AutomationRunLogDto,
  AutomationSettingsDto,
  AutomationTaskStatusDto,
  AutomationTriggerResponseDto,
  UpdateAutomationSettingsRequest,
} from '../models/portfolio.models';

/** HTTP transport only, zero state — see AutomationStateService for signals/polling. */
@Injectable({ providedIn: 'root' })
export class AutomationApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/automation';

  getSettings(): Observable<AutomationSettingsDto> {
    return this.http.get<AutomationSettingsDto>(`${this.base}/settings`);
  }

  updateSettings(request: UpdateAutomationSettingsRequest): Observable<AutomationSettingsDto> {
    return this.http.put<AutomationSettingsDto>(`${this.base}/settings`, request);
  }

  getLastRun(): Observable<AutomationRunLogDto> {
    return this.http.get<AutomationRunLogDto>(`${this.base}/last-run`);
  }

  getHistory(page = 1, pageSize = 50): Observable<AutomationRunLogDto[]> {
    return this.http.get<AutomationRunLogDto[]>(`${this.base}/history`, {
      params: { page, pageSize },
    });
  }

  getStatus(runId: string): Observable<AutomationRunLogDto> {
    return this.http.get<AutomationRunLogDto>(`${this.base}/status/${runId}`);
  }

  /** "Run Automation Now" — respects all existing business-time gates; never bypasses them. */
  runNow(): Observable<AutomationTriggerResponseDto> {
    return this.http.post<AutomationTriggerResponseDto>(`${this.base}/run-now`, {});
  }

  /** Non-destructive infra check only — never touches RSI/signals/snapshot/cash/transactions. */
  testWake(): Observable<AutomationTriggerResponseDto> {
    return this.http.post<AutomationTriggerResponseDto>(`${this.base}/test-wake`, {});
  }

  rotateSecret(): Observable<{ rotated: boolean }> {
    return this.http.post<{ rotated: boolean }>(`${this.base}/rotate-secret`, {});
  }

  getTaskStatus(): Observable<AutomationTaskStatusDto> {
    return this.http.get<AutomationTaskStatusDto>(`${this.base}/task-status`);
  }

  setup(): Observable<{ started: boolean; elevated: boolean }> {
    return this.http.post<{ started: boolean; elevated: boolean }>(`${this.base}/setup`, {});
  }
}
