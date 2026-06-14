import { NgClass } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioSummary } from '../../core/models/portfolio.models';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { StockCardSkeletonComponent } from '../../shared/skeleton/stock-card-skeleton.component';
import { AddManualDialogComponent } from './add-manual-dialog/add-manual-dialog.component';
import { AddStockDialogComponent } from './add-stock-dialog/add-stock-dialog.component';
import { ImportStocksDialogComponent } from './import-stocks-dialog/import-stocks-dialog.component';
import { PortfolioSummaryBarComponent } from './portfolio-summary-bar/portfolio-summary-bar.component';
import { StockCardComponent } from './stock-card/stock-card.component';

type SortField =
  | 'default'
  | 'marketValue'
  | 'gainLoss'
  | 'gainLossPct'
  | 'sector'
  | 'industry'
  | 'symbol';
type SortDir = 'asc' | 'desc';

@Component({
  selector: 'app-portfolio-page',
  templateUrl: './portfolio-page.component.html',
  styleUrl: './portfolio-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgClass,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatSelectModule,
    MatTooltipModule,
    StockCardComponent,
    PortfolioSummaryBarComponent,
    StockCardSkeletonComponent,
  ],
})
export class PortfolioPageComponent {
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly scanner = inject(ScannerStateService);
  private readonly api = inject(PortfolioApiService);
  private readonly dialog = inject(MatDialog);

  /** Ghost cards displayed while portfolio loads for the first time */
  protected readonly skeletonItems = Array.from({ length: 9 }, (_, i) => i);

  protected readonly sortField = signal<SortField>('marketValue');
  protected readonly sortDir = signal<SortDir>('desc');

  /**
   * RSI map fetched directly for all portfolio symbols.
   * This covers ALL RSI values, not just oversold/overbought extremes from the scanner.
   */
  protected readonly portfolioRsiMap = signal<Map<string, number>>(new Map());

  constructor() {
    // Whenever portfolio summaries change, load RSI for ALL non-manual symbols.
    // We batch in groups of 50 (backend limit) and merge results.
    effect(() => {
      const symbols = this.portfolio
        .summaries()
        .filter((s) => !s.item.isManual)
        .map((s) => s.item.symbol);
      if (symbols.length === 0) return;

      // Split into batches of 50
      const batchSize = 50;
      const batches: string[][] = [];
      for (let i = 0; i < symbols.length; i += batchSize)
        batches.push(symbols.slice(i, i + batchSize));

      const merged = new Map<string, number>();
      let completed = 0;
      for (const batch of batches) {
        this.api.analyzeSymbols(batch, 30, 75, 'Legacy').subscribe({
          next: (results) => {
            for (const r of results) merged.set(r.symbol.toUpperCase(), r.rsi);
            completed++;
            if (completed === batches.length) this.portfolioRsiMap.set(new Map(merged));
          },
          error: (err) => {
            console.warn('Portfolio RSI batch fetch failed', err);
            completed++;
            if (completed === batches.length) this.portfolioRsiMap.set(new Map(merged));
          },
        });
      }
    });
  }

  /** Flat RSI lookup: symbol (upper) → RSI value */
  protected readonly rsiMap = computed<Map<string, number>>(() => {
    const map = new Map<string, number>();
    // Start with dedicated portfolio RSI (covers all symbols)
    for (const [sym, rsi] of this.portfolioRsiMap()) map.set(sym, rsi);
    // Overlay scanner results (same values, just ensures consistency)
    for (const r of [...this.scanner.oversold(), ...this.scanner.overbought()])
      map.set(r.symbol.toUpperCase(), r.rsi);
    return map;
  });

  protected rsiForSymbol(symbol: string): number | null {
    return this.rsiMap().get(symbol.toUpperCase()) ?? null;
  }

  protected readonly sortedSummaries = computed(() => {
    const list = [...this.portfolio.summaries()];
    const field = this.sortField();
    const dir = this.sortDir() === 'asc' ? 1 : -1;

    if (field === 'default') return list;

    return list.sort((a, b) => {
      const av = this.sortValue(a, field);
      const bv = this.sortValue(b, field);
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  private sortValue(s: PortfolioSummary, field: SortField): number | string {
    const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
    const marketValue = s.item.isManual
      ? (s.item.manualMarketValue ?? s.item.averageCostBasis)
      : price * s.item.shares;
    const cost = s.item.averageCostBasis * s.item.shares;
    switch (field) {
      case 'marketValue':
        return marketValue;
      case 'gainLoss':
        return marketValue - cost;
      case 'gainLossPct':
        return cost > 0 ? ((marketValue - cost) / cost) * 100 : 0;
      case 'sector':
        return s.item.sector ?? '';
      case 'industry':
        return s.item.industry ?? '';
      case 'symbol':
        return s.item.symbol;
      default:
        return 0;
    }
  }

  toggleSortDir(): void {
    this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
  }

  setSortField(field: SortField): void {
    if (this.sortField() === field) {
      this.toggleSortDir();
    } else {
      this.sortField.set(field);
      this.sortDir.set('desc');
    }
  }

  exportCsv(): void {
    const rows: string[][] = [
      [
        'Symbol',
        'Company',
        'Sector',
        'Industry',
        'Shares',
        'Avg Cost',
        'Current Price',
        'Market Value',
        'Gain/Loss',
        'Gain/Loss %',
        'Daily Change',
        'Daily Change %',
      ],
    ];
    for (const s of this.sortedSummaries()) {
      const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
      const marketValue = s.item.isManual
        ? (s.item.manualMarketValue ?? s.item.averageCostBasis)
        : price * s.item.shares;
      const cost = s.item.averageCostBasis * s.item.shares;
      const gainLoss = marketValue - cost;
      const gainLossPct = cost > 0 ? ((gainLoss / cost) * 100).toFixed(2) : '0';
      rows.push([
        s.item.symbol,
        s.item.companyName,
        s.item.sector ?? '',
        s.item.industry ?? '',
        s.item.shares.toString(),
        s.item.averageCostBasis.toFixed(2),
        price.toFixed(2),
        marketValue.toFixed(2),
        gainLoss.toFixed(2),
        gainLossPct,
        (s.quote?.change ?? 0).toFixed(2),
        (s.quote?.changePercent ?? 0).toFixed(2),
      ]);
    }
    this.downloadCsv(rows, 'portfolio.csv');
  }

  private downloadCsv(rows: string[][], filename: string): void {
    const csv = rows.map((r) => r.map((c) => `"${c.replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  openAddStockDialog(): void {
    this.dialog.open(AddStockDialogComponent, { width: '480px', maxWidth: '95vw' });
  }

  openAddManualDialog(): void {
    this.dialog.open(AddManualDialogComponent, { width: '480px', maxWidth: '95vw' });
  }

  openImportDialog(): void {
    this.dialog
      .open(ImportStocksDialogComponent, { width: '640px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((imported) => {
        if (imported) this.scanner.refresh(true);
      });
  }

  confirmBulkDelete(): void {
    const count = this.portfolio.selectedCount();
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Positions',
          message: `Remove all ${count} selected positions? This cannot be undone.`,
          confirmLabel: `Remove ${count}`,
          danger: true,
        },
        width: '400px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.portfolio.deleteSelectedAsync();
      });
  }

  refresh(): void {
    this.portfolio.refresh();
  }
}
