// PM-029 Trading Opportunities — pure view logic (no Angular, no state). Thresholds always come from the published
// backend config; nothing here hard-codes a score boundary or a prototype value.
import {
  OpportunityConfig,
  OpportunityRow,
  OpportunityScoreSummary,
  OpportunitySectorHeat,
  ScorePerspective,
} from '../../core/models/opportunities.models';
import {
  OpportunitySort,
  OpportunityView,
  ScoreFilter,
} from '../../core/store/opportunities.store';
import { ExportCell, TxExportColumn, TxExportKind } from '../transactions/transactions-logic';

export interface ManualRValues {
  entry: number | null;
  stop: number | null;
  target: number | null;
}

export const EMPTY_MANUAL_R: ManualRValues = { entry: null, stop: null, target: null };

export function manualRScenarioKey(symbol: string, perspective: ScorePerspective): string {
  return `${perspective}:${symbol}`;
}

export function manualRewardRisk(values: ManualRValues | null | undefined): number | null {
  if (!values) return null;
  const { entry, stop, target } = values;
  if (
    entry === null ||
    stop === null ||
    target === null ||
    !Number.isFinite(entry) ||
    !Number.isFinite(stop) ||
    !Number.isFinite(target) ||
    entry <= 0 ||
    stop <= 0 ||
    target <= entry ||
    entry <= stop
  ) {
    return null;
  }
  const ratio = (target - entry) / (entry - stop);
  return Number.isFinite(ratio) ? ratio : null;
}

// ── Scores ──────────────────────────────────────────────────────────────────

export function activeScore(
  row: OpportunityRow,
  perspective: ScorePerspective,
): OpportunityScoreSummary {
  return perspective === 'Swing' ? row.swing : row.strategic;
}

/** Unscorable = no headline score (Insufficient / Unavailable). Distinct from Weak, which is a real low score. */
export function isUnscorable(s: OpportunityScoreSummary): boolean {
  return s.score === null;
}

export function isWeak(s: OpportunityScoreSummary): boolean {
  return s.score !== null && s.band === 'Weak';
}

export function heatClass(s: OpportunityScoreSummary): string {
  const band = s.score === null ? 'none' : s.band.toLowerCase();
  return `heat-fill heat--${band}${s.status === 'Provisional' && s.score !== null ? ' heat--provisional' : ''}`;
}

export function scoreText(s: OpportunityScoreSummary): string {
  if (s.status === 'NotEvaluated') return 'Not evaluated';
  return s.score === null ? 'N/A' : String(s.score);
}

export function scoreAriaLabel(perspective: ScorePerspective, s: OpportunityScoreSummary): string {
  if (s.status === 'NotEvaluated') return `${perspective}: Not evaluated — ETF scoring deferred`;
  if (s.score === null) {
    const reason = s.reasonCodes.length ? `: ${reasonLabel(s.reasonCodes[0])}` : '';
    return `${perspective} score not available (${s.status}${reason})`;
  }
  const provisional =
    s.status === 'Provisional' ? `, provisional, ${s.completenessPct}% complete` : '';
  return `${perspective} score ${s.score} of 100, ${s.band}${provisional}`;
}

export function minScoreFor(filter: ScoreFilter, config: OpportunityConfig | null): number | null {
  if (!config) return null;
  if (filter === 'attractive') return config.attractiveMin;
  if (filter === 'moderate') return config.moderateMin;
  return null;
}

// ── Unscorable reasons (C2 exclusion summary) ───────────────────────────────

export type ReasonCategory = 'identity' | 'stale' | 'setup' | 'missing' | 'other';

export const REASON_CATEGORY_LABELS: Record<ReasonCategory, string> = {
  identity: 'identity unresolved',
  stale: 'stale evidence',
  setup: 'no valid setup',
  missing: 'missing essential evidence',
  other: 'other',
};

const SETUP_CODES = new Set([
  'NoValidStop',
  'DynamicStopMissing',
  'StopAtOrAboveEntry',
  'StopOutsideVolatilityBand',
  'NoStructuralTarget',
  'NoEntryPrice',
]);
const STALE_CODES = new Set(['StaleScan', 'FundamentalsStale']);
const REASON_PRIORITY = [
  'IdentityUnresolved',
  'StaleScan',
  'FundamentalsStale',
  'DynamicStopMissing',
  'StopAtOrAboveEntry',
  'StopOutsideVolatilityBand',
  'NoValidStop',
  'NoStructuralTarget',
  'NoEntryPrice',
  'NoScan',
  'LowCompleteness',
];
const REASON_CATEGORIES: readonly ReasonCategory[] = [
  'identity',
  'stale',
  'setup',
  'missing',
  'other',
];

