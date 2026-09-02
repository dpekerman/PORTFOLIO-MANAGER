import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { DailySignal, EodSignalsMeta } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

/**
 * EOD Signals state management service.
 * Handles polling for new EOD signals and tracking EOD window status.
 * Integrated into AppRefreshService for unified refresh workflow.
 */
@Injectable({ providedIn: 'root' })
export class EodSignalsStateService {
  private readonly api = inject(PortfolioApiService);

  // ── State Signals ──────────────────────────────────────────────────────────
  private readonly _signals = signal<DailySignal[]>([]);
  private readonly _meta = signal<EodSignalsMeta | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _eodWindowActive = signal(false);
  private readonly _lastPollAt = signal<Date | null>(null);

  readonly signals = this._signals.asReadonly();
  readonly meta = this._meta.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly eodWindowActive = this._eodWindowActive.asReadonly();
  readonly lastPollAt = this._lastPollAt.asReadonly();

  readonly newSignalCount = computed(() => {
    const signals = this._signals();
    const latestTradingDate = signals.reduce<string | null>((latest, signal) => {
      const tradingDate = signal.tradingDate ?? signal.signalDate;
      return latest === null || tradingDate > latest ? tradingDate : latest;
    }, null);
    return latestTradingDate === null
      ? 0
      : signals.filter((signal) => (signal.tradingDate ?? signal.signalDate) === latestTradingDate)
          .length;
  });

  constructor() {
    // Load initial EOD signals
    this.loadSignals();

    // Poll for new EOD signals every 5 minutes (skipped while tab hidden)
    interval(5 * 60_000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        if (document.visibilityState === 'visible') {
          this.loadSignals();
        }
      });

    // Poll EOD window status every 30 seconds (skipped while tab hidden)
    interval(30_000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        if (document.visibilityState === 'visible') {
          this.checkEodWindowStatus();
        }
      });

    // Initial status check
    this.checkEodWindowStatus();
  }

  /** Fetch new EOD signals from API (called during unified refresh). */
  loadSignals(): void {
    this._loading.set(true);
    this._error.set(null);

    this.api.getEodSignals({ page: 1, pageSize: 10 }).subscribe({
      next: (response) => {
        this._signals.set(response.items);
        this._loading.set(false);
        this._lastPollAt.set(new Date());
      },
      error: (err) => {
        console.error('Failed to load EOD signals:', err);
        this._error.set('Failed to load EOD signals');
        this._loading.set(false);
        this._lastPollAt.set(new Date());
      },
    });

    this.api.getEodSignalsMeta().subscribe({
      next: (meta) => this._meta.set(meta),
      error: () => {}, // Non-critical
    });
  }

  /** Check if the EOD window is currently active on the server. */
  private checkEodWindowStatus(): void {
    this.api.getEodWindowStatus().subscribe({
      next: (status) => {
        this._eodWindowActive.set(status.isActive);
      },
      error: (err) => {
        console.error('Failed to check EOD window status:', err);
      },
    });
  }
}
