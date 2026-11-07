/** Presentation helpers shared by the automation cards. */

export function automationStatusClass(status: string | undefined | null): string {
  switch (status) {
    case 'Running':
      return 'status-badge--running';
    case 'Success':
      return 'status-badge--success';
    case 'Failed':
      return 'status-badge--failed';
    case 'Cancelled':
      return 'status-badge--cancelled';
    case 'SkippedNonTradingDay':
      return 'status-badge--skipped';
    default:
      return 'status-badge--partial';
  }
}

export function valueScreenerStatusLabel(status: string | null | undefined): string {
  return status === 'NotObserved' ? 'Scheduled separately' : status || 'pending';
}

/**
 * FixMissingData/TestWake runs never call the market-data refresh step by design (they only
 * recover EOD signals/snapshot/value screener or run a non-destructive infra check) — show that
 * distinction instead of a bare "—" so it isn't mistaken for a failure.
 */
export function refreshStatusLabel(run: { refreshStatus: string; triggerType: string }): string {
  if (run.refreshStatus) return run.refreshStatus;
  return run.triggerType === 'FixMissingData' || run.triggerType === 'TestWake'
    ? 'N/A (not part of this trigger)'
    : 'pending';
}
