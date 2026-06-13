import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PortfolioItem } from '../../../core/models/portfolio.models';

export interface EditPositionDialogData {
  item: PortfolioItem;
}

export interface EditPositionDialogResult {
  shares: number;
  averageCostBasis: number;
  companyName: string;
}

@Component({
  selector: 'app-edit-position-dialog',
  templateUrl: './edit-position-dialog.component.html',
  styleUrl: './edit-position-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
})
export class EditPositionDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<EditPositionDialogComponent>);
  protected readonly data: EditPositionDialogData = inject(MAT_DIALOG_DATA);

  protected readonly form = this.fb.group({
    companyName: [this.data.item.companyName, [Validators.required, Validators.maxLength(100)]],
    shares: [
      this.data.item.shares,
      [Validators.required, Validators.min(0.0001), Validators.max(1_000_000)],
    ],
    averageCostBasis: [
      this.data.item.averageCostBasis,
      [Validators.required, Validators.min(0), Validators.max(1_000_000)],
    ],
  });

  save(): void {
    if (this.form.invalid) return;
    const result: EditPositionDialogResult = {
      companyName: this.form.value.companyName!,
      shares: this.form.value.shares!,
      averageCostBasis: this.form.value.averageCostBasis!,
    };
    this.dialogRef.close(result);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
