import type { BrokerStop, JournalRow, RiskAuditEvent, RiskPlan, RiskPosition, SwingTradeRow } from '../../src/app/core/models/swing-trading.models';
import type { MockRequest } from '../support/mock-api';

/**
 * Deterministic Swing Trading data for the mocked API. Lot ids start at 9001 so they never collide with the portfolio
 * fixtures. Every mutation lives in `swingState`, which `resetSwing()` rebuilds before each test (isolated data: the
 * live portfolio database is never touched).
 */
const NOW = '2026-10-28T20:00:00Z';
const SESSION = '2026-10-28';

type RowOver = Partial<SwingTradeRow> & Pick<SwingTradeRow, 'symbol' | 'account' | 'lotIds'>;

const stop = (id: number, trigger: number, over: Partial<BrokerStop> = {}): BrokerStop => ({
  id,
  orderIdentity: `00000000-0000-0000-0000-${String(id).padStart(12, '0')}`,
  orderType: 'StopMarket',
  triggerPrice: trigger,
  limitPrice: null,
  currency: 'CAD',
  userStatus: 'UserConfirmedEstablished',
  brokerVerificationStatus: 'Unverified',
  coveredQuantity: null,
  placedDate: null,
  brokerOrderRef: null,
  lastUserVerifiedDate: null,
  confirmationSource: 'UserSuppliedStopSheet',
  notes: 'Initial user-supplied stop sheet',
  episodeOpenDate: '2026-08-13T00:00:00',
  isActive: true,
  belongsToCurrentEpisode: true,
  brokerCancellationUnverified: false,
  replacedByRecordId: null,
  updatedAtUtc: NOW,
  rowVersion: `v${id}`,
  ...over,
});

function row(over: RowOver): SwingTradeRow {
  const stops = over.brokerStops ?? [];
  return {
    key: `${over.account}|${over.symbol}`,
    companyName: `${over.symbol} Corp`,
    anchorItemId: over.lotIds[0],
    decisionSource: 'Manual - Buy on pullback',
    episodeOpenDate: '2026-08-13T00:00:00',
    firstPurchaseDate: '2026-08-13T00:00:00',
    lastExecutionDate: '2026-08-13T00:00:00',
    daysHeld: 76,
    episodeResolution: 'Derived',
    episodeNote: null,
    swingQuantity: 600,
    totalQuantity: 600,
    mixedRole: false,
    averageCost: 17,
    currency: 'CAD',
    currencyBasis: 'AssumedFromSuffix',
    currentPrice: 18,
    priceAsOfUtc: NOW,
    priceSessionDate: SESSION,
    priceBasis: 'EOD',
    priceSource: 'completed-session close from snapshot quote',
    marketValue: 10800,
    unrealizedPnl: 600,
    unrealizedPnlPct: 5.88,
    plannedStop: null,
    targetPrice: null,
    stopType: null,
    planFromPriorEpisode: false,
    brokerStopTrigger: stops[0]?.triggerPrice ?? null,
    brokerOrderType: stops[0]?.orderType ?? null,
    brokerStatus: stops[0]?.userStatus ?? null,
    brokerVerification: stops[0]?.brokerVerificationStatus ?? null,
    brokerStops: stops,
    coveredQuantity: null,
    coverageState: stops.length === 0 ? 'None' : 'Unknown',
    protection: stops.length === 0 ? 'No Stop' : 'Broker Stop Recorded',
    riskStatuses: [],
    severity: 'None',
    severityRank: 6,
    needsAttention: false,
    distanceAbs: null,
    distancePct: null,
    governingStopSource: stops.length ? 'Broker stop (user-recorded)' : null,
    observation: null,
    ...over,
  } as SwingTradeRow;
}

