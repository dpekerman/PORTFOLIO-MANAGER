import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  Subject,
  catchError,
  distinctUntilChanged,
  filter,
  forkJoin,
  map,
  of,
  switchMap,
  tap,
} from 'rxjs';
import * as XLSX from 'xlsx';
import { RsiScanResult, WatchlistSummary } from '../../core/models/portfolio.models';
import { DecisionEngineService, PageDecision } from '../../core/services/decision-engine.service';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { WatchlistStateService } from '../../core/services/watchlist-state.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { WatchlistCardSkeletonComponent } from '../../shared/skeleton/watchlist-card-skeleton.component';
import {
  AddWatchlistDialogComponent,
  AddWatchlistDialogResult,
} from './add-watchlist-dialog.component';
import { WatchlistCardComponent } from './watchlist-card.component';

type ViewMode = 'card' | 'grid';
type SortColumn =
  | 'symbol'
  | 'company'
  | 'role'
  | 'price'
  | 'change'
  | 'changePct'
  | 'sector'
  | 'rsi'
  | 'trendSetup'
  | 'momentumShift'
  | 'finalAction';
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
    MatProgressBarModule,
    MatSelectModule,
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
  private readonly destroyRef = inject(DestroyRef);
  private readonly engine = inject(DecisionEngineService);

  protected readonly viewMode = signal<ViewMode>('grid');
  protected readonly filterText = signal('');
  protected readonly sortCol = signal<SortColumn>('symbol');
  protected readonly sortDir = signal<SortDir>('asc');
  protected readonly roles = ['Core', 'Strategic', 'Swing', 'Speculative', 'Options'];

  // â”€â”€ RSI result map for watchlist symbols â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  protected readonly watchlistRsiMap = signal<Map<string, RsiScanResult>>(new Map());
  private readonly _rsiLoading = signal(false);
  protected readonly rsiLoading = this._rsiLoading.asReadonly();

  /** Emits the full symbol list whenever an RSI refresh is requested. */
  private readonly rsiTrigger$ = new Subject<string[]>();

  /**
   * Sorted comma-separated symbol key — changes only when symbols are added or removed,
   * NOT when roles or quote prices update. Used to gate RSI re-scans.
   */
  private readonly _symbolKey = computed(() =>
    [...this.watchlist.items().map((w) => w.item.symbol)].sort().join(','),
  );

  constructor() {
    // Pipeline: batches symbols (max 50/request), cancels in-flight on new trigger.
    this.rsiTrigger$
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        tap((symbols) => {
          console.log(
            `[Watchlist RSI] Scan started — ${symbols.length} symbols @ ${new Date().toISOString()}`,
          );
          this._rsiLoading.set(true);
        }),
        switchMap((symbols) => {
          const batchSize = 50;
          const batches: string[][] = [];
          for (let i = 0; i < symbols.length; i += batchSize)
            batches.push(symbols.slice(i, i + batchSize));

          return forkJoin(
            batches.map((batch) =>
              this.api.analyzeSymbols(batch, 30, 75, 'Enhanced').pipe(
                catchError((err) => {
                  console.warn('[Watchlist RSI] Batch fetch failed', err);
                  return of([] as RsiScanResult[]);
                }),
              ),
            ),
          ).pipe(map((batchResults) => batchResults.flat()));
        }),
      )
      .subscribe({
        next: (results) => {
          const map = new Map<string, RsiScanResult>();
          for (const r of results) map.set(r.symbol.toUpperCase(), r);
          this.watchlistRsiMap.set(map);
          this._rsiLoading.set(false);
          console.log(
            `[Watchlist RSI] Scan complete — ${results.length} results @ ${new Date().toISOString()}`,
          );
        },
        error: () => {
          this._rsiLoading.set(false);
          console.error(`[Watchlist RSI] Scan failed @ ${new Date().toISOString()}`);
        },
      });

    // Trigger RSI only when the set of symbols actually changes (add/remove).
    // Role updates and 60s quote refreshes do NOT change _symbolKey → no spurious scans.
    toObservable(this._symbolKey)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        distinctUntilChanged(),
        filter((key) => key.length > 0),
        map((key) => key.split(',')),
      )
      .subscribe((symbols) => this.rsiTrigger$.next(symbols));
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

  protected decisionForSymbol(symbol: string, role: string | null): PageDecision | null {
    const r = this.rsiMap().get(symbol.toUpperCase());
    if (!r) return null;
    return this.engine.translateForWatchlist(r, role);
  }

  protected readonly displayedColumns: string[] = [
    'symbol',
    'company',
    'role',
    'price',
    'changePct',
    'sector',
    'rsi',
    'trendSetup',
    'momentumShift',
    'finalAction',
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
        case 'role':
          av = a.item.role ?? 'Strategic';
          bv = b.item.role ?? 'Strategic';
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
        case 'rsi':
          av = this.rsiForSymbol(a.item.symbol) ?? -1;
          bv = this.rsiForSymbol(b.item.symbol) ?? -1;
          break;
        case 'trendSetup':
          av = this.decisionForSymbol(a.item.symbol, a.item.role)?.trendSetup ?? '';
          bv = this.decisionForSymbol(b.item.symbol, b.item.role)?.trendSetup ?? '';
          break;
        case 'momentumShift':
          av = this.decisionForSymbol(a.item.symbol, a.item.role)?.momentumShift ?? '';
          bv = this.decisionForSymbol(b.item.symbol, b.item.role)?.momentumShift ?? '';
          break;
        case 'finalAction':
          av = this.decisionForSymbol(a.item.symbol, a.item.role)?.finalAction ?? '';
          bv = this.decisionForSymbol(b.item.symbol, b.item.role)?.finalAction ?? '';
          break;
        default:
          av = a.item.symbol;
          bv = b.item.symbol;
      }
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  protected roleClass(role: string): string {
    switch (role) {
      case 'Core':
        return 'role-core';
      case 'Strategic':
        return 'role-strategic';
      case 'Swing':
        return 'role-swing';
      case 'Speculative':
        return 'role-speculative';
      case 'Options':
        return 'role-options';
      default:
        return 'role-strategic';
    }
  }

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
      .subscribe((result: AddWatchlistDialogResult | null) => {
        if (result) this.watchlist.addItem(result.symbol, result.role);
      });
  }

  refresh(): void {
    this.watchlist.refresh();
    const symbols = this.watchlist.items().map((w) => w.item.symbol);
    if (symbols.length > 0) this.rsiTrigger$.next(symbols);
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

  updateRole(w: WatchlistSummary, role: string): void {
    this.watchlist.updateRole(w.item.id, role);
  }

  exportToExcel(): void {
    const today = new Date().toISOString().slice(0, 10);
    const data = this.filteredSorted().map((w) => {
      const dec = this.decisionForSymbol(w.item.symbol, w.item.role);
      return {
        Symbol: w.item.symbol,
        Company: w.quote?.companyName ?? '',
        Role: w.item.role ?? 'Strategic',
        Price: w.quote?.currentPrice ?? '',
        Change: w.quote?.change ?? '',
        'Change %': w.quote?.changePercent != null ? +w.quote.changePercent.toFixed(2) : '',
        Sector: w.quote?.sector ?? '',
        Industry: w.quote?.industry ?? '',
        'RSI (14)': this.rsiForSymbol(w.item.symbol) ?? '',
        'Trend Setup': dec?.trendSetup ?? '',
        'Momentum Shift': dec?.momentumShift ?? '',
        'Base Action': dec?.baseAction ?? '',
        'Final Action': dec?.finalAction ?? '',
        'Hover Note': dec?.hoverDescription ?? '',
      };
    });
    const ws = XLSX.utils.json_to_sheet(data);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Watchlist');
    XLSX.writeFile(wb, `watchlist-${today}.xlsx`);
  }
}
