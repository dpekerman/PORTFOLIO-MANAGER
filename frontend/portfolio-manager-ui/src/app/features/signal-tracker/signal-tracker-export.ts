import { SignalAuditRow, SignalHorizon } from '../../core/models/portfolio.models';
import { stateLabel } from '../../shared/final-action-details-dialog/final-action-labels';

export const SIGNALS_EXPORT_HEADERS = [
  'Ticker',
  'Role',
  'Signal Date',
  'Days Since',
  'Signal Type',
  'Record Type',
  'Source',
  'Signal Price',
  'Currency',
  'Score',
  '5D %',
  '20D %',
  '1M %',
  'Today vs Signal %',
  'Today vs Actioned %',
  'Actioned Price',
  'My Action',
  'Eligibility',
  'Price Basis',
  'Lifecycle Status',
  'Effective State',
  'Actionable',
  'Evidence Health',
  'Last Evaluated (UTC)',
  'Data As Of',
  'Reason Codes',
] as const;

type Cell = string | number | null;

const horizon = (h: SignalHorizon): Cell => (h.status === 'Complete' ? h.pct : h.status === 'Pending' ? 'Pending' : 'N/A');

/** Display value for what the grid shows; money is passed through the demo-mode mask. */
export function signalsExportRows(rows: readonly SignalAuditRow[], maskValue: (v: number) => number): Cell[][] {
  return rows.map((r) => [
    r.symbol,
    r.role ?? '',
    r.signalDate,
    r.daysSince,
    r.signalTypeLabel,
    r.origin === 'INITIAL_BASELINE' ? 'Initial baseline' : r.origin === 'Backfill' ? 'Backfill' : 'New signal',
    r.source,
    maskValue(r.signalPrice),
    r.currency ?? '',
    r.score,
    horizon(r.return5D),
    horizon(r.return20D),
    horizon(r.return1M),
    horizon(r.today),
    r.todayVsActionedPct ?? 'N/A',
    r.actionedPrice != null ? maskValue(r.actionedPrice) : '',
    r.myAction,
    r.eligibilityStatus === 'Unverified' ? 'Score-qualified, eligibility unverified' : 'Verified',
    r.priceBasis,
    // Lifecycle (history) and the current effective state are separate: Active can be Awaiting Data and not actionable now.
    r.status ?? 'Active',
    r.effectiveState ? stateLabel(r.effectiveState) : '',
    r.isActionable == null ? '' : r.isActionable ? 'Yes' : 'No',
    r.evidenceHealth ?? '',
    r.lastValidatedAtUtc ?? '',
    r.dataAsOfDate ?? '',
    (r.reasonCodes ?? []).join('; '),
  ]);
}

/** RFC 4180 quoting; a leading BOM lets Excel read UTF-8. */
export function signalsToCsv(rows: readonly SignalAuditRow[], maskValue: (v: number) => number): string {
  const esc = (v: Cell) => {
    const s = v === null ? '' : String(v);
    return /[",\r\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const lines = [SIGNALS_EXPORT_HEADERS as readonly Cell[], ...signalsExportRows(rows, maskValue)].map((l) =>
    l.map(esc).join(','),
  );
  return '\uFEFF' + lines.join('\r\n');
}
