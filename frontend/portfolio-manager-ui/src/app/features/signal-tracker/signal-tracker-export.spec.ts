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
  it('has one value per header and labels baseline records', () => {
    const [r] = signalsExportRows([row], (v) => v);
    expect(r.length).toBe(SIGNALS_EXPORT_HEADERS.length);
    expect(r[5]).toBe('Initial baseline');
    expect(r[10]).toBe('Pending');
    expect(r[13]).toBe(5.25);
    expect(r[14]).toBe('N/A');
  });

  it('writes a BOM, quotes commas and applies the demo-mode mask', () => {
    const csv = signalsToCsv([{ ...row, symbol: 'A,B' }], () => 0);
    expect(csv.startsWith('\uFEFFTicker,')).toBe(true);
    expect(csv).toContain('"A,B"');
    expect(csv.split('\r\n')[1]).toContain(',0,CAD,');
  });
});
