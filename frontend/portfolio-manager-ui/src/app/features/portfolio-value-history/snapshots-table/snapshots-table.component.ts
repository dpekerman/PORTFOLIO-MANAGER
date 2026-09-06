import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  CashItem,
  PortfolioValueHistoryDto,
  SnapshotStatus,
} from '../../../core/models/portfolio.models';
import { CashStateService } from '../../../core/services/cash-state.service';
import { DemoModeService } from '../../../core/services/demo-mode.service';
import { PortfolioValueHistoryStateService } from '../../../core/services/portfolio-value-history-state.service';
import { SnapshotDetailComponent } from './snapshot-detail/snapshot-detail.component';

interface DailyChange {
  changeAmount: number | null;
  changePct: number | null;
}

interface ReconciliationIcon {
  icon: string;
  cssClass: string;
  tooltip: string;
}

@Component({
  selector: 'app-snapshots-table',
  templateUrl: './snapshots-table.component.html',
  styleUrl: './snapshots-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTableModule,
    MatChipsModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    SnapshotDetailComponent,
  ],
})
export class SnapshotsTableComponent {
  protected readonly historyState = inject(PortfolioValueHistoryStateService);
  protected readonly cashState = inject(CashStateService);
  protected readonly demoMode = inject(DemoModeService);

  protected readonly displayedColumns = [
    'recordedDate',
    'totalValue',
    'stocksValue',
    'optionsValue',
    'cashValue',
    'externalCashFlow',
    'dailyChange',
    'dailyPct',
    'status',
    'recordedAt',
    'lastRecalculatedAt',
    'expand',
  ];

  protected readonly expandedId = signal<number | null>(null);

  /** Chronological pairing (full unfiltered list) so filtering never distorts day-over-day deltas. */
  protected readonly dailyChanges = computed(() => {
    const all = this.historyState.snapshots();
    const map = new Map<number, DailyChange>();
    for (let i = 0; i < all.length; i++) {
      const current = all[i];
      const prev = all[i + 1];
      if (!prev) {
        map.set(current.id, { changeAmount: null, changePct: null });
        continue;
      }
      const changeAmount = current.totalValue - prev.totalValue;
      const changePct = prev.totalValue !== 0 ? (changeAmount / prev.totalValue) * 100 : null;
      map.set(current.id, { changeAmount, changePct });
    }
    return map;
  });

  protected dv(value: number): number {
    return this.demoMode.maskValue(value);
  }

  protected dailyChangeFor(row: PortfolioValueHistoryDto): DailyChange {
    return this.dailyChanges().get(row.id) ?? { changeAmount: null, changePct: null };
  }

  protected statusLabel(status: SnapshotStatus): string {
    switch (status) {
      case 'CashRecalculated':
        return 'Cash Recalculated';
      case 'PendingReseal':
        return 'Pending Reseal';
      default:
        return status;
    }
  }

  protected reconciliationIcon(row: PortfolioValueHistoryDto): ReconciliationIcon {
    if (row.snapshotStatus === 'PendingReseal') {
      return {
        icon: 'schedule',
        cssClass: 'icon-pending',
        tooltip: "Waiting for the 90-second quiet period before today's snapshot reseals",
      };
    }
    if (row.hasMismatch) {
      return {
        icon: 'warning',
        cssClass: 'icon-warning',
        tooltip: 'Detected mismatch: Total does not equal Stocks + Options + Cash',
      };
    }
    return { icon: 'check_circle', cssClass: 'icon-ok', tooltip: 'Internally reconciled' };
  }

  protected cashActivityFor(row: PortfolioValueHistoryDto): CashItem[] {
    return this.cashState.items().filter((c) => this.effectiveDate(c) === row.recordedDate);
  }

  protected toggleRow(row: PortfolioValueHistoryDto): void {
    this.expandedId.update((id) => (id === row.id ? null : row.id));
  }

  private effectiveDate(item: CashItem): string {
    return (item.transactionDate ?? item.addedAt).slice(0, 10);
  }
}