function buildRows(): SwingTradeRow[] {
  return [
    row({
      symbol: 'DIS.TO', account: 'TFSA_L_TD', lotIds: [9001], companyName: 'WALT DISNEY CDR (CAD HEDGED)', swingQuantity: 1500, totalQuantity: 1500,
      averageCost: 11.66, currentPrice: 11.5, marketValue: 17250, unrealizedPnl: -240, unrealizedPnlPct: -1.37, brokerStops: [stop(1, 11.85)],
      riskStatuses: ['StopBreached', 'UnknownCoverage', 'MissingRiskPlan'], severity: 'Critical', severityRank: 1, needsAttention: true,
      distanceAbs: -0.35, distancePct: -3.04,
      observation: 'Price 11.5 is at/below the recorded stop 11.85 as of 2026-10-28 (EOD). This is a market observation only; it does not show that a broker order triggered or filled. Verify with your broker.',
    }),
    row({
      symbol: 'NVDA.TO', account: 'Corp_TD', lotIds: [9002], companyName: 'NVIDIA Corporation', swingQuantity: 400, totalQuantity: 400, averageCost: 49.52,
      currentPrice: 48, marketValue: 19200, unrealizedPnl: -608, unrealizedPnlPct: -3.07, brokerStops: [stop(2, 46.5)],
      riskStatuses: ['ApproachingStop', 'UnknownCoverage', 'MissingRiskPlan'], severity: 'High', severityRank: 3, needsAttention: true,
      distanceAbs: 1.5, distancePct: 3.13,
    }),
    row({
      symbol: 'NOVO.TO', account: 'TFSA_D_TD', lotIds: [9003], companyName: 'Novo Nordisk A/S', plannedStop: 16.5, targetPrice: 21, stopType: 'Fixed',
      brokerStops: [stop(3, 16.2, { coveredQuantity: 300 })], coveredQuantity: 300, coverageState: 'Partial', protection: 'Partially Covered',
      riskStatuses: ['PartialProtection', 'StopMismatch'], severity: 'Medium', severityRank: 4, needsAttention: false, distanceAbs: 1.8, distancePct: 10,
    }),
    row({
      symbol: 'EMP-A.TO', account: 'TFSA_L_TD', lotIds: [9004], companyName: 'Empire Company Limited', swingQuantity: 200, totalQuantity: 200,
      averageCost: 51.15, currentPrice: 49.9, priceBasis: 'Stale', marketValue: 9980, unrealizedPnl: -250, unrealizedPnlPct: -2.44,
      priceAsOfUtc: '2026-10-20T20:00:00Z', brokerStops: [stop(4, 48.25)], riskStatuses: ['StaleEvidence', 'UnknownCoverage', 'MissingRiskPlan'],
      severity: 'Medium', severityRank: 4,
    }),
    row({
      symbol: 'EFR.TO', account: 'TFSA_D_TD', lotIds: [9005], companyName: 'Energy Fuels Inc.', swingQuantity: 1000, totalQuantity: 1000,
      averageCost: 12.13, currentPrice: 13.4, marketValue: 13400, unrealizedPnl: 1270, unrealizedPnlPct: 10.47, episodeOpenDate: '2026-10-15T00:00:00',
      daysHeld: 13, riskStatuses: ['MissingRiskPlan'], severity: 'Medium', severityRank: 4, needsAttention: true,
    }),
    row({
      symbol: 'TA.TO', account: 'Corp_TD', lotIds: [9006], companyName: 'TransAlta Corporation', swingQuantity: 1000, totalQuantity: 4000, mixedRole: true,
      averageCost: 19.2, currentPrice: 20.1, marketValue: 20100, unrealizedPnl: 900, unrealizedPnlPct: 4.69, brokerStops: [stop(6, 16.75)],
      coverageState: 'Ambiguous', riskStatuses: ['AmbiguousCoverage', 'MissingRiskPlan'], severity: 'Medium', severityRank: 4,
    }),
    row({
      symbol: 'WELL.TO', account: 'Corp_TD', lotIds: [9007], companyName: 'WELL Health Technologies Corp.', swingQuantity: 2000, totalQuantity: 2000,
      averageCost: 4.4, currentPrice: 4.6, marketValue: 9200, unrealizedPnl: 400, unrealizedPnlPct: 4.55, episodeResolution: 'Unresolved',
      episodeNote: 'A CLOSE record (#637) in this account overlaps the open period; it cannot be proven to belong to this trade.',
      plannedStop: 4.0, stopType: 'Fixed', protection: 'Alert-Only Stop (no broker order)', riskStatuses: ['NoBrokerProtection'], severity: 'Medium',
      severityRank: 4, needsAttention: true, distanceAbs: 0.6, distancePct: 13.04, governingStopSource: 'Planned stop (alert only)',
    }),
    row({
      symbol: 'MAL.TO', account: 'TFSA_D_TD', lotIds: [9008], companyName: 'Magellan Aerospace Corporation', swingQuantity: 200, totalQuantity: 200,
      averageCost: 34.1, currentPrice: 36.2, marketValue: 7240, unrealizedPnl: 420, unrealizedPnlPct: 6.16, episodeOpenDate: '2026-10-26T00:00:00',
      daysHeld: 2, brokerStops: [stop(8, 32.4)], riskStatuses: ['UnknownCoverage', 'MissingRiskPlan'], severity: 'Medium', severityRank: 4,
      distanceAbs: 3.8, distancePct: 10.5,
    }),
  ];
}

