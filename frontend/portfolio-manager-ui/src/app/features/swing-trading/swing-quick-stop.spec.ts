import { BrokerOrderReminder, BrokerStop } from '../../core/models/swing-trading.models';
import { groupReminders, overCoverageMessage, selfConfirmationLabel, validateQuickStopLimit } from './swing-trading-logic';

const ctx = { totalQuantity: 1000, otherCovered: 0, currentPrice: 18, priceUsable: true };
const ok = { triggerPrice: 16, limitPrice: 15.8, coveredQuantity: 1000, confirmed: true };

const stop = (over: Partial<BrokerStop> = {}): BrokerStop => ({
  id: 1, orderIdentity: 'o', orderType: 'StopLimit', triggerPrice: 16, limitPrice: 15.8, currency: 'CAD',
  userStatus: 'EpisodeClosed', brokerVerificationStatus: 'Unverified', coveredQuantity: 100, placedDate: null,
  brokerOrderRef: null, lastUserVerifiedDate: null, confirmationSource: 'ManualEntry', notes: null,
  episodeOpenDate: '2026-09-01T00:00:00', isActive: false, belongsToCurrentEpisode: false,
  brokerCancellationUnverified: true, replacedByRecordId: null, updatedAtUtc: '2026-09-20T00:00:00Z', rowVersion: 'v', ...over,
});

describe('validateQuickStopLimit', () => {
  it('accepts a standard long-position sell stop-limit with the confirmation', () => {
    expect(validateQuickStopLimit(ok, ctx).errors).toEqual({});
  });

  it('requires the confirmation checkbox', () => {
    expect(validateQuickStopLimit({ ...ok, confirmed: false }, ctx).errors['confirmed']).toBeDefined();
  });

  it('rejects a limit above the trigger, missing limit, and non-positive values', () => {
    expect(validateQuickStopLimit({ ...ok, limitPrice: 16.5 }, ctx).errors['limitPrice']).toContain('must not exceed');
    expect(validateQuickStopLimit({ ...ok, limitPrice: null }, ctx).errors['limitPrice']).toBeDefined();
    expect(validateQuickStopLimit({ ...ok, limitPrice: 0 }, ctx).errors['limitPrice']).toBeDefined();
    expect(validateQuickStopLimit({ ...ok, triggerPrice: 0 }, ctx).errors['triggerPrice']).toBeDefined();
    expect(validateQuickStopLimit({ ...ok, coveredQuantity: 0 }, ctx).errors['coveredQuantity']).toBeDefined();
    expect(validateQuickStopLimit({ ...ok, coveredQuantity: null }, ctx).errors['coveredQuantity']).toBeDefined();
  });

  it('rejects shares above the open quantity and combined coverage above it', () => {
    expect(validateQuickStopLimit({ ...ok, coveredQuantity: 1001 }, ctx).errors['coveredQuantity']).toBeDefined();
    expect(validateQuickStopLimit({ ...ok, coveredQuantity: 600 }, { ...ctx, otherCovered: 500 }).errors['coveredQuantity']).toContain('Combined');
    expect(validateQuickStopLimit({ ...ok, coveredQuantity: 500 }, { ...ctx, otherCovered: 500 }).errors).toEqual({});
  });
});

describe('PM-032 helpers', () => {
  it('formats the partial-close mismatch message exactly', () => {
    expect(overCoverageMessage(1500, 1000)).toBe('Order covers 1,500 shares; 1,000 remain open. Verify or amend at your broker.');
  });

  it('never reports an order without a user confirmation as self-confirmed', () => {
    expect(selfConfirmationLabel(stop())).toBe('Not self-confirmed');
    expect(selfConfirmationLabel(stop({ lastUserVerifiedDate: '2026-11-11T10:00:00Z' }))).toBe('Self-confirmed (not verified)');
    expect(selfConfirmationLabel(stop({ brokerVerificationStatus: 'Verified' }))).toBe('Broker-verified');
  });

  it('groups per-record reminders by account, symbol and episode', () => {
    const r = (id: number, account: string, episode: string): BrokerOrderReminder => ({
      account, symbol: 'ABC.TO', stop: stop({ id, episodeOpenDate: episode }),
    });
    const groups = groupReminders([r(1, 'A', '2026-09-01T00:00:00'), r(2, 'A', '2026-09-01T00:00:00'), r(3, 'B', '2026-09-01T00:00:00'), r(4, 'A', '2026-10-15T00:00:00')]);
    expect(groups.map((g) => g.orders.map((o) => o.id))).toEqual([[1, 2], [3], [4]]);
  });
});
