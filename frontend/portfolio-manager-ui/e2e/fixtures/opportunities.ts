import type {
  OpportunityConfig,
  OpportunityDetail,
  OpportunityFinalAction,
  OpportunityRow,
  OpportunityScoreSummary,
  OpportunitySnapshot,
  ScoreResult,
} from '../../src/app/core/models/opportunities.models';
import type { FinalActionResult } from '../../src/app/core/models/portfolio.models';
import { isoDate, isoDateTime } from './factories';

// PM-029 e2e fixtures. Every number is arbitrary test data, deliberately unlike the design prototypes.

export const opportunityConfig: OpportunityConfig = {
  version: 'pm029-v1',
  disclaimer: 'Scores are weighted setup assessments, not probabilities.',
  sectorMethodology: 'Split-adjusted price returns over completed sessions; dividends excluded.',
  strongMin: 85,
  attractiveMin: 70,
  moderateMin: 55,
  cautionMin: 40,
  minCompletenessPct: 70,
  shortWindowSessions: 20,
  longWindowSessions: 60,
  neutralBandPp: 0.5,
  intensity2Pp: 2,
  intensity3Pp: 5,
  actionableRewardRisk: 1.5,
  fundamentalsMaxAgeDays: 7,
};

const score = (s: Partial<OpportunityScoreSummary>): OpportunityScoreSummary => ({
  score: null,
  completenessPct: 100,
  status: 'Verified',
  band: 'None',
  reasonCodes: [],
  ...s,
});

const singleFa = (label: string, cls: string, actionable = false): OpportunityFinalAction => ({
  kind: 'Single',
  label,
  finalActionId: label.toUpperCase().replace(/[^A-Z]+/g, '_'),
  category: 'Advisory',
  finalActionClass: cls,
  isActionable: actionable,
  evaluationState: 'CurrentValid',
  evidenceHealth: 'Fresh',
  reason: `Authoritative ${label} decision`,
  decisionCount: 1,
  distinctLabels: [label],
});

const noFa: OpportunityFinalAction = {
  kind: 'None',
  label: null,
  finalActionId: null,
  category: null,
  finalActionClass: null,
  isActionable: false,
  evaluationState: null,
  evidenceHealth: null,
  reason: null,
  decisionCount: 0,
  distinctLabels: [],
};

function row(symbol: string, p: Partial<OpportunityRow> & { price: number }): OpportunityRow {
  const { price, ...rest } = p;
  return {
    symbol,
    companyName: `${symbol.split('.')[0]} Test Corp`,
    analysisTicker: symbol,
    usesUnderlying: false,
    exchange: 'Tsx',
    currency: 'CAD',
    sources: ['Watchlist'],
    owned: false,
    accounts: [],
    roles: [],
    watchlistRole: 'Swing',
    sector: 'Industrials',
    industry: 'Machinery',
    sectorSource: 'Provider',
    swing: score({}),
    strategic: score({}),
    sectorHeat: {
      diffPp: null,
      diff60Pp: null,
      stockReturnPct: null,
      benchmarkReturnPct: null,
      status: 'Unmapped',
      direction: 'None',
      intensity: 0,
      benchmarkSymbol: null,
    },
    setup: {
      kind: 'Unavailable',
      entry: null,
      stop: null,
      target: null,
      targetSource: null,
      rewardRisk: null,
      riskPct: null,
      rewardPct: null,
      stopDistanceAtr: null,
      currency: 'CAD',
      reasonCodes: ['NoStructuralTarget'],
    },
    risk: { category: 'Unavailable', riskToStopPct: null, atrPct: null, hardStructuralNegative: false, basis: 'Scanner' },
    fit: {
      status: 'Unavailable',
      reasonCodes: ['AllocationUnverifiable'],
      weightPct: null,
      positionLimitPct: null,
      positionHeadroomPct: null,
      sectorWeightPct: null,
      sectorTargetPct: null,
      sectorHeadroomPct: null,
    },
    finalAction: noFa,
    indicators: {
      price,
      changePct: 0.42,
      rsi14: 51.3,
      rsiSignal: 49.8,
      macdHistogram: 0.031,
      ema20: price * 0.98,
      sma50: price * 0.95,
      sma200: price * 0.9,
      atr: price * 0.025,
      volumeRatio: 1.08,
      week52High: price * 1.2,
      week52Low: price * 0.7,
      dynamicStopLoss: price * 0.94,
      trendShift: null,
      maStructure: 'Bullish',
      fibZone: null,
      channelState: null,
      rs20Pp: null,
      rs60Pp: null,
    },
    freshness: {
      dataAsOfDate: isoDate(-1),
      expectedSession: isoDate(-1),
      scannedAtUtc: isoDateTime(-1, 22),
      isStale: false,
      fundamentalsAsOfUtc: isoDateTime(-2, 22),
      fundamentalsOrigin: 'Watchlist',
    },
    rowStatus: 'Ok',
    ...rest,
  };
}

