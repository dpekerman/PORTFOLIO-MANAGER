import { BrokerStop, RiskStatus } from '../../core/models/swing-trading.models';
import { brokerStopView, plannedStopView, priceView, protectionView, riskView } from './swing-presenters';
import { swingRow } from './swing-row.testing';

const money = (n: number | null) => (n === null ? '—' : n.toFixed(2));
const stop = (over: Partial<BrokerStop> = {}): BrokerStop => ({ brokerVerificationStatus: 'Unverified', ...over }) as BrokerStop;

describe('plannedStopView (maps the backend PlannedStopState; never compares prices)', () => {
  it('Breached shows price, planned stop, timestamp and does not claim a broker execution', () => {
    const v = plannedStopView(swingRow({ plannedStopState: 'Breached' }), money);
    expect(v.text).toBe('Breached');
    expect(v.icon).toBe('error');
    expect(v.detail).toContain('Planned Stop 11.85');
    expect(v.detail).toContain('11.70');
    expect(v.detail).toContain('2026-10-28 20:00 UTC');
    expect(v.detail).toContain('BREACHED');
    expect(v.detail).toContain('does not show that a broker order triggered or filled');
  });

  it('is driven only by the state: a price above the stop still renders Breached if the backend says so', () => {
    const v = plannedStopView(swingRow({ plannedStopState: 'Breached', currentPrice: 99 }), money);
    expect(v.state).toBe('Breached');
  });

  it('Breached with a broker stop recorded keeps the same non-execution wording', () => {
    const v = plannedStopView(swingRow({ plannedStopState: 'Breached', brokerStops: [stop()], brokerStopTrigger: 11.5 }), money);
    expect(v.detail.toLowerCase()).not.toContain('executed');
    expect(v.detail).toContain('does not show that a broker order triggered or filled');
  });

  it('Unverified is a stale/unavailable price, not a breach and not "clear"', () => {
    const v = plannedStopView(swingRow({ plannedStopState: 'Unverified', priceBasis: 'Stale' }), money);
    expect(v.text).toBe('Unverified');
    expect(v.icon).toBe('schedule');
    expect(v.detail).toContain('stale');
    expect(v.detail).toContain('last known 11.70 as of 2026-10-28 20:00 UTC');
    expect(v.detail).toContain('neither confirmed nor ruled out');
    const none = plannedStopView(swingRow({ plannedStopState: 'Unverified', priceBasis: 'Unavailable', currentPrice: null, priceAsOfUtc: null }), money);
    expect(none.detail).toContain('unavailable');
    expect(none.detail).not.toContain('last known');
  });

  it('Clear and NoPlan show no warning', () => {
    expect(plannedStopView(swingRow({ plannedStopState: 'Clear' }), money)).toMatchObject({ text: null, icon: null });
    expect(plannedStopView(swingRow({ plannedStopState: 'NoPlan', plannedStop: null }), money)).toMatchObject({ text: null, icon: null });
  });
});

describe('priceView', () => {
  it('moves EOD basis, as-of time and source into the tooltip with no visible chip', () => {
    const v = priceView(swingRow());
    expect(v.warning).toBeNull();
    expect(v.tooltip).toContain('End-of-day close (not a live intraday quote)');
    expect(v.tooltip).toContain('2026-10-28 20:00 UTC');
    expect(v.tooltip).toContain('test source');
  });

  it('keeps a concise visible warning for stale or unavailable prices', () => {
    expect(priceView(swingRow({ priceBasis: 'Stale' })).warning).toBe('Stale');
    expect(priceView(swingRow({ priceBasis: 'Unavailable', currentPrice: null, priceAsOfUtc: null })).warning).toBe('No price');
  });
});

