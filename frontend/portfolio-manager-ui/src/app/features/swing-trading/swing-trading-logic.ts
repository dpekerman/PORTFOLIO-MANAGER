import { ExportCell, TxExportColumn } from '../transactions/transactions-logic';
import {
  BrokerStop,
  CoverageState,
  JournalRow,
  ManageRiskDialogData,
  RiskPosition,
  RiskSeverity,
  RiskStatus,
  SwingTradeRow,
} from '../../core/models/swing-trading.models';

export interface SwingFilters {
  account: string;
  search: string;
  protection: string;
  risk: RiskStatus | '';
  finalAction: string;
  attentionOnly: boolean;
}

export const EMPTY_SWING_FILTERS: SwingFilters = {
  account: '',
  search: '',
  protection: '',
  risk: '',
  finalAction: '',
  attentionOnly: false,
};

export function aggregateFinalActions(actions: readonly (string | null | undefined)[]): string | null {
  if (!actions.some(Boolean)) return null;
  if (actions.some((action) => !action)) return 'Incomplete Actions / Review Lots';
  return new Set(actions).size === 1 ? actions[0] ?? null : 'Mixed Actions / Review Lots';
}

export const RISK_STATUS_LABELS: Record<RiskStatus, string> = {
  StopBreached: 'At/below stop',
  ApproachingStop: 'Approaching stop',
  MissingRiskPlan: 'No risk plan',
  NoBrokerProtection: 'No broker stop',
  PartialProtection: 'Partial coverage',
  UnknownCoverage: 'Coverage unknown',
  AmbiguousCoverage: 'Coverage ambiguous',
  OverCovered: 'Over-covered',
  StopMismatch: 'Stop mismatch',
  StaleEvidence: 'Stale / no price',
};

export const COVERAGE_LABELS: Record<CoverageState, string> = {
  Unknown: 'Unknown',
  None: 'None',
  Partial: 'Partial',
  Full: 'Full (unverified)',
  OverCovered: 'Over-covered',
  Ambiguous: 'Ambiguous (mixed roles)',
};

export const PROTECTION_OPTIONS = [
  'Broker Stop Recorded',
  'Partially Covered',
  'Alert-Only Stop (no broker order)',
  'No Stop',
  'Fully Protected',
];

export function riskStatusLabel(status: RiskStatus): string {
  return RISK_STATUS_LABELS[status] ?? status;
}

export function severityClass(severity: RiskSeverity): string {
  return `sev-${severity.toLowerCase()}`;
}

export function priceBasisLabel(basis: string): string {
  switch (basis) {
    case 'Live':
      return 'Live';
    case 'EOD':
      return 'EOD close';
    case 'Stale':
      return 'Stale';
    default:
      return 'No price';
  }
}

/** A row's search text is symbol + company + account, so "TFSA" or "disney" both find it. */
export function matchesSwingFilters(
  row: SwingTradeRow,
  f: SwingFilters,
  finalActionOf: (row: SwingTradeRow) => string | null,
): boolean {
  if (f.account && row.account !== f.account) return false;
  const q = f.search.trim().toLowerCase();
  if (q && !`${row.symbol} ${row.companyName} ${row.account}`.toLowerCase().includes(q)) return false;
  if (f.protection && row.protection !== f.protection) return false;
  if (f.risk && !row.riskStatuses.includes(f.risk)) return false;
  if (f.finalAction && (finalActionOf(row) ?? '') !== f.finalAction) return false;
  if (f.attentionOnly && !row.needsAttention) return false;
  return true;
}

export function filterSwingRows(
  rows: readonly SwingTradeRow[],
  f: SwingFilters,
  finalActionOf: (row: SwingTradeRow) => string | null,
): SwingTradeRow[] {
  return rows.filter((r) => matchesSwingFilters(r, f, finalActionOf));
}

export function distinctSorted(values: Iterable<string | null | undefined>): string[] {
  return [...new Set([...values].filter((v): v is string => !!v))].sort((a, b) => a.localeCompare(b));
}

// ── Manage Risk validation ──────────────────────────────────────────────────────────────────

export interface ValidationResult {
  errors: Record<string, string>;
  warnings: string[];
}

const hasValidScale = (v: number): boolean => Math.abs(Math.round(v * 10_000) / 10_000 - v) < 1e-9;

function priceError(value: number | null, label: string, required: boolean): string | null {
  if (value === null || Number.isNaN(value)) return required ? `${label} is required.` : null;
  if (!(value > 0)) return `${label} must be a positive price.`;
  if (!hasValidScale(value)) return `${label} supports at most 4 decimal places.`;
  return null;
}

export interface RiskPlanValues {
  plannedStopPrice: number | null;
  targetPrice: number | null;
  stopType: 'Fixed' | 'Trailing';
}