const journal: JournalRow[] = [
  {
    key: 'ep:1', symbol: 'ABC.TO', account: 'TFSA_D_TD', episodeId: 'e1', episodeResolution: 'Persisted', openDate: '2026-06-01T00:00:00', finalCloseDate: '2026-06-30T00:00:00',
    daysHeld: 29, closedQuantity: 100, entryPrice: 10, exitPrice: 11.4, realizedPnl: 140, realizedPnlPct: 14, currency: 'CAD', originalDecisionSource: 'Catalyst',
    closingDecisionSource: 'Risk Control', thesis: 'Earnings catalyst', notes: 'Took profits into strength', outcome: 'Win', episodeStillOpen: false,
    riskHistory: [{ occurredAtUtc: '2026-06-02T14:00:00Z', eventType: 'PlanCreated', summary: 'PlannedStopPrice: — → 9.20' }], lotIds: [1, 2],
  },
  {
    key: 'ep:2', symbol: 'ABC.TO', account: 'TFSA_D_TD', episodeId: 'e2', episodeResolution: 'Persisted', openDate: '2026-08-01T00:00:00', finalCloseDate: '2026-08-10T00:00:00',
    daysHeld: 9, closedQuantity: 100, entryPrice: 20, exitPrice: 18.5, realizedPnl: -150, realizedPnlPct: -7.5, currency: 'CAD', originalDecisionSource: 'App Signal',
    closingDecisionSource: 'Loss Harvest', thesis: null, notes: '', outcome: 'Loss', episodeStillOpen: false, riskHistory: [], lotIds: [3],
  },
  {
    key: 'lot:4', symbol: 'UNL.TO', account: 'Corp_TD', episodeId: null, episodeResolution: 'Unresolved', openDate: '2026-05-01T00:00:00', finalCloseDate: '2026-05-05T00:00:00',
    daysHeld: 4, closedQuantity: 10, entryPrice: 5, exitPrice: 6, realizedPnl: 10, realizedPnlPct: 20, currency: 'CAD', originalDecisionSource: 'Manual',
    closingDecisionSource: null, thesis: null, notes: '', outcome: 'Win', episodeStillOpen: false, riskHistory: [], lotIds: [5],
  },
];

interface SwingState {
  rows: SwingTradeRow[];
  plans: Record<string, RiskPlan>;
  history: Record<string, RiskAuditEvent[]>;
  nextEvent: number;
  nextStop: number;
  /** When set, the next write answers 409 once (simulates another writer). */
  conflictNext: boolean;
}

export const swingState: SwingState = { rows: [], plans: {}, history: {}, nextEvent: 1, nextStop: 100, conflictNext: false };

export function resetSwing(): void {
  retired.length = 0;
  swingState.rows = buildRows();
  swingState.plans = {};
  for (const r of swingState.rows) {
    if (r.plannedStop === null) continue;
    swingState.plans[keyOf(r.account, r.symbol)] = {
      id: swingState.rows.indexOf(r) + 1, accountType: r.account, symbol: r.symbol, plannedStopPrice: r.plannedStop, targetPrice: r.targetPrice,
      stopType: r.stopType ?? 'Fixed', stopRationale: null, tradeRationale: null, riskNotes: null, currency: r.currency,
      episodeOpenDate: r.episodeOpenDate, belongsToCurrentEpisode: true, updatedAtUtc: NOW, rowVersion: `seed${r.symbol}`,
    };
  }
  swingState.history = {};
  swingState.nextEvent = 1;
  swingState.nextStop = 100;
  swingState.conflictNext = false;
}

