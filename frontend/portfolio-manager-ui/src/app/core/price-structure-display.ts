import { PriceStructureResult } from './models/portfolio.models';

export function priceStructureLabel(
  structure: PriceStructureResult | null | undefined,
  _maskValue: (value: number) => number = (value) => value,
): string {
  if (!hasPriceStructure(structure)) return '—';
  if (patternEventPriority(structure) > keyLevelEventPriority(structure)) {
    return friendlyPatternLabel(structure);
  }
  if (structure.keyLevelState !== 'NONE') return friendlyLevelLabel(structure);
  return friendlyPatternLabel(structure);
}

export function priceStructureTooltip(
  structure: PriceStructureResult | null | undefined,
  maskValue: (value: number) => number = (value) => value,
  analysisSource?: {
    ticker?: string | null;
    market?: string | null;
    currency?: string | null;
    usesUnderlying?: boolean;
  },
): string {
  if (!hasPriceStructure(structure)) return '';
  const currencySuffix = analysisSource?.usesUnderlying
    ? ` ${analysisSource.currency ?? 'USD'}`
    : '';
  const money = (value: number | null | undefined) =>
    value === null || value === undefined || value === 0
      ? '—'
      : `$${maskValue(value).toFixed(2)}${currencySuffix}`;
  const optional = (value: number | null | undefined, suffix = '') =>
    value === null || value === undefined ? '—' : `${value}${suffix}`;

  const isChannel = structure.primaryPatternType.includes('CHANNEL');
  const isWedge = structure.primaryPatternType.includes('WEDGE');
  const displayLabel = priceStructureLabel(structure, maskValue);
  const narrative = friendlyNarrative(structure);
  const touchDiagnostics = isChannel
    ? `Confirmed Lower Rail Touches: ${structure.channelTouchDetails.length}\nConfirmed Upper Rail Touches: —`
    : isWedge
      ? `Independent Upper Touches: ${structure.independentUpperTouchCount}\nIndependent Lower Touches: ${structure.independentLowerTouchCount}`
      : '';
  const pattern =
    structure.primaryPatternType === 'NONE'
      ? ''
      : `PRIMARY PATTERN\nPattern: ${humanize(structure.primaryPatternType)}\nInternal Pattern Type: ${structure.primaryPatternType}\nInternal State: ${structure.primaryPatternState}\nHorizon: ${structure.patternHorizon}\nLookback: ${optional(structure.patternLookbackSessions, ' sessions')}\nQuality: ${structure.primaryPatternQuality}/100\nStart: ${structure.patternStart?.slice(0, 10) ?? '—'}\nUpper Rail: ${money(structure.upperTrendline)}\nLower Rail: ${money(structure.lowerTrendline)}\nContraction: ${structure.contractionPercent}%\nProjected Apex: ${structure.projectedApexDate?.slice(0, 10) ?? '—'}\nTrading Days to Apex: ${optional(structure.tradingDaysToApex)}${touchDiagnostics ? `\n${touchDiagnostics}` : ''}`;
  const supportOriented = structure.keyLevelRole === 'SUPPORT';
  const upperTriggerLabel = supportOriented ? 'Hold / Confirmation Trigger' : 'Breakout Trigger';
  const lowerTriggerLabel = supportOriented ? 'Breakdown Trigger' : 'Failure / Rejection Trigger';
  const currentDecision = currentDecisionLabel(structure, money);
  const level =
    structure.keyLevelState === 'NONE'
      ? ''
      : `CURRENT DECISION LEVEL\n${currentDecision}\nInternal State: ${structure.keyLevelState}\nCurrent Role: ${structure.keyLevelRole}\nOriginal Role: ${structure.keyLevelOriginalRole ?? structure.keyLevelRole}\nLevel Type: ${structure.keyLevelType}\nZone: ${money(structure.keyLevelLow)} - ${money(structure.keyLevelHigh)}\nDistance: ${optional(structure.keyLevelDistancePercent, '%')}\nDistance ATR: ${optional(structure.keyLevelDistanceAtr, ' ATR')}\nDaily High: ${money(structure.dailyHigh)}\nDaily Low: ${money(structure.dailyLow)}\nEOD Close: ${money(structure.eodClose)}\nATR: ${money(structure.atr)}\n${upperTriggerLabel}: ${money(structure.breakoutTriggerPrice)}\n${lowerTriggerLabel}: ${money(structure.breakdownTriggerPrice)}\nSources: ${structure.keyLevelSources.join(', ') || '—'}\nConfluence Count: ${structure.keyLevelConfluenceCount}`;
  const touches = structure.channelTouchDetails.length
    ? `CHANNEL TOUCH HISTORY\n${structure.channelTouchDetails.map((touch) => `#${touch.touchNumber} ${touch.touchDate.slice(0, 10)} | Rail ${money(touch.railPrice)} | Low ${money(touch.actualLow)} | Bounce ${touch.bounceATR} ATR`).join('\n')}`
    : '';
  const explanation = `${displayLabel}\n\nWHAT IS HAPPENING?\n${narrative.what}\n\nWHY DOES IT MATTER?\n${narrative.why}\n\nWHAT TO WATCH NEXT?\n${narrative.watch}`;
  const technical = [pattern, level, touches].filter(Boolean).join('\n\n');
  const source = analysisSource?.usesUnderlying
    ? `TECHNICAL ANALYSIS SOURCE\nUnderlying: ${analysisSource.ticker ?? '—'} (${analysisSource.market ?? 'US'})\nTechnical levels are in ${analysisSource.currency ?? 'USD'}.`
    : '';
  return [explanation, source, `TECHNICAL DETAILS\n\n${technical}`].filter(Boolean).join('\n\n');
}

