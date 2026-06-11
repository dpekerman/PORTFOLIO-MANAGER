export interface PortfolioItem {
  id: number;
  symbol: string;
  companyName: string;
  shares: number;
  averageCostBasis: number;
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
  timestamp: number;
}

export interface PortfolioSummary {
  item: PortfolioItem;
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
}

export interface FinnhubSearchResult {
  description: string;
  displaySymbol: string;
  symbol: string;
  type: string;
}

// ── RSI Scanner ────────────────────────────────────────────────────────────────
export type ScanType = 'Oversold' | 'Overbought';
export type SignalStatus = 'Confirmed' | 'EarlyWarning';
export type ReversalProbability = 'Low' | 'Medium' | 'High';
export type MacdCrossover = 'Bullish' | 'Bearish' | 'Neutral';
export type VolumeSignal = 'Validated' | 'Low-Volume Trap' | 'Neutral';
export type BollingerPosition = 'Below Lower' | 'Above Upper' | 'Inside';

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
}

export interface ScannerResponse {
  oversoldChain: RsiScanResult[];
  overboughtChain: RsiScanResult[];
  scannedAt: string;
  isDemo: boolean;
  market: string;
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
}

export interface FinnhubSearchResult {
  description: string;
  displaySymbol: string;
  symbol: string;
  type: string;
}
