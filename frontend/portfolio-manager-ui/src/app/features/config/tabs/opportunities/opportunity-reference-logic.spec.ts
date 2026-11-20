import { describe, expect, it } from 'vitest';
import { benchmarkFormErrors, constituentFormErrors, parseAliases, splitAliases } from './opportunity-reference-logic';

const TODAY = '2026-11-14';

describe('opportunity reference validation', () => {
  it('requires a sourced, date-stamped constituent and rejects future or impossible dates', () => {
    expect(
      constituentFormErrors({ symbol: 'ABC.TO', companyName: '', effectiveFrom: '2026-01-02', source: 'Index provider', sourceAsOfDate: '2026-11-01' }, TODAY),
    ).toEqual([]);
    const errors = constituentFormErrors({ symbol: 'A B', companyName: '', effectiveFrom: '2026-02-30x', source: ' ', sourceAsOfDate: '2026-12-01' }, TODAY);
    expect(errors).toContain('Symbol contains invalid characters.');
    expect(errors).toContain('Effective from must be a valid date.');
    expect(errors).toContain('Source is required.');
    expect(errors).toContain('Source as-of date cannot be in the future.');
  });

  it('validates benchmark market, currency and source', () => {
    const ok = { sector: 'Energy', market: 'Tsx', benchmarkSymbol: 'XEG.TO', currency: 'CAD', aliases: '', source: 'Issuer', sourceAsOfDate: '2026-11-01' };
    expect(benchmarkFormErrors(ok, TODAY)).toEqual([]);
    const errors = benchmarkFormErrors({ ...ok, market: 'Lse', currency: 'CA', source: '' }, TODAY);
    expect(errors).toEqual(['Market must be Tsx or Nyse.', 'Currency must be a 3-letter code.', 'Source is required.']);
  });

  it('splits aliases case-insensitively without blanks and parses stored JSON defensively', () => {
    expect(splitAliases(' Energy, energy ,, Oil & Gas ')).toEqual(['Energy', 'Oil & Gas']);
    expect(parseAliases('["Energy",1,"Oil"]')).toEqual(['Energy', 'Oil']);
    expect(parseAliases('not json')).toEqual([]);
    expect(parseAliases(null)).toEqual([]);
  });
});
