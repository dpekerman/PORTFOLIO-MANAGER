import * as XLSX from 'xlsx';
import {
  TX_NONE,
  TX_OPTION_EXPORT_COLUMNS,
  TX_STOCK_EXPORT_COLUMNS,
  TxFilterState,
  TxFilterable,
  activeFilterSummary,
  compareSortValues,
  computeTxTotals,
  dateOnly,
  daysSinceClose,
  distinctValues,
  isFutureClose,
  matchesTx,
  msUntilTorontoMidnight,
  torontoDateString,
  txRowsToCsv,
  txSheet,
} from './transactions-logic';

const state = (over: Partial<TxFilterState> = {}): TxFilterState => ({
  tab: 'ALL',
  role: '',
  source: '',
  sourceClosed: '',
  ...over,
});

const closed = (over: Partial<TxFilterable> = {}): TxFilterable => ({
  type: 'CLOSE',
  openDate: '2026-01-01',
  role: 'Swing',
  source: 'Technical Signal',
  sourceClosed: 'Stop Loss',
  ...over,
});
const open = (over: Partial<TxFilterable> = {}): TxFilterable => ({
  type: 'OPEN',
  openDate: '2026-08-27',
  role: 'Swing',
  source: 'Technical Signal',
  sourceClosed: null,
  ...over,
});

describe('Toronto date', () => {
  it('uses the Toronto calendar day, not UTC', () => {
    // 2026-10-29T02:30Z is still Oct 28 in Toronto (EDT, UTC-4)
    expect(torontoDateString(new Date('2026-10-29T02:30:00Z'))).toBe('2026-10-28');
    expect(torontoDateString(new Date('2026-10-29T04:00:00Z'))).toBe('2026-10-29');
  });

  it('rolls over at Toronto midnight across the DST fall-back', () => {
    // 2026-11-01 DST ends: 05:00Z is 01:00 EDT, 06:00Z is 01:00 EST
    expect(torontoDateString(new Date('2026-11-01T03:59:00Z'))).toBe('2026-10-31');
    expect(torontoDateString(new Date('2026-11-01T04:00:00Z'))).toBe('2026-11-01');
  });

  it('computes the wait until the next Toronto midnight', () => {
    const ms = msUntilTorontoMidnight(new Date('2026-10-29T03:59:00Z')); // 23:59 Toronto
    expect(ms).toBeGreaterThan(60_000);
    expect(ms).toBeLessThan(62_000);
  });
});

describe('daysSinceClose', () => {
  const today = '2026-10-28';

  it.each([
    ['2026-10-13', 15],
    ['2026-10-28', 0],
    ['2026-10-15', 13],
    ['2026-10-13T00:00:00', 15],
    ['2026-10-13T23:59:59Z', 15],
  ])('closed %s → %s days', (close, days) => {
    expect(daysSinceClose('CLOSE', close, today)).toBe(days);
  });

  it('is null (never 0) for OPEN, missing and invalid dates', () => {
    expect(daysSinceClose('OPEN', '2026-10-13', today)).toBeNull();
    expect(daysSinceClose('OPEN', null, today)).toBeNull();
    expect(daysSinceClose('CLOSE', null, today)).toBeNull();
    expect(daysSinceClose('CLOSE', '', today)).toBeNull();
    expect(daysSinceClose('CLOSE', 'garbage', today)).toBeNull();
    expect(daysSinceClose(null, '2026-10-13', today)).toBeNull();
  });

  it('flags a future close date instead of returning a negative age', () => {
    expect(daysSinceClose('CLOSE', '2026-10-30', today)).toBeNull();
    expect(isFutureClose('CLOSE', '2026-10-30', today)).toBe(true);
    expect(isFutureClose('CLOSE', '2026-10-28', today)).toBe(false);
    expect(isFutureClose('OPEN', '2026-10-30', today)).toBe(false);
  });

  it('counts calendar days across DST changes', () => {
    expect(daysSinceClose('CLOSE', '2026-10-31', '2026-11-02')).toBe(2);
    expect(daysSinceClose('CLOSE', '2026-03-07', '2026-03-09')).toBe(2);
  });

  it('updates when the date changes', () => {
    expect(daysSinceClose('CLOSE', '2026-10-27', '2026-10-28')).toBe(1);
    expect(daysSinceClose('CLOSE', '2026-10-27', '2026-10-29')).toBe(2);
  });

  it('dateOnly keeps the day part only', () => {
    expect(dateOnly('2026-10-13T05:00:00Z')).toBe('2026-10-13');
    expect(dateOnly('nope')).toBeNull();
  });
});

