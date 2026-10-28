import * as XLSX from 'xlsx';

export type TxTypeFilter = 'ALL' | 'OPEN' | 'CLOSE';

/** Filter value that selects rows with no recorded role / close decision source. */
export const TX_NONE = '__none__';

/** Show OPEN records opened after this date (inclusive day after). */
export const OPEN_DATE_THRESHOLD = '2026-06-01';

const TORONTO = 'America/Toronto';
const DAY_MS = 86_400_000;

// ── Toronto calendar date ─────────────────────────────────────────────────────

/** Today's calendar date in Toronto as `yyyy-MM-dd`. */
export function torontoDateString(now: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: TORONTO,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}

/** Milliseconds until the next Toronto midnight (a re-armed timer corrects any DST drift). */
export function msUntilTorontoMidnight(now: Date = new Date()): number {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: TORONTO,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(now);
  const get = (t: string) => Number(parts.find((p) => p.type === t)?.value ?? 0);
  const elapsed = ((get('hour') * 60 + get('minute')) * 60 + get('second')) * 1000;
  return DAY_MS - elapsed + 500;
}

/** The `yyyy-MM-dd` part of an ISO date or date-time string, or null when it is not a date. */
export function dateOnly(value: string | null | undefined): string | null {
  const day = (value ?? '').substring(0, 10);
  return /^\d{4}-\d{2}-\d{2}$/.test(day) ? day : null;
}

function dayNumber(day: string): number {
  const [y, m, d] = day.split('-').map(Number);
  return Date.UTC(y, m - 1, d) / DAY_MS;
}

/**
 * Calendar days from the close date to `today` (both date-only, so DST and UTC offsets cannot
 * shift the result). Null for OPEN rows, a missing/invalid close date, or a close date in the
 * future — never 0 for "not applicable".
 */
export function daysSinceClose(
  type: string | null | undefined,
  closeDate: string | null | undefined,
  today: string,
): number | null {
  if (type !== 'CLOSE') return null;
  const close = dateOnly(closeDate);
  if (!close) return null;
  const days = dayNumber(today) - dayNumber(close);
  return days >= 0 ? days : null;
}

/** A closed row whose close date is after today: bad data that must be flagged, not shown as a negative. */
export function isFutureClose(
  type: string | null | undefined,
  closeDate: string | null | undefined,
  today: string,
): boolean {
  const close = dateOnly(closeDate);
  return type === 'CLOSE' && !!close && dayNumber(close) > dayNumber(today);
}

// ── Filtering ─────────────────────────────────────────────────────────────────

export interface TxFilterState {
  tab: TxTypeFilter;
  /** Role dropdown: '' = all, TX_NONE = no role, otherwise an exact role. */
  role: string;
  /** Opening Decision Source text (case-insensitive contains). */
  source: string;
  /** Close Decision Source dropdown: '' = all, TX_NONE = none recorded, otherwise exact. */
  sourceClosed: string;
}

export interface TxFilterable {
  type: string | null | undefined;
  openDate: string | null | undefined;
  role?: string | null;
  source: string | null | undefined;
  sourceClosed: string | null | undefined;
}

function matchesSelect(selected: string, actual: string | null | undefined): boolean {
  if (!selected) return true;
  const value = (actual ?? '').trim();
  if (selected === TX_NONE) return value === '';
  return value.toLowerCase() === selected.toLowerCase();
}

/** The close filter has no meaning on OPEN, so it is ignored there instead of hiding every row. */
export function closeFilterApplies(tab: TxTypeFilter): boolean {
  return tab !== 'OPEN';
}

/**
 * Tab + filter predicate shared by the stock and option grids. All active filters are AND-ed.
 * `roleApplies` is false for rows that carry no role (options).
 */
export function matchesTx(row: TxFilterable, f: TxFilterState, roleApplies = true): boolean {
  const type = row.type;
  if (f.tab === 'CLOSE') {
    if (type !== 'CLOSE') return false;
  } else if (f.tab === 'OPEN') {
    if (!(type === 'OPEN' && (row.openDate ?? '') > OPEN_DATE_THRESHOLD)) return false;
  } else if (type === 'OPEN') {
    if ((row.openDate ?? '') <= OPEN_DATE_THRESHOLD) return false;
  } else if (type !== 'CLOSE') {
    return false;
  }

  const source = f.source.trim().toLowerCase();
  if (source && !(row.source ?? '').toLowerCase().includes(source)) return false;
  if (roleApplies && !matchesSelect(f.role, row.role)) return false;
  if (closeFilterApplies(f.tab) && !matchesSelect(f.sourceClosed, row.sourceClosed)) return false;
  return true;
}

