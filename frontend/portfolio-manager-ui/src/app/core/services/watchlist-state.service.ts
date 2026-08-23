import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EMPTY, filter, interval, switchMap, take } from 'rxjs';
import { WatchlistSummary } from '../models/portfolio.models';
import { AuthStateService } from './auth-state.service';
import { ConfigService } from './config.service';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class WatchlistStateService {
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly configService = inject(ConfigService);
  private readonly authState = inject(AuthStateService);

  private readonly _items = signal<WatchlistSummary[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly count = computed(() => this._items().length);

  /** Human-readable label for the configured auto-refresh interval. */
  readonly refreshIntervalLabel = computed(() => {
    const secs = this.configService.watchlistRefreshSeconds();
    if (secs === 0) return 'Auto-refresh disabled';
    const min = Math.round(secs / 60);
    return min >= 1 ? `Auto-refreshes every ${min}min` : `Auto-refreshes every ${secs}s`;
  });

  /** True when the current data was loaded from the DB snapshot (not a live Yahoo call). */
  readonly fromSnapshot = signal(false);

  constructor() {
    // Wait for auth before loading snapshot — prevents 401 race on app start
    toObservable(this.authState.isAuthenticated)
      .pipe(
        takeUntilDestroyed(),
        filter((a) => a),
        take(1),
      )
      .subscribe(() => this.loadSnapshot());

    // Restart auto-refresh whenever the configured interval changes; 0 = disabled
    toObservable(this.configService.config)
      .pipe(
        takeUntilDestroyed(),
        switchMap((cfg) =>
          cfg.watchlistRefreshSeconds > 0 ? interval(cfg.watchlistRefreshSeconds * 1000) : EMPTY,
        ),
        switchMap(() => this.api.getWatchlist()),
      )
      .subscribe({
        next: (data) => this._items.set(data),
        error: () => {},
      });
  }

  /** Load last snapshot from DB (instant — no Yahoo Finance call).
   * Falls back to a live refresh when no snapshot exists yet. */
  private loadSnapshot(): void {
    this._loading.set(true);
    this.api.getWatchlistSnapshot().subscribe({
      next: (data) => {
        if (data) {
          this._items.set(data);
          this.fromSnapshot.set(true);
          this._loading.set(false);
        } else {
          this.refresh();
        }
      },
      error: () => this.refresh(),
    });
  }

  refresh(): void {
    this._loading.set(true);
    this._error.set(null);
    this.fromSnapshot.set(false);
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

  updateFavorite(id: number, isFavorite: boolean): void {
    this.api.updateWatchlistFavorite(id, isFavorite).subscribe({
      next: () => {
        this._items.update((items) =>
          items.map((s) => (s.item.id === id ? { ...s, item: { ...s.item, isFavorite } } : s)),
        );
      },
      error: () => this.snackBar.open('Failed to update favourite', 'Close', { duration: 4000 }),
    });
  }

  updateNotes(id: number, notes: string): void {
    this.api.updateWatchlistNotes(id, notes).subscribe({
      next: () => {
        this._items.update((items) =>
          items.map((s) => (s.item.id === id ? { ...s, item: { ...s.item, notes } } : s)),
        );
      },
      error: () => this.snackBar.open('Failed to update notes', 'Close', { duration: 4000 }),
    });
  }
}
