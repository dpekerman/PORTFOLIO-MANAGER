import {
  BrokerStop,
  CoverageState,
  JournalRow,
  RiskPosition,
  SwingTradeRow,
} from '../../core/models/swing-trading.models';
import {
  EMPTY_SWING_FILTERS,
  JOURNAL_EXPORT_COLUMNS,
  OPEN_TRADES_EXPORT_COLUMNS,
  filterSwingRows,
  hasErrors,
  journalExportRows,
  openTradesExportRows,
  summarizeCoverage,
  validateBrokerStop,
  validateRiskPlan,
} from './swing-trading-logic';
import { txRowsToCsv } from '../transactions/transactions-logic';

function row(over: Partial<SwingTradeRow> = {}): SwingTradeRow {
  return {
    key: 'TFSA_D_TD|NOVO.TO',
    symbol: 'NOVO.TO',
    companyName: 'Novo Nordisk A/S',
    account: 'TFSA_D_TD',
    anchorItemId: 639,
    lotIds: [639],
    decisionSource: 'Manual',
    episodeOpenDate: '2026-10-15T00:00:00',
    firstPurchaseDate: '2026-10-15T00:00:00',
    lastExecutionDate: '2026-10-15T00:00:00',
    daysHeld: 13,
    episodeResolution: 'Derived',
    episodeNote: null,
    swingQuantity: 600,
    totalQuantity: 600,
    mixedRole: false,
    averageCost: 17,
    currency: 'CAD',
    currencyBasis: 'AssumedFromSuffix',
    currentPrice: 18,
    priceAsOfUtc: '2026-10-28T20:00:00Z',
    priceSessionDate: '2026-10-28',
    priceBasis: 'EOD',
    priceSource: 'test',
    marketValue: 10800,
    unrealizedPnl: 600,
    unrealizedPnlPct: 5.88,
    plannedStop: null,
    targetPrice: null,
    stopType: null,
    planFromPriorEpisode: false,
    brokerStopTrigger: 16.2,
    brokerOrderType: 'StopMarket',
    brokerStatus: 'UserConfirmedEstablished',
    brokerVerification: 'Unverified',
    brokerStops: [],
    coveredQuantity: null,
    coverageState: 'Unknown',
    protection: 'Broker Stop Recorded',
    riskStatuses: ['UnknownCoverage', 'MissingRiskPlan'],
    severity: 'Medium',
    severityRank: 4,
    needsAttention: true,
    distanceAbs: 1.8,
    distancePct: 10,
    governingStopSource: 'Broker stop (user-recorded)',
    observation: null,
    plannedStopState: 'NoPlan',
    ...over,
  };
}

const fa = (r: SwingTradeRow): string | null => (r.symbol === 'NOVO.TO' ? 'Hold Swing' : 'Buy / Accumulate');

describe('Breached Planned Stops filter', () => {
  const rows = [
    row({ key: 'A', symbol: 'AAA', account: 'TFSA_D_TD', plannedStopState: 'Breached', riskStatuses: ['StopBreached'] }),
    row({ key: 'B', symbol: 'BBB', account: 'Corp_TD', plannedStopState: 'Breached', riskStatuses: ['StopBreached'] }),
    // broker-stop-only breach: StopBreached status but the Planned Stop is not breached
    row({ key: 'C', symbol: 'CCC', plannedStopState: 'Clear', riskStatuses: ['StopBreached'] }),
    row({ key: 'D', symbol: 'DDD', plannedStopState: 'Unverified', priceBasis: 'Stale', riskStatuses: ['StaleEvidence'] }),
    row({ key: 'E', symbol: 'EEE', plannedStopState: 'NoPlan' }),
  ];
  const on = { ...EMPTY_SWING_FILTERS, plannedStopBreached: true };

  it('uses only the backend PlannedStopState: broker-only breaches, stale and no-plan rows are excluded', () => {
    expect(filterSwingRows(rows, on, fa).map((r) => r.symbol)).toEqual(['AAA', 'BBB']);
  });

  it('honours the other filters', () => {
    expect(filterSwingRows(rows, { ...on, account: 'Corp_TD' }, fa).map((r) => r.symbol)).toEqual(['BBB']);
    expect(filterSwingRows(rows, { ...on, search: 'aaa' }, fa).map((r) => r.symbol)).toEqual(['AAA']);
    expect(filterSwingRows(rows, { ...on, account: 'Nope' }, fa)).toEqual([]);
  });

  it('is off by default', () => {
    expect(filterSwingRows(rows, EMPTY_SWING_FILTERS, fa)).toHaveLength(5);
  });
});

