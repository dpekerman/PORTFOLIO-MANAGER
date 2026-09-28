import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
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
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import {
  MarketLeadershipRow,
  MarketLeadershipTrackerType,
  SymbolSearchResult,
} from '../../../core/models/portfolio.models';
import { MarketLeadershipStateService } from '../../../core/services/market-leadership-state.service';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';

export interface MarketTrackerDialogData {
  tracker?: MarketLeadershipRow;
}

@Component({
  selector: 'app-add-market-tracker-dialog',
  templateUrl: './add-market-tracker-dialog.component.html',
  styleUrl: './add-market-tracker-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatAutocompleteModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
})
export class AddMarketTrackerDialogComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly portfolioApi = inject(PortfolioApiService);
  protected readonly state = inject(MarketLeadershipStateService);
  protected readonly data =
    inject<MarketTrackerDialogData>(MAT_DIALOG_DATA, { optional: true }) ?? {};
  private readonly dialogRef = inject(MatDialogRef<AddMarketTrackerDialogComponent>);
  protected readonly submitted = signal(false);
  protected readonly searchResults = signal<SymbolSearchResult[]>([]);
  protected readonly searching = signal(false);
  private readonly searchSubject = new Subject<string>();
  protected readonly trackerTypes: readonly MarketLeadershipTrackerType[] = [
    'ETF',
    'Theme',
    'Future',
    'Commodity',
    'SectorProxy',
    'Other',
  ];
  protected readonly form = this.formBuilder.nonNullable.group({
    symbol: [this.data.tracker?.symbol ?? '', [Validators.required, Validators.maxLength(20)]],
    displayName: [this.data.tracker?.displayName ?? '', [Validators.maxLength(200)]],
    trackerType: [
      this.data.tracker?.trackerType ?? ('Theme' as MarketLeadershipTrackerType),
      Validators.required,
    ],
  });

  constructor() {
    this.searchSubject
      .pipe(
        takeUntilDestroyed(),
        debounceTime(350),
        distinctUntilChanged(),
        switchMap((query) => {
          if (!query) {
            this.searchResults.set([]);
            this.searching.set(false);
            return [];
          }
          this.searching.set(true);
          return this.portfolioApi.searchSymbols(query);
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

  protected onSymbolInput(event: Event): void {
    this.searchSubject.next((event.target as HTMLInputElement).value);
  }

  protected selectResult(result: SymbolSearchResult): void {
    this.form.patchValue({ symbol: result.symbol, displayName: result.description });
    this.searchResults.set([]);
  }

  protected async save(): Promise<void> {
    if (this.form.invalid) return;
    this.submitted.set(true);
    const value = this.form.getRawValue();
    const request = {
      ...value,
      displayName: value.displayName.trim() || null,
    };
    const saved = this.data.tracker
      ? await this.state.updateTracker(this.data.tracker.id, request)
      : await this.state.addTracker(request);
    this.submitted.set(false);
    if (saved) this.dialogRef.close(true);
  }
}
