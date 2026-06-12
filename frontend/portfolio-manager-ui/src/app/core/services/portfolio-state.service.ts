import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { interval, switchMap } from 'rxjs';
import {
  AddManualPositionRequest,
  AddPortfolioItemRequest,
  PortfolioSummary,
} from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class PortfolioStateService {
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);

  // ── State signals ───────────────────────────────────────────────────────────
  private readonly _summaries = signal<PortfolioSummary[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _selectedIds = signal<Set<number>>(new Set());

  readonly summaries = this._summaries.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly selectedIds = this._selectedIds.asReadonly();

  readonly selectedCount = computed(() => this._selectedIds().size);
  readonly hasSelection = computed(() => this._selectedIds().size > 0);

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
    this.refresh();

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

  toggleSelection(id: number): void {
    this._selectedIds.update((s) => {
      const copy = new Set(s);
      copy.has(id) ? copy.delete(id) : copy.add(id);
      return copy;
    });
  }

  selectAll(): void {
    this._selectedIds.set(new Set(this._summaries().map((s) => s.item.id)));
  }

  clearSelection(): void {
    this._selectedIds.set(new Set());
  }

  isSelected(id: number): boolean {
    return this._selectedIds().has(id);
  }

  async deleteSelectedAsync(): Promise<void> {
    const selected = [...this._selectedIds()];
    if (selected.length === 0) return;
    const toDelete = this._summaries().filter((s) => selected.includes(s.item.id));
    await Promise.all(toDelete.map((s) => this.api.deleteItem(s.item.id).toPromise()));
    this._summaries.update((items) => items.filter((s) => !selected.includes(s.item.id)));
    this._selectedIds.set(new Set());
    this.snackBar.open(`Removed ${toDelete.length} position(s)`, 'Close', { duration: 3000 });
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

  addManualPosition(request: AddManualPositionRequest): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.addManualPosition(request).subscribe({
        next: () => {
          this.snackBar.open(`${request.name} added to portfolio`, 'Close', { duration: 3000 });
          this.refresh();
          resolve();
        },
        error: (err) => {
          this.snackBar.open('Failed to add position', 'Close', { duration: 4000 });
          reject(err);
        },
      });
    });
  }

  deleteItem(id: number, symbol: string): void {
    this.api.deleteItem(id).subscribe({
      next: () => {
        this._summaries.update((items) => items.filter((s) => s.item.id !== id));
        this._selectedIds.update((s) => {
          const c = new Set(s);
          c.delete(id);
          return c;
        });
        this.snackBar.open(`${symbol} removed from portfolio`, 'Close', { duration: 3000 });
      },
      error: () => this.snackBar.open('Failed to remove stock', 'Close', { duration: 4000 }),
    });
  }
}
