import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { interval, switchMap } from 'rxjs';
import { LogicMode, ScannerResponse } from '../models/portfolio.models';
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

  constructor() {
    this.refresh();
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
      .subscribe({ next: (r) => this._response.set(r) });
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
        },
        error: () => {
          this._error.set('Scanner unavailable');
          this._loading.set(false);
        },
      });
  }
}
