import type {
  DailySignal,
  DailySignalPagedResponse,
  EodSignalsMeta,
  RsiScanResult,
  ScannerResponse,
  ValueScreenerResult,
  WatchlistSummary,
  YesterdayEodResponse,
  MarketIndicesResponse,
  ActionTrigger,
  TechnicalState,
  ValueTier,
} from '../../src/app/core/models/portfolio.models';
import {
  isoDate,
  isoDateTime,
  makePriceStructure,
  makeScanResult,
  makeStockQuote,
  makeTechnicalFacts,
} from './factories';

// ── Watchlist ────────────────────────────────────────────────────────────────
const watchSeeds: [string, string, number, number, string, string, string][] = [
  ['SHOP', 'Shopify Inc.', 118.4, 2.8, 'Active', 'Swing', 'Technology'],
  [
    'BAM.TO',
    'Brookfield Asset Management Ltd.',
    71.2,
    0.6,
    'Active',
    'Strategic',
    'Financial Services',
  ],
  ['CP.TO', 'Canadian Pacific Kansas City Limited', 108.9, -0.7, 'Active', 'Core', 'Industrials'],
  ['GOOGL', 'Alphabet Inc. Class A', 182.4, 1.4, 'Active', 'Core', 'Communication Services'],
  ['AMZN', 'Amazon.com, Inc.', 196.7, -1.2, 'Universe', 'Strategic', 'Consumer Cyclical'],
  ['T.TO', 'TELUS Corporation', 22.6, -0.4, 'Strategic', 'Speculative', 'Communication Services'],
  ['ABX.TO', 'Barrick Gold Corporation', 26.3, 1.9, 'Active', 'Speculative', 'Basic Materials'],
  ['COST', 'Costco Wholesale Corporation', 905.1, 0.3, 'Active', 'Core', 'Consumer Defensive'],
  ['LLY', 'Eli Lilly and Company', 801.2, 2.2, 'Universe', 'Strategic', 'Healthcare'],
  ['FTS.TO', 'Fortis Inc.', 61.8, 0.2, 'Strategic', 'Core', 'Utilities'],
];

export const watchlistSummaries: WatchlistSummary[] = watchSeeds.map(
  ([symbol, name, price, chg, tier, role, sector], i) => ({
    item: {
      id: i + 1,
      symbol,
      notes: i % 3 === 0 ? 'Waiting for pullback to the 50-day average.' : '',
      addedAt: isoDateTime(-120 + i * 9),
      role,
      isFavorite: i % 4 === 0,
      earningsDate: i % 2 === 0 ? isoDate(5 + i) : null,
      watchlistTier: tier,
    },
    quote: makeStockQuote(symbol, name, price, chg, { sector }),
    priceStructure: makePriceStructure(symbol, { eodClose: price }),
    technicalFacts: makeTechnicalFacts(symbol, { rsi: 30 + ((i * 11) % 45) }),
  }),
);

// ── Scanner ──────────────────────────────────────────────────────────────────
const oversold: RsiScanResult[] = [
  makeScanResult('RY.TO', 'Royal Bank of Canada', {
    currentPrice: 171.2,
    rsi: 27.4,
    sector: 'Financial Services',
  }),
  makeScanResult('ENB.TO', 'Enbridge Inc.', {
    currentPrice: 55.9,
    rsi: 29.1,
    status: 'Confirmed',
    sector: 'Energy',
  }),
  makeScanResult('TD.TO', 'The Toronto-Dominion Bank', {
    currentPrice: 79.4,
    rsi: 24.8,
    status: 'EarlyWarning',
    reversalProbability: 'Medium',
    sector: 'Financial Services',
  }),
  makeScanResult('AMZN', 'Amazon.com, Inc.', {
    currentPrice: 196.7,
    rsi: 28.9,
    volumeSignal: 'Low-Volume Trap',
    reversalProbability: 'Low',
    sector: 'Consumer Cyclical',
  }),
  makeScanResult('T.TO', 'TELUS Corporation', {
    currentPrice: 22.6,
    rsi: 26.2,
    trendShift: '🟡 Stabilizing',
    sector: 'Communication Services',
  }),
  makeScanResult('CP.TO', 'Canadian Pacific Kansas City Limited', {
    currentPrice: 108.9,
    rsi: 29.8,
    stageStatus: 'STAGED',
    sector: 'Industrials',
  }),
];

