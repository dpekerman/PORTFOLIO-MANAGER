import { describe, expect, it } from 'vitest';
import { OpportunityConfig } from '../../core/models/opportunities.models';
import { DEFAULT_OPPORTUNITY_VIEW, OpportunityView } from '../../core/store/opportunities.store';
import {
  OpportunityFilterInput,
  exclusionSummary,
  filterRows,
  heatClass,
  isUnscorable,
  isWeak,
  opportunityExportColumns,
  opportunityExportRows,
  reasonCategory,
  sectorHeatClass,
  sectorHeatText,
  sortRows,
} from './opportunities-logic';
import { opportunityRow, scoreSummary, unscorable } from './testing/opportunity-fixtures';

const config = { attractiveMin: 72, moderateMin: 55 } as OpportunityConfig;

function input(view: Partial<OpportunityView> = {}, extra: Partial<OpportunityFilterInput> = {}): OpportunityFilterInput {
  return { view: { ...DEFAULT_OPPORTUNITY_VIEW, ...view }, search: '', reasonFilter: null, config, ...extra };
}

const strong = opportunityRow('AAA', { swing: scoreSummary({ score: 81 }) });
const moderate = opportunityRow('BBB', { swing: scoreSummary({ score: 60, band: 'Moderate' }) });
const weak = opportunityRow('CCC', { swing: scoreSummary({ score: 21, band: 'Weak' }) });
const noSetup = opportunityRow('DDD', {
  swing: unscorable('NoStructuralTarget'),
  strategic: scoreSummary({ score: 76 }),
});
const stale = opportunityRow('EEE', { swing: unscorable('StaleScan') });
const rows = [strong, moderate, weak, noSetup, stale];

describe('opportunities-logic score filter (C2)', () => {
  it('hides unscorable Swing rows at the default threshold but counts them by reason', () => {
    const i = input();
    expect(filterRows(rows, i).map((r) => r.symbol)).toEqual(['AAA']);
    const s = exclusionSummary(rows, i);
    expect(s).toMatchObject({ total: 5, shown: 1, belowThreshold: 2, unscorable: 2, filteredOut: 0 });
    expect(s.reasons).toEqual([
      { category: 'setup', label: 'no valid setup', count: 1 },
      { category: 'stale', label: 'stale evidence', count: 1 },
    ]);
  });

  it('uses config thresholds, not literals', () => {
    const i = input({ scoreFilter: 'attractive' }, { config: { attractiveMin: 55, moderateMin: 20 } as OpportunityConfig });
    expect(filterRows(rows, i).map((r) => r.symbol)).toEqual(['AAA', 'BBB']);
  });

  it('shows unscorable rows under All scores or Show unavailable', () => {
    expect(filterRows(rows, input({ scoreFilter: 'all' })).length).toBe(5);
    expect(filterRows(rows, input({ showUnavailable: true })).map((r) => r.symbol)).toEqual(['AAA', 'DDD', 'EEE']);
  });

  it('keeps the independently available Strategic score for a row whose Swing is unscorable', () => {
    const i = input({ perspective: 'Strategic' });
    expect(filterRows([noSetup], i).map((r) => r.symbol)).toEqual(['DDD']);
  });

  it('separates Weak from Unscorable', () => {
    for (const r of rows) {
      expect(isWeak(r.swing) && isUnscorable(r.swing)).toBe(false);
    }
    expect(rows.filter((r) => isWeak(r.swing)).map((r) => r.symbol)).toEqual(['CCC']);
    expect(rows.filter((r) => isUnscorable(r.swing)).map((r) => r.symbol)).toEqual(['DDD', 'EEE']);
  });

  it('reason chip filter shows only unscorable rows of that category', () => {
    expect(filterRows(rows, input({}, { reasonFilter: 'setup' })).map((r) => r.symbol)).toEqual(['DDD']);
  });

  it('verified only excludes provisional scores', () => {
    const prov = opportunityRow('FFF', { swing: scoreSummary({ score: 90, status: 'Provisional', completenessPct: 80 }) });
    expect(filterRows([strong, prov], input({ verifiedOnly: true })).map((r) => r.symbol)).toEqual(['AAA']);
    expect(heatClass(prov.swing)).toContain('heat--provisional');
  });

  it('categorises reasons with identity > stale > setup > missing precedence', () => {
    expect(reasonCategory(['NoValidStop', 'StaleScan'])).toBe('stale');
    expect(reasonCategory(['IdentityUnresolved', 'StaleScan'])).toBe('identity');
    expect(reasonCategory(['MissingEssential:TR'])).toBe('missing');
    expect(reasonCategory([])).toBe('other');
  });
});

describe('opportunities-logic sector heat', () => {
  it('formats signed percentage points with arrows and colour class', () => {
    const pos = opportunityRow('A').sectorHeat;
    expect(sectorHeatText(pos)).toBe('\u25B2 +2.7 pp');
    expect(sectorHeatClass(pos)).toBe('sector-heat-badge sh--pos-2');
    const neg = { ...pos, diffPp: -4.1, direction: 'Negative' as const, intensity: 3 };
    expect(sectorHeatText(neg)).toBe('\u25BC \u22124.1 pp');
    expect(sectorHeatClass(neg)).toBe('sector-heat-badge sh--neg-3');
    const neutral = { ...pos, diffPp: 0.3, direction: 'Neutral' as const, intensity: 0 };
    expect(sectorHeatClass(neutral)).toBe('sector-heat-badge sh--neutral');
    const unmapped = { ...pos, diffPp: null, status: 'Unmapped' as const, direction: 'None' as const };
    expect(sectorHeatText(unmapped)).toBe('Unavailable');
  });
});

describe('opportunities-logic sorting and export', () => {
  it('sorts nulls last in both directions with symbol tie-break', () => {
    const desc = sortRows(rows, { active: 'score', direction: 'desc' }, 'Swing').map((r) => r.symbol);
    expect(desc).toEqual(['AAA', 'BBB', 'CCC', 'DDD', 'EEE']);
    const asc = sortRows(rows, { active: 'score', direction: 'asc' }, 'Swing').map((r) => r.symbol);
    expect(asc).toEqual(['CCC', 'BBB', 'AAA', 'DDD', 'EEE']);
  });

  it('exports visible columns only, omits accounts unless requested, keeps currency last', () => {
    const owned = opportunityRow('GGG', { accounts: ['TFSA'], owned: true });
    const cols = opportunityExportColumns({ visibleOnly: true, visibleKeys: ['symbol', 'swing', 'accounts'], includeAccounts: false });
    const headers = cols.map((c) => c.header);
    expect(headers).toContain('Swing Score');
    expect(headers).not.toContain('Strategic Score');
    expect(headers).not.toContain('Accounts');
    expect(headers[headers.length - 1]).toBe('Currency');
    const withAccounts = opportunityExportColumns({ visibleOnly: false, visibleKeys: [], includeAccounts: true });
    const data = opportunityExportRows([owned], withAccounts)[0];
    expect(data[withAccounts.findIndex((c) => c.header === 'Accounts')]).toBe('TFSA');
  });
});
