import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ActionScoreDto, RsiScanResult } from '../../../core/models/portfolio.models';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';
import { DecisionEngineService } from '../../../core/services/decision-engine.service';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import { ScannerStateService } from '../../../core/services/scanner-state.service';
import { WatchlistRsiStateService } from '../../../core/services/watchlist-rsi-state.service';
import {
  PriorityCandidatesWidgetComponent,
  buildEligibleCandidates,
  isTechnicalDataPending,
  mergeRsiMaps,
} from './priority-candidates-widget.component';

// ── Pure-function tests: exercise the exact functions the component/template use ──────────────

describe('buildEligibleCandidates', () => {
  it('never backfills: when every candidate fails eligibility, the result is empty', () => {
    const scores = [scoreOf('A', 91), scoreOf('B', 88), scoreOf('C', 95)];
    const rsiMap = mapOf(['A', 'B', 'C'].map((s) => scanOf(s)));

    const result = buildEligibleCandidates(scores, rsiMap, new Map(), fakeEngine('AVOID'));

    expect(result).toEqual([]);
  });

  it('returns exactly one row when one candidate is eligible among twenty rejected', () => {
    const rejected = Array.from({ length: 20 }, (_, i) => scoreOf(`REJ${i}`, 90));
    const scores = [scoreOf('WINNER', 60), ...rejected];
    const symbols = scores.map((s) => s.symbol);
    const rsiMap = mapOf(symbols.map((s) => scanOf(s)));
    const engine = {
      translateForWatchlist: (scan: RsiScanResult) => ({
        finalAction: scan.symbol === 'WINNER' ? 'BUY WATCH' : 'AVOID',
        watchlistDiagnostics: { hasHardStructuralNegative: false },
      }),
    } as unknown as DecisionEngineService;

    const result = buildEligibleCandidates(scores, rsiMap, new Map(), engine);

    expect(result.length).toBe(1);
    expect(result[0].score.symbol).toBe('WINNER');
  });

  it('sorts eligible candidates descending by score, mixing HIGH and WATCH tiers', () => {
    const scores = [scoreOf('A', 51), scoreOf('B', 76), scoreOf('C', 68), scoreOf('D', 91)];
    const rsiMap = mapOf(scores.map((s) => scanOf(s.symbol)));

    const result = buildEligibleCandidates(scores, rsiMap, new Map(), fakeEngine('BUY WATCH'));

    expect(result.map((r) => r.score.symbol)).toEqual(['D', 'B', 'C', 'A']);
    expect(result.map((r) => r.eligibility.priorityLevel)).toEqual([
      'HIGH',
      'HIGH',
      'WATCH',
      'WATCH',
    ]);
  });

  it('excludes a candidate entirely when its symbol has no technical data at all', () => {
    const scores = [scoreOf('NODATA', 90)];

    const result = buildEligibleCandidates(
      scores,
      new Map(),
      new Map(),
      fakeEngine('ENTRY CANDIDATE'),
    );

    expect(result).toEqual([]);
  });
});

describe('mergeRsiMaps duplication/precedence', () => {
  it('collapses the same symbol from cache + overlay into a single entry, overlay winning', () => {
    const cached = mapOf([scanOf('DUP', { rsi: 40 })]);
    const overlay = [scanOf('DUP', { rsi: 25 })];

    const merged = mergeRsiMaps(cached, overlay);

    expect(merged.size).toBe(1);
    expect(merged.get('DUP')?.rsi).toBe(25);
  });

  it('is case-insensitive on symbol keys', () => {
    const cached = mapOf([scanOf('dup')]);
    const overlay = [scanOf('DUP')];

    const merged = mergeRsiMaps(cached, overlay);

    expect(merged.size).toBe(1);
  });
});

describe('isTechnicalDataPending', () => {
  it('is true only when the cache and both scanner chains are all empty', () => {
    expect(isTechnicalDataPending(0, 0, 0)).toBe(true);
  });

  it('is false as soon as any single source has data', () => {
    expect(isTechnicalDataPending(1, 0, 0)).toBe(false);
    expect(isTechnicalDataPending(0, 1, 0)).toBe(false);
    expect(isTechnicalDataPending(0, 0, 1)).toBe(false);
  });
});

// ── UI black-box tests: render the real template against fixed, controlled data ───────────────

