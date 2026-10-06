import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CashItem, OptionItem, PortfolioItem, UnlinkedTrade } from '../models/portfolio.models';
import { CashStateService } from './cash-state.service';
import { DashboardStateService } from './dashboard-state.service';
import { DemoModeService } from './demo-mode.service';
import { PortfolioApiService } from './portfolio-api.service';
import { TradeCashLinkService } from './trade-cash-link.service';

const stock = (over: Partial<PortfolioItem> = {}): PortfolioItem =>
  ({
    id: 7,
    symbol: 'MCD.TO',
    shares: 500,
    averageCostBasis: 20,
    isManual: false,
    transactionType: 'OPEN',
    accountType: 'TFSA_D_TD',
    openDate: '2026-09-25T00:00:00',
    closeDate: null,
    closingPrice: null,
    ...over,
  }) as PortfolioItem;

const option = (over: Partial<OptionItem> = {}): OptionItem =>
  ({
    id: 3,
    underlyingTicker: 'T.TO',
    positionType: 'CALL',
    strike: 30,
    premium: 1.4,
    numberOfContracts: 30,
    transactionType: 'OPEN',
    accountType: 'TFSA_D_TD',
    openDate: '2026-09-25T00:00:00',
    ...over,
  }) as OptionItem;

const linkedRow = (over: Partial<CashItem> = {}): CashItem =>
  ({
    id: 99,
    description: 'Buy',
    amount: -10000,
    cashFlowType: 'TradePurchase',
    accountType: 'TFSA_D_TD',
    transactionDate: '2026-09-25T00:00:00',
    sourceType: 'PortfolioOpen',
    sourceItemId: 7,
    ...over,
  }) as CashItem;

