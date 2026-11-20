/** Pure validation for the PM-029 reference-data forms (no Angular). */

export interface ConstituentFormValue {
  symbol: string;
  companyName: string;
  effectiveFrom: string;
  source: string;
  sourceAsOfDate: string;
}

export interface BenchmarkFormValue {
  sector: string;
  market: string;
  benchmarkSymbol: string;
  currency: string;
  aliases: string;
  source: string;
  sourceAsOfDate: string;
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const SYMBOL = /^[A-Za-z0-9.\-^=]{1,20}$/;
const CURRENCY = /^[A-Za-z]{3}$/;

/** Rejects a date that is not YYYY-MM-DD, not a real calendar day, or in the future relative to `today`. */
function dateError(label: string, value: string, today: string, required: boolean): string | null {
  if (!value) return required ? `${label} is required.` : null;
  if (!ISO_DATE.test(value) || Number.isNaN(Date.parse(`${value}T00:00:00Z`))) return `${label} must be a valid date.`;
  if (value > today) return `${label} cannot be in the future.`;
  return null;
}

function todayIso(): string {
  return new Date().toISOString().substring(0, 10);
}

/** Every constituent must be sourced and date-stamped (U7): symbol, effective date, source and source date. */
export function constituentFormErrors(v: ConstituentFormValue, today = todayIso()): string[] {
  const errors: string[] = [];
  if (!v.symbol.trim()) errors.push('Symbol is required.');
  else if (!SYMBOL.test(v.symbol.trim())) errors.push('Symbol contains invalid characters.');
  const eff = dateError('Effective from', v.effectiveFrom, today, true);
  if (eff) errors.push(eff);
  if (!v.source.trim()) errors.push('Source is required.');
  const src = dateError('Source as-of date', v.sourceAsOfDate, today, true);
  if (src) errors.push(src);
  return errors;
}

export function benchmarkFormErrors(v: BenchmarkFormValue, today = todayIso()): string[] {
  const errors: string[] = [];
  if (!v.sector.trim()) errors.push('Sector is required.');
  if (v.market !== 'Tsx' && v.market !== 'Nyse') errors.push('Market must be Tsx or Nyse.');
  if (!v.benchmarkSymbol.trim()) errors.push('Benchmark symbol is required.');
  else if (!SYMBOL.test(v.benchmarkSymbol.trim())) errors.push('Benchmark symbol contains invalid characters.');
  if (!CURRENCY.test(v.currency.trim())) errors.push('Currency must be a 3-letter code.');
  if (!v.source.trim()) errors.push('Source is required.');
  const src = dateError('Source as-of date', v.sourceAsOfDate, today, true);
  if (src) errors.push(src);
  return errors;
}

export function splitAliases(value: string): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const raw of value.split(',')) {
    const a = raw.trim();
    if (a && !seen.has(a.toLowerCase())) {
      seen.add(a.toLowerCase());
      out.push(a);
    }
  }
  return out;
}

export function parseAliases(json: string | null | undefined): string[] {
  if (!json) return [];
  try {
    const parsed: unknown = JSON.parse(json);
    return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string') : [];
  } catch {
    return [];
  }
}
