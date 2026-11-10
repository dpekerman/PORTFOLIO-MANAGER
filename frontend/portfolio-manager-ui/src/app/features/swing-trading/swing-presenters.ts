import { RiskStatus, SwingTradeRow } from '../../core/models/swing-trading.models';
import { priceBasisLabel, riskStatusLabel } from './swing-trading-logic';

/**
 * Presentation mappers for the Swing grids. They only translate fields the backend already evaluated
 * (PlannedStopState, coverageState, riskStatuses, priceBasis) into labels and explanations; no price is compared here.
 */
export type Money = (value: number | null) => string;

export function asOfLabel(iso: string | null): string {
  return iso ? `${iso.slice(0, 16).replace('T', ' ')} UTC` : 'unknown time';
}

// ── Price ─────────────────────────────────────────────────────────────────────────────────

export interface PriceView {
  /** Only stale or unavailable quotes keep a visible chip; Live/EOD basis moves to the tooltip. */
  warning: string | null;
  tooltip: string;
}

const PRICE_BASIS_DETAIL: Record<string, string> = {
  Live: 'Live quote',
  EOD: 'End-of-day close (not a live intraday quote)',
  Stale: 'Stale quote: not used to check stops',
  Unavailable: 'No price available',
};

export function priceView(row: SwingTradeRow): PriceView {
  const stale = row.priceBasis === 'Stale' || row.priceBasis === 'Unavailable';
  const basis = PRICE_BASIS_DETAIL[row.priceBasis] ?? priceBasisLabel(row.priceBasis);
  const asOf = row.priceAsOfUtc ? ` · as of ${asOfLabel(row.priceAsOfUtc)}` : '';
  return { warning: stale ? priceBasisLabel(row.priceBasis) : null, tooltip: `${basis}${asOf} · ${row.priceSource}` };
}

// ── Planned Stop ──────────────────────────────────────────────────────────────────────────

export interface PlannedStopView {
  state: SwingTradeRow['plannedStopState'];
  /** Short visible label; null when the cell only needs the number. */
  text: string | null;
  icon: 'error' | 'schedule' | null;
  /** Full explanation for the tooltip, the details popover and screen readers. */
  detail: string;
}

export function plannedStopView(row: SwingTradeRow, money: Money): PlannedStopView {
  const stop = money(row.plannedStop);
  const price = money(row.currentPrice);
  const when = asOfLabel(row.priceAsOfUtc);
  switch (row.plannedStopState) {
    case 'Breached':
      return {
        state: 'Breached', text: 'Breached', icon: 'error',
        detail: `Planned Stop ${stop}. Latest usable observed price ${price} (${row.priceBasis}) as of ${when}. Result: BREACHED. `
          + 'This is a market observation only; it does not show that a broker order triggered or filled, or that the position was sold.',
      };
    case 'Unverified':
      return {
        state: 'Unverified', text: 'Unverified', icon: 'schedule',
        detail: `Planned Stop ${stop}. The latest price is ${row.priceBasis === 'Stale' ? 'stale' : 'unavailable'}`
          + `${row.currentPrice === null ? '' : ` (last known ${price} as of ${when})`}, so a breach can be neither confirmed nor ruled out.`,
      };
    case 'Clear':
      return { state: 'Clear', text: null, icon: null, detail: `Planned Stop ${stop}. Latest usable observed price ${price} as of ${when} is above it.` };
    default:
      return { state: 'NoPlan', text: null, icon: null, detail: 'No Planned Stop is set for this trade.' };
  }
}

// ── Target ────────────────────────────────────────────────────────────────────────────────

export type TargetState = 'Reached' | 'NotReached' | 'Unverified' | 'Unavailable';

export interface TargetView {
  state: TargetState;
  /** Signed % from the current price to the target; null unless the price is a usable (Live/EOD) observation. */
  pct: number | null;
  /** Short visible label for Reached / Unverified only. */
  text: string | null;
  icon: 'flag' | 'schedule' | null;
  detail: string;
}

