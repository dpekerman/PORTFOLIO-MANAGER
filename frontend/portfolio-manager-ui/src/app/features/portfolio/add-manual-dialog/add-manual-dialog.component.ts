import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PortfolioStateService } from '../../../core/services/portfolio-state.service';

@Component({
  selector: 'app-add-manual-dialog',
  templateUrl: './add-manual-dialog.component.html',
  styleUrl: './add-manual-dialog.component.scss',
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
export class AddManualDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly state = inject(PortfolioStateService);
  private readonly dialogRef = inject(MatDialogRef<AddManualDialogComponent>);

  protected readonly saving = signal(false);

  readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(200)]],
    averageCost: [null as number | null, [Validators.required, Validators.min(0)]],
    marketValue: [null as number | null, [Validators.required, Validators.min(0)]],
  });

  async submit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      await this.state.addManualPosition({
        name: this.form.value.name!,
        description: this.form.value.description ?? '',
        averageCost: this.form.value.averageCost!,
        marketValue: this.form.value.marketValue!,
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