const REASON_LABELS: Record<string, string> = {
  EtfScoringDeferred: 'Not evaluated — ETF scoring deferred (PM-036)',
  SetupPolicyApprovalRequired: 'Advisory classification policy awaits owner approval',
  ConfirmedSupportUnavailable: 'Confirmed nearby support is unavailable',
  ConfirmedResistanceUnavailable: 'Confirmed nearby resistance is unavailable',
  StructuralHistoryUnavailable: 'Complete session-aligned OHLC history is unavailable',
  StructuralSessionGap: 'Structural history has missing exchange sessions',
  SetupTimestampUnverified: 'Setup evidence timestamp is not verified',
  SetupCurrencyUnverified: 'Structural price-basis currency is unverified',
  OriginalOhlcUnverified:
    'Original OHLC observations are not verified; reconstructed values are excluded',
  StructuralCurrencyMismatch: 'Chart and setup price currencies do not match',
  MarketSetupUnavailable: 'A market-derived entry, stop and target are required',
  NoValidStop: 'No valid stop',
  DynamicStopMissing: 'Scanner stop missing',
  StopAtOrAboveEntry: 'Scanner stop at or above entry',
  StopOutsideVolatilityBand: 'Stop outside volatility band',
  NoStructuralTarget: 'No structural target',
  NoEntryPrice: 'No entry price',
  StaleScan: 'Stale technical scan',
  ExpectedSessionMismatch: 'Expected exchange session is no longer current',
  InvalidAnalysisPrice: 'Analysis price is not positive',
  InvalidScanTimestamp: 'Scan timestamp is missing',
  ScanTimestampInFuture: 'Scan timestamp is after the evaluation time',
  ScanBeforeSessionClose: 'Scan timestamp predates the exchange session close',
  InvalidRsi: 'RSI is outside the 0–100 range',
  InvalidRsiSignal: 'RSI signal is outside the 0–100 range',
  InvalidMovingAverage: 'Moving average is negative',
  InvalidSma200: 'SMA200 is invalid',
  InvalidEma20: 'EMA20 is invalid',
  InvalidVolume: 'Volume is negative',
  InvalidVolumeRatio: 'Volume ratio is negative',
  InvalidChangePercent: 'Daily change is outside the valid price range',
  InvalidAtr: 'ATR is negative',
  MovingAveragesUnavailable: 'Required moving averages are unavailable',
  Sma200Unavailable: 'SMA200 data is unavailable',
  Ema20Unavailable: 'EMA20 data is unavailable',
  RsiSignalUnavailable: 'RSI signal is unavailable',
  VolumeUnavailable: 'Volume data is unavailable',
  AtrUnavailable: 'ATR data is unavailable',
  FibUnavailable: 'Fibonacci zone is unavailable',
  NoScan: 'No technical scan',
  LowCompleteness: 'Low evidence completeness',
  IdentityUnresolved: 'Identity unresolved',
  EvaluationFailed: 'Evaluation failed',
  FundamentalsStale: 'Fundamentals stale',
  FundamentalsMissing: 'Fundamentals missing',
  FundamentalsLegacy: 'Fundamentals lack availability data',
  FundamentalsInsufficientFactors: 'Too few fundamental factors',
  PiotroskiInsufficientSignals: 'Too few proxy-based fundamental health indicators',
};

const SECTOR_HEAT_REASON_LABELS: Record<string, string> = {
  Unmapped: 'Sector benchmark is not mapped',
  Stale: 'Sector benchmark data is stale',
  InsufficientObservations: 'Insufficient sector-relative observations',
  CurrencyMismatch: 'Sector benchmark currency does not match the security',
  PossibleCorporateAction: 'Possible corporate action affects the sector comparison',
  Unclassified: 'Sector-relative evidence could not be classified',
};