const hasUsablePrice = (row: SwingTradeRow): boolean =>
  row.currentPrice !== null && Number.isFinite(row.currentPrice) && row.currentPrice > 0
  && (row.priceBasis === 'Live' || row.priceBasis === 'EOD');

/**
 * Long-only (Swing positions are long). The target is the one saved on the user's Risk Plan; a plan carried over from a
 * prior trade, or a non-positive/invalid target, is treated as unavailable. This is a market observation only: it never
 * implies an order was placed or filled.
 */
export function targetView(row: SwingTradeRow, money: Money): TargetView {
  const target = row.targetPrice;
  if (target === null || !Number.isFinite(target) || target <= 0 || row.planFromPriorEpisode) {
    return { state: 'Unavailable', pct: null, text: null, icon: null, detail: 'No Target Price is set for this trade.' };
  }
  const t = money(target);
  const when = asOfLabel(row.priceAsOfUtc);
  if (!hasUsablePrice(row)) {
    const last = row.currentPrice === null ? '' : ` (last known ${money(row.currentPrice)} as of ${when})`;
    return {
      state: 'Unverified', pct: null, text: 'Unverified', icon: 'schedule',
      detail: `Target ${t}. The latest price is ${row.priceBasis === 'Stale' ? 'stale' : 'unavailable'}${last}, so reaching the target can be neither confirmed nor ruled out.`,
    };
  }
  const price = row.currentPrice as number;
  const pct = ((target - price) / price) * 100;
  const observed = `Latest usable observed price ${money(price)} (${row.priceBasis}) as of ${when}, ${row.priceSource}.`;
  if (price >= target) {
    return {
      state: 'Reached', pct, text: 'Target reached', icon: 'flag',
      detail: `Target ${t}. ${observed} Result: TARGET REACHED. This is a market observation only; it does not show that a sell order was placed or filled, or that the position was sold.`,
    };
  }
  return { state: 'NotReached', pct, text: null, icon: null, detail: `Target ${t}. ${observed} The price is below the target.` };
}

// ── Broker stop / protection ──────────────────────────────────────────────────────────────

export interface StatusView {
  label: string;
  tone: 'ok' | 'warn' | 'bad';
  detail: string;
}

const allVerified = (row: SwingTradeRow): boolean =>
  row.brokerStops.length > 0 && row.brokerStops.every((s) => s.brokerVerificationStatus === 'Verified');

function coverageDetail(row: SwingTradeRow): string {
  const covered = row.coveredQuantity === null ? 'unknown' : String(row.coveredQuantity);
  const orders = row.brokerStops.length;
  return `Covered quantity ${covered} of ${row.totalQuantity} (${row.coverageState}). `
    + (orders === 0
      ? 'No broker stop order is recorded.'
      : `${orders} user-recorded broker stop${orders === 1 ? '' : 's'}; ${allVerified(row) ? 'verified' : 'not verified with the broker'}.`);
}

export function brokerStopView(row: SwingTradeRow): StatusView {
  if (row.brokerStops.length === 0) {
    return row.plannedStop !== null
      ? { label: 'Planned only', tone: 'warn', detail: 'Only an alert-only Planned Stop exists. No broker stop order is recorded.' }
      : { label: 'Missing', tone: 'bad', detail: 'No broker stop order is recorded.' };
  }
  const detail = `${row.brokerOrderType ?? 'Broker'} order recorded by you. ${coverageDetail(row)}`;
  switch (row.coverageState) {
    case 'Partial': return { label: 'Partial', tone: 'warn', detail };
    case 'Unknown': return { label: 'Unknown', tone: 'warn', detail };
    case 'Ambiguous': return { label: 'Unknown', tone: 'warn', detail: `${detail} The account also holds non-Swing lots, so the order cannot be allocated to this exposure.` };
    case 'OverCovered': return { label: 'Over-covered', tone: 'warn', detail };
    default: return allVerified(row) ? { label: 'Recorded', tone: 'ok', detail } : { label: 'Recorded (unverified)', tone: 'warn', detail };
  }
}

