import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RsiScanResult } from '../../../core/models/portfolio.models';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import { ScannerRowSkeletonComponent } from '../../../shared/skeleton/scanner-row-skeleton.component';
import { RsiScannerTableComponent } from '../rsi-scanner-table.component';

@Component({
  selector: 'app-adhoc-analyzer',
  templateUrl: './adhoc-analyzer.component.html',
  styleUrl: './adhoc-analyzer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatTooltipModule,
    RsiScannerTableComponent,
    ScannerRowSkeletonComponent,
  ],
})
export class AdhocAnalyzerComponent {
  private readonly api = inject(PortfolioApiService);

  protected readonly symbols = signal<string[]>([]);
  protected readonly inputValue = signal('');
  protected readonly loading = signal(false);
  protected readonly results = signal<RsiScanResult[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly analyzed = signal(false);

  protected readonly oversoldResults = () =>
    this.results().filter((r) => r.scanType === 'Oversold');
  protected readonly overboughtResults = () =>
    this.results().filter((r) => r.scanType === 'Overbought');
  protected readonly neutralResults = () =>
    this.results().filter((r) => r.scanType !== 'Oversold' && r.scanType !== 'Overbought');

  addSymbol(value: string): void {
    const sym = value.trim().toUpperCase();
    if (!sym) return;
    if (this.symbols().includes(sym)) {
      this.inputValue.set('');
      return;
    }
    if (this.symbols().length >= 20) return;
    this.symbols.update((list) => [...list, sym]);
    this.inputValue.set('');
  }

  removeSymbol(sym: string): void {
    this.symbols.update((list) => list.filter((s) => s !== sym));
  }

  onInputKeydown(event: KeyboardEvent): void {
    const val = this.inputValue();
    if ((event.key === 'Enter' || event.key === ',') && val.trim()) {
      event.preventDefault();
      this.addSymbol(val);
    }
    // On backspace with empty input, remove last chip
    if (event.key === 'Backspace' && !val && this.symbols().length > 0) {
      this.symbols.update((list) => list.slice(0, -1));
    }
  }

  analyze(): void {
    if (this.symbols().length === 0 || this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    this.results.set([]);
    this.analyzed.set(false);

    this.api.analyzeSymbols(this.symbols()).subscribe({
      next: (r) => {
        this.results.set(r);
        this.loading.set(false);
        this.analyzed.set(true);
      },
      error: () => {
        this.error.set('Analysis failed — check symbol format (e.g. RY.TO, AAPL)');
        this.loading.set(false);
      },
    });
  }

  clear(): void {
    this.symbols.set([]);
    this.results.set([]);
    this.error.set(null);
    this.analyzed.set(false);
    this.inputValue.set('');
  }
}
