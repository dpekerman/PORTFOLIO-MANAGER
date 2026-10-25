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
export const signalState = { rows: freshRows(), settings: freshSettings() };

/** Re-seeds mutable signal state so one test's saves never leak into another. */
export function resetSignalsAudit(): void {
  signalState.rows = freshRows();
  signalState.settings = freshSettings();
}

export const signalTypes = [
  { key: 'PRIORITY_CANDIDATE', label: 'Priority Candidate', category: 'Entry', direction: 'Buy' },
];

export function signalSummary(rows: Row[]) {
  const followed = (r: Row) => r.myAction === 'Actioned' || r.myAction === 'Partial';
  return {
    total: rows.length,
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
