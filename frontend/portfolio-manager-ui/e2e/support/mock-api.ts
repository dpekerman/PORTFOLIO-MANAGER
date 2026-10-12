import type { Page, Route } from '@playwright/test';
import {
  accountTypes,
  cashItems,
  optionItems,
  portfolioItems,
  portfolioSummaries,
  sectorIndustryLists,
  unlinkedTrades,
} from '../fixtures/portfolio';
import {
  eodMeta,
  eodPage,
  marketIndices,
  scannerResponse,
  valueScreenerLatest,
  watchlistSummaries,
  yesterdayEod,
} from '../fixtures/market';
import {
  actionScores,
  adminUser,
  allUsers,
  allocationRiskConfig,
  automationRun,
  automationSettings,
  automationTaskStatus,
  automationTimezone,
  dashboardResponse,
  decisionPerformance,
  eodSummary,
  marketLeadership,
  performanceSummary,
  portfolioActions,
  portfolioBeta,
  valueHistory,
} from '../fixtures/dashboard';
import { isoDate, isoDateTime, makeStockQuote } from '../fixtures/factories';

/** Request as seen by a handler. `path` excludes the query string. */
export interface MockRequest {
  method: string;
  path: string;
  query: URLSearchParams;
  body: unknown;
}

/** Return `NO_CONTENT` for a 204, `{ status, body }` for a custom status, anything else is a 200 JSON body. */
export const NO_CONTENT = Symbol('no-content');
type HandlerResult = unknown | typeof NO_CONTENT | { status: number; body?: unknown };
type Handler = (req: MockRequest) => HandlerResult;

interface RouteDef {
  method: string;
  pattern: RegExp;
  handler: Handler;
}

const defs: RouteDef[] = [];
const on = (method: string, pattern: RegExp, handler: Handler) =>
  defs.push({ method, pattern, handler });
const get = (pattern: RegExp, handler: Handler | object | null) =>
  on('GET', pattern, typeof handler === 'function' ? (handler as Handler) : () => handler);

// ── Auth ─────────────────────────────────────────────────────────────────────
// Startup asks "is setup required?" then silently refreshes: answering both makes authGuard pass.
get(/^\/api\/auth\/setup-required$/, { required: false });
on('POST', /^\/api\/auth\/refresh$/, () => ({ accessToken: 'e2e-access-token', user: adminUser }));
get(/^\/api\/auth\/me$/, adminUser);
on('POST', /^\/api\/auth\/login$/, () => ({ accessToken: 'e2e-access-token', user: adminUser }));
on('POST', /^\/api\/auth\/logout$/, () => NO_CONTENT);

// ── Users / preferences / notifications ──────────────────────────────────────
get(/^\/api\/users$/, allUsers);
get(/^\/api\/users\/preferences$/, {});
get(/^\/api\/notification\/recipients$/, { emails: ['alerts@e2e.test', 'ops@e2e.test'] });
get(/^\/api\/notification\/status$/, {
  trackedSignals: 7,
  recipientCount: 2,
  recipients: ['alerts@e2e.test', 'ops@e2e.test'],
});

// ── Automation ───────────────────────────────────────────────────────────────
get(/^\/api\/automation\/settings$/, automationSettings);
get(/^\/api\/automation\/last-run$/, automationRun);
get(/^\/api\/automation\/history$/, [
  automationRun,
  { ...automationRun, runId: 'run-0002', tradingDate: isoDate(-2), triggerType: 'ManualRunNow' },
  {
    ...automationRun,
    runId: 'run-0003',
    tradingDate: isoDate(-3),
    overallStatus: 'PartialSuccess',
    rsiStatus: 'Failed',
    errorStep: 'Rsi',
    errorMessage: 'Yahoo Finance rate limited the request.',
  },
]);
get(/^\/api\/automation\/status\/.+$/, automationRun);
get(/^\/api\/automation\/task-status$/, automationTaskStatus);
get(/^\/api\/automation\/timezone-diagnostics$/, automationTimezone);

// ── Dashboard & analytics ────────────────────────────────────────────────────
get(/^\/api\/dashboard$/, dashboardResponse);
on('POST', /^\/api\/dashboard\/refresh$/, () => dashboardResponse);
get(/^\/api\/dashboard\/portfolio-actions$/, portfolioActions);
get(/^\/api\/dashboard\/eod-summary$/, eodSummary);
get(/^\/api\/dashboard\/market-leadership$/, marketLeadership);
get(/^\/api\/analytics\/decision-performance$/, decisionPerformance);
get(/^\/api\/analytics\/performance-summary$/, performanceSummary);
get(/^\/api\/analytics\/action-scores$/, actionScores);

