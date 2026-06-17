export interface PortfolioItem {
  id: number;
  symbol: string;
  companyName: string;
  shares: number;
  averageCostBasis: number;
  sector: string;
  industry: string;
  sectorIsOverridden: boolean;
  isManual: boolean;
  manualMarketValue: number | null;
  addedAt: string;
}

export interface StockQuote {
  symbol: string;
  companyName: string;
  currentPrice: number;
  change: number;
  changePercent: number;
  highPrice: number;
  lowPrice: number;
  openPrice: number;
  previousClose: number;
  sector: string;
  industry: string;
  /** Yahoo Finance market state: REGULAR, PRE, POST, CLOSED, PREPRE, POSTPOST */
  marketState: string;
  timestamp: number;
  week52High: number;
  week52Low: number;
  targetMeanPrice: number;
  // -- Fundamental data (Yahoo Finance v7 quote) --------------------------------
  trailingPE: number;
  forwardPE: number;
  priceToBook: number;
  dividendYield: number; // e.g. 0.035 = 3.5%
  marketCap: number;
}

export interface PortfolioSummary {
  item: PortfolioItem;
  quote: StockQuote | null;
}

export interface WatchlistItem {
  id: number;
  symbol: string;
  notes: string;
  addedAt: string;
}

export interface WatchlistSummary {
  item: WatchlistItem;
  quote: StockQuote | null;
}

export interface AddPortfolioItemRequest {
  symbol: string;
  companyName: string;
  shares: number;
  averageCostBasis: number;
}

export interface UpdatePortfolioItemRequest {
  companyName: string;
  shares: number;
  averageCostBasis: number;
  sector?: string;
  industry?: string;
  overrideSector?: boolean;
}

export interface SectorIndustryLists {
  sectors: string[];
  industries: string[];
}

export interface AddManualPositionRequest {
  name: string;
  description: string;
  averageCost: number;
  marketValue: number;
}

export interface SymbolSearchResult {
  description: string;
  displaySymbol: string;
  symbol: string;
  type: string;
  exchange: string;
}

// ── RSI Scanner ────────────────────────────────────────────────────────────────
export type ScanType = 'Oversold' | 'Overbought' | 'Neutral';
export type SignalStatus = 'Confirmed' | 'EarlyWarning';
export type ReversalProbability = 'Low' | 'Medium' | 'High';
export type MacdCrossover = 'Bullish' | 'Bearish' | 'Neutral';
export type VolumeSignal = 'Validated' | 'Low-Volume Trap' | 'Neutral';
export type BollingerPosition = 'Below Lower' | 'Above Upper' | 'Inside';
export type MacdHistSlope = 'Rising' | 'Falling' | 'Neutral';
export type LogicMode = 'Legacy' | 'Enhanced';

export interface RsiScanResult {
  symbol: string;
  companyName: string;
  rsi: number;
  currentPrice: number;
  change: number;
  changePercent: number;
  scanType: ScanType;
  status: SignalStatus;
  triggerDetails: string;
  sector: string;
  volume: number;
  volumeRatio: number;
  scannedAt: string;
  isDemo: boolean;
  // ── 5 Technical Indicators ────────────────────────────────────────────────
  stochasticK: number;
  stochasticsConfirm: boolean;
  macdValue: number;
  macdSignalLine: number;
  macdCrossover: MacdCrossover;
  bollingerBreakout: boolean;
  bollingerPosition: BollingerPosition;
  volumeSignal: VolumeSignal;
  dma50Deviation: number;
  dma200Deviation: number;
  has200Dma: boolean;
  reversalProbability: ReversalProbability;
  // -- Enhanced Mode fields ---------------------------------------------------
  macdHistogram: number;
  macdHistDelta: number;
  macdHistSlope: MacdHistSlope;
  logicMode: LogicMode;
  // -- Analyst & Market Data --------------------------------------------------
  analystTargetPrice: number;
  analystTargetUpside: number;
  week52High: number;
  week52Low: number;
  // -- RSI Signal (9-EMA of RSI) ----------------------------------------------
  rsiSignal: number | null;
  rsiSignalAvailable: boolean;
}

export interface ScannerResponse {
  oversoldChain: RsiScanResult[];
  overboughtChain: RsiScanResult[];
  scannedAt: string;
  isDemo: boolean;
  market: string;
}

// ── Ad-Hoc Session Persistence ────────────────────────────────────────────────
export interface AdhocSessionPayload {
  symbols: string[];
  results?: RsiScanResult[] | null;
  oversoldThreshold: number;
  overboughtThreshold: number;
  logicMode: string;
}

export interface AdhocSessionResponse {
  symbols: string[];
  results?: RsiScanResult[] | null;
  oversoldThreshold: number;
  overboughtThreshold: number;
  logicMode: string;
  updatedAt?: string | null;
}

export interface AddManualPositionRequest {
  name: string;
  description: string;
  averageCost: number;
  marketValue: number;
}

// -- Value Screener ------------------------------------------------------------

export type ValueTier = 'HighConviction' | 'FairValue' | 'ValueTrap';
export type TechnicalState =
  | 'DeepValueReversal'
  | 'OverboughtMomentum'
  | 'OverboughtPullback'
  | 'SidewaysConsolidation'
  | 'MeanReversion'
  | 'HighVolumeExhaustion';
export type ActionTrigger =
  | 'AccumulateYield'
  | 'AccumulateValue'
  | 'BuyLimitAlert'
  | 'HoldRideTrend'
  | 'ValueTrapWarning'
  | 'Observe';
export type ValueOrigin = 'Portfolio' | 'Watchlist';

export interface ValueScreenerResult {
  symbol: string;
  description: string;
  origin: ValueOrigin;
  technicalState: TechnicalState;
  tier: ValueTier;
  score: number;
  actionTrigger: ActionTrigger;
  // Individual factor scores
  scoreEarningsYield: number;
  scoreFcfYield: number;
  scorePriceToBook: number;
  scorePiotroski: number;
  scoreRoic: number;
  // Raw values
  earningsYield: number; // %
  fcfYieldProxy: number; // %
  priceToBook: number;
  piotroskiScore: number; // 0-9
  roicProxy: number; // %
  dividendYield: number; // %
  currentPrice: number;
  currentRsi: number;
  week52High: number;
  week52Low: number;
  sector: string;
  analyzedAt: string;
}

export interface ValueScreenerRequest {
  includePortfolio: boolean;
  includeWatchlist: boolean;
  adHocSymbols: string[];
}
