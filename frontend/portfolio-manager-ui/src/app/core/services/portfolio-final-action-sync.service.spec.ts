import { TestBed } from '@angular/core/testing';
import {
  PortfolioItem,
  PortfolioSummary,
  PriceStructureResult,
  RsiScanResult,
  StockQuote,
} from '../models/portfolio.models';
import { CashStateService } from './cash-state.service';
import { DecisionEngineService } from './decision-engine.service';
import { OptionStateService } from './option-state.service';
import { PortfolioApiService } from './portfolio-api.service';
import { PortfolioFinalActionSyncService } from './portfolio-final-action-sync.service';
import { PortfolioStateService } from './portfolio-state.service';

const item = (over: Partial<PortfolioItem> = {}): PortfolioItem =>
  ({
    id: 1,
    symbol: 'NVDA.TO',
    shares: 400,
    averageCostBasis: 49.52,
    isManual: false,
    transactionType: 'OPEN',
    accountType: 'TFSA_D_TD',
    openDate: '2026-01-01T00:00:00',
    ...over,
  }) as PortfolioItem;

const summary = (it: PortfolioItem, price: number | null): PortfolioSummary =>
  ({
    item: it,
    quote: price === null ? null : ({ currentPrice: price } as StockQuote),
  }) as PortfolioSummary;

describe('PortfolioFinalActionSyncService.buildContext (CDR currency handling)', () => {
  let service: PortfolioFinalActionSyncService;
  let engine: DecisionEngineService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        PortfolioFinalActionSyncService,
        { provide: PortfolioStateService, useValue: {} },
        { provide: CashStateService, useValue: {} },
        { provide: OptionStateService, useValue: {} },
        { provide: PortfolioApiService, useValue: {} },
      ],
    });
    service = TestBed.inject(PortfolioFinalActionSyncService);
    engine = TestBed.inject(DecisionEngineService);
  });

  it('measures gain and position size in CAD for a CDR analyzed via its USD underlying', () => {
    const nvdaTo = item();
    // CAD quote 52.82; scanner/analysis price is the USD underlying (~237) with a USD 52w high.
    const scan = scanOf({
      currentPrice: 237,
      week52High: 240,
      usesUnderlyingSecurity: true,
      analysisCurrency: 'USD',
    });

    const ctx = service.buildContext(nvdaTo, scan, [summary(nvdaTo, 52.82)], 782_000);

    expect(ctx.unrealizedGainPct).toBeCloseTo(6.66, 1);
    expect(ctx.positionSizePct).toBeCloseTo(2.7, 1);
    expect(ctx.distanceFrom52WeekHighPct).toBeCloseTo(-1.25, 1);
  });

  it('does not produce Sell 50% for a CDR with a small real gain and size', () => {
    const nvdaTo = item({ accountType: 'RRSP' });
    const scan = scanOf({
      currentPrice: 237,
      week52High: 240,
      usesUnderlyingSecurity: true,
      analysisCurrency: 'USD',
    });
    const ctx = service.buildContext(nvdaTo, scan, [summary(nvdaTo, 52.82)], 782_000);

    const decision = engine.translateForPortfolio(scan, 'Core', true, ctx);

    expect(decision.finalAction).not.toContain('Sell 50%');
    expect(decision.finalAction).not.toContain('Trim');
  });

  it('leaves gain and size null for a CDR without a CAD quote instead of using the USD price', () => {
    const nvdaTo = item();
    const scan = scanOf({ currentPrice: 237, usesUnderlyingSecurity: true });

    const ctx = service.buildContext(nvdaTo, scan, [summary(nvdaTo, null)], 782_000);

    expect(ctx.unrealizedGainPct).toBeNull();
    expect(ctx.positionSizePct).toBeNull();
  });

  it('still fires Rule 7 for a normal holding with a real 100% gain near its high', () => {
    const shop = item({ symbol: 'SHOP.TO', averageCostBasis: 100, shares: 100 });
    const scan = scanOf({ currentPrice: 200, week52High: 201 });

    const ctx = service.buildContext(shop, scan, [summary(shop, 200)], 200_000);
    const decision = engine.translateForPortfolio(scan, 'Core', true, ctx);

    expect(ctx.unrealizedGainPct).toBeCloseTo(100, 5);
    expect(ctx.positionSizePct).toBeCloseTo(10, 5);
    expect(decision.finalAction).toContain('Sell 50%');
  });
});

function scanOf(overrides: Partial<RsiScanResult> = {}): RsiScanResult {
  return {
    symbol: 'TEST',
    companyName: 'Test Co',
    rsi: 55,
    currentPrice: 100,
    change: 0,
    changePercent: 0,
    scanType: 'Neutral',
    status: 'Neutral',
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
    priceStructure: {
      primaryPatternType: 'NONE',
      primaryPatternState: 'NONE',
      keyLevelState: 'NONE',
      keyLevelRole: 'SUPPORT',
      hasHardStructuralNegative: false,
    } as PriceStructureResult,
    ...overrides,
  } as RsiScanResult;
}
