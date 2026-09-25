import { ActionScoreDto, PriceStructureResult, RsiScanResult } from '../models/portfolio.models';
import { DecisionEngineService, PageDecision } from './decision-engine.service';
import { resolvePriorityCandidateEligibility } from './priority-candidate-eligibility';

describe('resolvePriorityCandidateEligibility', () => {
  it('excludes a candidate with no scanner/RSI data (MISSING_TECHNICAL_DATA)', () => {
    const result = resolvePriorityCandidateEligibility(
      scoreOf(64),
      undefined,
      undefined,
      fakeEngine('ENTRY CANDIDATE'),
    );

    expect(result.eligible).toBe(false);
    expect(result.reasonExcluded).toEqual(['MISSING_TECHNICAL_DATA']);
  });

  it('Case A: BUY WATCH, no hard negative, score 64 -> eligible, WATCH tier', () => {
    const result = resolvePriorityCandidateEligibility(
      scoreOf(64),
      scanOf(),
      undefined,
      fakeEngine('BUY WATCH'),
    );

    expect(result.eligible).toBe(true);
    expect(result.priorityLevel).toBe('WATCH');
  });

  it('Case B: ENTRY CANDIDATE, score 82 -> eligible, HIGH tier', () => {
    const result = resolvePriorityCandidateEligibility(
      scoreOf(82),
      scanOf(),
      undefined,
      fakeEngine('ENTRY CANDIDATE'),
    );

    expect(result.eligible).toBe(true);
    expect(result.finalAction).toBe('ENTRY CANDIDATE');
    expect(result.priorityLevel).toBe('HIGH');
  });

  it('Case C: STARTER ENTRY, score 72 -> eligible, WATCH tier', () => {
    const result = resolvePriorityCandidateEligibility(
      scoreOf(72),
      scanOf(),
      undefined,
      fakeEngine('STARTER ENTRY'),
    );

    expect(result.eligible).toBe(true);
    expect(result.priorityLevel).toBe('WATCH');
  });

  it('Case D: hard negative overrides a high score -> ineligible', () => {
    const result = resolvePriorityCandidateEligibility(
      scoreOf(91),
      scanOf(),
      undefined,
      fakeEngine('BUY WATCH', true),
    );

    expect(result.eligible).toBe(false);
    expect(result.reasonExcluded).toContain('HARD_NEGATIVE_STRUCTURE');
  });

  it('Case E: WAIT FOR RECLAIM overrides a high score -> ineligible', () => {
    const result = resolvePriorityCandidateEligibility(
      scoreOf(86),
      scanOf(),
      undefined,
      fakeEngine('WAIT FOR RECLAIM'),
    );

    expect(result.eligible).toBe(false);
    expect(result.finalAction).toBe('WAIT FOR RECLAIM');
    expect(result.reasonExcluded).toContain('FINAL_ACTION_WAIT_FOR_RECLAIM');
  });

  it('Case F: eligible action but score below 50 -> ineligible (SCORE_BELOW_THRESHOLD)', () => {
    const result = resolvePriorityCandidateEligibility(
      scoreOf(47),
      scanOf(),
      undefined,
      fakeEngine('ENTRY CANDIDATE'),
    );

    expect(result.eligible).toBe(false);
    expect(result.reasonExcluded).toContain('SCORE_BELOW_THRESHOLD');
  });

  it('Case G: AVOID is never eligible regardless of score', () => {
    const result = resolvePriorityCandidateEligibility(
      scoreOf(95),
      scanOf(),
      undefined,
      fakeEngine('AVOID'),
    );

    expect(result.eligible).toBe(false);
    expect(result.reasonExcluded).toContain('FINAL_ACTION_AVOID');
  });

  describe('WATCH/HIGH boundary thresholds (score >= 50 WATCH, score >= 75 HIGH)', () => {
    it('score 49 (just below WATCH) -> ineligible, SCORE_BELOW_THRESHOLD', () => {
      const result = resolvePriorityCandidateEligibility(
        scoreOf(49),
        scanOf(),
        undefined,
        fakeEngine('BUY WATCH'),
      );

      expect(result.eligible).toBe(false);
      expect(result.priorityLevel).toBe('NONE');
      expect(result.reasonExcluded).toContain('SCORE_BELOW_THRESHOLD');
    });

    it('score 50 (WATCH lower boundary) -> eligible, WATCH', () => {
      const result = resolvePriorityCandidateEligibility(
        scoreOf(50),
        scanOf(),
        undefined,
        fakeEngine('BUY WATCH'),
      );

      expect(result.eligible).toBe(true);
      expect(result.priorityLevel).toBe('WATCH');
    });

    it('score 74 (WATCH upper boundary) -> eligible, WATCH', () => {
      const result = resolvePriorityCandidateEligibility(
        scoreOf(74),
        scanOf(),
        undefined,
        fakeEngine('BUY WATCH'),
      );

      expect(result.eligible).toBe(true);
      expect(result.priorityLevel).toBe('WATCH');
    });

    it('score 75 (HIGH lower boundary) -> eligible, HIGH', () => {
      const result = resolvePriorityCandidateEligibility(
        scoreOf(75),
        scanOf(),
        undefined,
        fakeEngine('ENTRY CANDIDATE'),
      );

      expect(result.eligible).toBe(true);
      expect(result.priorityLevel).toBe('HIGH');
    });
  });

  describe('REVERSAL WATCH policy', () => {
    it('is eligible (WATCH tier) once it clears the score threshold, same as any other whitelisted action', () => {
      const result = resolvePriorityCandidateEligibility(
        scoreOf(55),
        scanOf(),
        undefined,
        fakeEngine('REVERSAL WATCH'),
      );

      expect(result.eligible).toBe(true);
      expect(result.priorityLevel).toBe('WATCH');
    });

    it('is still excluded below the score threshold, like every other action', () => {
      const result = resolvePriorityCandidateEligibility(
        scoreOf(45),
        scanOf(),
        undefined,
        fakeEngine('REVERSAL WATCH'),
      );

      expect(result.eligible).toBe(false);
      expect(result.reasonExcluded).toContain('SCORE_BELOW_THRESHOLD');
    });

    it('cannot occur alongside a hard negative in the real engine (structurally impossible) — proven via the real engine, not a mock', () => {
      const engine = new DecisionEngineService();
      // Oversold + improving momentum context that would otherwise qualify for REVERSAL WATCH,
      // but with an active hard-negative structure — the real evaluateWatchlistEntry() checks
      // hard negative first, so it can never return REVERSAL WATCH here.
      const scan = scanOf({ rsi: 29, priceStructure: level('FAILED_BREAKOUT') });

      const result = resolvePriorityCandidateEligibility(scoreOf(90), scan, undefined, engine);

      expect(result.finalAction).not.toBe('REVERSAL WATCH');
      expect(result.eligible).toBe(false);
    });
  });

  it('XNDU regression: Waterfall/Falling Knife -> WAIT FOR RECLAIM via the REAL engine, excluded despite Bull Turn + score 48', () => {
    const engine = new DecisionEngineService();
    // Mirrors the originally-reported XNDU.TO case: RSI ~27, Bull Turn event, hard-negative
    // structure present, buy score effectively 0 -> canonical engine must resolve to a WAIT/AVOID
    // action, and the resolver must reject it even though the raw score (48) is close to threshold.
    const scan = scanOf({
      rsi: 27,
      rsiSignal: 22,
      rsiSignalAvailable: true,
      priceStructure: level('FAILED_BREAKOUT'),
    });

    const result = resolvePriorityCandidateEligibility(scoreOf(48), scan, undefined, engine);

    expect(result.eligible).toBe(false);
    expect(['WAIT FOR RECLAIM', 'AVOID']).toContain(result.finalAction);
    expect(result.reasonExcluded).toContain('SCORE_BELOW_THRESHOLD');
    expect(
      result.reasonExcluded.some(
        (r) => r === 'HARD_NEGATIVE_STRUCTURE' || r.startsWith('FINAL_ACTION_'),
      ),
    ).toBe(true);
  });

  it.each([
    'WATCH / NO CHASE',
    'WAIT FOR PULLBACK',
    'WAIT FOR REVERSAL',
    'WAIT FOR RECLAIM',
    'AVOID',
    'WATCH',
  ])('never renders %s (ineligible final action) even with score >= 75', (action) => {
    const result = resolvePriorityCandidateEligibility(
      scoreOf(90),
      scanOf(),
      undefined,
      fakeEngine(action),
    );

    expect(result.eligible).toBe(false);
  });

  it('reuses the real DecisionEngineService without re-deriving Final Action rules', () => {
    const engine = new DecisionEngineService();
    const scan = scanOf({ priceStructure: level('FAILED_BREAKOUT') });

    const result = resolvePriorityCandidateEligibility(scoreOf(86), scan, undefined, engine);

    // FAILED_BREAKOUT is a canonical hard negative -> WAIT FOR RECLAIM/AVOID, never a buy action.
    expect(result.eligible).toBe(false);
    expect(['WAIT FOR RECLAIM', 'AVOID']).toContain(result.finalAction);
  });
});

