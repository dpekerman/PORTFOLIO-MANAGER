import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuditFilters, AuditPagedResponse, AuditQueryParams } from '../models/portfolio.models';

@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/audit';

  query(p: AuditQueryParams): Observable<AuditPagedResponse> {
    return this.http.get<AuditPagedResponse>(this.base, { params: this.toParams(p) });
  }

  /** CSV of everything matching the filters (server caps at 10,000 rows); paging is ignored. */
  exportCsv(p: AuditQueryParams): Observable<Blob> {
    return this.http.get(`${this.base}/export`, { params: this.toParams(p), responseType: 'blob' });
  }

  getFilters(): Observable<AuditFilters> {
    return this.http.get<AuditFilters>(`${this.base}/filters`);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  deleteMany(ids: number[]): Observable<{ deleted: number }> {
    return this.http.post<{ deleted: number }>(`${this.base}/delete-batch`, { ids });
  }

  private toParams(p: AuditQueryParams): HttpParams {
    let params = new HttpParams()
      .set('page', p.page)
      .set('pageSize', p.pageSize)
      .set('sortBy', p.sortBy)
      .set('sortDir', p.sortDir);
    if (p.from) params = params.set('from', p.from);
    if (p.to) params = params.set('to', p.to);
    for (const c of p.category) params = params.append('category', c);
    for (const a of p.action) params = params.append('action', a);
    if (p.source) params = params.set('source', p.source);
    if (p.userId) params = params.set('userId', p.userId);
    if (p.q.trim()) params = params.set('q', p.q.trim());
    return params;
  }
}
