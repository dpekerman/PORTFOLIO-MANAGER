export type PreMarketPriority = 'HIGH' | 'MEDIUM' | 'LOW' | 'NONE';
export type NewsSourceCategory = 'COMPANY' | 'GLOBAL' | 'OFFICIAL' | 'DISCOVERY';
export type FeedbackRating = 'REVIEWED' | 'USEFUL' | 'NOT_USEFUL' | 'ACTION_TAKEN' | 'NO_ACTION_TAKEN';

export interface PreMarketConfig {
  id: number;
  scheduleEnabled: boolean;
  startTimeLocal: string;
  firstRunDate: string | null;
  asOfDate: string | null;
  deadlineLocal: string;
  shadowMode: boolean;
  userId: string | null;
  discoveryMode: string;
  aiEnabled: boolean;
  aiMaxCalls: number;
}

export interface PreMarketStatus {
  aiHealthy: boolean;
  aiMessage: string;
  aiModel: string;
  nextRunUtc: string | null;
  torontoToday: string;
}

export interface NewsItem {
  evidenceId: string;
  headline: string;
  publisher: string;
  url: string;
  publishedUtc: string;
  retrievedUtc: string;
  summary: string;
  isMock: boolean;
}

export interface AiAnalysis {
  bullish: string[];
  bearish: string[];
  risks: string[];
  evidenceIds: string[];
}

export interface StockAudit {
  symbol: string;
  companyName: string;
  kind: 'HOLDING' | 'WATCHLIST';
  sector: string;
  shares: number | null;
  averageCost: number | null;
  lastClose: number | null;
  priceDate: string | null;
  unrealizedPct: number | null;
  rsi14: number | null;
  rsi9: number | null;
  change5dPct: number | null;
  newsStatus: string;
  news: NewsItem[];
  action: string;
  priority: PreMarketPriority;
  rationale: string;
  warnings: string[];
  eventIds: string[] | null;
  ai: AiAnalysis | null;
  weightPct: number | null;
  currency: string | null;
  change: string | null;
  earningsInDays: number | null;
}

export interface EvaluationRow {
  symbol: string;
  action: string;
  priority: PreMarketPriority;
  referenceClose: number | null;
  ret1: number | null;
  ret5: number | null;
  ret20: number | null;
  excess1: number | null;
  excess5: number | null;
  excess20: number | null;
}

export interface EvaluationResult {
  runId: number;
  asOfDate: string;
  benchmark: string;
  rows: EvaluationRow[];
  note: string;
}

export interface GlobalEvent {
  eventId: string;
  category: string;
  title: string;
  mechanism: string;
  counterargument: string;
  affectedSectors: string[];
  affectedSymbols: string[];
  evidence: NewsItem[];
  aiSummary: string | null;
  aiPortfolioNote: string | null;
}

export interface PreMarketReport {
  asOfDate: string;
  generatedUtc: string;
  isPartial: boolean;
  isTestRun: boolean;
  shadowMode: boolean;
  aiStatus: string;
  holdings: StockAudit[];
  watchlist: StockAudit[];
  dataQuality: string[];
  events: GlobalEvent[] | null;
  metrics: {
    articlesRetrieved: number;
    events: number;
    aiCalls: number;
    aiDurationMs: number;
    newsDurationMs: number;
    aiModel: string | null;
  } | null;
}

export interface PreMarketRunSummary {
  id: number;
  runKey: string;
  asOfDate: string;
  isTestRun: boolean;
  status: string;
  emailStatus: string;
  durationMs: number;
  startedAtUtc: string;
  aiCalls: number;
  aiModel: string | null;
  articlesRetrieved: number;
}

export interface PreMarketRunDetail extends PreMarketRunSummary {
  emailMessage: string | null;
  error: string | null;
  report: PreMarketReport | null;
}

export interface NewsSource {
  id: number;
  name: string;
  url: string;
  providerType: string;
  category: NewsSourceCategory;
  priority: number;
  enabled: boolean;
  approvalStatus: 'APPROVED' | 'PENDING';
  markets: string;
  refreshMinutes: number;
  timeoutSeconds: number;
  maxItems: number;
  lastSuccessUtc: string | null;
  lastFailureUtc: string | null;
  lastError: string | null;
  avgDurationMs: number;
  usefulArticleCount: number;
}

export type NewsSourceRequest = Pick<
  NewsSource,
  'name' | 'url' | 'category' | 'priority' | 'enabled' | 'markets' | 'refreshMinutes' | 'timeoutSeconds' | 'maxItems'
>;

export interface RunPreMarketRequest {
  asOfDate: string | null;
  dryRun: boolean;
  useMockNews: boolean;
  sendEmail: boolean;
}