const overbought: RsiScanResult[] = [
  makeScanResult('NVDA', 'NVIDIA Corporation', {
    currentPrice: 131.6,
    rsi: 79.2,
    scanType: 'Overbought',
    status: 'Confirmed',
    macdCrossover: 'Bearish',
    bollingerPosition: 'Above Upper',
    trendShift: '🔴 Still Rising',
    sector: 'Technology',
    channelState: 'CHANNEL_ACTIVE',
  }),
  makeScanResult('LLY', 'Eli Lilly and Company', {
    currentPrice: 801.2,
    rsi: 77.5,
    scanType: 'Overbought',
    status: 'EodConfirm',
    trendShift: '🟢 Bear Turn',
    sector: 'Healthcare',
  }),
  makeScanResult('COST', 'Costco Wholesale Corporation', {
    currentPrice: 905.1,
    rsi: 76.1,
    scanType: 'Overbought',
    status: 'EarlyWarning',
    sector: 'Consumer Defensive',
  }),
  makeScanResult('MSFT', 'Microsoft Corporation', {
    currentPrice: 432.8,
    rsi: 75.9,
    scanType: 'Overbought',
    status: 'EodConfirm',
    sector: 'Technology',
  }),
];

export const scannerResponse: ScannerResponse = {
  oversoldChain: oversold,
  overboughtChain: overbought,
  scannedAt: isoDateTime(0, 15),
  isDemo: false,
  market: 'TSX',
};

export const yesterdayEod: YesterdayEodResponse = {
  hasData: true,
  signalDate: isoDate(-1),
  isMorningWindow: false,
  signals: [...oversold, ...overbought].slice(0, 6).map((r) => ({
    symbol: r.symbol,
    analysisTicker: r.symbol,
    analysisCurrency: r.analysisCurrency,
    companyName: r.companyName,
    scanType: r.scanType,
    rsi: r.rsi,
    price: r.currentPrice,
    triggerDetails: r.triggerDetails,
    scannedAt: isoDateTime(-1, 21),
  })),
};

export const marketIndices: MarketIndicesResponse = {
  fetchedAt: isoDateTime(0, 15),
  indices: [
    {
      symbol: '^GSPTSE',
      name: 'S&P/TSX Composite',
      price: 24310.5,
      change: 112.4,
      changePercent: 0.46,
    },
    { symbol: '^GSPC', name: 'S&P 500', price: 5842.1, change: -14.2, changePercent: -0.24 },
    { symbol: '^IXIC', name: 'NASDAQ', price: 18455.9, change: 63.7, changePercent: 0.35 },
    { symbol: '^VIX', name: 'VIX', price: 15.8, change: -0.4, changePercent: -2.47 },
  ],
};

// ── EOD signals ──────────────────────────────────────────────────────────────
const signalStates = ['Active', 'FollowThrough', 'Invalidated', 'Expired', 'Reversed'] as const;

export const eodSignalItems: DailySignal[] = [
  ...oversold,
  ...overbought,
  ...oversold.slice(0, 4),
].map((r, i) => ({
  id: i + 1,
  symbol: r.symbol,
  companyName: r.companyName,
  scanType: r.scanType,
  signalType: r.status,
  rsi: r.rsi,
  price: r.currentPrice,
  triggerDetails: r.triggerDetails,
  signalDate: isoDate(-(i % 6)),
  recordedAt: isoDateTime(-(i % 6), 21),
  tradingDate: isoDate(-(i % 6)),
  scannedAt: isoDateTime(-(i % 6), 21),
  tradingSessionsPassed: i % 6,
  ruleVersion: i % 3 === 0 ? 'Legacy' : 'Enhanced',
  signalState: signalStates[i % signalStates.length],
  sector: r.sector,
  reversalProbability: r.reversalProbability,
  volumeSignal: r.volumeSignal,
  notes: i % 4 === 0 ? 'Watching for follow-through above the 9-EMA.' : null,
  updatedAt: i % 2 === 0 ? isoDateTime(-(i % 6), 22) : null,
  trendShift: r.trendShift,
  rsiDelta1D: r.rsiDelta1D,
  entryPrice: r.currentPrice,
  stopLossPrice: +(r.currentPrice * 0.94).toFixed(2),
  riskPerShare: +(r.currentPrice * 0.06).toFixed(2),
  positionSizingShares: 120,
  positionSizingRiskAmount: 500,
  positionSizingPositionValue: +(r.currentPrice * 120).toFixed(2),
  positionSizingLimitingReason: 'Risk budget',
  sma200: +(r.currentPrice * 0.97).toFixed(2),
  ema9AtEntry: +(r.currentPrice * 1.01).toFixed(2),
  ema9ConfirmedAtEntry: i % 2 === 0,
  fib61_8AtSignal: +(r.currentPrice * 0.96).toFixed(2),
  fibZoneAtSignal: 'Value Zone',
  fibStatusAtSignal: 'Testing 61.8',
  analysisTicker: r.symbol,
  analysisCurrency: r.analysisCurrency,
}));

