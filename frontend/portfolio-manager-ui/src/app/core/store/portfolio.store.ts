import { computed, effect, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import {
  AddManualPositionRequest,
  AddPortfolioItemRequest,
  PortfolioItem,
  PortfolioSummary,
  UpdatePortfolioItemRequest,
} from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { TradeCashLinkService } from '../services/trade-cash-link.service';
import { AccountTypesStore } from './account-types.store';
import { DemoModeStore } from './demo-mode.store';
import { onceAuthenticated } from './features/auth-gate';

interface PortfolioState {
  summaries: PortfolioSummary[];
  loading: boolean;
  error: string | null;
  selectedIds: Set<number>;
  /** True when the current data was loaded from the DB snapshot (not a live Yahoo call). */
  fromSnapshot: boolean;
  /** Bumped after every successful Role correction so dependent views (Swing Trading) reload. */
  roleChangeVersion: number;
}

/** The backend returns `{ error }` (400/409) for risk/episode rejections; show that reason verbatim. */
export const backendErrorMessage = (err: unknown, fallback: string): string => {
  const body = (err as { error?: { error?: unknown } } | null)?.error;
  return typeof body?.error === 'string' && body.error ? body.error : fallback;
};

export interface UpdateItemCallbacks {
  success?: () => void;
  /** Receives the backend's reason (or a generic fallback). */
  failure?: (message: string) => void;
}

/**
 * The user's stock positions, their selection state and the portfolio totals.
 *
 * Totals deliberately exclude CLOSE rows (they are history, not holdings) and value manual
 * positions from their stored market value rather than a quote. Templates must read the `display*`
 * computeds, never the raw totals, so demo mode can mask real money.
 */
export const PortfolioStore = signalStore(
  { providedIn: 'root' },
  withState<PortfolioState>({
    summaries: [],
    loading: false,
    error: null,
    selectedIds: new Set<number>(),
    fromSnapshot: false,
    roleChangeVersion: 0,
  }),
  withComputed(({ summaries, selectedIds }, demoMode = inject(DemoModeStore)) => {
    const totalValue = computed(() =>
      summaries()
        .filter((s) => s.item.transactionType !== 'CLOSE')
        .reduce((acc, s) => {
          const mv = s.item.isManual
            ? (s.item.manualMarketValue ?? s.item.averageCostBasis * s.item.shares)
            : (s.quote?.currentPrice ?? s.item.averageCostBasis) * s.item.shares;
          return acc + mv;
        }, 0),
    );

    const totalCost = computed(() =>
      summaries()
        .filter((s) => s.item.transactionType !== 'CLOSE')
        .reduce((acc, s) => acc + s.item.averageCostBasis * s.item.shares, 0),
    );

    const totalGainLoss = computed(() => totalValue() - totalCost());

    const totalGainLossPct = computed(() => {
      const cost = totalCost();
      return cost === 0 ? 0 : (totalGainLoss() / cost) * 100;
    });

    return {
      selectedCount: computed(() => selectedIds().size),
      hasSelection: computed(() => selectedIds().size > 0),
      totalValue,
      totalCost,
      totalGainLoss,
      totalGainLossPct,

      // ── Demo-aware display values ─────────────────────────────────────────
      // Use these in templates. When demo mode is OFF they are identical to the raw values.
      // When demo mode is ON they return masked values so real positions are never revealed.
      displayTotalValue: computed(() => demoMode.maskValue(totalValue())),
      displayTotalCost: computed(() => demoMode.maskValue(totalCost())),
      displayTotalGainLoss: computed(() => demoMode.maskValue(totalGainLoss())),
      displayTotalGainLossPct: computed(() => demoMode.maskPercent(totalGainLossPct())),
    };
  }),
  withMethods(
    (
      store,
      api = inject(PortfolioApiService),
      snackBar = inject(MatSnackBar),
      tradeCashLink = inject(TradeCashLinkService),
    ) => {
      const updateSummaries = (
        project: (items: PortfolioSummary[]) => PortfolioSummary[],
      ): void => {
        patchState(store, ({ summaries }) => ({ summaries: project(summaries) }));
      };

      const refresh = (): void => {
        patchState(store, { loading: true, error: null, fromSnapshot: false });
        api.getAllQuotes().subscribe({
          next: (data) => patchState(store, { summaries: data, loading: false }),
          error: (err) => {
            patchState(store, { loading: false, error: 'Failed to load portfolio data' });
            console.error(err);
          },
        });
      };

      return {
        refresh,

        /** Load last snapshot from DB (instant — no Yahoo Finance call).
         * Falls back to a live refresh when no snapshot exists yet. */
        loadSnapshot(): void {
          patchState(store, { loading: true });
          api.getPortfolioSnapshot().subscribe({
            next: (data) => {
              if (data) {
                patchState(store, { summaries: data, fromSnapshot: true, loading: false });
              } else {
                refresh();
              }
            },
            error: () => refresh(),
          });
        },

        /** Silently update prices from a completed batch refresh — no loading state, no extra HTTP call. */
        setFromRefresh(data: PortfolioSummary[]): void {
          patchState(store, { summaries: data, fromSnapshot: false });
        },

        /** Applies an account-type rename to every position that referenced the old name. */
        applyAccountTypeRename(oldName: string, newName: string): void {
          updateSummaries((items) =>
            items.map((x) =>
              x.item.accountType === oldName
                ? { ...x, item: { ...x.item, accountType: newName } }
                : x,
            ),
          );
        },

        toggleSelection(id: number): void {
          patchState(store, ({ selectedIds }) => {
            const copy = new Set(selectedIds);
            copy.has(id) ? copy.delete(id) : copy.add(id);
            return { selectedIds: copy };
          });
        },

        selectAll(): void {
          patchState(store, ({ summaries }) => ({
            selectedIds: new Set(summaries.map((s) => s.item.id)),
          }));
        },

        clearSelection(): void {
          patchState(store, { selectedIds: new Set<number>() });
        },

        isSelected(id: number): boolean {
          return store.selectedIds().has(id);
        },

        async deleteSelectedAsync(): Promise<void> {
          const selected = [...store.selectedIds()];
          if (selected.length === 0) return;
          const toDelete = store.summaries().filter((s) => selected.includes(s.item.id));
          await Promise.all(toDelete.map((s) => api.deleteItem(s.item.id).toPromise()));
          updateSummaries((items) => items.filter((s) => !selected.includes(s.item.id)));
          patchState(store, { selectedIds: new Set<number>() });
          snackBar.open(`Removed ${toDelete.length} position(s)`, 'Close', { duration: 3000 });
        },

        /** Resolves with the created row; callers decide whether to follow up (bulk import must not prompt per row). */
        addItem(request: AddPortfolioItemRequest): Promise<PortfolioItem> {
          return new Promise((resolve, reject) => {
            api.addItem(request).subscribe({
              next: (newItem) => {
                // Append immediately — quote populates on next refresh
                updateSummaries((items) => [...items, { item: newItem, quote: null }]);
                snackBar.open(`${newItem.symbol} added to portfolio`, 'Close', { duration: 3000 });
                resolve(newItem);
              },
              error: (err) => {
                snackBar.open('Failed to add stock', 'Close', { duration: 4000 });
                reject(err);
              },
            });
          });
        },

        addManualPosition(request: AddManualPositionRequest): Promise<void> {
          return new Promise((resolve, reject) => {
            api.addManualPosition(request).subscribe({
              next: (newItem) => {
                updateSummaries((items) => [...items, { item: newItem, quote: null }]);
                snackBar.open(`${request.name} added to portfolio`, 'Close', { duration: 3000 });
                resolve();
              },
              error: (err) => {
                snackBar.open('Failed to add position', 'Close', { duration: 4000 });
                reject(err);
              },
            });
          });
        },

        deleteItem(id: number, symbol: string): void {
          api.deleteItem(id).subscribe({
            next: () => {
              updateSummaries((items) => items.filter((s) => s.item.id !== id));
              patchState(store, ({ selectedIds }) => {
                const c = new Set(selectedIds);
                c.delete(id);
                return { selectedIds: c };
              });
              snackBar.open(`${symbol} removed from portfolio`, 'Close', { duration: 3000 });
              void tradeCashLink.offerRemoveForStock(id, symbol);
            },
            error: () => snackBar.open('Failed to remove stock', 'Close', { duration: 4000 }),
          });
        },

        updateItem(id: number, request: UpdatePortfolioItemRequest, callbacks?: UpdateItemCallbacks): void {
          const prev = store.summaries().find((s) => s.item.id === id)?.item ?? null;
          api.updateItem(id, request).subscribe({
            next: (response) => {
              const updated = response.updated;
              updateSummaries((items) => {
                const patched = items.map((s) =>
                  s.item.id === id
                    ? {
                        ...s,
                        item: {
                          ...s.item,
                          holdingRole: updated.holdingRole ?? s.item.holdingRole,
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
                          decisionSource: updated.decisionSource ?? null,
                          decisionSourceClosed: updated.decisionSourceClosed ?? null,
                        },
                      }
                    : s,
                );
                // Partial close: the remaining shares were auto-split server-side into a new OPEN row.
                return response.newOpenItem
                  ? [...patched, { item: response.newOpenItem, quote: null }]
                  : patched;
              });
              snackBar.open(
                response.newOpenItem
                  ? `${updated.symbol} partially closed \u2014 remaining ${response.newOpenItem.shares} shares moved to a new open position`
                  : `Position updated`,
                'Close',
                { duration: response.newOpenItem ? 5000 : 3000 },
              );
              if (response.newOpenItem) tradeCashLink.onPartialClose();
              if (prev) void tradeCashLink.offerForStock(prev, updated);
              if (prev && (prev.holdingRole ?? null) !== (updated.holdingRole ?? null)) {
                patchState(store, ({ roleChangeVersion }) => ({ roleChangeVersion: roleChangeVersion + 1 }));
              }
              callbacks?.success?.();
            },
            error: (err: unknown) => {
              const message = backendErrorMessage(err, 'Failed to update position');
              snackBar.open(message, 'Close', { duration: 6000 });
              callbacks?.failure?.(message);
            },
          });
        },

        updateHoldingRole(id: number, holdingRole: string): void {
          const previousRole = store.summaries().find((s) => s.item.id === id)?.item.holdingRole ?? null;
          // Optimistic update
          updateSummaries((items) =>
            items.map((s) => (s.item.id === id ? { ...s, item: { ...s.item, holdingRole } } : s)),
          );
          api.updatePortfolioHoldingRole(id, holdingRole).subscribe({
            next: () => patchState(store, ({ roleChangeVersion }) => ({ roleChangeVersion: roleChangeVersion + 1 })),
            error: (err: unknown) => {
              snackBar.open(backendErrorMessage(err, 'Failed to update holding role'), 'Close', { duration: 6000 });
              // Revert only this row; no full reload needed because the server rejected the change.
              updateSummaries((items) =>
                items.map((s) => (s.item.id === id ? { ...s, item: { ...s.item, holdingRole: previousRole } } : s)),
              );
            },
          });
        },

        patchItemNotes(id: number, notes: string | null): void {
          updateSummaries((items) =>
            items.map((s) => (s.item.id === id ? { ...s, item: { ...s.item, notes } } : s)),
          );
        },
      };
    },
  ),
  withHooks({
    onInit(store, accountTypes = inject(AccountTypesStore)) {
      effect(() => {
        const rename = accountTypes.renamed();
        if (rename) store.applyAccountTypeRename(rename.oldName, rename.newName);
      });
      // Wait for auth before loading snapshot — prevents 401 race on app start
      onceAuthenticated(() => store.loadSnapshot());
    },
  }),
);
