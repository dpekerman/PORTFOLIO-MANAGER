import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  EvaluationResult,
  FeedbackRating,
  NewsSource,
  NewsSourceRequest,
  PreMarketConfig,
  PreMarketRunDetail,
  PreMarketRunSummary,
  PreMarketStatus,
  RunPreMarketRequest,
} from '../models/premarket.models';

/** HttpClient wrapper for the AI pre-market agent. Holds no state. */
@Injectable({ providedIn: 'root' })
export class PreMarketApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/premarket';

  getConfig(): Observable<PreMarketConfig> {
    return this.http.get<PreMarketConfig>(`${this.base}/config`);
  }
  saveConfig(cfg: PreMarketConfig): Observable<PreMarketConfig> {
    return this.http.put<PreMarketConfig>(`${this.base}/config`, cfg);
  }
  getStatus(): Observable<PreMarketStatus> {
    return this.http.get<PreMarketStatus>(`${this.base}/status`);
  }
  run(req: RunPreMarketRequest): Observable<{ id: number; status: string; emailStatus: string; emailMessage: string | null; error: string | null }> {
    return this.http.post<{ id: number; status: string; emailStatus: string; emailMessage: string | null; error: string | null }>(`${this.base}/run`, req);
  }
  listRuns(): Observable<PreMarketRunSummary[]> {
    return this.http.get<PreMarketRunSummary[]>(`${this.base}/runs`);
  }
  getRun(id: number): Observable<PreMarketRunDetail> {
    return this.http.get<PreMarketRunDetail>(`${this.base}/runs/${id}`);
  }
  getLatest(): Observable<PreMarketRunDetail | null> {
    return this.http.get<PreMarketRunDetail | null>(`${this.base}/runs/latest`);
  }
  getEvaluation(id: number): Observable<EvaluationResult> {
    return this.http.get<EvaluationResult>(`${this.base}/runs/${id}/evaluation`);
  }
  resend(id: number): Observable<{ ok: boolean; message: string }> {
    return this.http.post<{ ok: boolean; message: string }>(`${this.base}/runs/${id}/resend`, {});
  }
  testEmail(): Observable<{ ok: boolean; message: string; recipients: number }> {
    return this.http.post<{ ok: boolean; message: string; recipients: number }>(`${this.base}/test-email`, {});
  }
  feedback(runId: number, rating: FeedbackRating, symbol: string | null): Observable<void> {
    return this.http.post<void>(`${this.base}/runs/${runId}/feedback`, { rating, symbol, comment: null });
  }
  listSources(): Observable<NewsSource[]> {
    return this.http.get<NewsSource[]>(`${this.base}/sources`);
  }
  addSource(req: NewsSourceRequest): Observable<NewsSource> {
    return this.http.post<NewsSource>(`${this.base}/sources`, req);
  }
  updateSource(id: number, req: NewsSourceRequest): Observable<NewsSource> {
    return this.http.put<NewsSource>(`${this.base}/sources/${id}`, req);
  }
  archiveSource(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/sources/${id}`);
  }
  approveSource(id: number): Observable<NewsSource> {
    return this.http.post<NewsSource>(`${this.base}/sources/${id}/approve`, {});
  }
  testSource(id: number): Observable<{ ok: boolean; error: string | null; articles: number; durationMs: number }> {
    return this.http.post<{ ok: boolean; error: string | null; articles: number; durationMs: number }>(`${this.base}/sources/${id}/test`, {});
  }
}
