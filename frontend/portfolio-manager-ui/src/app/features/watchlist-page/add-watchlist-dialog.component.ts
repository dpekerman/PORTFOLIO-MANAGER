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
import { SymbolSearchResult } from '../../core/models/portfolio.models';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';

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
    ReactiveFormsModule,
  ],
  template: `
    <h2 mat-dialog-title>Add to Watch List</h2>
    <mat-dialog-content>
      <form [formGroup]="form">
        <mat-form-field appearance="outline" class="watchlist-symbol-field">
          <mat-label>Symbol (e.g. AAPL, RY.TO)</mat-label>
          <input
            matInput
            formControlName="symbol"
            (input)="onSymbolInput($event)"
            [matAutocomplete]="auto"
            placeholder="Search symbol..."
          />
          @if (searching()) {
            <mat-progress-spinner matSuffix mode="indeterminate" diameter="20" />
          }
          <mat-autocomplete #auto (optionSelected)="selectResult($event.option.value)">
            @for (r of searchResults(); track r.symbol) {
              <mat-option [value]="r">
                <strong>{{ r.symbol }}</strong> — {{ r.description }}
              </mat-option>
            }
          </mat-autocomplete>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Cancel</button>
      <button mat-flat-button color="primary" [disabled]="form.invalid" (click)="confirm()">
        Add
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .watchlist-symbol-field {
        width: 100%;
        display: block;
      }
    `,
  ],
})
export class AddWatchlistDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PortfolioApiService);
  private readonly dialogRef = inject(MatDialogRef<AddWatchlistDialogComponent>);

  protected readonly searchResults = signal<SymbolSearchResult[]>([]);
  protected readonly searching = signal(false);

  private readonly searchSubject = new Subject<string>();

  readonly form = this.fb.group({
    symbol: ['', [Validators.required, Validators.maxLength(20)]],
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
    this.dialogRef.close(this.form.value.symbol!.toUpperCase());
  }

  cancel(): void {
    this.dialogRef.close(null);
  }
}