describe('filterSwingRows', () => {
  const rows = [
    row(),
    row({ key: 'B', symbol: 'DIS.TO', companyName: 'WALT DISNEY CDR', account: 'TFSA_L_TD', protection: 'No Stop', needsAttention: false, riskStatuses: ['StopBreached'] }),
  ];

  it('returns everything with empty filters', () => {
    expect(filterSwingRows(rows, EMPTY_SWING_FILTERS, fa)).toHaveLength(2);
  });

  it('filters by account, search text, protection, risk status, final action and attention', () => {
    expect(filterSwingRows(rows, { ...EMPTY_SWING_FILTERS, account: 'TFSA_L_TD' }, fa).map((r) => r.symbol)).toEqual(['DIS.TO']);
    expect(filterSwingRows(rows, { ...EMPTY_SWING_FILTERS, search: 'disney' }, fa).map((r) => r.symbol)).toEqual(['DIS.TO']);
    expect(filterSwingRows(rows, { ...EMPTY_SWING_FILTERS, protection: 'No Stop' }, fa).map((r) => r.symbol)).toEqual(['DIS.TO']);
    expect(filterSwingRows(rows, { ...EMPTY_SWING_FILTERS, risk: 'StopBreached' }, fa).map((r) => r.symbol)).toEqual(['DIS.TO']);
    expect(filterSwingRows(rows, { ...EMPTY_SWING_FILTERS, finalAction: 'Hold Swing' }, fa).map((r) => r.symbol)).toEqual(['NOVO.TO']);
    expect(filterSwingRows(rows, { ...EMPTY_SWING_FILTERS, attentionOnly: true }, fa).map((r) => r.symbol)).toEqual(['NOVO.TO']);
  });

  it('combines filters', () => {
    expect(filterSwingRows(rows, { ...EMPTY_SWING_FILTERS, account: 'TFSA_L_TD', attentionOnly: true }, fa)).toHaveLength(0);
  });
});

describe('validateRiskPlan', () => {
  const ctx = { currentPrice: 18, priceUsable: true };

  it('rejects non-positive and over-precise prices', () => {
    expect(hasErrors(validateRiskPlan({ plannedStopPrice: -1, targetPrice: null, stopType: 'Fixed' }, ctx))).toBe(true);
    expect(hasErrors(validateRiskPlan({ plannedStopPrice: 0, targetPrice: null, stopType: 'Fixed' }, ctx))).toBe(true);
    expect(hasErrors(validateRiskPlan({ plannedStopPrice: 10.12345, targetPrice: null, stopType: 'Fixed' }, ctx))).toBe(true);
    expect(hasErrors(validateRiskPlan({ plannedStopPrice: 10.1234, targetPrice: 20, stopType: 'Fixed' }, ctx))).toBe(false);
  });

  it('treats both prices as optional', () => {
    expect(hasErrors(validateRiskPlan({ plannedStopPrice: null, targetPrice: null, stopType: 'Fixed' }, ctx))).toBe(false);
  });

  it('warns, without blocking, when a long stop is at or above the latest valid price', () => {
    const r = validateRiskPlan({ plannedStopPrice: 18, targetPrice: null, stopType: 'Fixed' }, ctx);
    expect(hasErrors(r)).toBe(false);
    expect(r.warnings.join(' ')).toContain('at or above the latest price');
  });

  it('does not compare with a price that is stale or unavailable', () => {
    const r = validateRiskPlan({ plannedStopPrice: 25, targetPrice: null, stopType: 'Fixed' }, { currentPrice: 18, priceUsable: false });
    expect(r.warnings).toHaveLength(0);
  });

  it('says a trailing stop is a label only', () => {
    expect(validateRiskPlan({ plannedStopPrice: 10, targetPrice: null, stopType: 'Trailing' }, ctx).warnings.join(' ')).toContain('label only');
  });
});

describe('validateBrokerStop', () => {
  const ctx = { totalQuantity: 600, currentPrice: 18, priceUsable: true };
  const base = { orderType: 'StopMarket' as const, triggerPrice: 16.2, limitPrice: null, coveredQuantity: null, allowOverCoverage: false, reason: '' };

  it('requires a valid trigger', () => {
    expect(hasErrors(validateBrokerStop({ ...base, triggerPrice: null }, ctx))).toBe(true);
    expect(hasErrors(validateBrokerStop({ ...base, triggerPrice: -2 }, ctx))).toBe(true);
    expect(hasErrors(validateBrokerStop(base, ctx))).toBe(false);
  });

  it('leaves covered quantity unknown when blank and rejects zero', () => {
    expect(hasErrors(validateBrokerStop({ ...base, coveredQuantity: null }, ctx))).toBe(false);
    expect(validateBrokerStop({ ...base, coveredQuantity: 0 }, ctx).errors['coveredQuantity']).toBeTruthy();
  });

  it('blocks covered quantity above the holding unless an unusual case is explained', () => {
    expect(validateBrokerStop({ ...base, coveredQuantity: 700 }, ctx).errors['coveredQuantity']).toBeTruthy();
    expect(hasErrors(validateBrokerStop({ ...base, coveredQuantity: 700, allowOverCoverage: true, reason: 'broker rounding' }, ctx))).toBe(false);
    expect(validateBrokerStop({ ...base, coveredQuantity: 700, allowOverCoverage: true, reason: '' }, ctx).errors['coveredQuantity']).toBeTruthy();
  });

  it('validates the limit price only for stop-limit orders', () => {
    expect(hasErrors(validateBrokerStop({ ...base, limitPrice: -1 }, ctx))).toBe(false);
    expect(hasErrors(validateBrokerStop({ ...base, orderType: 'StopLimit', limitPrice: -1 }, ctx))).toBe(true);
  });

  it('warns when the trigger is at or above the price', () => {
    expect(validateBrokerStop({ ...base, triggerPrice: 19 }, ctx).warnings.join(' ')).toContain('trigger immediately');
  });
});

