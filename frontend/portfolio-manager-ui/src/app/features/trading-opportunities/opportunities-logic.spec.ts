import { describe, expect, it } from 'vitest';
import { OpportunityConfig } from '../../core/models/opportunities.models';
import { DEFAULT_OPPORTUNITY_VIEW, OpportunityView } from '../../core/store/opportunities.store';
import {
  OpportunityFilterInput,
  columnValue,
  companyNameForDisplay,
  exclusionSummary,
  filterRows,
  fitReasonLabel,
  heatClass,
  isUnscorable,
  isWeak,
  manualRewardRisk,
  manualRScenarioKey,
  opportunityExportColumns,
  opportunityExportRows,
  reasonCategory,
  reasonLabel,
  sectorHeatClass,
  sectorHeatText,
  sortRows,
} from './opportunities-logic';
import {
  opportunityRow,
  scoreSummary,
  setupQualityFixture,
  unscorable,
} from './testing/opportunity-fixtures';

const config = { attractiveMin: 72, moderateMin: 55 } as OpportunityConfig;

function input(
  view: Partial<OpportunityView> = {},
  extra: Partial<OpportunityFilterInput> = {},
): OpportunityFilterInput {
  return {
    view: { ...DEFAULT_OPPORTUNITY_VIEW, ...view },
    search: '',
    reasonFilter: null,
    config,
    ...extra,
  };
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
  it('sorts advisory quality with unavailable last and filters only Swing', () => {
    const sample = [
      opportunityRow('POOR', { setupQuality: setupQualityFixture({ status: 'Poor' }) }),
      opportunityRow('NA'),
      opportunityRow('GOOD', { setupQuality: setupQualityFixture() }),
    ];
    expect(
      sortRows(sample, { active: 'setupQuality', direction: 'desc' }, 'Swing').map(
        (row) => row.symbol,
      ),
    ).toEqual(['GOOD', 'POOR', 'NA']);
    expect(
      filterRows(sample, input({ setupQuality: ['Good'], scoreFilter: 'all' })).map(
        (row) => row.symbol,
      ),
    ).toEqual(['GOOD']);
    expect(
      filterRows(
        sample,
        input({ perspective: 'Strategic', setupQuality: ['Good'], scoreFilter: 'all' }),
      ),
    ).toHaveLength(3);
  });

  it('keeps deferred ETF rows under All and exposes the exact deferral reason', () => {
    const fund = opportunityRow('ETF', {
      swing: scoreSummary({
        score: null,
        status: 'NotEvaluated',
        reasonCodes: ['EtfScoringDeferred'],
      }),
    });
    expect(filterRows([fund], input({ scoreFilter: 'all' }))).toEqual([fund]);
    expect(reasonLabel('EtfScoringDeferred')).toContain('ETF scoring deferred');
  });
  it('hides unscorable Swing rows at the default threshold but counts them by reason', () => {
    const i = input();
    expect(filterRows(rows, i).map((r) => r.symbol)).toEqual(['AAA']);
    const s = exclusionSummary(rows, i);
    expect(s).toMatchObject({
      total: 5,
      shown: 1,
      belowThreshold: 2,
      unscorable: 2,
      filteredOut: 0,
    });
    expect(s.reasons).toEqual([
      { category: 'setup', code: 'NoStructuralTarget', label: 'No structural target', count: 1 },
      { category: 'stale', code: 'StaleScan', label: 'Stale technical scan', count: 1 },
    ]);
  });

  it('uses config thresholds, not literals', () => {
    const i = input(
      { scoreFilter: 'attractive' },
      { config: { attractiveMin: 55, moderateMin: 20 } as OpportunityConfig },
    );
    expect(filterRows(rows, i).map((r) => r.symbol)).toEqual(['AAA', 'BBB']);
  });

  it('shows unscorable rows under All scores or Show unavailable', () => {
    expect(filterRows(rows, input({ scoreFilter: 'all' })).length).toBe(5);
    expect(filterRows(rows, input({ showUnavailable: true })).map((r) => r.symbol)).toEqual([
      'AAA',
      'DDD',
      'EEE',
    ]);
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
    expect(filterRows(rows, input({}, { reasonFilter: 'setup' })).map((r) => r.symbol)).toEqual([
      'DDD',
    ]);
  });

  it('counts and filters the most specific setup reason without changing the category filter', () => {
    const missingStop = opportunityRow('MISS', {
      swing: {
        ...unscorable('NoValidStop'),
        reasonCodes: ['NoValidStop', 'DynamicStopMissing', 'MissingEssential:S6'],
      },
    });
    const atEntry = opportunityRow('ATENTRY', {
      swing: {
        ...unscorable('NoValidStop'),
        reasonCodes: ['NoValidStop', 'StopAtOrAboveEntry', 'MissingEssential:S6'],
      },
    });

    expect(exclusionSummary([missingStop, atEntry], input()).reasons).toEqual([
      {
        category: 'setup',
        code: 'StopAtOrAboveEntry',
        label: 'Scanner stop at or above entry',
        count: 1,
      },
      { category: 'setup', code: 'DynamicStopMissing', label: 'Scanner stop missing', count: 1 },
    ]);
    expect(
      filterRows([missingStop, atEntry], input({}, { reasonFilter: 'DynamicStopMissing' })).map(
        (r) => r.symbol,
      ),
    ).toEqual(['MISS']);
    expect(
      filterRows([missingStop, atEntry], input({}, { reasonFilter: 'setup' })).map((r) => r.symbol),
    ).toEqual(['MISS', 'ATENTRY']);
  });

  it('applies industry, risk, account, role, and freshness filters together', () => {
    const match = opportunityRow('MATCH', {
      industry: 'Railroads',
      risk: { ...opportunityRow('MATCH').risk, category: 'Medium' },
      accounts: ['TFSA'],
      roles: ['Swing'],
      watchlistRole: null,
    });
    const excluded = opportunityRow('EXCLUDED', {
      industry: 'Banks',
      risk: { ...opportunityRow('EXCLUDED').risk, category: 'High' },
      accounts: ['RRSP'],
      roles: ['Strategic'],
      freshness: { ...opportunityRow('EXCLUDED').freshness, isStale: true },
    });
    const filters = {
      industries: ['Railroads'],
      riskCategories: ['Medium' as const],
      accounts: ['TFSA'],
      roles: ['Swing'],
      freshness: 'current' as const,
      scoreFilter: 'all' as const,
    };

    expect(filterRows([match, excluded], input(filters)).map((r) => r.symbol)).toEqual(['MATCH']);
    expect(
      filterRows([match, excluded], input({ freshness: 'stale', scoreFilter: 'all' })).map(
        (r) => r.symbol,
      ),
    ).toEqual(['EXCLUDED']);
  });

  it('verified only excludes provisional scores', () => {
    const prov = opportunityRow('FFF', {
      swing: scoreSummary({ score: 90, status: 'Provisional', completenessPct: 80 }),
    });
    expect(filterRows([strong, prov], input({ verifiedOnly: true })).map((r) => r.symbol)).toEqual([
      'AAA',
    ]);
    expect(heatClass(prov.swing)).toContain('heat--provisional');
  });

  it('categorises reasons with identity > stale > setup > missing precedence', () => {
    expect(reasonCategory(['NoValidStop', 'StaleScan'])).toBe('stale');
    expect(reasonCategory(['IdentityUnresolved', 'StaleScan'])).toBe('identity');
    expect(reasonCategory(['MissingEssential:TR'])).toBe('missing');
    expect(reasonCategory([])).toBe('other');
  });

  it('explains unavailable and invalid technical inputs in readable labels', () => {
    expect(reasonLabel('VolumeUnavailable')).toBe('Volume data is unavailable');
    expect(reasonLabel('InvalidChangePercent')).toBe(
      'Daily change is outside the valid price range',
    );
    expect(reasonLabel('ScanBeforeSessionClose')).toBe(
      'Scan timestamp predates the exchange session close',
    );
    expect(reasonLabel('SectorHeat20D:Unmapped')).toBe('Sector benchmark is not mapped');
  });
});

