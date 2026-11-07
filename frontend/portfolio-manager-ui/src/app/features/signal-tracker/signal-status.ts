import { RecommendationStatus, SignalAuditRow, SignalStatusFilter } from '../../core/models/portfolio.models';
import { describeReasonCode, stateLabel } from '../../shared/final-action-details-dialog/final-action-labels';

/**
 * One place that maps a recommendation lifecycle status to its label, style and meaning. The grid, the filter and the
 * details dialog all read it. Lifecycle is what happened to the recommendation; it is NOT the investor's My Action and
 * NOT a Final Action recommendation, so these styles deliberately avoid the Final Action palette.
 */
export interface SignalStatusMeta {
  label: string;
  /** Modifier class for `.life-badge`. */
  cls: string;
  meaning: string;
}

export const SIGNAL_STATUS_META: Record<RecommendationStatus, SignalStatusMeta> = {
  Active: { label: 'Active', cls: 'life-active', meaning: 'Not completed or exited yet. This does not by itself mean the market evidence is current.' },
  Acted: { label: 'Acted', cls: 'life-acted', meaning: 'You recorded taking action on this recommendation.' },
  Dismissed: { label: 'Dismissed', cls: 'life-closed', meaning: 'You dismissed this recommendation.' },
  Expired: { label: 'Expired', cls: 'life-closed', meaning: 'Its lifecycle window elapsed without a new qualifying evaluation.' },
  Invalidated: { label: 'Invalidated', cls: 'life-invalid', meaning: 'It stopped qualifying before it was acted on.' },
  Superseded: { label: 'Superseded', cls: 'life-closed', meaning: 'A newer recommendation replaced it.' },
};

export const SIGNAL_STATUSES = Object.keys(SIGNAL_STATUS_META) as RecommendationStatus[];

export function signalStatusMeta(status: string | null | undefined): SignalStatusMeta {
  return SIGNAL_STATUS_META[(status ?? 'Active') as RecommendationStatus] ?? { label: status ?? 'Active', cls: 'life-closed', meaning: '' };
}

/** Status filter options. Values are exactly what the API accepts. */
export const SIGNAL_STATUS_FILTER_OPTIONS: { value: SignalStatusFilter; label: string }[] = [
  { value: 'Active', label: 'Active' },
  { value: 'All', label: 'All' },
  ...SIGNAL_STATUSES.filter((s) => s !== 'Active').map((s) => ({ value: s as SignalStatusFilter, label: SIGNAL_STATUS_META[s].label })),
];

const UNRELIABLE_HEALTH = new Set(['Stale', 'Missing', 'Invalid', 'Failed']);

/**
 * The single decision point for the compact evidence warning in the Lifecycle cell.
 *
 * Evidence Health "Unknown" only means no current evaluation is cached; an Active recommendation that is still actionable
 * from a prior completed session is valid, so Unknown alone never warns. A warning is shown only for an Active Final Action
 * recommendation whose current actionability is lost, or whose evidence is explicitly Stale / Missing / Invalid / Failed.
 * Returns the tooltip / accessible text, or null for no warning.
 */
export function signalEvidenceWarning(row: Pick<SignalAuditRow, 'status' | 'effectiveState' | 'isActionable' | 'evidenceHealth' | 'reasonCodes' | 'dataAsOfDate'>): string | null {
  if ((row.status ?? 'Active') !== 'Active' || !row.effectiveState) return null;
  const why = (row.reasonCodes ?? []).map(describeReasonCode).filter(Boolean).join('; ');
  const asOf = row.dataAsOfDate ? ` Data as of ${row.dataAsOfDate}.` : '';
  const health = row.evidenceHealth ?? '';
  if (row.isActionable === false) {
    return `Not currently actionable: ${stateLabel(row.effectiveState)}.${why ? ` ${why}.` : ''}${asOf}`;
  }
  if (UNRELIABLE_HEALTH.has(health)) {
    return `Current evidence is ${health.toLowerCase()}.${why ? ` ${why}.` : ''}${asOf}`;
  }
  return null;
}

/** Plain-language evidence line for the details dialog; explains Unknown instead of implying a problem. */
export function signalEvidenceSummary(row: Pick<SignalAuditRow, 'status' | 'effectiveState' | 'isActionable' | 'evidenceHealth' | 'lastValidatedAtUtc'>): string {
  if (!row.effectiveState) return 'Not applicable (no current Final Action evaluation applies to this record).';
  const health = row.evidenceHealth ?? 'Unknown';
  if (health === 'Unknown') {
    return row.isActionable
      ? 'No current evaluation is cached. The recommendation remains valid from its last completed session.'
      : 'No current evaluation is cached and the recommendation is not currently actionable.';
  }
  return `Evidence is ${health.toLowerCase()}.`;
}
