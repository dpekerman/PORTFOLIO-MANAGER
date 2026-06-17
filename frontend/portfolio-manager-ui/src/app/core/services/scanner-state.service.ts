import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { interval, switchMap } from 'rxjs';
import { LogicMode, RsiScanResult, ScannerResponse } from '../models/portfolio.models';
import { ConfigService } from './config.service';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class ScannerStateService {
  private readonly api = inject(PortfolioApiService);
  private readonly configService = inject(ConfigService);

  private readonly _response = signal<ScannerResponse | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _logicMode = signal<LogicMode>('Enhanced');

  /** True when the EOD window is currently active on the server. */
  readonly eodWindowActive = signal(false);
  /** Summary of the last EOD window run result (e.g. "3 EOD Confirm signals"). */
  readonly lastEodRunSummary = signal<string | null>(null);

  readonly response = this._response.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly logicMode = this._logicMode.asReadonly();

  readonly oversold = computed(() => this._response()?.oversoldChain ?? []);
  readonly overbought = computed(() => this._response()?.overboughtChain ?? []);
  readonly isDemo = computed(() => this._response()?.isDemo ?? true);
  readonly market = computed(() => this._response()?.market ?? '');
  readonly scannedAt = computed(() => this._response()?.scannedAt ?? null);

  readonly confirmedOversold = computed(() =>
    this.oversold().filter((r) => r.status === 'Confirmed'),
  );
  readonly confirmedOverbought = computed(() =>
    this.overbought().filter((r) => r.status === 'Confirmed'),
  );
  readonly eodConfirmOversold = computed(() =>
    this.oversold().filter((r) => r.status === 'EodConfirm'),
  );
  readonly eodConfirmOverbought = computed(() =>
    this.overbought().filter((r) => r.status === 'EodConfirm'),
  );
  readonly totalEodConfirm = computed(
    () => this.eodConfirmOversold().length + this.eodConfirmOverbought().length,
  );

  // ── Ad-hoc analyzer in-memory state (survives route navigation) ────────────
  readonly adhocSymbols = signal<string[]>([]);
  readonly adhocResults = signal<RsiScanResult[]>([]);
  readonly adhocAnalyzed = signal(false);
  /** True while the initial DB restore is in flight (first load only). */
  readonly adhocSessionRestored = signal(false);

  constructor() {
    this.refresh();
    // Restore ad-hoc session from DB once on service init
    this.api.loadAdhocSession().subscribe({
      next: (session) => {
        if (session.symbols?.length && !this.adhocSessionRestored()) {
          this.adhocSymbols.set(session.symbols);
          if (session.results?.length) {
            this.adhocResults.set(session.results);
            this.adhocAnalyzed.set(true);
          }
        }
        this.adhocSessionRestored.set(true);
      },
      error: () => this.adhocSessionRestored.set(true),
    });
    // Restart auto-refresh whenever the configured interval changes
    toObservable(this.configService.config)
      .pipe(
        takeUntilDestroyed(),
        switchMap((cfg) => interval(cfg.scanIntervalSeconds * 1000)),
        switchMap(() => {
          const cfg = this.configService.config();
          return this.api.getRsiScan(
            false,
            cfg.rsiOversoldThreshold,
            cfg.rsiOverboughtThreshold,
            this._logicMode(),
          );
        }),
      )
      .subscribe({
        next: (r) => {
          this._response.set(r);
          this.updateEodSummary();
        },
      });

    // Poll EOD window status every 30 seconds
    interval(30_000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.checkEodWindowStatus());

    // Initial check
    this.checkEodWindowStatus();
  }

  private checkEodWindowStatus(): void {
    this.api.getEodWindowStatus().subscribe({
      next: (status) => {
        const wasActive = this.eodWindowActive();
        this.eodWindowActive.set(status.isActive);
        // When window closes, update the EOD summary
        if (wasActive && !status.isActive) {
          this.updateEodSummary();
        }
      },
      error: () => {}, // Silently fail — non-critical
    });
  }

  private updateEodSummary(): void {
    const count = this.totalEodConfirm();
    if (count > 0) {
      const os = this.eodConfirmOversold().length;
      const ob = this.eodConfirmOverbought().length;
      const parts: string[] = [];
      if (os > 0) parts.push(`${os} oversold`);
      if (ob > 0) parts.push(`${ob} overbought`);
      this.lastEodRunSummary.set(`${count} EOD Confirm signal(s): ${parts.join(', ')}`);
    }
  }

  toggleLogicMode(): void {
    const next: LogicMode = this._logicMode() === 'Legacy' ? 'Enhanced' : 'Legacy';
    this._logicMode.set(next);
    this.refresh(true);
  }

  refresh(force = false): void {
    this._loading.set(true);
    this._error.set(null);
    const cfg = this.configService.config();
    this.api
      .getRsiScan(force, cfg.rsiOversoldThreshold, cfg.rsiOverboughtThreshold, this._logicMode())
      .subscribe({
        next: (r) => {
          this._response.set(r);
          this._loading.set(false);
          this.updateEodSummary();
        },
        error: () => {
          this._error.set('Scanner unavailable');
          this._loading.set(false);
        },
      });
  }
}
