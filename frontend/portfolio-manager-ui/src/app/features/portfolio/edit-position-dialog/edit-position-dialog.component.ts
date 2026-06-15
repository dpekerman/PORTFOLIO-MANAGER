import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { PortfolioItem } from '../../../core/models/portfolio.models';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';

export interface EditPositionDialogData {
  item: PortfolioItem;
}

export interface EditPositionDialogResult {
  shares: number;
  averageCostBasis: number;
  companyName: string;
  sector: string;
  industry: string;
  overrideSector: boolean;
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
    MatSelectModule,
    MatAutocompleteModule,
  ],
})
export class EditPositionDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<EditPositionDialogComponent>);
  private readonly api = inject(PortfolioApiService);
  protected readonly data: EditPositionDialogData = inject(MAT_DIALOG_DATA);

  protected readonly sectors = signal<string[]>([]);
  protected readonly industries = signal<string[]>([]);

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
    sector: [this.data.item.sector ?? '', [Validators.maxLength(100)]],
    industry: [this.data.item.industry ?? '', [Validators.maxLength(150)]],
  });

  ngOnInit(): void {
    this.api.getSectorIndustryLists().subscribe({
      next: (lists) => {
        this.sectors.set(lists.sectors);
        this.industries.set(lists.industries);
      },
    });
  }

  save(): void {
    if (this.form.invalid) return;
    const newSector = this.form.value.sector ?? '';
    const newIndustry = this.form.value.industry ?? '';
    // Consider it an override when the user changed sector or industry
    const sectorChanged =
      newSector !== (this.data.item.sector ?? '') ||
      newIndustry !== (this.data.item.industry ?? '');
    const result: EditPositionDialogResult = {
      companyName: this.form.value.companyName!,
      shares: this.form.value.shares!,
      averageCostBasis: this.form.value.averageCostBasis!,
      sector: newSector,
      industry: newIndustry,
      overrideSector: sectorChanged || this.data.item.sectorIsOverridden,
    };
    this.dialogRef.close(result);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