@Component({
  selector: 'zz-host',
  template: '<app-priority-candidates-widget />',
  imports: [PriorityCandidatesWidgetComponent],
})
class HostComponent {}

describe('PriorityCandidatesWidgetComponent (rendered)', () => {
  async function render(
    scores: ActionScoreDto[],
    opts: {
      scanned?: string[];
      finalActionOf?: (symbol: string) => string;
      loading?: boolean;
    } = {},
  ): Promise<ComponentFixture<HostComponent>> {
    const scanned = opts.scanned ?? scores.map((s) => s.symbol);
    const finalActionOf = opts.finalActionOf ?? (() => 'ENTRY CANDIDATE');

    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        {
          provide: DashboardStateService,
          useValue: {
            actionScores: signal(scores),
            actionScoresLoading: signal(opts.loading ?? false),
            loadActionScores: () => {},
          },
        },
        {
          provide: ScannerStateService,
          useValue: { oversold: signal(scanned.map((s) => scanOf(s))), overbought: signal([]) },
        },
        { provide: WatchlistRsiStateService, useValue: { rsiMap: signal(new Map()) } },
        {
          provide: PortfolioApiService,
          useValue: { getLatestValueScreener: () => of({ watchlist: [], portfolio: [] }) },
        },
        {
          provide: DecisionEngineService,
          useValue: {
            translateForWatchlist: (scan: RsiScanResult) => ({
              finalAction: finalActionOf(scan.symbol),
              watchlistDiagnostics: { hasHardStructuralNegative: false },
            }),
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('renders eligible HIGH/WATCH candidates in score order with no NO_ADD or excluded rows', async () => {
    const scores = [scoreOf('HIGH1', 82), scoreOf('WATCH1', 61)];
    const fixture = await render(scores, {
      finalActionOf: () => 'ENTRY CANDIDATE',
    });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('HIGH1');
    expect(text).toContain('WATCH1');
    expect(text).not.toContain('NO ADD');
    expect(text).not.toContain('No eligible priority candidates');
    expect(text).not.toContain("Technical data hasn't been loaded");
    expect(text.indexOf('HIGH1')).toBeLessThan(text.indexOf('WATCH1')); // sorted desc by score
  });

  it('shows the "no eligible candidates" empty state when scored but none pass the gate', async () => {
    const scores = [scoreOf('REJECTED', 90)];
    const fixture = await render(scores, { finalActionOf: () => 'AVOID' });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('No eligible priority candidates right now');
    expect(text).not.toContain("Technical data hasn't been loaded");
    expect(text).not.toContain('REJECTED');
  });

  it('shows the "technical data pending" state distinctly when nothing has been analyzed yet', async () => {
    const scores = [scoreOf('WHATEVER', 90)];
    const fixture = await render(scores, { scanned: [] });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain("Technical data hasn't been loaded yet");
    expect(text).not.toContain('No eligible priority candidates right now');
  });

  it('shows the "no scores yet" state when the action-scores list itself is empty', async () => {
    const fixture = await render([]);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('No watchlist candidates scored yet');
  });
});

// ── Fixtures ────────────────────────────────────────────────────────────────────────────────

function fakeEngine(finalAction: string, hasHardStructuralNegative = false): DecisionEngineService {
  return {
    translateForWatchlist: () => ({
      finalAction,
      watchlistDiagnostics: { hasHardStructuralNegative },
    }),
  } as unknown as DecisionEngineService;
}

function scoreOf(symbol: string, totalScore: number): ActionScoreDto {
  return {
    symbol,
    companyName: symbol,
    holdingRole: 'Swing',
    watchlistTier: 'Active',
    portfolioNeedScore: 15,
    technicalScore: 10,
    fundamentalScore: 10,
    riskScore: 10,
    totalScore,
    badge: totalScore >= 75 ? 'HIGH_PRIORITY' : totalScore >= 50 ? 'WATCH' : 'NO_ADD',
    trendShift: '',
    rsi: 50,
    allocationStatus: '',
    currentPrice: 100,
  };
}

function scanOf(symbol: string, overrides: Partial<RsiScanResult> = {}): RsiScanResult {
  return { symbol, rsi: 50, ...overrides } as RsiScanResult;
}

function mapOf(scans: RsiScanResult[]): Map<string, RsiScanResult> {
  const map = new Map<string, RsiScanResult>();
  for (const s of scans) map.set(s.symbol.toUpperCase(), s);
  return map;
}
