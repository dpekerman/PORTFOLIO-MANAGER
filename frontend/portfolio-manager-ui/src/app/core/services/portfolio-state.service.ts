import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { interval, switchMap } from 'rxjs';
import { AddPortfolioItemRequest, PortfolioSummary } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class PortfolioStateService {
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);

  // ── State signals ───────────────────────────────────────────────────────────
  private readonly _summaries = signal<PortfolioSummary[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly summaries = this._summaries.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly totalValue = computed(() =>
    this._summaries().reduce((acc, s) => {
      const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
      return acc + price * s.item.shares;
    }, 0),
  );

  readonly totalCost = computed(() =>
    this._summaries().reduce((acc, s) => acc + s.item.averageCostBasis * s.item.shares, 0),
  );

  readonly totalGainLoss = computed(() => this.totalValue() - this.totalCost());

  readonly totalGainLossPct = computed(() => {
    const cost = this.totalCost();
    return cost === 0 ? 0 : (this.totalGainLoss() / cost) * 100;
  });

  constructor() {
    // Initial load
    this.refresh();

    // Poll every 30 seconds
    interval(30_000)
      .pipe(
        takeUntilDestroyed(),
        switchMap(() => this.api.getAllQuotes()),
      )
      .subscribe({
        next: (data) => this._summaries.set(data),
        error: () => this._error.set('Auto-refresh failed'),
      });
  }

  refresh(): void {
    this._loading.set(true);
    this._error.set(null);
    this.api.getAllQuotes().subscribe({
      next: (data) => {
        this._summaries.set(data);
        this._loading.set(false);
      },
      error: (err) => {
        this._loading.set(false);
        this._error.set('Failed to load portfolio data');
        console.error(err);
      },
    });
  }

  addItem(request: AddPortfolioItemRequest): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.addItem(request).subscribe({
        next: () => {
          this.snackBar.open(`${request.symbol} added to portfolio`, 'Close', { duration: 3000 });
          this.refresh();
          resolve();
        },
        error: (err) => {
          this.snackBar.open('Failed to add stock', 'Close', { duration: 4000 });
          reject(err);
        },
      });
    });
  }

  deleteItem(id: number, symbol: string): void {
    this.api.deleteItem(id).subscribe({
      next: () => {
        this._summaries.update((items) => items.filter((s) => s.item.id !== id));
        this.snackBar.open(`${symbol} removed from portfolio`, 'Close', { duration: 3000 });
      },
      error: () => this.snackBar.open('Failed to remove stock', 'Close', { duration: 4000 }),
    });
  }
}
