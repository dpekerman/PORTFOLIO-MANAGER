import type {
  ActionScoreDto,
  AllocationRiskConfig,
  AnalyticsDecisionPerformanceResponse,
  AutomationRunLogDto,
  AutomationSettingsDto,
  AutomationTaskStatusDto,
  AutomationTimezoneDiagnosticsDto,
  DashboardAllocation,
  DashboardEodSummary,
  DashboardResponse,
  MarketLeadershipResponse,
  MarketLeadershipRow,
  OpenDecision,
  OpenDecisionsResponse,
  PerformanceSummaryResponse,
  PortfolioActionDto,
  PortfolioBetaResult,
  PortfolioValueHistoryDto,
  UserInfo,
} from '../../src/app/core/models/portfolio.models';
import { isoDate, isoDateTime, makePriceStructure } from './factories';
import { marketIndices, scannerResponse } from './market';

// ── Auth / admin ─────────────────────────────────────────────────────────────
/** Admin so `canWrite`/`isAdmin` are true and every action button and admin card renders. */
export const adminUser: UserInfo = {
  id: 'e2e-admin-1',
  displayName: 'E2E Admin',
  email: 'admin@e2e.test',
  roles: ['Admin'],
};

export const allUsers: UserInfo[] = [
  adminUser,
  { id: 'e2e-trader-2', displayName: 'Taylor Trader', email: 'taylor@e2e.test', roles: ['Trader'] },
  { id: 'e2e-viewer-3', displayName: 'Vic Viewer', email: 'vic@e2e.test', roles: ['Viewer'] },
];

export const automationSettings: AutomationSettingsDto = {
  enabled: true,
  wakeTimeEt: '15:25',
  keepAwakeUntilEtOverride: null,
  computedKeepAwakeUntilEt: '16:45',
  completionGraceMinutes: 15,
  maxPollMinutes: 90,
  eodWindowStartEt: '15:30',
  eodWindowEndEt: '16:00',
  eodWindowEnabled: true,
  valueScreenerScheduledTimeEt: '17:00',
  valueScreenerEnabled: true,
  secretConfigured: true,
  missedRunAlertTimeEt: '18:30',
};

export const automationRun: AutomationRunLogDto = {
  runId: 'run-0001',
  tradingDate: isoDate(-1),
  triggerType: 'Scheduled',
  triggerCorrelationId: 'corr-abc123',
  scheduledStartUtc: isoDateTime(-1, 19),
  actualStartUtc: isoDateTime(-1, 19),
  completedAtUtc: isoDateTime(-1, 20),
  overallStatus: 'Success',
  refreshStatus: 'Succeeded',
  refreshStartedAtUtc: isoDateTime(-1, 19),
  refreshCompletedAtUtc: isoDateTime(-1, 19),
  portfolioSymbolCount: 9,
  watchlistSymbolCount: 10,
  rsiStatus: 'Succeeded',
  rsiCompletedAtUtc: isoDateTime(-1, 20),
  eodSignalsPersistedCount: 14,
  snapshotStatus: 'Succeeded',
  snapshotCompletedAtUtc: isoDateTime(-1, 20),
  snapshotSource: 'EodAuto',
  valueScreenerStatus: 'Succeeded',
  valueScreenerLastRunAtUtc: isoDateTime(-1, 21),
  powerRequestAcquiredAtUtc: isoDateTime(-1, 19),
  powerRequestReleasedAtUtc: isoDateTime(-1, 21),
  lastHeartbeatAtUtc: isoDateTime(-1, 21),
  lastHeartbeatStep: 'ValueScreener',
  errorStep: null,
  errorMessage: null,
  machineName: 'E2E-HOST',
};

export const automationTaskStatus: AutomationTaskStatusDto = {
  exists: true,
  state: 'Ready',
  nextRunTime: isoDateTime(1, 19),
  lastRunTime: isoDateTime(-1, 19),
  lastTaskResult: 0,
};

