import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { CashItem, PortfolioValueHistoryDto } from '../../core/models/portfolio.models';
import { CashStateService } from '../../core/services/cash-state.service';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { PortfolioValueHistoryStateService } from '../../core/services/portfolio-value-history-state.service';
import { CashLedgerTableComponent } from './cash-ledger-table/cash-ledger-table.component';
import { SnapshotsTableComponent } from './snapshots-table/snapshots-table.component';

function snapshot(id: number, totalValue: number): PortfolioValueHistoryDto {
  return {
    id,
    recordedDate: `2026-10-0${id}`,
    recordedAt: `2026-10-0${id}T21:00:00Z`,
    totalValue,
    stocksValue: totalValue,
    optionsValue: 0,
    cashValue: 0,
    externalCashFlow: 0,
    source: 'ManualRecordNow',
    lastRecalculatedAt: null,
    snapshotStatus: 'Original',
    hasMismatch: false,
  };
}

function cells<T>(fixture: ComponentFixture<T>, column: string): string[] {
  const element: HTMLElement = fixture.nativeElement;
  return Array.from(
    element.querySelectorAll(`td.mat-column-${column}`),
    (cell) => cell.textContent?.trim() ?? '',
  );
}

async function clickHeader<T>(fixture: ComponentFixture<T>, column: string): Promise<void> {
  const element: HTMLElement = fixture.nativeElement;
  const header = element.querySelector<HTMLElement>(
    `th.mat-column-${column} .mat-sort-header-container`,
  );
  expect(header).not.toBeNull();
  header!.click();
  fixture.detectChanges();
  await fixture.whenStable();
}

describe('Portfolio value history table sorting', () => {
  const snapshots = signal([snapshot(3, 120), snapshot(2, 90), snapshot(1, 100)]);
  const filteredSnapshots = signal([...snapshots()].reverse());
  const items = signal<CashItem[]>([
    {
      id: 1,
      description: 'Opening',
      amount: 100,
      addedAt: '2026-10-04T10:00:00Z',
      transactionDate: '2026-10-01',
      accountType: 'TFSA',
      isExternalFlow: false,
      cashFlowType: 'OpeningBalance',
    },
    {
      id: 2,
      description: 'Fee',
      amount: -5,
      addedAt: '2026-10-04T11:00:00Z',
      transactionDate: '2026-10-02',
      accountType: 'TFSA',
      isExternalFlow: false,
      cashFlowType: 'Fee',
    },
    {
      id: 3,
      description: 'Withdrawal',
      amount: -10,
      addedAt: '2026-10-03T12:00:00Z',
      accountType: 'TFSA',
      isExternalFlow: true,
      cashFlowType: 'Withdrawal',
    },
  ]);

  beforeEach(() => {
    filteredSnapshots.set([...snapshots()].reverse());
    TestBed.configureTestingModule({
      providers: [
        {
          provide: PortfolioValueHistoryStateService,
          useValue: { snapshots, filteredSnapshots, loading: signal(false) },
        },
        {
          provide: CashStateService,
          useValue: {
            items,
            loading: signal(false),
            totalsByAccount: signal([]),
            totalCash: signal(85),
          },
        },
        {
          provide: DemoModeService,
          useValue: { maskValue: (value: number) => value, maskPercent: (value: number) => value },
        },
        { provide: MatDialog, useValue: { open: vi.fn() } },
      ],
    });
  });

  it('defaults snapshots to descending dates and toggles ascending by clicking Date', async () => {
    const fixture = TestBed.createComponent(SnapshotsTableComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(cells(fixture, 'recordedDate')).toEqual(['Oct 3, 2026', 'Oct 2, 2026', 'Oct 1, 2026']);
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('th.mat-column-recordedDate')?.getAttribute('aria-sort')).toBe(
      'descending',
    );
    expect(element.querySelectorAll('th[mat-sort-header]').length).toBe(11);

    await clickHeader(fixture, 'recordedDate');
    expect(cells(fixture, 'recordedDate')).toEqual(['Oct 1, 2026', 'Oct 2, 2026', 'Oct 3, 2026']);
    await clickHeader(fixture, 'recordedDate');
    expect(cells(fixture, 'recordedDate')).toEqual(['Oct 3, 2026', 'Oct 2, 2026', 'Oct 1, 2026']);
  });

  it('sorts snapshot amounts and derived daily changes without changing chronological deltas', async () => {
    const fixture = TestBed.createComponent(SnapshotsTableComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    await clickHeader(fixture, 'totalValue');
    expect(cells(fixture, 'recordedDate')).toEqual(['Oct 3, 2026', 'Oct 1, 2026', 'Oct 2, 2026']);
    await clickHeader(fixture, 'totalValue');
    expect(cells(fixture, 'recordedDate')).toEqual(['Oct 2, 2026', 'Oct 1, 2026', 'Oct 3, 2026']);

    await clickHeader(fixture, 'dailyChange');
    expect(cells(fixture, 'recordedDate')).toEqual(['Oct 3, 2026', 'Oct 2, 2026', 'Oct 1, 2026']);
    expect(cells(fixture, 'dailyChange')).toEqual(['$30.00', '-$10.00', '—']);
    filteredSnapshots.set([snapshots()[0], snapshots()[2]]);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(cells(fixture, 'dailyChange')).toEqual(['$30.00', '—']);
    expect(snapshots().map((row) => row.id)).toEqual([3, 2, 1]);
  });

  it('defaults ledger to descending effective dates including the Created fallback', async () => {
    const fixture = TestBed.createComponent(CashLedgerTableComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(cells(fixture, 'effectiveDate')).toEqual(['Oct 3, 2026', 'Oct 2, 2026', 'Oct 1, 2026']);
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('th.mat-column-effectiveDate')?.getAttribute('aria-sort')).toBe(
      'descending',
    );
    expect(element.querySelectorAll('th[mat-sort-header]').length).toBe(8);
    await clickHeader(fixture, 'effectiveDate');
    expect(cells(fixture, 'effectiveDate')).toEqual(['Oct 1, 2026', 'Oct 2, 2026', 'Oct 3, 2026']);
  });

  it('sorts ledger amounts while preserving per-entry running balances and mobile order', async () => {
    const fixture = TestBed.createComponent(CashLedgerTableComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    await clickHeader(fixture, 'amount');
    expect(cells(fixture, 'effectiveDate')).toEqual(['Oct 1, 2026', 'Oct 2, 2026', 'Oct 3, 2026']);
    expect(cells(fixture, 'runningBalance')).toEqual(['$100.00', '$95.00', '$85.00']);
    const element: HTMLElement = fixture.nativeElement;
    expect(
      Array.from(element.querySelectorAll('.card-amount'), (cell) => cell.textContent?.trim()),
    ).toEqual(['+$100.00', '-$5.00', '-$10.00']);
    await clickHeader(fixture, 'runningBalance');
    await clickHeader(fixture, 'runningBalance');
    expect(cells(fixture, 'runningBalance')).toEqual(['$85.00', '$95.00', '$100.00']);
    expect(items().map((item) => item.id)).toEqual([1, 2, 3]);
  });
});