describe('matchesTx filters', () => {
  it('keeps the existing tab rules', () => {
    expect(matchesTx(open(), state({ tab: 'OPEN' }))).toBe(true);
    expect(matchesTx(closed(), state({ tab: 'OPEN' }))).toBe(false);
    expect(matchesTx(closed(), state({ tab: 'CLOSE' }))).toBe(true);
    expect(matchesTx(open(), state({ tab: 'CLOSE' }))).toBe(false);
    expect(matchesTx(open({ openDate: '2026-05-01' }), state())).toBe(false);
    expect(matchesTx(open({ type: null }), state())).toBe(false);
  });

  it('filters by role (exact, case-insensitive) and no-role', () => {
    expect(matchesTx(closed(), state({ role: 'swing' }))).toBe(true);
    expect(matchesTx(closed(), state({ role: 'Core' }))).toBe(false);
    expect(matchesTx(closed({ role: null }), state({ role: TX_NONE }))).toBe(true);
    expect(matchesTx(closed(), state({ role: TX_NONE }))).toBe(false);
  });

  it('ignores the role filter for rows that carry no role (options)', () => {
    expect(matchesTx(closed({ role: undefined }), state({ role: 'Core' }), false)).toBe(true);
  });

  it('filters the opening decision source by substring', () => {
    expect(matchesTx(closed(), state({ source: 'technical' }))).toBe(true);
    expect(matchesTx(closed(), state({ source: 'manual' }))).toBe(false);
  });

  it('filters the close decision source on ALL and CLOSE', () => {
    expect(matchesTx(closed(), state({ tab: 'CLOSE', sourceClosed: 'Stop Loss' }))).toBe(true);
    expect(matchesTx(closed(), state({ tab: 'CLOSE', sourceClosed: 'Target' }))).toBe(false);
    expect(matchesTx(closed(), state({ sourceClosed: 'Target' }))).toBe(false);
    expect(matchesTx(closed({ sourceClosed: null }), state({ sourceClosed: TX_NONE }))).toBe(true);
  });

  it('ignores the close filter on OPEN so open rows never disappear', () => {
    expect(matchesTx(open(), state({ tab: 'OPEN', sourceClosed: 'Stop Loss' }))).toBe(true);
  });

  it('ANDs every filter together', () => {
    const f = state({ tab: 'CLOSE', role: 'Swing', source: 'Technical Signal', sourceClosed: 'Stop Loss' });
    expect(matchesTx(closed(), f)).toBe(true);
    expect(matchesTx(closed({ role: 'Core' }), f)).toBe(false);
    expect(matchesTx(closed({ source: 'Manual' }), f)).toBe(false);
    expect(matchesTx(closed({ sourceClosed: 'Target' }), f)).toBe(false);
  });

  it('builds de-duplicated sorted options', () => {
    expect(distinctValues(['Swing', 'core', ' Core ', null, '', 'Swing'])).toEqual(['core', 'Swing']);
  });

  it('summarises active filters', () => {
    const rows = activeFilterSummary(state({ tab: 'OPEN', sourceClosed: 'X' }));
    expect(rows).toContainEqual(['Decision Source - Close', 'Not applicable (OPEN)']);
    expect(rows).toContainEqual(['Role', 'All Roles']);
  });
});

describe('computeTxTotals', () => {
  it('never adds CAD and USD together and keeps realized and open apart', () => {
    const t = computeTxTotals([
      { type: 'CLOSE', currency: 'CAD', gainLoss: 100, diffDollar: -10 },
      { type: 'CLOSE', currency: 'CAD', gainLoss: -40, diffDollar: 5 },
      { type: 'CLOSE', currency: 'USD', gainLoss: 50, diffDollar: null },
      { type: 'OPEN', currency: 'USD', gainLoss: null, diffDollar: 25 },
      { type: 'OPEN', currency: 'CAD', gainLoss: null, diffDollar: -5 },
    ]);
    expect(t.count).toBe(5);
    expect(t.closedCount).toBe(3);
    expect(t.openCount).toBe(2);
    expect(t.closedGainLoss).toEqual([
      { currency: 'CAD', amount: 60, count: 2 },
      { currency: 'USD', amount: 50, count: 1 },
    ]);
    expect(t.closedDiff).toEqual([{ currency: 'CAD', amount: -5, count: 2 }]);
    expect(t.openDiff).toEqual([
      { currency: 'CAD', amount: -5, count: 1 },
      { currency: 'USD', amount: 25, count: 1 },
    ]);
  });

  it('does not fabricate values for missing data', () => {
    const t = computeTxTotals([{ type: 'CLOSE', currency: 'CAD', gainLoss: null, diffDollar: null }]);
    expect(t.closedGainLoss).toEqual([]);
    expect(t.closedDiff).toEqual([]);
    expect(t.closedCount).toBe(1);
  });

  it('handles an empty filtered set', () => {
    expect(computeTxTotals([]).count).toBe(0);
  });
});

