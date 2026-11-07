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
  dateRangeInvalid,
  daysSinceClose,
  txEffectiveDate,
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
  account: '',
  dateFrom: '',
  dateTo: '',
  ...over,
});

const closed = (over: Partial<TxFilterable> = {}): TxFilterable => ({
  type: 'CLOSE',
  openDate: '2026-01-01',
  closeDate: '2026-10-15',
  account: 'TFSA_D_TD',
  role: 'Swing',
  source: 'Technical Signal',
  sourceClosed: 'Stop Loss',
  ...over,
});
const open = (over: Partial<TxFilterable> = {}): TxFilterable => ({
  type: 'OPEN',
  openDate: '2026-08-27',
  account: 'TFSA_D_TD',
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

  it('filters the opening decision source by exact value (not a substring)', () => {
    expect(matchesTx(closed(), state({ source: 'technical signal' }))).toBe(true);
    expect(matchesTx(closed(), state({ source: 'Technical' }))).toBe(false);
    expect(matchesTx(closed(), state({ source: 'Manual' }))).toBe(false);
    expect(matchesTx(closed({ source: null }), state({ source: TX_NONE }))).toBe(true);
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

describe('PM-013 advanced filters', () => {
  const rows: (TxFilterable & { currency: string; gainLoss: number | null; diffDollar: number | null; id: number })[] = [
    { id: 1, ...closed({ closeDate: '2026-10-01', account: 'TFSA_D_TD', sourceClosed: 'Stop Loss Trigerred' }), currency: 'CAD', gainLoss: 100, diffDollar: 5 },
    { id: 2, ...closed({ closeDate: '2026-10-31T00:00:00', account: 'TFSA_D_TD', sourceClosed: 'Stop Loss Trigerred' }), currency: 'USD', gainLoss: 50, diffDollar: 2 },
    { id: 3, ...closed({ closeDate: '2026-11-01', account: 'TFSA_D_TD', sourceClosed: 'Stop Loss Trigerred' }), currency: 'CAD', gainLoss: 7, diffDollar: 1 },
    { id: 4, ...closed({ closeDate: '2026-09-30', account: 'TFSA_D_TD', sourceClosed: 'Stop Loss Trigerred' }), currency: 'CAD', gainLoss: 9, diffDollar: 1 },
    { id: 5, ...closed({ closeDate: '2026-10-15', account: 'RRSP', sourceClosed: 'Stop Loss Trigerred' }), currency: 'CAD', gainLoss: 11, diffDollar: 1 },
    { id: 6, ...closed({ closeDate: '2026-10-15', role: 'Core', sourceClosed: 'Stop Loss Trigerred' }), currency: 'CAD', gainLoss: 13, diffDollar: 1 },
    { id: 7, ...closed({ closeDate: '2026-10-15', sourceClosed: 'Target' }), currency: 'CAD', gainLoss: 17, diffDollar: 1 },
    { id: 8, ...open({ openDate: '2026-10-10' }), currency: 'CAD', gainLoss: null, diffDollar: 3 },
    { id: 9, ...closed({ closeDate: null }), currency: 'CAD', gainLoss: 19, diffDollar: 1 },
  ];
  const ids = (f: TxFilterState) => rows.filter((r) => matchesTx(r, f)).map((r) => r.id);

  it('From only and To only are inclusive', () => {
    expect(ids(state({ tab: 'CLOSE', dateFrom: '2026-10-15' }))).toEqual([2, 3, 5, 6, 7]);
    expect(ids(state({ tab: 'CLOSE', dateTo: '2026-10-01' }))).toEqual([1, 4]);
  });

  it('From and To are both inclusive day boundaries, including date-time strings', () => {
    expect(ids(state({ tab: 'CLOSE', dateFrom: '2026-10-01', dateTo: '2026-10-31' }))).toEqual([1, 2, 5, 6, 7]);
  });

  it('a single-day range returns exactly that day', () => {
    expect(ids(state({ tab: 'CLOSE', dateFrom: '2026-10-31', dateTo: '2026-10-31' }))).toEqual([2]);
  });

  it('From after To matches nothing and is reported as invalid', () => {
    const f = state({ dateFrom: '2026-10-31', dateTo: '2026-10-01' });
    expect(dateRangeInvalid(f)).toBe(true);
    expect(ids(f)).toEqual([]);
    expect(dateRangeInvalid(state({ dateFrom: '2026-10-01', dateTo: '2026-10-01' }))).toBe(false);
  });

  it('uses the close date for CLOSE and the open date for OPEN, never a missing date', () => {
    expect(txEffectiveDate('CLOSE', '2026-01-01', '2026-10-15')).toBe('2026-10-15');
    expect(txEffectiveDate('OPEN', '2026-08-27', null)).toBe('2026-08-27');
    const inOct = state({ dateFrom: '2026-10-01', dateTo: '2026-10-31' });
    expect(ids(inOct)).toContain(8); // OPEN row dated by its open date
    expect(ids(inOct)).not.toContain(9); // CLOSE without a close date is excluded when a bound is active
    expect(ids(state())).toContain(9); // ...but still listed with no date filter
  });

  it('filters by account exactly', () => {
    expect(ids(state({ tab: 'CLOSE', account: 'RRSP' }))).toEqual([5]);
    expect(ids(state({ tab: 'CLOSE', account: 'rrsp' }))).toEqual([]);
  });

  it('ANDs the documented example: dates + account + role + close source', () => {
    const f = state({
      tab: 'CLOSE',
      dateFrom: '2026-10-01',
      dateTo: '2026-10-31',
      account: 'TFSA_D_TD',
      role: 'Swing',
      sourceClosed: 'Stop Loss Trigerred',
    });
    expect(ids(f)).toEqual([1, 2]);
  });

  it('combines Open and Close decision sources independently', () => {
    expect(ids(state({ tab: 'CLOSE', source: 'Technical Signal', sourceClosed: 'Target' }))).toEqual([7]);
    expect(ids(state({ tab: 'CLOSE', source: 'Manual', sourceClosed: 'Target' }))).toEqual([]);
  });

  it('keeps ALL / OPEN / CLOSED semantics with date filters', () => {
    const range = { dateFrom: '2026-10-01', dateTo: '2026-10-31' };
    expect(ids(state({ tab: 'OPEN', ...range }))).toEqual([8]);
    expect(ids(state({ tab: 'ALL', ...range }))).toEqual([1, 2, 5, 6, 7, 8]);
  });

  it('totals are computed from exactly the filtered rows, per currency', () => {
    const f = state({ tab: 'CLOSE', dateFrom: '2026-10-01', dateTo: '2026-10-31', account: 'TFSA_D_TD', role: 'Swing' });
    const t = computeTxTotals(rows.filter((r) => matchesTx(r, f)));
    expect(t.closedGainLoss).toEqual([
      { currency: 'CAD', amount: 117, count: 2 },
      { currency: 'USD', amount: 50, count: 1 },
    ]);
    expect(computeTxTotals([]).closedGainLoss).toEqual([]);
    expect(computeTxTotals([]).count).toBe(0);
  });

  it('the CSV contains every filtered row, not just a page', () => {
    const f = state({ tab: 'CLOSE', dateFrom: '2026-10-01', dateTo: '2026-10-31' });
    const matched = rows.filter((r) => matchesTx(r, f));
    const csv = txRowsToCsv(
      [{ header: 'Id', kind: 'int' }],
      matched.map((r) => [r.id]),
    );
    expect(csv.split('\n')).toEqual(['Id', '1', '2', '5', '6', '7']);
  });

  it('lists the new filters in the exported filter summary', () => {
    const summary = activeFilterSummary(state({ dateFrom: '2026-10-01', account: 'RRSP', source: 'Manual' }));
    expect(summary).toContainEqual(['Transaction Date From', '2026-10-01']);
    expect(summary).toContainEqual(['Transaction Date To', 'Any']);
    expect(summary).toContainEqual(['Account', 'RRSP']);
    expect(summary).toContainEqual(['Decision Source - Open', 'Manual']);
  });
});