/** Long positions only: a stop at/above the latest valid price would trigger immediately, so it only warns. */
export function validateRiskPlan(
  v: RiskPlanValues,
  ctx: { currentPrice: number | null; priceUsable: boolean },
): ValidationResult {
  const errors: Record<string, string> = {};
  const warnings: string[] = [];
  const stopErr = priceError(v.plannedStopPrice, 'Planned stop', false);
  if (stopErr) errors['plannedStopPrice'] = stopErr;
  const targetErr = priceError(v.targetPrice, 'Target price', false);
  if (targetErr) errors['targetPrice'] = targetErr;

  if (!stopErr && v.plannedStopPrice !== null && ctx.priceUsable && ctx.currentPrice !== null) {
    if (v.plannedStopPrice >= ctx.currentPrice)
      warnings.push(
        `The planned stop (${v.plannedStopPrice}) is at or above the latest price (${ctx.currentPrice}). For a long position it would already be breached.`,
      );
  }
  if (!targetErr && v.targetPrice !== null && v.plannedStopPrice !== null && v.targetPrice <= v.plannedStopPrice)
    warnings.push('The target price is at or below the planned stop.');
  if (v.stopType === 'Trailing')
    warnings.push('Trailing is recorded as a label only; Portfolio Manager does not move the stop automatically.');
  return { errors, warnings };
}

export interface BrokerStopValues {
  orderType: 'StopMarket' | 'StopLimit' | 'Other';
  triggerPrice: number | null;
  limitPrice: number | null;
  coveredQuantity: number | null;
  allowOverCoverage: boolean;
  reason: string;
}

export function validateBrokerStop(
  v: BrokerStopValues,
  ctx: { totalQuantity: number; currentPrice: number | null; priceUsable: boolean },
): ValidationResult {
  const errors: Record<string, string> = {};
  const warnings: string[] = [];
  const trigErr = priceError(v.triggerPrice, 'Trigger price', true);
  if (trigErr) errors['triggerPrice'] = trigErr;
  if (v.orderType === 'StopLimit') {
    const limErr = priceError(v.limitPrice, 'Limit price', false);
    if (limErr) errors['limitPrice'] = limErr;
  }
  if (v.coveredQuantity !== null && !Number.isNaN(v.coveredQuantity)) {
    if (!(v.coveredQuantity > 0)) errors['coveredQuantity'] = 'Covered quantity must be positive, or blank when unknown.';
    else if (v.coveredQuantity > ctx.totalQuantity) {
      if (!v.allowOverCoverage || !v.reason.trim())
        errors['coveredQuantity'] =
          `Exceeds the account quantity (${ctx.totalQuantity}). Tick "unusual case" and give a reason to keep it.`;
      else warnings.push('Covered quantity exceeds the account quantity; saved as an unusual case.');
    }
  }
  if (!trigErr && v.triggerPrice !== null && ctx.priceUsable && ctx.currentPrice !== null && v.triggerPrice >= ctx.currentPrice)
    warnings.push(
      `The trigger (${v.triggerPrice}) is at or above the latest price (${ctx.currentPrice}); for a long position the order would trigger immediately.`,
    );
  return { errors, warnings };
}

export function hasErrors(r: ValidationResult): boolean {
  return Object.keys(r.errors).length > 0;
}

/**
 * Display-only coverage for the dialog (the Risk Monitor uses the backend evaluator). Orders from a previous trade
 * episode or already inactive never count; unknown quantities are never treated as zero or as full coverage.
 */
export function summarizeCoverage(p: RiskPosition): { state: CoverageState; covered: number | null; active: BrokerStop[] } {
  const active = p.brokerStops.filter((s) => s.isActive && s.belongsToCurrentEpisode);
  if (active.length === 0) return { state: 'None', covered: null, active };
  if (p.mixedRole) return { state: 'Ambiguous', covered: null, active };
  if (active.some((s) => s.coveredQuantity === null)) return { state: 'Unknown', covered: null, active };
  const covered = active.reduce((sum, s) => sum + (s.coveredQuantity ?? 0), 0);
  const state: CoverageState =
    covered > p.totalQuantity ? 'OverCovered' : covered === p.totalQuantity ? 'Full' : 'Partial';
  return { state, covered, active };
}

/** Plain-language state of one recorded order for the dialog and tooltips. */
export function brokerStopStatusLabel(s: BrokerStop): string {
  switch (s.userStatus) {
    case 'UserConfirmedEstablished':
      return 'User-confirmed established';
    case 'UserReportedCancelled':
      return 'User reported cancelled';
    case 'UserReportedFilled':
      return 'User reported filled';
    case 'Superseded':
      return 'Replaced';
    default:
      return s.brokerCancellationUnverified ? 'Trade closed — verify at broker' : 'Trade closed';
  }
}

// ── Export ──────────────────────────────────────────────────────────────────────────────────