export function reasonLabel(code: string): string {
  if (code.startsWith('MissingEssential:'))
    return `Missing essential component ${code.substring('MissingEssential:'.length)}`;
  if (/^SectorHeat(?:20D|60D)?:/.test(code)) {
    const status = code.substring(code.indexOf(':') + 1);
    return SECTOR_HEAT_REASON_LABELS[status] ?? `Sector-relative evidence unavailable (${status})`;
  }
  return REASON_LABELS[code] ?? code;
}

const FIT_REASON_LABELS: Record<string, string> = {
  AllocationUnverifiable: 'Allocation cannot be verified',
  ExposureUnavailable: 'Exposure unavailable',
  PositionAtLimit: 'Position at its limit',
  PositionNearLimit: 'Position near its limit',
  NoPositionLimit: 'No position limit configured',
  RolePolicyAmbiguous: 'Role policy ambiguous',
  SectorOverTarget: 'Sector over target',
  SectorAboveTarget: 'Sector above target',
  SectorUnknown: 'Sector unknown',
  SectorNoTarget: 'No sector target',
  SectorExposureUnavailable: 'Sector exposure unavailable',
};

export function fitReasonLabel(code: string): string {
  return FIT_REASON_LABELS[code] ?? code;
}

/** One category per unscorable row so the summary counts add up: identity > stale > setup > missing > other. */
export function reasonCategory(codes: readonly string[]): ReasonCategory {
  if (codes.includes('IdentityUnresolved')) return 'identity';
  if (codes.some((c) => STALE_CODES.has(c))) return 'stale';
  if (codes.some((c) => SETUP_CODES.has(c))) return 'setup';
  if (codes.length > 0) return 'missing';
  return 'other';
}

/** Chooses one most-specific primary exclusion reason so the summary counts remain mutually exclusive. */
export function primaryReasonCode(codes: readonly string[]): string {
  for (const reason of REASON_PRIORITY) {
    if (codes.includes(reason)) return reason;
  }
  return codes[0] ?? 'other';
}

function matchesReasonFilter(codes: readonly string[], filter: string): boolean {
  return REASON_CATEGORIES.includes(filter as ReasonCategory)
    ? reasonCategory(codes) === filter
    : primaryReasonCode(codes) === filter;
}

// ── Filtering ───────────────────────────────────────────────────────────────

export interface OpportunityFilterInput {
  view: OpportunityView;
  search: string;
  reasonFilter: string | null;
  config: OpportunityConfig | null;
}

/** Labels used by the Final Action filter: "Multiple" for conflicting account/lot decisions, else the single label. */
export function finalActionFilterLabel(row: OpportunityRow): string {
  if (row.finalAction.kind === 'Multiple') return 'Multiple';
  return row.finalAction.label ?? 'Not evaluated';
}

/** Non-score filters (search, sector, fit, final action, ownership, source, heat). */
export function passesAttributeFilters(
  row: OpportunityRow,
  view: OpportunityView,
  search: string,
): boolean {
  const q = search.trim().toLowerCase();
  if (q) {
    const hay = [
      row.symbol,
      row.companyName,
      row.sector ?? '',
      row.industry ?? '',
      row.analysisTicker ?? '',
    ]
      .join(' ')
      .toLowerCase();
    if (!hay.includes(q)) return false;
  }
  if (view.sectors.length && !view.sectors.includes(row.sector ?? 'Unclassified')) return false;
  if (view.industries.length && !view.industries.includes(row.industry ?? 'Unclassified'))
    return false;
  if (view.riskCategories.length && !view.riskCategories.includes(row.risk.category)) return false;
  if (view.accounts.length && !row.accounts.some((account) => view.accounts.includes(account)))
    return false;
  if (
    view.roles.length &&
    ![...row.roles, ...(row.watchlistRole ? [row.watchlistRole] : [])].some((role) =>
      view.roles.includes(role),
    )
  ) {
    return false;
  }
  if (view.freshness === 'current' && row.freshness.isStale) return false;
  if (view.freshness === 'stale' && !row.freshness.isStale) return false;
  if (view.fit.length && !view.fit.includes(row.fit.status)) return false;
  if (
    view.finalActionLabels.length &&
    !view.finalActionLabels.includes(finalActionFilterLabel(row))
  )
    return false;
  if (view.ownership === 'owned' && !row.owned) return false;
  if (view.ownership === 'notOwned' && row.owned) return false;
  if (view.sources.length && !row.sources.some((s) => view.sources.includes(s))) return false;
  if (view.heat.length && !view.heat.includes(row.sectorHeat.direction)) return false;
  if (
    view.perspective === 'Swing' &&
    view.setupQuality.length &&
    !view.setupQuality.includes(row.setupQuality?.status ?? 'Unavailable')
  )
    return false;
  return true;
}

