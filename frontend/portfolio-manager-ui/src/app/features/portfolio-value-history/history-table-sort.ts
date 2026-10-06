import { Sort } from '@angular/material/sort';

type SortValue = string | number | null | undefined;
export type HistorySortAccessors<T> = Record<string, (row: T) => SortValue>;

export function sortHistoryRows<T extends { id: number }>(
  rows: readonly T[],
  sort: Sort,
  accessors: HistorySortAccessors<T>,
  dateColumn: string,
): T[] {
  const active = sort.direction ? sort.active : dateColumn;
  const direction = sort.direction === 'asc' ? 1 : -1;
  const valueFor = accessors[active];
  const dateFor = accessors[dateColumn];
  if (!valueFor || !dateFor) {
    throw new Error(`Unsupported history sort column: ${active}`);
  }

  return [...rows].sort((a, b) => {
    const av = valueFor(a);
    const bv = valueFor(b);
    // Missing audit dates and daily changes stay at the bottom in either direction.
    if (av == null && bv != null) return 1;
    if (av != null && bv == null) return -1;
    return (
      compareValues(av, bv) * direction || compareValues(dateFor(b), dateFor(a)) || b.id - a.id
    );
  });
}

function compareValues(a: SortValue, b: SortValue): number {
  if (a == null || b == null) return 0;
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  return String(a).localeCompare(String(b));
}