describe('compareSortValues', () => {
  it('sorts numerically with null always last in both directions', () => {
    const values = [10, null, 2, 0, null, 15];
    const asc = [...values].sort((a, b) => compareSortValues(a, b, 1));
    const desc = [...values].sort((a, b) => compareSortValues(a, b, -1));
    expect(asc).toEqual([0, 2, 10, 15, null, null]);
    expect(desc).toEqual([15, 10, 2, 0, null, null]);
  });

  it('sorts strings', () => {
    expect(compareSortValues('a', 'b', 1)).toBeLessThan(0);
    expect(compareSortValues('a', 'b', -1)).toBeGreaterThan(0);
  });
});

describe('exports', () => {
  const stockRow = [
    'CLOSE', 'Margin', 'SU.TO', 'Suncor, Inc', 180, 41.2, '2026-06-02', '2026-10-13', 51.75, 1899, 25.6,
    52.3, -0.55, -99, '2026-10-13', 'App Signal', 'Stop Loss', 'Swing', 'Oil', 133, 15, 'CAD',
  ];
  const openRow = [
    'OPEN', 'TFSA', 'NVDA', 'NVIDIA', 10, 100, '2026-08-27', null, null, null, null,
    110, 10, 100, '2026-08-27', '', '', 'Core', '', 62, null, 'USD',
  ];

  it('CSV puts SINCE CLOSE after AGE, numeric and empty for OPEN', () => {
    const csv = txRowsToCsv(TX_STOCK_EXPORT_COLUMNS, [stockRow, openRow]);
    const [header, closedLine, openLine] = csv.split('\n');
    expect(header).toContain('Age (days),Since Close (days),Currency');
    expect(closedLine).toContain('"Suncor, Inc"');
    expect(closedLine.endsWith(',133,15,CAD')).toBe(true);
    expect(openLine.endsWith(',62,,USD')).toBe(true);
    expect(closedLine).not.toContain('$');
  });

  it('CSV keeps negatives and uses plain numbers', () => {
    const csv = txRowsToCsv(TX_STOCK_EXPORT_COLUMNS, [stockRow]);
    expect(csv).toContain(',-0.55,-99.00,');
  });

  it('Excel stores numeric days, numeric money and real dates', () => {
    const sheet = txSheet(TX_STOCK_EXPORT_COLUMNS, [stockRow, openRow]);
    const sinceCol = TX_STOCK_EXPORT_COLUMNS.findIndex((c) => c.header === 'Since Close (days)');
    const cell = (r: number, c: number) => sheet[XLSX.utils.encode_cell({ r, c })];
    expect(cell(1, sinceCol).t).toBe('n');
    expect(cell(1, sinceCol).v).toBe(15);
    expect(cell(2, sinceCol)).toBeUndefined(); // OPEN stays blank
    const gl = TX_STOCK_EXPORT_COLUMNS.findIndex((c) => c.header === 'Gain/Loss');
    expect(cell(1, gl).t).toBe('n');
    expect(cell(1, gl).z).toContain('C$');
    expect(cell(2, 0).v).toBe('OPEN');
    const openDate = TX_STOCK_EXPORT_COLUMNS.findIndex((c) => c.header === 'Open Date');
    expect(cell(1, openDate).t).toBe('d');
    const diff = TX_STOCK_EXPORT_COLUMNS.findIndex((c) => c.header === 'Diff $');
    expect(cell(2, diff).z).toContain('US$');
  });

  it('option export has matching header and row widths', () => {
    expect(TX_OPTION_EXPORT_COLUMNS.at(-2)?.header).toBe('Since Close (days)');
    expect(TX_STOCK_EXPORT_COLUMNS.length).toBe(stockRow.length);
  });
});
