import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CashStateService } from '../../../core/services/cash-state.service';

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
    ReactiveFormsModule,
  ],
})
export class AddCashDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly cashState = inject(CashStateService);
  private readonly dialogRef = inject(MatDialogRef<AddCashDialogComponent>);

  protected readonly saving = signal(false);

  readonly form = this.fb.group({
    description: ['CASH', [Validators.required, Validators.maxLength(200)]],
    amount: [null as number | null, [Validators.required, Validators.min(0.01)]],
  });

  async submit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      await this.cashState.addItem({
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
