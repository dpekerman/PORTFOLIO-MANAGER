import { StockQuote } from './models/portfolio.models';
import { compareMissingLast, forwardDividendOf, forwardSortValue } from './forward-dividend.util';

const quote = (over: Partial<StockQuote>): StockQuote => ({ symbol: 'ENB.TO', currentPrice: 68.69, ...over }) as StockQuote;

describe('forward dividend view (PM-034)', () => {
  it('shows a positive provider-supported estimate with a source note and no invented update time', () => {
    const v = forwardDividendOf(
      quote({
        dividendStatus: 'Positive',
        forwardDividendPerShare: 3.88,
        forwardYieldPct: 5.65,
        dividendDateUtc: '2026-12-01T00:00:00Z',
        dividendSource: 'Yahoo Finance v7 quote: dividendRate',
      }),
    );
    expect(v.perShare).toBe(3.88);
    expect(v.yieldPct).toBe(5.65);
    expect(v.tooltip).toContain('not a guaranteed payment');
    expect(v.tooltip).toContain('Yahoo Finance v7 quote: dividendRate');
    expect(v.tooltip).toContain('2026-12-01');
    expect(v.tooltip).toContain('gives no time for when this estimate was last updated');
  });

  it('keeps a confirmed zero as 0 and distinguishes it from missing', () => {
    const zero = forwardDividendOf(quote({ dividendStatus: 'Zero', forwardDividendPerShare: 0, forwardYieldPct: 0 }));
    expect(zero.perShare).toBe(0);
    expect(zero.yieldPct).toBe(0);
    expect(zero.tooltip).toContain('zero forward dividend');

    const missing = forwardDividendOf(quote({ dividendStatus: 'Missing' }));
    expect(missing.perShare).toBeNull();
    expect(missing.tooltip).toContain('unknown, not zero');
  });

  it.each(['Stale', 'Inconsistent'] as const)('%s data never shows values even if numbers are present', (status) => {
    const v = forwardDividendOf(quote({ dividendStatus: status, forwardDividendPerShare: 3.88, forwardYieldPct: 5.6 }));
    expect(v.perShare).toBeNull();
    expect(v.yieldPct).toBeNull();
  });

  it('treats quotes saved before the feature, and no quote at all, as missing', () => {
    expect(forwardDividendOf(quote({})).status).toBe('Missing');
    expect(forwardDividendOf(null).perShare).toBeNull();
  });

  it('sorts numerically with missing values last in both directions', () => {
    const rows = [
      quote({ symbol: 'A', dividendStatus: 'Positive', forwardDividendPerShare: 1.5, forwardYieldPct: 2 }),
      quote({ symbol: 'B', dividendStatus: 'Missing' }),
      quote({ symbol: 'C', dividendStatus: 'Positive', forwardDividendPerShare: 10, forwardYieldPct: 6 }),
      quote({ symbol: 'D', dividendStatus: 'Zero', forwardDividendPerShare: 0, forwardYieldPct: 0 }),
      quote({ symbol: 'E', dividendStatus: 'Stale', forwardDividendPerShare: 4, forwardYieldPct: 4 }),
    ];
    const order = (dir: 1 | -1) =>
      [...rows]
        .sort((a, b) => compareMissingLast(forwardSortValue(a, 'perShare'), forwardSortValue(b, 'perShare'), dir))
        .map((r) => r.symbol);
    expect(order(1).slice(0, 3)).toEqual(['D', 'A', 'C']);
    expect(order(-1).slice(0, 3)).toEqual(['C', 'A', 'D']);
    expect(order(1).slice(3).sort()).toEqual(['B', 'E']);
    expect(order(-1).slice(3).sort()).toEqual(['B', 'E']);
  });
});
