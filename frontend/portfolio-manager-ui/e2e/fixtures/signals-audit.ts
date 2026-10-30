import { isoDate } from './factories';

const horizon = (pct: number | null, daysAgo: number, status = 'Complete') => ({
  pct,
  evalDate: pct === null ? null : isoDate(-daysAgo),
  evalPrice: pct === null ? null : Math.round(100 * (1 + pct / 100) * 100) / 100,
  status,
});

const pending = { pct: null, evalDate: null, evalPrice: null, status: 'Pending' };

type Row = Record<string, unknown> & { id: number; myAction: string; linkedTransactionId: number | null };

const baseRow = (over: Record<string, unknown>): Row =>
  ({
    role: 'Core',
    dateAnomaly: false,
    signalType: 'PRIORITY_CANDIDATE',
    signalTypeLabel: 'Priority Candidate',
    category: 'Entry',
    direction: 'Buy',
    source: 'Watchlist',
    currency: 'CAD',
    hasNote: false,
    linkedTransactionId: null,
    linkKind: 'None',
    origin: 'Live',
    episodeActive: true,
    ...over,
  }) as Row;

function freshRows(): Row[] {
  return [
    baseRow({
      id: 3,
      symbol: 'LMT.TO',
      signalDate: isoDate(-3),
      daysSince: 3,
      signalPrice: 28.71,
      score: 78,
      return5D: pending,
      return20D: pending,
      return1M: pending,
      today: horizon(2.4, 0),
      myAction: 'Pending',
    }),
    baseRow({
      id: 2,
      symbol: 'MSFT',
      role: 'Strategic',
      signalDate: isoDate(-40),
      daysSince: 40,
      signalPrice: 410.5,
      currency: 'USD',
      score: 82,
      return5D: horizon(3.1, 33),
      return20D: horizon(-1.8, 12),
      return1M: horizon(0.9, 8),
      today: horizon(5.2, 0),
      myAction: 'Actioned',
      hasNote: true,
      linkedTransactionId: 11,
      linkKind: 'TransactionVerified',
    }),
    baseRow({
      id: 1,
      symbol: 'FUT',
      role: 'Swing',
      signalDate: isoDate(4),
      daysSince: -4,
      dateAnomaly: true,
      signalPrice: 50,
      score: 77,
      return5D: pending,
      return20D: pending,
      return1M: pending,
      today: { pct: null, evalDate: null, evalPrice: null, status: 'NA' },
      myAction: 'Pending',
    }),
  ];
}

const DEFAULT_ENTRY_LABELS = [
  'STARTER ENTRY',
  'ADD/STAGGED ENTRY',
  'SMALL ADD',
  'TRIM/EXIT',
  'TRIM/REVIEW',
  'CUT LOSS/EXIT',
];

const freshSettings = () => ({
  priorityEnabled: true,
  priorityThreshold: 75,
  activatedAtUtc: null as string | null,
  baselineCount: 0,
  finalActionEnabled: true,
  finalActionBaselineCount: 0,
  entries: DEFAULT_ENTRY_LABELS.map((label) => ({
    id: label.toUpperCase(),
    label,
    enabled: true,
    custom: label === 'TRIM/EXIT',
    knownToRules: label !== 'TRIM/EXIT',
    category: 'Entry',
    direction: 'Buy',
  })),
});

export const finalActionCatalog = [
  { id: 'STARTER ENTRY', label: 'Starter Entry', category: 'Entry', direction: 'Buy', group: 'Watchlist' },
  { id: 'WAIT FOR PULLBACK', label: 'Wait For Pullback', category: 'Advisory', direction: 'Neutral', group: 'Watchlist' },
  { id: 'AVOID', label: 'Avoid', category: 'Advisory', direction: 'Neutral', group: 'Watchlist' },
];

/** Mutable mock state (properties, not rebindable exports, so every importer sees updates). */
export const signalState = {
  rows: freshRows(),
  settings: freshSettings(),
  /** When set, the API reports this many matching rows (PM-015: more than the 5,000-row evaluation limit). */
  largeTotal: null as number | null,
};

