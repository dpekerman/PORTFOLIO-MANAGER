import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { CashFlowType, SELECTABLE_CASH_FLOW_TYPES } from '../../../core/models/portfolio.models';
import { CashStateService } from '../../../core/services/cash-state.service';

export interface AdjustCashBalanceDialogData {
  accountType: string;
  currentTotal: number;
}

@Component({
  selector: 'app-adjust-cash-balance-dialog',
  templateUrl: './adjust-cash-balance-dialog.component.html',
  styleUrl: './adjust-cash-balance-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    ReactiveFormsModule,
    CurrencyPipe,
  ],
})
export class AdjustCashBalanceDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly cashState = inject(CashStateService);
  private readonly dialogRef = inject(MatDialogRef<AdjustCashBalanceDialogComponent>);
  protected readonly data = inject<AdjustCashBalanceDialogData>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly cashFlowTypes = SELECTABLE_CASH_FLOW_TYPES;

  readonly form = this.fb.group({
    desiredNewTotal: [this.data.currentTotal, [Validators.required]],
    cashFlowType: [null as CashFlowType | null, [Validators.required]],
    transactionDate: [null as Date | null],
  });

  private readonly desiredTotalSignal = toSignal(this.form.controls.desiredNewTotal.valueChanges, {
    initialValue: this.form.controls.desiredNewTotal.value,
  });

  /** Live preview of the delta that will be recorded as a new ledger row. */
  protected readonly delta = computed(
    () => (this.desiredTotalSignal() ?? this.data.currentTotal) - this.data.currentTotal,
  );

  private formatDate(d: Date | null | undefined): string | null {
    if (!d) return null;
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  async submit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.errorMessage.set(null);
    try {
      await this.cashState.adjustBalance({
        accountType: this.data.accountType,
        desiredNewTotal: this.form.value.desiredNewTotal!,
        cashFlowType: this.form.value.cashFlowType!,
        transactionDate: this.formatDate(this.form.value.transactionDate),
      });
      this.dialogRef.close(true);
    } catch (err: unknown) {
      const message = (err as { error?: string })?.error ?? 'Failed to adjust cash balance';
      this.errorMessage.set(message);
    } finally {
      this.saving.set(false);
    }
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}
