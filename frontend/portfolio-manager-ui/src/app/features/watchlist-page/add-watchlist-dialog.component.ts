import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import { SymbolSearchResult } from '../../core/models/portfolio.models';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';

export interface AddWatchlistDialogResult {
  symbol: string;
  role: string;
}

export interface AddWatchlistDialogData {
  /** Symbols already on the watchlist; used to block duplicates. */
  existingSymbols: string[];
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
  private readonly snackBar = inject(MatSnackBar);
  private readonly existing = new Set(
    (inject<AddWatchlistDialogData | null>(MAT_DIALOG_DATA, { optional: true })?.existingSymbols ?? []).map(
      (s) => s.trim().toUpperCase(),
    ),
  );

  protected readonly searchResults = signal<SymbolSearchResult[]>([]);
  protected readonly searching = signal(false);
  private readonly typedSymbol = signal('');

  /** True when the entered/selected ticker is already on the watchlist. */
  protected readonly isDuplicate = computed(() => this.existing.has(this.typedSymbol().trim().toUpperCase()));

  protected isExisting(symbol: string): boolean {
    return this.existing.has(symbol.trim().toUpperCase());
  }

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
    const value = (event.target as HTMLInputElement).value;
    this.typedSymbol.set(value);
    this.searchSubject.next(value);
  }

  selectResult(result: SymbolSearchResult): void {
    this.form.patchValue({ symbol: result.symbol });
    this.typedSymbol.set(result.symbol);
    this.searchResults.set([]);
    if (this.isDuplicate()) this.warnDuplicate(result.symbol);
  }

  private warnDuplicate(symbol: string): void {
    this.snackBar.open(`${symbol.toUpperCase()} already exists on your watchlist`, 'Close', {
      duration: 4000,
    });
  }

  confirm(): void {
    if (this.form.invalid) return;
    if (this.isDuplicate()) {
      this.warnDuplicate(this.form.value.symbol!);
      return;
    }
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
