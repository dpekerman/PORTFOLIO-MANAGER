import { DatePipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { WatchlistStateService } from '../../core/services/watchlist-state.service';
import { ScannerRowSkeletonComponent } from '../../shared/skeleton/scanner-row-skeleton.component';
import { AdhocAnalyzerComponent } from './adhoc-analyzer/adhoc-analyzer.component';
import { RsiScannerTableComponent } from './rsi-scanner-table.component';

@Component({
  selector: 'app-scanner-page',
  templateUrl: './scanner-page.component.html',
  styleUrl: './scanner-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    NgClass,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    RsiScannerTableComponent,
    ScannerRowSkeletonComponent,
    AdhocAnalyzerComponent,
  ],
})
export class ScannerPageComponent implements OnInit {
  protected readonly scanner = inject(ScannerStateService);
  private readonly portfolio = inject(PortfolioStateService);
  private readonly watchlist = inject(WatchlistStateService);

  /** Lowercase symbol set for O(1) lookup in the table. */
  protected readonly portfolioSymbols = computed<ReadonlySet<string>>(
    () => new Set(this.portfolio.summaries().map((s) => s.item.symbol.toLowerCase())),
  );

  protected readonly watchlistSymbols = computed<ReadonlySet<string>>(
    () => new Set(this.watchlist.items().map((w) => w.item.symbol.toLowerCase())),
  );

  /** Computed summary for EOD CONFIRM signals found in the current scan. */
  protected readonly eodConfirmSummary = computed(() => {
    const os = this.scanner.eodConfirmOversold().length;
    const ob = this.scanner.eodConfirmOverbought().length;
    const total = os + ob;
    if (total === 0) return null;
    const parts: string[] = [];
    if (os > 0) parts.push(`${os} Oversold`);
    if (ob > 0) parts.push(`${ob} Overbought`);
    return `${total} EOD Confirm signal${total > 1 ? 's' : ''}: ${parts.join(' · ')}`;
  });

  ngOnInit(): void {
    // Refresh when navigating back to this page if data is stale (> 5 min)
    const scanned = this.scanner.scannedAt();
    if (!scanned || Date.now() - new Date(scanned).getTime() > 5 * 60 * 1000) {
      this.scanner.refresh(false);
    }
  }

  refresh(): void {
    this.scanner.refresh(true);
  }
}
