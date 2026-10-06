import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CashItem } from '../../../core/models/portfolio.models';
import { CashStateService } from '../../../core/services/cash-state.service';
import { DemoModeService } from '../../../core/services/demo-mode.service';
import {
  EditCashDialogComponent,
  EditCashDialogData,
} from '../../portfolio/edit-cash-dialog/edit-cash-dialog.component';
import { HistorySortAccessors, sortHistoryRows } from '../history-table-sort';

@Component({
  selector: 'app-cash-ledger-table',
  templateUrl: './cash-ledger-table.component.html',
  styleUrl: './cash-ledger-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTableModule,
    MatSortModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    CurrencyPipe,
    DatePipe,
  ],
})
export class CashLedgerTableComponent {
  protected readonly cashState = inject(CashStateService);
  protected readonly demoMode = inject(DemoModeService);
  private readonly dialog = inject(MatDialog);

  protected readonly displayedColumns = [
    'effectiveDate',
    'accountType',
    'cashFlowType',
    'amount',
    'isExternalFlow',
    'runningBalance',
    'addedAt',
    'modifiedAt',
    'actions',
  ];

  protected readonly sort = signal<Sort>({ active: 'effectiveDate', direction: 'desc' });
  protected readonly sortedItems = computed(() =>
    sortHistoryRows(this.cashState.items(), this.sort(), this.sortAccessors, 'effectiveDate'),
  );

  private readonly sortAccessors: HistorySortAccessors<CashItem> = {
    effectiveDate: (item) => this.effectiveDate(item),
    accountType: (item) => item.accountType,
    cashFlowType: (item) => item.cashFlowType,
    amount: (item) => item.amount,
    isExternalFlow: (item) => Number(item.isExternalFlow),
    runningBalance: (item) => this.runningBalanceFor(item),
    addedAt: (item) => Date.parse(item.addedAt),
    modifiedAt: (item) => (item.modifiedAt ? Date.parse(item.modifiedAt) : null),
  };

  /** Cumulative running balance per account, computed in chronological (ascending) order. */
  protected readonly runningBalanceById = computed(() => {
    const byAccount = new Map<string, CashItem[]>();
    for (const item of this.cashState.items()) {
      const key = item.accountType ?? '—';
      const list = byAccount.get(key) ?? [];
      list.push(item);
      byAccount.set(key, list);
    }

    const result = new Map<number, number>();
    for (const items of byAccount.values()) {
      const chronological = [...items].sort(
        (a, b) => this.effectiveDate(a).localeCompare(this.effectiveDate(b)) || a.id - b.id,
      );
      let running = 0;
      for (const item of chronological) {
        running += item.amount;
        result.set(item.id, running);
      }
    }
    return result;
  });

  protected dv(value: number): number {
    return this.demoMode.maskValue(value);
  }

  protected effectiveDate(item: CashItem): string {
    return (item.transactionDate ?? item.addedAt).slice(0, 10);
  }

  protected runningBalanceFor(item: CashItem): number {
    return this.runningBalanceById().get(item.id) ?? item.amount;
  }

  protected editItem(item: CashItem): void {
    this.dialog.open(EditCashDialogComponent, {
      data: { item } satisfies EditCashDialogData,
      width: '420px',
      maxWidth: '95vw',
    });
  }

  /** OpeningBalance rows are migration/admin-only and must not be editable from this page. */
  protected isOpeningBalance(item: CashItem): boolean {
    return item.cashFlowType === 'OpeningBalance';
  }
}
