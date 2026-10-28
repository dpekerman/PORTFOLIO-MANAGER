import { SignalEffectivenessResultRow } from '../../core/models/portfolio.models';
import { buildEffectivenessChart } from './signal-effectiveness-chart';

const row = (over: Partial<SignalEffectivenessResultRow>): SignalEffectivenessResultRow => ({
  signalType: 'T',
  label: 'Type',
  direction: 'Buy',
  directionDefined: true,
  eligible: 1,
  overlappingCount: 0,
  observations5D: 0,
  rawAvg5D: null,
  adjustedAvg5D: null,
  observations20D: 0,
  rawAvg20D: null,
  adjustedAvg20D: null,
  observations1M: 0,
  rawAvg1M: null,
  adjustedAvg1M: null,
  favourableObservations: 0,
  favourablePct: null,
  actionedCount: 0,
  ...over,
});

describe('buildEffectivenessChart', () => {
  it('plots completed periods only and never treats a missing period as zero', () => {
    const chart = buildEffectivenessChart([row({ observations5D: 2, rawAvg5D: 2, adjustedAvg5D: 2 })]);
    const [b5, b20, b1m] = chart.groups[0].bars;
    expect(b5.value).toBe(2);
    expect(b5.shortLabel).toBe('+2.0');
    expect(b5.label).toBe('+2.00%');
    expect(b20.value).toBeNull();
    expect(b20.heightPct).toBe(0);
    expect(b1m.label).toBe('n/a');
    expect(chart.groups[0].ariaLabel).toContain('20D: insufficient history');
  });

  it('draws a decline below the zero line for a Buy signal', () => {
    const chart = buildEffectivenessChart([row({ observations20D: 4, rawAvg20D: -3.2, adjustedAvg20D: -3.2 })]);
    const bar = chart.groups[0].bars[1];
    expect(bar.negative).toBe(true);
    expect(bar.bottomPct + bar.heightPct).toBeCloseTo(chart.zeroPct, 5);
    expect(bar.bottomPct).toBeLessThan(chart.zeroPct);
  });

  it('plots a Sell signal followed by a decline as positive while keeping the raw return', () => {
    const chart = buildEffectivenessChart([
      row({ direction: 'Sell', observations20D: 3, rawAvg20D: -4.5, adjustedAvg20D: 4.5 }),
    ]);
    const bar = chart.groups[0].bars[1];
    expect(bar.value).toBe(4.5);
    expect(bar.negative).toBe(false);
    expect(bar.raw).toBe(-4.5);
    expect(bar.adjusted).toBe(true);
    expect(bar.bottomPct).toBeCloseTo(chart.zeroPct, 5);
    expect(chart.groups[0].ariaLabel).toContain('raw -4.50%');
    expect(chart.groups[0].ariaLabel).toContain('a fall counts as positive');
  });

  it('plots raw returns for a type with no defined direction', () => {
    const chart = buildEffectivenessChart([
      row({ direction: 'Neutral', directionDefined: false, observations5D: 2, rawAvg5D: 0.8 }),
    ]);
    expect(chart.groups[0].bars[0].value).toBe(0.8);
    expect(chart.groups[0].bars[0].adjusted).toBe(false);
    expect(chart.groups[0].ariaLabel).toContain('direction not defined');
  });

  it('uses one shared axis that always includes zero, with ordered ticks', () => {
    const chart = buildEffectivenessChart([
      row({ observations20D: 1, rawAvg20D: 12, adjustedAvg20D: 12 }),
      row({ signalType: 'U', observations20D: 1, rawAvg20D: -3, adjustedAvg20D: -3 }),
    ]);
    const values = chart.ticks.map((t) => t.value);
    expect(values).toEqual([...values].sort((a, b) => a - b));
    expect(values[0]).toBeLessThan(0);
    expect(values[values.length - 1]).toBeGreaterThanOrEqual(12);
    expect(values).toContain(0);
    expect(chart.ticks.every((t) => t.bottomPct >= 0 && t.bottomPct <= 100)).toBe(true);
    expect(chart.zeroPct).toBeGreaterThan(0);
    expect(chart.zeroPct).toBeLessThan(100);
  });

  it('reports no data when no period has completed', () => {
    const chart = buildEffectivenessChart([row({})]);
    expect(chart.hasData).toBe(false);
    expect(chart.ticks.length).toBeGreaterThan(1);
  });

  it('handles an empty result', () => {
    expect(buildEffectivenessChart([]).groups).toEqual([]);
  });
});
