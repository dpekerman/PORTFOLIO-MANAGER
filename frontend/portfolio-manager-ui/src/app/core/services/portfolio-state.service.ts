import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { interval, switchMap } from 'rxjs';
import {
  AddManualPositionRequest,
  AddPortfolioItemRequest,
  PortfolioSummary,
  UpdatePortfolioItemRequest,
} from '../models/portfolio.models';
import { DemoModeService } from './demo-mode.service';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class PortfolioStateService {
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly demoMode = inject(DemoModeService);

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
    this._summaries()
      .filter((s) => s.item.transactionType !== 'CLOSE')
      .reduce((acc, s) => {
        const mv = s.item.isManual
          ? (s.item.manualMarketValue ?? s.item.averageCostBasis * s.item.shares)
          : (s.quote?.currentPrice ?? s.item.averageCostBasis) * s.item.shares;
        return acc + mv;
      }, 0),
  );

  readonly totalCost = computed(() =>
    this._summaries()
      .filter((s) => s.item.transactionType !== 'CLOSE')
      .reduce((acc, s) => acc + s.item.averageCostBasis * s.item.shares, 0),
  );

  readonly totalGainLoss = computed(() => this.totalValue() - this.totalCost());

  readonly totalGainLossPct = computed(() => {
    const cost = this.totalCost();
    return cost === 0 ? 0 : (this.totalGainLoss() / cost) * 100;
  });

  // ── Demo-aware display values ───────────────────────────────────────────────
  // Use these in templates. When demo mode is OFF they are identical to the raw values.
  // When demo mode is ON they return masked values so real positions are never revealed.
  readonly displayTotalValue = computed(() => this.demoMode.maskValue(this.totalValue()));
  readonly displayTotalCost = computed(() => this.demoMode.maskValue(this.totalCost()));
  readonly displayTotalGainLoss = computed(() => this.demoMode.maskValue(this.totalGainLoss()));
  readonly displayTotalGainLossPct = computed(() =>
    this.demoMode.maskPercent(this.totalGainLossPct()),
  );

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

  updateItem(id: number, request: UpdatePortfolioItemRequest): void {
    this.api.updateItem(id, request).subscribe({
      next: (updated) => {
        this._summaries.update((items) =>
          items.map((s) =>
            s.item.id === id
              ? {
                  ...s,
                  item: {
                    ...s.item,
                    companyName: updated.companyName,
                    shares: updated.shares,
                    averageCostBasis: updated.averageCostBasis,
                    sector: updated.sector ?? s.item.sector,
                    industry: updated.industry ?? s.item.industry,
                    sectorIsOverridden: updated.sectorIsOverridden,
                    transactionType: updated.transactionType ?? null,
                    accountType: updated.accountType ?? null,
                    openDate: updated.openDate ?? null,
                    closeDate: updated.closeDate ?? null,
                    closingPrice: updated.closingPrice ?? null,
                  },
                }
              : s,
          ),
        );
        this.snackBar.open(`Position updated`, 'Close', { duration: 3000 });
      },
      error: () => this.snackBar.open('Failed to update position', 'Close', { duration: 4000 }),
    });
  }

  updateHoldingRole(id: number, holdingRole: string): void {
    // Optimistic update
    this._summaries.update((items) =>
      items.map((s) => (s.item.id === id ? { ...s, item: { ...s.item, holdingRole } } : s)),
    );
    this.api.updatePortfolioHoldingRole(id, holdingRole).subscribe({
      error: () => {
        this.snackBar.open('Failed to update holding role', 'Close', { duration: 4000 });
        this.refresh(); // revert by reloading
      },
    });
  }

  patchItemNotes(id: number, notes: string | null): void {
    this._summaries.update((items) =>
      items.map((s) => (s.item.id === id ? { ...s, item: { ...s.item, notes } } : s)),
    );
  }
}
