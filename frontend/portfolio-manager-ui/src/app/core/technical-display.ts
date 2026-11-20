import { FibonacciSwingEvidence } from './models/portfolio.models';

export function listingFibonacci(
  symbol: string,
  evidence: FibonacciSwingEvidence | null | undefined,
  currency?: string,
): FibonacciSwingEvidence | null {
  return evidence && evidence.symbol.toUpperCase() === symbol.toUpperCase()
    && (!currency || evidence.currency === currency)
    && evidence.swingLow > 0 && evidence.swingHigh > evidence.swingLow ? evidence : null;
}

export function fibonacciRange(
  fib: FibonacciSwingEvidence | null | undefined,
  mask: (value: number) => number = (value) => value,
): string {
  return fib ? `${fibPrice(fib.swingLow, fib.currency, mask)} → ${fibPrice(fib.swingHigh, fib.currency, mask)}` : 'N/A';
}

export function fibonacciDetails(
  fib: FibonacciSwingEvidence | null | undefined,
  mask: (value: number) => number,
): string {
  if (!fib) return 'Verified trading-listing Fibonacci evidence is unavailable.';
  const price = (value: number) => fibPrice(value, fib.currency, mask);
  return `${fib.symbol}: Low ${fib.lowDate}; High ${fib.highDate} · 38.2% ${price(fib.fib38_2)} · 50% ${price(fib.fib50)} · 61.8% ${price(fib.fib61_8)} · 78.6% ${price(fib.fib78_6)}`;
}

function fibPrice(value: number, currency: string, mask: (value: number) => number): string {
  const prefix = currency === 'CAD' ? 'C$' : currency === 'USD' ? 'US$' : `${currency} `;
  return `${prefix}${mask(value).toFixed(2)}`;
}

export function formatTrendShift(value: string | null | undefined, fallback = 'Waiting'): string {
  const cleaned = (value ?? '')
    .replace(/^\?\?\s*/, '')
    .replace(/^[\p{Emoji}\s]+/u, '')
    .trim();

  return cleaned || fallback;
}

export type MarketCurrencyCode = 'CAD' | 'USD';

export function currencyCodeForTradingSymbol(symbol: string | null | undefined): MarketCurrencyCode {
  return /\.(TO|V|CN|NE)$/.test((symbol ?? '').trim().toUpperCase()) ? 'CAD' : 'USD';
}

export function currencyCodeForAnalysis(
  symbol: string | null | undefined,
  analysisCurrency?: string | null,
): MarketCurrencyCode {
  const normalized = analysisCurrency?.trim().toUpperCase();
  return normalized === 'CAD' || normalized === 'USD'
    ? normalized
    : currencyCodeForTradingSymbol(symbol);
}
