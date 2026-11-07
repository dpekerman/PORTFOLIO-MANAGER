import { eligibleCloseLots, CloseLotCandidate } from './swing-trading-logic';
import { swingRow } from './swing-row.testing';

const lot = (over: Partial<CloseLotCandidate> = {}): CloseLotCandidate => ({
  id: 639,
  symbol: 'NOVO.TO',
  accountType: 'TFSA_D_TD',
  holdingRole: 'Swing',
  transactionType: 'OPEN',
  shares: 600,
  ...over,
});

describe('eligibleCloseLots (PM-020)', () => {
  it('offers an OPEN Swing lot even when the planned stop is breached or clear', () => {
    for (const state of ['Breached', 'Clear', 'NoPlan'] as const) {
      expect(eligibleCloseLots(swingRow({ plannedStopState: state }), [lot()]).map((l) => l.id)).toEqual([639]);
    }
  });

  it('excludes closed, empty, non-Swing, manual and foreign lots', () => {
    const row = swingRow({ lotIds: [1, 2, 3, 4, 5, 6, 7] });
    const items = [
      lot({ id: 1 }),
      lot({ id: 2, transactionType: 'CLOSE' }),
      lot({ id: 3, shares: 0 }),
      lot({ id: 4, holdingRole: 'Core' }),
      lot({ id: 5, isManual: true }),
      lot({ id: 6, accountType: 'OTHER' }),
      lot({ id: 7, symbol: 'ABC.TO' }),
      lot({ id: 99 }),
    ];
    expect(eligibleCloseLots(row, items).map((l) => l.id)).toEqual([1]);
  });

  it('returns several lots ordered by id so the picker can show each one', () => {
    const row = swingRow({ lotIds: [12, 10] });
    expect(eligibleCloseLots(row, [lot({ id: 12 }), lot({ id: 10 })]).map((l) => l.id)).toEqual([10, 12]);
  });
});