export const automationTimezone: AutomationTimezoneDiagnosticsDto = {
  businessTimeZoneLabel: 'Eastern Time (ET)',
  windowsLocalTimeZoneLabel: 'Pacific Standard Time',
  wakeTimeEtDisplay: '3:25 PM ET',
  wakeTimeLocalDisplay: '12:25 PM PT',
  taskExists: true,
  taskNextRunEtDisplay: '3:25 PM ET',
  taskNextRunLocalDisplay: '12:25 PM PT',
  expectedNextRunEtDisplay: '3:25 PM ET',
  taskMatchesExpected: true,
  driftMinutes: 0,
  warning: null,
};

// ── Dashboard ────────────────────────────────────────────────────────────────
const allocationRow = (label: string, percent: number, target: number): DashboardAllocation => ({
  label,
  value: percent * 3200,
  percent,
  targetPercent: target,
  delta: +(percent - target).toFixed(1),
  status: Math.abs(percent - target) < 3 ? 'good' : percent > target ? 'over' : 'under',
});

export const dashboardResponse: DashboardResponse = {
  updatedAt: isoDateTime(0, 15),
  summary: {
    totalValue: 318_450.72,
    todayChange: 1_842.3,
    todayChangePercent: 0.58,
    todayStocksChange: 1_790.1,
    todayCashChange: 0,
    todayOptionsChange: 52.2,
    weekChange: 4_210.55,
    weekChangePercent: 1.34,
    monthChange: -2_104.8,
    monthChangePercent: -0.66,
    oversoldCount: 6,
    overboughtCount: 4,
    performance: [
      { period: 'Day', status: 'Available', reason: null, baselineDate: '2026-10-29', valueChange: 11_842.3, netExternalFlow: 10_000, investmentGainLoss: 1_842.3, investmentGainLossPercent: null },
      { period: 'Week', status: 'Available', reason: null, baselineDate: '2026-10-23', valueChange: 4_210.55, netExternalFlow: 0, investmentGainLoss: 4_210.55, investmentGainLossPercent: 1.34 },
      { period: 'Month', status: 'Unavailable', reason: 'A USD or non-reporting-currency cash flow is not converted.', baselineDate: '2026-09-30', valueChange: null, netExternalFlow: null, investmentGainLoss: null, investmentGainLossPercent: null },
      { period: 'YTD', status: 'Updating', reason: 'Snapshot reseal is pending.', baselineDate: null, valueChange: null, netExternalFlow: null, investmentGainLoss: null, investmentGainLossPercent: null },
    ],
  },
  topMovers: [
    {
      symbol: 'NVDA',
      companyName: 'NVIDIA Corporation',
      changePercent: 3.9,
      isPortfolio: true,
      isWatchlist: false,
    },
    {
      symbol: 'SHOP.TO',
      companyName: 'Shopify Inc.',
      changePercent: 2.41,
      isPortfolio: true,
      isWatchlist: false,
    },
    {
      symbol: 'LLY',
      companyName: 'Eli Lilly and Company',
      changePercent: 2.2,
      isPortfolio: false,
      isWatchlist: true,
    },
    {
      symbol: 'ABX.TO',
      companyName: 'Barrick Gold Corporation',
      changePercent: 1.9,
      isPortfolio: false,
      isWatchlist: true,
    },
    {
      symbol: 'CNQ.TO',
      companyName: 'Canadian Natural Resources',
      changePercent: 1.6,
      isPortfolio: true,
      isWatchlist: false,
    },
  ],
  bottomMovers: [
    {
      symbol: 'AMZN',
      companyName: 'Amazon.com, Inc.',
      changePercent: -1.2,
      isPortfolio: false,
      isWatchlist: true,
    },
    {
      symbol: 'ENB.TO',
      companyName: 'Enbridge Inc.',
      changePercent: -1.05,
      isPortfolio: true,
      isWatchlist: false,
    },
    {
      symbol: 'TD.TO',
      companyName: 'The Toronto-Dominion Bank',
      changePercent: -0.9,
      isPortfolio: true,
      isWatchlist: false,
    },
    {
      symbol: 'CP.TO',
      companyName: 'Canadian Pacific Kansas City',
      changePercent: -0.7,
      isPortfolio: false,
      isWatchlist: true,
    },
    {
      symbol: 'T.TO',
      companyName: 'TELUS Corporation',
      changePercent: -0.4,
      isPortfolio: false,
      isWatchlist: true,
    },
  ],
  valueHistory: Array.from({ length: 30 }, (_, i) => ({
    date: isoDate(-(29 - i)),
    totalValue: 300_000 + i * 620 + Math.sin(i / 3) * 2_400,
  })),
  marketIndices: marketIndices.indices,
  allocation: [
    allocationRow('Technology', 34.2, 30),
    allocationRow('Financial Services', 27.5, 25),
    allocationRow('Energy', 13.1, 15),
    allocationRow('ETF', 11.4, 15),
    allocationRow('Cash', 8.3, 10),
  ],
  nextSevenDayEarnings: [
    { symbol: 'SHOP', companyName: 'Shopify Inc.', earningsDate: isoDate(2), source: 'Watchlist' },
    {
      symbol: 'RY.TO',
      companyName: 'Royal Bank of Canada',
      earningsDate: isoDate(4),
      source: 'Portfolio',
    },
    {
      symbol: 'CP.TO',
      companyName: 'Canadian Pacific Kansas City',
      earningsDate: isoDate(6),
      source: 'Watchlist',
    },
  ],
  rsiSection: {
    oversoldCount: 6,
    overboughtCount: 4,
    newTodayCount: 3,
    actionRequiredCount: 2,
    oversoldSignals: scannerResponse.oversoldChain.map((r, i) => ({
      symbol: r.symbol,
      companyName: r.companyName,
      rsi: r.rsi,
      momentumShift: r.trendShift,
      volumeSignal: r.volumeSignal,
      returnPct: -3.2 + i,
      action: 'BUY - Add on weakness',
      signalStatus: r.status,
      isInPortfolio: i % 2 === 0,
      isInWatchlist: i % 2 === 1,
      isNewToday: i === 0,
      isActionRequired: i < 2,
      severity: i < 2 ? ('REQUIRED' as const) : ('DEVELOPING' as const),
      channelState: r.channelState,
    })),
    overboughtSignals: scannerResponse.overboughtChain.map((r, i) => ({
      symbol: r.symbol,
      companyName: r.companyName,
      rsi: r.rsi,
      momentumShift: r.trendShift,
      volumeSignal: r.volumeSignal,
      returnPct: 12.4 - i,
      action: 'TRIM - Overextended',
      signalStatus: r.status,
      isInPortfolio: i % 2 === 0,
      isInWatchlist: i % 2 === 1,
      isNewToday: false,
      isActionRequired: false,
      severity: 'INFORMATIONAL' as const,
      channelState: r.channelState,
    })),
  },
  roleAllocation: [
    allocationRow('Core', 52, 50),
    allocationRow('Strategic', 28, 30),
    allocationRow('Swing', 8, 10),
    allocationRow('Speculative', 12, 10),
  ],
};