export function priceStructureSortRank(structure: PriceStructureResult | null | undefined): number {
  if (!structure) return 0;
  return Math.max(patternEventPriority(structure), keyLevelEventPriority(structure));
}

function patternEventPriority(structure: PriceStructureResult): number {
  const state = structure.primaryPatternState;
  if (state === 'BREAKDOWN' || state === 'CHANNEL_BROKEN') return 500;
  if (state === 'BREAKOUT' || state === 'BOUNCE_CONFIRMED') return 400;
  if (state === 'THIRD_TOUCH_TEST' || state === 'THIRD_RAIL_TEST' || state === 'LOWER_RAIL_RETEST')
    return 300;
  if (
    state === 'THIRD_TOUCH_APPROACHING' ||
    state === 'THIRD_RAIL_APPROACHING' ||
    state === 'LOWER_RAIL_APPROACHING' ||
    state === 'NEAR_APEX' ||
    state === 'TIGHTENING' ||
    state === 'DEVELOPING'
  )
    return 200;
  return 0;
}

function keyLevelEventPriority(structure: PriceStructureResult): number {
  const state = structure.keyLevelState;
  if (
    state === 'SUPPORT_BROKEN' ||
    state === 'BREAKDOWN_CONFIRMED' ||
    state === 'FAILED_BREAKOUT' ||
    state === 'CHANNEL_BROKEN'
  )
    return 500;
  if (state === 'BREAKOUT_CONFIRMED' || state === 'SUPPORT_RECLAIM' || state === 'BOUNCE_CONFIRMED')
    return 400;
  if (
    state === 'SUPPORT_TEST' ||
    state === 'RESISTANCE_TEST' ||
    state === 'CONFLUENCE_SUPPORT' ||
    state === 'CONFLUENCE_RESISTANCE' ||
    state === 'THIRD_RAIL_TEST' ||
    state === 'THIRD_TOUCH_TEST' ||
    state === 'LOWER_RAIL_RETEST'
  )
    return 300;
  if (
    state === 'APPROACHING_SUPPORT' ||
    state === 'APPROACHING_RESISTANCE' ||
    state === 'BREAKOUT_WATCH' ||
    state === 'BREAKDOWN_WATCH' ||
    state === 'THIRD_RAIL_APPROACHING' ||
    state === 'THIRD_TOUCH_APPROACHING'
  )
    return 200;
  return 0;
}