/** Distinct non-empty values (case-insensitive), alphabetically. */
export function distinctValues(values: Iterable<string | null | undefined>): string[] {
  const seen = new Map<string, string>();
  for (const v of values) {
    const t = (v ?? '').trim();
    if (t && !seen.has(t.toLowerCase())) seen.set(t.toLowerCase(), t);
  }
  return [...seen.values()].sort((a, b) => a.localeCompare(b));
}

// ── Totals ────────────────────────────────────────────────────────────────────

export interface TxTotalsRow {
  type: string | null | undefined;
  currency: string;
  /** Realized gain/loss; null when not applicable. */
  gainLoss: number | null;
  /** Existing per-row DIFF $; null when missing. */
  diffDollar: number | null;
}

export interface CurrencyAmount {
  currency: string;
  amount: number;
  count: number;
}

export interface TxTotals {
  count: number;
  closedCount: number;
  openCount: number;
  /** Realized Gain/Loss of CLOSE rows, per currency. */
  closedGainLoss: CurrencyAmount[];
  /** DIFF $ of CLOSE rows (opportunity cost after the sale), per currency. */
  closedDiff: CurrencyAmount[];
  /** DIFF $ of OPEN rows (unrealized move vs cost), per currency. */
  openDiff: CurrencyAmount[];
}

function addAmount(map: Map<string, CurrencyAmount>, currency: string, value: number | null): void {
  if (value == null || !Number.isFinite(value)) return;
  const entry = map.get(currency) ?? { currency, amount: 0, count: 0 };
  entry.amount += value;
  entry.count += 1;
  map.set(currency, entry);
}

const toList = (m: Map<string, CurrencyAmount>): CurrencyAmount[] =>
  [...m.values()].sort((a, b) => a.currency.localeCompare(b.currency));

/**
 * Totals over every matching row. No FX rate is available client-side, so amounts are never summed
 * across currencies: each currency gets its own subtotal. Realized (CLOSE) and unrealized (OPEN)
 * figures are kept apart. Each record is counted once (partial closures and lots are separate records).
 */
export function computeTxTotals(rows: readonly TxTotalsRow[]): TxTotals {
  const closedGl = new Map<string, CurrencyAmount>();
  const closedDiff = new Map<string, CurrencyAmount>();
  const openDiff = new Map<string, CurrencyAmount>();
  let closedCount = 0;
  let openCount = 0;
  for (const r of rows) {
    if (r.type === 'CLOSE') {
      closedCount++;
      addAmount(closedGl, r.currency, r.gainLoss);
      addAmount(closedDiff, r.currency, r.diffDollar);
    } else {
      openCount++;
      addAmount(openDiff, r.currency, r.diffDollar);
    }
  }
  return {
    count: rows.length,
    closedCount,
    openCount,
    closedGainLoss: toList(closedGl),
    closedDiff: toList(closedDiff),
    openDiff: toList(openDiff),
  };
}

// ── Sorting ───────────────────────────────────────────────────────────────────

/** Ascending/descending comparison where null (not applicable) always sorts last. */
export function compareSortValues(
  a: number | string | null,
  b: number | string | null,
  dir: 1 | -1,
): number {
  if (a === null && b === null) return 0;
  if (a === null) return 1;
  if (b === null) return -1;
  if (typeof a === 'number' && typeof b === 'number') return (a - b) * dir;
  return String(a).localeCompare(String(b)) * dir;
}

// ── Export ────────────────────────────────────────────────────────────────────

export type ExportCell = string | number | Date | null;

export const TX_EXPORT_SINCE_CLOSE = 'Since Close (days)';

export type TxExportKind = 'text' | 'qty' | 'money' | 'pct' | 'int' | 'date';

export interface TxExportColumn {
  header: string;
  kind: TxExportKind;
}

const col = (header: string, kind: TxExportKind = 'text'): TxExportColumn => ({ header, kind });