const actionSeeds: [string, string, string, string, string, string][] = [
  ['ENB.TO', 'Enbridge Inc.', 'Strategic', 'BUY - Add on weakness', 'buy', 'REQUIRED'],
  ['MSFT', 'Microsoft Corporation', 'Core', 'TRIM - Overextended', 'trim', 'DEVELOPING'],
  [
    'TD.TO',
    'The Toronto-Dominion Bank',
    'Strategic',
    'REVIEW - Below cost',
    'review',
    'DEVELOPING',
  ],
  ['SHOP.TO', 'Shopify Inc.', 'Strategic', 'HOLD - Trend intact', 'hold', 'INFORMATIONAL'],
  ['NVDA', 'NVIDIA Corporation', 'Speculative', 'WAIT - Extended', 'wait', 'INFORMATIONAL'],
  ['RY.TO', 'Royal Bank of Canada', 'Core', 'BUY - Value zone', 'buy', 'DEVELOPING'],
];

export const portfolioActions: PortfolioActionDto[] = actionSeeds.map(
  ([symbol, companyName, holdingRole, actionLabel, actionSeverity, actionPriority], i) => ({
    symbol,
    companyName,
    holdingRole,
    scanType: i % 2 === 0 ? 'Oversold' : 'Overbought',
    rsi: 28 + i * 7,
    trendShift: '🟢 Bull Turn',
    fibZone: 'Value Zone',
    chaseRisk: '',
    allocationStatus: ['under', 'over', 'on-target', ''][i % 4],
    actionLabel,
    actionSeverity,
    actionPriority,
    isInPortfolio: true,
    isInWatchlist: false,
    channelState: 'LOWER_RAIL_APPROACHING',
    channelDirection: 'RISING',
    channelQuality: 74,
    priorConfirmedLowerTouches: 2,
    lowerRailToday: 82.1,
    eodClose: 85.4,
    distanceToLowerRailPercent: 1.9,
    distanceToLowerRailATR: 0.8,
    lastLowerTouchDate: isoDate(-9),
    nearestOpenGapAbove: null,
    channelTouchDetails: [],
    maStructure: 'Bullish',
    momentumState: 'Improving',
    priceStructure: makePriceStructure(symbol),
    inclusionReason: 'Active EOD signal on a held position',
    reasonExcludedFromActionCenter: null,
    technicalCalculatedAt: isoDateTime(-1, 21),
    latestEodSignalState: 'Active',
    latestEodScanType: 'Oversold',
    latestEodTrendShift: '🟢 Bull Turn',
    latestEodIsNew: i === 0,
    latestEodIsInvalidated: false,
  }),
);

