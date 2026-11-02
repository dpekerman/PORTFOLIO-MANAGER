import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SignalsAuditQueryParams } from '../models/portfolio.models';
import { SignalsAuditApiService } from './signals-audit-api.service';

describe('SignalsAuditApiService effectiveness', () => {
  let api: SignalsAuditApiService;
  let http: HttpTestingController;

  const table: SignalsAuditQueryParams = {
    ticker: 'LMT',
    signalType: [],
    source: 'All',
    role: [],
    myAction: 'Pending',
    origin: 'All',
    includeBaselineInStats: false,
    includeUnaudited: false,
    status: 'Open',
    from: '2026-10-27',
    to: '2026-10-27',
    sortBy: 'signalDate',
    sortDir: 'desc',
    page: 1,
    pageSize: 25,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(SignalsAuditApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('summary asks the server to skip the effectiveness calculation', () => {
    api.summary(table).subscribe();
    const req = http.expectOne((r) => r.url === '/api/signals-audit/summary');
    expect(req.request.params.get('includeEffectiveness')).toBe('false');
    req.flush({});
  });

  it('effectiveness sends only the window and context filters, never ticker, dates, status or My Action', () => {
    api
      .effectiveness({ window: '180', signalType: ['A', 'B'], source: 'Portfolio', role: ['Core'], includeBaselineInStats: true })
      .subscribe();
    const req = http.expectOne((r) => r.url === '/api/signals-audit/effectiveness');
    const p = req.request.params;
    expect(p.get('window')).toBe('180');
    expect(p.getAll('signalType')).toEqual(['A', 'B']);
    expect(p.get('source')).toBe('Portfolio');
    expect(p.getAll('role')).toEqual(['Core']);
    expect(p.get('includeBaselineInStats')).toBe('true');
    for (const key of ['ticker', 'from', 'to', 'status', 'myAction']) expect(p.has(key)).toBe(false);
    req.flush({});
  });
});
