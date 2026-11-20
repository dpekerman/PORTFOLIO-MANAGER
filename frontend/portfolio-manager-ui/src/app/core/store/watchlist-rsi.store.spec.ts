import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import { vi } from 'vitest';
import { RsiScanResult } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { WatchlistRsiStore } from './watchlist-rsi.store';
import { WatchlistStore } from './watchlist.store';

describe('WatchlistRsiStore', () => {
  const items = signal<{ item: { symbol: string } }[]>([]);
  const api = { analyzeSymbols: vi.fn() };
  const result = (symbol: string) => ({ symbol }) as RsiScanResult;

  beforeEach(() => {
    items.set([]);
    vi.clearAllMocks();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    api.analyzeSymbols.mockImplementation((batch: string[]) => of(batch.map(result)));
    TestBed.configureTestingModule({
      providers: [
        { provide: WatchlistStore, useValue: { items } },
        { provide: PortfolioApiService, useValue: api },
      ],
    });
  });

  it('does nothing until a refresh is triggered, and ignores empty input', () => {
    const store = TestBed.inject(WatchlistRsiStore);
    store.triggerRefresh([]);
    expect(api.analyzeSymbols).not.toHaveBeenCalled();
    expect(store.rsiMap().size).toBe(0);
  });

  it('batches symbols by 50 and keys results by upper-case symbol', () => {
    const store = TestBed.inject(WatchlistRsiStore);
    const symbols = Array.from({ length: 60 }, (_, i) => `s${i}`);
    store.triggerRefresh(symbols);
    expect(api.analyzeSymbols).toHaveBeenCalledTimes(2);
    expect(store.rsiMap().size).toBe(60);
    expect(store.rsiMap().has('S0')).toBe(true);
    expect(store.rsiLoading()).toBe(false);
  });

  it('cancelRefresh drops the in-flight response', () => {
    const response = new Subject<RsiScanResult[]>();
    api.analyzeSymbols.mockReturnValue(response);
    const store = TestBed.inject(WatchlistRsiStore);
    store.triggerRefresh(['A']);
    expect(store.rsiLoading()).toBe(true);
    store.cancelRefresh();
    response.next([result('A')]);
    response.complete();
    expect(store.rsiLoading()).toBe(false);
    expect(store.rsiMap().size).toBe(0);
  });

  it('auto refresh fires only on symbol-set changes and is idempotent', () => {
    const store = TestBed.inject(WatchlistRsiStore);
    items.set([{ item: { symbol: 'B' } }, { item: { symbol: 'A' } }]);
    TestBed.tick();
    expect(api.analyzeSymbols).not.toHaveBeenCalled();

    store.enableAutoRefreshOnWatchlistChange();
    store.enableAutoRefreshOnWatchlistChange();
    TestBed.tick();
    expect(api.analyzeSymbols).toHaveBeenCalledTimes(1);
    expect(api.analyzeSymbols.mock.calls[0][0]).toEqual(['A', 'B']);

    items.set([{ item: { symbol: 'A' } }, { item: { symbol: 'B' } }]);
    TestBed.tick();
    expect(api.analyzeSymbols).toHaveBeenCalledTimes(1);

    items.update((l) => [...l, { item: { symbol: 'C' } }]);
    TestBed.tick();
    expect(api.analyzeSymbols).toHaveBeenCalledTimes(2);
  });
});