function friendlyPatternLabel(structure: PriceStructureResult): string {
  const key = `${structure.primaryPatternType}_${structure.primaryPatternState}`;
  const labels: Record<string, string> = {
    FALLING_WEDGE_DEVELOPING: 'FALLING WEDGE',
    FALLING_WEDGE_TIGHTENING: 'WEDGE TIGHTENING',
    FALLING_WEDGE_NEAR_APEX: 'WEDGE TIGHTENING',
    FALLING_WEDGE_BREAKOUT: 'WEDGE BREAKOUT',
    TIGHT_FALLING_WEDGE_DEVELOPING: 'TIGHT FALLING WEDGE',
    TIGHT_FALLING_WEDGE_TIGHTENING: 'TIGHT WEDGE — NEAR BREAKOUT',
    TIGHT_FALLING_WEDGE_NEAR_APEX: 'TIGHT WEDGE — NEAR BREAKOUT',
    TIGHT_FALLING_WEDGE_BREAKOUT: 'TIGHT WEDGE BREAKOUT',
    RISING_WEDGE_DEVELOPING: 'RISING WEDGE',
    RISING_WEDGE_TIGHTENING: 'RISING WEDGE — CAUTION',
    RISING_WEDGE_NEAR_APEX: 'RISING WEDGE — CAUTION',
    RISING_WEDGE_BREAKDOWN: 'WEDGE BREAKDOWN',
    TIGHT_RISING_WEDGE_DEVELOPING: 'TIGHT RISING WEDGE',
    TIGHT_RISING_WEDGE_TIGHTENING: 'TIGHT RISING WEDGE — CAUTION',
    TIGHT_RISING_WEDGE_NEAR_APEX: 'TIGHT RISING WEDGE — CAUTION',
    TIGHT_RISING_WEDGE_BREAKDOWN: 'TIGHT WEDGE BREAKDOWN',
    RISING_CHANNEL_THIRD_TOUCH_APPROACHING: 'NEAR CHANNEL SUPPORT',
    RISING_CHANNEL_THIRD_RAIL_APPROACHING: 'NEAR CHANNEL SUPPORT',
    RISING_CHANNEL_THIRD_TOUCH_TEST: 'TESTING CHANNEL SUPPORT',
    RISING_CHANNEL_THIRD_RAIL_TEST: 'TESTING CHANNEL SUPPORT',
    RISING_CHANNEL_LOWER_RAIL_APPROACHING: 'NEAR CHANNEL SUPPORT',
    RISING_CHANNEL_LOWER_RAIL_RETEST: 'RETESTING CHANNEL SUPPORT',
    RISING_CHANNEL_BOUNCE_CONFIRMED: 'CHANNEL BOUNCE CONFIRMED',
    RISING_CHANNEL_CHANNEL_BROKEN: 'CHANNEL SUPPORT BROKEN',
  };
  return (
    labels[key] ??
    (structure.label !== '—' ? structure.label : humanize(structure.primaryPatternType))
  );
}