export function protectionView(row: SwingTradeRow): StatusView {
  const detail = `${row.protection}. ${coverageDetail(row)}`;
  switch (row.coverageState) {
    case 'None':
      return row.plannedStop !== null
        ? { label: 'Planned only', tone: 'warn', detail }
        : { label: 'None', tone: 'bad', detail };
    case 'Partial': return { label: 'Partial', tone: 'warn', detail };
    case 'Unknown': return { label: 'Unknown', tone: 'warn', detail };
    case 'Ambiguous': return { label: 'Ambiguous', tone: 'warn', detail };
    case 'OverCovered': return { label: 'Over-covered', tone: 'warn', detail };
    default: return allVerified(row) ? { label: 'Covered', tone: 'ok', detail } : { label: 'Covered (unverified)', tone: 'warn', detail };
  }
}

// ── Risk ──────────────────────────────────────────────────────────────────────────────────

/** Most material first. */
export const RISK_PRIORITY: readonly RiskStatus[] = [
  'StopBreached', 'ApproachingStop', 'StaleEvidence', 'NoBrokerProtection', 'PartialProtection',
  'OverCovered', 'UnknownCoverage', 'AmbiguousCoverage', 'StopMismatch', 'MissingRiskPlan',
];
export const RISK_VISIBLE_LIMIT = 2;

export interface RiskView {
  visible: RiskStatus[];
  hidden: RiskStatus[];
}

export function riskView(row: SwingTradeRow): RiskView {
  const rank = (s: RiskStatus) => {
    const i = RISK_PRIORITY.indexOf(s);
    return i < 0 ? RISK_PRIORITY.length : i;
  };
  const sorted = [...row.riskStatuses].sort((a, b) => rank(a) - rank(b));
  // Hiding a single status behind "+1" saves no space.
  const limit = sorted.length <= RISK_VISIBLE_LIMIT + 1 ? sorted.length : RISK_VISIBLE_LIMIT;
  return { visible: sorted.slice(0, limit), hidden: sorted.slice(limit) };
}

export function riskSummaryLabel(row: SwingTradeRow): string {
  return row.riskStatuses.length === 0 ? 'No risk flags' : row.riskStatuses.map(riskStatusLabel).join(', ');
}

export type RiskTone = 'bad' | 'warn' | 'dim';

export interface CompactRisk {
  label: 'CRITICAL' | 'HIGH' | 'MEDIUM' | 'LOW' | 'UNKNOWN';
  /** Existing severity class (sev-*), so colours stay shared with the rest of Swing Trading. */
  severity: SwingTradeRow['severity'];
  detail: string;
}

/** Maps the backend severity to one compact word; evidence the backend could not verify shows UNKNOWN, never LOW. */
export function compactRisk(row: SwingTradeRow): CompactRisk {
  const flags = riskSummaryLabel(row);
  const detail = `Severity ${row.severity}. ${flags}.`;
  const unverified = row.riskStatuses.includes('StaleEvidence') || row.priceBasis === 'Stale' || row.priceBasis === 'Unavailable';
  switch (row.severity) {
    case 'Critical': return { label: 'CRITICAL', severity: row.severity, detail };
    case 'High': return { label: 'HIGH', severity: row.severity, detail };
    case 'Medium': return { label: 'MEDIUM', severity: row.severity, detail };
    default:
      return unverified
        ? { label: 'UNKNOWN', severity: row.severity, detail: `${detail} Price evidence is stale or unavailable, so the risk is unverified.` }
        : { label: 'LOW', severity: row.severity, detail };
  }
}

/** Visual weight only: a breach is red, the other material statuses amber, bookkeeping gaps muted. */
export function riskTone(status: RiskStatus): RiskTone {
  if (status === 'StopBreached') return 'bad';
  const i = RISK_PRIORITY.indexOf(status);
  return i >= 0 && i <= RISK_PRIORITY.indexOf('PartialProtection') ? 'warn' : 'dim';
}
