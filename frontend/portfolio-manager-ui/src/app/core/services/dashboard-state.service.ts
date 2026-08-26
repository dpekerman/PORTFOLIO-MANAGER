import { DestroyRef, Injectable, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DashboardResponse } from '../models/portfolio.models';
import { DashboardApiService } from './dashboard-api.service';
import { GlobalLoadingService } from './global-loading.service';

@Injectable({ providedIn: 'root' })
export class DashboardStateService {
  private readonly api = inject(DashboardApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly globalLoading = inject(GlobalLoadingService);
  private readonly _data = signal<DashboardResponse | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly data = this._data.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly hasData = this._data.asReadonly();

  constructor() {
    effect((onCleanup) => {
      if (this._loading()) {
        this.globalLoading.push();
        onCleanup(() => this.globalLoading.pop());
      }
    });
    this.load();
  }

  load(): void {
    this._loading.set(true);
    this._error.set(null);
    this.api
      .getLatest()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this._data.set(data);
          this._loading.set(false);
        },
        error: () => {
          this._error.set('Dashboard snapshot unavailable');
          this._loading.set(false);
        },
      });
  }

  refresh(): void {
    this._loading.set(true);
    this._error.set(null);
    this.api
      .refresh()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this._data.set(data);
          this._loading.set(false);
        },
        error: () => {
          this._error.set('Dashboard refresh failed');
          this._loading.set(false);
        },
      });
  }
}
