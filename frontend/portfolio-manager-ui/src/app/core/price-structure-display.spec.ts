import { PriceStructureResult } from './models/portfolio.models';
import {
  priceStructureLabel,
  priceStructureSortRank,
  priceStructureTooltip,
} from './price-structure-display';

describe('Price Structure display', () => {
  it('shows a hard wedge breakdown while retaining nearby channel support in details', () => {
    const result = structure({
      label: 'Tight Rising Wedge Breakdown',
      primaryPatternType: 'TIGHT_RISING_WEDGE',
      primaryPatternState: 'BREAKDOWN',
      keyLevelType: 'CHANNEL_RAIL',
      keyLevelRole: 'SUPPORT',
      keyLevelState: 'SUPPORT_TEST',
      keyLevelPrice: 208.07,
      keyLevelSources: ['Lower Channel Rail'],
      hasHardStructuralNegative: true,
    });

    expect(priceStructureLabel(result)).toBe('Tight Rising Wedge Breakdown');
    expect(priceStructureTooltip(result)).toContain('CURRENT DECISION LEVEL');
    expect(priceStructureTooltip(result)).toContain('Testing Channel Support @ 208.07');
  });

  it('uses channel touch diagnostics without wedge-specific counters', () => {
    const result = structure({
      label: '3rd Rail Approaching',
      primaryPatternType: 'RISING_CHANNEL',
      primaryPatternState: 'THIRD_TOUCH_APPROACHING',
      channelTouchDetails: [touch(1), touch(2)],
    });
    const tooltip = priceStructureTooltip(result);

    expect(tooltip).toContain('Confirmed Lower Rail Touches: 2');
    expect(tooltip).toContain('Confirmed Upper Rail Touches: —');
    expect(tooltip).toContain('CHANNEL TOUCH HISTORY');
    expect(tooltip).not.toContain('Independent Upper Touches');
    expect(tooltip).not.toContain('Independent Lower Touches');
  });

  it('uses support-oriented trigger captions', () => {
    const result = structure({
      keyLevelRole: 'SUPPORT',
      keyLevelState: 'SUPPORT_TEST',
      keyLevelPrice: 45.13,
      breakoutTriggerPrice: 46,
      breakdownTriggerPrice: 44,
      keyLevelSources: ['EMA20', 'Fib 38.2'],
    });
    const tooltip = priceStructureTooltip(result);

    expect(tooltip).toContain('Hold / Confirmation Trigger: 46.00');
    expect(tooltip).toContain('Breakdown Trigger: 44.00');
    expect(tooltip).not.toContain('\nBreakout Trigger:');
  });

  it('sorts a hard pattern event ahead of an ordinary level test', () => {
    const breakdown = structure({ primaryPatternState: 'BREAKDOWN' });
    const supportTest = structure({ keyLevelState: 'SUPPORT_TEST' });

    expect(priceStructureSortRank(breakdown)).toBeGreaterThan(priceStructureSortRank(supportTest));
  });
});

function structure(overrides: Partial<PriceStructureResult>): PriceStructureResult {
  return {
    label: '—',
    primaryPatternType: 'NONE',
    primaryPatternState: 'NONE',
    primaryPatternQuality: 0,
    patternHorizon: 'NONE',
    keyLevelType: 'NONE',
    keyLevelRole: 'SUPPORT',
    keyLevelState: 'NONE',
    keyLevelPrice: null,
    keyLevelLow: null,
    keyLevelHigh: null,
    keyLevelDistancePercent: null,
    keyLevelDistanceAtr: null,
    keyLevelSources: [],
    keyLevelConfluenceCount: 0,
    breakoutTriggerPrice: null,
    breakdownTriggerPrice: null,
    upperTrendline: 0,
    lowerTrendline: 0,
    contractionPercent: 0,
    independentUpperTouchCount: 0,
    independentLowerTouchCount: 0,
    channelTouchDetails: [],
    ...overrides,
  } as PriceStructureResult;
}

function touch(touchNumber: number) {
  return {
    touchNumber,
    touchDate: `2026-08-0${touchNumber}`,
    railPrice: 95,
    actualLow: 95.1,
    bounceATR: 1.5,
    confirmedBounce: true,
  };
}
