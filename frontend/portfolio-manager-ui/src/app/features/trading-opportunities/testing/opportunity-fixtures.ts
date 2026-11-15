// SYNTHETIC test-only fixtures for PM-029; values are not production or verified financial/security data.
import {
  OpportunityDetail,
  OpportunityRow,
  OpportunityScoreSummary,
  OpportunitySnapshot,
  ScoreResult,
} from '../../../core/models/opportunities.models';

export function scoreSummary(partial: Partial<OpportunityScoreSummary> = {}): OpportunityScoreSummary {
  return { score: 77, completenessPct: 100, status: 'Verified', band: 'Attractive', reasonCodes: [], ...partial };
}

export function unscorable(reason: string): OpportunityScoreSummary {
  return { score: null, completenessPct: 41, status: 'Unavailable', band: 'None', reasonCodes: [reason] };
}

export function opportunityRow(symbol: string, partial: Partial<OpportunityRow> = {}): OpportunityRow {
  return {
    symbol,
    companyName: `${symbol} Holdings`,
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
    industry: 'Railroads',
    sectorSource: 'Provider',
    swing: scoreSummary(),
    strategic: scoreSummary({ score: 64, band: 'Moderate' }),
    sectorHeat: {
      diffPp: 2.7,
      diff60Pp: -1.4,
      stockReturnPct: 6.2,
      benchmarkReturnPct: 3.5,
      status: 'Matched',
      direction: 'Positive',
      intensity: 2,
      benchmarkSymbol: 'ZIN.TO',
    },
    setup: {
      kind: 'ValidCandidate',
      entry: 23.45,
      stop: 21.95,
      target: 27.15,
      targetSource: 'Week52High',
      rewardRisk: 2.47,
      riskPct: 6.4,
      rewardPct: 15.8,
      stopDistanceAtr: 1.6,
      currency: 'CAD',
      reasonCodes: [],
    },
    risk: { category: 'Medium', riskToStopPct: 6.4, atrPct: 3.9, hardStructuralNegative: false, basis: 'Scanner' },
    fit: {
      status: 'Eligible',
      reasonCodes: [],
      weightPct: null,
      positionLimitPct: 7,
      positionHeadroomPct: 7,
      sectorWeightPct: 11.3,
      sectorTargetPct: 18,
      sectorHeadroomPct: 6.7,
    },
    finalAction: {
      kind: 'Single',
      label: 'Watch',
      finalActionId: 'WATCH',
      category: 'Monitor',
      finalActionClass: 'fa-watch',
      isActionable: false,
      evaluationState: 'Evaluated',
      evidenceHealth: 'Healthy',
      reason: 'Fixture reason',
      decisionCount: 1,
      distinctLabels: ['Watch'],
    },
    indicators: {
      price: 23.45,
      changePct: 0.83,
      rsi14: 57.2,
      rsiSignal: 54.1,
      macdHistogram: 0.12,
      ema20: 22.9,
      sma50: 22.1,
      sma200: 20.4,
      atr: 0.94,
      volumeRatio: 1.27,
      week52High: 27.15,
      week52Low: 17.6,
      dynamicStopLoss: 21.95,
      trendShift: null,
      maStructure: 'Bullish',
      fibZone: null,
      channelState: null,
      rs20Pp: 2.7,
      rs60Pp: -1.4,
    },
    freshness: {
      dataAsOfDate: '2026-11-13',
      expectedSession: '2026-11-13',
      scannedAtUtc: '2026-11-13T22:10:00Z',
      isStale: false,
      fundamentalsAsOfUtc: '2026-11-12T22:00:00Z',
      fundamentalsOrigin: 'Watchlist',
    },
    rowStatus: 'Ok',
    ...partial,
  };
}

export function opportunitySnapshot(
  rows: OpportunityRow[],
  partial: Partial<OpportunitySnapshot> = {},
): OpportunitySnapshot {
  return {
    snapshotId: 11,
    scoringVersion: 'pm029-v1',
    disclaimer: 'Fixture disclaimer',
    computedAtUtc: '2026-11-13T22:15:00Z',
    finalActionComputedAtUtc: '2026-11-13T22:14:00Z',
    dataAsOfSession: '2026-11-13',
    status: 'Succeeded',
    errorSummary: null,
    lastAttempt: { atUtc: '2026-11-13T22:15:00Z', status: 'Succeeded', errorSummary: null },
    counts: { watchlist: rows.length, portfolio: 0, tsx60: 0, total: rows.length },
    isStale: false,
    portfolioChangedSinceEvaluation: false,
    refreshInFlight: false,
    rows,
    ...partial,
  };
}

function scoreResult(perspective: 'Swing' | 'Strategic', summary: OpportunityScoreSummary): ScoreResult {
  return {
    perspective,
    scoringVersion: 'pm029-v1',
    score: summary.score,
    normalizedScore: summary.score,
    awardedTotal: summary.score ?? 0,
    availableMax: 100,
    completenessPct: summary.completenessPct,
    status: summary.status,
    band: summary.band,
    components: [],
    reasonCodes: summary.reasonCodes,
  };
}

export function opportunityDetail(row: OpportunityRow, snapshotId = 11): OpportunityDetail {
  return {
    snapshotId,
    row,
    swingScore: scoreResult('Swing', row.swing),
    strategicScore: scoreResult('Strategic', row.strategic),
    sectorRelative: null,
    setup: { ...row.setup, isScorable: row.setup.kind !== 'Unavailable' },
    decisions: [],
    savedPlans: [],
    evidence: {
      scoringVersion: 'pm029-v1',
      disclaimer: 'Fixture disclaimer',
      sectorMethodology: 'Fixture methodology',
      historySource: 'Yahoo',
      benchmarkSymbol: row.sectorHeat.benchmarkSymbol,
      benchmarkName: null,
      sectorSource: row.sectorSource,
      scanAsOfUtc: row.freshness.scannedAtUtc,
      scanTradingDate: row.freshness.dataAsOfDate,
      expectedSession: row.freshness.expectedSession,
      fundamentalsOrigin: row.freshness.fundamentalsOrigin,
      fundamentalsAsOfUtc: row.freshness.fundamentalsAsOfUtc,
      earningsYieldBasis: null,
      fcfYieldBasis: null,
      capitalEfficiencyBasis: null,
      allocationStatus: null,
      portfolioUpdatedAtUtc: null,
      finalActionEvaluationId: 'eval-fixture',
      finalActionComputedAtUtc: '2026-11-13T22:14:00Z',
      warnings: [],
    },
  };
}
