import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { torontoTodayDate } from '../../../core/close-date.util';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import { TradeCashLinkService } from '../../../core/services/trade-cash-link.service';
import { ConfigStore } from '../../../core/store/config.store';
import { PortfolioStore } from '../../../core/store/portfolio.store';
import { AddStockDialogComponent, AddStockDialogData } from './add-stock-dialog.component';

describe('AddStockDialogComponent prefill (PM-019)', () => {
  let close: ReturnType<typeof vi.fn>;
  let addItem: ReturnType<typeof vi.fn>;
  let searchSymbols: ReturnType<typeof vi.fn>;
  let offerForStock: ReturnType<typeof vi.fn>;

  function create(data: AddStockDialogData | null, results: { symbol: string; description: string }[] = []) {
    close = vi.fn();
    addItem = vi.fn(() => Promise.resolve({ id: 1 }));
    searchSymbols = vi.fn(() => of(results));
    offerForStock = vi.fn();
    TestBed.configureTestingModule({
      imports: [AddStockDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close } },
        { provide: PortfolioApiService, useValue: { searchSymbols } },
        { provide: PortfolioStore, useValue: { addItem } },
        { provide: TradeCashLinkService, useValue: { offerForStock } },
        { provide: ConfigStore, useValue: { config: signal({ decisionSources: ['Manual', 'App Signal'] }) } },
      ],
    });
    return TestBed.createComponent(AddStockDialogComponent).componentInstance;
  }

  it('keeps the plain dialog unchanged when opened without context, with Toronto today as the Open Date', () => {
    const cmp = create(null);
    expect(cmp.form.value.symbol).toBe('');
    expect(cmp.form.value.transactionType).toBe('OPEN');
    expect(cmp.form.value.openDate?.toDateString()).toBe(torontoTodayDate().toDateString());
    expect(searchSymbols).not.toHaveBeenCalled();
  });

  it('prefills a Canadian symbol without duplicating the suffix and resolves the company through the existing search', () => {
    const cmp = create({ symbol: 'xiu.to.to' }, [{ symbol: 'XIU.TO', description: 'iShares S&P/TSX 60' }]);
    expect(cmp.form.value.symbol).toBe('XIU.TO');
    expect(searchSymbols).toHaveBeenCalledWith('XIU.TO');
    expect(cmp.form.value.companyName).toBe('iShares S&P/TSX 60');
  });

  it('prefills a US symbol and never invents a company when the search has no exact match', () => {
    const cmp = create({ symbol: 'AAPL' }, [{ symbol: 'AAPL.MX', description: 'Apple (Mexico)' }]);
    expect(cmp.form.value.symbol).toBe('AAPL');
    expect(cmp.form.value.companyName).toBe('');
  });

  it('leaves account, shares, cost and decision source for the user and defaults to OPEN', () => {
    const cmp = create({ symbol: 'AAPL' });
    expect(cmp.form.value.transactionType).toBe('OPEN');
    expect(cmp.form.value.shares).toBeNull();
    expect(cmp.form.value.averageCostBasis).toBeNull();
    expect(cmp.form.value.accountType).toBeNull();
    expect(cmp.form.value.decisionSource).toBeNull();
    expect(cmp.form.invalid).toBe(true);
  });

  it('cancel closes without saving anything', () => {
    const cmp = create({ symbol: 'AAPL' });
    cmp.cancel();
    expect(close).toHaveBeenCalledWith(false);
    expect(addItem).not.toHaveBeenCalled();
  });

  it('a failed save keeps the entered values and creates nothing else', async () => {
    const cmp = create({ symbol: 'AAPL', companyName: 'Apple Inc.' });
    addItem.mockRejectedValueOnce(new Error('boom'));
    cmp.form.patchValue({ shares: 10, averageCostBasis: 190.5 });

    await cmp.submit();

    expect(addItem).toHaveBeenCalledTimes(1);
    expect(close).not.toHaveBeenCalled();
    expect(cmp.form.value.shares).toBe(10);
    expect(cmp.form.value.averageCostBasis).toBe(190.5);
    expect(cmp.form.value.symbol).toBe('AAPL');
  });

  it('a double submit while saving sends one request through the existing store', async () => {
    const cmp = create({ symbol: 'AAPL', companyName: 'Apple Inc.' });
    cmp.form.patchValue({ shares: 10, averageCostBasis: 190.5 });

    await Promise.all([cmp.submit(), cmp.submit()]);

    expect(addItem).toHaveBeenCalledTimes(1);
    expect(addItem.mock.calls[0][0]).toMatchObject({ symbol: 'AAPL', shares: 10, averageCostBasis: 190.5, transactionType: 'OPEN' });
    expect(close).toHaveBeenCalledWith(true);
  });
});
