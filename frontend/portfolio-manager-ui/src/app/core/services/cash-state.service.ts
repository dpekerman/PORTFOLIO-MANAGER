import { Injectable, computed, inject, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AddCashItemRequest, CashItem, UpdateCashItemRequest } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class CashStateService {
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly _items = signal<CashItem[]>([]);
  private readonly _loading = signal(false);

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();

  readonly totalCash = computed(() => this._items().reduce((acc, item) => acc + item.amount, 0));

  constructor() {
    this.refresh();
  }

  refresh(): void {
    this._loading.set(true);
    this.api.getCashItems().subscribe({
      next: (data) => {
        this._items.set(data);
        this._loading.set(false);
      },
      error: (err) => {
        this._loading.set(false);
        console.error('Failed to load cash items', err);
      },
    });
  }

  addItem(request: AddCashItemRequest): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.addCashItem(request).subscribe({
        next: (item) => {
          this._items.update((list) => [...list, item]);
          this.snackBar.open(`Cash position added`, 'Dismiss', { duration: 3000 });
          resolve();
        },
        error: (err) => {
          this.snackBar.open('Failed to add cash position', 'Dismiss', { duration: 4000 });
          reject(err);
        },
      });
    });
  }

  updateItem(id: number, request: UpdateCashItemRequest): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.updateCashItem(id, request).subscribe({
        next: (updated) => {
          this._items.update((list) => list.map((x) => (x.id === id ? updated : x)));
          this.snackBar.open('Cash position updated', 'Dismiss', { duration: 3000 });
          resolve();
        },
        error: (err) => {
          this.snackBar.open('Failed to update cash position', 'Dismiss', { duration: 4000 });
          reject(err);
        },
      });
    });
  }

  deleteItem(id: number): void {
    this.api.deleteCashItem(id).subscribe({
      next: () => {
        this._items.update((list) => list.filter((x) => x.id !== id));
        this.snackBar.open('Cash position removed', 'Dismiss', { duration: 3000 });
      },
      error: () => {
        this.snackBar.open('Failed to remove cash position', 'Dismiss', { duration: 4000 });
      },
    });
  }
}