const col = (header: string, kind: TxExportColumn['kind'] = 'text'): TxExportColumn => ({ header, kind });
const num = (v: number | null | undefined): number | null => (v === null || v === undefined ? null : v);
const day = (iso: string | null | undefined): string | null => (iso ? iso.substring(0, 10) : null);

/** The last cell of every export row is its currency code (shared sheet-formatting convention). */
export const OPEN_TRADES_EXPORT_COLUMNS: readonly TxExportColumn[] = [
  col('Symbol'),
  col('Account'),
  col('Investment Role'),
  col('Swing Quantity', 'qty'),
  col('Total Account Quantity', 'qty'),
  col('Opening Date', 'date'),
  col('Days Held', 'int'),
  col('Episode Status'),
  col('Average Cost', 'money'),
  col('Current Price', 'money'),
  col('Price As Of', 'date'),
  col('Price Basis'),
  col('Market Value', 'money'),
  col('Unrealized P/L', 'money'),
  col('Unrealized P/L %', 'pct'),
  col('Planned Stop', 'money'),
  col('Stop Type'),
  col('Target', 'money'),
  col('Broker Stop Trigger', 'money'),
  col('Broker Order Type'),
  col('Broker-Recorded Status'),
  col('Broker Verification'),
  col('Covered Quantity', 'qty'),
  col('Coverage Status'),
  col('Protection'),
  col('Distance To Stop', 'money'),
  col('Distance To Stop %', 'pct'),
  col('Risk State'),
  col('Risk Severity'),
  col('Final Action'),
  col('Decision Source'),
  col('Advisory Threshold %', 'pct'),
  col('Currency'),
];

export function openTradesExportRows(
  rows: readonly SwingTradeRow[],
  finalActionOf: (row: SwingTradeRow) => string | null,
  thresholdPct: number,
): ExportCell[][] {
  return rows.map((r) => [
    r.symbol,
    r.account,
    'Swing',
    r.swingQuantity,
    r.totalQuantity,
    day(r.episodeOpenDate),
    r.daysHeld,
    r.episodeResolution,
    r.averageCost,
    num(r.currentPrice),
    day(r.priceAsOfUtc),
    r.priceBasis,
    num(r.marketValue),
    num(r.unrealizedPnl),
    num(r.unrealizedPnlPct),
    num(r.plannedStop),
    r.stopType,
    num(r.targetPrice),
    num(r.brokerStopTrigger),
    r.brokerOrderType,
    r.brokerStatus,
    r.brokerVerification,
    // Unknown coverage is exported blank with an explicit status, never as 0 or full.
    num(r.coveredQuantity),
    r.coverageState,
    r.protection,
    num(r.distanceAbs),
    num(r.distancePct),
    r.riskStatuses.map(riskStatusLabel).join('; '),
    r.severity,
    finalActionOf(r),
    r.decisionSource,
    thresholdPct,
    r.currency,
  ]);
}

export const JOURNAL_EXPORT_COLUMNS: readonly TxExportColumn[] = [
  col('Symbol'),
  col('Account'),
  col('Opening Date', 'date'),
  col('Final Closing Date', 'date'),
  col('Days Held', 'int'),
  col('Closed Quantity', 'qty'),
  col('Entry Price', 'money'),
  col('Exit Price', 'money'),
  col('Realized P/L', 'money'),
  col('Realized P/L %', 'pct'),
  col('Outcome'),
  col('Episode Status'),
  col('Original Decision Source'),
  col('Closing Decision Source'),
  col('Thesis'),
  col('Notes'),
  col('Currency'),
];

export function journalExportRows(rows: readonly JournalRow[]): ExportCell[][] {
  return rows.map((r) => [
    r.symbol,
    r.account,
    day(r.openDate),
    day(r.finalCloseDate),
    r.daysHeld,
    r.closedQuantity,
    num(r.entryPrice),
    num(r.exitPrice),
    num(r.realizedPnl),
    num(r.realizedPnlPct),
    r.outcome,
    r.episodeResolution,
    r.originalDecisionSource,
    r.closingDecisionSource,
    r.thesis,
    r.notes,
    r.currency,
  ]);
}

/** Identity only: account and Swing facts come from the shared risk-position API. */
export function manageRiskDataFor(row: SwingTradeRow): ManageRiskDialogData {
  return {
    account: row.account,
    symbol: row.symbol,
    companyName: row.companyName,
    role: 'Swing',
  };
}

export function compareNullable(a: string | number | null, b: string | number | null): number {
  if (a === null && b === null) return 0;
  if (a === null) return 1;
  if (b === null) return -1;
  return typeof a === 'number' && typeof b === 'number' ? a - b : String(a).localeCompare(String(b));
}

export function downloadTextFile(filename: string, text: string, mime = 'text/csv;charset=utf-8;'): void {
  const blob = new Blob([text], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
