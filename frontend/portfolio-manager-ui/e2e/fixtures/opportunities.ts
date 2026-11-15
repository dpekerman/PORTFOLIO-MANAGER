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
import { isoDateTime } from './factories';

// SYNTHETIC PM-029 E2E fixtures only; values are not production or verified financial/security data.
// CCA preserves the no-setup Swing component evidence.

function marketSessionDate(daysAgo: number): string {
  const date = new Date();
  date.setUTCHours(0, 0, 0, 0);
  date.setUTCDate(date.getUTCDate() - daysAgo);
  while (date.getUTCDay() === 0 || date.getUTCDay() === 6) date.setUTCDate(date.getUTCDate() - 1);
  return date.toISOString().slice(0, 10);
}

function marketSessionTime(daysAgo: number, hour: number): string {
  return `${marketSessionDate(daysAgo)}T${String(hour).padStart(2, '0')}:30:00.000Z`;
}

export const opportunityConfig: OpportunityConfig = {
  version: 'pm029-v2',
  disclaimer: 'Swing-v2 is an evidence-adjusted technical ranking, not a probability of success or an executable buy recommendation.',
  sectorMethodology: 'Price return (split-adjusted, dividends excluded)',
  strongMin: 85,
  attractiveMin: 70,
  moderateMin: 60,
  cautionMin: 50,
  minCompletenessPct: 70,
  swingDenominatorMax: 80,
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
  const result: OpportunityRow = {
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
      dataAsOfDate: marketSessionDate(1),
      expectedSession: marketSessionDate(1),
      scannedAtUtc: marketSessionTime(1, 22),
      isStale: false,
      fundamentalsAsOfUtc: isoDateTime(-2, 22),
      fundamentalsOrigin: 'Watchlist',
    },
    rowStatus: 'Ok',
    ...rest,
  };
  result.indicators = {
    ...result.indicators,
    rs20Pp: result.sectorHeat.status === 'Matched' ? result.sectorHeat.diffPp : null,
    rs60Pp: result.sectorHeat.status === 'Matched' ? result.sectorHeat.diff60Pp : null,
  };
  return result;
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
  stopDistanceAtr: Math.round(((entry - stop) / (entry * 0.025)) * 100) / 100,
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
      setup: setupFor(48.2, 45.308, 57.84),
      risk: { category: 'Medium', riskToStopPct: 6.0, atrPct: 2.5, hardStructuralNegative: false, basis: 'Scanner' },
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
      industry: 'Banking',
      swing: score({ score: 76, band: 'Attractive', status: 'Provisional', completenessPct: 78, reasonCodes: ['LowCompleteness'] }),
      strategic: score({ score: 58, band: 'Moderate' }),
      sectorHeat: heat(-2.4, 'Negative', 2, 'XFN.TO'),
      setup: setupFor(17.35, 16.309, 20.82),
      risk: { category: 'Medium', riskToStopPct: 6.0, atrPct: 2.5, hardStructuralNegative: false, basis: 'Scanner' },
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
    row('CCA.TO', {
      price: 9.81,
      sector: 'Materials',
      swing: score({ score: 28, status: 'Provisional', completenessPct: 75, band: 'Weak', reasonCodes: ['SectorHeat:Unmapped'] }),
      strategic: score({ score: 67, band: 'Moderate' }),
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
      setup: setupFor(63.4, 59.596, 76.08),
      risk: { category: 'High', riskToStopPct: 6.0, atrPct: 2.5, hardStructuralNegative: true, basis: 'Scanner' },
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
      setup: setupFor(22.05, 20.727, 26.46),
      risk: { category: 'Low', riskToStopPct: 6.0, atrPct: 2.5, hardStructuralNegative: false, basis: 'Scanner' },
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
    scoringVersion: 'pm029-v2',
    disclaimer: opportunityConfig.disclaimer,
    computedAtUtc: isoDateTime(-1, 22),
    finalActionComputedAtUtc: isoDateTime(-1, 22),
    dataAsOfSession: marketSessionDate(1),
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

const swingComponents = [
  { code: 'S1', name: 'Trend & Structure', max: 25, essential: true },
  { code: 'S2', name: 'Relative Strength', max: 20, essential: false },
  { code: 'S3', name: 'RSI/MACD Momentum', max: 15, essential: true },
  { code: 'S4', name: 'Volume Confirmation', max: 10, essential: false },
  { code: 'S5', name: 'Entry Quality', max: 10, essential: false },
];

const strategicComponents = [
  { code: 'T1', name: 'Long-Term Trend', max: 20, essential: true },
  { code: 'T2', name: 'Relative Strength', max: 10, essential: false },
  { code: 'T3', name: 'Momentum', max: 5, essential: false },
  { code: 'T4', name: 'Volume', max: 5, essential: false },
  { code: 'T5', name: 'Entry Timing', max: 10, essential: false },
  { code: 'T6', name: 'Downside Risk', max: 10, essential: false },
  { code: 'T7', name: 'Valuation', max: 25, essential: false },
  { code: 'T8', name: 'Business Quality', max: 15, essential: true },
];

function distribute(total: number, capacities: readonly number[]): number[] {
  let remaining = total;
  return capacities.map((capacity) => {
    const value = Math.min(remaining, capacity);
    remaining -= value;
    return value;
  });
}

function scoreResult(perspective: 'Swing' | 'Strategic', s: OpportunityScoreSummary, symbol: string): ScoreResult {
  const swingV2 = perspective === 'Swing';
  const denominator = swingV2 ? 80 : 100;
  const availableMax = Math.round((s.completenessPct * denominator) / 100);
  const awardedTotal = s.score === null ? 0 : Math.round((s.score * (swingV2 ? denominator : availableMax)) / 100);
  const specs = swingV2 ? swingComponents : strategicComponents;
  const ccaSwingAvailability = [25, 0, 15, 10, 10];
  const ccaSwingAwarded = [0, 0, 8, 4, 10];
  const qrkSwingAwarded = [25, 17, 14, 7, 7];
  const isCcaSwing = swingV2 && symbol === 'CCA.TO';
  const componentAvailability = isCcaSwing
    ? ccaSwingAvailability
    : distribute(availableMax, specs.map((component) => component.max));
  const componentAwards = isCcaSwing
    ? ccaSwingAwarded
    : swingV2 && symbol === 'QRK.TO'
      ? qrkSwingAwarded
    : distribute(awardedTotal, componentAvailability);

  return {
    perspective,
    scoringVersion: swingV2 ? 'pm029-v2' : 'pm029-v1',
    score: s.score,
    normalizedScore: s.score,
    ...(swingV2 ? { scoreDenominatorMax: denominator } : {}),
    awardedTotal,
    availableMax,
    completenessPct: s.completenessPct,
    status: s.status,
    band: s.band,
    components: specs.map((component, index) => {
      const observedMax = componentAvailability[index];
      const state = observedMax === 0 ? 'Missing' : observedMax === component.max ? 'Observed' : 'Partial';
      const isQrKS2 = swingV2 && symbol === 'QRK.TO' && component.code === 'S2';
      return {
        ...component,
        state,
        awarded: componentAwards[index],
        availableMax: observedMax,
        items: isQrKS2
          ? [
              { code: 'Rs20', label: '20D vs sector', max: 14, points: 11, available: true, detail: '+3.6 pp' },
              { code: 'Rs60', label: '60D vs sector', max: 6, points: 6, available: true, detail: '+5.8 pp' },
            ]
          : [],
        missingReasons:
          isCcaSwing && component.code === 'S2'
            ? ['SectorHeat:Unmapped']
            : state === 'Missing'
              ? s.reasonCodes
              : [],
        note: null,
      };
    }),
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
  const d60Diff = r.sectorHeat.diff60Pp;
  const d60Direction = d60Diff === null ? 'None' : d60Diff > 0 ? 'Positive' : d60Diff < 0 ? 'Negative' : 'Neutral';
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
    swingScore: scoreResult('Swing', r.swing, r.symbol),
    strategicScore: scoreResult('Strategic', r.strategic, r.symbol),
    sectorRelative:
      r.sectorHeat.status === 'Matched'
        ? {
            status: 'Matched',
            benchmarkSymbol: r.sectorHeat.benchmarkSymbol,
            benchmarkName: `${r.sector} benchmark`,
            expectedSession: marketSessionDate(1),
            d20: {
              sessions: 20,
              status: 'Matched',
              startSession: marketSessionDate(29),
              endSession: marketSessionDate(1),
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
              status: d60Diff === null ? 'InsufficientObservations' : 'Matched',
              startSession: d60Diff === null ? null : marketSessionDate(85),
              endSession: d60Diff === null ? null : marketSessionDate(1),
              stockReturnPct: d60Diff === null ? null : 8 + d60Diff,
              benchmarkReturnPct: d60Diff === null ? null : 8,
              diffPp: d60Diff,
              displayDiffPp: d60Diff,
              direction: d60Direction,
              intensity: d60Diff === null ? 0 : Math.abs(d60Diff) >= 5 ? 3 : Math.abs(d60Diff) >= 2 ? 2 : 1,
              stockObservations: d60Diff === null ? 44 : 61,
              benchmarkObservations: d60Diff === null ? 44 : 61,
              expectedObservations: 61,
              isAvailable: d60Diff !== null,
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
      scoringVersion: 'pm029-v2',
      disclaimer: opportunityConfig.disclaimer,
      sectorMethodology: opportunityConfig.sectorMethodology,
      historySource: 'Yahoo Finance daily chart (split-adjusted close)',
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
      benchmarkCategory: r.sectorHeat.status === 'Matched' ? 'Sector mapping' : null,
      benchmarkCurrency: r.sectorHeat.status === 'Matched' ? 'CAD' : null,
      benchmarkMappingSource: r.sectorHeat.status === 'Matched' ? 'Synthetic E2E fixture' : null,
      benchmarkMappingRationale:
        r.sectorHeat.status === 'Matched' ? 'Fixture metadata only; not production mapping validation.' : null,
      benchmarkMappingAsOfDate: r.sectorHeat.status === 'Matched' ? marketSessionDate(1) : null,
    },
  };
}
