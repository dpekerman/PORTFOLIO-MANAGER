import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { DailySignal } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { AuthStore } from './auth.store';
import { EodSignalsStore } from './eod-signals.store';

const sig = (tradingDate: string | null, signalDate: string) =>
  ({ tradingDate, signalDate }) as unknown as DailySignal;

describe('EodSignalsStore', () => {
  const authenticated = signal(false);
  const api = {
    getEodSignals: vi.fn(),
    getEodSignalsMeta: vi.fn(),
    getEodWindowStatus: vi.fn(),
  };

  beforeEach(() => {
    authenticated.set(false);
    Object.values(api).forEach((fn) => fn.mockReset());
    api.getEodSignals.mockReturnValue(
      of({ items: [sig('2026-03-03', '2026-03-02'), sig(null, '2026-03-03'), sig('2026-03-02', '2026-03-01')] }),
    );
    api.getEodSignalsMeta.mockReturnValue(of({ total: 3 }));
    api.getEodWindowStatus.mockReturnValue(of({ isActive: true }));
    TestBed.configureTestingModule({
      providers: [
        { provide: PortfolioApiService, useValue: api },
        { provide: AuthStore, useValue: { isAuthenticated: authenticated } },
      ],
    });
  });

  function store(): InstanceType<typeof EodSignalsStore> {
    const s = TestBed.inject(EodSignalsStore);
    TestBed.tick();
    return s;
  }

  it('does not load until authenticated, then loads signals, meta and window status', () => {
    const s = store();
    expect(api.getEodSignals).not.toHaveBeenCalled();

    authenticated.set(true);
    TestBed.tick();

    expect(s.signals()).toHaveLength(3);
    expect(s.meta()).toEqual({ total: 3 });
    expect(s.eodWindowActive()).toBe(true);
    expect(s.loading()).toBe(false);
    expect(s.lastPollAt()).toBeInstanceOf(Date);
  });

  it('counts signals on the latest trading date, falling back to signalDate', () => {
    const s = store();
    authenticated.set(true);
    TestBed.tick();

    expect(s.newSignalCount()).toBe(2);
  });

  it('records an error and still stamps lastPollAt when loading fails', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    api.getEodSignals.mockReturnValue(throwError(() => new Error('x')));
    const s = store();
    authenticated.set(true);
    TestBed.tick();

    expect(s.error()).toBe('Failed to load EOD signals');
    expect(s.loading()).toBe(false);
    expect(s.lastPollAt()).toBeInstanceOf(Date);
  });
});
