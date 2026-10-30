import { SignalAuditRow } from '../../core/models/portfolio.models';
import {
  SIGNAL_STATUS_FILTER_OPTIONS,
  SIGNAL_STATUS_META,
  SIGNAL_STATUSES,
  signalEvidenceSummary,
  signalEvidenceWarning,
  signalStatusMeta,
} from './signal-status';

type Evidence = Pick<SignalAuditRow, 'status' | 'effectiveState' | 'isActionable' | 'evidenceHealth' | 'reasonCodes' | 'dataAsOfDate'>;
const row = (over: Partial<Evidence>): Evidence => ({
  status: 'Active', effectiveState: 'PriorSessionValid', isActionable: true, evidenceHealth: 'Unknown', reasonCodes: [], dataAsOfDate: null, ...over,
});

describe('signal status mapping', () => {
  it('covers every backend lifecycle status with a short label and one style', () => {
    expect(SIGNAL_STATUSES.sort()).toEqual(['Acted', 'Active', 'Dismissed', 'Expired', 'Invalidated', 'Superseded']);
    for (const s of SIGNAL_STATUSES) {
      expect(SIGNAL_STATUS_META[s].label).toBe(s);
      expect(SIGNAL_STATUS_META[s].label).not.toMatch(/Lifecycle/);
    }
    expect(signalStatusMeta(undefined).label).toBe('Active');
    expect(signalStatusMeta('Invalidated').cls).toBe('life-invalid');
  });

  it('filter offers Active first (the default), All, and every other lifecycle status', () => {
    const values = SIGNAL_STATUS_FILTER_OPTIONS.map((o) => o.value);
    expect(values[0]).toBe('Active');
    expect(values).toEqual(expect.arrayContaining(['All', 'Acted', 'Invalidated', 'Dismissed', 'Superseded', 'Expired']));
    expect(values).not.toContain('Open');
  });
});

describe('signalEvidenceWarning', () => {
  it('does NOT warn for Unknown evidence health while the recommendation is still actionable', () => {
    expect(signalEvidenceWarning(row({ evidenceHealth: 'Unknown', isActionable: true }))).toBeNull();
    expect(signalEvidenceWarning(row({ evidenceHealth: null }))).toBeNull();
  });

  it('warns when an Active recommendation is not currently actionable, even with Unknown health', () => {
    const msg = signalEvidenceWarning(row({ isActionable: false, effectiveState: 'AwaitingData' }));
    expect(msg).toMatch(/Not currently actionable/);
  });

  it.each(['Stale', 'Missing', 'Invalid', 'Failed'])('warns for explicitly %s evidence', (health) => {
    expect(signalEvidenceWarning(row({ evidenceHealth: health }))).toContain(health.toLowerCase());
  });

  it('never warns for closed lifecycles or rows without a Final Action effective state', () => {
    expect(signalEvidenceWarning(row({ status: 'Acted', isActionable: false, evidenceHealth: 'Stale' }))).toBeNull();
    expect(signalEvidenceWarning(row({ status: 'Invalidated', isActionable: false }))).toBeNull();
    expect(signalEvidenceWarning(row({ effectiveState: null, isActionable: null, evidenceHealth: 'Stale' }))).toBeNull();
  });

  it('explains Unknown + actionable in plain language instead of implying a defect', () => {
    expect(signalEvidenceSummary({ status: 'Active', effectiveState: 'PriorSessionValid', isActionable: true, evidenceHealth: 'Unknown', lastValidatedAtUtc: null }))
      .toContain('remains valid from its last completed session');
    expect(signalEvidenceSummary({ status: 'Active', effectiveState: 'AwaitingData', isActionable: false, evidenceHealth: 'Stale', lastValidatedAtUtc: null }))
      .toBe('Evidence is stale.');
  });
});
