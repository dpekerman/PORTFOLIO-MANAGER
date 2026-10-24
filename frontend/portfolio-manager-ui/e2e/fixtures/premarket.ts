const news = (id: string, headline: string, mock = false) => ({
  evidenceId: id,
  headline,
  publisher: 'Reuters',
  url: `https://example.com/${id}`,
  publishedUtc: '2026-10-20T10:00:00Z',
  retrievedUtc: '2026-10-20T11:00:00Z',
  summary: '',
  isMock: mock,
});

const stock = (symbol: string, kind: 'HOLDING' | 'WATCHLIST', over: Record<string, unknown> = {}) => ({
  symbol,
  companyName: `${symbol} Corp`,
  kind,
  sector: 'Energy',
  shares: kind === 'HOLDING' ? 100 : null,
  averageCost: kind === 'HOLDING' ? 50 : null,
  lastClose: 55,
  priceDate: '2026-10-19',
  unrealizedPct: kind === 'HOLDING' ? 10 : null,
  rsi14: 48.2,
  rsi9: 50,
  change5dPct: 1.2,
  newsStatus: 'NEWS_FOUND',
  news: [news(`A-${symbol}`, `${symbol} announces earnings`)],
  action: 'HOLD',
  priority: 'LOW',
  rationale: 'RSI(14) 48.2 is neutral; no technical trigger.',
  warnings: [],
  eventIds: [],
  ai: null,
  weightPct: kind === 'HOLDING' ? 12.5 : null,
  currency: kind === 'HOLDING' ? 'USD' : null,
  change: null,
  earningsInDays: null,
  ...over,
});

export const preMarketReport = {
  asOfDate: '2026-10-20',
  generatedUtc: '2026-10-20T11:30:00Z',
  isPartial: false,
  isTestRun: true,
  shadowMode: true,
  aiStatus: 'OK (qwen3:4b, 3 calls)',
  holdings: [
    stock('XOM', 'HOLDING', {
      priority: 'HIGH',
      action: 'INVESTIGATE',
      eventIds: ['E1'],
      rationale: 'Material news detected (1): review the linked sources.',
      ai: { bullish: ['Earnings beat reported'], bearish: [], risks: ['Oil volatility'], evidenceIds: ['A-XOM'] },
    }),
    stock('SHOP.TO', 'HOLDING', { sector: 'Technology' }),
  ],
  watchlist: [stock('NVDA', 'WATCHLIST', { priority: 'MEDIUM', action: 'BUY_CANDIDATE', rsi14: 28 })],
  dataQuality: ['News coverage is incomplete: absence of news is NOT evidence that nothing happened.'],
  events: [
    {
      eventId: 'E1',
      category: 'ENERGY_SUPPLY',
      title: 'Crude oil surges as OPEC cuts output',
      mechanism: 'Oil supply news can move producers and fuel-intensive businesses in opposite directions.',
      counterargument: 'Prices may already reflect the news.',
      affectedSectors: ['Energy'],
      affectedSymbols: ['XOM'],
      evidence: [news('A-G1', 'Crude oil surges as OPEC cuts output')],
      aiSummary: 'OPEC announced output cuts.',
      aiPortfolioNote: 'XOM may benefit; effect is uncertain.',
    },
  ],
  metrics: { articlesRetrieved: 6, events: 1, aiCalls: 3, aiDurationMs: 42000, newsDurationMs: 3100, aiModel: 'qwen3:4b' },
};

export const preMarketLatest = {
  id: 7,
  runKey: '2026-10-20|TEST',
  asOfDate: '2026-10-20',
  isTestRun: true,
  status: 'COMPLETE',
  emailStatus: 'SMTP_ACCEPTED',
  emailMessage: 'sent',
  error: null,
  durationMs: 45000,
  startedAtUtc: '2026-10-20T11:00:00Z',
  aiModel: 'qwen3:4b',
  aiCalls: 3,
  aiDurationMs: 42000,
  newsDurationMs: 3100,
  articlesRetrieved: 6,
  report: preMarketReport,
};

export const preMarketRuns = [
  {
    id: 7, runKey: '2026-10-20|TEST', asOfDate: '2026-10-20', isTestRun: true, status: 'COMPLETE',
    emailStatus: 'SMTP_ACCEPTED', durationMs: 45000, startedAtUtc: '2026-10-20T11:00:00Z', aiCalls: 3, aiModel: 'qwen3:4b', articlesRetrieved: 6,
  },
];

export const preMarketSources = [
  {
    id: 3, name: 'Google News search (discovery)', url: 'https://news.google.com/rss/search?q={query}&hl=en-CA',
    providerType: 'RSS', category: 'DISCOVERY', priority: 90, enabled: false, approvalStatus: 'PENDING', markets: 'TSX,US',
    refreshMinutes: 60, timeoutSeconds: 10, maxItems: 12, lastSuccessUtc: null, lastFailureUtc: null,
    lastError: null, avgDurationMs: 0, usefulArticleCount: 0,
  },
  {
    id: 1, name: 'Yahoo Finance company headlines', url: 'https://feeds.finance.yahoo.com/rss/2.0/headline?s={symbol}',
    providerType: 'RSS', category: 'COMPANY', priority: 10, enabled: true, approvalStatus: 'APPROVED', markets: 'TSX,US',
    refreshMinutes: 60, timeoutSeconds: 10, maxItems: 12, lastSuccessUtc: '2026-10-20T11:00:00Z', lastFailureUtc: null,
    lastError: null, avgDurationMs: 420, usefulArticleCount: 18,
  },
  {
    id: 2, name: 'Proposed feed', url: 'https://news.example.com/rss', providerType: 'RSS', category: 'GLOBAL', priority: 50,
    enabled: false, approvalStatus: 'PENDING', markets: 'TSX,US', refreshMinutes: 60, timeoutSeconds: 10, maxItems: 15,
    lastSuccessUtc: null, lastFailureUtc: null, lastError: null, avgDurationMs: 0, usefulArticleCount: 0,
  },
];

export const preMarketEvaluation = {
  runId: 7,
  asOfDate: '2026-10-20',
  benchmark: 'SPY (US) / XIU.TO (Canada)',
  note: 'Unadjusted closes.',
  rows: [
    { symbol: 'XOM', action: 'INVESTIGATE', priority: 'HIGH', referenceClose: 55, ret1: 1.5, ret5: 3.2, ret20: null, excess1: 0.8, excess5: 1.1, excess20: null },
  ],
};

export const preMarketConfig = {
  id: 1, scheduleEnabled: false, startTimeLocal: '07:00', firstRunDate: null, asOfDate: null, deadlineLocal: '09:25',
  shadowMode: true, userId: null, discoveryMode: 'APPROVED_ONLY', aiEnabled: true, aiMaxCalls: 20,
};

export const preMarketStatus = {
  aiHealthy: true, aiMessage: 'Ollama ready, model qwen3:4b.', aiModel: 'qwen3:4b', nextRunUtc: null, torontoToday: '2026-10-23',
};
