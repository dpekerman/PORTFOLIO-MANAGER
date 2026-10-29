import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { PortfolioItem, PortfolioSummary } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { TradeCashLinkService } from '../services/trade-cash-link.service';
import { AccountTypesStore } from './account-types.store';
import { AuthStore } from './auth.store';
import { DemoModeStore } from './demo-mode.store';
import { PortfolioStore } from './portfolio.store';

interface PositionOptions {
  shares: number;
  averageCostBasis: number;
  price?: number | null;
  isManual?: boolean;
  manualMarketValue?: number | null;
  transactionType?: string | null;
  accountType?: string | null;
  holdingRole?: string | null;
}

let nextId = 1;

const position = (o: PositionOptions): PortfolioSummary =>
  ({
    item: {
      id: nextId++,
      symbol: 'AAPL',
      shares: o.shares,
      averageCostBasis: o.averageCostBasis,
      isManual: o.isManual ?? false,
      manualMarketValue: o.manualMarketValue ?? null,
      transactionType: o.transactionType ?? 'OPEN',
      accountType: o.accountType ?? 'TFSA',
      holdingRole: o.holdingRole ?? null,
    },
    quote: o.price === undefined ? { currentPrice: 100 } : o.price === null ? null : { currentPrice: o.price },
  }) as unknown as PortfolioSummary;

