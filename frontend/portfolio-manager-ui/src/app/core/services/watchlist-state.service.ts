import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { interval, switchMap } from 'rxjs';
import { WatchlistSummary } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class WatchlistStateService {
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly _items = signal<WatchlistSummary[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly count = computed(() => this._items().length);

  constructor() {
    this.refresh();

    // Auto-refresh every 60 seconds (watchlist is lower priority than portfolio)
    interval(60_000)
      .pipe(
        takeUntilDestroyed(),
        switchMap(() => this.api.getWatchlist()),
      )
      .subscribe({
        next: (data) => this._items.set(data),
        error: () => {},
      });
  }

  refresh(): void {
    this._loading.set(true);
    this._error.set(null);
    this.api.getWatchlist().subscribe({
      next: (data) => {
        this._items.set(data);
        this._loading.set(false);
      },
      error: () => {
        this._loading.set(false);
        this._error.set('Failed to load watchlist');
      },
    });
  }

  addItem(symbol: string, role = 'Strategic'): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.addWatchlistItem(symbol.toUpperCase(), '', role).subscribe({
        next: () => {
          this.snackBar.open(`${symbol.toUpperCase()} added to watchlist`, 'Close', {
            duration: 3000,
          });
          this.refresh();
          resolve();
        },
        error: (err) => {
          const msg = err?.error?.title ?? 'Failed to add symbol';
          this.snackBar.open(msg, 'Close', { duration: 4000 });
          reject(err);
        },
      });
    });
  }

  deleteItem(id: number, symbol: string): void {
    this.api.deleteWatchlistItem(id).subscribe({
      next: () => {
        this._items.update((items) => items.filter((s) => s.item.id !== id));
        this.snackBar.open(`${symbol} removed from watchlist`, 'Close', { duration: 3000 });
      },
      error: () => this.snackBar.open('Failed to remove', 'Close', { duration: 4000 }),
    });
  }

  updateRole(id: number, role: string): void {
    this.api.updateWatchlistRole(id, role).subscribe({
      next: () => {
        this._items.update((items) =>
          items.map((s) => (s.item.id === id ? { ...s, item: { ...s.item, role } } : s)),
        );
      },
      error: () => this.snackBar.open('Failed to update role', 'Close', { duration: 4000 }),
    });
  }
}