function friendlyLevelLabel(structure: PriceStructureResult): string {
  const state = structure.keyLevelState;
  if (state === 'CONFLUENCE_SUPPORT') return 'STRONG SUPPORT ZONE';
  if (state === 'CONFLUENCE_RESISTANCE') return 'STRONG RESISTANCE ZONE';
  if (state === 'SUPPORT_BROKEN' || state === 'BREAKDOWN_CONFIRMED') return 'SUPPORT BROKEN';
  if (state === 'FAILED_BREAKOUT') return 'BREAKOUT FAILED';
  if (state === 'BREAKOUT_CONFIRMED') return 'BREAKOUT CONFIRMED';
  if (state === 'SUPPORT_RECLAIM') return 'SUPPORT RECOVERED';
  if (state === 'BREAKOUT_WATCH') return 'BREAKOUT WATCH';
  if (state === 'BREAKDOWN_WATCH') return 'SUPPORT AT RISK';
  const roleChanged =
    structure.keyLevelOriginalRole !== undefined &&
    structure.keyLevelOriginalRole !== structure.keyLevelRole;
  if (roleChanged && (state === 'SUPPORT_TEST' || state === 'RESISTANCE_TEST')) {
    return `TESTING ${currentLevelSource(structure).toUpperCase()}`;
  }
  if (roleChanged && (state === 'APPROACHING_SUPPORT' || state === 'APPROACHING_RESISTANCE')) {
    return `NEAR ${currentLevelSource(structure).toUpperCase()}`;
  }
  if (structure.keyLevelType === 'CONFLUENCE_ZONE') {
    if (structure.keyLevelRole === 'SUPPORT') return 'STRONG SUPPORT ZONE';
    if (structure.keyLevelRole === 'RESISTANCE') return 'STRONG RESISTANCE ZONE';
  }
  if (structure.keyLevelType === 'SWING_HIGH') {
    return state === 'RESISTANCE_TEST' ? 'TESTING RECENT HIGH' : 'NEAR RECENT HIGH';
  }
  if (structure.keyLevelType === 'SWING_LOW') {
    return state === 'SUPPORT_TEST' ? 'TESTING RECENT LOW' : 'NEAR RECENT LOW';
  }
  if (
    structure.keyLevelType.startsWith('FIB_') &&
    (state === 'SUPPORT_TEST' || state === 'RESISTANCE_TEST' || state.endsWith('_TEST'))
  ) {
    const fibLabel: Record<string, string> = {
      FIB_38_2: 'FIB 38.2',
      FIB_50: 'FIB 50',
      FIB_61_8: 'FIB 61.8',
    };
    return `TESTING ${fibLabel[structure.keyLevelType] ?? humanize(structure.keyLevelType).toUpperCase()}`;
  }
  if (structure.keyLevelType === 'CHANNEL_RAIL') {
    if (state === 'SUPPORT_TEST') return 'TESTING CHANNEL SUPPORT';
    if (state === 'APPROACHING_SUPPORT') return 'NEAR CHANNEL SUPPORT';
  }
  const labels: Record<string, string> = {
    SUPPORT_TEST: 'TESTING SUPPORT',
    RESISTANCE_TEST: 'TESTING RESISTANCE',
    APPROACHING_SUPPORT: 'NEAR SUPPORT',
    APPROACHING_RESISTANCE: 'NEAR RESISTANCE',
    SWING_HIGH_TEST: 'TESTING RECENT HIGH',
    APPROACHING_SWING_HIGH: 'NEAR RECENT HIGH',
    SWING_LOW_TEST: 'TESTING RECENT LOW',
    APPROACHING_SWING_LOW: 'NEAR RECENT LOW',
    FIB_38_2_TEST: 'TESTING FIB 38.2',
    FIB_50_TEST: 'TESTING FIB 50',
    FIB_61_8_TEST: 'TESTING FIB 61.8',
    THIRD_RAIL_APPROACHING: 'NEAR CHANNEL SUPPORT',
    THIRD_TOUCH_APPROACHING: 'NEAR CHANNEL SUPPORT',
    THIRD_RAIL_TEST: 'TESTING CHANNEL SUPPORT',
    THIRD_TOUCH_TEST: 'TESTING CHANNEL SUPPORT',
    LOWER_RAIL_RETEST: 'RETESTING CHANNEL SUPPORT',
    CHANNEL_BROKEN: 'CHANNEL SUPPORT BROKEN',
  };
  return labels[state] ?? humanize(state).toUpperCase();
}

function currentDecisionLabel(
  structure: PriceStructureResult,
  money: (value: number | null | undefined) => string,
): string {
  const interaction =
    structure.keyLevelState === 'SUPPORT_TEST'
      ? 'Testing'
      : structure.keyLevelState === 'RESISTANCE_TEST'
        ? 'Testing'
        : humanize(structure.keyLevelState);
  const source =
    structure.keyLevelType === 'CHANNEL_RAIL'
      ? structure.keyLevelRole === 'SUPPORT'
        ? 'Channel Support'
        : 'Channel Resistance'
      : currentLevelSource(structure);
  return `${interaction} ${source} @ ${money(structure.keyLevelPrice)}`;
}

function currentLevelSource(structure: PriceStructureResult): string {
  return lifecycleAwareSource(
    structure.keyLevelSources[0] ?? humanize(structure.keyLevelType),
    structure.keyLevelOriginalRole ?? structure.keyLevelRole,
    structure.keyLevelRole,
  );
}

function lifecycleAwareSource(source: string, originalRole: string, currentRole: string): string {
  if (originalRole === currentRole) return source;
  if (originalRole === 'RESISTANCE' && currentRole === 'SUPPORT') {
    if (source.toUpperCase().includes('WEDGE')) return 'Former Wedge Resistance — Now Support';
    return `Former ${source} — Now Support`;
  }
  if (originalRole === 'SUPPORT' && currentRole === 'RESISTANCE') {
    if (source.toUpperCase().includes('WEDGE')) return 'Former Wedge Support — Now Resistance';
    return `Former ${source} — Now Resistance`;
  }
  return source;
}

