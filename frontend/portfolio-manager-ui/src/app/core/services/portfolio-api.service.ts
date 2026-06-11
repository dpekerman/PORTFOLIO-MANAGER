import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AddPortfolioItemRequest,
  FinnhubSearchResult,
  PortfolioItem,
  PortfolioSummary,
  ScannerResponse,
  StockQuote,
  UpdatePortfolioItemRequest,
} from '../models/portfolio.models';

@Injectable({ providedIn: 'root' })
export class PortfolioApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  // ── Portfolio CRUD ──────────────────────────────────────────────────────────
  getPortfolio(): Observable<PortfolioItem[]> {
    return this.http.get<PortfolioItem[]>(`${this.base}/portfolio`);
  }

  addItem(request: AddPortfolioItemRequest): Observable<PortfolioItem> {
    return this.http.post<PortfolioItem>(`${this.base}/portfolio`, request);
  }

  updateItem(id: number, request: UpdatePortfolioItemRequest): Observable<PortfolioItem> {
    return this.http.put<PortfolioItem>(`${this.base}/portfolio/${id}`, request);
  }

  deleteItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/portfolio/${id}`);
  }

  // ── Stock Quotes ────────────────────────────────────────────────────────────
  getAllQuotes(): Observable<PortfolioSummary[]> {
    return this.http.get<PortfolioSummary[]>(`${this.base}/stocks/quotes`);
  }

  getQuote(symbol: string): Observable<StockQuote> {
    return this.http.get<StockQuote>(`${this.base}/stocks/quote/${symbol}`);
  }

  searchSymbols(query: string): Observable<FinnhubSearchResult[]> {
    const params = new HttpParams().set('q', query);
    return this.http.get<FinnhubSearchResult[]>(`${this.base}/stocks/search`, { params });
  }

  // ── RSI Scanner ─────────────────────────────────────────────────────────────
  /** @param force true = bypass server-side 4-minute cache (use on manual refresh only) */
  getRsiScan(force = false): Observable<ScannerResponse> {
    const url = force ? `${this.base}/scanner/rsi?force=true` : `${this.base}/scanner/rsi`;
    return this.http.get<ScannerResponse>(url);
  }
}