/** Re-seeds mutable signal state so one test's saves never leak into another. */
export function resetSignalsAudit(): void {
  signalState.rows = freshRows();
  signalState.settings = freshSettings();
  signalState.largeTotal = null;
}

/** PM-015: adds an old Active recommendation plus Acted / Dismissed / Invalidated history (opt-in so older specs keep 3 rows). */
export function seedLifecycleHistory(): void {
  const fa = (over: Record<string, unknown>) =>
    baseRow({
      signalType: 'FINAL_ACTION:STARTER ENTRY',
      signalTypeLabel: 'Starter Entry',
      source: 'Portfolio',
      score: null,
      return5D: pending,
      return20D: pending,
      return1M: pending,
      today: pending,
      myAction: 'Pending',
      status: 'Active',
      effectiveState: 'PriorSessionValid',
      isActionable: true,
      evidenceHealth: 'Unknown',
      ...over,
    });
  signalState.rows.push(
    // Weeks old but still Active and still actionable from a prior completed session: must be listed, with no warning.
    fa({ id: 10, symbol: 'WMT.TO', signalDate: isoDate(-25), daysSince: 25, signalPrice: 80 }),
    // Active but its evidence is stale and it is not currently actionable: the single compact warning applies.
    fa({ id: 11, symbol: 'BMO.TO', signalDate: isoDate(-2), daysSince: 2, signalPrice: 120, effectiveState: 'AwaitingData', isActionable: false, evidenceHealth: 'Stale', reasonCodes: ['AWAITING_STALE_DATA'] }),
    fa({ id: 12, symbol: 'ACT.TO', signalDate: isoDate(-30), daysSince: 30, signalPrice: 55, status: 'Acted', myAction: 'Actioned', effectiveState: null, isActionable: null, evidenceHealth: null }),
    fa({ id: 13, symbol: 'DIS.TO', signalDate: isoDate(-12), daysSince: 12, signalPrice: 31, status: 'Dismissed', effectiveState: null, isActionable: null, evidenceHealth: null }),
    fa({ id: 14, symbol: 'INV.TO', signalDate: isoDate(-8), daysSince: 8, signalPrice: 22, status: 'Invalidated', statusReason: 'Condition no longer produced', effectiveState: null, isActionable: null, evidenceHealth: null }),
  );
}

export const signalTypes = [
  { key: 'PRIORITY_CANDIDATE', label: 'Priority Candidate', category: 'Entry', direction: 'Buy' },
];

export function signalSummary(rows: Row[], largeTotal: number | null = null) {
  const followed = (r: Row) => r.myAction === 'Actioned' || r.myAction === 'Partial';
  return {
    total: largeTotal ?? rows.length,
    evaluated: largeTotal ? 5000 : rows.length,
    evaluationLimit: 5000,
    partial: !!largeTotal && largeTotal > 5000,
    priorityCandidate: rows.length,
    entryAdd: rows.length,
    trimExit: 0,
    actioned: rows.filter(followed).length,
    pendingReview: rows.filter((r) => r.myAction === 'Pending').length,
    effectiveness: rows.length
      ? [
          {
            signalType: 'PRIORITY_CANDIDATE',
            label: 'Priority Candidate',
            direction: 'Buy',
            count: rows.length,
            observations5D: 1,
            avg5D: 3.1,
            observations20D: 1,
            avg20D: -1.8,
            observations1M: 1,
            avg1M: 0.9,
            favourableObservations: 1,
            favourablePct: 0,
            actionedCount: rows.filter(followed).length,
          },
        ]
      : [],
  };
}

const effRow = (over: Record<string, unknown>) => ({
  directionDefined: true,
  overlappingCount: 0,
  observations5D: 0,
  rawAvg5D: null,
  adjustedAvg5D: null,
  observations20D: 0,
  rawAvg20D: null,
  adjustedAvg20D: null,
  observations1M: 0,
  rawAvg1M: null,
  adjustedAvg1M: null,
  favourableObservations: 0,
  favourablePct: null,
  actionedCount: 0,
  ...over,
});