function friendlyNarrative(structure: PriceStructureResult): {
  what: string;
  why: string;
  watch: string;
} {
  const label = priceStructureLabel(structure);
  const support = structure.keyLevelRole === 'SUPPORT';
  const resistance = structure.keyLevelRole === 'RESISTANCE';
  const wedge = structure.primaryPatternType.includes('WEDGE');

  if (structure.hasHardStructuralNegative) {
    return {
      what: 'A broader structural breakdown remains active despite any local improvement at the current decision level.',
      why: 'A hard structural negative blocks new entry permission until the damaged pattern or level is repaired.',
      watch:
        'Treat any local support reclaim as monitoring context. Wait for the structural failure to clear before considering a new entry.',
    };
  }

  if (label === 'NEAR RECENT HIGH' || label === 'TESTING RECENT HIGH') {
    return {
      what: 'Price is approaching or testing an important recent high or resistance area.',
      why: 'Recent highs are resistance-oriented until price closes decisively above them.',
      watch:
        'A confirmed EOD close above the level may indicate breakout. Rejection below it keeps resistance active.',
    };
  }

  if (
    label === 'SUPPORT BROKEN' ||
    label === 'CHANNEL SUPPORT BROKEN' ||
    label.includes('WEDGE SUPPORT BROKEN')
  ) {
    return {
      what: 'Price closed decisively below an important support level.',
      why: 'The technical structure that had been supporting price is no longer holding.',
      watch:
        'Continued trading below the level confirms weakness. Recovery above it may signal that support is being recovered.',
    };
  }
  if (label === 'BREAKOUT CONFIRMED' || label.includes('WEDGE BREAKOUT')) {
    return {
      what: 'Price closed decisively above an important resistance level.',
      why: 'Resistance was cleared using the configured ATR confirmation threshold.',
      watch:
        'Continued strength is constructive. A pullback should hold the former resistance; a close back below it may signal a failed breakout.',
    };
  }
  if (label === 'SUPPORT RECOVERED') {
    return {
      what: 'Price recovered above an important level after previously trading below it.',
      why: 'A completed EOD recovery can turn former resistance back into usable support.',
      watch:
        'Holding above the recovered level is constructive. A close back below it would weaken the recovery.',
    };
  }
  if (label === 'BREAKOUT FAILED') {
    return {
      what: 'Price moved above resistance but closed back below the level.',
      why: 'The attempted breakout did not hold, so resistance remains active.',
      watch:
        'Watch for renewed strength above the breakout trigger or continued weakness below the rejection threshold.',
    };
  }
  if (label === 'STRONG SUPPORT ZONE') {
    return {
      what: 'Price is trading near an area where several technical support levels overlap.',
      why: 'Several independent support levels cluster near the same price, making the area more important than a single level.',
      watch:
        'Holding or bouncing above the zone is constructive. An EOD close below the breakdown trigger means support is weakening.',
    };
  }
  if (label === 'STRONG RESISTANCE ZONE') {
    return {
      what: 'Price is testing an area where several technical resistance levels overlap.',
      why: 'Several independent resistance levels cluster near the same price and may require stronger buying to clear.',
      watch:
        'An EOD close above the breakout trigger confirms strength. Rejection below the zone keeps resistance active.',
    };
  }
  if (wedge) {
    return {
      what: 'Price is moving inside an increasingly narrow range.',
      why: 'Buyers and sellers are compressing price into a smaller area, which can precede a meaningful move.',
      watch:
        'A close above upper resistance may confirm a bullish breakout. A close below lower support signals a bearish failure.',
    };
  }
  if (support) {
    return {
      what: 'Price is near or testing an important support level.',
      why: 'Buyers previously defended this area or another important technical reference exists here.',
      watch:
        'Holding support with improving momentum is constructive. An EOD close below the breakdown trigger means support failed.',
    };
  }
  if (resistance) {
    return {
      what: 'Price is near or testing an important level from below.',
      why: 'This area previously acted as resistance or contains a technical level price has not clearly broken.',
      watch:
        'An EOD close above the breakout trigger confirms a breakout. A touch followed by a close below keeps resistance active.',
    };
  }
  return {
    what: 'Price is interacting with an important technical structure.',
    why: 'The level or pattern may influence the next meaningful price move.',
    watch: 'Use the EOD confirmation and failure thresholds in the technical details below.',
  };
}

function hasPriceStructure(
  structure: PriceStructureResult | null | undefined,
): structure is PriceStructureResult {
  return (
    !!structure && (structure.primaryPatternType !== 'NONE' || structure.keyLevelState !== 'NONE')
  );
}

function humanize(value: string): string {
  return value
    .toLowerCase()
    .split('_')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ');
}
