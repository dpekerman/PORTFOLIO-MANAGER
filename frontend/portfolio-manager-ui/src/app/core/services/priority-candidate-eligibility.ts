import { ActionScoreDto, RsiScanResult, ValueScreenerResult } from '../models/portfolio.models';
import { DecisionEngineService, WatchlistValueContext } from './decision-engine.service';

/** Canonical Watchlist Final Actions that qualify for new-capital deployment. */
const ELIGIBLE_FINAL_ACTIONS = new Set<string>([
  'ENTRY CANDIDATE',
  'STARTER ENTRY',
  'BUY WATCH',
  'REVERSAL WATCH',
]);

const FINAL_ACTION_REASON: Record<string, string> = {
  'WATCH / NO CHASE': 'FINAL_ACTION_WATCH_NO_CHASE',
  'WAIT FOR PULLBACK': 'FINAL_ACTION_WAIT_FOR_PULLBACK',
  'WAIT FOR REVERSAL': 'FINAL_ACTION_WAIT_FOR_REVERSAL',
  'WAIT FOR RECLAIM': 'FINAL_ACTION_WAIT_FOR_RECLAIM',
  AVOID: 'FINAL_ACTION_AVOID',
  WATCH: 'FINAL_ACTION_WATCH',
};

const SCORE_THRESHOLD_WATCH = 50;
const SCORE_THRESHOLD_HIGH = 75;

export type PriorityLevel = 'HIGH' | 'WATCH' | 'NONE';

export interface PriorityCandidateEligibility {
  eligible: boolean;
  finalAction: string | null;
  priorityLevel: PriorityLevel;
  reasonExcluded: string[];
}

/**
 * Gates a scored watchlist candidate against the canonical Watchlist Final Action
 * (reused as-is from DecisionEngineService — never re-derived here) plus the score
 * threshold. Eligibility ranks qualified opportunities; it never qualifies unready ones.
 */
export function resolvePriorityCandidateEligibility(
  score: ActionScoreDto,
  scan: RsiScanResult | undefined,
  valueResult: ValueScreenerResult | undefined,
  engine: DecisionEngineService,
): PriorityCandidateEligibility {
  if (!scan) {
    return {
      eligible: false,
      finalAction: null,
      priorityLevel: 'NONE',
      reasonExcluded: ['MISSING_TECHNICAL_DATA'],
    };
  }

  const valueCtx: WatchlistValueContext = {
    valueScore: valueResult?.score ?? null,
    valueTrapWarning: valueResult?.actionTrigger === 'ValueTrapWarning',
  };
  const decision = engine.translateForWatchlist(scan, score.holdingRole, valueCtx);
  const finalAction = decision.finalAction;
  const hardNegative = decision.watchlistDiagnostics?.hasHardStructuralNegative === true;

  const reasonExcluded: string[] = [];
  if (hardNegative) reasonExcluded.push('HARD_NEGATIVE_STRUCTURE');
  if (!ELIGIBLE_FINAL_ACTIONS.has(finalAction)) {
    reasonExcluded.push(FINAL_ACTION_REASON[finalAction] ?? 'FINAL_ACTION_INELIGIBLE');
  }
  if (score.totalScore < SCORE_THRESHOLD_WATCH) reasonExcluded.push('SCORE_BELOW_THRESHOLD');

  const eligible = reasonExcluded.length === 0;
  const priorityLevel: PriorityLevel = !eligible
    ? 'NONE'
    : score.totalScore >= SCORE_THRESHOLD_HIGH
      ? 'HIGH'
      : 'WATCH';

  return { eligible, finalAction, priorityLevel, reasonExcluded };
}
