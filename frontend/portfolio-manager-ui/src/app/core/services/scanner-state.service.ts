import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { EMPTY, interval, switchMap } from 'rxjs';
import {
  LogicMode,
  MarketIndexDto,
  RsiScanResult,
  ScannerResponse,
  YesterdayEodResponse,
} from '../models/portfolio.models';
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
  /** True when the current response was loaded from the DB snapshot (not a live scan). */
  private readonly _fromSnapshot = signal(false);

  /** True when the EOD window is currently active on the server. */
  readonly eodWindowActive = signal(false);
  /** Summary of the last EOD window run result (e.g. "3 EOD Confirm signals"). */
  readonly lastEodRunSummary = signal<string | null>(null);
  /** Yesterday's EOD CONFIRM signals (fetched on init and refreshed periodically). */
  readonly yesterdayEod = signal<YesterdayEodResponse | null>(null);
  /** Live market index prices (Dow, Nasdaq 100, S&P 500). Refreshed with each scan. */
  readonly marketIndices = signal<MarketIndexDto[]>([]);

  readonly response = this._response.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly logicMode = this._logicMode.asReadonly();
  /** True while showing cached DB snapshot data (before first live scan). */
  readonly fromSnapshot = this._fromSnapshot.asReadonly();
  /** Minutes since last scan. Null when no data loaded yet. */
  readonly snapshotAgeMinutes = computed(() => {
    const scannedAt = this._response()?.scannedAt;
    if (!scannedAt) return null;
    return Math.round((Date.now() - new Date(scannedAt).getTime()) / 60_000);
  });

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
    // Load persisted snapshot immediately — no Yahoo Finance call
    this.loadSnapshot();
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
    // Restart auto-refresh whenever the configured interval changes.
    // interval = 0 means disabled — emit EMPTY so no timer fires.
    toObservable(this.configService.config)
      .pipe(
        takeUntilDestroyed(),
        switchMap((cfg) =>
          cfg.scanIntervalSeconds > 0 ? interval(cfg.scanIntervalSeconds * 1000) : EMPTY,
        ),
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
          this._fromSnapshot.set(false);
          this.updateEodSummary();
        },
      });

    // Poll EOD window status every 30 seconds
    interval(30_000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.checkEodWindowStatus());

    // Initial check
    this.checkEodWindowStatus();
    // Load yesterday's EOD signals on init; refresh every 5 minutes
    this.loadYesterdayEod();
    interval(5 * 60_000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.loadYesterdayEod());

    // Load market indices on init; refresh every 5 minutes
    this.loadMarketIndices();
    interval(5 * 60_000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.loadMarketIndices());
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

  private loadYesterdayEod(): void {
    this.api.getYesterdayEod().subscribe({
      next: (data) => this.yesterdayEod.set(data),
      error: () => {}, // Non-critical — silently ignore
    });
  }

  private loadMarketIndices(): void {
    this.api.getMarketIndices().subscribe({
      next: (data) => this.marketIndices.set(data.indices),
      error: () => {}, // Non-critical
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

  /** Load last scan from DB snapshot (instant — no Yahoo Finance call).
   * If no snapshot exists yet, does a single initial live scan to populate the DB. */
  private loadSnapshot(): void {
    this._loading.set(true);
    this.api.getRsiSnapshot().subscribe({
      next: (r) => {
        if (r) {
          this._response.set(r);
          this._fromSnapshot.set(true);
          this.updateEodSummary();
          this._loading.set(false);
        } else {
          // No snapshot in DB yet — run one initial scan to populate it.
          this.refresh(false);
        }
      },
      error: () => {
        // Snapshot API unavailable — fall back to a live scan.
        this.refresh(false);
      },
    });
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
          this._fromSnapshot.set(false);
          this._loading.set(false);
          this.updateEodSummary();
          this.loadMarketIndices(); // refresh indices on every live scan
        },
        error: () => {
          this._error.set('Scanner unavailable');
          this._loading.set(false);
        },
      });
  }
}
