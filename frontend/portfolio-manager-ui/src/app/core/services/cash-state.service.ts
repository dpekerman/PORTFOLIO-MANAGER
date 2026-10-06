import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import {
  AddCashItemRequest,
  AddLinkedCashRequest,
  AdjustCashBalanceRequest,
  CashItem,
  UnlinkedTrade,
  UpdateCashItemRequest,
} from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';
import { AccountTypesStateService } from './account-types-state.service';

@Injectable({ providedIn: 'root' })
export class CashStateService {
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly accountTypes = inject(AccountTypesStateService);

  private readonly _items = signal<CashItem[]>([]);
  private readonly _loading = signal(false);
  private readonly _ledgerStartDate = signal<string | null>(null);
  private readonly _unlinkedTrades = signal<UnlinkedTrade[]>([]);
  private readonly _unlinkedLoading = signal(false);

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly ledgerStartDate = this._ledgerStartDate.asReadonly();
  readonly unlinkedTrades = this._unlinkedTrades.asReadonly();
  readonly unlinkedLoading = this._unlinkedLoading.asReadonly();

  readonly totalCash = computed(() => this._items().reduce((acc, item) => acc + item.amount, 0));

  /** Running total per AccountType, sorted for stable display. */
  readonly totalsByAccount = computed(() => {
    const totals = new Map<string, number>();
    for (const item of this._items()) {
      const key = item.accountType ?? '—';
      totals.set(key, (totals.get(key) ?? 0) + item.amount);
    }
    return Array.from(totals.entries())
      .map(([accountType, total]) => ({ accountType, total }))
      .sort((a, b) => a.accountType.localeCompare(b.accountType));
  });

  constructor() {
    effect(() => {
      const rename = this.accountTypes.renamed();
      if (rename) {
        this._items.update((items) =>
          items.map((x) =>
            x.accountType === rename.oldName ? { ...x, accountType: rename.newName } : x,
          ),
        );
        this._unlinkedTrades.update((items) =>
          items.map((x) =>
            x.accountType === rename.oldName ? { ...x, accountType: rename.newName } : x,
          ),
        );
      }
    });
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

  /** "Adjust Balance": types the desired new total; backend computes the delta and returns the new
   * ledger row. Refreshes the full list afterward since the account's running total changed. */
  adjustBalance(request: AdjustCashBalanceRequest): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.adjustCashBalance(request).subscribe({
        next: (item) => {
          this._items.update((list) => [...list, item]);
          this.snackBar.open('Cash balance adjusted', 'Dismiss', { duration: 3000 });
          resolve();
        },
        error: (err) => {
          const message = err?.error ?? 'Failed to adjust cash balance';
          this.snackBar.open(message, 'Dismiss', { duration: 5000 });
          reject(err);
        },
      });
    });
  }

  /** Creates the cash row for a trade leg; resolves with it, rejects (after a snackbar) on failure. */
  addLinked(request: AddLinkedCashRequest): Promise<CashItem> {
    return new Promise((resolve, reject) => {
      this.api.addLinkedCash(request).subscribe({
        next: (item) => {
          this._items.update((list) => [...list, item]);
          resolve(item);
        },
        error: (err) => {
          const message = typeof err?.error === 'string' ? err.error : 'Failed to link cash entry';
          this.snackBar.open(message, 'Dismiss', { duration: 5000 });
          reject(err);
        },
      });
    });
  }

  /** Deletes the given ledger rows, then reloads (each delete can trigger a server-side snapshot recalculation). */
  async removeItems(ids: number[]): Promise<void> {
    try {
      for (const id of ids) await firstValueFrom(this.api.deleteCashItem(id));
      this.snackBar.open('Linked cash entry removed', 'Dismiss', { duration: 3000 });
    } catch {
      this.snackBar.open('Failed to remove linked cash entry', 'Dismiss', { duration: 4000 });
    } finally {
      this.refresh();
    }
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

  /** Trade legs since the ledger start with no cash counterpart (each one still double-counts in Portfolio Value). */
  loadUnlinkedTrades(): void {
    this._unlinkedLoading.set(true);
    this.api.getUnlinkedTrades().subscribe({
      next: (data) => {
        this._unlinkedTrades.set(data);
        this._unlinkedLoading.set(false);
      },
      error: (err) => {
        this._unlinkedLoading.set(false);
        console.error('Failed to load unlinked trades', err);
      },
    });
  }

  loadLedgerStartDate(): void {
    this.api.getCashLedgerStartDate().subscribe({
      next: (res) => this._ledgerStartDate.set(res.ledgerStartDate),
      error: (err) => console.error('Failed to load ledger start date', err),
    });
  }
}