export type ScoreGate = 'shown' | 'belowThreshold' | 'notVerified' | 'unscorable';

/** How the score filter treats a row in the active perspective. */
export function scoreGate(row: OpportunityRow, input: OpportunityFilterInput): ScoreGate {
  const s = activeScore(row, input.view.perspective);
  if (isUnscorable(s)) {
    return input.view.scoreFilter === 'all' || input.view.showUnavailable ? 'shown' : 'unscorable';
  }
  if (input.view.verifiedOnly && s.status !== 'Verified') return 'notVerified';
  const min = minScoreFor(input.view.scoreFilter, input.config);
  if (min !== null && (s.score ?? 0) < min) return 'belowThreshold';
  return 'shown';
}

export function filterRows(
  rows: readonly OpportunityRow[],
  input: OpportunityFilterInput,
): OpportunityRow[] {
  return rows.filter((row) => {
    if (!passesAttributeFilters(row, input.view, input.search)) return false;
    if (input.reasonFilter) {
      const s = activeScore(row, input.view.perspective);
      return isUnscorable(s) && matchesReasonFilter(s.reasonCodes, input.reasonFilter);
    }
    return scoreGate(row, input) === 'shown';
  });
}

export interface ExclusionSummary {
  total: number;
  shown: number;
  filteredOut: number;
  belowThreshold: number;
  notVerified: number;
  unscorable: number;
  reasons: { category: ReasonCategory; code: string; label: string; count: number }[];
}

/**
 * "Showing n of total · a below threshold · b unscorable: …" — computed from the snapshot, never hard-coded. Rows removed by
 * attribute filters are counted separately so the parts always add up to the total.
 */
export function exclusionSummary(
  rows: readonly OpportunityRow[],
  input: OpportunityFilterInput,
): ExclusionSummary {
  const summary: ExclusionSummary = {
    total: rows.length,
    shown: 0,
    filteredOut: 0,
    belowThreshold: 0,
    notVerified: 0,
    unscorable: 0,
    reasons: [],
  };
  const byReason = new Map<string, number>();
  for (const row of rows) {
    if (!passesAttributeFilters(row, input.view, input.search)) {
      summary.filteredOut++;
      continue;
    }
    const s = activeScore(row, input.view.perspective);
    if (isUnscorable(s)) {
      const code = primaryReasonCode(s.reasonCodes);
      byReason.set(code, (byReason.get(code) ?? 0) + 1);
    }
    const gate = input.reasonFilter
      ? isUnscorable(s) && matchesReasonFilter(s.reasonCodes, input.reasonFilter)
        ? 'shown'
        : 'filtered'
      : scoreGate(row, input);
    if (gate === 'shown') summary.shown++;
    else if (gate === 'belowThreshold') summary.belowThreshold++;
    else if (gate === 'notVerified') summary.notVerified++;
    else if (gate === 'unscorable') summary.unscorable++;
    else summary.filteredOut++;
  }
  const order: ReasonCategory[] = ['setup', 'stale', 'missing', 'identity', 'other'];
  summary.reasons = [...byReason.entries()]
    .map(([code, count]) => {
      const category = code === 'other' ? 'other' : reasonCategory([code]);
      return { category, code, label: reasonLabel(code), count };
    })
    .sort(
      (a, b) =>
        order.indexOf(a.category) - order.indexOf(b.category) ||
        b.count - a.count ||
        a.label.localeCompare(b.label),
    );
  return summary;
}

// ── Sector Heat ─────────────────────────────────────────────────────────────

const MINUS = '\u2212';

export function signedPp(value: number): string {
  const abs = Math.abs(value).toFixed(1);
  if (value > 0) return `+${abs}`;
  if (value < 0) return `${MINUS}${abs}`;
  return abs;
}

