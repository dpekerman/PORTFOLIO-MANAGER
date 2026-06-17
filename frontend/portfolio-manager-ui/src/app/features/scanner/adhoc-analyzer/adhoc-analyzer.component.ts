import { ChangeDetectionStrategy, Component, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ConfigService } from '../../../core/services/config.service';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import { ScannerStateService } from '../../../core/services/scanner-state.service';
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
export class AdhocAnalyzerComponent implements OnDestroy {
  private readonly api = inject(PortfolioApiService);
  private readonly config = inject(ConfigService);
  private readonly scannerState = inject(ScannerStateService);

  protected readonly logicMode = this.scannerState.logicMode;

  // ── State lives in the service so it survives navigation away and back ──
  protected readonly symbols = this.scannerState.adhocSymbols;
  protected readonly results = this.scannerState.adhocResults;
  protected readonly analyzed = this.scannerState.adhocAnalyzed;

  protected readonly inputValue = signal('');
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly oversoldResults = () =>
    this.results().filter((r) => r.scanType === 'Oversold');
  protected readonly overboughtResults = () =>
    this.results().filter((r) => r.scanType === 'Overbought');
  protected readonly neutralResults = () =>
    this.results().filter((r) => r.scanType !== 'Oversold' && r.scanType !== 'Overbought');

  ngOnDestroy(): void {
    this.persistSession();
  }

  private persistSession(): void {
    if (this.symbols().length === 0) return;
    this.api
      .saveAdhocSession({
        symbols: this.symbols(),
        results: this.analyzed() ? this.results() : null,
        oversoldThreshold: this.config.rsiOversoldThreshold(),
        overboughtThreshold: this.config.rsiOverboughtThreshold(),
        logicMode: this.scannerState.logicMode(),
      })
      .subscribe({ error: () => {} });
  }

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
    this.persistSession();
  }

  removeSymbol(sym: string): void {
    this.symbols.update((list) => list.filter((s) => s !== sym));
    this.results.update((list) => list.filter((r) => r.symbol.toUpperCase() !== sym.toUpperCase()));
    if (this.results().length === 0) {
      this.analyzed.set(false);
    }
    this.persistSession();
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

    const oversold = this.config.rsiOversoldThreshold();
    const overbought = this.config.rsiOverboughtThreshold();

    this.api
      .analyzeSymbols(this.symbols(), oversold, overbought, this.scannerState.logicMode())
      .subscribe({
        next: (r) => {
          this.results.set(r);
          this.loading.set(false);
          this.analyzed.set(true);
          this.persistSession();
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
