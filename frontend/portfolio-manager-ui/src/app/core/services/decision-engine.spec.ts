import { PriceStructureResult } from '../models/portfolio.models';
import {
  evaluateWatchlistEntry,
  WatchlistEntryContext,
  WatchlistEntryStatus,
} from './decision-engine.service';

describe('canonical watchlist entry status', () => {
  const canonical: WatchlistEntryStatus[] = [
    'ENTRY CANDIDATE',
    'STARTER ENTRY',
    'BUY WATCH',
    'REVERSAL WATCH',
    'WATCH / NO CHASE',
    'WAIT FOR PULLBACK',
    'WAIT FOR REVERSAL',
    'WAIT FOR RECLAIM',
    'AVOID',
    'WATCH',
  ];

  it.each([
    [
      'DPRO.CN',
      context({ priceStructure: level('FAILED_BREAKOUT'), momentumState: 'Positive', buyScore: 5 }),
      'WAIT FOR RECLAIM',
    ],
    [
      'ATD.TO',
      context({
        priceStructure: wedgeBreakdown(),
        momentumState: 'Declining',
        rsi: 29,
        buyScore: 5,
      }),
      'AVOID',
    ],
    [
      'ATS.TO',
      context({
        trendSetup: 'Oversold Reversal Watch',
        momentumState: 'Bullish Shift',
        rsi: 29.7,
        buyScore: 3,
      }),
      'REVERSAL WATCH',
    ],
    [
      'TSLA',
      context({
        priceStructure: level('RESISTANCE_TEST', 'RESISTANCE'),
        momentumState: 'Accelerating',
        buyScore: 5,
      }),
      'WATCH / NO CHASE',
    ],
    [
      'URA',
      context({ priceStructure: level('SUPPORT_TEST'), momentumState: 'Neutral', buyScore: 2 }),
      'WATCH',
    ],
    [
      'PG',
      context({
        priceStructure: wedgeBreakout(),
        momentumState: 'Accelerating',
        buyScore: 4,
        maStructure: 'P > 50 > 200',
      }),
      'ENTRY CANDIDATE',
    ],
    [
      'KHC',
      context({
        role: 'Strategic',
        priceStructure: level('SUPPORT_TEST'),
        momentumState: 'Positive',
        buyScore: 4,
        maStructure: 'P > 50 > 200',
      }),
      'STARTER ENTRY',
    ],
    [
      'L.TO',
      context({
        role: 'Strategic',
        priceStructure: tightWedgeNearBreakout(),
        momentumState: 'Neutral',
        buyScore: 4,
      }),
      'BUY WATCH',
    ],
    [
      'GOOGL',
      context({ priceStructure: level('SUPPORT_TEST'), momentumState: 'Neutral', buyScore: 3 }),
      'WATCH',
    ],
    [
      'MRVL',
      context({
        priceStructure: wedgeBreakdown('SUPPORT_TEST'),
        momentumState: 'Neutral',
        buyScore: 5,
      }),
      'WAIT FOR RECLAIM',
    ],
  ])('%s resolves to %s', (_symbol, input, expected) => {
    const decision = evaluateWatchlistEntry(input);

    expect(decision.finalAction).toBe(expected);
    expect(canonical).toContain(decision.finalAction);
  });

  it('never grants entry permission when a hard structural negative is active', () => {
    const decision = evaluateWatchlistEntry(
      context({
        role: 'Speculative',
        priceStructure: level('FAILED_BREAKOUT'),
        momentumState: 'Accelerating',
        buyScore: 5,
      }),
    );

    expect(decision.entryBlockedByHardNegative).toBe(true);
    expect(decision.finalAction).not.toMatch(/BUY|ENTRY|STARTER|ACCUMULATE/);
    expect(decision.finalActionReason).toContain('cannot override confirmed structural damage');
  });

  it.each(['CHANNEL_SUPPORT_BROKEN', 'WEDGE_BREAKDOWN', 'TIGHT_WEDGE_BREAKDOWN'])(
    'treats equivalent hard-negative state %s as an entry blocker',
    (state) => {
      const decision = evaluateWatchlistEntry(
        context({ priceStructure: level(state), momentumState: 'Positive', buyScore: 5 }),
      );

      expect(decision.entryBlockedByHardNegative).toBe(true);
      expect(decision.finalAction).toBe('WAIT FOR RECLAIM');
    },
  );
});

function context(overrides: Partial<WatchlistEntryContext>): WatchlistEntryContext {
  return {
    role: 'Swing',
    rsi: 50,
    buyScore: 3,
    trendSetup: 'Neutral / No Setup',
    momentumShift: 'Neutral',
    momentumState: 'Neutral',
    maStructure: 'P > 50 > 200',
    priceStructure: null,
    ...overrides,
  };
}

function level(state: string, role = 'SUPPORT'): PriceStructureResult {
  return structure({
    keyLevelState: state,
    keyLevelRole: role,
    hasHardStructuralNegative: state === 'FAILED_BREAKOUT',
  });
}

function wedgeBreakdown(keyLevelState = 'NONE'): PriceStructureResult {
  return structure({
    primaryPatternType: 'TIGHT_RISING_WEDGE',
    primaryPatternState: 'BREAKDOWN',
    keyLevelState,
    hasHardStructuralNegative: true,
  });
}

function wedgeBreakout(): PriceStructureResult {
  return structure({ primaryPatternType: 'TIGHT_FALLING_WEDGE', primaryPatternState: 'BREAKOUT' });
}

function tightWedgeNearBreakout(): PriceStructureResult {
  return structure({ primaryPatternType: 'TIGHT_FALLING_WEDGE', primaryPatternState: 'NEAR_APEX' });
}

function structure(overrides: Partial<PriceStructureResult>): PriceStructureResult {
  return {
    primaryPatternType: 'NONE',
    primaryPatternState: 'NONE',
    keyLevelState: 'NONE',
    keyLevelRole: 'SUPPORT',
    hasHardStructuralNegative: false,
    ...overrides,
  } as PriceStructureResult;
}