const decision = (
  id: number,
  symbol: string,
  context: OpenDecision['context'],
  actionId: string,
  actionLabel: string,
  bucket: OpenDecision['bucket'],
  urgency: OpenDecision['urgency'],
  overrides: Partial<OpenDecision> = {},
): OpenDecision => ({
  id,
  symbol,
  context,
  role: context === 'Holding' ? 'Core' : 'Strategic',
  actionLabel,
  actionId,
  bucket,
  urgency,
  rationale: 'Volume and momentum confirmed on the latest EOD evaluation',
  evidence: 'Signal Tracker / EOD',
  signalDate: isoDate(-1),
  lastValidatedAtUtc: isoDateTime(-1, 21),
  isStale: false,
  status: 'Active',
  myAction: 'Pending',
  signalPrice: 100,
  origin: 'Live',
  ...overrides,
});

/** Current validated decisions: 1 risk review, 1 trim/exit, 2 entries (one stale baseline record). */
export const openDecisionsResponse: OpenDecisionsResponse = {
  summary: {
    entryAdd: 2,
    trimExit: 1,
    riskReview: 1,
    totalOpen: 4,
    asOfUtc: isoDateTime(0, 9),
    lastValidatedAtUtc: isoDateTime(-1, 21),
  },
  validation: { state: 'Validated', lastCompleteEvaluationUtc: isoDateTime(-1, 21), lastAttemptUtc: isoDateTime(-1, 21), pendingCount: 0 },
  items: [
    decision(1, 'ENB.TO', 'Holding', 'CUT LOSS/THESIS REVIEW', 'Cut Loss / Thesis Review', 'RiskReview', 'Urgent'),
    decision(2, 'WMT.TO', 'Holding', 'TRIM/REVIEW', 'Trim / Review', 'TrimExit', 'High'),
    decision(3, 'HPS-A.TO', 'Holding', 'ADD/STAGED ENTRY', 'Add / Staged Entry', 'EntryAdd', 'High', {
      myAction: 'Not Actioned',
    }),
    decision(4, 'CP.TO', 'Watchlist', 'STARTER ENTRY', 'Starter Entry', 'EntryAdd', 'Normal', {
      isStale: true,
      origin: 'INITIAL_BASELINE',
    }),
  ],
};

