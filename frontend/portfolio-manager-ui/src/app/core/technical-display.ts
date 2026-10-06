export function formatTrendShift(value: string | null | undefined, fallback = 'Waiting'): string {
  const cleaned = (value ?? '')
    .replace(/^\?\?\s*/, '')
    .replace(/^[\p{Emoji}\s]+/u, '')
    .trim();

  return cleaned || fallback;
}

export type MarketCurrencyCode = 'CAD' | 'USD';

export function currencyCodeForTradingSymbol(symbol: string | null | undefined): MarketCurrencyCode {
  return (symbol ?? '').trim().toUpperCase().endsWith('.TO') ? 'CAD' : 'USD';
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