const setupFor = (entry: number, stop: number, target: number) => ({
  kind: 'ValidCandidate' as const,
  entry,
  stop,
  target,
  targetSource: 'Week52High',
  rewardRisk: Math.round(((target - entry) / (entry - stop)) * 100) / 100,
  riskPct: Math.round(((entry - stop) / entry) * 1000) / 10,
  rewardPct: Math.round(((target - entry) / entry) * 1000) / 10,
  stopDistanceAtr: 1.7,
  currency: 'CAD',
  reasonCodes: [],
});

const heat = (diff: number, direction: 'Positive' | 'Negative' | 'Neutral', intensity: number, bench = 'XIT.TO') => ({
  diffPp: diff,
  diff60Pp: diff * 1.6,
  stockReturnPct: 4.1 + diff,
  benchmarkReturnPct: 4.1,
  status: 'Matched' as const,
  direction,
  intensity,
  benchmarkSymbol: bench,
});

const eligibleFit = {
  status: 'Eligible' as const,
  reasonCodes: [],
  weightPct: 3.2,
  positionLimitPct: 8,
  positionHeadroomPct: 4.8,
  sectorWeightPct: 13.7,
  sectorTargetPct: 20,
  sectorHeadroomPct: 7.6,
};

export function opportunityRows(): OpportunityRow[] {
  return [
    row('QRK.TO', {
      price: 48.2,
      sources: ['Portfolio', 'Watchlist'],
      owned: true,
      accounts: ['TFSA_E2E'],
      roles: ['Swing'],
      sector: 'Technology',
      swing: score({ score: 88, band: 'Strong' }),
      strategic: score({ score: 73, band: 'Attractive' }),
      sectorHeat: heat(3.6, 'Positive', 2),
      setup: setupFor(48.2, 45.1, 56.3),
      risk: { category: 'Medium', riskToStopPct: 6.4, atrPct: 2.5, hardStructuralNegative: false, basis: 'Scanner' },
      fit: eligibleFit,
      finalAction: singleFa('Hold / Monitor', 'ma-hold'),
    }),
    row('PLW.TO', {
      price: 17.35,
      sources: ['Portfolio'],
      owned: true,
      accounts: ['RRSP_E2E', 'TFSA_E2E'],
      roles: ['Swing', 'Strategic'],
      sector: 'Financials',
      swing: score({ score: 76, band: 'Attractive', status: 'Provisional', completenessPct: 78, reasonCodes: ['LowCompleteness'] }),
      strategic: score({ score: 58, band: 'Moderate' }),
      sectorHeat: heat(-2.4, 'Negative', 2, 'XFN.TO'),
      setup: setupFor(17.35, 16.2, 19.9),
      risk: { category: 'Medium', riskToStopPct: 6.6, atrPct: 2.8, hardStructuralNegative: false, basis: 'Scanner' },
      fit: { ...eligibleFit, status: 'Review', reasonCodes: ['PositionNearLimit'] },
      finalAction: {
        ...singleFa('Hold / Monitor', 'ma-hold'),
        kind: 'Multiple',
        label: null,
        finalActionId: null,
        finalActionClass: null,
        decisionCount: 2,
        distinctLabels: ['Hold / Monitor', 'Trim / Review'],
        reason: null,
      },
    }),
    row('NVX.TO', {
      price: 9.81,
      sector: 'Materials',
      swing: score({ score: null, status: 'Unavailable', completenessPct: 52, reasonCodes: ['NoStructuralTarget'] }),
      strategic: score({ score: 67, band: 'Moderate' }),
      sectorHeat: heat(0.3, 'Neutral', 0, 'XMA.TO'),
      finalAction: singleFa('Watch', 'ma-watch'),
    }),
    row('BRQ.TO', {
      price: 63.4,
      sources: ['TSX60'],
      watchlistRole: null,
      sector: 'Energy',
      swing: score({ score: 34, band: 'Weak' }),
      strategic: score({ score: 47, band: 'Caution' }),
      sectorHeat: heat(-6.1, 'Negative', 3, 'XEG.TO'),
      setup: setupFor(63.4, 59.8, 65.1),
      risk: { category: 'High', riskToStopPct: 5.7, atrPct: 3.6, hardStructuralNegative: true, basis: 'Scanner' },
      fit: eligibleFit,
    }),
    row('ZEN.NE', {
      price: 22.05,
      analysisTicker: 'ZEN',
      usesUnderlying: true,
      exchange: 'Neo',
      sector: 'Technology',
      swing: score({ score: 81, band: 'Attractive' }),
      strategic: score({ score: null, status: 'Insufficient', completenessPct: 44, reasonCodes: ['FundamentalsMissing'] }),
      setup: setupFor(22.05, 20.9, 25.4),
      risk: { category: 'Low', riskToStopPct: 5.2, atrPct: 1.9, hardStructuralNegative: false, basis: 'Scanner' },
      fit: eligibleFit,
      finalAction: singleFa('Entry / Add', 'ma-buy', true),
    }),
  ];
}

