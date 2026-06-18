import { CurrencyPipe } from '@angular/common';
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
import { OptionStateService } from '../../../core/services/option-state.service';

@Component({
  selector: 'app-add-option-dialog',
  templateUrl: './add-option-dialog.component.html',
  styleUrl: './add-option-dialog.component.scss',
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
export class AddOptionDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly optionState = inject(OptionStateService);
  private readonly dialogRef = inject(MatDialogRef<AddOptionDialogComponent>);

  protected readonly saving = signal(false);

  readonly form = this.fb.group({
    underlyingTicker: ['', [Validators.required, Validators.maxLength(20)]],
    positionType: ['PUT', [Validators.required]],
    expirationDate: [new Date() as Date | null, [Validators.required]],
    strike: [null as number | null, [Validators.required, Validators.min(0.01)]],
    premium: [null as number | null, [Validators.required, Validators.min(0.001)]],
    numberOfContracts: [null as number | null, [Validators.required, Validators.min(1)]],
    marketPrice: [null as number | null, [Validators.required, Validators.min(0)]],
  });

  async submit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      const d = this.form.value.expirationDate!;
      const expirationDate =
        d instanceof Date
          ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
          : (d as string);
      await this.optionState.addItem({
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
