import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AutomationStore } from '../../../../../core/store/automation.store';
import { ResponsiveTableDirective } from '../../../../../shared/responsive-table/responsive-table.directive';
import { AllocConfirmDialogComponent } from '../../../alloc-confirm-dialog.component';
import {
  automationStatusClass,
  refreshStatusLabel,
  valueScreenerStatusLabel,
} from '../automation-status';

/** Filterable, sortable table of past automation runs. */
@Component({
  selector: 'app-automation-history-card',
  templateUrl: './automation-history-card.component.html',
  styleUrl: './automation-history-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    ResponsiveTableDirective,
  ],
})
export class AutomationHistoryCardComponent {
  protected readonly automationState = inject(AutomationStore);
  private readonly dialog = inject(MatDialog);

  protected readonly statusClass = automationStatusClass;
  protected readonly refreshLabel = refreshStatusLabel;
  protected readonly valueScreenerLabel = valueScreenerStatusLabel;

  protected readonly sortCol = signal<string>('actualStartUtc');
  protected readonly sortDir = signal<'asc' | 'desc'>('desc');
  protected readonly filter = signal('');
  protected readonly columns = [
    'overallStatus',
    'tradingDate',
    'triggerType',
    'refreshStatus',
    'rsiStatus',
    'snapshotStatus',
    'valueScreenerStatus',
    'actualStartUtc',
    'errorMessage',
  ];

  protected readonly rows = computed(() => {
    const filter = this.filter().trim().toLowerCase();
    let rows = this.automationState.history();

    if (filter) {
      rows = rows.filter((r) =>
        [r.overallStatus, r.tradingDate, r.triggerType, r.errorMessage, r.errorStep]
          .filter(Boolean)
          .some((v) => v!.toLowerCase().includes(filter)),
      );
    }

    const col = this.sortCol();
    const dir = this.sortDir() === 'asc' ? 1 : -1;
    return [...rows].sort((a, b) => {
      const av = (a as unknown as Record<string, unknown>)[col];
      const bv = (b as unknown as Record<string, unknown>)[col];
      if (av == null && bv == null) return 0;
      if (av == null) return 1;
      if (bv == null) return -1;
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  protected onSortChange(sort: Sort): void {
    this.sortCol.set(sort.active || 'actualStartUtc');
    this.sortDir.set((sort.direction as 'asc' | 'desc') || 'desc');
  }

  protected clearHistory(): void {
    this.dialog
      .open(AllocConfirmDialogComponent, {
        data: {
          title: 'Clear automation run history?',
          message:
            "This permanently deletes all recorded automation run history entries. It does NOT touch portfolio snapshots, RSI signals, cash, transactions, or any other data — only this feature's own audit log.",
          confirmLabel: 'Clear History',
          confirmIcon: 'delete_sweep',
        },
        width: '400px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.automationState.clearHistory();
      });
  }
}