function fakeEngine(finalAction: string, hasHardStructuralNegative = false): DecisionEngineService {
  return {
    translateForWatchlist: () =>
      ({
        finalAction,
        watchlistDiagnostics: { hasHardStructuralNegative },
      }) as unknown as PageDecision,
  } as unknown as DecisionEngineService;
}

function scoreOf(totalScore: number): ActionScoreDto {
  return {
    symbol: 'TEST',
    companyName: 'Test Co',
    holdingRole: 'Swing',
    watchlistTier: 'Active',
    portfolioNeedScore: 15,
    technicalScore: 20,
    fundamentalScore: 15,
    riskScore: 10,
    totalScore,
    badge: totalScore >= 75 ? 'HIGH_PRIORITY' : totalScore >= 50 ? 'WATCH' : 'NO_ADD',
    trendShift: '',
    rsi: 50,
    allocationStatus: '',
    currentPrice: 100,
  };
}

function scanOf(overrides: Partial<RsiScanResult> = {}): RsiScanResult {
  return {
    symbol: 'TEST',
    companyName: 'Test Co',
    rsi: 50,
    currentPrice: 100,
    change: 0,
    changePercent: 0,
    scanType: 'Neutral',
    status: 'Confirmed',
    triggerDetails: '',
    sector: '',
    volume: 0,
    volumeRatio: 1,
    scannedAt: new Date().toISOString(),
    isDemo: false,
    stochasticK: 0,
    stochasticD: 0,
    rsiDivergence: 'None',
    stochasticsConfirm: false,
    macdValue: 0,
    macdSignalLine: 0,
    macdCrossover: 'Neutral',
    bollingerBreakout: false,
    bollingerPosition: 'Inside',
    bollingerPctB: 0,
    bollingerBandwidth: 0,
    volumeProjection: 0,
    positionSizingShares: 0,
    positionSizingRiskAmount: 0,
    positionSizingPositionValue: 0,
    positionSizingLimitingReason: '',
    volumeSignal: 'Neutral',
    dma50Deviation: 0,
    dma200Deviation: 0,
    has200Dma: true,
    reversalProbability: 'Low',
    macdHistogram: 0,
    macdHistDelta: 0,
    macdHistSlope: 'Neutral',
    logicMode: 'Enhanced',
    analystTargetPrice: 0,
    analystTargetUpside: 0,
    week52High: 0,
    week52Low: 0,
    rsiSignal: 50,
    rsiSignalAvailable: true,
    dailyAtr: 1,
    ema9Price: 100,
    sma20Price: 100,
    sma50Price: 95,
    ema10Price: 100,
    ema20Price: 100,
    dayHigh: 101,
    dayLow: 99,
    openPrice: 100,
    previousClose: 100,
    rsiDelta1D: 0,
    trendShift: '',
    sma200: 90,
    trendSetup200: '',
    dynamicStopLoss: 0,
    isTracked: false,
    stageStatus: '',
    turnStrength: '',
    chaseRisk: '',
    fibSwingLow: 0,
    fibSwingHigh: 0,
    fib38_2: 0,
    fib50: 0,
    fib61_8: 0,
    fib78_6: 0,
    fibZone: '',
    fibStatus: '',
    distanceToFib61_8Pct: 0,
    channelDirection: 'NONE',
    channelSlope: 0,
    lowerRailToday: 0,
    upperRailToday: 0,
    channelQuality: 0,
    priorConfirmedLowerTouches: 0,
    lastLowerTouchDate: null,
    distanceToLowerRailPercent: 0,
    distanceToLowerRailATR: 0,
    channelState: 'NONE',
    nearestOpenGapAbove: null,
    nearestOpenGapBelow: null,
    distanceToGapAbovePercent: null,
    distanceToGapBelowPercent: null,
    channelTouchDetails: [],
    priceStructure: level('NONE'),
    ...overrides,
  } as RsiScanResult;
}

function level(state: string, role = 'SUPPORT'): PriceStructureResult {
  return {
    primaryPatternType: 'NONE',
    primaryPatternState: 'NONE',
    keyLevelState: state,
    keyLevelRole: role,
    hasHardStructuralNegative: state === 'FAILED_BREAKOUT',
  } as PriceStructureResult;
}
