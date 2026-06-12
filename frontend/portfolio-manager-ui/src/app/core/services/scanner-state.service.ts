import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval, switchMap } from 'rxjs';
import { ScannerResponse } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class ScannerStateService {
  private readonly api = inject(PortfolioApiService);

  private readonly _response = signal<ScannerResponse | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly response = this._response.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

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
    // Refresh scanner every 5 minutes
    interval(5 * 60_000)
      .pipe(
        takeUntilDestroyed(),
        switchMap(() => this.api.getRsiScan()),
      )
      .subscribe({ next: (r) => this._response.set(r) });
  }

  refresh(force = false): void {
    this._loading.set(true);
    this._error.set(null);
    this.api.getRsiScan(force).subscribe({
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