// ── Portfolio / quotes ───────────────────────────────────────────────────────
get(/^\/api\/portfolio$/, portfolioItems);
get(/^\/api\/stocks\/quotes(\/snapshot)?$/, portfolioSummaries);
get(/^\/api\/stocks\/quote\/([^/]+)$/, (r) => {
  const symbol = decodeURIComponent(r.path.split('/').pop() ?? 'AAPL');
  return makeStockQuote(symbol, symbol, 100, 0.5);
});
get(/^\/api\/stocks\/search$/, [
  {
    description: 'APPLE INC',
    displaySymbol: 'AAPL',
    symbol: 'AAPL',
    type: 'Common Stock',
    exchange: 'US',
  },
  {
    description: 'APPLE HOSPITALITY REIT',
    displaySymbol: 'APLE',
    symbol: 'APLE',
    type: 'Common Stock',
    exchange: 'US',
  },
]);
on('POST', /^\/api\/stocks\/batch-prices$/, () => []);
on('POST', /^\/api\/data\/refresh$/, () => ({
  portfolioSymbolCount: portfolioSummaries.length,
  watchlistSymbolCount: watchlistSummaries.length,
  dashboardRebuilt: true,
  refreshedAt: isoDateTime(0, 15),
  durationMs: 850,
  portfolioSummaries,
  watchlistSummaries,
  portfolioSymbols: portfolioSummaries.map((s) => s.item.symbol),
  watchlistSymbols: watchlistSummaries.map((s) => s.item.symbol),
}));
get(/^\/api\/account-types$/, accountTypes);
get(/^\/api\/sector-industry$/, sectorIndustryLists);
get(/^\/api\/sector-industry\/decision-sources$/, { items: sectorIndustryLists.decisionSources });
get(/^\/api\/security-analysis-mappings\/([^/]+)$/, (r) => ({
  tradingTicker: decodeURIComponent(r.path.split('/').pop() ?? ''),
  analysisTicker: decodeURIComponent(r.path.split('/').pop() ?? ''),
  analysisMarket: 'US',
  analysisCurrency: 'USD',
  usesUnderlyingSecurity: false,
  resolutionStatus: 'NotApplicable',
  mappingSource: null,
  dataError: null,
}));

// ── Watchlist ────────────────────────────────────────────────────────────────
get(/^\/api\/watchlist(\/snapshot)?$/, watchlistSummaries);
on('POST', /^\/api\/watchlist$/, (r) => {
  const b = (r.body ?? {}) as { symbol?: string; notes?: string; role?: string };
  return {
    id: 999,
    symbol: b.symbol ?? 'NEW',
    notes: b.notes ?? '',
    addedAt: isoDateTime(0),
    role: b.role ?? 'Strategic',
  };
});

// ── Options & cash ───────────────────────────────────────────────────────────
get(/^\/api\/options$/, optionItems);
get(/^\/api\/options\/technical\/([^/]+)$/, (r) => ({
  symbol: decodeURIComponent(r.path.split('/').pop() ?? ''),
  currentPrice: 228.1,
  previousClose: 226.2,
  yesterdayHigh: 229.4,
  yesterdayLow: 224.8,
  rsi14: 55.2,
  rsiSignal9: 52.8,
  rsiSignalAvailable: true,
  sma20: 224.4,
  sma50: 219.7,
  ema21: 225.1,
  atr14: 3.4,
  bollingerUpper: 233.5,
  bollingerLower: 215.2,
}));
get(/^\/api\/cash$/, cashItems);
get(/^\/api\/cash\/unlinked-trades$/, unlinkedTrades);
get(/^\/api\/cash\/ledger-start-date$/, { ledgerStartDate: isoDate(-400) });
// Nullable: "no linked cash row for this trade" is represented as a null body.
get(/^\/api\/cash\/link\/.+$/, () => null);

// ── Scanner ──────────────────────────────────────────────────────────────────
get(/^\/api\/scanner\/rsi(\/snapshot)?$/, scannerResponse);
on('POST', /^\/api\/scanner\/analyze$/, () => [
  ...scannerResponse.oversoldChain.slice(0, 2),
  ...scannerResponse.overboughtChain.slice(0, 1),
]);
get(/^\/api\/scanner\/eod-settings$/, {
  eodWindowStart: '15:30',
  eodWindowEnd: '16:00',
  eodWindowEnabled: true,
  eodOversoldRsiThreshold: 30,
  eodOverboughtRsiThreshold: 75,
});
get(/^\/api\/scanner\/eod-window-active$/, {
  isActive: false,
  eodWindowStart: '15:30',
  eodWindowEnd: '16:00',
  eodWindowEnabled: true,
  serverTimeUtc: isoDateTime(0, 15),
});
get(/^\/api\/scanner\/yesterday-eod$/, yesterdayEod);
get(/^\/api\/scanner\/adhoc-session$/, {
  symbols: [],
  results: null,
  oversoldThreshold: 30,
  overboughtThreshold: 75,
  logicMode: 'Enhanced',
  updatedAt: null,
});
get(/^\/api\/scanner\/market-indices$/, marketIndices);

