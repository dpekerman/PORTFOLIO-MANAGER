export interface AccountType {
  id: number;
  name: string;
  version: string;
  stockCount: number;
  optionCount: number;
  cashCount: number;
}

export interface AccountTypeRename {
  oldName: string;
  item: AccountType;
}

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
  // Transaction tracking fields
  transactionType?: string | null;
  accountType?: string | null;
  openDate?: string | null;
  closeDate?: string | null;
  closingPrice?: number | null;
  /** @optional Holding role for portfolio items: Core | Strategic | Swing | Speculative | Options */
  holdingRole?: string | null;
  /** @optional Free-text notes stored per transaction record */
  notes?: string | null;
  /** @optional Decision source: App Signal | Manual | Catalyst | Rebalance | Risk Control | Loss Harvest */
  decisionSource?: string | null;
  /** @optional Decision source recorded at close */
  decisionSourceClosed?: string | null;
  /** @optional Final Action synced from the Portfolio grid, consumed by Dashboard Action Center */
  finalAction?: string | null;
  finalActionSeverity?: string | null;
  finalActionPriority?: string | null;
  finalActionUpdatedAt?: string | null;
  /** Completed market session (yyyy-MM-dd) the published Final Action's evidence belongs to. */
  finalActionSession?: string | null;
}

/** One Portfolio holding's computed Final Action, pushed to the backend after every recompute. */
export interface FinalActionSyncItem {
  itemId: number;
  symbol: string;
  finalAction: string;
  severity: string;
  priority: 'REQUIRED' | 'DEVELOPING' | 'INFORMATIONAL';
}

/** 5-trading-day and 1-month change % for a symbol; null = not enough history (never 0). */
export interface PeriodChange {
  symbol: string;
  change5DPct: number | null;
  change1MPct: number | null;
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
  sourcePriceTimestamp?: number | null;
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
  priceStructure?: PriceStructureResult | null;
  technicalFacts?: SharedTechnicalFacts | null;
}

export interface SharedTechnicalFacts {
  symbol: string;
  rsi: number | null;
  maStructure: string | null;
  maCrossState: string | null;
  momentumState: string | null;
  priceStructure: PriceStructureResult;
  buyScore: number | null;
  calculatedAt: string;
  latestEodTradingDate?: string | null;
  latestEodSignalDate?: string | null;
  latestEodSignalState?: string | null;
  latestEodScanType?: string | null;
  latestEodRsi?: number | null;
  latestEodTrendShift?: string | null;
  latestEodEntryPrice?: number | null;
  latestEodStopLoss?: number | null;
  latestEodRiskPercent?: number | null;
  latestEodReversalStrength?: string | null;
  latestEodVolumeState?: string | null;
  latestEodIsNew?: boolean;
  latestEodIsInvalidated?: boolean;
  analysisTicker?: string | null;
  analysisMarket?: string | null;
  analysisCurrency?: string | null;
  usesUnderlyingSecurity?: boolean;
}

export type SecurityAnalysisMappingSource = 'AUTO' | 'USER';
export type UnderlyingResolutionStatus = 'NotApplicable' | 'Resolved' | 'NeedsUserInput';

export interface SecurityAnalysisMapping {
  tradingTicker: string;
  analysisTicker: string;
  analysisMarket: string;
  analysisCurrency: string;
  usesUnderlyingSecurity: boolean;
  resolutionStatus: UnderlyingResolutionStatus;
  mappingSource: SecurityAnalysisMappingSource | null;
  dataError?: string | null;
}

export interface SaveSecurityAnalysisMappingRequest {
  underlyingTicker: string;
  useUnderlyingForAnalysis: boolean;
}

export interface WatchlistItem {
  id: number;
  symbol: string;
  notes: string;
  addedAt: string;
  /** Investment role: Core | Strategic | Swing | Speculative. Default: Strategic. */
  role: string;
  /** Whether this symbol is marked as a favourite. */
  isFavorite: boolean;
  earningsDate?: string | null;
  /** Monitoring intensity: Active | Strategic | Universe. Default: Strategic. */
  watchlistTier: string;
}

export interface WatchlistSummary {
  item: WatchlistItem;
  quote: StockQuote | null;
  priceStructure?: PriceStructureResult | null;
  technicalFacts?: SharedTechnicalFacts | null;
}

export interface DataRefreshResultDto {
  portfolioSymbolCount: number;
  watchlistSymbolCount: number;
  dashboardRebuilt: boolean;
  refreshedAt: string;
  durationMs: number;
  portfolioSummaries: PortfolioSummary[];
  watchlistSummaries: WatchlistSummary[];
  portfolioSymbols: string[];
  watchlistSymbols: string[];
}

export interface AddPortfolioItemRequest {
  symbol: string;
  companyName: string;
  shares: number;
  averageCostBasis: number;
  transactionType?: string | null;
  accountType?: string | null;
  openDate?: string | null;
  closeDate?: string | null;
  closingPrice?: number | null;
  decisionSource?: string | null;
  holdingRole?: string | null;
}

export interface UpdatePortfolioItemRequest {
  companyName: string;
  shares: number;
  averageCostBasis: number;
  sector?: string;
  industry?: string;
  overrideSector?: boolean;
  transactionType?: string | null;
  accountType?: string | null;
  openDate?: string | null;
  closeDate?: string | null;
  closingPrice?: number | null;
  holdingRole?: string | null;
  decisionSource?: string | null;
  decisionSourceClosed?: string | null;
}

/** Response of PUT /api/portfolio/{id}. newOpenItem is populated only when the edit was a partial
 * close — the remaining shares were auto-split into a new OPEN position. */
export interface UpdatePortfolioItemResponse {
  updated: PortfolioItem;
  newOpenItem: PortfolioItem | null;
}

