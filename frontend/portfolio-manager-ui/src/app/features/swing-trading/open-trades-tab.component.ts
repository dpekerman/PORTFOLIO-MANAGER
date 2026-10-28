import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import * as XLSX from 'xlsx';
import { SwingTradeRow } from '../../core/models/swing-trading.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { GridColumnStore } from '../../core/store/grid-column.store';
import { SwingTradingStore } from '../../core/store/swing-trading.store';
import { FinalActionDetailsDialogComponent } from '../../shared/final-action-details-dialog/final-action-details-dialog.component';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';
import { openManageRiskDialog } from '../../shared/manage-risk-dialog/manage-risk-dialog.component';
import { MobileTableControlsComponent } from '../../shared/responsive-table/mobile-table-controls.component';
import { ResponsiveTableDirective } from '../../shared/responsive-table/responsive-table.directive';
import { torontoDateString, txRowsToCsv, txSheet } from '../transactions/transactions-logic';
import { SwingFilterBarComponent } from './swing-filter-bar.component';
import {
  OPEN_TRADES_EXPORT_COLUMNS,
  compareNullable,
  downloadTextFile,
  manageRiskDataFor,
  openTradesExportRows,
  priceBasisLabel,
  riskStatusLabel,
  severityClass,
} from './swing-trading-logic';

type SortKey = string;

/**
 * Open Swing trades: Swing exposure per account+symbol with the stop the user planned and the broker stop the user
 * recorded. The Final Action shown is the canonical one from {@link FinalActionStore}; nothing here recommends a trade.
 */
@Component({
  selector: 'app-open-trades-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    GridColumnButtonComponent,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    MobileTableControlsComponent,
    ResponsiveTableDirective,
    SwingFilterBarComponent,
  ],
  templateUrl: './open-trades-tab.component.html',
  styleUrl: './open-trades-tab.component.scss',
})
export class OpenTradesTabComponent {
  protected readonly store = inject(SwingTradingStore);
  protected readonly demo = inject(DemoModeStore);
  private readonly dialog = inject(MatDialog);

  protected readonly columns = inject(GridColumnStore).getResponsiveColumnKeys('swing-open-trades');
  protected readonly sort = signal<Sort>({ active: 'openDate', direction: 'desc' });
  protected readonly severityClass = severityClass;
  protected readonly riskLabel = riskStatusLabel;
  protected readonly basisLabel = priceBasisLabel;

  protected readonly rows = computed(() => {
    const { active, direction } = this.sort();
    const data = this.store.filteredRows();
    if (!active || !direction) return data;
    const dir = direction === 'asc' ? 1 : -1;
    return [...data].sort((a, b) => dir * compareNullable(this.sortValue(a, active), this.sortValue(b, active)));
  });

  protected onSort(s: Sort): void {
    this.sort.set(s);
  }

  private sortValue(r: SwingTradeRow, key: SortKey): string | number | null {
    switch (key) {
      case 'symbol': return r.symbol;
      case 'company': return r.companyName;
      case 'account': return r.account;
      case 'decisionSource': return r.decisionSource;
      case 'openDate': return r.episodeOpenDate;
      case 'daysHeld': return r.daysHeld;
      case 'quantity': return r.swingQuantity;
      case 'price': return r.currentPrice;
      case 'marketValue': return r.marketValue;
      case 'pnl': return r.unrealizedPnl;
      case 'stopLoss': return r.plannedStop;
      case 'brokerStop': return r.brokerStopTrigger;
      case 'target': return r.targetPrice;
      case 'stopType': return r.stopType;
      case 'protection': return r.protection;
      case 'distance': return r.distancePct;
      case 'risk': return r.severityRank;
      case 'finalAction': return this.store.finalActionOf(r);
      default: return null;
    }
  }

  protected mv(n: number | null): number | null {
    return n === null ? null : this.demo.maskValue(n);
  }

  protected mp(n: number | null): number | null {
    return n === null ? null : this.demo.maskPercent(n);
  }

  protected manage(row: SwingTradeRow): void {
    openManageRiskDialog(this.dialog, manageRiskDataFor(row));
  }

  protected showFinalAction(row: SwingTradeRow): void {
    const result = this.store.finalActionResultFor(row);
    if (result) this.dialog.open(FinalActionDetailsDialogComponent, { data: { result }, autoFocus: 'dialog', maxWidth: '95vw' });
  }

  protected exportCsv(): void {
    const rows = openTradesExportRows(this.rows(), (r) => this.store.finalActionOf(r), this.store.thresholdPct());
    downloadTextFile(`swing-open-trades-${torontoDateString()}.csv`, txRowsToCsv(OPEN_TRADES_EXPORT_COLUMNS, rows));
  }

  protected exportXlsx(): void {
    const rows = openTradesExportRows(this.rows(), (r) => this.store.finalActionOf(r), this.store.thresholdPct());
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, txSheet(OPEN_TRADES_EXPORT_COLUMNS, rows), 'Open Trades');
    XLSX.utils.book_append_sheet(
      book,
      XLSX.utils.aoa_to_sheet([
        ['Advisory approaching-stop threshold %', this.store.thresholdPct()],
        ['Note', this.store.thresholdNote()],
        ['Stops', 'User-recorded broker orders; not verified with the broker. Blank covered quantity = unknown.'],
      ]),
      'Notes',
    );
    XLSX.writeFile(book, `swing-open-trades-${torontoDateString()}.xlsx`);
  }
}
