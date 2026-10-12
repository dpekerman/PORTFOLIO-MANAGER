import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { OptionItem, OptionTechnicalData } from '../models/portfolio.models';
import { PortfolioApiService } from '../services/portfolio-api.service';
import { TradeCashLinkService } from '../services/trade-cash-link.service';
import { AccountTypesStore } from './account-types.store';
import { OptionStore } from './option.store';

const DAY = 24 * 60 * 60 * 1000;
const inDays = (n: number) => new Date(Date.now() + n * DAY).toISOString();

const option = (over: Partial<OptionItem> = {}): OptionItem =>
  ({
    id: 1,
    underlyingTicker: 'AAPL',
    positionType: 'CALL',
    strike: 100,
    premium: 2,
    marketPrice: 3,
    numberOfContracts: 2,
    expirationDate: inDays(60),
    transactionType: 'OPEN',
    accountType: 'TFSA',
    ...over,
  }) as OptionItem;

const technical = (over: Partial<OptionTechnicalData> = {}): OptionTechnicalData =>
  ({
    currentPrice: 100,
    sma20: 100,
    sma50: 100,
    ema21: 100,
    atr14: 1,
    rsi14: 50,
    rsiSignal9: 50,
    rsiSignalAvailable: false,
    yesterdayHigh: 101,
    yesterdayLow: 99,
    previousClose: 100,
    bollingerLower: 90,
    bollingerUpper: 110,
    ...over,
  }) as OptionTechnicalData;

