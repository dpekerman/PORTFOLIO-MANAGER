import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
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
import {
  CashFlowType,
  CashItem,
  SELECTABLE_CASH_FLOW_TYPES,
} from '../../../core/models/portfolio.models';
import { CashStateService } from '../../../core/services/cash-state.service';
import { ACCOUNT_TYPES } from '../add-stock-dialog/add-stock-dialog.component';

export interface EditCashDialogData {
  item: CashItem;
}

@Component({
  selector: 'app-edit-cash-dialog',
  templateUrl: './edit-cash-dialog.component.html',
  styleUrl: './edit-cash-dialog.component.scss',
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
  ],
})
export class EditCashDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly cashState = inject(CashStateService);
  private readonly dialogRef = inject(MatDialogRef<EditCashDialogComponent>);
  protected readonly data = inject<EditCashDialogData>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);
  protected readonly accountTypes = ACCOUNT_TYPES;
  /** OpeningBalance is migration/admin-only — never selectable, but must stay visible+pinned when
   * editing a row that already has it (backend rejects moving a row to/from OpeningBalance). */
  protected readonly isOpeningBalance = this.data.item.cashFlowType === 'OpeningBalance';
  protected readonly cashFlowTypes = this.isOpeningBalance
    ? (['OpeningBalance'] as CashFlowType[])
    : SELECTABLE_CASH_FLOW_TYPES;

  readonly form = this.fb.group({
    description: [this.data.item.description, [Validators.required, Validators.maxLength(200)]],
    amount: [
      Math.abs(this.data.item.amount) as number | null,
      [Validators.required, Validators.min(0.01)],
    ],
    cashFlowType: [
      { value: this.data.item.cashFlowType ?? null, disabled: this.isOpeningBalance },
      [Validators.required],
    ],
    accountType: [this.data.item.accountType ?? (null as string | null)],
    transactionDate: [
      this.data.item.transactionDate
        ? new Date(this.data.item.transactionDate)
        : (null as Date | null),
    ],
  });

  private formatDate(d: Date | null | undefined): string | null {
    if (!d) return null;
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  async submit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      const raw = this.form.getRawValue();
      await this.cashState.updateItem(this.data.item.id, {
        description: raw.description ?? 'CASH',
        amount: raw.amount!,
        cashFlowType: raw.cashFlowType!,
        accountType: raw.accountType ?? null,
        transactionDate: this.formatDate(raw.transactionDate),
      });
      this.dialogRef.close(true);
    } finally {
      this.saving.set(false);
    }
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}
