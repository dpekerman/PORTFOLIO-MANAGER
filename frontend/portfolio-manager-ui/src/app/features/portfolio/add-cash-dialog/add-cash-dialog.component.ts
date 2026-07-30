import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { CashStateService } from '../../../core/services/cash-state.service';
import { ACCOUNT_TYPES } from '../add-stock-dialog/add-stock-dialog.component';

@Component({
  selector: 'app-add-cash-dialog',
  templateUrl: './add-cash-dialog.component.html',
  styleUrl: './add-cash-dialog.component.scss',
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
export class AddCashDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly cashState = inject(CashStateService);
  private readonly dialogRef = inject(MatDialogRef<AddCashDialogComponent>);

  protected readonly saving = signal(false);
  protected readonly accountTypes = ACCOUNT_TYPES;

  readonly form = this.fb.group({
    description: ['CASH', [Validators.required, Validators.maxLength(200)]],
    amount: [null as number | null, [Validators.required, Validators.min(0.01)]],
    accountType: [null as string | null],
    transactionDate: [null as Date | null],
  });

  private formatDate(d: Date | null | undefined): string | null {
    if (!d) return null;
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  async submit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      await this.cashState.addItem({
        description: this.form.value.description ?? 'CASH',
        amount: this.form.value.amount!,
        accountType: this.form.value.accountType ?? null,
        transactionDate: this.formatDate(this.form.value.transactionDate),
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