export const emptyOpenDecisions: OpenDecisionsResponse = {
  summary: { entryAdd: 0, trimExit: 0, riskReview: 0, totalOpen: 0, asOfUtc: isoDateTime(0, 9), lastValidatedAtUtc: null },
  validation: { state: 'Validated', lastCompleteEvaluationUtc: isoDateTime(-1, 21), lastAttemptUtc: isoDateTime(-1, 21), pendingCount: 0 },
  items: [],
};

/** What the live app showed after the migration: recommendations exist but no EOD evaluation has confirmed them yet. */
export const pendingOpenDecisions: OpenDecisionsResponse = {
  summary: { entryAdd: 0, trimExit: 0, riskReview: 0, totalOpen: 0, asOfUtc: isoDateTime(0, 9), lastValidatedAtUtc: null },
  validation: { state: 'Pending', lastCompleteEvaluationUtc: null, lastAttemptUtc: null, pendingCount: 6 },
  items: [],
};

/** The latest EOD evaluation failed or is incomplete. */
export const unavailableOpenDecisions: OpenDecisionsResponse = {
  summary: { entryAdd: 0, trimExit: 0, riskReview: 0, totalOpen: 0, asOfUtc: isoDateTime(0, 9), lastValidatedAtUtc: null },
  validation: { state: 'Unavailable', lastCompleteEvaluationUtc: isoDateTime(-3, 21), lastAttemptUtc: isoDateTime(-1, 21), pendingCount: 2 },
  items: [],
};

export const eodSummary: DashboardEodSummary = {
  tradingDate: isoDate(-1),
  rawRecordCount: 14,
  uniqueTickerCount: 10,
  rows: scannerResponse.oversoldChain.concat(scannerResponse.overboughtChain).map((r, i) => ({
    symbol: r.symbol,
    companyName: r.companyName,
    signal: r.scanType,
    rsi: r.rsi,
    trendShift: r.trendShift,
    signalState: 'Active',
    structure: 'Rising Channel',
    why: r.triggerDetails,
    ownership: (['Portfolio', 'Watchlist', 'Universe'] as const)[i % 3],
    action: ['BUY - Add on weakness', 'TRIM - Overextended', 'WAIT'][i % 3],
    actionPriority: ['REQUIRED', 'DEVELOPING', 'INFORMATIONAL'][i % 3],
    actionResolutionStatus: 'Resolved' as const,
    actionResolutionReason: '',
  })),
};

const leadershipTypes = ['ETF', 'Theme', 'Future', 'Commodity', 'SectorProxy', 'Other'] as const;
const leadershipSignals = ['Leading', 'Emerging', 'Neutral', 'Cooling', 'Weak'] as const;