/** `mask` is the demo-mode percent mask; direction and colour always come from the server value. */
export function sectorHeatText(
  h: OpportunitySectorHeat,
  mask: (v: number) => number = (v) => v,
): string {
  if (h.diffPp === null || h.status !== 'Matched') return 'Unavailable';
  const pp = signedPp(mask(h.diffPp));
  if (h.direction === 'Positive') return `\u25B2 ${pp} pp`;
  if (h.direction === 'Negative') return `\u25BC ${pp} pp`;
  return `\u2022 ${pp} pp`;
}

export function sectorHeatClass(h: OpportunitySectorHeat): string {
  if (h.status !== 'Matched' || h.diffPp === null || h.benchmarkReturnPct === null) {
    return 'sector-heat-badge sh--unavailable';
  }
  const momentum =
    h.benchmarkReturnPct > 0 ? 'strong' : h.benchmarkReturnPct < 0 ? 'weak' : 'neutral';
  return `sector-heat-badge sh--benchmark-${momentum}`;
}

const HEAT_STATUS_LABELS: Record<string, string> = {
  Unmapped: 'No sector benchmark mapped',
  Stale: 'Price history is stale',
  InsufficientObservations: 'Not enough aligned trading sessions',
  CurrencyMismatch: 'Stock and benchmark currencies differ',
  PossibleCorporateAction: 'Possible unadjusted corporate action',
  Unclassified: 'Sector unknown',
};

export function sectorHeatAriaLabel(h: OpportunitySectorHeat, sector: string | null): string {
  if (h.diffPp === null || h.status !== 'Matched' || h.benchmarkReturnPct === null) {
    return `Sector heat unavailable: ${HEAT_STATUS_LABELS[h.status] ?? h.status}`;
  }
  const vs = sector ? ` versus ${sector} benchmark` : '';
  const word =
    h.direction === 'Positive'
      ? 'outperforming'
      : h.direction === 'Negative'
        ? 'underperforming'
        : 'in line';
  const momentum =
    h.benchmarkReturnPct > 0 ? 'strong' : h.benchmarkReturnPct < 0 ? 'weak' : 'neutral';
  return `Sector heat ${signedPp(h.diffPp)} percentage points over 20 sessions${vs}, ${word}; benchmark momentum ${momentum} (${signedPp(h.benchmarkReturnPct)}%)`;
}

export function sectorHeatStatusLabel(status: string): string {
  return status === 'Matched' ? 'Matched' : (HEAT_STATUS_LABELS[status] ?? status);
}

// ── Sorting (nulls last in both directions) ─────────────────────────────────

const RISK_ORDER: Record<string, number> = { Low: 1, Medium: 2, High: 3 };
const FIT_ORDER: Record<string, number> = { Eligible: 1, Review: 2, NoAdd: 3 };

