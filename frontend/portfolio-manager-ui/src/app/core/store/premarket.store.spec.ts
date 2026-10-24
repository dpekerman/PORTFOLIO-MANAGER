import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { PreMarketRunDetail } from '../models/premarket.models';
import { PreMarketApiService } from '../services/premarket-api.service';
import { AuthStore } from './auth.store';
import { PreMarketStore } from './premarket.store';

const detail = { id: 7, status: 'COMPLETE', emailStatus: 'NOT_SENT', report: null } as unknown as PreMarketRunDetail;

describe('PreMarketStore', () => {
  const authenticated = signal(false);
  const api = {
    getLatest: vi.fn(),
    listRuns: vi.fn(),
    listSources: vi.fn(),
    getConfig: vi.fn(),
    getStatus: vi.fn(),
    run: vi.fn(),
    addSource: vi.fn(),
    updateSource: vi.fn(),
    approveSource: vi.fn(),
    archiveSource: vi.fn(),
    testSource: vi.fn(),
    feedback: vi.fn(),
    testEmail: vi.fn(),
    resend: vi.fn(),
    saveConfig: vi.fn(),
    getRun: vi.fn(),
  };
  const snackBar = { open: vi.fn() };

  beforeEach(() => {
    authenticated.set(false);
    Object.values(api).forEach((fn) => fn.mockReset());
    snackBar.open.mockReset();
    api.getLatest.mockReturnValue(of(detail));
    api.listRuns.mockReturnValue(of([]));
    api.listSources.mockReturnValue(of([]));
    api.getConfig.mockReturnValue(of({ id: 1 }));
    api.getStatus.mockReturnValue(of({ aiHealthy: false }));
    TestBed.configureTestingModule({
      providers: [
        { provide: PreMarketApiService, useValue: api },
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AuthStore, useValue: { isAuthenticated: authenticated } },
      ],
    });
  });

  function store(): InstanceType<typeof PreMarketStore> {
    const s = TestBed.inject(PreMarketStore);
    TestBed.tick();
    return s;
  }

  it('does not load until authenticated, then loads the latest report', () => {
    const s = store();
    expect(api.getLatest).not.toHaveBeenCalled();
    authenticated.set(true);
    TestBed.tick();
    expect(api.getLatest).toHaveBeenCalledTimes(1);
    expect(s.report()?.id).toBe(7);
  });

  it('runNow reloads the report and clears the running flag', () => {
    api.run.mockReturnValue(of({ id: 7, status: 'COMPLETE', emailStatus: 'NOT_SENT', emailMessage: null, error: null }));
    const s = store();
    s.runNow({ asOfDate: '2026-10-20', dryRun: true, useMockNews: true, sendEmail: false });
    expect(api.run).toHaveBeenCalledWith({ asOfDate: '2026-10-20', dryRun: true, useMockNews: true, sendEmail: false });
    expect(s.running()).toBe(false);
    expect(api.getLatest).toHaveBeenCalled();
  });

  it('shows the server validation message when a source is rejected', () => {
    api.addSource.mockReturnValue(throwError(() => ({ error: { error: 'Only https URLs are allowed.' } })));
    const s = store();
    s.saveSource(null, {
      name: 'x', url: 'http://x', category: 'GLOBAL', priority: 1, enabled: false,
      markets: 'US', refreshMinutes: 60, timeoutSeconds: 10, maxItems: 5,
    });
    expect(snackBar.open).toHaveBeenCalledWith('Only https URLs are allowed.', 'Close', { duration: 4000 });
  });

  it('sends feedback without touching other state', () => {
    api.feedback.mockReturnValue(of(undefined));
    const s = store();
    s.sendFeedback(7, 'USEFUL', 'AAPL');
    expect(api.feedback).toHaveBeenCalledWith(7, 'USEFUL', 'AAPL');
  });
});
