import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import { FinnhubSearchResult } from '../../core/models/portfolio.models';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';

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
    ReactiveFormsModule,
  ],
})
export class AddStockDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortfolioApiService);
  private readonly state = inject(PortfolioStateService);
  private readonly dialogRef = inject(MatDialogRef<AddStockDialogComponent>);

  protected readonly searchResults = signal<FinnhubSearchResult[]>([]);
  protected readonly searching = signal(false);
  protected readonly saving = signal(false);

  private readonly searchSubject = new Subject<string>();

  readonly form = this.fb.group({
    symbol: ['', [Validators.required, Validators.maxLength(20)]],
    companyName: ['', [Validators.required, Validators.maxLength(200)]],
    shares: [null as number | null, [Validators.required, Validators.min(0.0001)]],
    averageCostBasis: [null as number | null, [Validators.required, Validators.min(0.01)]],
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

  selectResult(result: FinnhubSearchResult): void {
    this.form.patchValue({
      symbol: result.symbol,
      companyName: result.description,
    });
    this.searchResults.set([]);
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