/** Sortable / exportable value of a grid column. `score` is the active-perspective score. */
export function columnValue(
  row: OpportunityRow,
  key: string,
  perspective: ScorePerspective,
  manualR?: ManualRValues | null,
): string | number | null {
  switch (key) {
    case 'symbol':
      return row.symbol;
    case 'score':
      return activeScore(row, perspective).score;
    case 'swing':
      return row.swing.score;
    case 'strategic':
      return row.strategic.score;
    case 'rr':
      return row.setup.rewardRisk;
    case 'setupQuality':
      return { Poor: 0, Caution: 1, Good: 2, Unavailable: null }[
        row.setupQuality?.status ?? 'Unavailable'
      ];
    case 'manualR':
      return manualRewardRisk(manualR);
    case 'risk':
      return RISK_ORDER[row.risk.category] ?? null;
    case 'fit':
      return FIT_ORDER[row.fit.status] ?? null;
    case 'finalAction':
      return row.finalAction.kind === 'None' ? null : finalActionFilterLabel(row);
    case 'sectorHeat':
    case 'rs20':
      return row.sectorHeat.status === 'Matched' ? row.sectorHeat.diffPp : null;
    case 'rs60':
      return row.sectorHeat.status === 'Matched' ? row.sectorHeat.diff60Pp : null;
    case 'sector':
      return row.sector;
    case 'industry':
      return row.industry;
    case 'price':
      return row.indicators.price;
    case 'rsi14':
      return row.indicators.rsi14;
    case 'macdHist':
      return row.indicators.macdHistogram;
    case 'ema20':
      return row.indicators.ema20;
    case 'sma50':
      return row.indicators.sma50;
    case 'sma200':
      return row.indicators.sma200;
    case 'volumeRatio':
      return row.indicators.volumeRatio;
    case 'stock20d':
      return row.sectorHeat.stockReturnPct;
    case 'bench20d':
      return row.sectorHeat.benchmarkReturnPct;
    case 'entry':
      return row.setup.entry;
    case 'stop':
      return row.setup.stop;
    case 'target':
      return row.setup.target;
    case 'riskPct':
      return row.setup.riskPct;
    case 'rewardPct':
      return row.setup.rewardPct;
    case 'setupKind':
      return row.setup.kind;
    case 'owned':
      return row.owned ? 'Yes' : 'No';
    case 'sources':
      return row.sources.join(', ');
    case 'weightPct':
      return row.fit.weightPct;
    case 'sectorWeightPct':
      return row.fit.sectorWeightPct;
    case 'positionHeadroom':
      return row.fit.positionHeadroomPct;
    case 'sectorHeadroom':
      return row.fit.sectorHeadroomPct;
    case 'accounts':
      return row.accounts.join(', ') || null;
    case 'roles':
      return row.roles.join(', ') || null;
    case 'swingCompleteness':
      return row.swing.completenessPct;
    case 'strategicCompleteness':
      return row.strategic.completenessPct;
    case 'dataAsOf':
      return row.freshness.dataAsOfDate;
    case 'status':
      return row.rowStatus;
    default:
      return null;
  }
}

/** The sort key shown by the table header: the `score` pseudo-key maps to the active perspective's column. */
export function tableSortKey(sort: OpportunitySort, perspective: ScorePerspective): string {
  return sort.active === 'score' ? perspective.toLowerCase() : sort.active;
}

/** Do not repeat the ticker as a company subtitle when no distinct verified description is available. */
export function companyNameForDisplay(
  symbol: string,
  companyName: string | null | undefined,
): string | null {
  const name = companyName?.trim();
  return name && name.toLocaleUpperCase() !== symbol.trim().toLocaleUpperCase() ? name : null;
}

export function sortRows(
  rows: readonly OpportunityRow[],
  sort: OpportunitySort,
  perspective: ScorePerspective,
  manualRByScenario: Readonly<Record<string, ManualRValues>> = {},
): OpportunityRow[] {
  const copy = [...rows];
  if (!sort.direction) return copy;
  const dir = sort.direction === 'asc' ? 1 : -1;
  return copy.sort((a, b) => {
    const va = columnValue(
      a,
      sort.active,
      perspective,
      manualRByScenario[manualRScenarioKey(a.symbol, perspective)],
    );
    const vb = columnValue(
      b,
      sort.active,
      perspective,
      manualRByScenario[manualRScenarioKey(b.symbol, perspective)],
    );
    if (va === null && vb === null) return a.symbol.localeCompare(b.symbol);
    if (va === null) return 1;
    if (vb === null) return -1;
    const cmp =
      typeof va === 'number' && typeof vb === 'number'
        ? va - vb
        : String(va).localeCompare(String(vb));
    return cmp !== 0 ? cmp * dir : a.symbol.localeCompare(b.symbol);
  });
}

// ── Export (complete filtered + sorted set, numeric cells, currency last) ───

interface OpportunityExportColumn extends TxExportColumn {
  /** Grid column this export column belongs to (for "visible columns only"); always included when absent. */
  gridKey?: string;
  accountDetail?: boolean;
  value: (
    row: OpportunityRow,
    manualRByScenario: Readonly<Record<string, ManualRValues>>,
    perspective: ScorePerspective,
  ) => ExportCell;
}

const xcol = (
  header: string,
  kind: TxExportKind,
  value: OpportunityExportColumn['value'],
  gridKey?: string,
  accountDetail = false,
): OpportunityExportColumn => ({ header, kind, value, gridKey, accountDetail });

const list = (v: readonly string[]): string | null => (v.length ? v.join('; ') : null);

