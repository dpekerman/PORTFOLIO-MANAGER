import { FinalActionResult } from '../../core/models/portfolio.models';

/** Plain-language labels for the machine reason codes the backend attaches to a Final Action. */
const REASON_LABELS: Record<string, string> = {
  TECHNICAL_ONLY_NO_PORTFOLIO_TRIGGER: 'Technical signal only — no portfolio-level trigger',
  EVIDENCE_NOT_POST_ENTRY: 'Signal was evaluated on or before the entry date',
  ENTRY_DATE_UNKNOWN: 'Entry date not recorded',
  ALLOCATION_LIMIT_BREACH: 'Position is above its configured limit',
  POST_ENTRY_LOSS: 'Price is below cost after entry',
  CONFIRMED_STRUCTURAL_BREAKDOWN: 'Confirmed structural breakdown',
  TECHNICAL_EXIT_POST_ENTRY: 'Technical exit confirmed after entry',
  ADD_BLOCKED_POSITION_LIMIT: 'Add blocked: position at or above its limit',
  ADD_BLOCKED_SECTOR_OVER: 'Add blocked: sector above its target',
  ADD_ROLE_POLICY_AMBIGUOUS: 'Add needs review: the role policy is ambiguous',
  ADD_ELIGIBLE: 'Add eligible within position and sector limits',
  PROFIT_TAKING_RULE: 'Profit-taking rule',
  NO_ADD_TECHNICAL_COOLING: 'No add: momentum is cooling',
  NO_ADD_OVERBOUGHT: 'No add: extended after an overbought run',
  NO_ADD_TECHNICAL: 'No add: technical setup does not support buying',
  WARNING_NOT_INSTRUCTION: 'Warning only — not an instruction to trade',
  AWAITING_NO_EVALUATION: 'No evaluation is available yet',
  AWAITING_STALE_DATA: 'Market data is older than the last completed session',
  AWAITING_REFRESH_FAILED: 'The market-data refresh failed',
  AWAITING_INVALID_PRICE: 'The price is missing or invalid',
  EVALUATION_INTERRUPTED: 'The evaluation was interrupted',
  REVALIDATION_MISSED: 'The latest evaluation did not re-confirm this recommendation',
  LIFECYCLE_WINDOW_ELAPSED: 'The recommendation window has elapsed',
  QUOTE_OLDER_THAN_SCAN: 'The source quote is older than the scan',
};

export function describeReasonCode(code: string): string {
  if (REASON_LABELS[code]) return REASON_LABELS[code];
  if (code.startsWith('AWAITING_REQUIRED_')) {
    return `Required input unavailable: ${code.slice('AWAITING_REQUIRED_'.length).toLowerCase().replace(/_/g, ' ')}`;
  }
  return code;
}

export function stateLabel(state: string | null | undefined): string {
  switch (state) {
    case 'CurrentValid':
      return 'Current evaluation';
    case 'PriorSessionValid':
      return 'Valid prior-session recommendation';
    case 'AwaitingData':
      return 'Awaiting Data';
    case 'Evaluating':
      return 'Evaluating';
    case 'HistoricalWarning':
      return 'Unresolved historical warning';
    case 'Expired':
      return 'Expired';
    default:
      return 'Current evaluation';
  }
}

const SELL_LIKE_TECHNICALS = ['Active Sell Trigger', 'Breakdown', 'Bearish Shift', 'Warning'];

/**
 * A technical status can look bearish while the portfolio decision is a Hold. Explains the difference instead of
 * rewriting the indicator. Null when there is no apparent conflict.
 */
export function technicalConflictNote(r: FinalActionResult): string | null {
  const sellLike = SELL_LIKE_TECHNICALS.some((t) => r.momentumShift?.startsWith(t));
  if (!sellLike || r.category === 'TrimExit') return null;
  if (r.finalAction === 'Awaiting Data') return null;
  return 'The technical status above is a market reading, not a portfolio decision. No post-entry loss, confirmed breakdown or allocation breach supports selling this holding.';
}
