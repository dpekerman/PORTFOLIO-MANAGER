import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CashItem } from '../../../core/models/portfolio.models';
import { CashStateService } from '../../../core/services/cash-state.service';

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
    ReactiveFormsModule,
  ],
})
export class EditCashDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly cashState = inject(CashStateService);
  private readonly dialogRef = inject(MatDialogRef<EditCashDialogComponent>);
  protected readonly data = inject<EditCashDialogData>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);

  readonly form = this.fb.group({
    description: [this.data.item.description, [Validators.required, Validators.maxLength(200)]],
    amount: [this.data.item.amount as number | null, [Validators.required, Validators.min(0.01)]],
  });

  async submit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      await this.cashState.updateItem(this.data.item.id, {
        description: this.form.value.description ?? 'CASH',
        amount: this.form.value.amount!,
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