const EXPORT_COLUMNS: readonly OpportunityExportColumn[] = [
  xcol('Symbol', 'text', (r) => r.symbol),
  xcol('Company', 'text', (r) => companyNameForDisplay(r.symbol, r.companyName)),
  xcol('Analysis Ticker', 'text', (r) => r.analysisTicker, 'symbol'),
  xcol('Exchange', 'text', (r) => r.exchange, 'symbol'),
  xcol('Swing Score', 'int', (r) => r.swing.score, 'swing'),
  xcol('Swing Status', 'text', (r) => r.swing.status, 'swing'),
  xcol(
    'Swing Band',
    'text',
    (r) => (r.swing.score === null ? 'Unscorable' : r.swing.band),
    'swing',
  ),
  xcol('Swing Completeness %', 'qty', (r) => r.swing.completenessPct, 'swingCompleteness'),
  xcol('Swing Reasons', 'text', (r) => list(r.swing.reasonCodes), 'swing'),
  xcol('Strategic Score', 'int', (r) => r.strategic.score, 'strategic'),
  xcol('Strategic Status', 'text', (r) => r.strategic.status, 'strategic'),
  xcol(
    'Strategic Band',
    'text',
    (r) => (r.strategic.score === null ? 'Unscorable' : r.strategic.band),
    'strategic',
  ),
  xcol(
    'Strategic Completeness %',
    'qty',
    (r) => r.strategic.completenessPct,
    'strategicCompleteness',
  ),
  xcol('Strategic Reasons', 'text', (r) => list(r.strategic.reasonCodes), 'strategic'),
  xcol('Market R', 'qty', (r) => r.setup.rewardRisk, 'rr'),
  xcol('SETUP QUALITY', 'text', (r) => r.setupQuality?.status ?? 'Unavailable', 'setupQuality'),
  xcol(
    'Conservative R',
    'qty',
    (r) => r.setupQuality?.conservativeRewardRisk ?? null,
    'setupQuality',
  ),
  xcol(
    'Setup Quality Reasons',
    'text',
    (r) =>
      r.setupQuality
        ? list(r.setupQuality.reasonCodes)
        : 'Structural snapshot evidence unavailable',
    'setupQuality',
  ),
  xcol(
    'Setup Policy Version',
    'text',
    (r) => r.setupQuality?.policyVersion ?? null,
    'setupQuality',
  ),
  xcol(
    'Setup Policy Approved',
    'text',
    (r) => (r.setupQuality?.policyApproved === true ? 'Yes' : 'No'),
    'setupQuality',
  ),
  xcol('Instrument Type', 'text', (r) => r.instrumentType ?? 'Unconfirmed', 'symbol'),
  xcol(
    'Manual R',
    'qty',
    (r, scenarios, perspective) =>
      manualRewardRisk(scenarios[manualRScenarioKey(r.symbol, perspective)]),
    'manualR',
  ),
  xcol('Setup', 'text', (r) => r.setup.kind, 'setupKind'),
  xcol('Setup Reasons', 'text', (r) => list(r.setup.reasonCodes), 'setupKind'),
  xcol('Entry', 'money', (r) => r.setup.entry, 'entry'),
  xcol('Stop', 'money', (r) => r.setup.stop, 'stop'),
  xcol('Target', 'money', (r) => r.setup.target, 'target'),
  xcol('Risk %', 'qty', (r) => r.setup.riskPct, 'riskPct'),
  xcol('Reward %', 'qty', (r) => r.setup.rewardPct, 'rewardPct'),
  xcol('Risk', 'text', (r) => r.risk.category, 'risk'),
  xcol('Portfolio Fit', 'text', (r) => r.fit.status, 'fit'),
  xcol('Fit Reasons', 'text', (r) => list(r.fit.reasonCodes), 'fit'),
  xcol(
    'Final Action',
    'text',
    (r) => (r.finalAction.kind === 'None' ? null : finalActionFilterLabel(r)),
    'finalAction',
  ),
  xcol('Final Action Decisions', 'text', (r) => list(r.finalAction.distinctLabels), 'finalAction'),
  xcol(
    'Sector Heat 20D (pp)',
    'qty',
    (r) => columnValue(r, 'rs20', 'Swing') as number | null,
    'sectorHeat',
  ),
  xcol(
    'Sector Heat 60D (pp)',
    'qty',
    (r) => columnValue(r, 'rs60', 'Swing') as number | null,
    'rs60',
  ),
  xcol('Sector Heat Status', 'text', (r) => r.sectorHeat.status, 'sectorHeat'),
  xcol('Benchmark', 'text', (r) => r.sectorHeat.benchmarkSymbol, 'sectorHeat'),
  xcol('Stock 20D %', 'qty', (r) => r.sectorHeat.stockReturnPct, 'stock20d'),
  xcol('Benchmark 20D %', 'qty', (r) => r.sectorHeat.benchmarkReturnPct, 'bench20d'),
  xcol('Sector', 'text', (r) => r.sector, 'sector'),
  xcol('Industry', 'text', (r) => r.industry, 'industry'),
  xcol('Price', 'money', (r) => r.indicators.price, 'price'),
  xcol('RSI (14)', 'qty', (r) => r.indicators.rsi14, 'rsi14'),
  xcol('MACD Hist.', 'qty', (r) => r.indicators.macdHistogram, 'macdHist'),
  xcol('EMA 20', 'money', (r) => r.indicators.ema20, 'ema20'),
  xcol('SMA 50', 'money', (r) => r.indicators.sma50, 'sma50'),
  xcol('SMA 200', 'money', (r) => r.indicators.sma200, 'sma200'),
  xcol('Volume Ratio', 'qty', (r) => r.indicators.volumeRatio, 'volumeRatio'),
  xcol('Owned', 'text', (r) => (r.owned ? 'Yes' : 'No'), 'owned'),
  xcol('Sources', 'text', (r) => list(r.sources), 'sources'),
  xcol('Portfolio Weight %', 'qty', (r) => r.fit.weightPct, 'weightPct'),
  xcol('Sector Weight %', 'qty', (r) => r.fit.sectorWeightPct, 'sectorWeightPct'),
  xcol('Position Headroom %', 'qty', (r) => r.fit.positionHeadroomPct, 'positionHeadroom'),
  xcol('Sector Headroom %', 'qty', (r) => r.fit.sectorHeadroomPct, 'sectorHeadroom'),
  xcol('Accounts', 'text', (r) => list(r.accounts), 'accounts', true),
  xcol('Roles', 'text', (r) => list(r.roles), 'roles'),
  xcol('Data As Of', 'date', (r) => r.freshness.dataAsOfDate, 'dataAsOf'),
  xcol('Data Status', 'text', (r) => r.rowStatus, 'status'),
  xcol('Currency', 'text', (r) => r.currency ?? r.setup.currency),
];