describe('OptionStore', () => {
  const renamed = signal<{ oldName: string; newName: string } | null>(null);
  const api = {
    getOptionItems: vi.fn(),
    getOptionTechnicalData: vi.fn(),
    addOptionItem: vi.fn(),
    updateOptionItem: vi.fn(),
    deleteOptionItem: vi.fn(),
  };
  const snackBar = { open: vi.fn() };
  const tradeCashLink = { offerForOption: vi.fn(), offerRemoveForOption: vi.fn() };

  beforeEach(() => {
    renamed.set(null);
    Object.values(api).forEach((fn) => fn.mockReset());
    snackBar.open.mockReset();
    Object.values(tradeCashLink).forEach((fn) => fn.mockReset());
    api.getOptionItems.mockReturnValue(of([]));
    api.getOptionTechnicalData.mockReturnValue(of(technical()));
    TestBed.configureTestingModule({
      providers: [
        { provide: PortfolioApiService, useValue: api },
        { provide: MatSnackBar, useValue: snackBar },
        { provide: TradeCashLinkService, useValue: tradeCashLink },
        { provide: AccountTypesStore, useValue: { renamed } },
      ],
    });
  });

  function storeWith(items: OptionItem[], td?: OptionTechnicalData) {
    api.getOptionItems.mockReturnValue(of(items));
    if (td) api.getOptionTechnicalData.mockReturnValue(of(td));
    const s = TestBed.inject(OptionStore);
    TestBed.tick();
    return s;
  }

  it('loads positions and one technical request per unique underlying', () => {
    const s = storeWith([
      option({ id: 1, underlyingTicker: 'AAPL' }),
      option({ id: 2, underlyingTicker: 'AAPL' }),
      option({ id: 3, underlyingTicker: 'MSFT' }),
    ]);

    expect(s.items()).toHaveLength(3);
    expect(s.loading()).toBe(false);
    expect(api.getOptionTechnicalData).toHaveBeenCalledTimes(2);
  });

  it('keeps a load failure silent apart from the loading flag', () => {
    api.getOptionItems.mockReturnValue(throwError(() => new Error('boom')));
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const s = TestBed.inject(OptionStore);
    TestBed.tick();

    expect(s.loading()).toBe(false);
    expect(s.items()).toEqual([]);
  });

  describe('contract multiplier and totals', () => {
    it('multiplies premium and market price by 100 per contract', () => {
      const s = storeWith([option({ premium: 2, marketPrice: 3, numberOfContracts: 2 })]);
      const [a] = s.analyses();

      expect(a.cost).toBe(400);
      expect(a.marketValue).toBe(600);
      expect(a.gainLoss).toBe(200);
      expect(a.gainLossPct).toBe(50);
    });

    it('reports 0% gain when the cost is zero', () => {
      const s = storeWith([option({ premium: 0, marketPrice: 1 })]);
      expect(s.analyses()[0].gainLossPct).toBe(0);
    });

    it('excludes CLOSE rows from the totals', () => {
      const s = storeWith([
        option({ id: 1, premium: 2, marketPrice: 3, numberOfContracts: 1 }),
        option({
          id: 2,
          premium: 5,
          marketPrice: 9,
          numberOfContracts: 1,
          transactionType: 'CLOSE',
        }),
      ]);

      expect(s.totalCost()).toBe(200);
      expect(s.totalMarketValue()).toBe(300);
    });
  });

  describe('rules engine', () => {
    it('flags a doubled contract as a free trade regardless of DTE', () => {
      const s = storeWith([option({ premium: 2, marketPrice: 4, expirationDate: inDays(3) })]);
      expect(s.analyses()[0].optionState).toBe('FREE_TRADE_MILESTONE');
    });

    it('falls back to MONITOR when no technical data arrives', () => {
      api.getOptionTechnicalData.mockReturnValue(throwError(() => new Error('boom')));
      const s = storeWith([option()]);

      expect(s.analyses()[0].optionState).toBe('MONITOR');
      expect(s.analyses()[0].stockPrice).toBeNull();
    });

    it('cracks a short-dated call when the stock falls under the strike buffer', () => {
      const s = storeWith(
        [option({ positionType: 'CALL', strike: 100, expirationDate: inDays(5) })],
        technical({ currentPrice: 99 }),
      );
      expect(s.analyses()[0].optionState).toBe('INTRINSIC_CRACKED');
    });

    it('reverses a swing put that closes above the 21-EMA', () => {
      const s = storeWith(
        [option({ positionType: 'PUT', strike: 100, expirationDate: inDays(20) })],
        technical({ currentPrice: 105, ema21: 100 }),
      );
      expect(s.analyses()[0].optionState).toBe('TREND_REVERSED');
    });

    it('takes profit on a long-dated oversold put', () => {
      const s = storeWith(
        [option({ positionType: 'PUT', strike: 100, expirationDate: inDays(90) })],
        technical({ currentPrice: 100, rsi14: 30, yesterdayHigh: 200 }),
      );
      expect(s.analyses()[0].optionState).toBe('TARGET_ACHIEVED');
    });
  });

  describe('mutations', () => {
    it('addItem appends, fetches technicals and notifies', async () => {
      const created = option({ id: 7, underlyingTicker: 'NVDA' });
      api.addOptionItem.mockReturnValue(of(created));
      const s = storeWith([]);
      api.getOptionTechnicalData.mockClear();

      await expect(s.addItem({} as never)).resolves.toBe(created);
      expect(s.items()).toEqual([created]);
      expect(api.getOptionTechnicalData).toHaveBeenCalledWith('NVDA');
      expect(snackBar.open).toHaveBeenCalledWith('Option position added', 'Dismiss', {
        duration: 3000,
      });
    });

    it('updateItem replaces the row and offers the cash follow-up', async () => {
      const updated = option({ id: 1, marketPrice: 9 });
      api.updateOptionItem.mockReturnValue(of(updated));
      const s = storeWith([option({ id: 1 })]);

      await s.updateItem(1, {} as never);

      expect(s.items()[0].marketPrice).toBe(9);
      expect(tradeCashLink.offerForOption).toHaveBeenCalledOnce();
    });

    it('updateItem rejects and warns on failure', async () => {
      api.updateOptionItem.mockReturnValue(throwError(() => new Error('boom')));
      const s = storeWith([option({ id: 1 })]);

      await expect(s.updateItem(1, {} as never)).rejects.toThrow();
      expect(snackBar.open).toHaveBeenCalledWith('Failed to update option position', 'Dismiss', {
        duration: 4000,
      });
    });

    it('deleteItem removes the row and describes it to the cash follow-up', () => {
      api.deleteOptionItem.mockReturnValue(of(void 0));
      const s = storeWith([
        option({ id: 1, underlyingTicker: 'AAPL', positionType: 'PUT', strike: 150 }),
      ]);

      s.deleteItem(1);

      expect(s.items()).toEqual([]);
      expect(tradeCashLink.offerRemoveForOption).toHaveBeenCalledWith(1, 'AAPL PUT $150');
    });

    it('patchItemNotes updates one row only', () => {
      const s = storeWith([option({ id: 1 }), option({ id: 2 })]);

      s.patchItemNotes(2, 'roll me');

      expect(s.items().map((x) => x.notes)).toEqual([undefined, 'roll me']);
    });
  });

  it('renames the account type on every affected position', () => {
    const s = storeWith([
      option({ id: 1, accountType: 'TFSA' }),
      option({ id: 2, accountType: 'RRSP' }),
    ]);

    renamed.set({ oldName: 'TFSA', newName: 'TFSA-1' });
    TestBed.tick();

    expect(s.items().map((x) => x.accountType)).toEqual(['TFSA-1', 'RRSP']);
  });
});
