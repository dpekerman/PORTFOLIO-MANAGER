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
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import { SymbolSearchResult } from '../../core/models/portfolio.models';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';

export interface AddWatchlistDialogResult {
  symbol: string;
  role: string;
}

@Component({
  selector: 'app-add-watchlist-dialog',
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
    ReactiveFormsModule,
  ],
  templateUrl: './add-watchlist-dialog.component.html',
  styleUrl: './add-watchlist-dialog.component.scss',
})
export class AddWatchlistDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortfolioApiService);
  private readonly dialogRef = inject(MatDialogRef<AddWatchlistDialogComponent>);

  protected readonly searchResults = signal<SymbolSearchResult[]>([]);
  protected readonly searching = signal(false);

  readonly roles = ['Core', 'Strategic', 'Swing', 'Speculative', 'Options'];

  private readonly searchSubject = new Subject<string>();

  readonly form = this.fb.group({
    symbol: ['', [Validators.required, Validators.maxLength(20)]],
    role: ['Strategic', [Validators.required]],
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
    this.searchSubject.next((event.target as HTMLInputElement).value);
  }

  selectResult(result: SymbolSearchResult): void {
    this.form.patchValue({ symbol: result.symbol });
    this.searchResults.set([]);
  }

  confirm(): void {
    if (this.form.invalid) return;
    const result: AddWatchlistDialogResult = {
      symbol: this.form.value.symbol!.toUpperCase(),
      role: this.form.value.role ?? 'Strategic',
    };
    this.dialogRef.close(result);
  }

  cancel(): void {
    this.dialogRef.close(null);
  }
}
