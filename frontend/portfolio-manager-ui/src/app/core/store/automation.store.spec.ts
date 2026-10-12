import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import {
  AutomationRunLogDto,
  AutomationSettingsDto,
  MissedDataRecoveryResultDto,
} from '../models/portfolio.models';
import { AutomationApiService } from '../services/automation-api.service';
import { AutomationStore } from './automation.store';

const run = (overallStatus: string): AutomationRunLogDto =>
  ({ overallStatus }) as AutomationRunLogDto;

describe('AutomationStore', () => {
  const api = {
    getSettings: vi.fn(),
    updateSettings: vi.fn(),
    getLastRun: vi.fn(),
    getHistory: vi.fn(),
    getTaskStatus: vi.fn(),
    getTimezoneDiagnostics: vi.fn(),
    runNow: vi.fn(),
    testWake: vi.fn(),
    getStatus: vi.fn(),
    rotateSecret: vi.fn(),
    cancel: vi.fn(),
    clearHistory: vi.fn(),
    setup: vi.fn(),
    recoverMissedData: vi.fn(),
    backupNow: vi.fn(),
  };

  beforeEach(() => {
    vi.useFakeTimers();
    Object.values(api).forEach((fn) => fn.mockReset());
    api.getSettings.mockReturnValue(of({ maxPollMinutes: 90 } as AutomationSettingsDto));
    api.getHistory.mockReturnValue(of([]));
    api.getTaskStatus.mockReturnValue(of({ exists: true }));
    api.getStatus.mockReturnValue(of(run('Running')));
    api.runNow.mockReturnValue(of({ runId: 'r1' }));
    api.testWake.mockReturnValue(of({ runId: 'r2' }));
    TestBed.configureTestingModule({
      providers: [{ provide: AutomationApiService, useValue: api }],
    });
  });

  afterEach(() => {
    TestBed.inject(AutomationStore).stopPolling();
    vi.useRealTimers();
  });

  function store(): InstanceType<typeof AutomationStore> {
    return TestBed.inject(AutomationStore);
  }

  it('does not call the API until something is requested', () => {
    store();
    expect(api.getSettings).not.toHaveBeenCalled();
  });

  it('loads settings and reports a failure', () => {
    const s = store();
    s.loadSettings();
    expect(s.settings()).toEqual({ maxPollMinutes: 90 });
    expect(s.loadingSettings()).toBe(false);

    api.getSettings.mockReturnValue(throwError(() => new Error('boom')));
    s.loadSettings();
    expect(s.error()).toBe('Failed to load Automation settings');
  });

  it('treats a missing last run as "no runs yet" rather than an error', () => {
    api.getLastRun.mockReturnValue(throwError(() => ({ status: 404 })));
    const s = store();

    s.loadLastRun();

    expect(s.lastRun()).toBeNull();
    expect(s.error()).toBeNull();
    expect(s.loadingLastRun()).toBe(false);
  });

  describe('run polling', () => {
    it('polls immediately and then every 3s while the run is Running', () => {
      const s = store();

      s.runNow();
      expect(s.runInFlight()).toBe(true);
      vi.advanceTimersByTime(0);
      expect(api.getStatus).toHaveBeenCalledTimes(1); // timer(0, 3000) does not wait 3s first

      vi.advanceTimersByTime(3000);
      expect(api.getStatus).toHaveBeenCalledTimes(2);
      expect(s.runInFlight()).toBe(true);
    });

    it('stops polling and reloads history once the run completes', () => {
      const s = store();
      s.runNow();
      vi.advanceTimersByTime(0);

      api.getStatus.mockReturnValue(of(run('Completed')));
      vi.advanceTimersByTime(3000);

      expect(s.runInFlight()).toBe(false);
      expect(s.activeOperation()).toBeNull();
      expect(api.getHistory).toHaveBeenCalledOnce();

      vi.advanceTimersByTime(9000);
      expect(api.getStatus).toHaveBeenCalledTimes(2); // unsubscribed
    });

    it('gives up after the configured maximum wait time', () => {
      api.getSettings.mockReturnValue(of({ maxPollMinutes: 1 } as AutomationSettingsDto));
      const s = store();
      s.loadSettings();

      s.runNow();
      vi.advanceTimersByTime(60_000);

      expect(s.runInFlight()).toBe(false);
      expect(s.activeOperation()).toBe('Automation exceeded its configured maximum wait time.');
    });

    it('defaults to a 90 minute poll budget when settings have not loaded', () => {
      const s = store();

      s.runNow();
      vi.advanceTimersByTime(89 * 60_000);
      expect(s.runInFlight()).toBe(true);

      vi.advanceTimersByTime(60_000);
      expect(s.runInFlight()).toBe(false);
    });

    it('stops polling when the status endpoint fails', () => {
      api.getStatus.mockReturnValue(throwError(() => new Error('boom')));
      const s = store();

      s.runNow();
      vi.advanceTimersByTime(0);

      expect(s.runInFlight()).toBe(false);
      expect(s.activeOperation()).toBe('Automation status polling timed out or failed.');
      vi.advanceTimersByTime(9000);
      expect(api.getStatus).toHaveBeenCalledOnce();
    });

    it('a second run replaces the first poll subscription', () => {
      const s = store();
      s.runNow();
      s.testWake();

      vi.advanceTimersByTime(3000);

      // Only the second run's timer survives, so one initial tick plus one 3s tick.
      expect(api.getStatus).toHaveBeenCalledTimes(2);
      expect(api.getStatus).toHaveBeenCalledWith('r2');
      expect(api.getStatus).not.toHaveBeenCalledWith('r1');
    });

    it('does not start polling when the run fails to start', () => {
      api.runNow.mockReturnValue(throwError(() => new Error('boom')));
      const s = store();

      s.runNow();

      expect(s.runInFlight()).toBe(false);
      expect(s.error()).toBe('Failed to start Run Automation Now');
      expect(api.getStatus).not.toHaveBeenCalled();
    });

    it('cancelRun does not stop the poll itself', () => {
      api.cancel.mockReturnValue(of(void 0));
      const s = store();
      s.runNow();

      s.cancelRun();

      expect(s.cancelling()).toBe(false);
      vi.advanceTimersByTime(3000);
      expect(api.getStatus).toHaveBeenCalledTimes(2);
    });
  });

  it('clearHistory empties both the list and the last run', () => {
    api.clearHistory.mockReturnValue(of(void 0));
    const s = store();

    s.clearHistory();

    expect(s.history()).toEqual([]);
    expect(s.lastRun()).toBeNull();
    expect(s.activeOperation()).toBeNull();
  });

  it('clearHistory surfaces the server message', () => {
    api.clearHistory.mockReturnValue(throwError(() => ({ error: { message: 'locked' } })));
    const s = store();

    s.clearHistory();

    expect(s.error()).toBe('locked');
    expect(s.clearingHistory()).toBe(false);
  });

  it('setup reloads settings and task status', () => {
    api.setup.mockReturnValue(of(void 0));
    const s = store();

    s.setup();

    expect(s.settingUp()).toBe(false);
    expect(api.getSettings).toHaveBeenCalledOnce();
    expect(api.getTaskStatus).toHaveBeenCalledOnce();
  });

  it('recoverMissedData reports a non-Completed status in activeOperation', () => {
    api.recoverMissedData.mockReturnValue(
      of({ status: 'Partial' } as MissedDataRecoveryResultDto),
    );
    const s = store();

    s.recoverMissedData();

    expect(s.recovering()).toBe(false);
    expect(s.activeOperation()).toBe('Fix Missing Data ended with status: Partial.');
  });

  it('backupNow clears the previous result before running', () => {
    api.backupNow.mockReturnValue(of({ success: true }));
    const s = store();

    s.backupNow();

    expect(s.backupResult()).toEqual({ success: true });
    expect(s.backingUp()).toBe(false);
    expect(s.activeOperation()).toBeNull();
  });
});