export const swingFinalActions = (): unknown[] =>
  swingState.rows.map((r, i) => ({
    symbol: r.symbol, source: 'Portfolio', itemId: r.lotIds[0], role: 'Swing', finalAction: i % 2 === 0 ? 'Hold Swing' : 'Hold / Trail Stop',
    finalActionId: 'HOLD', category: 'Portfolio', direction: 'Neutral', severity: 'hold', priority: 'INFORMATIONAL', finalActionClass: 'fa-hold',
    trendSetup: 'Constructive', momentumShift: 'Uptrend', baseAction: 'Hold', gapStatus: 'None', hoverDescription: 'Hold', reason: null,
    hasHardStructuralNegative: false, analysisPrice: r.currentPrice ?? 0, analysisTicker: r.symbol, analysisCurrency: 'CAD', trendSetupReason: '',
    momentumShiftReason: '', trendSetupClass: '', momentumShiftClass: '', gapScore: 0, gapPct: 0, reasonCodes: [], isActionable: false,
  }));

export const openTradesResponse = () => {
  const rows = [...swingState.rows].sort((a, b) => a.severityRank - b.severityRank);
  const totals = rows.reduce<Record<string, { mv: number; pnl: number }>>((acc, r) => {
    if (r.marketValue === null) return acc;
    const t = (acc[r.currency] ??= { mv: 0, pnl: 0 });
    t.mv += r.marketValue;
    t.pnl += r.unrealizedPnl ?? 0;
    return acc;
  }, {});
  return {
    brokerOrderReminders: retired.filter(({ stop }) => stop.brokerCancellationUnverified || stop.isActive)
      .map(({ key, stop }) => ({ account: key.split('|')[0], symbol: key.split('|')[1], stop })),
    summary: {
      openTrades: rows.length,
      needsAttention: rows.filter((r) => r.needsAttention).length,
      totals: Object.entries(totals).map(([currency, t]) => ({
        currency, marketValue: t.mv, unrealizedPnl: t.pnl, unrealizedPnlPct: Math.round((t.pnl / (t.mv - t.pnl)) * 10000) / 100,
      })),
      asOfUtc: NOW, anyStale: rows.some((r) => r.priceBasis === 'Stale'), anyUnavailable: false,
    },
    rows,
    approachingThresholdPct: 5,
    thresholdNote: 'Advisory alert threshold in Portfolio Manager only. It is not a broker order level and does not place or change any order.',
  };
};

export const journalResponse = () => journal;

const keyOf = (account: string, symbol: string) => `${account}|${symbol}`;

function positionFor(account: string, symbol: string): RiskPosition | null {
  const r = swingState.rows.find((x) => x.account === account && x.symbol === symbol);
  if (!r) return null;
  const k = keyOf(account, symbol);
  const plan = swingState.plans[k] ?? null;
  const stops = [...r.brokerStops, ...retired.filter((s) => s.key === k).map((s) => s.stop)];
  return {
    accountType: account, symbol, hasOpenPosition: true, totalQuantity: r.totalQuantity, swingQuantity: r.swingQuantity, mixedRole: r.mixedRole,
    episodeResolution: r.episodeResolution, episodeNote: r.episodeNote, episodeOpenDate: r.episodeOpenDate, currency: r.currency, currencyBasis: r.currencyBasis,
    plan, brokerStops: stops, history: swingState.history[k] ?? [],
    facts: {
      averageCost: r.averageCost, swingAverageCost: r.averageCost, currentPrice: r.currentPrice,
      priceBasis: r.priceBasis, priceAsOfUtc: r.priceAsOfUtc,
      marketValue: r.currentPrice === null ? null : r.currentPrice * r.totalQuantity,
      unrealizedPnl: r.currentPrice === null ? null : (r.currentPrice - r.averageCost) * r.totalQuantity,
      unrealizedPnlPct: r.unrealizedPnlPct, swingMarketValue: r.marketValue, swingUnrealizedPnl: r.unrealizedPnl,
    },
  };
}

const retired: { key: string; stop: BrokerStop }[] = [];

