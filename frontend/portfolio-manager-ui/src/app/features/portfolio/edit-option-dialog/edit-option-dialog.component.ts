import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
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

  /** Format date string to yyyy-MM-dd for the date input */
  private toDateInputValue(dateStr: string): string {
    return dateStr.split('T')[0];
  }

  readonly form = this.fb.group({
    underlyingTicker: [
      this.data.item.underlyingTicker,
      [Validators.required, Validators.maxLength(20)],
    ],
    positionType: [this.data.item.positionType, [Validators.required]],
    expirationDate: [this.toDateInputValue(this.data.item.expirationDate), [Validators.required]],
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
      await this.optionState.updateItem(this.data.item.id, {
        underlyingTicker: this.form.value.underlyingTicker!.toUpperCase(),
        positionType: this.form.value.positionType!,
        expirationDate: this.form.value.expirationDate!,
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
