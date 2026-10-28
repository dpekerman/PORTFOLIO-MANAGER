import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import * as XLSX from 'xlsx';
import { JournalRow } from '../../core/models/swing-trading.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { GridColumnStore } from '../../core/store/grid-column.store';
import { SwingTradingStore } from '../../core/store/swing-trading.store';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';
import { MobileTableControlsComponent } from '../../shared/responsive-table/mobile-table-controls.component';
import { ResponsiveTableDirective } from '../../shared/responsive-table/responsive-table.directive';
import { torontoDateString, txRowsToCsv, txSheet } from '../transactions/transactions-logic';
import { JournalDetailsDialogComponent } from './journal-details-dialog.component';
import {
  JOURNAL_EXPORT_COLUMNS,
  compareNullable,
  distinctSorted,
  downloadTextFile,
  journalExportRows,
} from './swing-trading-logic';

/**
 * Trade Journal: completed (or partly closed) Swing trades from the existing transaction records. Realized P/L is the
 * existing (closing price − average cost) × shares of each CLOSE record; trades are never merged unless they share a
 * stored trade-episode id, so an unresolved CLOSE record always appears as its own row.
 */
@Component({
  selector: 'app-trade-journal-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    GridColumnButtonComponent,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    MobileTableControlsComponent,
    ResponsiveTableDirective,
  ],
  templateUrl: './trade-journal-tab.component.html',
  styleUrl: './trade-journal-tab.component.scss',
})
export class TradeJournalTabComponent {
  protected readonly store = inject(SwingTradingStore);
  protected readonly demo = inject(DemoModeStore);
  private readonly dialog = inject(MatDialog);

  protected readonly columns = inject(GridColumnStore).getResponsiveColumnKeys('swing-journal');
  protected readonly search = signal('');
  protected readonly account = signal('');
  protected readonly outcome = signal('');
  protected readonly sort = signal<Sort>({ active: '', direction: '' });

  protected readonly accounts = computed(() => distinctSorted(this.store.journal().map((r) => r.account)));
  protected readonly outcomes = ['Win', 'Loss', 'Flat', 'Unknown', 'Partially closed'];

  protected readonly rows = computed(() => {
    const q = this.search().trim().toLowerCase();
    const acct = this.account();
    const out = this.outcome();
    const filtered = this.store
      .journal()
      .filter(
        (r) =>
          (!acct || r.account === acct) &&
          (!out || r.outcome === out) &&
          (!q || `${r.symbol} ${r.account} ${r.thesis ?? ''} ${r.notes ?? ''}`.toLowerCase().includes(q)),
      );
    const { active, direction } = this.sort();
    if (!active || !direction) return filtered;
    const dir = direction === 'asc' ? 1 : -1;
    return [...filtered].sort((a, b) => dir * compareNullable(this.sortValue(a, active), this.sortValue(b, active)));
  });

  protected onSort(s: Sort): void {
    this.sort.set(s);
  }

  private sortValue(r: JournalRow, key: string): string | number | null {
    switch (key) {
      case 'symbol': return r.symbol;
      case 'account': return r.account;
      case 'openDate': return r.openDate;
      case 'closeDate': return r.finalCloseDate;
      case 'daysHeld': return r.daysHeld;
      case 'entry': return r.entryPrice;
      case 'exit': return r.exitPrice;
      case 'pnl': return r.realizedPnl;
      case 'pnlPct': return r.realizedPnlPct;
      case 'outcome': return r.outcome;
      case 'originalSource': return r.originalDecisionSource;
      case 'closingSource': return r.closingDecisionSource;
      default: return null;
    }
  }

  protected mv(n: number | null): number | null {
    return n === null ? null : this.demo.maskValue(n);
  }

  protected mp(n: number | null): number | null {
    return n === null ? null : this.demo.maskPercent(n);
  }

  protected details(row: JournalRow): void {
    this.dialog.open(JournalDetailsDialogComponent, { data: { row }, autoFocus: 'dialog', maxWidth: '95vw', width: '640px' });
  }

  protected exportCsv(): void {
    downloadTextFile(
      `swing-trade-journal-${torontoDateString()}.csv`,
      txRowsToCsv(JOURNAL_EXPORT_COLUMNS, journalExportRows(this.rows())),
    );
  }

  protected exportXlsx(): void {
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, txSheet(JOURNAL_EXPORT_COLUMNS, journalExportRows(this.rows())), 'Trade Journal');
    XLSX.writeFile(book, `swing-trade-journal-${torontoDateString()}.xlsx`);
  }
}