export const marketLeadership: MarketLeadershipResponse = {
  computedAt: isoDateTime(0, 15),
  emergingCount: 2,
  leadingCount: 2,
  neutralCount: 2,
  coolingCount: 1,
  weakCount: 1,
  rows: [
    ['SPY', 'S&P 500 ETF'],
    ['QQQ', 'Nasdaq 100 ETF'],
    ['XLE', 'Energy Select Sector'],
    ['GLD', 'Gold Trust'],
    ['SMH', 'Semiconductor ETF'],
    ['XLU', 'Utilities Select Sector'],
    ['ES=F', 'S&P 500 Futures'],
    ['CL=F', 'Crude Oil Futures'],
  ].map(([symbol, displayName], i): MarketLeadershipRow => ({
    id: i + 1,
    symbol,
    displayName,
    trackerType: leadershipTypes[i % leadershipTypes.length],
    hasTechnicalData: true,
    dataError: null,
    currentPrice: 100 + i * 37.5,
    dayReturnPct: 0.6 - i * 0.25,
    fiveDayReturnPct: 1.8 - i * 0.5,
    previousFiveDayReturnPct: 0.9 - i * 0.2,
    twentyDayReturnPct: 4.2 - i,
    previousTwentyDayReturnPct: 2.1 - i * 0.3,
    sma50: 98 + i * 36,
    sma200: 92 + i * 35,
    priceVsSma50Pct: 3.2 - i * 0.8,
    priceVsSma200Pct: 8.1 - i * 1.4,
    sma50VsSma200Pct: 6.4 - i,
    trendState: i < 5 ? 'Uptrend' : 'Downtrend',
    momentumState: i < 4 ? 'Improving' : 'Fading',
    maStructure: i < 5 ? 'Bullish' : 'Bearish',
    maBadge: i < 5 ? '50>200' : '50<200',
    lastCross: 'GoldenCross',
    lastCrossDate: isoDate(-45 - i),
    lastCrossTradingDaysAgo: 31 + i,
    momentumReason: 'Higher highs on rising volume',
    priceStructure: makePriceStructure(symbol),
    leadershipSignal: leadershipSignals[i % leadershipSignals.length],
    leadershipReason: 'Outperforming the benchmark over 20 sessions',
    analysisTicker: symbol,
    analysisMarket: 'US',
    analysisCurrency: 'USD',
    usesUnderlyingSecurity: false,
  })),
};

export const performanceSummary: PerformanceSummaryResponse = {
  portfolioYtdReturnPct: 11.4,
  portfolioYtdDollar: 32_610.4,
  portfolioStartValue: 285_840.32,
  portfolioStartDate: isoDate(-280),
  portfolioCurrentValue: 318_450.72,
  portfolioCurrentDate: isoDate(0),
  benchmarks: [
    { name: 'S&P/TSX Composite', symbol: '^GSPTSE', ytdReturnPct: 9.2 },
    { name: 'S&P 500', symbol: '^GSPC', ytdReturnPct: 13.8 },
  ],
  alphaVsPrimaryBenchmarkPct: 2.2,
  primaryBenchmarkName: 'S&P/TSX Composite',
  isFullYear: true,
};

export const decisionPerformance: AnalyticsDecisionPerformanceResponse = {
  totalClosedTrades: 12,
  overallWinRatePct: 66.7,
  overallAvgReturnPct: 5.8,
  rows: [
    {
      decisionSource: 'App Signal',
      tradeCount: 5,
      winCount: 4,
      winRatePct: 80,
      avgReturnPct: 8.4,
      avgHoldingDays: 42,
    },
    {
      decisionSource: 'Manual',
      tradeCount: 4,
      winCount: 2,
      winRatePct: 50,
      avgReturnPct: 2.1,
      avgHoldingDays: 96,
    },
    {
      decisionSource: 'Loss Harvest',
      tradeCount: 3,
      winCount: 2,
      winRatePct: 66.7,
      avgReturnPct: 4.9,
      avgHoldingDays: 31,
    },
  ],
};

