import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { MarketLeadershipRow } from '../../../core/models/portfolio.models';
import {
  priceStructureSortRank,
  priceStructureTooltip,
} from '../../../core/price-structure-display';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';
import { DemoModeService } from '../../../core/services/demo-mode.service';
import { MarketLeadershipStateService } from '../../../core/services/market-leadership-state.service';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { AddMarketTrackerDialogComponent } from '../add-market-tracker-dialog/add-market-tracker-dialog.component';

type MarketLeadershipSortColumn =
  | 'name'
  | 'price'
  | 'day'
  | 'fiveDay'
  | 'twentyDay'
  | 'ma'
  | 'momentum'
  | 'structure'
  | 'signal';

@Component({
  selector: 'app-market-leadership-widget',
  templateUrl: './market-leadership-widget.component.html',
  styleUrl: './market-leadership-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
})
export class MarketLeadershipWidgetComponent {
  private readonly demoMode = inject(DemoModeService);
  protected readonly dashboard = inject(DashboardStateService);
  protected readonly trackerState = inject(MarketLeadershipStateService);
  private readonly dialog = inject(MatDialog);
  protected readonly leadership = this.dashboard.marketLeadership;
  protected readonly loading = this.dashboard.marketLeadershipLoading;
  protected readonly selectedSignal = signal<string | null>(null);
  protected readonly sortColumn = signal<MarketLeadershipSortColumn | null>(null);
  protected readonly sortDirection = signal<1 | -1>(1);
  protected readonly expandedRowId = signal<number | null>(null);
  protected readonly rows = computed(() => {
    const response = this.leadership();
    const filter = this.selectedSignal();
    const rows = response?.rows.filter((row) => !filter || row.leadershipSignal === filter) ?? [];
    const column = this.sortColumn();
    if (!column) return rows;
    const direction = this.sortDirection();
    return [...rows].sort(
      (left, right) =>
        this.compareRows(left, right, column) * direction ||
        left.symbol.localeCompare(right.symbol),
    );
  });

  protected signalClass(signalName: string): string {
    return `ml-${signalName
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-|-$/g, '')}`;
  }

  protected returnClass(value: number): string {
    return value > 0.05 ? 'ml-positive' : value < -0.05 ? 'ml-negative' : 'ml-flat';
  }

  protected setSignalFilter(signalName: string): void {
    this.selectedSignal.update((current) => (current === signalName ? null : signalName));
  }

  protected clearSignalFilter(): void {
    this.selectedSignal.set(null);
  }

  protected toggleSort(column: MarketLeadershipSortColumn): void {
    if (this.sortColumn() === column) {
      this.sortDirection.update((direction) => (direction === 1 ? -1 : 1));
    } else {
      this.sortColumn.set(column);
      this.sortDirection.set(1);
    }
  }

  protected sortIndicator(column: MarketLeadershipSortColumn): string {
    return this.sortColumn() !== column ? '' : this.sortDirection() === 1 ? ' ▲' : ' ▼';
  }

  protected toggleDetails(rowId: number): void {
    this.expandedRowId.update((current) => (current === rowId ? null : rowId));
  }

  protected editTracker(row: MarketLeadershipRow): void {
    this.dialog.open(AddMarketTrackerDialogComponent, {
      autoFocus: 'first-tabbable',
      data: { tracker: row },
    });
  }

  protected addTracker(): void {
    this.dialog.open(AddMarketTrackerDialogComponent, { autoFocus: 'first-tabbable' });
  }

  protected async confirmRemove(row: MarketLeadershipRow): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open(ConfirmDialogComponent, {
          data: {
            title: 'Remove market tracker?',
            message: `${row.displayName} (${row.symbol}) will no longer appear in Market Leadership.`,
            confirmLabel: 'Remove',
            danger: true,
          },
        })
        .afterClosed(),
    );
    if (confirmed) this.trackerState.removeTracker(row.id);
  }

  protected maTooltip(row: MarketLeadershipRow): string {
    const cross = row.lastCross
      ? `\nLast 50/200 cross: ${row.lastCross}${row.lastCrossDate ? `\n${row.lastCrossDate}` : ''}${row.lastCrossTradingDaysAgo !== null ? `\n${row.lastCrossTradingDaysAgo} trading days ago` : ''}`
      : '\nLast 50/200 cross: none in available history';
    return `Moving Average Structure\nPrice: ${row.currentPrice}\nSMA50: ${row.sma50} (${row.priceVsSma50Pct}%)\nSMA200: ${row.sma200} (${row.priceVsSma200Pct}%)\nSMA50 vs SMA200: ${row.sma50VsSma200Pct}%\nStructure: ${row.maStructure}\nStatus: ${row.maBadge}${cross}`;
  }

  protected momentumTooltip(row: MarketLeadershipRow): string {
    return `Momentum\nDay: ${row.dayReturnPct}%\nCurrent 5D: ${row.fiveDayReturnPct}%\nPrevious 5D: ${row.previousFiveDayReturnPct}%\nCurrent 20D: ${row.twentyDayReturnPct}%\nPrevious 20D: ${row.previousTwentyDayReturnPct}%\nState: ${row.momentumState}\nReason: ${row.momentumReason}`;
  }

  protected signalTooltip(row: MarketLeadershipRow): string {
    const structure =
      row.priceStructure.label === '—' ? '' : `\nPrice Structure: ${row.priceStructure.label}`;
    return `${row.leadershipSignal}\n${row.leadershipReason}${structure}`;
  }

  protected structureTooltip(row: MarketLeadershipRow): string {
    return priceStructureTooltip(row.priceStructure, (value) => this.demoMode.maskValue(value), {
      ticker: row.analysisTicker,
      market: row.analysisMarket,
      currency: row.analysisCurrency,
      usesUnderlying: row.usesUnderlyingSecurity,
    });
  }

  private compareRows(
    left: MarketLeadershipRow,
    right: MarketLeadershipRow,
    column: MarketLeadershipSortColumn,
  ): number {
    const maRanks: Record<string, number> = {
      'P > 50 > 200': 6,
      'P > 200 > 50': 5,
      '50 > P > 200': 4,
      '200 > P > 50': 3,
      '50 > 200 > P': 2,
      '200 > 50 > P': 1,
    };
    const momentumRanks: Record<string, number> = {
      Accelerating: 5,
      Positive: 4,
      Neutral: 3,
      Weakening: 2,
      Declining: 1,
    };
    const signalRanks: Record<string, number> = {
      Emerging: 5,
      Leading: 4,
      Neutral: 3,
      Cooling: 2,
      Weak: 1,
    };
    switch (column) {
      case 'name':
        return (
          left.displayName.localeCompare(right.displayName) ||
          left.symbol.localeCompare(right.symbol)
        );
      case 'price':
        return left.currentPrice - right.currentPrice;
      case 'day':
        return left.dayReturnPct - right.dayReturnPct;
      case 'fiveDay':
        return left.fiveDayReturnPct - right.fiveDayReturnPct;
      case 'twentyDay':
        return left.twentyDayReturnPct - right.twentyDayReturnPct;
      case 'ma':
        return (maRanks[left.maStructure] ?? 0) - (maRanks[right.maStructure] ?? 0);
      case 'momentum':
        return (momentumRanks[left.momentumState] ?? 0) - (momentumRanks[right.momentumState] ?? 0);
      case 'structure':
        return (
          priceStructureSortRank(left.priceStructure) - priceStructureSortRank(right.priceStructure)
        );
      case 'signal':
        return (
          (signalRanks[left.leadershipSignal] ?? 0) - (signalRanks[right.leadershipSignal] ?? 0)
        );
    }
  }
}