// ── EOD signals ──────────────────────────────────────────────────────────────
get(/^\/api\/eod-signals$/, (r) =>
  eodPage(Number(r.query.get('page') ?? 1), Number(r.query.get('pageSize') ?? 25)),
);
get(/^\/api\/eod-signals\/meta$/, eodMeta);

// ── Value screener ───────────────────────────────────────────────────────────
get(/^\/api\/valuescreener\/latest$/, valueScreenerLatest);
on('POST', /^\/api\/valuescreener\/refresh$/, () => valueScreenerLatest);
on('POST', /^\/api\/valuescreener\/analyze$/, () => [
  ...valueScreenerLatest.portfolio,
  ...valueScreenerLatest.watchlist,
]);
get(/^\/api\/valuescreener\/schedule$/, { scheduledTimeEt: '17:00', enabled: true });

// ── Allocation, beta, history ────────────────────────────────────────────────
get(/^\/api\/allocation-risk$/, allocationRiskConfig);
get(/^\/api\/portfoliobeta$/, portfolioBeta);
on('POST', /^\/api\/portfoliobeta\/calculate$/, () => portfolioBeta);
get(/^\/api\/portfoliovaluehistory\/(latest|range)$/, valueHistory);
get(/^\/api\/portfoliovaluehistory\/missing-days$/, [isoDate(-9), isoDate(-8)]);
on('POST', /^\/api\/portfoliovaluehistory\/backfill$/, () => []);

// ── Fire-and-forget writes the pages issue on load ───────────────────────────
// Portfolio publishes its computed Final Actions on every recompute; preferences write through.
on('PATCH', /^\/api\/portfolio\/final-actions$/, () => NO_CONTENT);
on('PUT', /^\/api\/users\/preferences\/.+$/, () => NO_CONTENT);

/**
 * Fallbacks for endpoints without an explicit handler. Collections are the common case, so GETs
 * default to `[]`; the few object-shaped GETs that crash on an array are listed explicitly above.
 */
function fallback(method: string): HandlerResult {
  if (method === 'GET') return [];
  if (method === 'DELETE') return NO_CONTENT;
  return {};
}

export interface ApiMocks {
  /** `METHOD /path` of every `/api` request that hit the fallback (no explicit handler). */
  unmocked: string[];
  /** Every `/api` request, in order — useful when debugging a spec. */
  requests: string[];
}

/** Hosts the app loads fonts from; stubbed offline so CI never depends on Google. */
const FONT_HOSTS = /^https:\/\/fonts\.(googleapis|gstatic)\.com\//;

/**
 * Fulfils every same-origin `/api/**` call in the browser so no .NET API or SQL Server is needed.
 * Register once per page, before the first navigation.
 *
 * Web fonts (Roboto and the Material Icons ligature font) are fetched from the real CDN so icon
 * glyphs occupy their true width — overflow measurements are meaningless if icons render as
 * their ligature text. Set `E2E_STUB_FONTS=1` to run fully offline (icons then render as text).
 */
export async function installApiMocks(page: Page): Promise<ApiMocks> {
  const mocks: ApiMocks = { unmocked: [], requests: [] };

  await page.route(
    (url) => url.pathname.startsWith('/api/') && url.hostname === 'localhost',
    async (route) => {
      const request = route.request();
      const url = new URL(request.url());
      const method = request.method();
      const key = `${method} ${url.pathname}`;
      mocks.requests.push(key);

      let body: unknown = null;
      try {
        body = request.postDataJSON();
      } catch {
        body = request.postData();
      }

      const def = defs.find((d) => d.method === method && d.pattern.test(url.pathname));
      if (!def) mocks.unmocked.push(key);
      const result = def
        ? def.handler({ method, path: url.pathname, query: url.searchParams, body })
        : fallback(method);
      await respond(route, result);
    },
  );

  await page.route(FONT_HOSTS, async (route) => {
    const isCss = route.request().url().includes('googleapis.com/css');
    if (process.env['E2E_STUB_FONTS']) {
      return route.fulfill({
        status: 200,
        contentType: isCss ? 'text/css' : 'font/woff2',
        body: '',
      });
    }
    try {
      await route.fulfill({ response: await route.fetch({ timeout: 8_000 }) });
    } catch {
      // Offline, or the test already ended and tore the route down — either way nothing to serve.
      await route
        .fulfill({ status: 200, contentType: isCss ? 'text/css' : 'font/woff2', body: '' })
        .catch(() => undefined);
    }
  });

  return mocks;
}

async function respond(route: Route, result: HandlerResult): Promise<void> {
  if (result === NO_CONTENT) return route.fulfill({ status: 204 });
  if (
    result &&
    typeof result === 'object' &&
    'status' in result &&
    typeof result.status === 'number'
  ) {
    const { status, body } = result as { status: number; body?: unknown };
    return route.fulfill({
      status,
      contentType: 'application/json',
      body: JSON.stringify(body ?? {}),
    });
  }
  return route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(result ?? null),
  });
}
