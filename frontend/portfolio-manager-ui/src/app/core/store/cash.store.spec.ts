import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CashItem } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { AccountTypesStore } from './account-types.store';
import { AuthStore } from './auth.store';
import { CashStore } from './cash.store';

const row = (id: number, amount: number, accountType: string | null = 'TFSA') =>
  ({ id, amount, accountType }) as unknown as CashItem;

describe('CashStore', () => {
  const authenticated = signal(false);
  const renamed = signal<{ oldName: string; newName: string } | null>(null);
  const api = {
    getCashItems: vi.fn(),
    addCashItem: vi.fn(),
    updateCashItem: vi.fn(),
    adjustCashBalance: vi.fn(),
    addLinkedCash: vi.fn(),
    deleteCashItem: vi.fn(),
    getUnlinkedTrades: vi.fn(),
    getCashLedgerStartDate: vi.fn(),
  };
  const snackBar = { open: vi.fn() };

  beforeEach(() => {
    authenticated.set(false);
    renamed.set(null);
    Object.values(api).forEach((fn) => fn.mockReset());
    snackBar.open.mockReset();
    api.getCashItems.mockReturnValue(of([row(1, 100, 'TFSA'), row(2, 50, 'RRSP'), row(3, 25, 'TFSA')]));
    TestBed.configureTestingModule({
      providers: [
        { provide: PortfolioApiService, useValue: api },
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AccountTypesStore, useValue: { renamed } },
        { provide: AuthStore, useValue: { isAuthenticated: authenticated } },
      ],
    });
  });

  function store(): InstanceType<typeof CashStore> {
    const s = TestBed.inject(CashStore);
    TestBed.tick();
    authenticated.set(true);
    TestBed.tick();
    return s;
  }

  it('does not load until authenticated', () => {
    TestBed.inject(CashStore);
    TestBed.tick();
    expect(api.getCashItems).not.toHaveBeenCalled();
  });

  it('computes the total and per-account totals sorted by account name', () => {
    const s = store();
    expect(s.totalCash()).toBe(175);
    expect(s.totalsByAccount()).toEqual([
      { accountType: 'RRSP', total: 50 },
      { accountType: 'TFSA', total: 125 },
    ]);
  });

  it('groups rows without an account type under an em dash', () => {
    api.getCashItems.mockReturnValue(of([row(1, 10, null)]));
    expect(store().totalsByAccount()).toEqual([{ accountType: '—', total: 10 }]);
  });

  it('appends a confirmed add and notifies; keeps state and rejects on failure', async () => {
    api.addCashItem.mockReturnValueOnce(of(row(4, 5)));
    const s = store();
    await s.addItem({} as never);
    expect(s.items().map((x) => x.id)).toEqual([1, 2, 3, 4]);
    expect(snackBar.open).toHaveBeenCalledWith('Cash position added', 'Dismiss', { duration: 3000 });

    const boom = new Error('x');
    api.addCashItem.mockReturnValueOnce(throwError(() => boom));
    await expect(s.addItem({} as never)).rejects.toBe(boom);
    expect(s.items()).toHaveLength(4);
    expect(snackBar.open).toHaveBeenLastCalledWith('Failed to add cash position', 'Dismiss', {
      duration: 4000,
    });
  });

  it('replaces an updated row in place', async () => {
    api.updateCashItem.mockReturnValue(of(row(2, 999, 'RRSP')));
    const s = store();
    await s.updateItem(2, {} as never);
    expect(s.items().map((x) => x.amount)).toEqual([100, 999, 25]);
  });

  it('uses the server message when adjusting the balance fails', async () => {
    api.adjustCashBalance.mockReturnValue(throwError(() => ({ error: 'Too big' })));
    const s = store();
    await expect(s.adjustBalance({} as never)).rejects.toBeDefined();
    expect(snackBar.open).toHaveBeenCalledWith('Too big', 'Dismiss', { duration: 5000 });
  });

  it('addLinked resolves with the new row, or falls back to a default message', async () => {
    api.addLinkedCash.mockReturnValueOnce(of(row(9, 1)));
    const s = store();
    expect((await s.addLinked({} as never)).id).toBe(9);
    expect(s.items()).toHaveLength(4);

    api.addLinkedCash.mockReturnValueOnce(throwError(() => ({ error: { not: 'a string' } })));
    await expect(s.addLinked({} as never)).rejects.toBeDefined();
    expect(snackBar.open).toHaveBeenLastCalledWith('Failed to link cash entry', 'Dismiss', {
      duration: 5000,
    });
  });

  it('removeItems deletes each row then always reloads', async () => {
    api.deleteCashItem.mockReturnValue(of(undefined));
    const s = store();
    api.getCashItems.mockClear();

    await s.removeItems([1, 2]);

    expect(api.deleteCashItem).toHaveBeenCalledTimes(2);
    expect(api.getCashItems).toHaveBeenCalledTimes(1);

    api.deleteCashItem.mockReturnValue(throwError(() => new Error('x')));
    await s.removeItems([3]);
    expect(snackBar.open).toHaveBeenLastCalledWith('Failed to remove linked cash entry', 'Dismiss', {
      duration: 4000,
    });
    expect(api.getCashItems).toHaveBeenCalledTimes(2);
  });

  it('deleteItem removes the row only after the server confirms', () => {
    const s = store();
    api.deleteCashItem.mockReturnValueOnce(throwError(() => new Error('x')));
    s.deleteItem(1);
    expect(s.items()).toHaveLength(3);

    api.deleteCashItem.mockReturnValueOnce(of(undefined));
    s.deleteItem(1);
    expect(s.items().map((x) => x.id)).toEqual([2, 3]);
  });

  it('follows an account-type rename in items and unlinked trades', () => {
    api.getUnlinkedTrades.mockReturnValue(of([{ id: 1, accountType: 'TFSA' }]));
    const s = store();
    s.loadUnlinkedTrades();

    renamed.set({ oldName: 'TFSA', newName: 'Tax Free' });
    TestBed.tick();

    expect(s.items().map((x) => x.accountType)).toEqual(['Tax Free', 'RRSP', 'Tax Free']);
    expect(s.unlinkedTrades()[0].accountType).toBe('Tax Free');
    expect(s.unlinkedLoading()).toBe(false);
  });

  it('loads the ledger start date', () => {
    api.getCashLedgerStartDate.mockReturnValue(of({ ledgerStartDate: '2026-01-01' }));
    const s = store();
    s.loadLedgerStartDate();
    expect(s.ledgerStartDate()).toBe('2026-01-01');
  });
});
