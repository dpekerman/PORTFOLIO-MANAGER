import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface NotificationRecipients {
  emails: string[];
}

export interface TestEmailResult {
  success: boolean;
  message?: string;
  error?: string;
}

export interface ScanNowResult {
  triggered: boolean;
  confirmedSignals?: number;
  recipientCount?: number;
  message?: string;
  reason?: string;
}

export interface NotificationStatus {
  trackedSignals: number;
  recipientCount: number;
  recipients: string[];
}

@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/notification';

  getRecipients(): Observable<NotificationRecipients> {
    return this.http.get<NotificationRecipients>(`${this.base}/recipients`);
  }

  updateRecipients(emails: string[]): Observable<NotificationRecipients> {
    return this.http.put<NotificationRecipients>(`${this.base}/recipients`, { emails });
  }

  sendTestEmail(toEmail: string): Observable<TestEmailResult> {
    return this.http.post<TestEmailResult>(`${this.base}/send-test`, { toEmail });
  }

  scanAndNotifyNow(): Observable<ScanNowResult> {
    return this.http.post<ScanNowResult>(`${this.base}/scan-now`, {});
  }

  getStatus(): Observable<NotificationStatus> {
    return this.http.get<NotificationStatus>(`${this.base}/status`);
  }
}
