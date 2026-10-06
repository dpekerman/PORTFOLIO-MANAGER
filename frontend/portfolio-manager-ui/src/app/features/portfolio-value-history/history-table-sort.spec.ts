import { HistorySortAccessors, sortHistoryRows } from './history-table-sort';

interface Row {
  id: number;
  date: string;
  amount: number | null;
  label: string;
}

describe('sortHistoryRows', () => {
  const rows: Row[] = [
    { id: 1, date: '2026-10-01', amount: 100, label: 'Zulu' },
    { id: 2, date: '2026-10-03', amount: -20, label: 'Alpha' },
    { id: 3, date: '2026-10-03', amount: 9, label: 'Beta' },
    { id: 4, date: '2026-10-02', amount: null, label: 'Beta' },
  ];
  const accessors: HistorySortAccessors<Row> = {
    date: (row) => row.date,
    amount: (row) => row.amount,
    label: (row) => row.label,
  };

  function ids(active: string, direction: 'asc' | 'desc' | ''): number[] {
    return sortHistoryRows(rows, { active, direction }, accessors, 'date').map((row) => row.id);
  }

  it('sorts dates newest first with descending IDs for equal dates without mutating input', () => {
    expect(ids('date', 'desc')).toEqual([3, 2, 4, 1]);
    expect(rows.map((row) => row.id)).toEqual([1, 2, 3, 4]);
  });

  it('sorts dates oldest first', () => {
    expect(ids('date', 'asc')).toEqual([1, 4, 3, 2]);
  });

  it('compares amounts numerically and keeps missing values last in both directions', () => {
    expect(ids('amount', 'asc')).toEqual([2, 3, 1, 4]);
    expect(ids('amount', 'desc')).toEqual([1, 3, 2, 4]);
  });

  it('sorts text with newest dates first for equal values', () => {
    expect(ids('label', 'asc')).toEqual([2, 3, 4, 1]);
    expect(ids('label', 'desc')).toEqual([1, 3, 4, 2]);
  });

  it('restores newest-first date sorting if sorting is cleared', () => {
    expect(ids('amount', '')).toEqual([3, 2, 4, 1]);
  });

  it('rejects unsupported sort columns', () => {
    expect(() => ids('unknown', 'asc')).toThrowError('Unsupported history sort column: unknown');
  });
});
