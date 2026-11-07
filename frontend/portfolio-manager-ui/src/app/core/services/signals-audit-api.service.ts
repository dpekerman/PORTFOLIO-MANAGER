import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AuditedActionEntry,
  FinalActionCatalogItem,
  SignalAuditDetail,
  SignalAuditPagedResponse,
  SignalAuditRow,
  SignalAuditSummary,
  SignalEffectivenessQueryParams,
  SignalEffectivenessResult,
  SignalMyAction,
  SignalsAuditQueryParams,
  SignalsAuditSettings,
  SignalTransactionCandidate,
  SignalTypeOption,
} from '../models/portfolio.models';

/** HTTP wrapper for the Signals Audit endpoints; holds no state. */
@Injectable({ providedIn: 'root' })
export class SignalsAuditApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/signals-audit';

  getSettings(): Observable<SignalsAuditSettings> {
    return this.http.get<SignalsAuditSettings>(`${this.base}/settings`);
  }

  saveSettings(
    priorityEnabled: boolean,
    priorityThreshold: number,
    finalActionEnabled?: boolean,
    entries?: AuditedActionEntry[],
  ): Observable<SignalsAuditSettings> {
    return this.http.put<SignalsAuditSettings>(`${this.base}/settings`, {
      priorityEnabled,
      priorityThreshold,
      finalActionEnabled,
      entries,
    });
  }

  getFinalActionCatalog(): Observable<FinalActionCatalogItem[]> {
    return this.http.get<FinalActionCatalogItem[]>(`${this.base}/final-action-catalog`);
  }

  getSignalTypes(): Observable<SignalTypeOption[]> {
    return this.http.get<SignalTypeOption[]>(`${this.base}/signal-types`);
  }

  query(p: SignalsAuditQueryParams): Observable<SignalAuditPagedResponse> {
    return this.http.get<SignalAuditPagedResponse>(this.base, {
      params: this.toParams(p, true),
    });
  }

  summary(p: SignalsAuditQueryParams): Observable<SignalAuditSummary> {
    // Effectiveness has its own endpoint and window; the summary skips that calculation.
    return this.http.get<SignalAuditSummary>(`${this.base}/summary`, {
      params: this.toParams(p, false).set('includeEffectiveness', false),
    });
  }

  effectiveness(p: SignalEffectivenessQueryParams): Observable<SignalEffectivenessResult> {
    let params = new HttpParams().set('window', p.window);
    for (const t of p.signalType) params = params.append('signalType', t);
    if (p.source !== 'All') params = params.set('source', p.source);
    for (const r of p.role) params = params.append('role', r);
    if (p.includeBaselineInStats) params = params.set('includeBaselineInStats', true);
    return this.http.get<SignalEffectivenessResult>(`${this.base}/effectiveness`, { params });
  }

  detail(id: number): Observable<SignalAuditDetail> {
    return this.http.get<SignalAuditDetail>(`${this.base}/${id}`);
  }

  transactionCandidates(id: number): Observable<SignalTransactionCandidate[]> {
    return this.http.get<SignalTransactionCandidate[]>(`${this.base}/${id}/transaction-candidates`);
  }

  saveAction(
    id: number,
    myAction: SignalMyAction,
    note: string | null,
    linkedTransactionId: number | null,
  ): Observable<SignalAuditRow> {
    return this.http.put<SignalAuditRow>(`${this.base}/${id}/action`, {
      myAction,
      note,
      linkedTransactionId,
    });
  }

  dismiss(id: number, note: string | null): Observable<SignalAuditRow> {
    return this.http.post<SignalAuditRow>(`${this.base}/${id}/dismiss`, { note });
  }

  private toParams(p: SignalsAuditQueryParams, paging: boolean): HttpParams {
    let params = new HttpParams();
    if (p.ticker.trim()) params = params.set('ticker', p.ticker.trim());
    for (const t of p.signalType) params = params.append('signalType', t);
    if (p.source !== 'All') params = params.set('source', p.source);
    for (const r of p.role) params = params.append('role', r);
    if (p.myAction !== 'All') params = params.set('myAction', p.myAction);
    if (p.origin !== 'All') params = params.set('origin', p.origin);
    if (p.includeBaselineInStats) params = params.set('includeBaselineInStats', true);
    if (p.includeUnaudited) params = params.set('includeUnaudited', true);
    if (p.status !== 'All') params = params.set('status', p.status);
    if (p.from) params = params.set('from', p.from);
    if (p.to) params = params.set('to', p.to);
    if (paging) {
      params = params
        .set('sortBy', p.sortBy)
        .set('sortDir', p.sortDir)
        .set('page', p.page)
        .set('pageSize', p.pageSize);
    }
    return params;
  }
}
