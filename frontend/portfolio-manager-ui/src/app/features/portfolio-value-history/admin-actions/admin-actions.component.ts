import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { AuthStateService } from '../../../core/services/auth-state.service';
import { CashStateService } from '../../../core/services/cash-state.service';
import { PortfolioValueHistoryStateService } from '../../../core/services/portfolio-value-history-state.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-admin-actions',
  templateUrl: './admin-actions.component.html',
  styleUrl: './admin-actions.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    ReactiveFormsModule,
  ],
})
export class AdminActionsComponent {
  protected readonly authState = inject(AuthStateService);
  protected readonly historyState = inject(PortfolioValueHistoryStateService);
  protected readonly cashState = inject(CashStateService);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly recalcFromDate = new FormControl<Date | null>(null);

  recordTodayNow(): void {
    this.confirm(
      {
        title: "Record today's portfolio snapshot?",
        message:
          "This will fully recompute today's Stocks, Options, Cash, and Total portfolio values using the current portfolio state and overwrite today's existing snapshot if one already exists. Continue?",
        confirmLabel: 'Record',
      },
      () => this.historyState.recordTodayNow(),
    );
  }

  reconcileToday(): void {
    this.confirm(
      {
        title: "Reconcile today's cash value?",
        message:
          "This will recalculate today's CashValue and TotalValue from the cash ledger while preserving the existing StocksValue and OptionsValue. Continue?",
        confirmLabel: 'Reconcile',
      },
      () => this.historyState.reconcileToday(),
    );
  }

  recalculateCashFromDate(): void {
    const date = this.recalcFromDate.value;
    if (!date) return;
    const iso = this.toIso(date);
    this.confirm(
      {
        title: 'Recalculate historical cash values?',
        message: `This will recalculate CashValue and TotalValue from ${this.formatDate(date)} through today. Historical StocksValue and OptionsValue will be preserved. Continue?`,
        confirmLabel: 'Recalculate',
      },
      () => this.historyState.recalculateCashFrom(iso),
    );
  }

  private confirm(data: ConfirmDialogData, onConfirmed: () => void): void {
    this.dialog
      .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
        data,
        width: '460px',
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed) => {
        if (confirmed) onConfirmed();
      });
  }

  private toIso(date: Date): string {
    return date.toISOString().slice(0, 10);
  }

  private formatDate(date: Date): string {
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }
}
