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
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RsiScanResult, WatchlistSummary } from '../../core/models/portfolio.models';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { WatchlistStateService } from '../../core/services/watchlist-state.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { WatchlistCardSkeletonComponent } from '../../shared/skeleton/watchlist-card-skeleton.component';
import { AddWatchlistDialogComponent } from './add-watchlist-dialog.component';
import { WatchlistCardComponent } from './watchlist-card.component';

type ViewMode = 'card' | 'grid';
type SortColumn = 'symbol' | 'company' | 'price' | 'change' | 'changePct' | 'sector';
type SortDir = 'asc' | 'desc';

@Component({
  selector: 'app-watchlist-page',
  templateUrl: './watchlist-page.component.html',
  styleUrl: './watchlist-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    NgClass,
    CurrencyPipe,
    DecimalPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    WatchlistCardComponent,
    WatchlistCardSkeletonComponent,
  ],
})
export class WatchlistPageComponent {
  protected readonly watchlist = inject(WatchlistStateService);
  private readonly dialog = inject(MatDialog);
  private readonly api = inject(PortfolioApiService);
  private readonly scanner = inject(ScannerStateService);

  protected readonly viewMode = signal<ViewMode>('grid');
  protected readonly filterText = signal('');
  protected readonly sortCol = signal<SortColumn>('symbol');
  protected readonly sortDir = signal<SortDir>('asc');

  // ── RSI result map for watchlist symbols ──────────────────────────────────
  protected readonly watchlistRsiMap = signal<Map<string, RsiScanResult>>(new Map());

  constructor() {
    effect(() => {
      const symbols = this.watchlist.items().map((w) => w.item.symbol);
      if (symbols.length === 0) return;
      this.api.analyzeSymbols(symbols, 30, 75, 'Enhanced').subscribe({
        next: (results) => {
          const map = new Map<string, RsiScanResult>();
          for (const r of results) map.set(r.symbol.toUpperCase(), r);
          this.watchlistRsiMap.set(map);
        },
        error: (err) => console.warn('Watchlist RSI fetch failed', err),
      });
    });
  }

  protected readonly rsiMap = computed<Map<string, RsiScanResult>>(() => {
    const map = new Map<string, RsiScanResult>(this.watchlistRsiMap());
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

  protected readonly displayedColumns: string[] = [
    'symbol',
    'company',
    'price',
    'change',
    'changePct',
    'sector',
    'rsi',
    'momentumShift',
    'momentumAction',
    'actions',
  ];

  protected readonly filteredSorted = computed<WatchlistSummary[]>(() => {
    const filter = this.filterText().trim().toLowerCase();
    let items = this.watchlist.items();

    if (filter) {
      items = items.filter(
        (w) =>
          w.item.symbol.toLowerCase().includes(filter) ||
          (w.quote?.companyName ?? '').toLowerCase().includes(filter) ||
          (w.quote?.sector ?? '').toLowerCase().includes(filter),
      );
    }

    const col = this.sortCol();
    const dir = this.sortDir() === 'asc' ? 1 : -1;

    return [...items].sort((a, b) => {
      let av: string | number;
      let bv: string | number;
      switch (col) {
        case 'symbol':
          av = a.item.symbol;
          bv = b.item.symbol;
          break;
        case 'company':
          av = a.quote?.companyName ?? '';
          bv = b.quote?.companyName ?? '';
          break;
        case 'price':
          av = a.quote?.currentPrice ?? 0;
          bv = b.quote?.currentPrice ?? 0;
          break;
        case 'change':
          av = a.quote?.change ?? 0;
          bv = b.quote?.change ?? 0;
          break;
        case 'changePct':
          av = a.quote?.changePercent ?? 0;
          bv = b.quote?.changePercent ?? 0;
          break;
        case 'sector':
          av = a.quote?.sector ?? '';
          bv = b.quote?.sector ?? '';
          break;
        default:
          av = a.item.symbol;
          bv = b.item.symbol;
      }
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  setSort(col: SortColumn): void {
    if (this.sortCol() === col) {
      this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortCol.set(col);
      this.sortDir.set('asc');
    }
  }

  onMatSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.sortCol.set(sort.active as SortColumn);
    this.sortDir.set(sort.direction as SortDir);
  }

  openAddDialog(): void {
    this.dialog
      .open(AddWatchlistDialogComponent, { width: '420px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((symbol: string | null) => {
        if (symbol) this.watchlist.addItem(symbol);
      });
  }

  refresh(): void {
    this.watchlist.refresh();
  }

  remove(w: WatchlistSummary): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Symbol',
          message: `Remove ${w.item.symbol} from your watchlist?`,
          confirmLabel: 'Remove',
          danger: true,
        },
        width: '360px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.watchlist.deleteItem(w.item.id, w.item.symbol);
      });
  }
}
