import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NEVER, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { AuthStore } from './auth.store';
import { ConfigStore } from './config.store';
import { ScannerStore } from './scanner.store';

const scan = (statuses: Array<[string, 'Oversold' | 'Overbought']>) => ({
  scannedAt: new Date().toISOString(),
  isDemo: false,
  market: 'US',
  oversoldChain: statuses
    .filter(([, t]) => t === 'Oversold')
    .map(([status]) => ({ status, symbol: 'AAA' })),
  overboughtChain: statuses
    .filter(([, t]) => t === 'Overbought')
    .map(([status]) => ({ status, symbol: 'BBB' })),
});

describe('ScannerStore', () => {
  const authenticated = signal(false);
  const config = signal({
    scanIntervalSeconds: 0,
    rsiOversoldThreshold: 30,
    rsiOverboughtThreshold: 70,
  });
  const api = {
    getRsiSnapshot: vi.fn(),
    getRsiScan: vi.fn(),
    loadAdhocSession: vi.fn(),
    getEodWindowStatus: vi.fn(),
    getYesterdayEod: vi.fn(),
    getMarketIndices: vi.fn(),
  };

  beforeEach(() => {
    authenticated.set(false);
    Object.values(api).forEach((fn) => fn.mockReset());
    api.getRsiSnapshot.mockReturnValue(of(scan([['Confirmed', 'Oversold']])));
    api.getRsiScan.mockReturnValue(of(scan([['EodConfirm', 'Oversold']])));
    api.loadAdhocSession.mockReturnValue(of({ symbols: [], results: [] }));
    api.getEodWindowStatus.mockReturnValue(of({ isActive: false }));
    api.getYesterdayEod.mockReturnValue(of(null));
    api.getMarketIndices.mockReturnValue(of({ indices: [] }));
    TestBed.configureTestingModule({
      providers: [
        { provide: PortfolioApiService, useValue: api },
        { provide: ConfigStore, useValue: { config } },
        { provide: AuthStore, useValue: { isAuthenticated: authenticated } },
      ],
    });
  });

  function store(): InstanceType<typeof ScannerStore> {
    const s = TestBed.inject(ScannerStore);
    TestBed.tick();
    return s;
  }

  it('does not load until the user is authenticated, then shows the snapshot without a live scan', () => {
    const s = store();
    expect(api.getRsiSnapshot).not.toHaveBeenCalled();

    authenticated.set(true);
    TestBed.tick();

    expect(s.fromSnapshot()).toBe(true);
    expect(s.loading()).toBe(false);
    expect(s.confirmedOversold()).toHaveLength(1);
    expect(api.getRsiScan).not.toHaveBeenCalled();
  });

  it('falls back to a live scan when no snapshot exists', () => {
    api.getRsiSnapshot.mockReturnValue(of(null));
    const s = store();
    authenticated.set(true);
    TestBed.tick();

    expect(api.getRsiScan).toHaveBeenCalledWith(false, 30, 70, 'Enhanced');
    expect(s.fromSnapshot()).toBe(false);
    expect(s.totalEodConfirm()).toBe(1);
    expect(s.lastEodRunSummary()).toBe('1 EOD Confirm signal(s): 1 oversold');
  });

  it('reports an error when the live scan fails', () => {
    api.getRsiSnapshot.mockReturnValue(throwError(() => new Error('x')));
    api.getRsiScan.mockReturnValue(throwError(() => new Error('x')));
    const s = store();
    authenticated.set(true);
    TestBed.tick();

    expect(s.error()).toBe('Scanner unavailable');
    expect(s.loading()).toBe(false);
  });

  it('toggles the logic mode and forces a refresh with it', () => {
    const s = store();
    s.toggleLogicMode();

    expect(s.logicMode()).toBe('Legacy');
    expect(api.getRsiScan).toHaveBeenCalledWith(true, 30, 70, 'Legacy');
  });

  it('cancelRefresh stops an in-flight scan and clears loading', () => {
    api.getRsiScan.mockReturnValue(NEVER);
    const s = store();
    s.refresh(true);
    expect(s.loading()).toBe(true);

    s.cancelRefresh();
    expect(s.loading()).toBe(false);
  });

  it('restores the ad-hoc session once and lets consumers edit it through methods', () => {
    api.loadAdhocSession.mockReturnValue(
      of({ symbols: ['AAPL'], results: [{ symbol: 'AAPL', scanType: 'Oversold' }] }),
    );
    const s = store();
    authenticated.set(true);
    TestBed.tick();

    expect(s.adhocSymbols()).toEqual(['AAPL']);
    expect(s.adhocAnalyzed()).toBe(true);
    expect(s.adhocSessionRestored()).toBe(true);

    s.setAdhocSymbols([]);
    s.setAdhocResults([]);
    s.setAdhocAnalyzed(false);
    expect(s.adhocSymbols()).toEqual([]);
    expect(s.adhocAnalyzed()).toBe(false);
  });

  it('marks the ad-hoc session restored even when the restore fails', () => {
    api.loadAdhocSession.mockReturnValue(throwError(() => new Error('x')));
    const s = store();
    authenticated.set(true);
    TestBed.tick();

    expect(s.adhocSessionRestored()).toBe(true);
  });
});