export const actionScores: ActionScoreDto[] = [
  'BAM.TO',
  'CP.TO',
  'GOOGL',
  'COST',
  'FTS.TO',
  'ABX.TO',
].map((symbol, i) => ({
  symbol,
  companyName: symbol,
  holdingRole: ['Core', 'Strategic', 'Swing'][i % 3],
  watchlistTier: ['Active', 'Strategic', 'Universe'][i % 3],
  portfolioNeedScore: 22 - i,
  technicalScore: 25 - i * 2,
  fundamentalScore: 18 + i,
  riskScore: 10 - (i % 4),
  totalScore: 80 - i * 7,
  badge: ['HIGH_PRIORITY', 'WATCH', 'NO_ADD'][i % 3],
  trendShift: '🟢 Bull Turn',
  rsi: 31 + i * 4,
  allocationStatus: ['under', 'on-target', 'over'][i % 3],
  currentPrice: 60 + i * 20,
  analysisCurrency: 'CAD',
  latestEodSignalState: 'Active',
  latestEodScanType: 'Oversold',
  latestEodIsNew: i === 0,
  latestEodIsInvalidated: false,
}));

// ── Allocation ───────────────────────────────────────────────────────────────
export const allocationRiskConfig: AllocationRiskConfig = {
  riskTargets: [
    { id: 1, role: 'Core', targetPct: 50, displayOrder: 1 },
    { id: 2, role: 'Strategic', targetPct: 30, displayOrder: 2 },
    { id: 3, role: 'Swing', targetPct: 10, displayOrder: 3 },
    { id: 4, role: 'Speculative', targetPct: 10, displayOrder: 4 },
  ],
  sectorTargets: [
    { id: 1, sector: 'Technology', targetPct: 30, displayOrder: 1 },
    { id: 2, sector: 'Financial Services', targetPct: 25, displayOrder: 2 },
    { id: 3, sector: 'Energy', targetPct: 15, displayOrder: 3 },
    { id: 4, sector: 'ETF', targetPct: 15, displayOrder: 4 },
    { id: 5, sector: 'Real Estate', targetPct: 5, displayOrder: 5 },
    { id: 6, sector: 'Cash', targetPct: 10, displayOrder: 6 },
  ],
  positionLimits: [
    { id: 1, role: 'Core', targetPct: 12, displayOrder: 1 },
    { id: 2, role: 'Strategic', targetPct: 8, displayOrder: 2 },
    { id: 3, role: 'Swing', targetPct: 5, displayOrder: 3 },
    { id: 4, role: 'Speculative', targetPct: 3, displayOrder: 4 },
  ],
};

export const portfolioBeta: PortfolioBetaResult = {
  portfolioBeta: 0.94,
  exCashBeta: 1.03,
  cashPct: 8.3,
  proxyPct: 11.4,
  status: 'Good',
  topContributors: [
    { symbol: 'MSFT', weightPct: 14.2, beta: 0.9, isProxy: false },
    { symbol: 'RY.TO', weightPct: 11.5, beta: 0.85, isProxy: false },
    { symbol: 'NVDA', weightPct: 8.3, beta: 1.7, isProxy: false },
    { symbol: 'MANUAL-PRIVATE-RE', weightPct: 8.6, beta: 0.7, isProxy: true },
  ],
};

// ── Value history ────────────────────────────────────────────────────────────
const sources = [
  'EodAuto',
  'ManualRecordNow',
  'SameDayReseal',
  'CashRecalculation',
  'Migration',
] as const;
const statuses = ['Original', 'Resealed', 'CashRecalculated', 'PendingReseal'] as const;

export const valueHistory: PortfolioValueHistoryDto[] = Array.from({ length: 24 }, (_, i) => {
  const stocks = 262_000 + i * 780;
  const options = 3_400 + (i % 5) * 120;
  const cash = 26_500 + (i % 7) * 310;
  const day = -(23 - i);
  return {
    id: i + 1,
    recordedAt: isoDateTime(day, 21),
    recordedDate: isoDate(day),
    totalValue: stocks + options + cash,
    stocksValue: stocks,
    cashValue: cash,
    optionsValue: options,
    source: sources[i % sources.length],
    lastRecalculatedAt: i % 4 === 1 ? isoDateTime(day, 22) : null,
    externalCashFlow: i % 6 === 0 ? 1_500 : 0,
    snapshotStatus: statuses[i % statuses.length],
    hasMismatch: i === 5,
  };
});