describe('TradeCashLinkService', () => {
  let service: TradeCashLinkService;
  let getLinkedCash: ReturnType<typeof vi.fn>;
  let dialogResult: unknown;
  let dialogOpen: ReturnType<typeof vi.fn>;
  const cashState = {
    items: () => [] as CashItem[],
    addLinked: vi.fn(),
    updateItem: vi.fn(),
    removeItems: vi.fn(),
    refresh: vi.fn(),
  };
  const dashboardState = { refresh: vi.fn() };

  const result = (amount: number, over: Record<string, unknown> = {}) => ({
    amount,
    accountType: 'TFSA_D_TD',
    transactionDate: '2026-09-25',
    description: 'desc',
    ...over,
  });

  beforeEach(() => {
    vi.clearAllMocks();
    getLinkedCash = vi.fn(() => of(null));
    dialogResult = undefined;
    dialogOpen = vi.fn(() => ({ afterClosed: () => of(dialogResult) }));
    cashState.items = () => [];
    cashState.addLinked.mockResolvedValue(linkedRow());
    cashState.updateItem.mockResolvedValue(undefined);
    cashState.removeItems.mockResolvedValue(undefined);

    TestBed.configureTestingModule({
      providers: [
        TradeCashLinkService,
        { provide: PortfolioApiService, useValue: { getLinkedCash } },
        { provide: CashStateService, useValue: cashState },
        { provide: DashboardStateService, useValue: dashboardState },
        { provide: MatDialog, useValue: { open: dialogOpen } },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
        {
          provide: DemoModeService,
          useValue: {
            isDemoMode: () => false,
            demoStyle: () => 'fake',
            maskValue: (v: number) => v,
          },
        },
      ],
    });
    service = TestBed.inject(TradeCashLinkService);
  });

  it('offers a purchase for a brand-new stock and links the confirmed amount', async () => {
    dialogResult = result(10000);

    await service.offerForStock(null, stock());

    expect(dialogOpen).toHaveBeenCalledOnce();
    const data = dialogOpen.mock.calls[0][1].data;
    expect(data.mode).toBe('create');
    expect(data.direction).toBe('out');
    expect(data.amount).toBe(10000);
    expect(cashState.addLinked).toHaveBeenCalledWith(
      expect.objectContaining({ sourceType: 'PortfolioOpen', sourceItemId: 7, amount: 10000 }),
    );
    expect(dashboardState.refresh).toHaveBeenCalledOnce();
  });

  it('creates nothing when the user skips the prompt', async () => {
    dialogResult = undefined;

    await service.offerForStock(null, stock());

    expect(cashState.addLinked).not.toHaveBeenCalled();
    expect(dashboardState.refresh).not.toHaveBeenCalled();
  });

  it('does not look anything up or prompt when an edit changes no cash-relevant field', async () => {
    await service.offerForStock(
      stock(),
      stock({ companyName: 'Renamed' } as Partial<PortfolioItem>),
    );

    expect(getLinkedCash).not.toHaveBeenCalled();
    expect(dialogOpen).not.toHaveBeenCalled();
  });

  it('never prompts for a manual position', async () => {
    await service.offerForStock(null, stock({ isManual: true }));

    expect(dialogOpen).not.toHaveBeenCalled();
  });

  it('never nags about an old, never-linked position when only its size is edited', async () => {
    await service.offerForStock(stock(), stock({ shares: 600 }));

    expect(getLinkedCash).toHaveBeenCalled();
    expect(dialogOpen).not.toHaveBeenCalled();
    expect(cashState.addLinked).not.toHaveBeenCalled();
  });

  it('offers proceeds (cash in) when an open position becomes closed', async () => {
    dialogResult = result(12000);

    await service.offerForStock(
      stock(),
      stock({ transactionType: 'CLOSE', closingPrice: 24, closeDate: '2026-09-28T00:00:00' }),
    );

    const data = dialogOpen.mock.calls[0][1].data;
    expect(data.direction).toBe('in');
    expect(data.amount).toBe(12000);
    expect(cashState.addLinked).toHaveBeenCalledWith(
      expect.objectContaining({ sourceType: 'PortfolioClose', amount: 12000 }),
    );
  });

  it('leaves the amount empty for a close that has no closing price', async () => {
    await service.offerForStock(stock(), stock({ transactionType: 'CLOSE', closingPrice: null }));

    expect(dialogOpen.mock.calls[0][1].data.amount).toBeNull();
  });

  it('applies the x100 contract multiplier for options', async () => {
    dialogResult = result(4200);

    await service.offerForOption(null, option());

    expect(dialogOpen.mock.calls[0][1].data.amount).toBe(4200);
    expect(cashState.addLinked).toHaveBeenCalledWith(
      expect.objectContaining({ sourceType: 'OptionOpen', sourceItemId: 3 }),
    );
  });

  it('updates the same linked row when a cash-relevant field changes', async () => {
    getLinkedCash.mockReturnValue(of(linkedRow()));
    dialogResult = result(20000);

    await service.offerForStock(stock(), stock({ shares: 1000 }));

    expect(dialogOpen.mock.calls[0][1].data.mode).toBe('update');
    expect(cashState.updateItem).toHaveBeenCalledWith(
      99,
      expect.objectContaining({ amount: 20000, cashFlowType: 'TradePurchase' }),
    );
    expect(cashState.addLinked).not.toHaveBeenCalled();
    expect(dashboardState.refresh).toHaveBeenCalledOnce();
  });

  it('stays quiet when the linked row already matches the edited trade', async () => {
    getLinkedCash.mockReturnValue(of(linkedRow({ amount: -20000 })));

    await service.offerForStock(stock(), stock({ shares: 1000 }));

    expect(dialogOpen).not.toHaveBeenCalled();
  });

  it('warns about a hand-entered look-alike row before creating a duplicate', async () => {
    cashState.items = () => [
      linkedRow({ id: 5, sourceType: null, sourceItemId: null, transactionDate: '2026-09-25' }),
    ];

    await service.offerForStock(null, stock());

    expect(dialogOpen.mock.calls[0][1].data.duplicate?.id).toBe(5);
  });

  it('pre-fills the trade date when catching up an unlinked trade', async () => {
    dialogResult = result(200);
    const leg: UnlinkedTrade = {
      sourceType: 'PortfolioOpen',
      sourceItemId: 11,
      symbol: 'BBB.TO',
      label: 'Buy BBB.TO',
      quantity: '100 sh',
      price: 2,
      amount: 200,
      accountType: 'TFSA_D_TD',
      tradeDate: '2026-09-02T00:00:00',
      reason: 'NoCash',
    };

    await service.linkUnlinkedTrade(leg);

    const data = dialogOpen.mock.calls[0][1].data;
    expect(data.defaultDate).toBe('2026-09-02');
    expect(cashState.addLinked).toHaveBeenCalledWith(
      expect.objectContaining({ sourceType: 'PortfolioOpen', sourceItemId: 11, amount: 200 }),
    );
  });

  it('removes the linked cash after a delete only when the user confirms', async () => {
    getLinkedCash.mockImplementation((type: string) =>
      of(type === 'PortfolioOpen' ? linkedRow() : null),
    );
    dialogResult = false;
    await service.offerRemoveForStock(7, 'MCD.TO');
    expect(cashState.removeItems).not.toHaveBeenCalled();

    dialogResult = true;
    await service.offerRemoveForStock(7, 'MCD.TO');
    expect(cashState.removeItems).toHaveBeenCalledWith([99]);
    expect(dashboardState.refresh).toHaveBeenCalledOnce();
  });

  it('does not prompt on delete when nothing was linked', async () => {
    await service.offerRemoveForOption(3, 'T.TO');

    expect(dialogOpen).not.toHaveBeenCalled();
  });

  it('reloads cash and dashboard after a partial close', () => {
    service.onPartialClose();

    expect(cashState.refresh).toHaveBeenCalledOnce();
    expect(dashboardState.refresh).toHaveBeenCalledOnce();
  });
});
