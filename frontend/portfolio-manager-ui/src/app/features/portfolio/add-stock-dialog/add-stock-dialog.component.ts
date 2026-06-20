import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import { SymbolSearchResult } from '../../../core/models/portfolio.models';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import { PortfolioStateService } from '../../../core/services/portfolio-state.service';

export const ACCOUNT_TYPES = [
  'TFSA_L_RBC',
  'TFSA_L_TD',
  'TFSA_D_TD',
  'Margin_L_TD',
  'Margin_L_RBC',
  'Margin_D_TD',
  'Corp_TD',
] as const;

@Component({
  selector: 'app-add-stock-dialog',
  templateUrl: './add-stock-dialog.component.html',
  styleUrl: './add-stock-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatAutocompleteModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    ReactiveFormsModule,
  ],
})
export class AddStockDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortfolioApiService);
  private readonly state = inject(PortfolioStateService);
  private readonly dialogRef = inject(MatDialogRef<AddStockDialogComponent>);

  protected readonly searchResults = signal<SymbolSearchResult[]>([]);
  protected readonly searching = signal(false);
  protected readonly saving = signal(false);

  protected readonly accountTypes = ACCOUNT_TYPES;
  protected readonly today = new Date();

  private readonly searchSubject = new Subject<string>();

  readonly form = this.fb.group({
    transactionType: ['OPEN', [Validators.required]],
    symbol: ['', [Validators.required, Validators.maxLength(20)]],
    companyName: ['', [Validators.required, Validators.maxLength(200)]],
    shares: [null as number | null, [Validators.required, Validators.min(0.0001)]],
    averageCostBasis: [null as number | null, [Validators.required, Validators.min(0.01)]],
    openDate: [new Date() as Date | null],
    closeDate: [new Date() as Date | null],
    closingPrice: [null as number | null, [Validators.min(0)]],
    accountType: [null as string | null],
  });

  constructor() {
    this.searchSubject
      .pipe(
        takeUntilDestroyed(),
        debounceTime(350),
        distinctUntilChanged(),
        switchMap((q) => {
          if (!q || q.length < 1) {
            this.searchResults.set([]);
            this.searching.set(false);
            return [];
          }
          this.searching.set(true);
          return this.api.searchSymbols(q);
        }),
      )
      .subscribe({
        next: (results) => {
          this.searchResults.set(results.slice(0, 8));
          this.searching.set(false);
        },
        error: () => this.searching.set(false),
      });
  }

  onSymbolInput(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.searchSubject.next(val);
  }

  selectResult(result: SymbolSearchResult): void {
    this.form.patchValue({
      symbol: result.symbol,
      companyName: result.description,
    });
    this.searchResults.set([]);
  }

  private formatDate(d: Date | null | undefined): string | null {
    if (!d) return null;
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  async submit(): Promise<void> {
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      await this.state.addItem({
        symbol: this.form.value.symbol!,
        companyName: this.form.value.companyName!,
        shares: this.form.value.shares!,
        averageCostBasis: this.form.value.averageCostBasis!,
        transactionType: this.form.value.transactionType,
        accountType: this.form.value.accountType,
        openDate: this.formatDate(this.form.value.openDate),
        closeDate: this.formatDate(this.form.value.closeDate),
        closingPrice: this.form.value.closingPrice,
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
