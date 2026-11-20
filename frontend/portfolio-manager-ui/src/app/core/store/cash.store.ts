import { computed, effect, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import {
  AddCashItemRequest,
  AddLinkedCashRequest,
  AdjustCashBalanceRequest,
  CashItem,
  UnlinkedTrade,
  UpdateCashItemRequest,
} from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { AccountTypesStore } from './account-types.store';
import { onceAuthenticated } from './features/auth-gate';
import { setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

interface CashState {
  items: CashItem[];
  ledgerStartDate: string | null;
  unlinkedTrades: UnlinkedTrade[];
  unlinkedLoading: boolean;
}

/**
 * The cash ledger. Mutations update local state only after the server confirms, and callers get
 * Promises so dialogs can stay open on failure. Follows account-type renames so rows never show a
 * stale name between the rename and the next reload.
 */
export const CashStore = signalStore(
  { providedIn: 'root' },
  withState<CashState>({
    items: [],
    ledgerStartDate: null,
    unlinkedTrades: [],
    unlinkedLoading: false,
  }),
  withRequestStatus(),
  withComputed(({ items }) => ({
    totalCash: computed(() => items().reduce((acc, item) => acc + item.amount, 0)),

    /** Running total per AccountType, sorted for stable display. */
    totalsByAccount: computed(() => {
      const totals = new Map<string, number>();
      for (const item of items()) {
        const key = item.accountType ?? '—';
        totals.set(key, (totals.get(key) ?? 0) + item.amount);
      }
      return Array.from(totals.entries())
        .map(([accountType, total]) => ({ accountType, total }))
        .sort((a, b) => a.accountType.localeCompare(b.accountType));
    }),
  })),
  withMethods((store, api = inject(PortfolioApiService), snackBar = inject(MatSnackBar)) => {
    const refresh = (): void => {
      patchState(store, setLoading());
      api.getCashItems().subscribe({
        next: (items) => patchState(store, { items }, setLoaded()),
        error: (err) => {
          patchState(store, setLoaded());
          console.error('Failed to load cash items', err);
        },
      });
    };

    const append = (item: CashItem): void =>
      patchState(store, ({ items }) => ({ items: [...items, item] }));

    return {
      refresh,

      /** Applies an account-type rename to the already-loaded rows. */
      _applyAccountRename(oldName: string, newName: string): void {
        const rename = <T extends { accountType?: string | null }>(rows: T[]): T[] =>
          rows.map((x) => (x.accountType === oldName ? { ...x, accountType: newName } : x));
        patchState(store, ({ items, unlinkedTrades }) => ({
          items: rename(items),
          unlinkedTrades: rename(unlinkedTrades),
        }));
      },

      async addItem(request: AddCashItemRequest): Promise<void> {
        try {
          append(await firstValueFrom(api.addCashItem(request)));
          snackBar.open(`Cash position added`, 'Dismiss', { duration: 3000 });
        } catch (err) {
          snackBar.open('Failed to add cash position', 'Dismiss', { duration: 4000 });
          throw err;
        }
      },

      async updateItem(id: number, request: UpdateCashItemRequest): Promise<void> {
        try {
          const updated = await firstValueFrom(api.updateCashItem(id, request));
          patchState(store, ({ items }) => ({ items: items.map((x) => (x.id === id ? updated : x)) }));
          snackBar.open('Cash position updated', 'Dismiss', { duration: 3000 });
        } catch (err) {
          snackBar.open('Failed to update cash position', 'Dismiss', { duration: 4000 });
          throw err;
        }
      },

      /**
       * "Adjust Balance": types the desired new total; backend computes the delta and returns the
       * new ledger row.
       */
      async adjustBalance(request: AdjustCashBalanceRequest): Promise<void> {
        try {
          append(await firstValueFrom(api.adjustCashBalance(request)));
          snackBar.open('Cash balance adjusted', 'Dismiss', { duration: 3000 });
        } catch (err) {
          const message = (err as { error?: string } | null)?.error ?? 'Failed to adjust cash balance';
          snackBar.open(message, 'Dismiss', { duration: 5000 });
          throw err;
        }
      },

      /** Creates the cash row for a trade leg; resolves with it, rejects (after a snackbar) on failure. */
      async addLinked(request: AddLinkedCashRequest): Promise<CashItem> {
        try {
          const item = await firstValueFrom(api.addLinkedCash(request));
          append(item);
          return item;
        } catch (err) {
          const error = (err as { error?: unknown } | null)?.error;
          const message = typeof error === 'string' ? error : 'Failed to link cash entry';
          snackBar.open(message, 'Dismiss', { duration: 5000 });
          throw err;
        }
      },

      /** Deletes the given ledger rows, then reloads (each delete can trigger a server-side snapshot recalculation). */
      async removeItems(ids: number[]): Promise<void> {
        try {
          for (const id of ids) await firstValueFrom(api.deleteCashItem(id));
          snackBar.open('Linked cash entry removed', 'Dismiss', { duration: 3000 });
        } catch {
          snackBar.open('Failed to remove linked cash entry', 'Dismiss', { duration: 4000 });
        } finally {
          refresh();
        }
      },

      deleteItem(id: number): void {
        api.deleteCashItem(id).subscribe({
          next: () => {
            patchState(store, ({ items }) => ({ items: items.filter((x) => x.id !== id) }));
            snackBar.open('Cash position removed', 'Dismiss', { duration: 3000 });
          },
          error: () => {
            snackBar.open('Failed to remove cash position', 'Dismiss', { duration: 4000 });
          },
        });
      },

      /** Trade legs since the ledger start with no cash counterpart (each one still double-counts in Portfolio Value). */
      loadUnlinkedTrades(): void {
        patchState(store, { unlinkedLoading: true });
        api.getUnlinkedTrades().subscribe({
          next: (unlinkedTrades) => patchState(store, { unlinkedTrades, unlinkedLoading: false }),
          error: (err) => {
            patchState(store, { unlinkedLoading: false });
            console.error('Failed to load unlinked trades', err);
          },
        });
      },

      loadLedgerStartDate(): void {
        api.getCashLedgerStartDate().subscribe({
          next: (res) => patchState(store, { ledgerStartDate: res.ledgerStartDate }),
          error: (err) => console.error('Failed to load ledger start date', err),
        });
      },
    };
  }),
  withHooks({
    onInit(store) {
      const accountTypes = inject(AccountTypesStore);
      effect(() => {
        const rename = accountTypes.renamed();
        if (rename) store._applyAccountRename(rename.oldName, rename.newName);
      });

      // Wait for auth before loading: loading earlier races the token refresh and 401s.
      onceAuthenticated(() => store.refresh());
    },
  }),
);