describe('brokerStopView', () => {
  it('distinguishes planned-only from missing', () => {
    expect(brokerStopView(swingRow({ plannedStop: 11.85 })).label).toBe('Planned only');
    expect(brokerStopView(swingRow({ plannedStop: null })).label).toBe('Missing');
  });

  it('never presents an unverified user-recorded stop as verified', () => {
    const base = { brokerStops: [stop()], coverageState: 'Full' as const, coveredQuantity: 600, brokerOrderType: 'StopMarket' as const };
    expect(brokerStopView(swingRow(base)).label).toBe('Recorded (unverified)');
    expect(brokerStopView(swingRow({ ...base, brokerStops: [stop({ brokerVerificationStatus: 'Verified' })] })).label).toBe('Recorded');
  });

  it('keeps partial, unknown, ambiguous and over-covered distinct and accurate', () => {
    const s = { brokerStops: [stop()], brokerOrderType: 'StopMarket' as const };
    expect(brokerStopView(swingRow({ ...s, coverageState: 'Partial', coveredQuantity: 300 })).label).toBe('Partial');
    expect(brokerStopView(swingRow({ ...s, coverageState: 'Unknown' })).label).toBe('Unknown');
    expect(brokerStopView(swingRow({ ...s, coverageState: 'Ambiguous' })).detail).toContain('non-Swing lots');
    expect(brokerStopView(swingRow({ ...s, coverageState: 'OverCovered', coveredQuantity: 900 })).label).toBe('Over-covered');
  });
});

describe('protectionView', () => {
  const s = { brokerStops: [stop()] };

  it('maps coverage to one concise state and keeps covered quantity in the detail', () => {
    expect(protectionView(swingRow({ coverageState: 'None', plannedStop: 11 })).label).toBe('Planned only');
    expect(protectionView(swingRow({ coverageState: 'None', plannedStop: null })).label).toBe('None');
    const partial = protectionView(swingRow({ ...s, coverageState: 'Partial', coveredQuantity: 300 }));
    expect(partial.label).toBe('Partial');
    expect(partial.detail).toContain('Covered quantity 300 of 600');
    const unknown = protectionView(swingRow({ ...s, coverageState: 'Unknown' }));
    expect(unknown.label).toBe('Unknown');
    expect(unknown.detail).toContain('Covered quantity unknown');
    expect(protectionView(swingRow({ ...s, coverageState: 'Ambiguous' })).label).toBe('Ambiguous');
    expect(protectionView(swingRow({ ...s, coverageState: 'OverCovered' })).label).toBe('Over-covered');
  });

  it('Full coverage is only plain "Covered" when the broker verified it', () => {
    const full = { coverageState: 'Full' as const, coveredQuantity: 600 };
    expect(protectionView(swingRow({ ...full, brokerStops: [stop()] })).label).toBe('Covered (unverified)');
    expect(protectionView(swingRow({ ...full, brokerStops: [stop({ brokerVerificationStatus: 'Verified' })] })).label).toBe('Covered');
  });
});

describe('riskView', () => {
  const all: RiskStatus[] = ['MissingRiskPlan', 'UnknownCoverage', 'NoBrokerProtection', 'StopBreached', 'StaleEvidence'];

  it('shows the most material statuses first and keeps every status reachable', () => {
    const v = riskView(swingRow({ riskStatuses: all }));
    expect(v.visible).toEqual(['StopBreached', 'StaleEvidence']);
    expect(v.hidden).toEqual(['NoBrokerProtection', 'UnknownCoverage', 'MissingRiskPlan']);
    expect([...v.visible, ...v.hidden].sort()).toEqual([...all].sort());
  });

  it('does not hide a single status behind "+1"', () => {
    const v = riskView(swingRow({ riskStatuses: ['MissingRiskPlan', 'StopBreached', 'UnknownCoverage'] }));
    expect(v.hidden).toEqual([]);
    expect(v.visible).toHaveLength(3);
  });

  it('handles no statuses', () => {
    expect(riskView(swingRow({ riskStatuses: [] }))).toEqual({ visible: [], hidden: [] });
  });
});