export interface SectorIndustryLists {
  sectors: string[];
  industries: string[];
  decisionSources?: string[];
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
export type SignalStatus = 'Confirmed' | 'EodConfirm' | 'EarlyWarning';
export type ReversalProbability = 'Low' | 'Medium' | 'High';
export type MacdCrossover = 'Bullish' | 'Bearish' | 'Neutral';
export type VolumeSignal = 'Validated' | 'Low-Volume Trap' | 'Neutral';
export type BollingerPosition = 'Below Lower' | 'Above Upper' | 'Inside';
export type MacdHistSlope = 'Rising' | 'Falling' | 'Neutral';
export type LogicMode = 'Legacy' | 'Enhanced';
export type RsiDivergence = 'Bullish' | 'Bearish' | 'None';
export type ChannelDirection = 'NONE' | 'RISING';
export type ChannelState =
  | 'NONE'
  | 'CHANNEL_ACTIVE'
  | 'THIRD_TOUCH_APPROACHING'
  | 'THIRD_TOUCH_TEST'
  | 'LOWER_RAIL_APPROACHING'
  | 'LOWER_RAIL_RETEST'
  | 'REVERSAL_DEVELOPING'
  | 'BOUNCE_CONFIRMED'
  | 'CHANNEL_BROKEN';

export interface RsiScanResult {
  symbol: string;
  analysisTicker?: string;
  analysisMarket?: string;
  analysisCurrency?: string;
  usesUnderlyingSecurity?: boolean;
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
  stochasticD: number;
  rsiDivergence: RsiDivergence;
  stochasticsConfirm: boolean;
  macdValue: number;
  macdSignalLine: number;
  macdCrossover: MacdCrossover;
  bollingerBreakout: boolean;
  bollingerPosition: BollingerPosition;
  bollingerPctB: number;
  bollingerBandwidth: number;
  volumeProjection: number;
  positionSizingShares: number;
  positionSizingRiskAmount: number;
  positionSizingPositionValue: number;
  positionSizingLimitingReason: string;
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
  // -- EOD Confirm data -------------------------------------------------------
  /** 14-day Average True Range (Wilder's smoothing). 0 when insufficient data. */
  dailyAtr: number;
  /** 9-period EMA of closing price. */
  ema9Price: number;
  /** 20-period SMA of closing price. Used by Momentum Shift Consolidation rule. */
  sma20Price: number;
  /** 50-period SMA. Used by Trend Setup engine. */
  sma50Price: number;
  /** 10-period EMA. Used by Trend Setup engine. */
  ema10Price: number;
  /** 20-period EMA. Used by Trend Setup engine. */
  ema20Price: number;
  /** Today's session high price. Used for BottomHalfClose / TopHalfClose calculation. */
  dayHigh: number;
  /** Today's session low price. Used for BottomHalfClose / TopHalfClose calculation. */
  dayLow: number;
  /** Today's opening price. Used for gap detection. */
  openPrice: number;
  /** Yesterday's closing price. GapPct = (openPrice - previousClose) / previousClose * 100. */
  previousClose: number;
  // -- Day-over-Day Momentum Tracking (StagedSignals) -------------------------
  /** RSI change from previous trading session. Null on Day 1. */
  rsiDelta1D: number | null;
  /** Trend shift state: "Waiting" | "🟢 Bull Turn" | "🟡 Stabilizing" | "🔴 Still Falling" | "🟢 Bear Turn" | "🔴 Still Rising" */
  trendShift: string;
  /** 200-day SMA value. 0 when not enough data. */
  sma200: number;
  /** Price vs SMA200: "Trend-Aligned" | "Counter-Trend" | "" */
  trendSetup200: string;
  /** Dynamic stop loss calculated from ExtremeLow/High + 1.5×ATR. 0 when not yet computed. */
  dynamicStopLoss: number;
  /** True when this result is kept from a prior staged signal (RSI may have recovered). */
  isTracked: boolean;
  // -- 2-Stage Engine status --------------------------------------------------
  /** Stage workflow: "STAGED" | "TRACKING" | "CONFIRMING" | "" */
  stageStatus: string;
  /** RSI velocity label: "" | "Early" | "Normal" | "Strong" | "Explosive" */
  turnStrength: string;
  /** "Elevated" when TurnStrength is Explosive; "" otherwise */
  chaseRisk: string; // -- Fibonacci Retracement V1 -----------------------------------------------
  /** Swing low price used for Fib calculation (60-day lookback). 0 when not calculable. */
  fibSwingLow: number;
  /** Swing high price (after swing low). 0 when not calculable. */
  fibSwingHigh: number;
  /** Fibonacci 38.2% level. 0 when not calculable. */
  fib38_2: number;
  /** Fibonacci 50% level. */
  fib50: number;
  /** Fibonacci 61.8% level (Golden Ratio). */
  fib61_8: number;
  /** Fibonacci 78.6% level. */
  fib78_6: number;
  /** Price zone relative to Fib levels: "Shallow Pullback" | "Normal Pullback" | "Value Zone" | "Key Fib Support" | "Deep Pullback" | "Trend Damage" | "" */
  fibZone: string;
  /** Fib status vs 61.8: "Above 61.8" | "Testing 61.8" | "Reclaimed 61.8" | "Below 61.8" | "Below 78.6" | "" */
  fibStatus: string;
  /** ((CurrentPrice − Fib61.8) / Fib61.8) × 100. Positive = above level. 0 when not calculable. */
  distanceToFib61_8Pct: number;
  channelDirection: ChannelDirection;
  channelSlope: number;
  lowerRailToday: number;
  upperRailToday: number;
  channelQuality: number;
  priorConfirmedLowerTouches: number;
  lastLowerTouchDate: string | null;
  distanceToLowerRailPercent: number;
  distanceToLowerRailATR: number;
  channelState: ChannelState;
  nearestOpenGapAbove: number | null;
  nearestOpenGapBelow: number | null;
  distanceToGapAbovePercent: number | null;
  distanceToGapBelowPercent: number | null;
  channelTouchDetails: Array<{
    touchNumber: number;
    touchDate: string;
    railPrice: number;
    actualLow: number;
    bounceATR: number;
    confirmedBounce: boolean;
  }>;
  priceStructure: PriceStructureResult;
}

export interface ScannerResponse {
  oversoldChain: RsiScanResult[];
  overboughtChain: RsiScanResult[];
  scannedAt: string;
  isDemo: boolean;
  market: string;
}

// ── Yesterday's EOD Signals (overnight persistence / Gap 3) ──────────────────
export interface EodSignalRecord {
  symbol: string;
  analysisTicker?: string | null;
  analysisCurrency?: string | null;
  companyName: string;
  scanType: string;
  rsi: number;
  price: number;
  triggerDetails: string;
  scannedAt: string;
}

export interface YesterdayEodResponse {
  hasData: boolean;
  signalDate: string;
  isMorningWindow: boolean;
  signals: EodSignalRecord[];
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

// ── Cash ─────────────────────────────────────────────────────────────────────
// CashFlowType classifies WHY cash moved — required on every new ledger entry. OpeningBalance is
// migration/admin-only (never user-selectable). IsExternalFlow distinguishes real contributions/
// withdrawals (Deposit/Withdrawal) from internal portfolio movements (everything else) for future
// performance calculations.
export type CashFlowType =
  | 'OpeningBalance'
  | 'Deposit'
  | 'Withdrawal'
  | 'TradeProceeds'
  | 'TradePurchase'
  | 'Dividend'
  | 'Interest'
  | 'Fee'
  | 'Tax'
  | 'AdjustmentIncrease'
  | 'AdjustmentDecrease';

/** User-selectable types for Add/Edit/Adjust dialogs — OpeningBalance excluded (migration-only). */
export const SELECTABLE_CASH_FLOW_TYPES: CashFlowType[] = [
  'Deposit',
  'Withdrawal',
  'TradeProceeds',
  'TradePurchase',
  'Dividend',
  'Interest',
  'Fee',
  'Tax',
  'AdjustmentIncrease',
  'AdjustmentDecrease',
];

/** Trade leg that owns a cash ledger row: Open = cash out (TradePurchase), Close = cash in (TradeProceeds). */
export type TradeLinkSourceType = 'PortfolioOpen' | 'PortfolioClose' | 'OptionOpen' | 'OptionClose';

export interface CashItem {
  id: number;
  description: string;
  amount: number;
  addedAt: string;
  accountType?: string | null;
  transactionDate?: string | null;
  cashFlowType?: CashFlowType | null;
  isExternalFlow: boolean;
  /** Set when an existing row was edited after creation; null if never edited. */
  modifiedAt?: string | null;
  /** Set only on rows created from a trade via "Link cash"; null for manual entries. */
  sourceType?: TradeLinkSourceType | null;
  sourceItemId?: number | null;
}

/** Amount is a positive magnitude; the server derives CashFlowType and sign from sourceType. */
export interface AddLinkedCashRequest {
  sourceType: TradeLinkSourceType;
  sourceItemId: number;
  amount: number;
  description?: string | null;
  accountType?: string | null;
  transactionDate?: string | null;
}

/** A trade leg with no cash-ledger counterpart (it still double-counts in Portfolio Value). */
export interface UnlinkedTrade {
  sourceType: TradeLinkSourceType;
  sourceItemId: number;
  symbol: string;
  label: string;
  /** e.g. "500 sh" or "10 contracts". */
  quantity: string;
  price: number | null;
  amount: number | null;
  accountType: string | null;
  tradeDate: string;
  /** NoCash, or MissingPrice when the leg has no price to size the cash row. */
  reason: 'NoCash' | 'MissingPrice';
  diagnosis?: UnlinkedTradeDiagnosis | null;
}

export interface UnlinkedNearestCash {
  id: number;
  description: string;
  amount: number;
  accountType: string | null;
  date: string | null;
  cashFlowType: string | null;
  /** Which matching rules this row failed. */
  mismatches: string[];
}

/** Why a trade leg is reported as having no cash entry. */
export interface UnlinkedTradeDiagnosis {
  summary: string;
  tradeRecordedAt: string;
  expectedCashFlowType: string;
  expectedCashAmount: number | null;
  amountTolerance: number | null;
  nearestCash: UnlinkedNearestCash | null;
  details: string[];
}

export interface AddCashItemRequest {
  description: string;
  amount: number;
  cashFlowType: CashFlowType;
  accountType?: string | null;
  transactionDate?: string | null;
}

export interface UpdateCashItemRequest {
  description: string;
  amount: number;
  cashFlowType: CashFlowType;
  accountType?: string | null;
  transactionDate?: string | null;
}

/** "Adjust Balance": user types the desired new total; backend computes the delta and inserts one new
 * ledger row. cashFlowType direction must match whether the total is increasing or decreasing. */
export interface AdjustCashBalanceRequest {
  accountType: string;
  desiredNewTotal: number;
  cashFlowType: CashFlowType;
  transactionDate?: string | null;
}

// ── Options ───────────────────────────────────────────────────────────────────
export interface OptionItem {
  id: number;
  underlyingTicker: string;
  positionType: 'CALL' | 'PUT';
  expirationDate: string;
  strike: number;
  premium: number;
  numberOfContracts: number;
  marketPrice: number;
  addedAt: string;
  // Transaction tracking fields
  transactionType?: string | null;
  accountType?: string | null;
  openDate?: string | null;
  closeDate?: string | null;
  closingPrice?: number | null;
  /** @optional Free-text notes stored per transaction record */
  notes?: string | null;
  /** @optional Decision source: App Signal | Manual | Catalyst | Rebalance | Risk Control | Loss Harvest */
  decisionSource?: string | null;
  /** @optional Decision source recorded when the position was closed */
  decisionSourceClosed?: string | null;
}

export interface AddOptionItemRequest {
  underlyingTicker: string;
  positionType: string;
  expirationDate: string;
  strike: number;
  premium: number;
  numberOfContracts: number;
  marketPrice: number;
  transactionType?: string | null;
  accountType?: string | null;
  openDate?: string | null;
  closeDate?: string | null;
  closingPrice?: number | null;
  decisionSource?: string | null;
}

export interface UpdateOptionItemRequest {
  underlyingTicker: string;
  positionType: string;
  expirationDate: string;
  strike: number;
  premium: number;
  numberOfContracts: number;
  marketPrice: number;
  transactionType?: string | null;
  accountType?: string | null;
  openDate?: string | null;
  closeDate?: string | null;
  closingPrice?: number | null;
  decisionSource?: string | null;
  decisionSourceClosed?: string | null;
}

export interface OptionTechnicalData {
  symbol: string;
  currentPrice: number;
  previousClose: number;
  yesterdayHigh: number;
  yesterdayLow: number;
  rsi14: number;
  rsiSignal9: number;
  rsiSignalAvailable: boolean;
  sma20: number;
  sma50: number;
  ema21: number;
  atr14: number;
  bollingerUpper: number;
  bollingerLower: number;
}

export type OptionState =
  | 'FREE_TRADE_MILESTONE'
  | 'INTRINSIC_CRACKED'
  | 'TEMPORARILY_BROKEN'
  | 'TARGET_ACHIEVED'
  | 'VELOCITY_INVERSION'
  | 'TREND_REVERSED'
  | 'VOLATILITY_EXPANSION'
  | 'MONITOR';

export interface OptionAnalysis {
  item: OptionItem;
  technical: OptionTechnicalData | null;
  optionState: OptionState;
  stateDescription: string;
  action: string;
  actionDescription: string;
  /** Current stock price for the underlying ticker */
  stockPrice: number | null;
  /** Days to expiration */
  dte: number;
  /** Premium × contracts × 100 */
  cost: number;
  /** MarketPrice × contracts × 100 */
  marketValue: number;
  gainLoss: number;
  gainLossPct: number;
}

// ── EOD Signals Dashboard ─────────────────────────────────────────────────────

export type SignalState = 'Active' | 'FollowThrough' | 'Invalidated' | 'Expired' | 'Reversed';

export interface DailySignal {
  id: number;
  symbol: string;
  companyName: string;
  /** Oversold | Overbought */
  scanType: string;
  /** EodConfirm | Confirmed | EarlyWarning */
  signalType: string;
  rsi: number;
  price: number;
  triggerDetails: string;
  /** yyyy-MM-dd (ET) */
  signalDate: string;
  recordedAt: string;
  /** yyyy-MM-dd of the completed market session that produced this signal. */
  tradingDate: string | null;
  /** UTC timestamp when the scanner evaluated the market session. */
  scannedAt: string | null;
  /** Completed market sessions since tradingDate, calculated by the API. */
  tradingSessionsPassed: number | null;
  /** Legacy | Enhanced */
  ruleVersion: string;
  signalState: SignalState;
  sector: string;
  reversalProbability: string;
  volumeSignal: string;
  notes: string | null;
  updatedAt: string | null;
  // -- Confirmation snapshot fields -----------------------------------------
  trendShift: string | null;
  rsiDelta1D: number | null;
  entryPrice: number | null;
  stopLossPrice: number | null;
  riskPerShare: number | null;
  positionSizingShares: number | null;
  positionSizingRiskAmount: number | null;
  positionSizingPositionValue: number | null;
  positionSizingLimitingReason: string | null;
  sma200: number | null;
  ema9AtEntry: number | null;
  ema9ConfirmedAtEntry: boolean | null;
  // -- Fibonacci snapshot (informational) ------------------------------------
  fib61_8AtSignal: number | null;
  fibZoneAtSignal: string | null;
  fibStatusAtSignal: string | null;
  /** Underlying ticker whose (USD) prices are stored; null when the signal symbol itself was analyzed. */
  analysisTicker?: string | null;
  analysisCurrency?: string | null;
}

export interface DailySignalPagedResponse {
  items: DailySignal[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface EodSignalsMeta {
  tickers: string[];
  scanTypes: string[];
  signalTypes: string[];
  signalStates: string[];
  ruleVersions: string[];
  minDate: string | null;
  maxDate: string | null;
  totalCount: number;
}

// ── Portfolio Value History ────────────────────────────────────────────────────
/** What last wrote/recomputed a snapshot row. Mirrors backend PortfolioValueSource. */
export type PortfolioValueSource =
  | 'EodAuto'
  | 'ManualRecordNow'
  | 'SameDayReseal'
  | 'CashRecalculation'
  | 'Migration';

/** Derived display status — Original/Resealed/CashRecalculated come from persisted audit fields;
 * PendingReseal is the one transient state computed from live backend state (today's row only). */
export type SnapshotStatus = 'Original' | 'Resealed' | 'CashRecalculated' | 'PendingReseal';

export interface PortfolioValueHistoryDto {
  id: number;
  recordedAt: string;
  recordedDate: string;
  totalValue: number;
  stocksValue: number;
  cashValue: number;
  optionsValue: number;
  source: PortfolioValueSource;
  lastRecalculatedAt: string | null;
  /** Sum of CashItems on recordedDate with isExternalFlow===true. Never inferred from CashValue deltas. */
  externalCashFlow: number;
  snapshotStatus: SnapshotStatus;
  /** Simple arithmetic check: true if totalValue !== stocksValue+optionsValue+cashValue. */
  hasMismatch: boolean;
}

// ── Portfolio Beta ─────────────────────────────────────────────────────────────
export interface BetaContributor {
  symbol: string;
  weightPct: number;
  beta: number;
  isProxy: boolean;
}

export interface PortfolioBetaResult {
  portfolioBeta: number;
  exCashBeta: number;
  cashPct: number;
  proxyPct: number;
  /** "Good" | "Warning" | "TooMuchRisk" */
  status: string;
  topContributors: BetaContributor[];
}

// ── Market Indices ─────────────────────────────────────────────────────────────
export interface MarketIndexDto {
  symbol: string;
  name: string;
  price: number;
  change: number;
  changePercent: number;
}

// ── Authentication ─────────────────────────────────────────────────────────────
export type AppRole = 'Admin' | 'Trader' | 'Viewer';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface SetupRequest {
  displayName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface CreateUserRequest {
  displayName: string;
  email: string;
  password: string;
  role: AppRole;
}

export interface UserInfo {
  id: string;
  displayName: string;
  email: string;
  roles: AppRole[];
}

export interface AuthResponse {
  accessToken: string;
  user: UserInfo;
}

export interface SetupRequiredResponse {
  required: boolean;
}

// ── EOD Automation Pipeline ──────────────────────────────────────────────────
export type AutomationOverallStatus =
  | 'Running'
  | 'Success'
  | 'PartialSuccess'
  | 'Failed'
  | 'SkippedNonTradingDay'
  | 'Cancelled';
export type AutomationStepStatus =
  | ''
  | 'Succeeded'
  | 'Failed'
  | 'NotEligible'
  | 'NotScheduled'
  | 'NotObserved';
export type AutomationTriggerType = 'Scheduled' | 'ManualRunNow' | 'TestWake';

export interface AutomationRunLogDto {
  runId: string;
  tradingDate: string;
  triggerType: AutomationTriggerType | string;
  triggerCorrelationId: string | null;
  scheduledStartUtc: string | null;
  actualStartUtc: string;
  completedAtUtc: string | null;
  overallStatus: AutomationOverallStatus | string;
  refreshStatus: AutomationStepStatus | string;
  refreshStartedAtUtc: string | null;
  refreshCompletedAtUtc: string | null;
  portfolioSymbolCount: number;
  watchlistSymbolCount: number;
  rsiStatus: AutomationStepStatus | string;
  rsiCompletedAtUtc: string | null;
  eodSignalsPersistedCount: number;
  snapshotStatus: AutomationStepStatus | string;
  snapshotCompletedAtUtc: string | null;
  snapshotSource: string | null;
  valueScreenerStatus: AutomationStepStatus | string;
  valueScreenerLastRunAtUtc: string | null;
  powerRequestAcquiredAtUtc: string | null;
  powerRequestReleasedAtUtc: string | null;
  lastHeartbeatAtUtc: string | null;
  lastHeartbeatStep: string | null;
  errorStep: string | null;
  errorMessage: string | null;
  machineName: string;
}

export interface AutomationSettingsDto {
  enabled: boolean;
  wakeTimeEt: string;
  keepAwakeUntilEtOverride: string | null;
  computedKeepAwakeUntilEt: string;
  completionGraceMinutes: number;
  maxPollMinutes: number;
  eodWindowStartEt: string;
  eodWindowEndEt: string;
  eodWindowEnabled: boolean;
  valueScreenerScheduledTimeEt: string;
  valueScreenerEnabled: boolean;
  secretConfigured: boolean;
  missedRunAlertTimeEt: string;
}

export interface UpdateAutomationSettingsRequest {
  enabled: boolean;
  wakeTimeEt: string;
  keepAwakeUntilEtOverride: string | null;
  completionGraceMinutes: number;
  maxPollMinutes: number;
  missedRunAlertTimeEt?: string | null;
}

export interface AutomationTaskStatusDto {
  exists: boolean;
  state: string | null;
  nextRunTime: string | null;
  lastRunTime: string | null;
  lastTaskResult: number | null;
}

export interface AutomationTriggerResponseDto {
  runId: string;
}

/** Diagnostic/display-only — never affects scheduling logic. A false `taskMatchesExpected` means the
 * underlying Scheduled Task trigger itself has drifted from Eastern Time, not just a display issue. */
export interface AutomationTimezoneDiagnosticsDto {
  businessTimeZoneLabel: string;
  windowsLocalTimeZoneLabel: string;
  wakeTimeEtDisplay: string;
  wakeTimeLocalDisplay: string;
  taskExists: boolean;
  taskNextRunEtDisplay: string | null;
  taskNextRunLocalDisplay: string | null;
  expectedNextRunEtDisplay: string;
  taskMatchesExpected: boolean | null;
  driftMinutes: number | null;
  warning: string | null;
}

/** Result of a "Fix Missing Data" recovery run — see MissedDataRecoveryService (backend). */
export interface RecoveryStepResultDto {
  success: boolean;
  message: string;
}

export interface MissedDataRecoveryResultDto {
  started: boolean;
  status: 'Completed' | 'AlreadyRunning' | string;
  eodSignals: RecoveryStepResultDto | null;
  snapshot: RecoveryStepResultDto | null;
  valueScreener: RecoveryStepResultDto | null;
}

/** Result of "Backup Now" (see DatabaseBackupService — backend). */
export interface DatabaseBackupResultDto {
  ran: boolean;
  filePath: string | null;
  message: string;
}

export interface MarketIndicesResponse {
  indices: MarketIndexDto[];
  fetchedAt: string;
}

export interface DashboardSummary {
  totalValue: number;
  todayChange: number;
  todayChangePercent: number;
  todayStocksChange: number;
  todayCashChange: number;
  todayOptionsChange: number;
  weekChange: number;
  weekChangePercent: number;
  monthChange: number;
  monthChangePercent: number;
  oversoldCount: number;
  overboughtCount: number;
}

export interface DashboardMover {
  symbol: string;
  companyName: string;
  changePercent: number;
  isPortfolio: boolean;
  isWatchlist: boolean;
}

export interface DashboardChartPoint {
  date: string;
  totalValue: number;
}

export interface DashboardAllocation {
  label: string;
  value: number;
  percent: number;
  targetPercent: number;
  delta: number;
  status: string; // good | watch-over | watch-under | over | under | no-target
}

export interface DashboardRsiSignal {
  symbol: string;
  companyName: string;
  rsi: number;
  momentumShift: string;
  volumeSignal: string;
  returnPct: number;
  action: string;
  signalStatus: string;
  isInPortfolio: boolean;
  isInWatchlist: boolean;
  isNewToday: boolean;
  isActionRequired: boolean;
  severity: 'REQUIRED' | 'DEVELOPING' | 'INFORMATIONAL';
  channelState?: ChannelState;
}

export interface DashboardRsiSection {
  oversoldCount: number;
  overboughtCount: number;
  newTodayCount: number;
  actionRequiredCount: number;
  oversoldSignals: DashboardRsiSignal[];
  overboughtSignals: DashboardRsiSignal[];
}

export interface DashboardEarning {
  symbol: string;
  companyName: string;
  earningsDate: string;
  source: string;
}

export interface DashboardResponse {
  updatedAt: string;
  summary: DashboardSummary;
  topMovers: DashboardMover[];
  bottomMovers: DashboardMover[];
  valueHistory: DashboardChartPoint[];
  marketIndices: MarketIndexDto[];
  allocation: DashboardAllocation[];
  nextSevenDayEarnings: DashboardEarning[];
  rsiSection?: DashboardRsiSection;
  roleAllocation?: DashboardAllocation[];
}

export interface EodSignalFilters {
  ticker?: string;
  scanType?: string;
  signalType?: string;
  signalState?: string;
  ruleVersion?: string;
  volumeSignal?: string;
  dateFrom?: string;
  dateTo?: string;
  page: number;
  pageSize: number;
}

// ── Allocation & Risk Management ────────────────────────────────────────────────
export interface AllocationRiskTarget {
  id: number;
  role: string;
  targetPct: number;
  displayOrder: number;
}

export interface AllocationSectorTarget {
  id: number;
  sector: string;
  targetPct: number;
  displayOrder: number;
}

export interface SinglePositionLimit {
  id: number;
  role: string;
  targetPct: number;
  displayOrder: number;
}

export interface AllocationRiskConfig {
  riskTargets: AllocationRiskTarget[];
  sectorTargets: AllocationSectorTarget[];
  positionLimits: SinglePositionLimit[];
}

// ── Watchlist Tier ──────────────────────────────────────────────────────────────
export type WatchlistTier = 'Active' | 'Strategic' | 'Universe';

// ── Portfolio Actions (Dashboard) ──────────────────────────────────────────────
export interface PortfolioActionDto {
  symbol: string;
  companyName: string;
  holdingRole: string;
  scanType: string;
  rsi: number | null;
  trendShift: string;
  fibZone: string;
  chaseRisk: string;
  allocationStatus: string; // "over" | "under" | "on-target" | ""
  actionLabel: string;
  actionSeverity: string; // "buy" | "trim" | "hold" | "review" | "wait" | "danger"
  actionPriority: string; // "REQUIRED" | "DEVELOPING" | "INFORMATIONAL"
  isInPortfolio: boolean;
  isInWatchlist: boolean;
  channelState: ChannelState;
  channelDirection: ChannelDirection;
  channelQuality: number;
  priorConfirmedLowerTouches: number;
  lowerRailToday: number;
  eodClose: number;
  distanceToLowerRailPercent: number;
  distanceToLowerRailATR: number;
  lastLowerTouchDate: string | null;
  nearestOpenGapAbove: number | null;
  channelTouchDetails?: Array<{
    touchNumber: number;
    touchDate: string;
    railPrice: number;
    actualLow: number;
    bounceATR: number;
    confirmedBounce: boolean;
  }>;
  maStructure: string | null;
  momentumState: string | null;
  priceStructure: PriceStructureResult | null;
  inclusionReason: string | null;
  reasonExcludedFromActionCenter: string | null;
  technicalCalculatedAt: string | null;
  latestEodSignalState?: string | null;
  latestEodScanType?: string | null;
  latestEodTrendShift?: string | null;
  latestEodIsNew?: boolean;
  latestEodIsInvalidated?: boolean;
  /** CurrentValid | PriorSessionValid | AwaitingData | Evaluating — the effective canonical state. */
  evaluationState?: string | null;
  isActionable?: boolean;
  lastValidAction?: string | null;
  lastEvaluatedAtUtc?: string | null;
  reasonCodes?: string[] | null;
}

export type ActionResolutionStatus =
  | 'Resolved'
  | 'OwnershipActionNotCalculated'
  | 'OwnershipActionNotApplicable';

export interface DashboardEodSummaryRow {
  symbol: string;
  companyName: string;
  signal: string;
  rsi: number;
  trendShift: string;
  signalState: string;
  structure: string;
  why: string;
  ownership: 'Portfolio' | 'Watchlist' | 'Universe';
  action: string;
  actionPriority: string;
  actionResolutionStatus: ActionResolutionStatus;
  actionResolutionReason: string;
}

export interface DashboardEodSummary {
  tradingDate: string | null;
  rawRecordCount: number;
  uniqueTickerCount: number;
  rows: DashboardEodSummaryRow[];
  lastScanDate?: string | null;
}

// ── Decision Analytics ──────────────────────────────────────────────────────────
export interface DecisionPerformanceRow {
  decisionSource: string;
  tradeCount: number;
  winCount: number;
  winRatePct: number;
  avgReturnPct: number;
  avgHoldingDays: number;
}

export interface AnalyticsDecisionPerformanceResponse {
  rows: DecisionPerformanceRow[];
  totalClosedTrades: number;
  overallWinRatePct: number;
  overallAvgReturnPct: number;
}

// ── Transaction Context Snapshot (Decision Journal) ────────────────────────────
export interface TransactionContextSnapshot {
  id: number;
  transactionId: number;
  capturedAt: string;
  rsiAtEntry: number | null;
  trendShiftAtEntry: string | null;
  fibZoneAtEntry: string | null;
  volumeSignalAtEntry: string | null;
  turnStrengthAtEntry: string | null;
  valueScoreAtEntry: number | null;
  valueTierAtEntry: string | null;
  holdingRoleAtEntry: string | null;
  sectorAllocationStatusAtEntry: string | null;
}

// ── Portfolio Action Score ───────────────────────────────────────────────────────
export interface ActionScoreDto {
  symbol: string;
  companyName: string;
  holdingRole: string;
  watchlistTier: string;
  portfolioNeedScore: number;
  technicalScore: number;
  fundamentalScore: number;
  riskScore: number;
  totalScore: number;
  badge: string; // HIGH_PRIORITY | WATCH | NO_ADD
  trendShift: string;
  rsi: number;
  allocationStatus: string;
  currentPrice: number;
  analysisCurrency?: string | null;
  latestEodSignalState?: string | null;
  latestEodScanType?: string | null;
  latestEodIsNew?: boolean;
  latestEodIsInvalidated?: boolean;
}

// ── Market Leadership ────────────────────────────────────────────────────────────
export type MarketLeadershipTrackerType =
  | 'ETF'
  | 'Theme'
  | 'Future'
  | 'Commodity'
  | 'SectorProxy'
  | 'Other';

export interface CreateMarketLeadershipTrackerRequest {
  symbol: string;
  displayName: string | null;
  trackerType: MarketLeadershipTrackerType;
}

export interface MarketLeadershipTrackerDto {
  id: number;
  symbol: string;
  displayName: string;
  trackerType: MarketLeadershipTrackerType;
}

export interface MarketLeadershipRow {
  id: number;
  symbol: string;
  displayName: string;
  trackerType: MarketLeadershipTrackerType;
  hasTechnicalData: boolean;
  dataError: string | null;
  currentPrice: number;
  dayReturnPct: number;
  fiveDayReturnPct: number;
  previousFiveDayReturnPct: number;
  twentyDayReturnPct: number;
  previousTwentyDayReturnPct: number;
  sma50: number;
  sma200: number;
  priceVsSma50Pct: number;
  priceVsSma200Pct: number;
  sma50VsSma200Pct: number;
  trendState: string;
  momentumState: string;
  maStructure: string;
  maBadge: string;
  lastCross: string | null;
  lastCrossDate: string | null;
  lastCrossTradingDaysAgo: number | null;
  momentumReason: string;
  priceStructure: PriceStructureResult;
  leadershipSignal: 'Emerging' | 'Leading' | 'Neutral' | 'Cooling' | 'Weak';
  leadershipReason: string;
  analysisTicker?: string | null;
  analysisMarket?: string | null;
  analysisCurrency?: string | null;
  usesUnderlyingSecurity?: boolean;
}

export interface PriceStructureResult {
  symbol: string;
  label: string;
  quality: number;
  patternStart: string | null;
  upperTrendline: number;
  lowerTrendline: number;
  startWidth: number;
  currentWidth: number;
  contractionPercent: number;
  projectedApexDate: string | null;
  tradingDaysToApex: number | null;
  pivotHighs: number;
  pivotLows: number;
  atr: number;
  ema9: number;
  volumeRatio20: number;
  rawPivotHighCount: number;
  rawPivotLowCount: number;
  independentUpperTouchCount: number;
  independentLowerTouchCount: number;
  upperFitQuality: number;
  lowerFitQuality: number;
  primaryPatternType: string;
  primaryPatternState: string;
  primaryPatternQuality: number;
  primaryPatternHorizon: number | null;
  keyLevelPrice: number | null;
  keyLevelType: string;
  keyLevelOriginalRole?: string;
  keyLevelRole: string;
  keyLevelState: string;
  keyLevelDistancePercent: number | null;
  keyLevelDistanceAtr: number | null;
  keyLevelQuality: number;
  keyLevelSources: string[];
  keyLevelConfluenceCount: number;
  breakoutTriggerPrice: number | null;
  breakdownTriggerPrice: number | null;
  calculatedAt: string;
  patternHorizon: 'NONE' | 'STRUCTURAL' | 'TIGHT';
  patternLookbackSessions: number | null;
  keyLevelLow: number | null;
  keyLevelHigh: number | null;
  dailyHigh: number;
  dailyLow: number;
  eodClose: number;
  channelTouchDetails: Array<{
    touchNumber: number;
    touchDate: string;
    railPrice: number;
    actualLow: number;
    bounceATR: number;
    confirmedBounce: boolean;
  }>;
  hasHardStructuralNegative?: boolean;
}

export interface MarketLeadershipResponse {
  rows: MarketLeadershipRow[];
  emergingCount: number;
  leadingCount: number;
  coolingCount: number;
  neutralCount: number;
  weakCount: number;
  computedAt: string;
}

// ── Performance Summary (Alpha) ───────────────────────────────────────────────────
export interface BenchmarkReturn {
  name: string;
  symbol: string;
  ytdReturnPct: number;
}

export interface PerformanceSummaryResponse {
  portfolioYtdReturnPct: number;
  portfolioYtdDollar: number;
  portfolioStartValue: number;
  portfolioStartDate: string;
  portfolioCurrentValue: number;
  portfolioCurrentDate: string;
  benchmarks: BenchmarkReturn[];
  alphaVsPrimaryBenchmarkPct: number;
  primaryBenchmarkName: string;
  /** False when no snapshot exists before Jan 1 of the current year — the figures are a
   *  "since inception" return using the earliest available snapshot, not a true YTD return. */
  isFullYear: boolean;
}

// ── Audit log ─────────────────────────────────────────────────────────────

export type AuditSource = 'User' | 'System';
export type AuditCategory =
  | 'Stock'
  | 'Option'
  | 'Cash'
  | 'Watchlist'
  | 'Allocation'
  | 'Config'
  | 'User'
  | 'Automation'
  | 'Signals';
export type AuditAction = 'Create' | 'Update' | 'Delete' | 'Restore' | 'Adjust' | 'Other';

// -- Signals Audit ----------------------------------------------------------

export type SignalMyAction = 'Pending' | 'Actioned' | 'Partial' | 'Not Actioned';
export type SignalCategory = 'Entry' | 'TrimExit' | 'Advisory';
export type SignalDirection = 'Buy' | 'Sell' | 'Neutral';
export type SignalHorizonStatus = 'Complete' | 'Pending' | 'NA';
export type SignalLinkKind = 'None' | 'ManualConfirmed' | 'TransactionVerified' | 'LinkBroken';

// -- Final Action (canonical backend engine) ---------------------------------

export interface FinalActionResult {
  symbol: string;
  source: 'Watchlist' | 'Portfolio';
  itemId: number | null;
  role: string | null;
  finalAction: string;
  finalActionId: string;
  category: SignalCategory;
  direction: SignalDirection;
  severity: 'buy' | 'trim' | 'hold' | 'wait';
  priority: 'REQUIRED' | 'DEVELOPING' | 'INFORMATIONAL';
  finalActionClass: string;
  trendSetup: string;
  momentumShift: string;
  baseAction: string;
  gapStatus: string;
  hoverDescription: string;
  reason: string | null;
  hasHardStructuralNegative: boolean;
  analysisPrice: number;
  analysisTicker: string;
  analysisCurrency: string | null;
  trendSetupReason: string;
  momentumShiftReason: string;
  trendSetupClass: string;
  momentumShiftClass: string;
  gapScore: number;
  gapPct: number;
  /** True for an Entry/Add, Trim/Exit or Risk Review decision; the one rule every screen and export uses. */
  isActionable?: boolean;
  reasonCodes?: string[] | null;
  evidenceAsOfUtc?: string | null;
  entryDate?: string | null;
  /** The technical action the portfolio gate replaced (e.g. a stop-review reminder that is not a sell). */
  originalAction?: string | null;
  /** Part of the canonical contract: effective evaluation state and evidence health. */
  evaluationState?: EffectiveState | null;
  evidenceHealth?: 'Fresh' | 'Stale' | 'Missing' | 'Invalid' | 'Failed' | null;
  /** The last valid published action, kept separately when this result is Awaiting Data. */
  lastValidAction?: string | null;
  lastSuccessfulEvaluationUtc?: string | null;
  dataAsOfDate?: string | null;
  expectedSession?: string | null;
  quoteTimestampUtc?: string | null;
  isHistoricalWarning?: boolean;
  /** Verified = a source quote time was checked; Unavailable = only the scan's bar date backs the freshness. */
  quoteVerification?: 'Verified' | 'Unavailable' | null;
  /** Metadata | Suffix | Assumed — how the exchange (session calendar) was resolved. */
  exchangeBasis?: 'Metadata' | 'Suffix' | 'Assumed' | null;
  /** Present on watchlist decisions: the structure / momentum / MA facts behind the entry status. */
  diagnostics?: {
    priceStructureState: string;
    momentumState: string;
    maStructure: string;
    buyScore: number | null;
    role: string;
  } | null;
}

export interface FinalActionSnapshot {
  watchlist: FinalActionResult[];
  portfolio: FinalActionResult[];
  computedAtUtc: string;
}

export interface FinalActionCatalogItem {
  id: string;
  label: string;
  category: SignalCategory;
  direction: SignalDirection;
  group: string;
}

export type PriorityLevel = 'HIGH' | 'WATCH' | 'NONE';

export interface PriorityCandidateEligibility {
  eligible: boolean;
  finalAction: string | null;
  priorityLevel: PriorityLevel;
  reasonExcluded: string[];
}

/** One Priority Candidate as decided by the backend (score + canonical Final Action + eligibility). */
export interface PriorityCandidateApiRow {
  score: ActionScoreDto;
  eligible: boolean;
  priorityLevel: PriorityLevel;
  reasonsExcluded: string[];
  finalAction: string | null;
  finalActionId: string | null;
  finalActionReason: string | null;
  hoverDescription: string | null;
}

export interface PriorityCandidateRow {
  score: ActionScoreDto;
  eligibility: PriorityCandidateEligibility;
}

export interface AuditedActionEntry {
  id: string;
  label: string;
  enabled: boolean;
  custom: boolean;
}

export interface AuditedActionEntryView extends AuditedActionEntry {
  knownToRules: boolean;
  category: SignalCategory;
  direction: SignalDirection;
}

export interface SignalsAuditSettings {
  priorityEnabled: boolean;
  priorityThreshold: number;
  activatedAtUtc: string | null;
  baselineCount: number;
  finalActionEnabled: boolean;
  entries: AuditedActionEntryView[];
  finalActionBaselineCount: number;
}

export interface SignalTypeOption {
  key: string;
  label: string;
  category: SignalCategory;
  direction: SignalDirection;
}

export interface SignalHorizon {
  pct: number | null;
  evalDate: string | null;
  evalPrice: number | null;
  status: SignalHorizonStatus;
}

export interface SignalAuditRow {
  id: number;
  symbol: string;
  role: string | null;
  signalDate: string;
  daysSince: number;
  dateAnomaly: boolean;
  signalType: string;
  signalTypeLabel: string;
  category: SignalCategory;
  direction: SignalDirection;
  source: string;
  signalPrice: number;
  currency: string | null;
  score: number | null;
  return5D: SignalHorizon;
  return20D: SignalHorizon;
  return1M: SignalHorizon;
  today: SignalHorizon;
  /** Price of the linked transaction, and the change from it to the latest close. Null when nothing is linked. */
  actionedPrice?: number | null;
  todayVsActionedPct?: number | null;
  myAction: SignalMyAction;
  hasNote: boolean;
  linkedTransactionId: number | null;
  linkKind: SignalLinkKind;
  origin: string;
  episodeActive: boolean;
  /** Verified = passed the Action Center eligibility rules at capture; Unverified = captured before that check existed. */
  eligibilityStatus: 'Verified' | 'Unverified';
  /** Verified = history matches the recorded price basis; Adjusted = rebased (split) and corrected; Unverified = cannot be proven. */
  priceBasis: 'Verified' | 'Adjusted' | 'Unverified';
  status?: RecommendationStatus;
  /** False = recorded for the Action Center but excluded from performance analytics. */
  performanceAudited?: boolean;
  lastValidatedAtUtc?: string | null;
  statusReason?: string | null;
  supersededByEventId?: number | null;
  /**
   * The CURRENT effective state of a Final Action recommendation, separate from the lifecycle `status` above: a recommendation can
   * be historically Active yet currently AwaitingData and non-actionable. Null for closed lifecycles and non-Final-Action rows.
   */
  effectiveState?: EffectiveState | null;
  isActionable?: boolean | null;
  evidenceHealth?: string | null;
  dataAsOfDate?: string | null;
  reasonCodes?: string[] | null;
  /** When the lifecycle status last changed (UTC). Only the current status is stored, not a full transition history. */
  statusChangedAtUtc?: string | null;
}

export interface SignalAuditPagedResponse {
  items: SignalAuditRow[];
  /** Exact count of every matching authorized record, independent of page size and the evaluation limit. */
  total: number;
  page: number;
  pageSize: number;
  /** Rows actually evaluated for this response. Equals `total` for SQL-backed sorts. */
  evaluated?: number;
  evaluationLimit?: number;
  /** True only when a computed sort (returns, signal type) ranked just the newest `evaluated` of `total` rows. */
  partial?: boolean;
}

export interface SignalEffectivenessRow {
  signalType: string;
  label: string;
  direction: SignalDirection;
  count: number;
  observations5D: number;
  avg5D: number | null;
  observations20D: number;
  avg20D: number | null;
  observations1M: number;
  avg1M: number | null;
  favourableObservations: number;
  favourablePct: number | null;
  actionedCount: number;
}

export interface SignalAuditSummary {
  total: number;
  priorityCandidate: number;
  entryAdd: number;
  trimExit: number;
  actioned: number;
  pendingReview: number;
  /** Deprecated: the page loads effectiveness from its own endpoint; the summary sends an empty list. */
  effectiveness: SignalEffectivenessRow[];
  /** KPIs are computed over the newest `evaluated` rows; `total` is the exact matching count. */
  evaluated?: number;
  evaluationLimit?: number;
  partial?: boolean;
}

export type EffectivenessWindow = '30' | '90' | '180' | 'all';

/** Context filters for the effectiveness view. Ticker, date, status and My Action are deliberately absent. */
export interface SignalEffectivenessQueryParams {
  window: EffectivenessWindow;
  signalType: string[];
  source: string;
  role: string[];
  includeBaselineInStats: boolean;
}

/** Raw returns are the stock's price change; adjusted flips the sign for Sell signals and is null where direction is undefined. */
export interface SignalEffectivenessResultRow {
  signalType: string;
  label: string;
  direction: SignalDirection;
  directionDefined: boolean;
  eligible: number;
  overlappingCount: number;
  observations5D: number;
  rawAvg5D: number | null;
  adjustedAvg5D: number | null;
  observations20D: number;
  rawAvg20D: number | null;
  adjustedAvg20D: number | null;
  observations1M: number;
  rawAvg1M: number | null;
  adjustedAvg1M: number | null;
  favourableObservations: number;
  favourablePct: number | null;
  actionedCount: number;
}

export interface SignalEffectivenessCoverage {
  cohortEvents: number;
  withPriceHistory: number;
  noPriceHistory: number;
  overlappingEvents: number;
  complete: boolean;
  batches: number;
  note: string | null;
}

export interface SignalEffectivenessResult {
  window: EffectivenessWindow;
  windowStart: string | null;
  windowEnd: string;
  coverage: SignalEffectivenessCoverage;
  rows: SignalEffectivenessResultRow[];
}

export interface SignalLinkedTransaction {
  id: number;
  symbol: string;
  transactionType: string | null;
  accountType: string | null;
  openDate: string | null;
  closeDate: string | null;
  shares: number;
  price: number;
  decisionSource: string | null;
}

/** Point-in-time context as captured; every field may be absent for older or partial captures. */
export interface SignalContext {
  score?: {
    total?: number;
    portfolioNeed?: number;
    technical?: number;
    fundamental?: number;
    risk?: number;
    badge?: string;
  };
  trendShift?: string;
  rsi?: number;
  allocationStatus?: string;
  watchlistTier?: string;
  eodSignalState?: string | null;
  eodScanType?: string | null;
  finalAction?: string | null;
  finalActionReason?: string | null;
  trendSetup?: string | null;
  baseAction?: string | null;
  hover?: string | null;
  fibonacci?: { zone?: string | null; status?: string | null; level618?: number | null } | null;
  structuralStop?: number | null;
  technicalFacts?: {
    maStructure?: string | null;
    maCrossState?: string | null;
    momentumState?: string | null;
    rsi?: number | null;
    priceStructure?: Record<string, unknown> | null;
  } | null;
}

export interface SignalAuditDetail {
  row: SignalAuditRow;
  capturedAtUtc: string;
  ruleVersion: string | null;
  note: string | null;
  myActionUpdatedUtc: string | null;
  episodeEndedDate: string | null;
  context: SignalContext | null;
  linkedTransaction: SignalLinkedTransaction | null;
}

export interface SignalTransactionCandidate {
  id: number;
  symbol: string;
  transactionType: string | null;
  accountType: string | null;
  date: string | null;
  shares: number;
  price: number;
  decisionSource: string | null;
  daysFromSignal: number;
  isLinked: boolean;
}

export type SignalOriginFilter = 'All' | 'Live' | 'INITIAL_BASELINE';

/** System lifecycle of a recommendation. Independent of the user's response (`SignalMyAction`). */
export type RecommendationStatus = 'Active' | 'Acted' | 'Dismissed' | 'Expired' | 'Invalidated' | 'Superseded';
export type SignalStatusFilter = 'All' | 'Open' | 'Closed' | RecommendationStatus;

export type OpenDecisionBucket = 'EntryAdd' | 'TrimExit' | 'RiskReview';
export type OpenDecisionUrgency = 'Urgent' | 'High' | 'Normal';
export type ActionCenterFilter = 'All' | 'Entry' | 'ReduceExit' | 'Risk' | 'Holdings' | 'Watchlist';

export interface OpenDecision {
  id: number;
  symbol: string;
  context: 'Holding' | 'Watchlist';
  role: string | null;
  actionLabel: string;
  actionId: string;
  bucket: OpenDecisionBucket;
  urgency: OpenDecisionUrgency;
  rationale: string | null;
  evidence: string;
  signalDate: string;
  lastValidatedAtUtc: string | null;
  isStale: boolean;
  status: RecommendationStatus;
  myAction: SignalMyAction;
  signalPrice: number;
  origin: string;
  /** CurrentValid | PriorSessionValid | AwaitingData | Evaluating | HistoricalWarning. */
  effectiveState?: EffectiveState | null;
  ageTradingDays?: number | null;
  isHistoricalWarning?: boolean;
  reasonCodes?: string[] | null;
  createdAtUtc?: string | null;
}

export type EffectiveState =
  | 'CurrentValid'
  | 'PriorSessionValid'
  | 'AwaitingData'
  | 'Evaluating'
  | 'Expired'
  | 'HistoricalWarning';

export interface ActionSummary {
  entryAdd: number;
  trimExit: number;
  riskReview: number;
  totalOpen: number;
  asOfUtc: string;
  lastValidatedAtUtc: string | null;
  /** Recommendations without usable evidence; never part of totalOpen or the actionable counts. */
  awaitingData?: number;
  historicalWarnings?: number;
}

export interface OpenDecisionsResponse {
  summary: ActionSummary;
  items: OpenDecision[];
  validation: ValidationStatus;
  /** Awaiting Data and historical-warning rows: listed so nothing is hidden, never counted. */
  notActionable?: OpenDecision[];
}

/**
 * Validated = the latest EOD evaluation completed and nothing awaits revalidation.
 * Pending = recommendations await revalidation, or no evaluation has completed yet.
 * Unavailable = the latest evaluation failed or is incomplete, or the last complete one is stale.
 */
export type ValidationState = 'Validated' | 'Pending' | 'Unavailable';

export interface ValidationStatus {
  state: ValidationState;
  lastCompleteEvaluationUtc: string | null;
  lastAttemptUtc: string | null;
  pendingCount: number;
}

export interface SignalsAuditQueryParams {
  ticker: string;
  signalType: string[];
  source: 'All' | 'Portfolio' | 'Watchlist';
  role: string[];
  myAction: 'All' | SignalMyAction;
  /** All records, only genuine new signals, or only the initial baseline records. */
  origin: SignalOriginFilter;
  /** Effectiveness excludes initial-baseline records unless this is set. */
  includeBaselineInStats: boolean;
  /** Unaudited recommendations are hidden from the default list; they never count in performance statistics. */
  includeUnaudited: boolean;
  status: SignalStatusFilter;
  from: string;
  to: string;
  sortBy: string;
  sortDir: 'asc' | 'desc';
  page: number;
  pageSize: number;
}

export interface AuditChange {
  field: string;
  old: string | null;
  new: string | null;
}

export interface AuditLogEntry {
  id: number;
  occurredAtUtc: string;
  userId: string | null;
  userName: string | null;
  source: AuditSource;
  category: AuditCategory;
  action: AuditAction;
  entityType: string;
  entityId: string | null;
  symbol: string | null;
  accountType: string | null;
  summary: string;
  changes: AuditChange[];
  correlationId: string | null;
}

export interface AuditPagedResponse {
  items: AuditLogEntry[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AuditFilters {
  categories: AuditCategory[];
  actions: AuditAction[];
  users: { userId: string; userName: string }[];
}

export interface AuditQueryParams {
  page: number;
  pageSize: number;
  sortBy: string;
  sortDir: 'asc' | 'desc';
  from: string | null;
  to: string | null;
  category: AuditCategory[];
  action: AuditAction[];
  source: AuditSource | null;
  userId: string | null;
  q: string;
}
