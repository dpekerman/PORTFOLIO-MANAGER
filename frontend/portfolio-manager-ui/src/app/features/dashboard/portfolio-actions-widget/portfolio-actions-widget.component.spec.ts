import { PortfolioActionDto } from '../../../core/models/portfolio.models';
import {
  ACTION_CENTER_FILTER_STORAGE_KEY,
  compareActionCenterValues,
  loadActionCenterFilter,
  saveActionCenterFilter,
  toggleSort,
} from './portfolio-actions-widget.component';

describe('Action Center view state', () => {
  it('restores a valid persisted priority filter', () => {
    const storage = {
      getItem: (key: string) => (key === ACTION_CENTER_FILTER_STORAGE_KEY ? 'DEVELOPING' : null),
    };
    expect(loadActionCenterFilter(storage)).toBe('DEVELOPING');
  });

  it('falls back to ALL for invalid persisted data', () => {
    expect(loadActionCenterFilter({ getItem: () => 'INVALID' })).toBe('ALL');
  });

  it('persists the selected filter under the Action Center key', () => {
    const values = new Map<string, string>();
    saveActionCenterFilter({ setItem: (key, value) => values.set(key, value) }, 'INFORMATIONAL');

    expect(values.get(ACTION_CENTER_FILTER_STORAGE_KEY)).toBe('INFORMATIONAL');
  });

  it('toggles the active sort direction and resets new columns ascending', () => {
    expect(toggleSort({ column: 'symbol', direction: 1 }, 'symbol')).toEqual({
      column: 'symbol',
      direction: -1,
    });
    expect(toggleSort({ column: 'symbol', direction: -1 }, 'rsi')).toEqual({
      column: 'rsi',
      direction: 1,
    });
  });

  it('sorts nullable RSI and allocation values deterministically', () => {
    expect(compareActionCenterValues(action({ rsi: 30 }), action({ rsi: 60 }), 'rsi')).toBeLessThan(
      0,
    );
    expect(
      compareActionCenterValues(
        action({ allocationStatus: 'under' }),
        action({ allocationStatus: 'over' }),
        'allocation',
      ),
    ).toBeLessThan(0);
  });
});

function action(overrides: Partial<PortfolioActionDto>): PortfolioActionDto {
  return {
    symbol: 'TEST',
    holdingRole: 'Strategic',
    rsi: null,
    maStructure: null,
    momentumState: null,
    priceStructure: null,
    allocationStatus: '',
    actionLabel: 'WATCH',
    ...overrides,
  } as PortfolioActionDto;
}