function logEvent(k: string, eventType: string, changes: { field: string; old: string | null; new: string | null }[], reason: string | null, stopId: number | null = null) {
  (swingState.history[k] ??= []).unshift({
    id: swingState.nextEvent++, occurredAtUtc: NOW, userName: 'e2e', source: 'User', eventType, changes, reason, brokerStopRecordId: stopId,
  });
}

const bad = (message: string) => ({ status: 400, body: { error: message } });
const fmt = (n: number | null) => (n === null ? null : String(n));

function recompute(r: SwingTradeRow): void {
  const active = r.brokerStops.filter((s) => s.isActive);
  r.brokerStopTrigger = active[0]?.triggerPrice ?? null;
  r.brokerOrderType = active[0]?.orderType ?? null;
  r.brokerStatus = active[0]?.userStatus ?? null;
  const known = active.length > 0 && active.every((s) => s.coveredQuantity !== null);
  const covered = known ? active.reduce((s, x) => s + (x.coveredQuantity ?? 0), 0) : null;
  r.coveredQuantity = covered;
  r.coverageState = active.length === 0 ? 'None' : r.mixedRole ? 'Ambiguous' : !known ? 'Unknown' : covered! > r.totalQuantity ? 'OverCovered' : covered === r.totalQuantity ? 'Full' : 'Partial';
  r.protection = active.length === 0 ? (r.plannedStop !== null ? 'Alert-Only Stop (no broker order)' : 'No Stop') : r.coverageState === 'Partial' ? 'Partially Covered' : 'Broker Stop Recorded';
}