export function eodPage(page: number, pageSize: number): DailySignalPagedResponse {
  const start = (page - 1) * pageSize;
  return {
    items: eodSignalItems.slice(start, start + pageSize),
    totalCount: eodSignalItems.length,
    page,
    pageSize,
  };
}

export const eodMeta: EodSignalsMeta = {
  tickers: [...new Set(eodSignalItems.map((s) => s.symbol))].sort(),
  scanTypes: ['Oversold', 'Overbought'],
  signalTypes: ['EodConfirm', 'Confirmed', 'EarlyWarning'],
  signalStates: [...signalStates],
  ruleVersions: ['Legacy', 'Enhanced'],
  minDate: isoDate(-30),
  maxDate: isoDate(0),
  totalCount: eodSignalItems.length,
};

// ── Value screener ───────────────────────────────────────────────────────────
const tiers: ValueTier[] = ['HighConviction', 'FairValue', 'ValueTrap'];
const states: TechnicalState[] = [
  'DeepValueReversal',
  'OverboughtMomentum',
  'OverboughtPullback',
  'SidewaysConsolidation',
  'MeanReversion',
  'HighVolumeExhaustion',
];
const triggers: ActionTrigger[] = [
  'AccumulateYield',
  'AccumulateValue',
  'BuyLimitAlert',
  'HoldRideTrend',
  'ValueTrapWarning',
  'Observe',
];

function screenerRows(
  origin: 'Portfolio' | 'Watchlist',
  symbols: [string, string, number][],
): ValueScreenerResult[] {
  return symbols.map(([symbol, description, price], i) => ({
    symbol,
    description,
    origin,
    technicalState: states[i % states.length],
    tier: tiers[i % tiers.length],
    score: 82 - i * 6,
    actionTrigger: triggers[i % triggers.length],
    scoreEarningsYield: 14 - (i % 4),
    scoreFcfYield: 12 - (i % 5),
    scorePriceToBook: 10 - (i % 3),
    scorePiotroski: 15 - (i % 4),
    scoreRoic: 11 - (i % 3),
    earningsYield: 6.2 - i * 0.4,
    fcfYieldProxy: 5.1 - i * 0.3,
    priceToBook: 1.4 + i * 0.35,
    piotroskiScore: 8 - (i % 5),
    roicProxy: 14.5 - i,
    dividendYield: 3.2 - i * 0.25,
    currentPrice: price,
    currentRsi: 34 + i * 5,
    week52High: price * 1.22,
    week52Low: price * 0.74,
    sector: ['Financial Services', 'Energy', 'Technology', 'Utilities'][i % 4],
    analyzedAt: isoDateTime(-1, 21),
  }));
}

export const valueScreenerLatest = {
  portfolio: screenerRows('Portfolio', [
    ['RY.TO', 'Royal Bank of Canada', 171.2],
    ['ENB.TO', 'Enbridge Inc.', 55.9],
    ['TD.TO', 'The Toronto-Dominion Bank', 79.4],
    ['CNQ.TO', 'Canadian Natural Resources', 46.15],
    ['XEQT.TO', 'iShares Core Equity ETF', 34.85],
    ['MSFT', 'Microsoft Corporation', 432.8],
  ]),
  portfolioRunAt: isoDateTime(-1, 21),
  watchlist: screenerRows('Watchlist', [
    ['BAM.TO', 'Brookfield Asset Management', 71.2],
    ['CP.TO', 'Canadian Pacific Kansas City', 108.9],
    ['T.TO', 'TELUS Corporation', 22.6],
    ['FTS.TO', 'Fortis Inc.', 61.8],
    ['ABX.TO', 'Barrick Gold', 26.3],
  ]),
  watchlistRunAt: isoDateTime(-1, 21),
};
