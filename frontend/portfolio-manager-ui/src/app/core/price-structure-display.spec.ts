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

    expect(priceStructureLabel(result)).toBe('TIGHT WEDGE BREAKDOWN');
    expect(priceStructureTooltip(result)).toContain('CURRENT DECISION LEVEL');
    expect(priceStructureTooltip(result)).toContain('Testing Channel Support @ $208.07');
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

    expect(tooltip).toContain('Hold / Confirmation Trigger: $46.00');
    expect(tooltip).toContain('Breakdown Trigger: $44.00');
    expect(tooltip).not.toContain('\nBreakout Trigger:');
  });

  it('sorts a hard pattern event ahead of an ordinary level test', () => {
    const breakdown = structure({ primaryPatternState: 'BREAKDOWN' });
    const supportTest = structure({ keyLevelState: 'SUPPORT_TEST' });

    expect(priceStructureSortRank(breakdown)).toBeGreaterThan(priceStructureSortRank(supportTest));
  });

  it.each([
    [
      { keyLevelType: 'CONFLUENCE_ZONE', keyLevelRole: 'SUPPORT', keyLevelState: 'SUPPORT_TEST' },
      'STRONG SUPPORT ZONE',
    ],
    [
      {
        keyLevelType: 'CONFLUENCE_ZONE',
        keyLevelRole: 'RESISTANCE',
        keyLevelState: 'RESISTANCE_TEST',
      },
      'STRONG RESISTANCE ZONE',
    ],
    [{ keyLevelState: 'CONFLUENCE_SUPPORT' }, 'STRONG SUPPORT ZONE'],
    [
      { keyLevelRole: 'RESISTANCE', keyLevelState: 'CONFLUENCE_RESISTANCE' },
      'STRONG RESISTANCE ZONE',
    ],
    [{ keyLevelState: 'SUPPORT_TEST' }, 'TESTING SUPPORT'],
    [{ keyLevelRole: 'RESISTANCE', keyLevelState: 'RESISTANCE_TEST' }, 'TESTING RESISTANCE'],
    [{ keyLevelState: 'APPROACHING_SUPPORT' }, 'NEAR SUPPORT'],
    [{ keyLevelRole: 'RESISTANCE', keyLevelState: 'APPROACHING_RESISTANCE' }, 'NEAR RESISTANCE'],
    [{ keyLevelState: 'SUPPORT_RECLAIM' }, 'SUPPORT RECOVERED'],
    [{ keyLevelState: 'BREAKOUT_WATCH' }, 'BREAKOUT WATCH'],
    [{ keyLevelState: 'BREAKOUT_CONFIRMED' }, 'BREAKOUT CONFIRMED'],
    [{ keyLevelState: 'BREAKDOWN_WATCH' }, 'SUPPORT AT RISK'],
    [{ keyLevelState: 'BREAKDOWN_CONFIRMED' }, 'SUPPORT BROKEN'],
    [{ keyLevelState: 'FAILED_BREAKOUT' }, 'BREAKOUT FAILED'],
    [
      { keyLevelType: 'SWING_HIGH', keyLevelRole: 'RESISTANCE', keyLevelState: 'RESISTANCE_TEST' },
      'TESTING RECENT HIGH',
    ],
    [
      {
        keyLevelType: 'SWING_HIGH',
        keyLevelRole: 'RESISTANCE',
        keyLevelState: 'APPROACHING_RESISTANCE',
      },
      'NEAR RECENT HIGH',
    ],
    [{ keyLevelType: 'SWING_LOW', keyLevelState: 'SUPPORT_TEST' }, 'TESTING RECENT LOW'],
    [{ keyLevelType: 'SWING_LOW', keyLevelState: 'APPROACHING_SUPPORT' }, 'NEAR RECENT LOW'],
    [{ keyLevelType: 'FIB_38_2', keyLevelState: 'SUPPORT_TEST' }, 'TESTING FIB 38.2'],
    [
      { keyLevelType: 'FIB_50', keyLevelRole: 'RESISTANCE', keyLevelState: 'RESISTANCE_TEST' },
      'TESTING FIB 50',
    ],
    [{ keyLevelType: 'FIB_61_8', keyLevelState: 'SUPPORT_TEST' }, 'TESTING FIB 61.8'],
    [{ keyLevelState: 'THIRD_RAIL_APPROACHING' }, 'NEAR CHANNEL SUPPORT'],
    [{ keyLevelState: 'THIRD_RAIL_TEST' }, 'TESTING CHANNEL SUPPORT'],
    [{ keyLevelState: 'LOWER_RAIL_RETEST' }, 'RETESTING CHANNEL SUPPORT'],
    [{ keyLevelState: 'CHANNEL_BROKEN' }, 'CHANNEL SUPPORT BROKEN'],
  ])('maps level state %j to %s', (overrides, expected) => {
    const result = structure({ keyLevelPrice: 100, ...overrides });
    const originalState = result.keyLevelState;

    expect(priceStructureLabel(result)).toBe(expected);
    expect(result.keyLevelState).toBe(originalState);
  });

  it.each([
    ['RISING_CHANNEL', 'THIRD_TOUCH_APPROACHING', 'NEAR CHANNEL SUPPORT'],
    ['RISING_CHANNEL', 'THIRD_RAIL_TEST', 'TESTING CHANNEL SUPPORT'],
    ['RISING_CHANNEL', 'LOWER_RAIL_RETEST', 'RETESTING CHANNEL SUPPORT'],
    ['RISING_CHANNEL', 'CHANNEL_BROKEN', 'CHANNEL SUPPORT BROKEN'],
    ['FALLING_WEDGE', 'DEVELOPING', 'FALLING WEDGE'],
    ['FALLING_WEDGE', 'NEAR_APEX', 'WEDGE TIGHTENING'],
    ['TIGHT_FALLING_WEDGE', 'DEVELOPING', 'TIGHT FALLING WEDGE'],
    ['TIGHT_FALLING_WEDGE', 'NEAR_APEX', 'TIGHT WEDGE — NEAR BREAKOUT'],
    ['FALLING_WEDGE', 'BREAKOUT', 'WEDGE BREAKOUT'],
    ['TIGHT_FALLING_WEDGE', 'BREAKOUT', 'TIGHT WEDGE BREAKOUT'],
    ['RISING_WEDGE', 'DEVELOPING', 'RISING WEDGE'],
    ['RISING_WEDGE', 'NEAR_APEX', 'RISING WEDGE — CAUTION'],
    ['RISING_WEDGE', 'BREAKDOWN', 'WEDGE BREAKDOWN'],
    ['TIGHT_RISING_WEDGE', 'BREAKDOWN', 'TIGHT WEDGE BREAKDOWN'],
  ])('maps pattern %s/%s to %s', (primaryPatternType, primaryPatternState, expected) => {
    const result = structure({ primaryPatternType, primaryPatternState });
    const originalState = result.primaryPatternState;

    expect(priceStructureLabel(result)).toBe(expected);
    expect(result.primaryPatternState).toBe(originalState);
  });

  it('leads tooltip with investor questions before technical details', () => {
    const tooltip = priceStructureTooltip(
      structure({ keyLevelPrice: 100, keyLevelState: 'SUPPORT_TEST' }),
    );

    expect(tooltip.indexOf('WHAT IS HAPPENING?')).toBeLessThan(
      tooltip.indexOf('TECHNICAL DETAILS'),
    );
    expect(tooltip).toContain('WHY DOES IT MATTER?');
    expect(tooltip).toContain('WHAT TO WATCH NEXT?');
    expect(tooltip).toContain('Internal State: SUPPORT_TEST');
  });

  it('describes a transitioned wedge level using its current role', () => {
    const tooltip = priceStructureTooltip(
      structure({
        keyLevelPrice: 100,
        keyLevelState: 'SUPPORT_TEST',
        keyLevelOriginalRole: 'RESISTANCE',
        keyLevelRole: 'SUPPORT',
        keyLevelSources: ['Upper Wedge Resistance'],
      }),
    );

    expect(tooltip).toContain('Testing Former Wedge Resistance — Now Support');
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
