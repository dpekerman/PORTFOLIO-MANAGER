import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { PortfolioItem } from '../../../core/models/portfolio.models';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import { ACCOUNT_TYPES } from '../add-stock-dialog/add-stock-dialog.component';

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
  transactionType: string | null;
  accountType: string | null;
  openDate: string | null;
  closeDate: string | null;
  closingPrice: number | null;
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
    MatDatepickerModule,
    MatNativeDateModule,
  ],
})
export class EditPositionDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<EditPositionDialogComponent>);
  private readonly api = inject(PortfolioApiService);
  protected readonly data: EditPositionDialogData = inject(MAT_DIALOG_DATA);

  protected readonly sectors = signal<string[]>([]);
  protected readonly industries = signal<string[]>([]);
  protected readonly accountTypes = ACCOUNT_TYPES;

  private toDate(val: string | null | undefined): Date | null {
    if (!val) return null;
    return new Date(val.split('T')[0] + 'T12:00:00');
  }

  private formatDate(d: Date | null | undefined): string | null {
    if (!d) return null;
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  protected readonly form = this.fb.group({
    transactionType: [this.data.item.transactionType ?? 'OPEN'],
    companyName: [this.data.item.companyName, [Validators.required, Validators.maxLength(200)]],
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
    accountType: [this.data.item.accountType ?? (null as string | null)],
    openDate: [this.toDate(this.data.item.openDate) as Date | null],
    closeDate: [this.toDate(this.data.item.closeDate) as Date | null],
    closingPrice: [this.data.item.closingPrice ?? (null as number | null), [Validators.min(0)]],
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
      transactionType: this.form.value.transactionType ?? null,
      accountType: this.form.value.accountType ?? null,
      openDate: this.formatDate(this.form.value.openDate),
      closeDate: this.formatDate(this.form.value.closeDate),
      closingPrice: this.form.value.closingPrice ?? null,
    };
    this.dialogRef.close(result);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