const coverage = (cohortEvents: number, noPriceHistory = 0) => ({
  cohortEvents,
  withPriceHistory: cohortEvents - noPriceHistory,
  noPriceHistory,
  overlappingEvents: 1,
  complete: true,
  batches: 1,
  note: noPriceHistory
    ? `${noPriceHistory} of ${cohortEvents} signals have no usable price history and are not evaluated; the figures use the rest.`
    : null,
});

/**
 * Deterministic historical effectiveness: depends ONLY on the window and the context filters (role, source,
 * signal type), mirroring the server. The Priority Candidate row is an Entry followed by declines; the exit row is
 * a Sell signal followed by declines (raw negative, adjusted positive); the watch row has no defined direction.
 */
export function signalEffectiveness(q: URLSearchParams) {
  const window = q.get('window') ?? '90';
  const roles = q.getAll('role');
  const end = isoDate(0);
  const start = window === 'all' ? null : isoDate(-Number(window));
  const base = { window, windowStart: start, windowEnd: end };

  if (roles.length > 0 && !roles.some((r) => ['Core', 'Strategic', 'Swing'].includes(r))) {
    return { ...base, coverage: { ...coverage(0), overlappingEvents: 0 }, rows: [] };
  }
  if (window === '30') {
    return {
      ...base,
      coverage: coverage(2),
      rows: [effRow({ signalType: 'PRIORITY_CANDIDATE', label: 'Priority Candidate', direction: 'Buy', eligible: 2, observations5D: 2, rawAvg5D: 1.2, adjustedAvg5D: 1.2 })],
    };
  }
  const entry = effRow({
    signalType: 'PRIORITY_CANDIDATE', label: 'Priority Candidate', direction: 'Buy', eligible: 6, overlappingCount: 1,
    observations5D: 6, rawAvg5D: 2.1, adjustedAvg5D: 2.1,
    observations20D: 4, rawAvg20D: -3.2, adjustedAvg20D: -3.2,
    observations1M: 3, rawAvg1M: 0.4, adjustedAvg1M: 0.4,
    favourableObservations: 4, favourablePct: 25, actionedCount: 2,
  });
  const exit = effRow({
    signalType: 'FA:CUT LOSS / EXIT', label: 'Cut Loss / Exit', direction: 'Sell', eligible: 3,
    observations5D: 3, rawAvg5D: -1.5, adjustedAvg5D: 1.5,
    observations20D: 3, rawAvg20D: -4.5, adjustedAvg20D: 4.5,
    observations1M: 2, rawAvg1M: -6, adjustedAvg1M: 6,
    favourableObservations: 3, favourablePct: 100,
  });
  const watch = effRow({
    signalType: 'FA:BUY WATCH', label: 'Buy Watch', direction: 'Neutral', directionDefined: false, eligible: 2,
    observations5D: 2, rawAvg5D: 0.8, observations20D: 1, rawAvg20D: 2.4,
  });
  return {
    ...base,
    coverage: coverage(window === '180' || window === 'all' ? 14 : 11, window === '180' ? 2 : 0),
    rows: [entry, exit, watch],
  };
}

export const signalCandidates = [
  {
    id: 21,
    symbol: 'LMT.TO',
    transactionType: 'OPEN',
    accountType: 'TFSA_L_TD',
    date: isoDate(-2),
    shares: 500,
    price: 28.71,
    decisionSource: 'App Signal',
    daysFromSignal: 1,
    isLinked: false,
  },
];

export function signalDetail(row: Row) {
  return {
    row,
    capturedAtUtc: `${row['signalDate']}T21:05:00Z`,
    ruleVersion: 'priority-score-v1',
    note: row['hasNote'] ? 'Followed the score' : null,
    myActionUpdatedUtc: null,
    episodeEndedDate: null,
    context: {
      score: { total: row['score'], portfolioNeed: 20, technical: 25, fundamental: 20, risk: 13, badge: 'HIGH_PRIORITY' },
      trendShift: 'Bullish Shift',
      rsi: 44.2,
      allocationStatus: 'under',
      technicalFacts: { maStructure: 'Above 50/200', maCrossState: null, priceStructure: { label: 'Ascending Channel' } },
    },
    linkedTransaction: null,
  };
}
