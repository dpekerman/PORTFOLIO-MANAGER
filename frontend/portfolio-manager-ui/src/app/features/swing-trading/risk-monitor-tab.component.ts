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
import { SwingClosePositionService } from '../../core/services/swing-close-position.service';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { GridColumnStore } from '../../core/store/grid-column.store';
import { SwingTradingStore } from '../../core/store/swing-trading.store';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';
import { FinalActionDetailsDialogComponent } from '../../shared/final-action-details-dialog/final-action-details-dialog.component';
import { FinalActionBadgeComponent } from '../../shared/final-action-badge/final-action-badge.component';
import { openManageRiskDialog } from '../../shared/manage-risk-dialog/manage-risk-dialog.component';
import { MobileTableControlsComponent } from '../../shared/responsive-table/mobile-table-controls.component';
import { ResponsiveTableDirective } from '../../shared/responsive-table/responsive-table.directive';
import { torontoDateString, txRowsToCsv, txSheet } from '../transactions/transactions-logic';
import { SwingDetailChipComponent } from './swing-detail-chip.component';
import { SwingFilterBarComponent } from './swing-filter-bar.component';
import { SwingPlannedStopCellComponent } from './swing-planned-stop-cell.component';
import { SwingRiskCellComponent } from './swing-risk-cell.component';
import { brokerStopView, priceView, protectionView } from './swing-presenters';
import {
  OPEN_TRADES_EXPORT_COLUMNS,
  compareNullable,
  downloadTextFile,
  manageRiskDataFor,
  openTradesExportRows,
} from './swing-trading-logic';

/**
 * Risk Monitor: the same Swing trades as Open Trades, prioritised by stop risk. A breach is a market observation
 * (price at/below the recorded stop at a stated time and basis); it never claims a broker order triggered or filled,
 * and it never creates a trading recommendation. The canonical Final Action is shown next to it, unchanged.
 */
@Component({
  selector: 'app-risk-monitor-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FinalActionBadgeComponent,
    GridColumnButtonComponent,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    MobileTableControlsComponent,
    ResponsiveTableDirective,
    SwingDetailChipComponent,
    SwingFilterBarComponent,
    SwingPlannedStopCellComponent,
    SwingRiskCellComponent,
  ],
  templateUrl: './risk-monitor-tab.component.html',
  styleUrl: './risk-monitor-tab.component.scss',
})
export class RiskMonitorTabComponent {
  protected readonly store = inject(SwingTradingStore);
  protected readonly demo = inject(DemoModeStore);
  private readonly dialog = inject(MatDialog);
  private readonly closer = inject(SwingClosePositionService);

  protected readonly columns = inject(GridColumnStore).getResponsiveColumnKeys('swing-risk-monitor');
  protected readonly sort = signal<Sort>({ active: '', direction: '' });
  protected readonly priceOf = priceView;
  protected readonly brokerStopOf = brokerStopView;
  protected readonly protectionOf = protectionView;

  /** Server order is already highest risk first (breached + unprotected, then breached, approaching, gaps, stale). */
  protected readonly rows = computed(() => {
    const { active, direction } = this.sort();
    const data = this.store.filteredRows();
    if (!active || !direction) return data;
    const dir = direction === 'asc' ? 1 : -1;
    return [...data].sort((a, b) => dir * compareNullable(this.sortValue(a, active), this.sortValue(b, active)));
  });

  protected readonly breached = computed(() => this.rows().filter((r) => r.riskStatuses.includes('StopBreached')));

  protected mv(value: number | null): number | null {
    return value === null ? null : this.demo.maskValue(value);
  }

  protected mp(value: number | null): number | null {
    return value === null ? null : this.demo.maskPercent(value);
  }

  protected onSort(s: Sort): void {
    this.sort.set(s);
  }

  private sortValue(r: SwingTradeRow, key: string): string | number | null {
    switch (key) {
      case 'symbol': return r.symbol;
      case 'account': return r.account;
      case 'price': return r.currentPrice;
      case 'asOf': return r.priceAsOfUtc;
      case 'plannedStop': return r.plannedStop;
      case 'brokerStop': return r.brokerStopTrigger;
      case 'distance': return r.distancePct;
      case 'covered': return r.coveredQuantity;
      case 'protection': return r.protection;
      case 'severity': return r.severityRank;
      case 'finalAction': return this.store.finalActionOf(r);
      default: return null;
    }
  }

  protected manage(row: SwingTradeRow): void {
    openManageRiskDialog(this.dialog, manageRiskDataFor(row));
  }

  protected canClose(row: SwingTradeRow): boolean {
    return this.closer.hasEligibleLot(row);
  }

  protected closePosition(row: SwingTradeRow): void {
    void this.closer.open(row);
  }

  protected reviewOrder(account: string, symbol: string): void {
    openManageRiskDialog(this.dialog, { account, symbol });
  }

  protected showFinalAction(row: SwingTradeRow, lotId?: number): void {
    const result = this.store.finalActionResultFor(lotId === undefined ? row : { ...row, lotIds: [lotId] });
    if (result) this.dialog.open(FinalActionDetailsDialogComponent, { data: { result }, autoFocus: 'dialog', maxWidth: '95vw' });
  }

  protected exportCsv(): void {
    if (this.demo.isDemoMode()) return;
    const rows = openTradesExportRows(this.rows(), (r) => this.store.finalActionOf(r), this.store.thresholdPct());
    downloadTextFile(`swing-risk-monitor-${torontoDateString()}.csv`, txRowsToCsv(OPEN_TRADES_EXPORT_COLUMNS, rows));
  }

  protected exportXlsx(): void {
    if (this.demo.isDemoMode()) return;
    const rows = openTradesExportRows(this.rows(), (r) => this.store.finalActionOf(r), this.store.thresholdPct());
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, txSheet(OPEN_TRADES_EXPORT_COLUMNS, rows), 'Risk Monitor');
    XLSX.utils.book_append_sheet(
      book,
      XLSX.utils.aoa_to_sheet([
        ['Advisory approaching-stop threshold %', this.store.thresholdPct()],
        ['Note', this.store.thresholdNote()],
        ['Breach wording', 'A price at/below a recorded stop is a market observation, not proof a broker order executed.'],
      ]),
      'Notes',
    );
    XLSX.writeFile(book, `swing-risk-monitor-${torontoDateString()}.xlsx`);
  }
}
