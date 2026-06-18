import { CurrencyPipe } from '@angular/common';
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
import { OptionItem } from '../../../core/models/portfolio.models';
import { OptionStateService } from '../../../core/services/option-state.service';

export interface EditOptionDialogData {
  item: OptionItem;
}

@Component({
  selector: 'app-edit-option-dialog',
  templateUrl: './edit-option-dialog.component.html',
  styleUrl: './edit-option-dialog.component.scss',
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
export class EditOptionDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly optionState = inject(OptionStateService);
  private readonly dialogRef = inject(MatDialogRef<EditOptionDialogComponent>);
  protected readonly data = inject<EditOptionDialogData>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);

  /** Parse date string to Date object for the datepicker */
  private toDate(dateStr: string): Date {
    return new Date(dateStr.split('T')[0] + 'T12:00:00');
  }

  /** Format Date object back to yyyy-MM-dd string */
  private formatDate(d: Date | string): string {
    if (typeof d === 'string') return d.split('T')[0];
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  readonly form = this.fb.group({
    underlyingTicker: [
      this.data.item.underlyingTicker,
      [Validators.required, Validators.maxLength(20)],
    ],
    positionType: [this.data.item.positionType, [Validators.required]],
    expirationDate: [
      this.toDate(this.data.item.expirationDate) as Date | null,
      [Validators.required],
    ],
    strike: [this.data.item.strike as number | null, [Validators.required, Validators.min(0.01)]],
    premium: [
      this.data.item.premium as number | null,
      [Validators.required, Validators.min(0.001)],
    ],
    numberOfContracts: [
      this.data.item.numberOfContracts as number | null,
      [Validators.required, Validators.min(1)],
    ],
    marketPrice: [
      this.data.item.marketPrice as number | null,
      [Validators.required, Validators.min(0)],
    ],
  });

  async submit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      const d = this.form.value.expirationDate!;
      const expirationDate = this.formatDate(d instanceof Date ? d : new Date(d as string));
      await this.optionState.updateItem(this.data.item.id, {
        underlyingTicker: this.form.value.underlyingTicker!.toUpperCase(),
        positionType: this.form.value.positionType!,
        expirationDate,
        strike: this.form.value.strike!,
        premium: this.form.value.premium!,
        numberOfContracts: this.form.value.numberOfContracts!,
        marketPrice: this.form.value.marketPrice!,
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
