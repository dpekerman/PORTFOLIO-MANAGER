import { PriceStructureResult } from './models/portfolio.models';

export function priceStructureLabel(
  structure: PriceStructureResult | null | undefined,
  maskValue: (value: number) => number = (value) => value,
): string {
  if (!hasPriceStructure(structure)) return '—';
  if (patternEventPriority(structure) > keyLevelEventPriority(structure)) {
    return patternLabel(structure);
  }
  if (structure.keyLevelPrice !== null && structure.keyLevelState !== 'NONE') {
    return `${humanize(structure.keyLevelState)} @ ${maskValue(structure.keyLevelPrice).toFixed(2)}`;
  }
  return patternLabel(structure);
}

export function priceStructureTooltip(
  structure: PriceStructureResult | null | undefined,
  maskValue: (value: number) => number = (value) => value,
): string {
  if (!hasPriceStructure(structure)) return '';
  const money = (value: number | null | undefined) =>
    value === null || value === undefined || value === 0 ? '—' : maskValue(value).toFixed(2);
  const optional = (value: number | null | undefined, suffix = '') =>
    value === null || value === undefined ? '—' : `${value}${suffix}`;

  const isChannel = structure.primaryPatternType.includes('CHANNEL');
  const isWedge = structure.primaryPatternType.includes('WEDGE');
  const touchDiagnostics = isChannel
    ? `Confirmed Lower Rail Touches: ${structure.channelTouchDetails.length}\nConfirmed Upper Rail Touches: —`
    : isWedge
      ? `Independent Upper Touches: ${structure.independentUpperTouchCount}\nIndependent Lower Touches: ${structure.independentLowerTouchCount}`
      : '';
  const pattern =
    structure.primaryPatternType === 'NONE'
      ? ''
      : `PRIMARY PATTERN\nType: ${humanize(structure.primaryPatternType)}\nState: ${humanize(structure.primaryPatternState)}\nHorizon: ${structure.patternHorizon}\nLookback: ${optional(structure.patternLookbackSessions, ' sessions')}\nQuality: ${structure.primaryPatternQuality}/100\nStart: ${structure.patternStart?.slice(0, 10) ?? '—'}\nUpper Rail: ${money(structure.upperTrendline)}\nLower Rail: ${money(structure.lowerTrendline)}\nContraction: ${structure.contractionPercent}%\nProjected Apex: ${structure.projectedApexDate?.slice(0, 10) ?? '—'}\nTrading Days to Apex: ${optional(structure.tradingDaysToApex)}${touchDiagnostics ? `\n${touchDiagnostics}` : ''}`;
  const supportOriented = structure.keyLevelRole === 'SUPPORT';
  const upperTriggerLabel = supportOriented ? 'Hold / Confirmation Trigger' : 'Breakout Trigger';
  const lowerTriggerLabel = supportOriented ? 'Breakdown Trigger' : 'Failure / Rejection Trigger';
  const currentDecision = currentDecisionLabel(structure, money);
  const level =
    structure.keyLevelState === 'NONE'
      ? ''
      : `CURRENT DECISION LEVEL\n${currentDecision}\nState: ${humanize(structure.keyLevelState)}\nCurrent Role: ${humanize(structure.keyLevelRole)}\nOriginal Role: ${humanize(structure.keyLevelOriginalRole ?? structure.keyLevelRole)}\nType: ${humanize(structure.keyLevelType)}\nZone: ${money(structure.keyLevelLow)} - ${money(structure.keyLevelHigh)}\nDistance: ${optional(structure.keyLevelDistancePercent, '%')}\nDistance ATR: ${optional(structure.keyLevelDistanceAtr)}\nDaily High: ${money(structure.dailyHigh)}\nDaily Low: ${money(structure.dailyLow)}\nEOD Close: ${money(structure.eodClose)}\nATR: ${money(structure.atr)}\n${upperTriggerLabel}: ${money(structure.breakoutTriggerPrice)}\n${lowerTriggerLabel}: ${money(structure.breakdownTriggerPrice)}\nSources: ${structure.keyLevelSources.join(', ') || '—'}\nConfluence: ${structure.keyLevelConfluenceCount}`;
  const touches = structure.channelTouchDetails.length
    ? `CHANNEL TOUCH HISTORY\n${structure.channelTouchDetails.map((touch) => `#${touch.touchNumber} ${touch.touchDate.slice(0, 10)} | Rail ${money(touch.railPrice)} | Low ${money(touch.actualLow)} | Bounce ${touch.bounceATR} ATR`).join('\n')}`
    : '';
  return [pattern, level, touches].filter(Boolean).join('\n\n');
}

export function priceStructureSortRank(structure: PriceStructureResult | null | undefined): number {
  if (!structure) return 0;
  return Math.max(patternEventPriority(structure), keyLevelEventPriority(structure));
}

function patternEventPriority(structure: PriceStructureResult): number {
  const state = structure.primaryPatternState;
  if (state === 'BREAKDOWN' || state === 'CHANNEL_BROKEN') return 500;
  if (state === 'BREAKOUT' || state === 'BOUNCE_CONFIRMED') return 400;
  if (state === 'THIRD_TOUCH_TEST' || state === 'LOWER_RAIL_RETEST') return 300;
  if (
    state === 'THIRD_TOUCH_APPROACHING' ||
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
  if (state === 'SUPPORT_BROKEN' || state === 'BREAKDOWN_CONFIRMED' || state === 'FAILED_BREAKOUT')
    return 500;
  if (state === 'BREAKOUT_CONFIRMED' || state === 'SUPPORT_RECLAIM' || state === 'BOUNCE_CONFIRMED')
    return 400;
  if (state === 'SUPPORT_TEST' || state === 'RESISTANCE_TEST') return 300;
  if (
    state === 'APPROACHING_SUPPORT' ||
    state === 'APPROACHING_RESISTANCE' ||
    state === 'BREAKOUT_WATCH'
  )
    return 200;
  return 0;
}

function patternLabel(structure: PriceStructureResult): string {
  return structure.label && structure.label !== '—'
    ? structure.label
    : humanize(structure.primaryPatternType);
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
      : (structure.keyLevelSources[0] ?? humanize(structure.keyLevelType));
  return `${interaction} ${source} @ ${money(structure.keyLevelPrice)}`;
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