export function handleRisk(req: MockRequest): unknown {
  const { method, path, body } = req;
  const b = (body ?? {}) as Record<string, unknown>;

  if (method === 'GET' && path === '/api/risk/position') {
    const p = positionFor(req.query.get('account') ?? '', req.query.get('symbol') ?? '');
    return p ?? { status: 404, body: { error: 'No open position in this account for that symbol.' } };
  }

  if (swingState.conflictNext && method !== 'GET') {
    swingState.conflictNext = false;
    return { status: 409, body: { error: 'The record was changed by someone else. Reload and review before saving again.' } };
  }

  const account = String(b['accountType'] ?? '');
  const symbol = String(b['symbol'] ?? '');
  const r = swingState.rows.find((x) => x.account === account && x.symbol === symbol);
  const k = keyOf(account, symbol);

  if (method === 'PUT' && path === '/api/risk/plan') {
    if (!r) return { status: 404, body: { error: 'No open position in this account for that symbol.' } };
    const stopPrice = b['plannedStopPrice'] as number | null;
    if (stopPrice !== null && !(stopPrice > 0)) return bad('Planned stop must be a positive price.');
    const old = swingState.plans[k];
    const plan: RiskPlan = {
      id: old?.id ?? 1, accountType: account, symbol, plannedStopPrice: stopPrice, targetPrice: (b['targetPrice'] as number | null) ?? null,
      stopType: (b['stopType'] as RiskPlan['stopType']) ?? 'Fixed', stopRationale: (b['stopRationale'] as string | null) ?? null,
      tradeRationale: (b['tradeRationale'] as string | null) ?? null, riskNotes: (b['riskNotes'] as string | null) ?? null, currency: r.currency,
      episodeOpenDate: r.episodeOpenDate, belongsToCurrentEpisode: true, updatedAtUtc: NOW, rowVersion: `p${swingState.nextEvent}`,
    };
    logEvent(k, old ? 'PlanUpdated' : 'PlanCreated', [{ field: 'PlannedStopPrice', old: fmt(old?.plannedStopPrice ?? null), new: fmt(stopPrice) }], (b['reason'] as string | null) ?? null);
    swingState.plans[k] = plan;
    r.plannedStop = plan.plannedStopPrice;
    r.targetPrice = plan.targetPrice;
    r.stopType = plan.stopType;
    recompute(r);
    return positionFor(account, symbol);
  }

  if (method === 'POST' && path === '/api/risk/broker-stops') {
    if (!r) return { status: 404, body: { error: 'No open position in this account for that symbol.' } };
    const trigger = b['triggerPrice'] as number | null;
    if (trigger === null || !(trigger > 0)) return bad('Trigger price must be a positive price.');
    const covered = (b['coveredQuantity'] as number | null) ?? null;
    if (covered !== null && covered > r.totalQuantity && !(b['allowOverCoverage'] && b['reason'])) return bad('Covered quantity exceeds the current account quantity.');
    const s = stop(swingState.nextStop++, trigger, { coveredQuantity: covered, confirmationSource: 'ManualEntry', notes: (b['notes'] as string | null) ?? null, orderType: (b['orderType'] as BrokerStop['orderType']) ?? 'StopMarket' });
    r.brokerStops = [s, ...r.brokerStops];
    logEvent(k, 'BrokerStopRecorded', [{ field: 'TriggerPrice', old: null, new: String(trigger) }], (b['reason'] as string | null) ?? null, s.id);
    recompute(r);
    return positionFor(account, symbol);
  }

  const upd = /^\/api\/risk\/broker-stops\/(\d+)$/.exec(path);
  if (method === 'PUT' && upd && r) {
    const s = r.brokerStops.find((x) => x.id === Number(upd[1]));
    if (!s) return { status: 404, body: { error: 'Broker stop record not found.' } };
    const trigger = b['triggerPrice'] as number | null;
    if (trigger === null || !(trigger > 0)) return bad('Trigger price must be a positive price.');
    const changes = [
      { field: 'TriggerPrice', old: String(s.triggerPrice), new: String(trigger) },
      { field: 'CoveredQuantity', old: fmt(s.coveredQuantity), new: fmt((b['coveredQuantity'] as number | null) ?? null) },
    ].filter((c) => c.old !== c.new);
    s.triggerPrice = trigger;
    s.coveredQuantity = (b['coveredQuantity'] as number | null) ?? null;
    s.rowVersion = `${s.rowVersion}+`;
    logEvent(k, changes.length === 1 && changes[0].field === 'CoveredQuantity' ? 'CoverageAdjusted' : 'BrokerStopUpdated', changes, (b['reason'] as string | null) ?? null, s.id);
    recompute(r);
    return positionFor(account, symbol);
  }

  const tr = /^\/api\/risk\/broker-stops\/(\d+)\/status$/.exec(path);
  if (method === 'POST' && tr) {
    const previous = retired.find((entry) => entry.stop.id === Number(tr[1]));
    const previousRow = previous && swingState.rows.find((row) => keyOf(row.account, row.symbol) === previous.key);
    const target = swingState.rows.flatMap((x) => x.brokerStops.map((s) => ({ x, s }))).find((t) => t.s.id === Number(tr[1]))
      ?? (previous && previousRow ? { x: previousRow, s: previous.stop } : undefined);
    if (!target) return { status: 404, body: { error: 'Broker stop record not found.' } };
    if (!String(b['reason'] ?? '').trim()) return bad('A broker confirmation reason is required.');
    if (b['rowVersion'] !== target.s.rowVersion) return { status: 409, body: { error: 'Reload the broker record before saving.' } };
    const status = String(b['status']) as BrokerStop['userStatus'];
    const kk = keyOf(target.x.account, target.x.symbol);
    logEvent(kk, 'BrokerStatusChanged', [{ field: 'UserStatus', old: target.s.userStatus, new: status }], (b['reason'] as string | null) ?? null, target.s.id);
    target.s.userStatus = status;
    target.s.isActive = false;
    target.s.brokerCancellationUnverified = false;
    target.s.rowVersion += '+';
    target.x.brokerStops = target.x.brokerStops.filter((s) => s.id !== target.s.id);
    if (!previous) retired.push({ key: kk, stop: target.s });
    recompute(target.x);
    return positionFor(target.x.account, target.x.symbol);
  }

  return { status: 404, body: { error: 'Unhandled mock route' } };
}

export function clearRetired(): void {
  retired.length = 0;
}

/** Simulates "the trade closed and was reopened": the old order is retired (not cancelled at the broker) and not active now. */
export function retireStopsAsPreviousTrade(account: string, symbol: string): void {
  const r = swingState.rows.find((x) => x.account === account && x.symbol === symbol);
  if (!r) return;
  for (const s of r.brokerStops) {
    retired.push({
      key: keyOf(account, symbol),
      stop: { ...s, isActive: false, userStatus: 'EpisodeClosed', brokerCancellationUnverified: true, belongsToCurrentEpisode: false },
    });
  }
  r.brokerStops = [];
  recompute(r);
}
