import { eligibleCloseLotsForSymbol } from './swing-trading-logic';
import { dateSortKey } from '../transactions/transactions-logic';

const lot = (id: number, symbol: string, accountType: string, over: Record<string, unknown> = {}) => ({
  id, symbol, accountType, transactionType: 'OPEN', shares: 10, isManual: false, holdingRole: 'Swing', ...over,
});

describe('eligibleCloseLotsForSymbol (Signal Tracker Close Position)', () => {
  const items = [
    lot(3, 'DIS', 'TFSA'),
    lot(1, 'DIS', 'Corp'),
    lot(2, 'DIS', 'Corp'),
    lot(4, 'DIS.TO', 'TFSA'),
    lot(5, 'DIS', 'TFSA', { transactionType: 'CLOSE' }),
    lot(6, 'DIS', 'TFSA', { shares: 0 }),
    lot(7, 'DIS', 'TFSA', { isManual: true }),
    lot(8, 'AAPL', 'TFSA', { holdingRole: 'Core' }),
  ];

  it('returns only open, non-manual lots with shares for the exact symbol, across accounts, ordered', () => {
    expect(eligibleCloseLotsForSymbol('dis', items).map((l) => l.id)).toEqual([1, 2, 3]);
  });

  it('never matches an underlying to its CDR or the reverse', () => {
    expect(eligibleCloseLotsForSymbol('DIS.TO', items).map((l) => l.id)).toEqual([4]);
  });

  it('includes lots of any holding role and returns nothing for closed-only or unknown tickers', () => {
    expect(eligibleCloseLotsForSymbol('AAPL', items).map((l) => l.id)).toEqual([8]);
    expect(eligibleCloseLotsForSymbol('ZZZ', items)).toEqual([]);
    expect(eligibleCloseLotsForSymbol('', items)).toEqual([]);
    expect(eligibleCloseLotsForSymbol('DIS', [lot(9, 'DIS', 'TFSA', { transactionType: 'CLOSE' })])).toEqual([]);
  });
});

describe('dateSortKey', () => {
  it('orders across month/year boundaries regardless of time part or format', () => {
    const a = dateSortKey('2026-01-01T00:00:00')!;
    const b = dateSortKey('2025-12-31')!;
    const c = dateSortKey('2025-12-31T23:59:59Z')!;
    expect(a).toBeGreaterThan(b);
    expect(b).toBe(c);
  });

  it('returns null for missing or invalid dates', () => {
    expect(dateSortKey(null)).toBeNull();
    expect(dateSortKey(undefined)).toBeNull();
    expect(dateSortKey('garbage')).toBeNull();
  });
});
