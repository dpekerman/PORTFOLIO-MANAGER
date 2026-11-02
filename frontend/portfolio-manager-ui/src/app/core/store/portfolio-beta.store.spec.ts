import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { PortfolioBetaResult } from '../models/portfolio.models';
import { PortfolioBetaApiService } from '../services/portfolio-beta-api.service';
import { PortfolioBetaStore } from './portfolio-beta.store';

describe('PortfolioBetaStore', () => {
  const result = {
    topContributors: [{ symbol: 'aapl', beta: 1.2 }],
  } as unknown as PortfolioBetaResult;
  const api = { getBeta: vi.fn() };

  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    api.getBeta.mockReturnValue(of(result));
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [{ provide: PortfolioBetaApiService, useValue: api }],
    });
  });

  it('loads the result and exposes fetched betas by upper-case symbol', () => {
    const store = TestBed.inject(PortfolioBetaStore);
    store.load();
    expect(store.result()).toBe(result);
    expect(store.loading()).toBe(false);
    expect(store.betaForSymbol('AAPL')).toBe(1.2);
    expect(store.betaForSymbol('msft')).toBeNull();
  });

  it('override wins, persists, and triggers a reload sending the overrides', () => {
    const store = TestBed.inject(PortfolioBetaStore);
    store.load();
    store.setOverride('aapl', 2);
    expect(store.betaForSymbol('AAPL')).toBe(2);
    expect(JSON.parse(localStorage.getItem('pm_beta_overrides_v1')!)).toEqual({ AAPL: 2 });
    expect(api.getBeta).toHaveBeenLastCalledWith({ AAPL: 2 });
    store.setOverride('AAPL', null);
    expect(store.betaForSymbol('AAPL')).toBe(1.2);
    expect(api.getBeta).toHaveBeenLastCalledWith(undefined);
  });

  it('restores overrides from storage', () => {
    localStorage.setItem('pm_beta_overrides_v1', '{"TSLA":1.5}');
    expect(TestBed.inject(PortfolioBetaStore).betaForSymbol('tsla')).toBe(1.5);
  });

  it('surfaces a load failure', () => {
    api.getBeta.mockReturnValue(throwError(() => new Error('x')));
    const store = TestBed.inject(PortfolioBetaStore);
    store.load();
    expect(store.error()).toBe('Failed to load portfolio beta');
    expect(store.loading()).toBe(false);
  });
});
