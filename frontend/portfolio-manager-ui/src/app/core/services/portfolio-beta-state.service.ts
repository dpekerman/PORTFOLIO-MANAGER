import { Injectable, inject, signal } from '@angular/core';
import { PortfolioBetaResult } from '../models/portfolio.models';
import { PortfolioBetaApiService } from './portfolio-beta-api.service';

@Injectable({ providedIn: 'root' })
export class PortfolioBetaStateService {
  private readonly api = inject(PortfolioBetaApiService);

  private readonly _result = signal<PortfolioBetaResult | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly result = this._result.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  load(): void {
    if (this._loading()) return;
    this._loading.set(true);
    this._error.set(null);
    this.api.getBeta().subscribe({
      next: (r) => {
        this._result.set(r);
        this._loading.set(false);
      },
      error: (err) => {
        console.error('[PortfolioBeta] Failed to load beta', err);
        this._error.set('Failed to load portfolio beta');
        this._loading.set(false);
      },
    });
  }
}
