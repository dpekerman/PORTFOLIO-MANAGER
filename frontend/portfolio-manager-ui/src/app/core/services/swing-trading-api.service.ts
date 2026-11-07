import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  BrokerStopTransitionRequest,
  JournalRow,
  OpenTradesResponse,
  RiskPosition,
  SaveBrokerStopRequest,
  SaveRiskPlanRequest,
} from '../models/swing-trading.models';

/** HTTP only for the Swing Trading workspace and the shared Manage Risk dialog. Holds no state. */
@Injectable({ providedIn: 'root' })
export class SwingTradingApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  getOpenTrades(): Observable<OpenTradesResponse> {
    return this.http.get<OpenTradesResponse>(`${this.base}/swing-trading/open-trades`);
  }

  getJournal(): Observable<JournalRow[]> {
    return this.http.get<JournalRow[]>(`${this.base}/swing-trading/journal`);
  }

  getRiskPosition(account: string, symbol: string): Observable<RiskPosition> {
    const params = new HttpParams().set('account', account).set('symbol', symbol);
    return this.http.get<RiskPosition>(`${this.base}/risk/position`, { params });
  }

  savePlan(request: SaveRiskPlanRequest): Observable<RiskPosition> {
    return this.http.put<RiskPosition>(`${this.base}/risk/plan`, request);
  }

  addBrokerStop(request: SaveBrokerStopRequest): Observable<RiskPosition> {
    return this.http.post<RiskPosition>(`${this.base}/risk/broker-stops`, request);
  }

  updateBrokerStop(id: number, request: SaveBrokerStopRequest): Observable<RiskPosition> {
    return this.http.put<RiskPosition>(`${this.base}/risk/broker-stops/${id}`, request);
  }

  transitionBrokerStop(id: number, request: BrokerStopTransitionRequest): Observable<RiskPosition> {
    return this.http.post<RiskPosition>(`${this.base}/risk/broker-stops/${id}/status`, request);
  }
}
