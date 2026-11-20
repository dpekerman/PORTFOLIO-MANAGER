import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  IndexConstituent,
  IndexConstituentRequest,
  OpportunityConfig,
  OpportunityDetail,
  OpportunityReference,
  OpportunityScanHistory,
  OpportunitySchedule,
  OpportunityScheduleRequest,
  OpportunitySnapshot,
  SectorBenchmarkMapping,
  SectorBenchmarkRequest,
} from '../models/opportunities.models';

const BASE = '/api/opportunities';

/** PM-029 Trading Opportunities endpoints. HTTP only, no state. */
@Injectable({ providedIn: 'root' })
export class OpportunitiesApiService {
  private readonly http = inject(HttpClient);

  /** Latest stored snapshot (DB only, no market calls). */
  getLatest(): Observable<OpportunitySnapshot> {
    return this.http.get<OpportunitySnapshot>(BASE);
  }

  /** Recent persisted scan results and the latest failed/in-flight attempt. */
  getHistory(): Observable<OpportunityScanHistory[]> {
    return this.http.get<OpportunityScanHistory[]>(`${BASE}/history`);
  }

  /** Drawer detail from a specific snapshot. 410 = that snapshot has expired. */
  getDetail(symbol: string, snapshotId: number | null): Observable<OpportunityDetail> {
    let params = new HttpParams();
    if (snapshotId !== null) params = params.set('snapshotId', snapshotId);
    return this.http.get<OpportunityDetail>(`${BASE}/${encodeURIComponent(symbol)}`, { params });
  }

  /** Builds a new snapshot. 409 = a refresh is already running for this user. */
  refresh(): Observable<OpportunitySnapshot> {
    return this.http.post<OpportunitySnapshot>(`${BASE}/refresh`, {});
  }

  getConfig(): Observable<OpportunityConfig> {
    return this.http.get<OpportunityConfig>(`${BASE}/config`);
  }

  getSchedule(): Observable<OpportunitySchedule> {
    return this.http.get<OpportunitySchedule>(`${BASE}/schedule`);
  }

  saveSchedule(request: OpportunityScheduleRequest): Observable<OpportunitySchedule> {
    return this.http.put<OpportunitySchedule>(`${BASE}/schedule`, request);
  }

  getReference(): Observable<OpportunityReference> {
    return this.http.get<OpportunityReference>(`${BASE}/reference`);
  }

  addConstituent(request: IndexConstituentRequest): Observable<IndexConstituent> {
    return this.http.post<IndexConstituent>(`${BASE}/reference/constituents`, request);
  }

  retireConstituent(id: number, effectiveTo: string | null): Observable<IndexConstituent> {
    let params = new HttpParams();
    if (effectiveTo) params = params.set('effectiveTo', effectiveTo);
    return this.http.post<IndexConstituent>(`${BASE}/reference/constituents/${id}/retire`, {}, { params });
  }

  /** Undo of a retire. 409 = the symbol is already a current constituent. */
  restoreConstituent(id: number): Observable<IndexConstituent> {
    return this.http.post<IndexConstituent>(`${BASE}/reference/constituents/${id}/restore`, {});
  }

  upsertBenchmark(request: SectorBenchmarkRequest): Observable<SectorBenchmarkMapping> {
    return this.http.put<SectorBenchmarkMapping>(`${BASE}/reference/benchmarks`, request);
  }
}
