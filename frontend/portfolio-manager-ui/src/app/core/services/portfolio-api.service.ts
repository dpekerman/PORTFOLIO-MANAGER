import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import {
  AddCashItemRequest,
  AddManualPositionRequest,
  AddOptionItemRequest,
  AddPortfolioItemRequest,
  AdhocSessionPayload,
  AdhocSessionResponse,
  AllocationRiskConfig,
  AllocationRiskTarget,
  AllocationSectorTarget,
  CashItem,
  DailySignalPagedResponse,
  DashboardResponse,
  DataRefreshResultDto,
  EodSignalFilters,
  EodSignalsMeta,
  MarketIndicesResponse,
  OptionItem,
  OptionTechnicalData,
  PortfolioBetaResult,
  PortfolioItem,
  PortfolioSummary,
  PortfolioValueHistoryDto,
  RsiScanResult,
  ScannerResponse,
  SectorIndustryLists,
  SinglePositionLimit,
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

  updateWatchlistTier(id: number, watchlistTier: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/watchlist/${id}/tier`, { watchlistTier });
  }

  updateWatchlistFavorite(id: number, isFavorite: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/watchlist/${id}/favorite`, { isFavorite });
  }

  updateWatchlistNotes(id: number, notes: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/watchlist/${id}/notes`, { notes });
  }

  updateWatchlistEarningsDate(id: number, earningsDate: string | null): Observable<void> {
    return this.http.patch<void>(`${this.base}/watchlist/${id}/earnings-date`, { earningsDate });
  }

  refreshWatchlistEarnings(): Observable<{ refreshed: number; total: number }> {
    return this.http.post<{ refreshed: number; total: number }>(
      `${this.base}/watchlist/refresh-earnings`,
      {},
    );
  }

  updatePortfolioHoldingRole(id: number, holdingRole: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/portfolio/${id}/holding-role`, { holdingRole });
  }

  updatePortfolioNotes(id: number, notes: string | null): Observable<void> {
    return this.http.patch<void>(`${this.base}/portfolio/${id}/notes`, { notes });
  }

  /** Returns the latest persisted portfolio snapshot from DB — no Yahoo Finance call. Null when no snapshot exists yet. */
  getPortfolioSnapshot(): Observable<PortfolioSummary[] | null> {
    return this.http
      .get<PortfolioSummary[]>(`${this.base}/stocks/quotes/snapshot`, { observe: 'response' })
      .pipe(
        map((r) => (r.status === 204 ? null : r.body)),
        catchError(() => of(null)),
      );
  }

  /** Returns the latest persisted watchlist snapshot from DB — no Yahoo Finance call. Null when no snapshot exists yet. */
  getWatchlistSnapshot(): Observable<WatchlistSummary[] | null> {
    return this.http
      .get<WatchlistSummary[]>(`${this.base}/watchlist/snapshot`, { observe: 'response' })
      .pipe(
        map((r) => (r.status === 204 ? null : r.body)),
        catchError(() => of(null)),
      );
  }

  /** Batch-refreshes portfolio + watchlist quotes and rebuilds the dashboard in one backend call. */
  refreshAll(): Observable<DataRefreshResultDto> {
    return this.http.post<DataRefreshResultDto>(`${this.base}/data/refresh`, {});
  }

  // ── RSI Scanner ─────────────────────────────────────────────────────────────
  /** Returns the latest persisted RSI scan snapshot from DB — no Yahoo Finance call. Null when no snapshot exists yet. */
  getRsiSnapshot(): Observable<ScannerResponse | null> {
    return this.http
      .get<ScannerResponse>(`${this.base}/scanner/rsi/snapshot`, { observe: 'response' })
      .pipe(
        map((r) => (r.status === 204 ? null : r.body)),
        catchError(() => of(null)),
      );
  }

  getDashboard(): Observable<DashboardResponse | null> {
    return this.http.get<DashboardResponse>(`${this.base}/dashboard`, { observe: 'response' }).pipe(
      map((r) => (r.status === 204 ? null : r.body)),
      catchError(() => of(null)),
    );
  }

  refreshDashboard(): Observable<DashboardResponse> {
    return this.http.post<DashboardResponse>(`${this.base}/dashboard/refresh`, {});
  }

  /** Triggers a live RSI scan against Yahoo Finance and saves the result as the new snapshot. */
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

  /** Lightweight batch price lookup — max 50 symbols, single Yahoo Finance call. Much faster than analyzeSymbols. */
  getBatchPrices(symbols: string[]): Observable<{ symbol: string; price: number }[]> {
    return this.http.post<{ symbol: string; price: number }[]>(
      `${this.base}/stocks/batch-prices`,
      symbols,
    );
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
    eodOversoldRsiThreshold: number;
    eodOverboughtRsiThreshold: number;
  }> {
    return this.http.get<{
      eodWindowStart: string;
      eodWindowEnd: string;
      eodWindowEnabled: boolean;
      eodOversoldRsiThreshold: number;
      eodOverboughtRsiThreshold: number;
    }>(`${this.base}/scanner/eod-settings`);
  }

  /** Update the EOD confirmation window settings on the backend (runtime — no restart needed). */
  updateEodSettings(settings: {
    eodWindowStart: string;
    eodWindowEnd: string;
    eodWindowEnabled: boolean;
    eodOversoldRsiThreshold: number;
    eodOverboughtRsiThreshold: number;
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

  getLatestValueScreener(): Observable<{
    portfolio: ValueScreenerResult[];
    portfolioRunAt: string | null;
    watchlist: ValueScreenerResult[];
    watchlistRunAt: string | null;
  }> {
    return this.http.get<{
      portfolio: ValueScreenerResult[];
      portfolioRunAt: string | null;
      watchlist: ValueScreenerResult[];
      watchlistRunAt: string | null;
    }>(`${this.base}/valuescreener/latest`);
  }

  refreshValueScreener(): Observable<{
    portfolio: ValueScreenerResult[];
    portfolioRunAt: string | null;
    watchlist: ValueScreenerResult[];
    watchlistRunAt: string | null;
  }> {
    return this.http.post<{
      portfolio: ValueScreenerResult[];
      portfolioRunAt: string | null;
      watchlist: ValueScreenerResult[];
      watchlistRunAt: string | null;
    }>(`${this.base}/valuescreener/refresh`, {});
  }

  getValueScreenerSchedule(): Observable<{ scheduledTimeEt: string; enabled: boolean }> {
    return this.http.get<{ scheduledTimeEt: string; enabled: boolean }>(
      `${this.base}/valuescreener/schedule`,
    );
  }

  updateValueScreenerSchedule(scheduledTimeEt: string, enabled: boolean): Observable<void> {
    return this.http.put<void>(`${this.base}/valuescreener/schedule`, { scheduledTimeEt, enabled });
  }

  clearValueScreenerData(origin?: string): Observable<void> {
    const params = origin ? `?origin=${origin}` : '';
    return this.http.delete<void>(`${this.base}/valuescreener/data${params}`);
  }

  // ── Sector / Industry Lists ─────────────────────────────────────────────────
  getSectorIndustryLists(): Observable<SectorIndustryLists> {
    return this.http.get<SectorIndustryLists>(`${this.base}/sector-industry`);
  }

  saveSectorIndustryLists(lists: SectorIndustryLists): Observable<SectorIndustryLists> {
    return this.http.put<SectorIndustryLists>(`${this.base}/sector-industry`, lists);
  }

  // ── Decision Sources (dedicated endpoint, independent of sectors/industries) ──
  getDecisionSources(): Observable<{ items: string[] }> {
    return this.http.get<{ items: string[] }>(`${this.base}/sector-industry/decision-sources`);
  }

  saveDecisionSourcesList(items: string[]): Observable<{ items: string[] }> {
    return this.http.put<{ items: string[] }>(`${this.base}/sector-industry/decision-sources`, {
      items,
    });
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
    if (filters.volumeSignal) params = params.set('volumeSignal', filters.volumeSignal);
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

  persistEodSignalsNow(): Observable<{
    persisted: number;
    bullBearTurnCount: number;
    oversoldScanned: number;
    overboughtScanned: number;
  }> {
    return this.http.post<{
      persisted: number;
      bullBearTurnCount: number;
      oversoldScanned: number;
      overboughtScanned: number;
    }>(`${this.base}/eod-signals/persist-now`, {});
  }

  // ── Backup / Restore ────────────────────────────────────────────────────────

  backupWatchlist(): Observable<unknown[]> {
    return this.http.get<unknown[]>(`${this.base}/watchlist/backup`);
  }

  restoreWatchlist(request: { items: unknown[] }): Observable<{ restored: number }> {
    return this.http.post<{ restored: number }>(`${this.base}/watchlist/restore`, request);
  }

  backupCash(): Observable<unknown[]> {
    return this.http.get<unknown[]>(`${this.base}/cash/backup`);
  }

  restoreCash(request: { items: unknown[] }): Observable<{ restored: number }> {
    return this.http.post<{ restored: number }>(`${this.base}/cash/restore`, request);
  }

  backupOptions(): Observable<unknown[]> {
    return this.http.get<unknown[]>(`${this.base}/options/backup`);
  }

  restoreOptions(request: { items: unknown[] }): Observable<{ restored: number }> {
    return this.http.post<{ restored: number }>(`${this.base}/options/restore`, request);
  }

  // ── Portfolio Value History ─────────────────────────────────────────────────
  getPortfolioValueHistory(count = 30): Observable<PortfolioValueHistoryDto[]> {
    return this.http.get<PortfolioValueHistoryDto[]>(
      `${this.base}/portfoliovaluehistory/latest?count=${count}`,
    );
  }

  /** Immediately records the current portfolio value (seeds DB when background service hasn't fired). */
  recordPortfolioValueNow(): Observable<PortfolioValueHistoryDto> {
    return this.http.post<PortfolioValueHistoryDto>(
      `${this.base}/portfoliovaluehistory/record-now`,
      {},
    );
  }

  /** Backfills any missing weekday snapshots in the past lookbackDays days using Yahoo Finance historical prices. */
  backfillMissingHistory(lookbackDays = 14): Observable<PortfolioValueHistoryDto[]> {
    return this.http.post<PortfolioValueHistoryDto[]>(
      `${this.base}/portfoliovaluehistory/backfill?lookbackDays=${lookbackDays}`,
      {},
    );
  }

  /** Returns the list of weekday dates in the past lookbackDays that have no snapshot (read-only). */
  getMissingHistoryDays(lookbackDays = 30): Observable<string[]> {
    return this.http.get<string[]>(
      `${this.base}/portfoliovaluehistory/missing-days?lookbackDays=${lookbackDays}`,
    );
  }

  // ── Portfolio Beta ──────────────────────────────────────────────────────────
  getPortfolioBeta(): Observable<PortfolioBetaResult> {
    return this.http.get<PortfolioBetaResult>(`${this.base}/portfoliobeta`);
  }

  /** Calculate portfolio beta applying user-supplied overrides (symbol → beta). */
  calculatePortfolioBeta(betaOverrides: Record<string, number>): Observable<PortfolioBetaResult> {
    return this.http.post<PortfolioBetaResult>(`${this.base}/portfoliobeta/calculate`, {
      betaOverrides,
    });
  }

  getMarketIndices(force = false): Observable<MarketIndicesResponse> {
    return this.http.get<MarketIndicesResponse>(
      `${this.base}/scanner/market-indices${force ? '?force=true' : ''}`,
    );
  }

  backupPortfolio(): Observable<unknown[]> {
    return this.http.get<unknown[]>(`${this.base}/portfolio/backup`);
  }

  restorePortfolio(request: { items: unknown[] }): Observable<{ restored: number }> {
    return this.http.post<{ restored: number }>(`${this.base}/portfolio/restore`, request);
  }

  // ── Allocation & Risk Management ────────────────────────────────────────────
  getAllocationRiskConfig(): Observable<AllocationRiskConfig> {
    return this.http.get<AllocationRiskConfig>(`${this.base}/allocation-risk`);
  }

  upsertRiskTarget(
    id: number | null,
    role: string,
    targetPct: number,
  ): Observable<AllocationRiskTarget> {
    if (id)
      return this.http.put<AllocationRiskTarget>(
        `${this.base}/allocation-risk/risk-targets/${id}`,
        { role, targetPct },
      );
    return this.http.post<AllocationRiskTarget>(`${this.base}/allocation-risk/risk-targets`, {
      role,
      targetPct,
    });
  }

  deleteRiskTarget(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/allocation-risk/risk-targets/${id}`);
  }

  upsertSectorTarget(
    id: number | null,
    sector: string,
    targetPct: number,
  ): Observable<AllocationSectorTarget> {
    if (id)
      return this.http.put<AllocationSectorTarget>(
        `${this.base}/allocation-risk/sector-targets/${id}`,
        { sector, targetPct },
      );
    return this.http.post<AllocationSectorTarget>(`${this.base}/allocation-risk/sector-targets`, {
      sector,
      targetPct,
    });
  }

  deleteSectorTarget(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/allocation-risk/sector-targets/${id}`);
  }

  upsertPositionLimit(
    id: number | null,
    role: string,
    targetPct: number,
  ): Observable<SinglePositionLimit> {
    if (id)
      return this.http.put<SinglePositionLimit>(
        `${this.base}/allocation-risk/position-limits/${id}`,
        { role, targetPct },
      );
    return this.http.post<SinglePositionLimit>(`${this.base}/allocation-risk/position-limits`, {
      role,
      targetPct,
    });
  }

  deletePositionLimit(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/allocation-risk/position-limits/${id}`);
  }
}
