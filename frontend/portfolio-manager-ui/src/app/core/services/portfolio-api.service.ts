import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AddManualPositionRequest,
  AddPortfolioItemRequest,
  AdhocSessionPayload,
  AdhocSessionResponse,
  PortfolioItem,
  PortfolioSummary,
  RsiScanResult,
  ScannerResponse,
  SectorIndustryLists,
  StockQuote,
  SymbolSearchResult,
  UpdatePortfolioItemRequest,
  ValueScreenerRequest,
  ValueScreenerResult,
  WatchlistSummary,
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

  addManualPosition(request: AddManualPositionRequest): Observable<PortfolioItem> {
    return this.http.post<PortfolioItem>(`${this.base}/portfolio/manual`, request);
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

  searchSymbols(query: string): Observable<SymbolSearchResult[]> {
    const params = new HttpParams().set('q', query);
    return this.http.get<SymbolSearchResult[]>(`${this.base}/stocks/search`, { params });
  }

  /** Fetches sector/industry from Yahoo Finance for all portfolio items and persists. */
  refreshSectors(): Observable<{ updated: number }> {
    return this.http.post<{ updated: number }>(`${this.base}/portfolio/refresh-sectors`, {});
  }

  // ── Watchlist ───────────────────────────────────────────────────────────────
  getWatchlist(): Observable<WatchlistSummary[]> {
    return this.http.get<WatchlistSummary[]>(`${this.base}/watchlist`);
  }

  addWatchlistItem(
    symbol: string,
    notes = '',
  ): Observable<{ id: number; symbol: string; notes: string; addedAt: string }> {
    return this.http.post<{ id: number; symbol: string; notes: string; addedAt: string }>(
      `${this.base}/watchlist`,
      { symbol, notes },
    );
  }

  deleteWatchlistItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/watchlist/${id}`);
  }

  // ── RSI Scanner ─────────────────────────────────────────────────────────────
  /** @param force true = bypass server-side 4-minute cache (use on manual refresh only) */
  getRsiScan(
    force = false,
    oversold = 30,
    overbought = 75,
    logicMode = 'Legacy',
  ): Observable<ScannerResponse> {
    let params = new HttpParams()
      .set('oversold', oversold)
      .set('overbought', overbought)
      .set('logicMode', logicMode);
    if (force) params = params.set('force', 'true');
    return this.http.get<ScannerResponse>(`${this.base}/scanner/rsi`, { params });
  }

  /** Ad-hoc analysis: analyzes up to 20 user-supplied symbols live. */
  analyzeSymbols(
    symbols: string[],
    oversold = 30,
    overbought = 75,
    logicMode = 'Legacy',
  ): Observable<RsiScanResult[]> {
    return this.http.post<RsiScanResult[]>(`${this.base}/scanner/analyze`, {
      symbols,
      oversoldThreshold: oversold,
      overboughtThreshold: overbought,
      logicMode,
    });
  }

  /** Invalidate all server-side RSI scan cache entries (call after config/threshold change). */
  clearRsiCache(): Observable<void> {
    return this.http.delete<void>(`${this.base}/scanner/rsi/cache`);
  }

  // ── EOD Window Settings ──────────────────────────────────────────────────────

  /** Get current EOD confirmation window settings from the backend. */
  getEodSettings(): Observable<{
    eodWindowStart: string;
    eodWindowEnd: string;
    eodWindowEnabled: boolean;
  }> {
    return this.http.get<{
      eodWindowStart: string;
      eodWindowEnd: string;
      eodWindowEnabled: boolean;
    }>(`${this.base}/scanner/eod-settings`);
  }

  /** Update the EOD confirmation window settings on the backend (runtime — no restart needed). */
  updateEodSettings(settings: {
    eodWindowStart: string;
    eodWindowEnd: string;
    eodWindowEnabled: boolean;
  }): Observable<void> {
    return this.http.put<void>(`${this.base}/scanner/eod-settings`, settings);
  }

  /** Check whether the EOD window is currently active on the server. */
  getEodWindowStatus(): Observable<{
    isActive: boolean;
    eodWindowStart: string;
    eodWindowEnd: string;
    eodWindowEnabled: boolean;
    serverTimeUtc: string;
  }> {
    return this.http.get<{
      isActive: boolean;
      eodWindowStart: string;
      eodWindowEnd: string;
      eodWindowEnabled: boolean;
      serverTimeUtc: string;
    }>(`${this.base}/scanner/eod-window-active`);
  }

  // ── Ad-Hoc Session Persistence ──────────────────────────────────────────────

  /** Save the current ad-hoc analysis session to the database. */
  saveAdhocSession(payload: AdhocSessionPayload): Observable<void> {
    return this.http.post<void>(`${this.base}/scanner/adhoc-session`, payload);
  }

  /** Load the last saved ad-hoc analysis session from the database. */
  loadAdhocSession(): Observable<AdhocSessionResponse> {
    return this.http.get<AdhocSessionResponse>(`${this.base}/scanner/adhoc-session`);
  }

  // ── Value Screener ──────────────────────────────────────────────────────────
  runValueScreener(request: ValueScreenerRequest): Observable<ValueScreenerResult[]> {
    return this.http.post<ValueScreenerResult[]>(`${this.base}/valuescreener/analyze`, request);
  }

  // ── Sector / Industry Lists ─────────────────────────────────────────────────
  getSectorIndustryLists(): Observable<SectorIndustryLists> {
    return this.http.get<SectorIndustryLists>(`${this.base}/sector-industry`);
  }

  saveSectorIndustryLists(lists: SectorIndustryLists): Observable<SectorIndustryLists> {
    return this.http.put<SectorIndustryLists>(`${this.base}/sector-industry`, lists);
  }
}
