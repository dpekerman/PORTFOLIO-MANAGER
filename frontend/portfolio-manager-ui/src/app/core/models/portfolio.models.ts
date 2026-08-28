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
}

export interface DataRefreshResultDto {
  portfolioSymbolCount: number;
  watchlistSymbolCount: number;
  dashboardRebuilt: boolean;
  refreshedAt: string;
  durationMs: number;
  portfolioSummaries: PortfolioSummary[];
  watchlistSummaries: WatchlistSummary[];
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
  | 'REVERSAL_DEVELOPING'
  | 'BOUNCE_CONFIRMED'
  | 'CHANNEL_BROKEN';

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
export interface CashItem {
  id: number;
  description: string;
  amount: number;
  addedAt: string;
  accountType?: string | null;
  transactionDate?: string | null;
}

export interface AddCashItemRequest {
  description: string;
  amount: number;
  accountType?: string | null;
  transactionDate?: string | null;
}

export interface UpdateCashItemRequest {
  description: string;
  amount: number;
  accountType?: string | null;
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
export interface PortfolioValueHistoryDto {
  id: number;
  recordedAt: string;
  recordedDate: string;
  totalValue: number;
  stocksValue: number;
  cashValue: number;
  optionsValue: number;
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
  rsi: number;
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
  distanceToLowerRailPercent: number;
  distanceToLowerRailATR: number;
  lastLowerTouchDate: string | null;
  nearestOpenGapAbove: number | null;
}

export interface StateChangeDto {
  signalId: number;
  symbol: string;
  companyName: string;
  scanType: string;
  previousState: string;
  newState: string;
  rsi: number;
  trendShift: string;
  changedAt: string;
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
}

// ── Market Leadership ────────────────────────────────────────────────────────────
export interface MarketLeadershipRow {
  sector: string;
  symbolCount: number;
  avgRsi: number;
  avg1MReturnPct: number;
  pctAboveEma20: number;
  leadership: string; // Strong | Improving | Neutral | Weakening | Declining
  leadershipEmoji: string;
}

export interface MarketLeadershipResponse {
  rows: MarketLeadershipRow[];
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
}