export interface OpportunityMockState {
  snapshotId: number;
  rows: OpportunityRow[];
  refreshes: number;
}

export const opportunityState: OpportunityMockState = { snapshotId: 41, rows: opportunityRows(), refreshes: 0 };

export function resetOpportunities(): void {
  opportunityState.snapshotId = 41;
  opportunityState.rows = opportunityRows();
  opportunityState.refreshes = 0;
}

export function opportunitySnapshotResponse(): OpportunitySnapshot {
  const rows = opportunityState.rows;
  return {
    snapshotId: opportunityState.snapshotId,
    scoringVersion: 'pm029-v1',
    disclaimer: opportunityConfig.disclaimer,
    computedAtUtc: isoDateTime(-1, 22),
    finalActionComputedAtUtc: isoDateTime(-1, 22),
    dataAsOfSession: isoDate(-1),
    status: 'Succeeded',
    errorSummary: null,
    lastAttempt: { atUtc: isoDateTime(-1, 22), status: 'Succeeded', errorSummary: null },
    counts: {
      watchlist: rows.filter((r) => r.sources.includes('Watchlist')).length,
      portfolio: rows.filter((r) => r.sources.includes('Portfolio')).length,
      tsx60: rows.filter((r) => r.sources.includes('TSX60')).length,
      total: rows.length,
    },
    isStale: false,
    portfolioChangedSinceEvaluation: false,
    refreshInFlight: false,
    rows,
  };
}

function scoreResult(perspective: 'Swing' | 'Strategic', s: OpportunityScoreSummary): ScoreResult {
  const available = s.score !== null;
  return {
    perspective,
    scoringVersion: 'pm029-v1',
    score: s.score,
    normalizedScore: s.score,
    awardedTotal: s.score ?? 0,
    availableMax: Math.round(s.completenessPct),
    completenessPct: s.completenessPct,
    status: s.status,
    band: s.band,
    components: [
      {
        code: perspective === 'Swing' ? 'S1' : 'F1',
        name: perspective === 'Swing' ? 'Trend structure' : 'Valuation',
        max: 30,
        essential: true,
        state: available ? 'Observed' : 'Missing',
        awarded: available ? 22 : 0,
        availableMax: available ? 30 : 0,
        items: [
          { code: 'A', label: 'Price above EMA20', max: 10, points: available ? 10 : 0, available, detail: null },
          { code: 'B', label: 'EMA20 above SMA50', max: 20, points: available ? 12 : 0, available, detail: null },
        ],
        missingReasons: s.reasonCodes,
        note: null,
      },
    ],
    reasonCodes: s.reasonCodes,
  };
}

const decision = (symbol: string, account: string, role: string, label: string, cls: string): FinalActionResult =>
  ({
    symbol,
    source: 'Portfolio',
    itemId: 1,
    role,
    finalAction: label,
    finalActionId: label.toUpperCase(),
    category: 'Advisory',
    direction: 'Neutral',
    severity: 'hold',
    priority: 'INFORMATIONAL',
    finalActionClass: cls,
    trendSetup: 'Constructive',
    momentumShift: 'None',
    baseAction: label,
    gapStatus: 'No Gap',
    hoverDescription: `${account} ${role}: ${label}`,
    reason: `${account} ${role} authoritative decision`,
    hasHardStructuralNegative: false,
    analysisPrice: 17.35,
    analysisTicker: symbol,
    analysisCurrency: 'CAD',
    trendSetupReason: '',
    momentumShiftReason: '',
    trendSetupClass: '',
    momentumShiftClass: '',
    gapScore: 0,
    gapPct: 0,
    isActionable: false,
    reasonCodes: [],
    evaluationState: 'CurrentValid',
    evidenceHealth: 'Fresh',
  }) as FinalActionResult;