describe('PortfolioStore', () => {
  const authenticated = signal(false);
  const renamed = signal<{ oldName: string; newName: string } | null>(null);
  const demoEnabled = signal(false);
  const api = {
    getPortfolioSnapshot: vi.fn(),
    getAllQuotes: vi.fn(),
    deleteItem: vi.fn(),
    addItem: vi.fn(),
    addManualPosition: vi.fn(),
    updateItem: vi.fn(),
    updatePortfolioHoldingRole: vi.fn(),
  };
  const snackBar = { open: vi.fn() };
  const tradeCashLink = {
    offerRemoveForStock: vi.fn(),
    offerForStock: vi.fn(),
    onPartialClose: vi.fn(),
  };

  beforeEach(() => {
    nextId = 1;
    authenticated.set(false);
    renamed.set(null);
    demoEnabled.set(false);
    Object.values(api).forEach((fn) => fn.mockReset());
    snackBar.open.mockReset();
    Object.values(tradeCashLink).forEach((fn) => fn.mockReset());
    api.getPortfolioSnapshot.mockReturnValue(of(null));
    api.getAllQuotes.mockReturnValue(of([]));
    TestBed.configureTestingModule({
      providers: [
        { provide: PortfolioApiService, useValue: api },
        { provide: MatSnackBar, useValue: snackBar },
        { provide: TradeCashLinkService, useValue: tradeCashLink },
        { provide: AuthStore, useValue: { isAuthenticated: authenticated } },
        { provide: AccountTypesStore, useValue: { renamed } },
        {
          provide: DemoModeStore,
          useValue: {
            maskValue: (v: number) => (demoEnabled() ? v * 0.37 : v),
            maskPercent: (v: number) => (demoEnabled() ? v / 2 : v),
          },
        },
      ],
    });
  });

  function store(): InstanceType<typeof PortfolioStore> {
    const s = TestBed.inject(PortfolioStore);
    TestBed.tick();
    return s;
  }

  function withPositions(items: PortfolioSummary[]): InstanceType<typeof PortfolioStore> {
    const s = store();
    s.setFromRefresh(items);
    return s;
  }

  describe('totals', () => {
    it('values live positions from the quote and ignores CLOSE rows', () => {
      const s = withPositions([
        position({ shares: 10, averageCostBasis: 50, price: 80 }),
        position({ shares: 5, averageCostBasis: 20, price: 200, transactionType: 'CLOSE' }),
      ]);

      expect(s.totalValue()).toBe(800);
      expect(s.totalCost()).toBe(500);
      expect(s.totalGainLoss()).toBe(300);
      expect(s.totalGainLossPct()).toBeCloseTo(60);
    });

    it('falls back to the cost basis when a live position has no quote', () => {
      const s = withPositions([position({ shares: 10, averageCostBasis: 50, price: null })]);

      expect(s.totalValue()).toBe(500);
      expect(s.totalGainLoss()).toBe(0);
    });

    it('values manual positions from their stored market value', () => {
      const s = withPositions([
        position({
          shares: 2,
          averageCostBasis: 100,
          price: 999,
          isManual: true,
          manualMarketValue: 350,
        }),
      ]);

      expect(s.totalValue()).toBe(350);
      expect(s.totalCost()).toBe(200);
    });

    it('falls back to cost basis for a manual position with no market value', () => {
      const s = withPositions([
        position({ shares: 3, averageCostBasis: 10, isManual: true, manualMarketValue: null }),
      ]);

      expect(s.totalValue()).toBe(30);
    });

    it('reports 0% rather than dividing by a zero cost', () => {
      const s = withPositions([position({ shares: 10, averageCostBasis: 0, price: 7 })]);

      expect(s.totalCost()).toBe(0);
      expect(s.totalGainLoss()).toBe(70);
      expect(s.totalGainLossPct()).toBe(0);
    });
  });

  describe('demo masking', () => {
    const positions = [position({ shares: 10, averageCostBasis: 50, price: 80 })];

    it('display values equal the raw values when demo mode is off', () => {
      const s = withPositions(positions);

      expect(s.displayTotalValue()).toBe(s.totalValue());
      expect(s.displayTotalCost()).toBe(s.totalCost());
      expect(s.displayTotalGainLoss()).toBe(s.totalGainLoss());
      expect(s.displayTotalGainLossPct()).toBe(s.totalGainLossPct());
    });

    it('display values are masked when demo mode is on', () => {
      const s = withPositions(positions);
      demoEnabled.set(true);

      expect(s.displayTotalValue()).toBeCloseTo(800 * 0.37);
      expect(s.displayTotalCost()).toBeCloseTo(500 * 0.37);
      expect(s.displayTotalGainLoss()).toBeCloseTo(300 * 0.37);
      expect(s.displayTotalGainLossPct()).toBeCloseTo(30);
      // Raw totals are untouched.
      expect(s.totalValue()).toBe(800);
    });
  });

  describe('loading', () => {
    it('waits for auth before loading the snapshot', () => {
      const s = store();
      expect(api.getPortfolioSnapshot).not.toHaveBeenCalled();

      api.getPortfolioSnapshot.mockReturnValue(
        of([position({ shares: 1, averageCostBasis: 1, price: 1 })]),
      );
      authenticated.set(true);
      TestBed.tick();

      expect(api.getPortfolioSnapshot).toHaveBeenCalledOnce();
      expect(s.fromSnapshot()).toBe(true);
      expect(s.loading()).toBe(false);
    });

    it('falls back to a live refresh when there is no snapshot', () => {
      const s = store();
      authenticated.set(true);
      TestBed.tick();

      expect(api.getAllQuotes).toHaveBeenCalledOnce();
      expect(s.fromSnapshot()).toBe(false);
    });

    it('falls back to a live refresh when the snapshot call fails', () => {
      api.getPortfolioSnapshot.mockReturnValue(throwError(() => new Error('boom')));
      store();
      authenticated.set(true);
      TestBed.tick();

      expect(api.getAllQuotes).toHaveBeenCalledOnce();
    });

    it('surfaces an error when the live refresh fails', () => {
      api.getAllQuotes.mockReturnValue(throwError(() => new Error('boom')));
      vi.spyOn(console, 'error').mockImplementation(() => {});
      const s = store();

      s.refresh();

      expect(s.error()).toBe('Failed to load portfolio data');
      expect(s.loading()).toBe(false);
    });
  });

  describe('selection', () => {
    it('toggles, selects all and clears', () => {
      const s = withPositions([
        position({ shares: 1, averageCostBasis: 1 }),
        position({ shares: 1, averageCostBasis: 1 }),
      ]);

      s.toggleSelection(1);
      expect(s.isSelected(1)).toBe(true);
      expect(s.selectedCount()).toBe(1);
      expect(s.hasSelection()).toBe(true);

      s.toggleSelection(1);
      expect(s.isSelected(1)).toBe(false);

      s.selectAll();
      expect(s.selectedCount()).toBe(2);

      s.clearSelection();
      expect(s.hasSelection()).toBe(false);
    });

    it('deleteSelectedAsync removes the rows, clears selection and notifies', async () => {
      api.deleteItem.mockReturnValue(of(void 0));
      const s = withPositions([
        position({ shares: 1, averageCostBasis: 1 }),
        position({ shares: 1, averageCostBasis: 1 }),
      ]);
      s.toggleSelection(1);

      await s.deleteSelectedAsync();

      expect(api.deleteItem).toHaveBeenCalledWith(1);
      expect(s.summaries().map((x) => x.item.id)).toEqual([2]);
      expect(s.selectedCount()).toBe(0);
      expect(snackBar.open).toHaveBeenCalledWith('Removed 1 position(s)', 'Close', {
        duration: 3000,
      });
    });

    it('deleteSelectedAsync is a no-op with an empty selection', async () => {
      const s = store();
      await s.deleteSelectedAsync();
      expect(api.deleteItem).not.toHaveBeenCalled();
      expect(snackBar.open).not.toHaveBeenCalled();
    });
  });

  describe('mutations', () => {
    it('addItem appends the row and resolves with it', async () => {
      const newItem = { id: 42, symbol: 'MSFT' } as PortfolioItem;
      api.addItem.mockReturnValue(of(newItem));
      const s = store();

      await expect(s.addItem({} as never)).resolves.toBe(newItem);
      expect(s.summaries()).toEqual([{ item: newItem, quote: null }]);
      expect(snackBar.open).toHaveBeenCalledWith('MSFT added to portfolio', 'Close', {
        duration: 3000,
      });
    });

    it('addItem rejects and warns when the server fails', async () => {
      api.addItem.mockReturnValue(throwError(() => new Error('boom')));
      const s = store();

      await expect(s.addItem({} as never)).rejects.toThrow();
      expect(snackBar.open).toHaveBeenCalledWith('Failed to add stock', 'Close', {
        duration: 4000,
      });
    });

    it('deleteItem removes the row, deselects it and offers the cash follow-up', () => {
      api.deleteItem.mockReturnValue(of(void 0));
      const s = withPositions([position({ shares: 1, averageCostBasis: 1 })]);
      s.toggleSelection(1);

      s.deleteItem(1, 'AAPL');

      expect(s.summaries()).toEqual([]);
      expect(s.selectedCount()).toBe(0);
      expect(tradeCashLink.offerRemoveForStock).toHaveBeenCalledWith(1, 'AAPL');
    });

    it('updateItem appends the auto-split open row on a partial close', () => {
      api.updateItem.mockReturnValue(
        of({
          updated: { symbol: 'AAPL', shares: 4, averageCostBasis: 50 },
          newOpenItem: { id: 99, shares: 6 },
        }),
      );
      const s = withPositions([position({ shares: 10, averageCostBasis: 50 })]);

      s.updateItem(1, {} as never);

      expect(s.summaries().map((x) => x.item.id)).toEqual([1, 99]);
      expect(tradeCashLink.onPartialClose).toHaveBeenCalledOnce();
      expect(tradeCashLink.offerForStock).toHaveBeenCalledOnce();
      expect(snackBar.open).toHaveBeenCalledWith(
        'AAPL partially closed \u2014 remaining 6 shares moved to a new open position',
        'Close',
        { duration: 5000 },
      );
    });

    it('updateHoldingRole updates optimistically and reverts only that row on failure', () => {
      api.updatePortfolioHoldingRole.mockReturnValue(throwError(() => new Error('boom')));
      const s = withPositions([position({ shares: 1, averageCostBasis: 1, holdingRole: 'Swing' })]);

      s.updateHoldingRole(1, 'Core');

      expect(snackBar.open).toHaveBeenCalledWith('Failed to update holding role', 'Close', {
        duration: 6000,
      });
      expect(s.summaries()[0].item.holdingRole).toBe('Swing');
      expect(api.getAllQuotes).not.toHaveBeenCalled();
      expect(s.roleChangeVersion()).toBe(0);
    });

    it('updateHoldingRole success bumps roleChangeVersion and shows the backend reason on rejection', () => {
      api.updatePortfolioHoldingRole.mockReturnValue(of(void 0));
      const s = withPositions([position({ shares: 1, averageCostBasis: 1, holdingRole: 'Swing' })]);
      s.updateHoldingRole(1, 'Speculative');
      expect(s.summaries()[0].item.holdingRole).toBe('Speculative');
      expect(s.roleChangeVersion()).toBe(1);

      api.updatePortfolioHoldingRole.mockReturnValue(
        throwError(() => ({ status: 409, error: { error: 'Other open lots have different roles.' } })),
      );
      s.updateHoldingRole(1, 'Core');
      expect(snackBar.open).toHaveBeenCalledWith('Other open lots have different roles.', 'Close', {
        duration: 6000,
      });
      expect(s.summaries()[0].item.holdingRole).toBe('Speculative');
    });

    it('updateItem patches holdingRole on success and reports success to the caller', () => {
      api.updateItem.mockReturnValue(
        of({ updated: { symbol: 'HUN.TO', shares: 10, averageCostBasis: 5, holdingRole: 'Speculative' }, newOpenItem: null }),
      );
      const s = withPositions([position({ shares: 10, averageCostBasis: 5, holdingRole: 'Swing' })]);
      const success = vi.fn();
      const failure = vi.fn();

      s.updateItem(1, {} as never, { success, failure });

      expect(s.summaries()[0].item.holdingRole).toBe('Speculative');
      expect(s.roleChangeVersion()).toBe(1);
      expect(success).toHaveBeenCalledOnce();
      expect(failure).not.toHaveBeenCalled();
    });

    it('updateItem surfaces the exact backend message and leaves the row untouched on failure', () => {
      api.updateItem.mockReturnValue(
        throwError(() => ({ status: 409, error: { error: 'This lot has no trade episode.' } })),
      );
      const s = withPositions([position({ shares: 10, averageCostBasis: 5, holdingRole: 'Speculative' })]);
      const failure = vi.fn();

      s.updateItem(1, {} as never, { failure });

      expect(failure).toHaveBeenCalledWith('This lot has no trade episode.');
      expect(snackBar.open).toHaveBeenCalledWith('This lot has no trade episode.', 'Close', { duration: 6000 });
      expect(s.summaries()[0].item.holdingRole).toBe('Speculative');
      expect(s.roleChangeVersion()).toBe(0);
    });

    it('patchItemNotes updates one row only', () => {
      const s = withPositions([
        position({ shares: 1, averageCostBasis: 1 }),
        position({ shares: 1, averageCostBasis: 1 }),
      ]);

      s.patchItemNotes(2, 'watch earnings');

      expect(s.summaries().map((x) => x.item.notes)).toEqual([undefined, 'watch earnings']);
    });
  });

  it('renames the account type on every affected position', () => {
    const s = withPositions([
      position({ shares: 1, averageCostBasis: 1, accountType: 'TFSA' }),
      position({ shares: 1, averageCostBasis: 1, accountType: 'RRSP' }),
    ]);

    renamed.set({ oldName: 'TFSA', newName: 'TFSA-1' });
    TestBed.tick();

    expect(s.summaries().map((x) => x.item.accountType)).toEqual(['TFSA-1', 'RRSP']);
  });
});
