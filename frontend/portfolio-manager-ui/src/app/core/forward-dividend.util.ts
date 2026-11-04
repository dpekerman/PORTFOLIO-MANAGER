import { StockQuote } from './models/portfolio.models';

export type ForwardDividendMetric = 'perShare' | 'yield';

export interface ForwardDividendView {
  perShare: number | null;
  yieldPct: number | null;
  /** Positive, Zero, Missing, Stale or Inconsistent; Missing when the quote carries no dividend evidence. */
  status: string;
  tooltip: string;
}

const STATUS_TEXT: Record<string, string> = {
  Missing: 'No forward dividend is reported by the data provider. This is unknown, not zero.',
  Stale: 'The provider’s latest dividend date is too old to treat the rate as a forward estimate.',
  Inconsistent: 'The provider’s dividend rate, yield or currency could not be reconciled, so no estimate is shown.',
};

/**
 * Forward dividend facts from the cached quote. Values are estimates of future distributions, not guaranteed payments.
 * Missing, stale and inconsistent data stay null (never 0); only a provider-reported zero rate is a confirmed zero.
 */
export function forwardDividendOf(quote: StockQuote | null | undefined): ForwardDividendView {
  const status = quote?.dividendStatus ?? 'Missing';
  const usable = status === 'Positive' || status === 'Zero';
  const perShare = usable ? (quote?.forwardDividendPerShare ?? null) : null;
  const yieldPct = usable ? (quote?.forwardYieldPct ?? null) : null;
  return { perShare, yieldPct, status, tooltip: dividendTooltip(quote, status, perShare) };
}

/** Numeric value for sorting; null (missing) must sort last in either direction. */
export function forwardSortValue(quote: StockQuote | null | undefined, metric: ForwardDividendMetric): number | null {
  const v = forwardDividendOf(quote);
  return metric === 'perShare' ? v.perShare : v.yieldPct;
}

/** Comparator for nullable numbers that keeps missing values last whatever the direction. */
export function compareMissingLast(a: number | null, b: number | null, dir: 1 | -1): number {
  if (a === null || b === null) return a === b ? 0 : a === null ? 1 : -1;
  return (a - b) * dir;
}

function dividendTooltip(quote: StockQuote | null | undefined, status: string, perShare: number | null): string {
  if (perShare === null) return STATUS_TEXT[status] ?? STATUS_TEXT['Missing'];
  const parts = [
    status === 'Zero'
      ? 'The provider reports a zero forward dividend.'
      : 'Estimate of the annual forward dividend per share; not a guaranteed payment.',
    `Source: ${quote?.dividendSource ?? 'market data provider'}.`,
  ];
  parts.push(
    quote?.dividendDateUtc
      ? `Latest dividend date ${quote.dividendDateUtc.slice(0, 10)}. The provider gives no time for when this estimate was last updated.`
      : 'The provider gives no time for when this estimate was last updated.',
  );
  return parts.join(' ');
}