export function opportunityDetailResponse(symbol: string, snapshotId: number | null): OpportunityDetail | { status: number } {
  if (snapshotId !== null && snapshotId !== opportunityState.snapshotId) return { status: 410 };
  const r = opportunityState.rows.find((x) => x.symbol === symbol);
  if (!r) return { status: 404 };
  const decisions =
    r.finalAction.kind === 'Multiple'
      ? [
          decision(r.symbol, 'RRSP_E2E', 'Strategic', 'Hold / Monitor', 'ma-hold'),
          decision(r.symbol, 'TFSA_E2E', 'Swing', 'Trim / Review', 'ma-trim'),
        ]
      : r.finalAction.kind === 'Single'
        ? [decision(r.symbol, r.accounts[0] ?? 'Watchlist', r.roles[0] ?? 'Swing', r.finalAction.label!, r.finalAction.finalActionClass!)]
        : [];
  return {
    snapshotId: opportunityState.snapshotId,
    row: r,
    swingScore: scoreResult('Swing', r.swing),
    strategicScore: scoreResult('Strategic', r.strategic),
    sectorRelative:
      r.sectorHeat.status === 'Matched'
        ? {
            status: 'Matched',
            benchmarkSymbol: r.sectorHeat.benchmarkSymbol,
            benchmarkName: `${r.sector} benchmark`,
            expectedSession: isoDate(-1),
            d20: {
              sessions: 20,
              status: 'Matched',
              startSession: isoDate(-29),
              endSession: isoDate(-1),
              stockReturnPct: r.sectorHeat.stockReturnPct,
              benchmarkReturnPct: r.sectorHeat.benchmarkReturnPct,
              diffPp: r.sectorHeat.diffPp,
              displayDiffPp: r.sectorHeat.diffPp,
              direction: r.sectorHeat.direction,
              intensity: r.sectorHeat.intensity,
              stockObservations: 21,
              benchmarkObservations: 21,
              expectedObservations: 21,
              isAvailable: true,
            },
            d60: {
              sessions: 60,
              status: 'InsufficientObservations',
              startSession: null,
              endSession: null,
              stockReturnPct: null,
              benchmarkReturnPct: null,
              diffPp: null,
              displayDiffPp: null,
              direction: 'None',
              intensity: 0,
              stockObservations: 44,
              benchmarkObservations: 61,
              expectedObservations: 61,
              isAvailable: false,
            },
            methodology: opportunityConfig.sectorMethodology,
          }
        : null,
    setup: { ...r.setup, isScorable: r.setup.kind !== 'Unavailable' },
    decisions,
    savedPlans: r.owned
      ? [
          {
            account: r.accounts[0],
            averageCost: (r.indicators.price ?? 0) * 0.93,
            plannedStop: (r.indicators.price ?? 0) * 0.9,
            target: null,
            rewardRisk: null,
            stopType: 'Manual',
            currency: 'CAD',
            updatedAtUtc: isoDateTime(-5, 15),
          },
        ]
      : [],
    evidence: {
      scoringVersion: 'pm029-v1',
      disclaimer: opportunityConfig.disclaimer,
      sectorMethodology: opportunityConfig.sectorMethodology,
      historySource: 'Daily OHLC (split-adjusted)',
      benchmarkSymbol: r.sectorHeat.benchmarkSymbol,
      benchmarkName: r.sectorHeat.benchmarkSymbol ? `${r.sector} benchmark` : null,
      sectorSource: r.sectorSource,
      scanAsOfUtc: r.freshness.scannedAtUtc,
      scanTradingDate: r.freshness.dataAsOfDate,
      expectedSession: r.freshness.expectedSession,
      fundamentalsOrigin: r.freshness.fundamentalsOrigin,
      fundamentalsAsOfUtc: r.freshness.fundamentalsAsOfUtc,
      earningsYieldBasis: 'Trailing',
      fcfYieldBasis: 'Trailing',
      capitalEfficiencyBasis: 'ROE',
      allocationStatus: r.owned ? 'Verified' : null,
      portfolioUpdatedAtUtc: isoDateTime(-1, 21),
      finalActionEvaluationId: 'eval-e2e',
      finalActionComputedAtUtc: isoDateTime(-1, 22),
      warnings: r.usesUnderlying ? ['FundamentalsMissing'] : [],
    },
  };
}
