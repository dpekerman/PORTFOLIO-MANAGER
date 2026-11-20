import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { SecurityAnalysisMapping } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { SecurityAnalysisMappingStore } from './security-analysis-mapping.store';

describe('SecurityAnalysisMappingStore', () => {
  const mapping = { tradingTicker: 'TQQQ' } as unknown as SecurityAnalysisMapping;
  const api = {
    getSecurityAnalysisMapping: vi.fn(),
    validateUnderlyingTicker: vi.fn(),
    saveSecurityAnalysisMapping: vi.fn(),
    removeSecurityAnalysisMapping: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    api.getSecurityAnalysisMapping.mockReturnValue(of(mapping));
    api.validateUnderlyingTicker.mockReturnValue(of(undefined));
    api.saveSecurityAnalysisMapping.mockReturnValue(of(mapping));
    api.removeSecurityAnalysisMapping.mockReturnValue(of(undefined));
    TestBed.configureTestingModule({ providers: [{ provide: PortfolioApiService, useValue: api }] });
  });

  it('loads, saves and removes a mapping', async () => {
    const store = TestBed.inject(SecurityAnalysisMappingStore);
    expect(await store.load('TQQQ')).toBe(mapping);
    expect(store.mapping()).toBe(mapping);
    expect(store.loading()).toBe(false);
    expect(await store.remove('TQQQ')).toBe(true);
    expect(store.mapping()).toBeNull();
    expect(await store.save('TQQQ', {} as never)).toBe(true);
    expect(store.mapping()).toBe(mapping);
  });

  it('normalises the underlying ticker on validate', async () => {
    const store = TestBed.inject(SecurityAnalysisMappingStore);
    await store.validate('TQQQ', ' qqq ');
    expect(api.validateUnderlyingTicker).toHaveBeenCalledWith('TQQQ', {
      underlyingTicker: 'QQQ',
      useUnderlyingForAnalysis: true,
    });
  });

  it('reports failures through error() and a falsy result', async () => {
    const store = TestBed.inject(SecurityAnalysisMappingStore);
    api.saveSecurityAnalysisMapping.mockReturnValue(throwError(() => ({ error: 'Bad ticker' })));
    expect(await store.save('TQQQ', {} as never)).toBe(false);
    expect(store.error()).toBe('Bad ticker');
    api.getSecurityAnalysisMapping.mockReturnValue(throwError(() => ({ error: { title: 'T' } })));
    expect(await store.load('TQQQ')).toBeNull();
    expect(store.error()).toBe('T');
    api.removeSecurityAnalysisMapping.mockReturnValue(throwError(() => ({})));
    expect(await store.remove('TQQQ')).toBe(false);
    expect(store.error()).toBe('Unable to update the underlying security mapping.');
    expect(store.loading()).toBe(false);
  });
});