describe('opportunities-logic sector heat', () => {
  it('keeps relative-return text independent from absolute benchmark-momentum colour', () => {
    const pos = { ...opportunityRow('A').sectorHeat, benchmarkReturnPct: -2.1 };
    expect(sectorHeatText(pos)).toBe('\u25B2 +2.7 pp');
    expect(sectorHeatClass(pos)).toBe('sector-heat-badge sh--benchmark-weak');
    const neg = {
      ...pos,
      diffPp: -4.1,
      direction: 'Negative' as const,
      benchmarkReturnPct: 5.2,
      intensity: 3,
    };
    expect(sectorHeatText(neg)).toBe('\u25BC \u22124.1 pp');
    expect(sectorHeatClass(neg)).toBe('sector-heat-badge sh--benchmark-strong');
    const neutral = { ...pos, diffPp: 0.3, direction: 'Neutral' as const, benchmarkReturnPct: 0 };
    expect(sectorHeatClass(neutral)).toBe('sector-heat-badge sh--benchmark-neutral');
    const unmapped = {
      ...pos,
      diffPp: null,
      status: 'Unmapped' as const,
      direction: 'None' as const,
      benchmarkReturnPct: null,
    };
    expect(sectorHeatText(unmapped)).toBe('Unavailable');
    expect(sectorHeatClass(unmapped)).toBe('sector-heat-badge sh--unavailable');
  });
});

