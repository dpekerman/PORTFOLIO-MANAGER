import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioSummary, RsiScanResult } from '../../core/models/portfolio.models';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { StockCardSkeletonComponent } from '../../shared/skeleton/stock-card-skeleton.component';
import { AddManualDialogComponent } from './add-manual-dialog/add-manual-dialog.component';
import { AddStockDialogComponent } from './add-stock-dialog/add-stock-dialog.component';
import {
  EditPositionDialogComponent,
  EditPositionDialogResult,
} from './edit-position-dialog/edit-position-dialog.component';
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
type ViewMode = 'card' | 'grid';
type GridSortCol =
  | 'symbol'
  | 'company'
  | 'sector'
  | 'industry'
  | 'shares'
  | 'avgCost'
  | 'price'
  | 'marketValue'
  | 'gainLoss'
  | 'gainLossPct'
  | 'rsi'
  | 'changePct';

@Component({
  selector: 'app-portfolio-page',
  templateUrl: './portfolio-page.component.html',
  styleUrl: './portfolio-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgClass,
    FormsModule,
    CurrencyPipe,
    DecimalPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
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

  // â”€â”€ View mode â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  protected readonly viewMode = signal<ViewMode>('grid');

  // â”€â”€ Grid filters â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  protected readonly filterTicker = signal('');
  protected readonly filterSector = signal('');
  protected readonly filterIndustry = signal('');
  protected readonly filterRsiMin = signal<number | null>(null);
  protected readonly filterRsiMax = signal<number | null>(null);

  // â”€â”€ Grid sort â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  protected readonly gridSortCol = signal<GridSortCol>('marketValue');
  protected readonly gridSortDir = signal<SortDir>('desc');

  protected readonly gridDisplayedColumns: string[] = [
    'symbol',
    'company',
    'sector',
    'industry',
    'shares',
    'avgCost',
    'price',
    'changePct',
    'marketValue',
    'gainLoss',
    'gainLossPct',
    'rsi',
    'momentumShift',
    'momentumAction',
    'actions',
  ];

  /**
   * Full RsiScanResult map for all portfolio symbols (keyed by UPPER symbol).
   * Covers ALL symbols, not just oversold/overbought extremes.
   */
  protected readonly portfolioRsiResultMap = signal<Map<string, RsiScanResult>>(new Map());

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

      const merged = new Map<string, RsiScanResult>();
      let completed = 0;
      for (const batch of batches) {
        this.api.analyzeSymbols(batch, 30, 75, 'Enhanced').subscribe({
          next: (results) => {
            for (const r of results) merged.set(r.symbol.toUpperCase(), r);
            completed++;
            if (completed === batches.length) this.portfolioRsiResultMap.set(new Map(merged));
          },
          error: (err) => {
            console.warn('Portfolio RSI batch fetch failed', err);
            completed++;
            if (completed === batches.length) this.portfolioRsiResultMap.set(new Map(merged));
          },
        });
      }
    });
  }

  /** Full result map: symbol (upper) → RsiScanResult */
  protected readonly rsiMap = computed<Map<string, RsiScanResult>>(() => {
    const map = new Map<string, RsiScanResult>();
    for (const [sym, r] of this.portfolioRsiResultMap()) map.set(sym, r);
    for (const r of [...this.scanner.oversold(), ...this.scanner.overbought()])
      map.set(r.symbol.toUpperCase(), r);
    return map;
  });

  protected rsiForSymbol(symbol: string): number | null {
    return this.rsiMap().get(symbol.toUpperCase())?.rsi ?? null;
  }

  protected momentumShift(symbol: string): string {
    const r = this.rsiMap().get(symbol.toUpperCase());
    if (!r) return '—';
    const rsi = r.rsi;
    const sig = r.rsiSignal ?? rsi;
    if (rsi > 65) {
      if (r.status === 'Confirmed') return 'Active SELL Trigger';
      if (r.rsiSignalAvailable && rsi <= sig) return 'Bearish Shift';
      return 'Warning';
    }
    if (rsi < 30) {
      if (r.status === 'Confirmed') return 'Active BUY Trigger';
      if (r.rsiSignalAvailable && rsi >= sig) return 'Bullish Shift';
      return 'Warning';
    }
    if (rsi >= 55) return 'Uptrend';
    if (rsi >= 45) return 'Neutral';
    return 'Downtrend';
  }

  protected momentumAction(symbol: string): string {
    const r = this.rsiMap().get(symbol.toUpperCase());
    if (!r) return '—';
    const rsi = r.rsi;
    const sig = r.rsiSignal ?? rsi;
    if (rsi > 65) {
      if (r.status === 'Confirmed') return 'CONFIRMED SELL';
      if (r.rsiSignalAvailable && rsi <= sig) return 'EARLY WARNING';
      return 'AVOID / WAIT';
    }
    if (rsi < 30) {
      if (r.status === 'Confirmed') return 'CONFIRMED BUY';
      if (r.rsiSignalAvailable && rsi >= sig) return 'EARLY WARNING';
      return 'AVOID / WAIT';
    }
    if (rsi >= 55) return 'HOLD LONGS';
    if (rsi >= 45) return 'HANDS OFF';
    return 'STAND BY';
  }

  protected momentumShiftClass(symbol: string): string {
    const s = this.momentumShift(symbol);
    if (s === 'Active BUY Trigger') return 'ms-confirmed-buy';
    if (s === 'Bullish Shift') return 'ms-bullish';
    if (s === 'Active SELL Trigger') return 'ms-confirmed-sell';
    if (s === 'Bearish Shift') return 'ms-bearish';
    if (s === 'Warning') return 'ms-warning';
    if (s === 'Uptrend') return 'ms-uptrend';
    if (s === 'Downtrend') return 'ms-downtrend';
    return 'ms-neutral';
  }

  protected momentumActionClass(symbol: string): string {
    const a = this.momentumAction(symbol);
    if (a === 'CONFIRMED BUY') return 'ma-confirmed-buy';
    if (a === 'CONFIRMED SELL') return 'ma-confirmed-sell';
    if (a === 'EARLY WARNING') return 'ma-early-warning';
    if (a === 'AVOID / WAIT') return 'ma-avoid';
    if (a === 'HOLD LONGS') return 'ma-hold';
    return 'ma-standby';
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

  // â”€â”€ Unique filter options â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  protected readonly uniqueSectors = computed<string[]>(() => {
    const set = new Set<string>();
    for (const s of this.portfolio.summaries()) {
      const sector = s.item.sector || s.quote?.sector;
      if (sector) set.add(sector);
    }
    return [...set].sort();
  });

  protected readonly uniqueIndustries = computed<string[]>(() => {
    const set = new Set<string>();
    for (const s of this.portfolio.summaries()) {
      const ind = s.item.industry || s.quote?.industry;
      if (ind) set.add(ind);
    }
    return [...set].sort();
  });

  // â”€â”€ Grid filtered + sorted rows â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  protected readonly gridRows = computed<PortfolioSummary[]>(() => {
    const ticker = this.filterTicker().trim().toLowerCase();
    const sector = this.filterSector();
    const industry = this.filterIndustry();
    const rsiMin = this.filterRsiMin();
    const rsiMax = this.filterRsiMax();

    let rows = this.portfolio.summaries().filter((s) => {
      if (
        ticker &&
        !s.item.symbol.toLowerCase().includes(ticker) &&
        !s.item.companyName.toLowerCase().includes(ticker)
      )
        return false;
      if (sector && (s.item.sector || s.quote?.sector || '') !== sector) return false;
      if (industry && (s.item.industry || s.quote?.industry || '') !== industry) return false;
      const rsi = this.rsiForSymbol(s.item.symbol);
      if (rsiMin !== null && rsiMin !== undefined && (rsi === null || rsi < rsiMin)) return false;
      if (rsiMax !== null && rsiMax !== undefined && (rsi === null || rsi > rsiMax)) return false;
      return true;
    });

    const col = this.gridSortCol();
    const dir = this.gridSortDir() === 'asc' ? 1 : -1;

    return [...rows].sort((a, b) => {
      const av = this.gridSortValue(a, col);
      const bv = this.gridSortValue(b, col);
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  private gridSortValue(s: PortfolioSummary, col: GridSortCol): number | string {
    const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
    const mv = s.item.isManual
      ? (s.item.manualMarketValue ?? s.item.averageCostBasis)
      : price * s.item.shares;
    const cost = s.item.averageCostBasis * s.item.shares;
    const rsi = this.rsiForSymbol(s.item.symbol) ?? 0;
    switch (col) {
      case 'symbol':
        return s.item.symbol;
      case 'company':
        return s.item.companyName;
      case 'sector':
        return s.item.sector || s.quote?.sector || '';
      case 'industry':
        return s.item.industry || s.quote?.industry || '';
      case 'shares':
        return s.item.shares;
      case 'avgCost':
        return s.item.averageCostBasis;
      case 'price':
        return price;
      case 'marketValue':
        return mv;
      case 'gainLoss':
        return mv - cost;
      case 'gainLossPct':
        return cost > 0 ? ((mv - cost) / cost) * 100 : 0;
      case 'rsi':
        return rsi;
      case 'changePct':
        return s.quote?.changePercent ?? 0;
      default:
        return 0;
    }
  }

  onGridSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.gridSortCol.set(sort.active as GridSortCol);
    this.gridSortDir.set(sort.direction as SortDir);
  }

  clearGridFilters(): void {
    this.filterTicker.set('');
    this.filterSector.set('');
    this.filterIndustry.set('');
    this.filterRsiMin.set(null);
    this.filterRsiMax.set(null);
  }

  get hasActiveFilters(): boolean {
    return !!(
      this.filterTicker() ||
      this.filterSector() ||
      this.filterIndustry() ||
      this.filterRsiMin() !== null ||
      this.filterRsiMax() !== null
    );
  }

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

  openEditGridRow(s: PortfolioSummary): void {
    this.dialog
      .open(EditPositionDialogComponent, {
        data: { item: s.item },
        width: '480px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .subscribe((result: EditPositionDialogResult | undefined) => {
        if (!result) return;
        this.portfolio.updateItem(s.item.id, {
          companyName: result.companyName,
          shares: result.shares,
          averageCostBasis: result.averageCostBasis,
          sector: result.sector,
          industry: result.industry,
          overrideSector: result.overrideSector,
        });
      });
  }

  confirmDeleteGridRow(s: PortfolioSummary): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Position',
          message: `Remove ${s.item.symbol} (${s.item.companyName}) from your portfolio? This cannot be undone.`,
          confirmLabel: 'Remove',
          danger: true,
        },
        width: '400px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.portfolio.deleteItem(s.item.id, s.item.symbol);
      });
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
