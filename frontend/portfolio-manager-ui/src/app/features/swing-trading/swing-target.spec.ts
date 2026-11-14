import { epochOrNull } from './swing-trading-logic';
import { targetView } from './swing-presenters';
import { swingRow } from './swing-row.testing';

const money = (n: number | null) => (n === null ? '—' : n.toFixed(2));

describe('targetView (To Target % and Target Reached)', () => {
  it('computes a positive percentage when the price is below target', () => {
    const v = targetView(swingRow({ currentPrice: 10, targetPrice: 12 }), money);
    expect(v.state).toBe('NotReached');
    expect(v.pct).toBeCloseTo(20, 6);
    expect(v.text).toBeNull();
  });

  it('is zero and reached when the price equals the target', () => {
    const v = targetView(swingRow({ currentPrice: 12, targetPrice: 12 }), money);
    expect(v.state).toBe('Reached');
    expect(v.pct).toBe(0);
  });

  it('is negative and reached when the price is above the target, and never claims a sale', () => {
    const v = targetView(swingRow({ currentPrice: 15, targetPrice: 12 }), money);
    expect(v.state).toBe('Reached');
    expect(v.pct).toBeCloseTo(-20, 6);
    expect(v.detail).toContain('does not show that a sell order was placed or filled');
  });

  it('is unverified, with no percentage, for a stale or missing price', () => {
    for (const priceBasis of ['Stale', 'Unavailable'] as const) {
      const v = targetView(swingRow({ currentPrice: 15, targetPrice: 12, priceBasis }), money);
      expect(v.state).toBe('Unverified');
      expect(v.pct).toBeNull();
    }
    expect(targetView(swingRow({ currentPrice: null, targetPrice: 12, priceBasis: 'Unavailable' }), money).pct).toBeNull();
  });

  it('is unavailable for a missing, zero, negative or prior-trade target and never divides by zero', () => {
    for (const targetPrice of [null, 0, -3]) {
      expect(targetView(swingRow({ currentPrice: 10, targetPrice }), money).state).toBe('Unavailable');
    }
    expect(targetView(swingRow({ currentPrice: 10, targetPrice: 12, planFromPriorEpisode: true }), money).state).toBe('Unavailable');
    const zeroPrice = targetView(swingRow({ currentPrice: 0, targetPrice: 12 }), money);
    expect(zeroPrice.pct).toBeNull();
    expect(zeroPrice.state).toBe('Unverified');
  });
});

describe('epochOrNull', () => {
  it('orders dates numerically across month and year boundaries', () => {
    const dates = ['2026-01-05', '2025-12-31', '2026-02-01'];
    expect(dates.sort((a, b) => epochOrNull(b)! - epochOrNull(a)!)).toEqual(['2026-02-01', '2026-01-05', '2025-12-31']);
  });

  it('returns null for missing or invalid dates', () => {
    expect(epochOrNull(null)).toBeNull();
    expect(epochOrNull('')).toBeNull();
    expect(epochOrNull('not a date')).toBeNull();
  });
});