describe('opportunities-logic sorting and export', () => {
  it('does not repeat a ticker as a company description in the UI or export', () => {
    expect(companyNameForDisplay('ABC.TO', 'ABC.TO')).toBeNull();
    expect(companyNameForDisplay('ABC.TO', '  abc.to  ')).toBeNull();
    expect(companyNameForDisplay('ABC.TO', 'ABC Railways')).toBe('ABC Railways');

    const row = opportunityRow('ABC.TO', { companyName: 'ABC.TO' });
    const columns = opportunityExportColumns({
      visibleOnly: false,
      visibleKeys: [],
      includeAccounts: false,
    });
    const companyColumn = columns.findIndex((column) => column.header === 'Company');
    expect(opportunityExportRows([row], columns)[0][companyColumn]).toBeNull();
  });

  it('sorts nulls last in both directions with symbol tie-break', () => {
    const desc = sortRows(rows, { active: 'score', direction: 'desc' }, 'Swing').map(
      (r) => r.symbol,
    );
    expect(desc).toEqual(['AAA', 'BBB', 'CCC', 'DDD', 'EEE']);
    const asc = sortRows(rows, { active: 'score', direction: 'asc' }, 'Swing').map((r) => r.symbol);
    expect(asc).toEqual(['CCC', 'BBB', 'AAA', 'DDD', 'EEE']);
  });

  it('sorts the active Strategic score profile with unavailable values last and stable symbols', () => {
    const strategicRows = [
      opportunityRow('ZZZ', { strategic: scoreSummary({ score: 72 }) }),
      opportunityRow('AAA', { strategic: scoreSummary({ score: 72 }) }),
      opportunityRow('MISS', { strategic: unscorable('FundamentalsMissing') }),
      opportunityRow('HIGH', { strategic: scoreSummary({ score: 84 }) }),
    ];
    expect(
      sortRows(strategicRows, { active: 'score', direction: 'desc' }, 'Strategic').map(
        (r) => r.symbol,
      ),
    ).toEqual(['HIGH', 'AAA', 'ZZZ', 'MISS']);
  });

  it('keeps Market R and a valid Manual R scenario separate for sorting and export', () => {
    const scenario = {
      [manualRScenarioKey('AAA', 'Swing')]: { entry: 100, stop: 90, target: 130 },
      [manualRScenarioKey('AAA', 'Strategic')]: { entry: 100, stop: 90, target: 110 },
    };
    expect(manualRewardRisk(scenario[manualRScenarioKey('AAA', 'Swing')])).toBe(3);
    expect(manualRewardRisk({ entry: 100, stop: 100, target: 130 })).toBeNull();
    expect(columnValue(strong, 'rr', 'Swing')).toBe(strong.setup.rewardRisk);
    expect(
      columnValue(strong, 'manualR', 'Swing', scenario[manualRScenarioKey('AAA', 'Swing')]),
    ).toBe(3);

    const ordered = sortRows(
      [strong, opportunityRow('LOW')],
      { active: 'manualR', direction: 'desc' },
      'Swing',
      scenario,
    );
    expect(ordered.map((row) => row.symbol)).toEqual(['AAA', 'LOW']);

    const columns = opportunityExportColumns({
      visibleOnly: true,
      visibleKeys: ['rr', 'manualR'],
      includeAccounts: false,
    });
    expect(columns.map((column) => column.header)).toContain('Market R');
    expect(columns.map((column) => column.header)).toContain('Manual R');
    const values = opportunityExportRows([strong], columns, scenario, 'Swing')[0];
    expect(values[columns.findIndex((column) => column.header === 'Market R')]).toBe(
      strong.setup.rewardRisk,
    );
    expect(values[columns.findIndex((column) => column.header === 'Manual R')]).toBe(3);
    expect(
      opportunityExportRows([strong], columns, scenario, 'Strategic')[0][
        columns.findIndex((column) => column.header === 'Manual R')
      ],
    ).toBe(1);
  });

  it('returns no Manual R for incomplete or invalid long scenarios', () => {
    expect(manualRewardRisk({ entry: null, stop: 90, target: 120 })).toBeNull();
    expect(manualRewardRisk({ entry: 100, stop: null, target: 120 })).toBeNull();
    expect(manualRewardRisk({ entry: 100, stop: 90, target: null })).toBeNull();
    expect(manualRewardRisk({ entry: 100, stop: 0, target: 120 })).toBeNull();
    expect(manualRewardRisk({ entry: 100, stop: 90, target: 100 })).toBeNull();
    expect(manualRewardRisk({ entry: 100, stop: 90, target: Number.POSITIVE_INFINITY })).toBeNull();
  });

  it('rejects zero, negative, and non-finite prices in every Manual R field', () => {
    const invalidScenarios = [
      { entry: 0, stop: 0, target: 120 },
      { entry: 100, stop: -1, target: 120 },
      { entry: 100, stop: 90, target: 0 },
      { entry: -1, stop: 90, target: 120 },
      { entry: 100, stop: 90, target: -1 },
      { entry: Number.NaN, stop: 90, target: 120 },
      { entry: 100, stop: Number.POSITIVE_INFINITY, target: 120 },
      { entry: 100, stop: 90, target: Number.NEGATIVE_INFINITY },
    ];

    for (const scenario of invalidScenarios) expect(manualRewardRisk(scenario)).toBeNull();
  });

  it('explains unavailable sector exposure in Portfolio Fit', () => {
    expect(fitReasonLabel('SectorExposureUnavailable')).toBe('Sector exposure unavailable');
  });

  it('exports visible columns only, omits accounts unless requested, keeps currency last', () => {
    const owned = opportunityRow('GGG', { accounts: ['TFSA'], owned: true });
    const cols = opportunityExportColumns({
      visibleOnly: true,
      visibleKeys: ['symbol', 'swing', 'accounts'],
      includeAccounts: false,
    });
    const headers = cols.map((c) => c.header);
    expect(headers).toContain('Swing Score');
    expect(headers).not.toContain('Strategic Score');
    expect(headers).not.toContain('Accounts');
    expect(headers[headers.length - 1]).toBe('Currency');
    const withAccounts = opportunityExportColumns({
      visibleOnly: false,
      visibleKeys: [],
      includeAccounts: true,
    });
    const data = opportunityExportRows([owned], withAccounts)[0];
    expect(data[withAccounts.findIndex((c) => c.header === 'Accounts')]).toBe('TFSA');
  });

  it('does not invent a currency when both row and setup currency are unavailable', () => {
    const base = opportunityRow('NOCURRENCY');
    const row = opportunityRow('NOCURRENCY', {
      currency: null,
      setup: { ...base.setup, currency: null },
    });
    const columns = opportunityExportColumns({
      visibleOnly: false,
      visibleKeys: [],
      includeAccounts: false,
    });
    const currencyColumn = columns.findIndex((c) => c.header === 'Currency');

    expect(opportunityExportRows([row], columns)[0][currencyColumn]).toBeNull();
  });
});
