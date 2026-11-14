import { SignalAuditRow } from '../../core/models/portfolio.models';
import { SIGNALS_EXPORT_HEADERS, signalsExportRows, signalsToCsv } from './signal-tracker-export';

const done = { pct: 5.25, evalDate: '2026-10-30', evalPrice: 10, status: 'Complete' as const };
const pending = { pct: null, evalDate: null, evalPrice: null, status: 'Pending' as const };

const row = {
  id: 1,
  symbol: 'HPS-A.TO',
  role: 'Strategic',
  signalDate: '2026-10-23',
  daysSince: 1,
  signalTypeLabel: 'Add / Staged Entry',
  origin: 'INITIAL_BASELINE',
  source: 'Portfolio',
  signalPrice: 217.17,
  currency: 'CAD',
  score: null,
  return5D: pending,
  return20D: pending,
  return1M: pending,
  today: done,
  todayVsActionedPct: null,
  actionedPrice: null,
  myAction: 'Pending',
  eligibilityStatus: 'Verified',
  priceBasis: 'Verified',
} as unknown as SignalAuditRow;

describe('signal tracker export', () => {
  it('PM-015: CSV schema is fixed — first line is the header row, no comment or notice lines', () => {
    const csv = signalsToCsv([row], (v) => v).replace('\uFEFF', '');
    const lines = csv.split('\r\n');
    expect(lines[0]).toBe(SIGNALS_EXPORT_HEADERS.join(','));
    expect(lines).toHaveLength(2);
    expect(csv).not.toMatch(/^#|PARTIAL/m);
    expect(SIGNALS_EXPORT_HEADERS).toContain('Lifecycle Status');
  });

  it('has one value per header and labels baseline records', () => {
    const [r] = signalsExportRows([row], (v) => v);
    const col = (name: (typeof SIGNALS_EXPORT_HEADERS)[number]) => r[SIGNALS_EXPORT_HEADERS.indexOf(name)];
    expect(r.length).toBe(SIGNALS_EXPORT_HEADERS.length);
    expect(col('Record Type')).toBe('Initial baseline');
    expect(col('5D %')).toBe('Pending');
    expect(col('Today vs Signal %')).toBe(5.25);
    expect(col('Today vs Actioned %')).toBe('N/A');
  });

  it('PM-019: Current Price follows the signal price columns, is numeric, and is blank (never the signal price) when unavailable', () => {
    expect(SIGNALS_EXPORT_HEADERS.indexOf('Current Price')).toBe(SIGNALS_EXPORT_HEADERS.indexOf('Currency') + 1);
    const priced = {
      ...row,
      currentPrice: 230.5,
      currentPriceCurrency: 'CAD',
      currentPriceStatus: 'EOD',
      currentPriceAsOfUtc: '2026-11-02T21:00:00Z',
    } as unknown as SignalAuditRow;
    const [p] = signalsExportRows([priced], (v) => v);
    const col = (r: (string | number | null)[], name: (typeof SIGNALS_EXPORT_HEADERS)[number]) => r[SIGNALS_EXPORT_HEADERS.indexOf(name)];
    expect(col(p, 'Current Price')).toBe(230.5);
    expect(col(p, 'Current Price Currency')).toBe('CAD');
    expect(col(p, 'Current Price Status')).toBe('EOD');
    expect(col(p, 'Current Price As Of (UTC)')).toBe('2026-11-02T21:00:00Z');
    expect(col(p, 'Signal Price')).toBe(217.17);

    const [u] = signalsExportRows([row], (v) => v);
    expect(col(u, 'Current Price')).toBe('');
    expect(col(u, 'Current Price Status')).toBe('Unavailable');
    expect(col(u, 'Signal Price')).toBe(217.17);
  });

  it('exports lifecycle status and the current effective state as separate, non-actionable-aware columns', () => {
    const awaiting = {
      ...row,
      status: 'Active',
      effectiveState: 'AwaitingData',
      isActionable: false,
      evidenceHealth: 'Stale',
      lastValidatedAtUtc: '2026-10-20T21:00:00Z',
      dataAsOfDate: '2026-10-20',
      reasonCodes: ['AWAITING_STALE_DATA', 'REVALIDATION_MISSED'],
    } as unknown as SignalAuditRow;
    const [r] = signalsExportRows([awaiting], (v) => v);
    const col = (name: (typeof SIGNALS_EXPORT_HEADERS)[number]) => r[SIGNALS_EXPORT_HEADERS.indexOf(name)];
    expect(col('Lifecycle Status')).toBe('Active');
    expect(col('Effective State')).toBe('Awaiting Data');
    expect(col('Actionable')).toBe('No');
    expect(col('Evidence Health')).toBe('Stale');
    expect(col('Last Evaluated (UTC)')).toBe('2026-10-20T21:00:00Z');
    expect(col('Data As Of')).toBe('2026-10-20');
    expect(col('Reason Codes')).toBe('AWAITING_STALE_DATA; REVALIDATION_MISSED');
    // the original signal date/price columns are untouched by the effective state
    expect(col('Signal Date')).toBe('2026-10-23');
  });

  it('leaves the effective columns blank for closed lifecycles and never claims actionability it does not know', () => {
    const [r] = signalsExportRows([{ ...row, status: 'Invalidated' } as unknown as SignalAuditRow], (v) => v);
    expect(r[SIGNALS_EXPORT_HEADERS.indexOf('Lifecycle Status')]).toBe('Invalidated');
    expect(r[SIGNALS_EXPORT_HEADERS.indexOf('Effective State')]).toBe('');
    expect(r[SIGNALS_EXPORT_HEADERS.indexOf('Actionable')]).toBe('');
  });

  it('writes a BOM, quotes commas and applies the demo-mode mask', () => {
    const csv = signalsToCsv([{ ...row, symbol: 'A,B' }], () => 0);
    expect(csv.startsWith('\uFEFFTicker,')).toBe(true);
    expect(csv).toContain('"A,B"');
    expect(csv.split('\r\n')[1]).toContain(',0,CAD,');
  });
});