/** The last cell of every export row is its currency code. */
export const TX_STOCK_EXPORT_COLUMNS: readonly TxExportColumn[] = [
  col('Type'),
  col('Account'),
  col('Ticker'),
  col('Company'),
  col('Shares', 'qty'),
  col('Avg Cost', 'money'),
  col('Open Date', 'date'),
  col('Close Date', 'date'),
  col('Closing Price', 'money'),
  col('Gain/Loss', 'money'),
  col('Gain %', 'pct'),
  col('Last Price', 'money'),
  col('Price Diff', 'money'),
  col('Diff $', 'money'),
  col('Trans Date', 'date'),
  col('Decision Source'),
  col('Decision Source - Closed'),
  col('Role'),
  col('Industry'),
  col('Age (days)', 'int'),
  col(TX_EXPORT_SINCE_CLOSE, 'int'),
  col('Currency'),
];

export const TX_OPTION_EXPORT_COLUMNS: readonly TxExportColumn[] = [
  col('Type'),
  col('Account'),
  col('Underlying'),
  col('Position'),
  col('Expiry', 'date'),
  col('Strike', 'money'),
  col('Premium', 'money'),
  col('Contracts', 'int'),
  col('Open Date', 'date'),
  col('Close Date', 'date'),
  col('Closing Price', 'money'),
  col('Gain/Loss', 'money'),
  col('Gain %', 'pct'),
  col('Current Mkt Value', 'money'),
  col('Decision Source'),
  col('Decision Source - Closed'),
  col('Age (days)', 'int'),
  col(TX_EXPORT_SINCE_CLOSE, 'int'),
  col('Currency'),
];

/** RFC 4180 quoting. */
export function csvEscape(v: unknown): string {
  const s = v == null ? '' : String(v);
  return /[",\r\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

/** Parseable CSV: plain numbers (no currency symbols), ISO dates, empty for not-applicable. */
export function txRowsToCsv(columns: readonly TxExportColumn[], rows: readonly ExportCell[][]): string {
  const fmt = (c: TxExportColumn, v: ExportCell): string => {
    if (v === null || v === undefined || v === '') return '';
    if (typeof v === 'number') {
      return c.kind === 'money' || c.kind === 'pct' ? v.toFixed(2) : String(v);
    }
    return csvEscape(v instanceof Date ? v.toISOString().substring(0, 10) : v);
  };
  return [
    columns.map((c) => csvEscape(c.header)).join(','),
    ...rows.map((r) => columns.map((c, i) => fmt(c, r[i])).join(',')),
  ].join('\n');
}

const MONEY_FORMATS: Record<string, string> = {
  CAD: '"C$"#,##0.00;-"C$"#,##0.00',
  USD: '"US$"#,##0.00;-"US$"#,##0.00',
};

/** Worksheet with numeric cells (day counts, money, dates) rather than formatted strings. */
export function txSheet(columns: readonly TxExportColumn[], rows: readonly ExportCell[][]): XLSX.WorkSheet {
  const aoa: ExportCell[][] = [
    columns.map((c) => c.header),
    ...rows.map((r) =>
      r.map((v, i) => {
        if (v === null || v === '') return null;
        if (columns[i].kind === 'date' && typeof v === 'string') {
          const [y, m, d] = v.split('-').map(Number);
          return new Date(Date.UTC(y, m - 1, d));
        }
        return v;
      }),
    ),
  ];
  const sheet = XLSX.utils.aoa_to_sheet(aoa, { cellDates: true });
  rows.forEach((r, ri) => {
    const currency = String(r[columns.length - 1] ?? 'CAD');
    columns.forEach((c, ci) => {
      const cell = sheet[XLSX.utils.encode_cell({ r: ri + 1, c: ci })] as XLSX.CellObject | undefined;
      if (!cell || cell.v === undefined || cell.v === null) return;
      if (c.kind === 'money') cell.z = MONEY_FORMATS[currency] ?? '#,##0.00;-#,##0.00';
      else if (c.kind === 'pct') cell.z = '0.00';
      else if (c.kind === 'int') cell.z = '0';
      else if (c.kind === 'date') cell.z = 'yyyy-mm-dd';
    });
  });
  sheet['!cols'] = columns.map((c) => ({ wch: Math.max(10, c.header.length + 2) }));
  return sheet;
}

export function activeFilterSummary(f: TxFilterState): string[][] {
  const label = (v: string, all: string) => (v === TX_NONE ? '(none)' : v || all);
  return [
    ['Tab', f.tab === 'CLOSE' ? 'CLOSED' : f.tab],
    ['Role', label(f.role, 'All Roles')],
    ['Decision Source', f.source.trim() || 'All'],
    [
      'Decision Source - Close',
      closeFilterApplies(f.tab) ? label(f.sourceClosed, 'All') : 'Not applicable (OPEN)',
    ],
  ];
}