export interface OpportunityExportOptions {
  visibleOnly: boolean;
  visibleKeys: readonly string[];
  includeAccounts: boolean;
}

export function opportunityExportColumns(
  options: OpportunityExportOptions,
): OpportunityExportColumn[] {
  const visible = new Set(options.visibleKeys);
  return EXPORT_COLUMNS.filter((c) => {
    if (c.accountDetail && !options.includeAccounts) return false;
    if (!options.visibleOnly || !c.gridKey) return true;
    return visible.has(c.gridKey);
  });
}

export function opportunityExportRows(
  rows: readonly OpportunityRow[],
  columns: readonly OpportunityExportColumn[],
  manualRByScenario: Readonly<Record<string, ManualRValues>> = {},
  perspective: ScorePerspective = 'Swing',
): ExportCell[][] {
  return rows.map((r) => columns.map((c) => c.value(r, manualRByScenario, perspective)));
}

/** Provenance sheet: snapshot id, scoring version, timestamps and the score disclaimer travel with every export. */
export function exportNotes(
  snapshot: {
    snapshotId: number | null;
    scoringVersion: string;
    computedAtUtc: string | null;
    dataAsOfSession: string | null;
    disclaimer: string;
  },
  view: OpportunityView,
  summary: ExclusionSummary,
): string[][] {
  return [
    ['Snapshot Id', String(snapshot.snapshotId ?? '')],
    ['Scoring Version', snapshot.scoringVersion],
    ['Computed At (UTC)', snapshot.computedAtUtc ?? ''],
    ['Data As Of Session', snapshot.dataAsOfSession ?? ''],
    ['Perspective', view.perspective],
    ['Score Filter', view.scoreFilter],
    ['Rows Exported', String(summary.shown)],
    ['Universe Rows', String(summary.total)],
    ['Disclaimer', snapshot.disclaimer],
  ];
}