describe('summarizeCoverage', () => {
  const stop = (over: Partial<BrokerStop> = {}): BrokerStop => ({
    id: 1, orderIdentity: 'x', orderType: 'StopMarket', triggerPrice: 16.2, limitPrice: null, currency: 'CAD',
    userStatus: 'UserConfirmedEstablished', brokerVerificationStatus: 'Unverified', coveredQuantity: null, placedDate: null,
    brokerOrderRef: null, lastUserVerifiedDate: null, confirmationSource: 'UserSuppliedStopSheet', notes: null,
    episodeOpenDate: null, isActive: true, belongsToCurrentEpisode: true, brokerCancellationUnverified: false,
    replacedByRecordId: null, updatedAtUtc: '', rowVersion: '', ...over,
  });
  const pos = (stops: BrokerStop[], over: Partial<RiskPosition> = {}): RiskPosition => ({
    accountType: 'A', symbol: 'X', hasOpenPosition: true, totalQuantity: 100, swingQuantity: 100, mixedRole: false,
    episodeResolution: 'Derived', episodeNote: null, episodeOpenDate: null, currency: 'CAD', currencyBasis: 'x',
    plan: null, brokerStops: stops, history: [], ...over,
  });
  const state = (p: RiskPosition): CoverageState => summarizeCoverage(p).state;

  it('is None without an active stop of the current trade', () => {
    expect(state(pos([]))).toBe('None');
    expect(state(pos([stop({ belongsToCurrentEpisode: false })]))).toBe('None');
    expect(state(pos([stop({ isActive: false })]))).toBe('None');
  });

  it('is Unknown (never zero or full) when any quantity is not provided', () => {
    const s = summarizeCoverage(pos([stop()]));
    expect(s.state).toBe('Unknown');
    expect(s.covered).toBeNull();
  });

  it('distinguishes partial, full and over-covered, and ambiguous for mixed roles', () => {
    expect(state(pos([stop({ coveredQuantity: 40 })]))).toBe('Partial');
    expect(state(pos([stop({ coveredQuantity: 60 }), stop({ id: 2, coveredQuantity: 40 })]))).toBe('Full');
    expect(state(pos([stop({ coveredQuantity: 150 })]))).toBe('OverCovered');
    expect(state(pos([stop({ coveredQuantity: 100 })], { mixedRole: true, totalQuantity: 400 }))).toBe('Ambiguous');
  });
});

describe('exports', () => {
  it('exports unknown coverage as blank with an explicit status, never as zero', () => {
    const csv = txRowsToCsv(OPEN_TRADES_EXPORT_COLUMNS, openTradesExportRows([row()], fa, 5));
    const [header, line] = csv.split('\n');
    const headers = header.split(',');
    const cells = line.split(',');
    expect(cells[headers.indexOf('Covered Quantity')]).toBe('');
    expect(cells[headers.indexOf('Coverage Status')]).toBe('Unknown');
    expect(cells[headers.indexOf('Broker Stop Trigger')]).toBe('16.20');
    expect(cells[headers.indexOf('Planned Stop')]).toBe('');
    expect(cells[headers.indexOf('Final Action')]).toBe('Hold Swing');
    expect(cells[headers.length - 1]).toBe('CAD');
  });

  it('keeps the filtered dataset: one export row per supplied row', () => {
    expect(openTradesExportRows([row(), row({ symbol: 'DIS.TO' })], fa, 5)).toHaveLength(2);
    expect(openTradesExportRows([], fa, 5)).toHaveLength(0);
  });

  it('exports the journal with numeric P/L and blank nulls', () => {
    const j: JournalRow = {
      key: 'ep:1', symbol: 'ABC.TO', account: 'A', episodeId: 'e1', episodeResolution: 'Persisted', openDate: '2026-06-01T00:00:00',
      finalCloseDate: '2026-06-30T00:00:00', daysHeld: 29, closedQuantity: 100, entryPrice: 10, exitPrice: null, realizedPnl: null,
      realizedPnlPct: null, currency: 'CAD', originalDecisionSource: 'Catalyst', closingDecisionSource: null, thesis: null, notes: 'a, "b"',
      outcome: 'Unknown', episodeStillOpen: false, riskHistory: [], lotIds: [1],
    };
    const csv = txRowsToCsv(JOURNAL_EXPORT_COLUMNS, journalExportRows([j]));
    const line = csv.split('\n')[1];
    expect(line).toContain('2026-06-01,2026-06-30,29,100,10.00,,,,');
    expect(line).toContain('"a, ""b"""');
  });
});
