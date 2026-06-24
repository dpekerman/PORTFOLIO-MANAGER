import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AddCashItemRequest,
  AddManualPositionRequest,
  AddOptionItemRequest,
  AddPortfolioItemRequest,
  AdhocSessionPayload,
  AdhocSessionResponse,
  CashItem,
  DailySignalPagedResponse,
  EodSignalFilters,
  EodSignalsMeta,
  OptionItem,
  OptionTechnicalData,
  PortfolioItem,
  PortfolioSummary,
  RsiScanResult,
  ScannerResponse,
  SectorIndustryLists,
  StockQuote,
  SymbolSearchResult,
  UpdateCashItemRequest,
  UpdateOptionItemRequest,
  UpdatePortfolioItemRequest,
  ValueScreenerRequest,
  ValueScreenerResult,
  WatchlistSummary,
  YesterdayEodResponse,
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
    role = 'Strategic',
  ): Observable<{ id: number; symbol: string; notes: string; addedAt: string; role: string }> {
    return this.http.post<{
      id: number;
      symbol: string;
      notes: string;
      addedAt: string;
      role: string;
    }>(`${this.base}/watchlist`, { symbol, notes, role });
  }

  deleteWatchlistItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/watchlist/${id}`);
  }

  updateWatchlistRole(id: number, role: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/watchlist/${id}/role`, { role });
  }

  updatePortfolioHoldingRole(id: number, holdingRole: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/portfolio/${id}/holding-role`, { holdingRole });
  }

  updatePortfolioNotes(id: number, notes: string | null): Observable<void> {
    return this.http.patch<void>(`${this.base}/portfolio/${id}/notes`, { notes });
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

  /** Returns the most-recently persisted EOD CONFIRM signals plus morning-window metadata. */
  getYesterdayEod(): Observable<YesterdayEodResponse> {
    return this.http.get<YesterdayEodResponse>(`${this.base}/scanner/yesterday-eod`);
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

  // ── Cash CRUD ───────────────────────────────────────────────────────────────
  getCashItems(): Observable<CashItem[]> {
    return this.http.get<CashItem[]>(`${this.base}/cash`);
  }

  addCashItem(request: AddCashItemRequest): Observable<CashItem> {
    return this.http.post<CashItem>(`${this.base}/cash`, request);
  }

  updateCashItem(id: number, request: UpdateCashItemRequest): Observable<CashItem> {
    return this.http.put<CashItem>(`${this.base}/cash/${id}`, request);
  }

  deleteCashItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/cash/${id}`);
  }

  // ── Options CRUD ────────────────────────────────────────────────────────────
  getOptionItems(): Observable<OptionItem[]> {
    return this.http.get<OptionItem[]>(`${this.base}/options`);
  }

  addOptionItem(request: AddOptionItemRequest): Observable<OptionItem> {
    return this.http.post<OptionItem>(`${this.base}/options`, request);
  }

  updateOptionItem(id: number, request: UpdateOptionItemRequest): Observable<OptionItem> {
    return this.http.put<OptionItem>(`${this.base}/options/${id}`, request);
  }

  deleteOptionItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/options/${id}`);
  }

  updateOptionNotes(id: number, notes: string | null): Observable<void> {
    return this.http.patch<void>(`${this.base}/options/${id}/notes`, { notes });
  }

  getOptionTechnicalData(symbol: string): Observable<OptionTechnicalData> {
    return this.http.get<OptionTechnicalData>(`${this.base}/options/technical/${symbol}`);
  }

  // ── EOD Signals Dashboard ───────────────────────────────────────────────────

  getEodSignals(filters: EodSignalFilters): Observable<DailySignalPagedResponse> {
    let params = new HttpParams().set('page', filters.page).set('pageSize', filters.pageSize);
    if (filters.ticker) params = params.set('ticker', filters.ticker);
    if (filters.scanType) params = params.set('scanType', filters.scanType);
    if (filters.signalType) params = params.set('signalType', filters.signalType);
    if (filters.signalState) params = params.set('signalState', filters.signalState);
    if (filters.ruleVersion) params = params.set('ruleVersion', filters.ruleVersion);
    if (filters.dateFrom) params = params.set('dateFrom', filters.dateFrom);
    if (filters.dateTo) params = params.set('dateTo', filters.dateTo);
    return this.http.get<DailySignalPagedResponse>(`${this.base}/eod-signals`, { params });
  }

  getEodSignalsMeta(): Observable<EodSignalsMeta> {
    return this.http.get<EodSignalsMeta>(`${this.base}/eod-signals/meta`);
  }

  updateEodSignalState(id: number, signalState: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/eod-signals/${id}/state`, { signalState });
  }

  updateEodSignalNotes(id: number, notes: string | null): Observable<void> {
    return this.http.patch<void>(`${this.base}/eod-signals/${id}/notes`, { notes });
  }

  deleteEodSignal(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/eod-signals/${id}`);
  }

  deleteAllEodSignals(
    ticker?: string,
    dateFrom?: string,
    dateTo?: string,
  ): Observable<{ deleted: number }> {
    let params = new HttpParams().set('confirm', 'true');
    if (ticker) params = params.set('ticker', ticker);
    if (dateFrom) params = params.set('dateFrom', dateFrom);
    if (dateTo) params = params.set('dateTo', dateTo);
    return this.http.delete<{ deleted: number }>(`${this.base}/eod-signals`, { params });
  }

  seedEodSignals(): Observable<{ seeded: number; skipped: number }> {
    return this.http.post<{ seeded: number; skipped: number }>(`${this.base}/eod-signals/seed`, {});
  }

  persistEodSignalsNow(): Observable<{ persisted: number; eodConfirm: number; confirmed: number }> {
    return this.http.post<{ persisted: number; eodConfirm: number; confirmed: number }>(
      `${this.base}/eod-signals/persist-now`,
      {},
    );
  }
}
