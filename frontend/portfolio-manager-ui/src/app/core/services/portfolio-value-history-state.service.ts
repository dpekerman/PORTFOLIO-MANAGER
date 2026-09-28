import { Injectable, computed, inject, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PortfolioValueHistoryDto, SnapshotStatus } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

export interface HistoryDateRange {
  from: string | null;
  to: string | null;
}

/** Snapshots-tab state for the Portfolio Value History page. Cash Ledger tab reuses CashStateService. */
@Injectable({ providedIn: 'root' })
export class PortfolioValueHistoryStateService {
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly _snapshots = signal<PortfolioValueHistoryDto[]>([]);
  private readonly _loading = signal(false);
  private readonly _actionError = signal<string | null>(null);
  private readonly _dateRange = signal<HistoryDateRange>({ from: null, to: null });
  private readonly _statusFilter = signal<SnapshotStatus | null>(null);

  readonly snapshots = this._snapshots.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly actionError = this._actionError.asReadonly();
  readonly dateRange = this._dateRange.asReadonly();
  readonly statusFilter = this._statusFilter.asReadonly();

  /** Client-side filtering over the fetched window (server defaults to the last 90 days). */
  readonly filteredSnapshots = computed(() => {
    const { from, to } = this._dateRange();
    const status = this._statusFilter();
    return this._snapshots().filter((s) => {
      if (from && s.recordedDate < from) return false;
      if (to && s.recordedDate > to) return false;
      if (status && s.snapshotStatus !== status) return false;
      return true;
    });
  });

  /** Rows come back most-recent-first from the API. */
  readonly latestSnapshot = computed<PortfolioValueHistoryDto | null>(
    () => this._snapshots()[0] ?? null,
  );

  constructor() {
    this.refresh();
  }

  setDateRange(range: HistoryDateRange): void {
    this._dateRange.set(range);
  }

  setStatusFilter(status: SnapshotStatus | null): void {
    this._statusFilter.set(status);
  }

  resetFilters(): void {
    this._dateRange.set({ from: null, to: null });
    this._statusFilter.set(null);
  }

  refresh(): void {
    this._loading.set(true);
    this.api.getPortfolioValueHistoryRange().subscribe({
      next: (data) => {
        this._snapshots.set(data);
        this._loading.set(false);
      },
      error: (err) => {
        this._loading.set(false);
        console.error('Failed to load portfolio value history', err);
      },
    });
  }

  recordTodayNow(): void {
    this._actionError.set(null);
    this.api.recordPortfolioValueNow().subscribe({
      next: () => {
        this.snackBar.open("Today's snapshot recorded", 'Dismiss', { duration: 3000 });
        this.refresh();
      },
      error: (err) => {
        const message = this.extractErrorMessage(err) ?? "Failed to record today's snapshot";
        this._actionError.set(message);
        this.snackBar.open(message, 'Dismiss', { duration: 5000 });
      },
    });
  }

  reconcileToday(): void {
    this._actionError.set(null);
    this.api.reconcileCashToday().subscribe({
      next: () => {
        this.snackBar.open("Today's cash reconciled", 'Dismiss', { duration: 3000 });
        this.refresh();
      },
      error: (err) => {
        const message = this.extractErrorMessage(err) ?? "Failed to reconcile today's cash";
        this._actionError.set(message);
        this.snackBar.open(message, 'Dismiss', { duration: 5000 });
      },
    });
  }

  recalculateCashFrom(fromDate: string): void {
    this._actionError.set(null);
    this.api.recalculateCashFromDate(fromDate).subscribe({
      next: () => {
        this.snackBar.open(`Cash recalculated from ${fromDate}`, 'Dismiss', { duration: 3000 });
        this.refresh();
      },
      error: (err) => {
        const message = this.extractErrorMessage(err) ?? 'Failed to recalculate cash';
        this._actionError.set(message);
        this.snackBar.open(message, 'Dismiss', { duration: 5000 });
      },
    });
  }

  private extractErrorMessage(err: unknown): string | null {
    const e = err as { error?: { message?: string } | string };
    if (typeof e?.error === 'string') return e.error;
    return e?.error?.message ?? null;
  }
}
